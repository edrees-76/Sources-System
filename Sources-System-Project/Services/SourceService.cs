using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Sources.Data;
using Sources.Models;
using Sources.Helpers;

namespace Sources.Services;

public class SourceService : ISourceService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly IDecayCalculationService _decayService;
    private readonly IAuditService _auditService;
    private readonly IUserService _userService;

    public SourceService(IDbContextFactory<AppDbContext> dbFactory, IDecayCalculationService decayService, IAuditService auditService, IUserService userService)
    {
        _dbFactory = dbFactory;
        _decayService = decayService;
        _auditService = auditService;
        _userService = userService;
    }

    public List<Source> GetAllSources()
    {
        using var db = _dbFactory.CreateDbContext();
        return db.Sources
            .AsNoTracking()
            .AsSplitQuery()
            .Include(s => s.Radioisotope)
            .Include(s => s.InitialActivityUnit)
            .Include(s => s.CurrentActivityUnit)
            .Include(s => s.Location)
            .Include(s => s.AddedByUser)
            .Include(s => s.SourceIsotopes).ThenInclude(si => si.Radioisotope)
            .Include(s => s.SourceIsotopes).ThenInclude(si => si.ActivityUnit)
            .OrderBy(s => s.SourceCode)
            .ToList()
            .DistinctBy(s => s.Id)
            .ToList();
    }

    public Source? GetSourceById(Guid id)
    {
        using var db = _dbFactory.CreateDbContext();
        var source = db.Sources
            .AsNoTracking()
            .Include(s => s.Radioisotope)
            .Include(s => s.InitialActivityUnit)
            .Include(s => s.CurrentActivityUnit)
            .Include(s => s.Location)
            .Include(s => s.AddedByUser)
            .Include(s => s.SourceIsotopes).ThenInclude(si => si.Radioisotope)
            .Include(s => s.SourceIsotopes).ThenInclude(si => si.ActivityUnit)
            .FirstOrDefault(s => s.Id == id);

        if (source != null && (source.Status == "InUse" || source.Status == "Storage"))
        {
            var isotopesDict = db.Radioisotopes.AsNoTracking().ToDictionary(r => r.Id);
            var unitsDict = db.ActivityUnits.AsNoTracking().ToDictionary(u => u.Id);
            CalculateSourceCurrentActivityInMemory(source, isotopesDict, unitsDict);
        }

        return source;
    }

    public (bool Success, string Message) CreateSource(Source source, List<SourceIsotope>? isotopes = null)
    {
        if (source == null) return (false, "بيانات المصدر غير صالحة");
        if (!double.IsFinite(source.InitialActivityValue))
            return (false, TranslationHelper.GetString("MsgErrInvalidInitialActivityFinite") ?? "قيمة النشاط الابتدائي غير صالحة (يجب أن تكون رقماً منتهياً)");
        if (source.InitialActivityValue <= 0 && (isotopes == null || !isotopes.Any()))
            return (false, "قيمة النشاط الابتدائي يجب أن تكون أكبر من صفر");
        if (isotopes != null && isotopes.Any(si => si.InitialActivityValue.HasValue && !double.IsFinite(si.InitialActivityValue.Value)))
            return (false, TranslationHelper.GetString("MsgErrInvalidIsotopeActivityFinite") ?? "قيمة النشاط لنظير الخليط غير صالحة (يجب أن تكون رقماً منتهياً)");

        using var db = _dbFactory.CreateDbContext();
        var trimmedCode = source.SourceCode?.Trim() ?? string.Empty;
        var lowerCode = trimmedCode.ToLower();

        // 1. التحقق من وجود مصدر نشط بنفس الكود
        if (db.Sources.Any(s => s.SourceCode.ToLower() == lowerCode))
            return (false, "كود المصدر موجود بالفعل");

        // 2. التحقق من وجود مصدر محذوف بنفس الكود
        if (db.Sources.IgnoreQueryFilters().Any(s => s.IsDeleted && s.SourceCode.ToLower() == lowerCode))
            return (false, $"كود المصدر ({trimmedCode}) مستخدم لمصدر محذوف. لا يمكن إعادة استخدام كود المصدر حفاظاً على سجل التدقيق، ويمكنك استرجاع المصدر من قسم المحذوفات.");

        source.SourceCode = trimmedCode;

        var isotopesDict = db.Radioisotopes.ToDictionary(r => r.Id);
        var unitsDict = db.ActivityUnits.ToDictionary(u => u.Id);

        // إضافة النظائر المتعددة أولاً لضمان توفرها للحساب
        if (isotopes != null && isotopes.Count > 0)
        {
            source.HasDetailedIsotopes = true;
            foreach (var si in isotopes)
            {
                si.SourceId = source.Id;
                source.SourceIsotopes.Add(si);
                UpdateIsotopeActivityInMemory(si, isotopesDict, unitsDict, source.CalibrationDate, source.InitialActivityUnitId);
                db.SourceIsotopes.Add(si);
            }
        }

        // حساب النشاط الحالي
        CalculateSourceCurrentActivityInMemory(source, isotopesDict, unitsDict);

        var addedByUserId = _userService.CurrentUser?.Id;
        source.AddedBy = (addedByUserId.HasValue && db.Users.Any(u => u.Id == addedByUserId.Value))
            ? addedByUserId.Value
            : null;

        db.Sources.Add(source);
        if (source.LocationId.HasValue)
        {
            db.SourceLocationHistories.Add(new SourceLocationHistory
            {
                Id = Guid.NewGuid(),
                SourceId = source.Id,
                LocationId = source.LocationId,
                PreviousLocationId = null,
                MovedAt = DateTime.Now
            });
        }
        db.SaveChanges();
        _auditService.Log("Create", "Sources", source.Id, $"إنشاء مصدر: {source.SourceCode}");
        return (true, "تم إضافة المصدر بنجاح");
    }

    public (bool Success, string Message) UpdateSource(Source source, List<SourceIsotope>? isotopes = null)
    {
        if (source == null) return (false, "بيانات المصدر غير صالحة");
        if (!double.IsFinite(source.InitialActivityValue))
            return (false, TranslationHelper.GetString("MsgErrInvalidInitialActivityFinite") ?? "قيمة النشاط الابتدائي غير صالحة (يجب أن تكون رقماً منتهياً)");
        if (source.InitialActivityValue <= 0 && (isotopes == null || !isotopes.Any()))
            return (false, "قيمة النشاط الابتدائي يجب أن تكون أكبر من صفر");
        if (isotopes != null && isotopes.Any(si => si.InitialActivityValue.HasValue && !double.IsFinite(si.InitialActivityValue.Value)))
            return (false, TranslationHelper.GetString("MsgErrInvalidIsotopeActivityFinite") ?? "قيمة النشاط لنظير الخليط غير صالحة (يجب أن تكون رقماً منتهياً)");

        using var db = _dbFactory.CreateDbContext();
        var existing = db.Sources.Include(s => s.SourceIsotopes).FirstOrDefault(s => s.Id == source.Id);
        if (existing == null) return (false, "المصدر غير موجود");

        var trimmedCode = source.SourceCode?.Trim() ?? string.Empty;
        var lowerCode = trimmedCode.ToLower();

        // 1. التحقق من وجود مصدر نشط آخر بنفس الكود
        if (db.Sources.Any(s => s.Id != source.Id && s.SourceCode.ToLower() == lowerCode))
            return (false, "كود المصدر موجود بالفعل");

        // 2. التحقق من وجود مصدر محذوف آخر بنفس الكود
        if (db.Sources.IgnoreQueryFilters().Any(s => s.IsDeleted && s.Id != source.Id && s.SourceCode.ToLower() == lowerCode))
            return (false, $"كود المصدر ({trimmedCode}) مستخدم لمصدر محذوف. لا يمكن إعادة استخدام كود المصدر حفاظاً على سجل التدقيق، ويمكنك استرجاع المصدر من قسم المحذوفات.");

        // منع تعديل الموقع أو الحالة لمصدر قيد الاستعارة النشطة
        bool hasActiveBorrow = db.BorrowRequests.Any(b => b.SourceId == source.Id && (b.Status == "Delivered" || b.Status == "Overdue"));
        if (hasActiveBorrow && (existing.LocationId != source.LocationId || existing.Status != source.Status))
        {
            return (false, "لا يمكن تعديل الموقع أو الحالة لمصدر قيد الاستعارة النشطة حالياً");
        }

        var oldLocationId = existing.LocationId;
        var newLocationId = source.LocationId;
        if (oldLocationId != newLocationId)
        {
            db.SourceLocationHistories.Add(new SourceLocationHistory
            {
                Id = Guid.NewGuid(),
                SourceId = existing.Id,
                LocationId = newLocationId,
                PreviousLocationId = oldLocationId,
                MovedAt = DateTime.Now
            });
        }

        existing.SourceCode = trimmedCode;
        existing.RadioisotopeId = source.RadioisotopeId;
        existing.SerialNumber = source.SerialNumber;
        existing.Manufacturer = source.Manufacturer;
        existing.Model = source.Model;
        existing.InitialActivityValue = source.InitialActivityValue;
        existing.InitialActivityUnitId = source.InitialActivityUnitId;
        existing.CalibrationDate = source.CalibrationDate;
        existing.CurrentActivityUnitId = source.CurrentActivityUnitId;
        existing.LocationId = source.LocationId;
        existing.Status = source.Status;
        existing.IsSealed = source.IsSealed;
        existing.Notes = source.Notes;
        existing.ImagePath = source.ImagePath;


        var isotopesDict = db.Radioisotopes.ToDictionary(r => r.Id);
        var unitsDict = db.ActivityUnits.ToDictionary(u => u.Id);

        // تحديث النظائر المتعددة أولاً
        if (isotopes != null && isotopes.Count > 0)
        {
            existing.HasDetailedIsotopes = true;
            // حذف النظائر القديمة واستبدالها
            db.SourceIsotopes.RemoveRange(existing.SourceIsotopes);
            existing.SourceIsotopes.Clear();
            
            foreach (var si in isotopes)
            {
                si.SourceId = existing.Id;
                existing.SourceIsotopes.Add(si);
                UpdateIsotopeActivityInMemory(si, isotopesDict, unitsDict, existing.CalibrationDate, existing.InitialActivityUnitId);
                db.SourceIsotopes.Add(si);
            }
        }
        else
        {
            existing.HasDetailedIsotopes = false;
        }

        // إعادة حساب النشاط الحالي بعد تحديث النظائر
        CalculateSourceCurrentActivityInMemory(existing, isotopesDict, unitsDict);

        db.SaveChanges();
        _auditService.Log("Update", "Sources", source.Id, $"تعديل مصدر: {source.SourceCode}");
        return (true, "تم تحديث المصدر بنجاح");
    }

    public (bool Success, string Message) DeleteSource(Guid id)
    {
        using var db = _dbFactory.CreateDbContext();
        var source = db.Sources
            .Include(s => s.Radioisotope)
            .Include(s => s.Location)
            .Include(s => s.CurrentActivityUnit)
            .Include(s => s.SourceIsotopes).ThenInclude(si => si.Radioisotope)
            .FirstOrDefault(s => s.Id == id);
        if (source == null) return (false, "المصدر غير موجود");

        var pendingOrActiveBorrow = db.BorrowRequests.FirstOrDefault(b => b.SourceId == id && 
            (b.Status == "Pending" || b.Status == "Approved" || b.Status == "Delivered" || b.Status == "Overdue"));
        
        if (pendingOrActiveBorrow != null)
        {
            string statusMsg = pendingOrActiveBorrow.Status switch
            {
                "Pending" => "لوجود طلب استعارة معلّق عليه (قيد الانتظار)",
                "Approved" => "لوجود طلب استعارة معتمد عليه",
                "Delivered" => "لوجود استعارة نشطة عليه",
                "Overdue" => "لوجود استعارة نشطة عليه",
                _ => "لوجود طلب استعارة غير مكتمل عليه"
            };
            return (false, $"لا يمكن حذف المصدر {statusMsg}");
        }

        // إثراء سجل التدقيق: التقاط تفاصيل المصدر الكاملة قبل الحذف
        var oldValuesObj = new
        {
            source.SourceCode,
            Status = source.Status,
            ArabicStatus = source.ArabicStatus,
            Location = source.Location?.LocationName ?? "—",
            Isotopes = source.DisplayIsotopes,
            CurrentActivity = source.CurrentActivityWithUnit,
            SerialNumber = source.SerialNumber ?? "—",
            Manufacturer = source.Manufacturer ?? "—",
            Model = source.Model ?? "—",
            CalibrationDate = source.CalibrationDate.ToString("yyyy-MM-dd")
        };
        string oldValuesJson = System.Text.Json.JsonSerializer.Serialize(oldValuesObj);

        source.IsDeleted = true;
        source.DeletedAt = DateTime.Now;
        var currentUserId = _userService.CurrentUser?.Id;
        if (currentUserId.HasValue && db.Users.Any(u => u.Id == currentUserId.Value))
        {
            source.DeletedBy = currentUserId.Value;
        }
        else
        {
            source.DeletedBy = null;
        }
        db.SaveChanges();

        _auditService.LogWithChanges("Delete", "Sources", id, $"حذف مصدر: {source.SourceCode} (الحالة السابقة: {source.ArabicStatus})", oldValuesJson, null);
        return (true, "تم حذف المصدر بنجاح");
    }

    public List<Source> GetDeletedSources()
    {
        using var db = _dbFactory.CreateDbContext();
        return db.Sources
            .IgnoreQueryFilters()
            .AsNoTracking()
            .AsSplitQuery()
            .Include(s => s.Radioisotope)
            .Include(s => s.InitialActivityUnit)
            .Include(s => s.CurrentActivityUnit)
            .Include(s => s.Location)
            .Include(s => s.DeletedByUser)
            .Include(s => s.AddedByUser)
            .Include(s => s.SourceIsotopes).ThenInclude(si => si.Radioisotope)
            .Include(s => s.SourceIsotopes).ThenInclude(si => si.ActivityUnit)
            .Where(s => s.IsDeleted)
            .OrderByDescending(s => s.DeletedAt)
            .ThenBy(s => s.SourceCode)
            .ToList()
            .DistinctBy(s => s.Id)
            .ToList();
    }

    public (bool Success, string Message) RestoreSource(Guid id)
    {
        using var db = _dbFactory.CreateDbContext();
        var source = db.Sources
            .IgnoreQueryFilters()
            .Include(s => s.Radioisotope)
            .Include(s => s.Location)
            .Include(s => s.CurrentActivityUnit)
            .Include(s => s.SourceIsotopes).ThenInclude(si => si.Radioisotope)
            .FirstOrDefault(s => s.Id == id);

        if (source == null) return (false, "المصدر غير موجود");
        if (!source.IsDeleted) return (false, "المصدر غير محذوف أصلاً");

        // فحص الموقع: إذا كان للمصدر موقع أصلي، تحقق هل الموقع محذوف
        if (source.LocationId.HasValue)
        {
            var loc = db.Locations.IgnoreQueryFilters().FirstOrDefault(l => l.Id == source.LocationId.Value);
            if (loc != null && loc.IsDeleted)
            {
                return (false, $"لا يمكن استرجاع المصدر لأن موقعه الأصلي \"{loc.LocationName}\" محذوف حالياً. يرجى استرجاع الموقع أولاً من سجل المحذوفات ثم إعادة المحاولة.");
            }
        }

        source.IsDeleted = false;
        source.DeletedAt = null;
        source.DeletedBy = null;
        db.SaveChanges();

        var locationName = source.Location?.LocationName ?? "غير محدد";
        var statusDisplay = source.ArabicStatus;
        var msg = $"تم استرجاع المصدر {source.SourceCode} إلى موقع {locationName} بحالة {statusDisplay}";

        var newValuesObj = new
        {
            source.SourceCode,
            Status = source.Status,
            ArabicStatus = source.ArabicStatus,
            Location = locationName,
            Isotopes = source.DisplayIsotopes,
            CurrentActivity = source.CurrentActivityWithUnit
        };
        string newValuesJson = System.Text.Json.JsonSerializer.Serialize(newValuesObj);

        _auditService.LogWithChanges("Restore", "Sources", id, $"استرجاع مصدر: {source.SourceCode} إلى موقع {locationName} (الحالة: {statusDisplay})", null, newValuesJson);
        return (true, msg);
    }

    /// <summary>
    /// تحديث النشاط الحالي لجميع المصادر في قاعدة البيانات
    /// </summary>
    public void UpdateAllCurrentActivities()
    {
        using var db = _dbFactory.CreateDbContext();
        var sources = db.Sources
            .Include(s => s.Radioisotope)
            .Include(s => s.InitialActivityUnit)
            .Include(s => s.CurrentActivityUnit)
            .Include(s => s.SourceIsotopes).ThenInclude(si => si.Radioisotope)
            .Include(s => s.SourceIsotopes).ThenInclude(si => si.ActivityUnit)
            .Where(s => s.Status == "InUse" || s.Status == "Storage")
            .ToList();

        var isotopesDict = db.Radioisotopes.ToDictionary(r => r.Id);
        var unitsDict = db.ActivityUnits.ToDictionary(u => u.Id);

        foreach (var source in sources)
        {
            CalculateSourceCurrentActivityInMemory(source, isotopesDict, unitsDict);
        }
        db.SaveChanges();
    }

    public void UpdateCurrentActivity(Source source, AppDbContext db)
    {
        var isotopesDict = db.Radioisotopes.ToDictionary(r => r.Id);
        var unitsDict = db.ActivityUnits.ToDictionary(u => u.Id);
        CalculateSourceCurrentActivityInMemory(source, isotopesDict, unitsDict);
    }

    private void CalculateSourceCurrentActivityInMemory(
        Source source, 
        IReadOnlyDictionary<Guid, Radioisotope> isotopesDict, 
        IReadOnlyDictionary<Guid, ActivityUnit> unitsDict)
    {
        var currentUnit = source.CurrentActivityUnit ?? (unitsDict.TryGetValue(source.CurrentActivityUnitId, out var cu) ? cu : null);
        double curConvFactor = currentUnit?.ConversionToBq ?? 1;

        if (source.HasDetailedIsotopes && source.SourceIsotopes != null && source.SourceIsotopes.Any())
        {
            double totalCurrentBq = 0;
            double totalInitialBq = 0;
            var isotopeActivities = new List<(Radioisotope Isotope, double ActivityMBq)>();

            foreach (var si in source.SourceIsotopes)
            {
                var siIsotope = si.Radioisotope ?? (isotopesDict.TryGetValue(si.RadioisotopeId, out var ri) ? ri : null);
                Guid siUnitId = si.ActivityUnitId ?? source.InitialActivityUnitId;
                var siUnit = si.ActivityUnit ?? (unitsDict.TryGetValue(siUnitId, out var au) ? au : null);
                
                if (siIsotope == null || siUnit == null || si.InitialActivityValue == null) continue;

                var calibDate = si.CalibrationDate ?? source.CalibrationDate;
                
                var initialBq = si.InitialActivityValue.Value * siUnit.ConversionToBq;
                totalInitialBq += initialBq;

                var curBq = _decayService.CalculateCurrentActivity(initialBq, siIsotope.HalfLife, siIsotope.HalfLifeUnit, calibDate);
                si.CurrentActivityValue = _decayService.ConvertFromBq(curBq, siUnit.ConversionToBq);
                
                totalCurrentBq += curBq;
                isotopeActivities.Add((siIsotope, curBq / 1e6));
            }
            
            var initialUnit = source.InitialActivityUnit ?? (unitsDict.TryGetValue(source.InitialActivityUnitId, out var iu) ? iu : null);
            double initConvFactor = initialUnit?.ConversionToBq ?? 1;

            source.InitialActivityValue = _decayService.ConvertFromBq(totalInitialBq, initConvFactor);
            source.CurrentActivityValue = _decayService.ConvertFromBq(totalCurrentBq, curConvFactor);
            source.CurrentDoseRateResult = _decayService.CalculateDoseRateAtOneMeter(isotopeActivities);
        }
        else
        {
            // المصدر المفرد
            var isotope = source.Radioisotope ?? (isotopesDict.TryGetValue(source.RadioisotopeId, out var ri) ? ri : null);
            var initialUnit = source.InitialActivityUnit ?? (unitsDict.TryGetValue(source.InitialActivityUnitId, out var iu) ? iu : null);

            if (isotope == null || initialUnit == null || currentUnit == null) return;

            var currentBq = _decayService.CalculateCurrentActivityForSource(source, isotope, initialUnit);
            source.CurrentActivityValue = _decayService.ConvertFromBq(currentBq, currentUnit.ConversionToBq);
            source.CurrentDoseRateResult = _decayService.CalculateDoseRateAtOneMeter(new[] { (isotope, currentBq / 1e6) });
        }
    }

    /// <summary>
    /// حساب النشاط الحالي لنظير واحد داخل مصدر متعدد النظائر في الذاكرة
    /// </summary>
    private void UpdateIsotopeActivityInMemory(
        SourceIsotope si, 
        IReadOnlyDictionary<Guid, Radioisotope> isotopesDict, 
        IReadOnlyDictionary<Guid, ActivityUnit> unitsDict,
        DateTime? parentCalibrationDate = null, 
        Guid? fallbackUnitId = null)
    {
        if (si.InitialActivityValue == null || si.InitialActivityValue <= 0) return;

        var isotope = si.Radioisotope ?? (isotopesDict.TryGetValue(si.RadioisotopeId, out var ri) ? ri : null);
        var unitId = si.ActivityUnitId ?? fallbackUnitId;
        var unit = si.ActivityUnit ?? (unitId.HasValue && unitsDict.TryGetValue(unitId.Value, out var au) ? au : null);

        if (isotope == null || unit == null) return;

        var calibDate = si.CalibrationDate ?? parentCalibrationDate ?? si.Source?.CalibrationDate ?? DateTime.Now;
        var initialBq = si.InitialActivityValue.Value * unit.ConversionToBq;
        var currentBq = _decayService.CalculateCurrentActivity(initialBq, isotope.HalfLife, isotope.HalfLifeUnit, calibDate);
        si.CurrentActivityValue = _decayService.ConvertFromBq(currentBq, unit.ConversionToBq);
    }

    public int GetTotalSourcesCount()
    {
        using var db = _dbFactory.CreateDbContext();
        return db.Sources.Count();
    }

    public List<Source> GetLowActivitySources(double thresholdPercent = 10)
    {
        var sources = GetAllSources();
        return sources
            .Where(s => s.Status == "InUse" || s.Status == "Storage")
            .Where(s =>
            {
                if (s.InitialActivityValue <= 0) return false;
                var ratio = (s.CurrentActivityValue / s.InitialActivityValue) * 100;
                return ratio <= thresholdPercent;
            })
            .OrderBy(s => (s.CurrentActivityValue / s.InitialActivityValue))
            .ThenBy(s => s.SourceCode)
            .ToList();
    }

    public bool HasActiveBorrow(Guid sourceId)
    {
        using var db = _dbFactory.CreateDbContext();
        return db.BorrowRequests.Any(b => b.SourceId == sourceId && (b.Status == "Delivered" || b.Status == "Overdue"));
    }
}
