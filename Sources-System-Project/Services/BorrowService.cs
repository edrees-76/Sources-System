using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Sources.Data;
using Sources.Models;

namespace Sources.Services;

public class BorrowService : IBorrowService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly IAuditService _auditService;
    private readonly IUserService _userService;
    private readonly ISystemSettingsService? _settingsService;

    public BorrowService(
        IDbContextFactory<AppDbContext> dbFactory, 
        IAuditService auditService, 
        IUserService userService,
        ISystemSettingsService? settingsService = null)
    {
        _dbFactory = dbFactory;
        _auditService = auditService;
        _userService = userService;
        _settingsService = settingsService;
    }

    public List<BorrowRequest> GetAll()
    {
        using var db = _dbFactory.CreateDbContext();
        return db.BorrowRequests
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Include(b => b.Source).ThenInclude(s => s!.Radioisotope)
            .Include(b => b.Source).ThenInclude(s => s!.InitialActivityUnit)
            .Include(b => b.Source).ThenInclude(s => s!.CurrentActivityUnit)
            .Include(b => b.Source).ThenInclude(s => s!.SourceIsotopes).ThenInclude(si => si.Radioisotope)
            .Include(b => b.Source).ThenInclude(s => s!.SourceIsotopes).ThenInclude(si => si.ActivityUnit)
            .Include(b => b.BorrowerUser)
            .Include(b => b.ApproverUser)
            .Include(b => b.ReturnedByUser)
            .Include(b => b.AddedByUser)
            .OrderByDescending(b => b.RequestDate)
            .ThenBy(b => b.Source != null ? b.Source.SourceCode : string.Empty)
            .ToList();
    }

    public List<BorrowRequest> GetBySource(Guid sourceId)
    {
        using var db = _dbFactory.CreateDbContext();
        return db.BorrowRequests
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Include(b => b.Source).ThenInclude(s => s!.Radioisotope)
            .Include(b => b.Source).ThenInclude(s => s!.InitialActivityUnit)
            .Include(b => b.Source).ThenInclude(s => s!.CurrentActivityUnit)
            .Include(b => b.Source).ThenInclude(s => s!.SourceIsotopes).ThenInclude(si => si.Radioisotope)
            .Include(b => b.Source).ThenInclude(s => s!.SourceIsotopes).ThenInclude(si => si.ActivityUnit)
            .Include(b => b.BorrowerUser)
            .Include(b => b.ApproverUser)
            .Include(b => b.ReturnedByUser)
            .Include(b => b.AddedByUser)
            .Where(b => b.SourceId == sourceId)
            .OrderByDescending(b => b.RequestDate)
            .ThenBy(b => b.Source != null ? b.Source.SourceCode : string.Empty)
            .ToList();
    }

    public List<BorrowRequest> GetPending()
    {
        using var db = _dbFactory.CreateDbContext();
        return db.BorrowRequests
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Include(b => b.Source).ThenInclude(s => s!.Radioisotope)
            .Include(b => b.Source).ThenInclude(s => s!.InitialActivityUnit)
            .Include(b => b.Source).ThenInclude(s => s!.CurrentActivityUnit)
            .Include(b => b.Source).ThenInclude(s => s!.SourceIsotopes).ThenInclude(si => si.Radioisotope)
            .Include(b => b.Source).ThenInclude(s => s!.SourceIsotopes).ThenInclude(si => si.ActivityUnit)
            .Include(b => b.BorrowerUser)
            .Include(b => b.AddedByUser)
            .Where(b => b.Status == "Pending")
            .OrderByDescending(b => b.RequestDate)
            .ToList();
    }
    
    public int GetPendingCount()
    {
        using var db = _dbFactory.CreateDbContext();
        return db.BorrowRequests.Count(b => b.Status == "Pending");
    }

    public List<BorrowRequest> GetOverdue()
    {
        using var db = _dbFactory.CreateDbContext();
        return db.BorrowRequests
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Include(b => b.Source).ThenInclude(s => s!.Radioisotope)
            .Include(b => b.Source).ThenInclude(s => s!.InitialActivityUnit)
            .Include(b => b.Source).ThenInclude(s => s!.CurrentActivityUnit)
            .Include(b => b.Source).ThenInclude(s => s!.SourceIsotopes).ThenInclude(si => si.Radioisotope)
            .Include(b => b.Source).ThenInclude(s => s!.SourceIsotopes).ThenInclude(si => si.ActivityUnit)
            .Include(b => b.BorrowerUser)
            .Include(b => b.AddedByUser)
            .Where(b => b.Status == "Overdue")
            .OrderByDescending(b => b.RequestDate)
            .ToList();
    }

    public (bool Success, string Message) CreateRequest(BorrowRequest request)
    {
        try
        {
            using var db = _dbFactory.CreateDbContext();
            
            // التحقق من أن المصدر موجود ومتاح (في المخزن فقط)
            var source = db.Sources.Find(request.SourceId);
            if (source == null) return (false, "المصدر غير موجود.");
            if (source.Status != "Storage") return (false, "المصدر غير متاح للاستعارة حالياً. يجب أن يكون في المخزن.");
            
            // التحقق من نتيجة آخر فحص تسرب للمصدر
            var latestLeakTest = db.LeakTestRecords
                .Where(r => r.SourceId == request.SourceId)
                .OrderByDescending(r => r.TestDate)
                .ThenByDescending(r => r.CreatedAt)
                .FirstOrDefault();

            if (latestLeakTest != null && latestLeakTest.Result == "Fail")
            {
                return (false, "لا يمكن استعارة هذا المصدر لأن نتيجة آخر فحص تسرب له كانت راسبة (تسرب إشعاعي مكتشف). يجب إجراء فحص جديد بنتيجة ناجحة أولاً.");
            }

            // التحقق من عدم وجود استعارة نشطة لنفس المصدر
            var existingActive = db.BorrowRequests.Any(b => b.SourceId == request.SourceId && 
                (b.Status == "Delivered" || b.Status == "Overdue"));
            if (existingActive) return (false, "يوجد استعارة نشطة لهذا المصدر بالفعل.");

            // استعارة فورية: الحالة مباشرة "تم التسليم"
            request.Status = "Delivered";
            request.RequestDate = DateTime.Now;
            request.ApprovalDate = DateTime.Now;
            request.DeliveryDate = DateTime.Now;
            var addedByUserId = _userService.CurrentUser?.Id;
            request.AddedBy = (addedByUserId.HasValue && db.Users.Any(u => u.Id == addedByUserId.Value))
                ? addedByUserId.Value
                : null;
            
            // نحن هنا نفترض أن المشغل الحالي قد تم تعيينه في View Model أو نتركه كمُنفّذ
            // تحديث حالة المصدر إلى "قيد الاستخدام"
            source.Status = "InUse";
            
            db.BorrowRequests.Add(request);
            db.SaveChanges();

            _auditService.Log("Create", "BorrowRequests", request.Id, $"استعارة فورية للمصدر {source.SourceCode} بواسطة {request.BorrowerName}");
            
            return (true, "تم تسجيل الاستعارة بنجاح. المصدر الآن في عهدة المستعير.");
        }
        catch (DbUpdateException)
        {
            return (false, "يوجد استعارة نشطة لهذا المصدر بالفعل.");
        }
        catch (Exception ex)
        {
            return (false, $"حدث خطأ: {ex.Message} {(ex.InnerException != null ? " - " + ex.InnerException.Message : "")}");
        }
    }

    public (bool Success, string Message) MarkReturned(Guid requestId, Guid returnedByUserId, DateTime actualReturnDate, string? notes = null)
    {
        try
        {
            using var db = _dbFactory.CreateDbContext();
            var req = db.BorrowRequests.Include(b => b.Source).FirstOrDefault(b => b.Id == requestId);
            if (req == null) return (false, "الطلب غير موجود.");
            if (req.Status != "Delivered" && req.Status != "Approved" && req.Status != "Overdue") 
                return (false, "الحالة الحالية لا تسمح بالإرجاع.");

            req.Status = "Returned";
            req.ActualReturnDate = actualReturnDate;
            req.ReturnedByUserId = returnedByUserId;
            if (!string.IsNullOrWhiteSpace(notes))
            {
                if (string.IsNullOrWhiteSpace(req.Notes))
                    req.Notes = notes;
                else if (!req.Notes.Contains(notes))
                    req.Notes = $"{req.Notes}\n{notes}";
            }

            if (req.Source != null)
            {
                req.Source.Status = "Storage";
            }

            db.SaveChanges();

            string code = req.Source?.SourceCode ?? "غير معروف";
            _auditService.Log("Return", "BorrowRequests", requestId, $"إرجاع المصدر المستعار {code}");
            
            return (true, "تم تسجيل إرجاع المصدر بنجاح وعاد ليكون متاحاً في المخزن.");
        }
        catch (Exception ex)
        {
            return (false, $"حدث خطأ: {ex.Message}");
        }
    }

    public void CheckAndUpdateOverdue()
    {
        try
        {
            using var db = _dbFactory.CreateDbContext();
            var today = DateTime.Today;
            
            // Any request that is delivered or approved and passed expected return date
            var overdueReqs = db.BorrowRequests
                .Where(b => (b.Status == "Delivered" || b.Status == "Approved") && b.ExpectedReturnDate < today)
                .ToList();

            if (!overdueReqs.Any()) return;

            foreach (var req in overdueReqs)
            {
                req.Status = "Overdue";
                // System notification could be added here
            }

            db.SaveChanges();
            
            if (overdueReqs.Count > 0)
            {
                _auditService.Log("System", "BorrowRequests", Guid.Empty, $"تحديث حالة {overdueReqs.Count} طلبات إلى متأخرة");
            }
        }
        catch (Exception ex)
        {
            _auditService.Log("Error", "BorrowRequests", Guid.Empty, $"خطأ أثناء فحص وتحديث الطلبات المتأخرة: {ex.Message}");
        }
    }

    public int GetDueSoonDaysThreshold()
    {
        if (_settingsService != null)
        {
            return _settingsService.GetSetting("DueSoonDaysThreshold", 7);
        }
        return 7;
    }

    public int GetDueSoonCount(IEnumerable<BorrowRequest>? requests = null)
    {
        var thresholdDays = GetDueSoonDaysThreshold();
        var today = DateTime.Today;
        var maxDate = today.AddDays(thresholdDays + 1).Date;

        if (requests != null)
        {
            return requests.Count(r => r.Status == "Delivered" 
                && r.ExpectedReturnDate.Date >= today 
                && r.ExpectedReturnDate.Date < maxDate);
        }

        using var db = _dbFactory.CreateDbContext();
        return db.BorrowRequests
            .Count(r => r.Status == "Delivered" 
                && r.ExpectedReturnDate >= today 
                && r.ExpectedReturnDate < maxDate);
    }

    public List<BorrowRequest> GetDueSoonRequests()
    {
        var thresholdDays = GetDueSoonDaysThreshold();
        var today = DateTime.Today;
        var maxDate = today.AddDays(thresholdDays + 1).Date;

        using var db = _dbFactory.CreateDbContext();
        return db.BorrowRequests
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Include(b => b.Source).ThenInclude(s => s!.Radioisotope)
            .Include(b => b.Source).ThenInclude(s => s!.InitialActivityUnit)
            .Include(b => b.Source).ThenInclude(s => s!.CurrentActivityUnit)
            .Include(b => b.Source).ThenInclude(s => s!.SourceIsotopes).ThenInclude(si => si.Radioisotope)
            .Include(b => b.Source).ThenInclude(s => s!.SourceIsotopes).ThenInclude(si => si.ActivityUnit)
            .Include(b => b.BorrowerUser)
            .Where(r => r.Status == "Delivered" 
                && r.ExpectedReturnDate >= today 
                && r.ExpectedReturnDate < maxDate)
            .OrderBy(r => r.ExpectedReturnDate)
            .ToList();
    }
}
