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

    public BorrowService(IDbContextFactory<AppDbContext> dbFactory, IAuditService auditService, IUserService userService)
    {
        _dbFactory = dbFactory;
        _auditService = auditService;
        _userService = userService;
    }

    public List<BorrowRequest> GetAll()
    {
        using var db = _dbFactory.CreateDbContext();
        return db.BorrowRequests
            .Include(b => b.Source)
            .Include(b => b.BorrowerUser)
            .Include(b => b.ApproverUser)
            .Include(b => b.ReturnedByUser)
            .OrderByDescending(b => b.RequestDate)
            .ToList();
    }

    public List<BorrowRequest> GetBySource(Guid sourceId)
    {
        using var db = _dbFactory.CreateDbContext();
        return db.BorrowRequests
            .Include(b => b.BorrowerUser)
            .Include(b => b.ApproverUser)
            .Include(b => b.ReturnedByUser)
            .Where(b => b.SourceId == sourceId)
            .OrderByDescending(b => b.RequestDate)
            .ToList();
    }

    public List<BorrowRequest> GetPending()
    {
        using var db = _dbFactory.CreateDbContext();
        return db.BorrowRequests
            .Include(b => b.Source)
            .Include(b => b.BorrowerUser)
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
            .Include(b => b.Source)
            .Include(b => b.BorrowerUser)
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
            
            // التحقق من عدم وجود استعارة نشطة لنفس المصدر
            var existingActive = db.BorrowRequests.Any(b => b.SourceId == request.SourceId && 
                (b.Status == "Delivered" || b.Status == "Overdue"));
            if (existingActive) return (false, "يوجد استعارة نشطة لهذا المصدر بالفعل.");

            // استعارة فورية: الحالة مباشرة "تم التسليم"
            request.Status = "Delivered";
            request.RequestDate = DateTime.Now;
            request.ApprovalDate = DateTime.Now;
            request.DeliveryDate = DateTime.Now;
            request.AddedBy = _userService.CurrentUser?.FullName;
            
            // نحن هنا نفترض أن المشغل الحالي قد تم تعيينه في View Model أو نتركه كمُنفّذ
            // تحديث حالة المصدر إلى "قيد الاستخدام"
            source.Status = "InUse";
            
            db.BorrowRequests.Add(request);
            db.SaveChanges();

            _auditService.Log("Create", "BorrowRequests", request.Id, $"استعارة فورية للمصدر {source.SourceCode} بواسطة {request.BorrowerName}");
            
            return (true, "تم تسجيل الاستعارة بنجاح. المصدر الآن في عهدة المستعير.");
        }
        catch (Exception ex)
        {
            return (false, $"حدث خطأ: {ex.Message} {(ex.InnerException != null ? " - " + ex.InnerException.Message : "")}");
        }
    }

    public (bool Success, string Message) ApproveRequest(Guid requestId, Guid approverId)
    {
        try
        {
            using var db = _dbFactory.CreateDbContext();
            var req = db.BorrowRequests.Include(b => b.Source).FirstOrDefault(b => b.Id == requestId);
            if (req == null) return (false, "الطلب غير موجود.");
            if (req.Status != "Pending") return (false, "الطلب ليس بانتظار الموافقة.");
            if (req.Source == null) return (false, "المصدر غير موجود.");

            req.Status = "Approved";
            req.ApproverUserId = approverId;
            req.ApprovalDate = DateTime.Now;
            
            req.Source.Status = "Borrowed";

            db.SaveChanges();

            _auditService.Log("Approve", "BorrowRequests", requestId, $"الموافقة على طلب استعارة المصدر {req.Source.SourceCode}");
            
            return (true, "تمت الموافقة على طلب الاستعارة.");
        }
        catch (Exception ex)
        {
            return (false, $"حدث خطأ: {ex.Message}");
        }
    }

    public (bool Success, string Message) RejectRequest(Guid requestId, Guid approverId, string reason)
    {
        try
        {
            using var db = _dbFactory.CreateDbContext();
            var req = db.BorrowRequests.Include(b => b.Source).FirstOrDefault(b => b.Id == requestId);
            if (req == null) return (false, "الطلب غير موجود.");
            if (req.Status != "Pending") return (false, "الطلب ليس بانتظار الموافقة.");

            req.Status = "Rejected";
            req.ApproverUserId = approverId;
            req.ApprovalDate = DateTime.Now;
            req.RejectionReason = reason;

            db.SaveChanges();

            string code = req.Source?.SourceCode ?? "غير معروف";
            _auditService.Log("Reject", "BorrowRequests", requestId, $"رفض طلب استعارة المصدر {code}");
            
            return (true, "تم رفض طلب الاستعارة.");
        }
        catch (Exception ex)
        {
            return (false, $"حدث خطأ: {ex.Message}");
        }
    }

    public (bool Success, string Message) MarkDelivered(Guid requestId)
    {
        try
        {
            using var db = _dbFactory.CreateDbContext();
            var req = db.BorrowRequests.Include(b => b.Source).FirstOrDefault(b => b.Id == requestId);
            if (req == null) return (false, "الطلب غير موجود.");
            if (req.Status != "Approved") return (false, "يجب الموافقة على الطلب أولاً قبل التسليم.");

            req.Status = "Delivered";
            req.DeliveryDate = DateTime.Now;

            db.SaveChanges();

            string code = req.Source?.SourceCode ?? "غير معروف";
            _auditService.Log("Deliver", "BorrowRequests", requestId, $"تسليم المصدر المستعار {code}");
            
            return (true, "تم تسجيل تسليم المصدر للمستعير.");
        }
        catch (Exception ex)
        {
            return (false, $"حدث خطأ: {ex.Message}");
        }
    }

    public (bool Success, string Message) MarkReturned(Guid requestId, Guid returnedByUserId)
    {
        try
        {
            using var db = _dbFactory.CreateDbContext();
            var req = db.BorrowRequests.Include(b => b.Source).FirstOrDefault(b => b.Id == requestId);
            if (req == null) return (false, "الطلب غير موجود.");
            if (req.Status != "Delivered" && req.Status != "Approved" && req.Status != "Overdue") 
                return (false, "الحالة الحالية لا تسمح بالإرجاع.");

            req.Status = "Returned";
            req.ActualReturnDate = DateTime.Now;
            req.ReturnedByUserId = returnedByUserId;

            if (req.Source != null)
            {
                req.Source.Status = "Storage";
            }

            db.SaveChanges();

            string code = req.Source?.SourceCode ?? "غير معروف";
            _auditService.Log("Return", "BorrowRequests", requestId, $"إرجاع المصدر المستعار {code}");
            
            return (true, "تم تسجيل إرجاع المصدر بنجاح وعاد ليكون نشطاً والمزامنة تمت.");
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
            var today = DateTime.Now.Date;
            
            // Any request that is delivered or approved and passed expected return date
            var overdueReqs = db.BorrowRequests
                .Where(b => (b.Status == "Delivered" || b.Status == "Approved") && b.ExpectedReturnDate.Date < today)
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
            // Logging failure gracefully, this might be triggered by a background service potentially
            Console.WriteLine($"Error checking overdue: {ex.Message}");
        }
    }
}
