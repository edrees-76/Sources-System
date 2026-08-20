using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Sources.Data;
using Sources.Models;

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
            .AsSplitQuery()
            .Include(s => s.Radioisotope)
            .Include(s => s.InitialActivityUnit)
            .Include(s => s.CurrentActivityUnit)
            .Include(s => s.Location)
            .Include(s => s.SourceIsotopes).ThenInclude(si => si.Radioisotope)
            .Include(s => s.SourceIsotopes).ThenInclude(si => si.ActivityUnit)
            .OrderByDescending(s => s.CreatedAt)
            .ToList()
            .DistinctBy(s => s.Id)
            .ToList();
    }

    public Source? GetSourceById(Guid id)
    {
        using var db = _dbFactory.CreateDbContext();
        return db.Sources
            .Include(s => s.Radioisotope)
            .Include(s => s.InitialActivityUnit)
            .Include(s => s.CurrentActivityUnit)
            .Include(s => s.Location)
            .Include(s => s.SourceIsotopes).ThenInclude(si => si.Radioisotope)
            .Include(s => s.SourceIsotopes).ThenInclude(si => si.ActivityUnit)
            .FirstOrDefault(s => s.Id == id);
    }

    public (bool Success, string Message) CreateSource(Source source, List<SourceIsotope>? isotopes = null)
    {
        using var db = _dbFactory.CreateDbContext();
        if (db.Sources.Any(s => s.SourceCode == source.SourceCode))
            return (false, "كود المصدر موجود بالفعل");

        // إضافة النظائر المتعددة أولاً لضمان توفرها للحساب
        if (isotopes != null && isotopes.Count > 0)
        {
            source.HasDetailedIsotopes = true;
            foreach (var si in isotopes)
            {
                si.SourceId = source.Id;
                // إضافة النظير للمجموعة لضمان وصول UpdateCurrentActivity إليها
                source.SourceIsotopes.Add(si);
                UpdateIsotopeActivity(si, db, source.CalibrationDate, source.InitialActivityUnitId);
                db.SourceIsotopes.Add(si);
            }
        }

        // حساب النشاط الحالي (سيعمل الآن على النظائر المرتبطة)
        UpdateCurrentActivity(source, db);

        source.AddedBy = _userService.CurrentUser?.FullName ?? "غير معروف";

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
        using var db = _dbFactory.CreateDbContext();
        var existing = db.Sources.Include(s => s.SourceIsotopes).FirstOrDefault(s => s.Id == source.Id);
        if (existing == null) return (false, "المصدر غير موجود");

        if (db.Sources.Any(s => s.Id != source.Id && s.SourceCode == source.SourceCode))
            return (false, "كود المصدر موجود بالفعل");

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

        existing.SourceCode = source.SourceCode;
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
        existing.Notes = source.Notes;
        existing.ImagePath = source.ImagePath;

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
                UpdateIsotopeActivity(si, db, existing.CalibrationDate, existing.InitialActivityUnitId);
                db.SourceIsotopes.Add(si);
            }
        }
        else
        {
            existing.HasDetailedIsotopes = false;
        }

        // إعادة حساب النشاط الحالي بعد تحديث النظائر
        UpdateCurrentActivity(existing, db);

        db.SaveChanges();
        _auditService.Log("Update", "Sources", source.Id, $"تعديل مصدر: {source.SourceCode}");
        return (true, "تم تحديث المصدر بنجاح");
    }

    public (bool Success, string Message) DeleteSource(Guid id)
    {
        using var db = _dbFactory.CreateDbContext();
        var source = db.Sources.Find(id);
        if (source == null) return (false, "المصدر غير موجود");

        bool hasActiveBorrow = db.BorrowRequests.Any(b => b.SourceId == id && (b.Status == "Delivered" || b.Status == "Overdue"));
        if (hasActiveBorrow)
            return (false, "لا يمكن حذف المصدر لوجود استعارة نشطة عليه");

        source.IsDeleted = true;
        db.SaveChanges();
        _auditService.Log("Delete", "Sources", id, $"حذف مصدر: {source.SourceCode}");
        return (true, "تم حذف المصدر بنجاح");
    }

    /// <summary>
    /// تحديث النشاط الحالي لجميع المصادر
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

        foreach (var source in sources)
        {
            // الطريقة الموحدة الآن تعالج كلاً من المصدر ونظائره الفردية بدقة
            UpdateCurrentActivity(source, db);
        }
        db.SaveChanges();
    }

    private void UpdateCurrentActivity(Source source, AppDbContext db)
    {
        var currentUnit = source.CurrentActivityUnit ?? db.ActivityUnits.Find(source.CurrentActivityUnitId);
        // التراجع للبيكرل إذا لم تكن وحدة العرض محددة لمتابعة الحساب
        double curConvFactor = currentUnit?.ConversionToBq ?? 1;

        if (source.HasDetailedIsotopes)
        {
            // التأكد من تحميل النظائر المتعددة إذا لم تكن محملة
            if (source.SourceIsotopes == null || !source.SourceIsotopes.Any())
            {
                db.Entry(source).Collection(s => s.SourceIsotopes).Load();
                foreach (var si in source.SourceIsotopes!)
                {
                    db.Entry(si).Reference(x => x.Radioisotope).Load();
                    db.Entry(si).Reference(x => x.ActivityUnit).Load();
                }
            }

            if (source.SourceIsotopes != null && source.SourceIsotopes.Any())
            {
                // المصادر المتعددة: مجموع النشاط الابتدائي والحالي لكل النظائر
                double totalCurrentBq = 0;
                double totalInitialBq = 0;

                foreach (var si in source.SourceIsotopes)
                {
                    var siIsotope = si.Radioisotope ?? db.Radioisotopes.Find(si.RadioisotopeId);
                    // التراجع لوحدة المصدر إذا لم تكن وحدة النظير محددة
                    var siUnitId = si.ActivityUnitId ?? source.InitialActivityUnitId;
                    var siUnit = si.ActivityUnit ?? db.ActivityUnits.Find(siUnitId);
                    
                    if (siIsotope == null || siUnit == null || si.InitialActivityValue == null) continue;

                    var calibDate = si.CalibrationDate ?? source.CalibrationDate;
                    
                    // حساب النشاط الابتدائي بالـ Bq للتجميع
                    var initialBq = si.InitialActivityValue.Value * siUnit.ConversionToBq;
                    totalInitialBq += initialBq;

                    // حساب النشاط الحالي بالـ Bq
                    var curBq = _decayService.CalculateCurrentActivity(initialBq, siIsotope.HalfLife, siIsotope.HalfLifeUnit, calibDate);
                    
                    // تحديث القيمة الحالية للنظير الفردي (هذا ما يظهر في نافذة التفاصيل)
                    si.CurrentActivityValue = _decayService.ConvertFromBq(curBq, siUnit.ConversionToBq);
                    
                    totalCurrentBq += curBq;
                }
                
                // تحديث قيم المصدر الأساسية (للجدول والتقارير)
                // معامل تحويل النشاط الابتدائي: استخدام وحدة المصدر المحددة أو الافتراضي Bq
                var initialUnit = source.InitialActivityUnit ?? db.ActivityUnits.Find(source.InitialActivityUnitId);
                double initConvFactor = initialUnit?.ConversionToBq ?? 1;

                source.InitialActivityValue = _decayService.ConvertFromBq(totalInitialBq, initConvFactor);
                source.CurrentActivityValue = _decayService.ConvertFromBq(totalCurrentBq, curConvFactor);
            }
        }
        else
        {
            // المصدر المفرد
            var isotope = source.Radioisotope ?? db.Radioisotopes.Find(source.RadioisotopeId);
            var initialUnit = source.InitialActivityUnit ?? db.ActivityUnits.Find(source.InitialActivityUnitId);

            if (isotope == null || initialUnit == null || currentUnit == null) return;

            var currentBq = _decayService.CalculateCurrentActivityForSource(source, isotope, initialUnit);
            source.CurrentActivityValue = _decayService.ConvertFromBq(currentBq, currentUnit.ConversionToBq);
        }
    }

    /// <summary>
    /// حساب النشاط الحالي لنظير واحد داخل مصدر متعدد النظائر
    /// </summary>
    private void UpdateIsotopeActivity(SourceIsotope si, AppDbContext db, DateTime? parentCalibrationDate = null, Guid? fallbackUnitId = null)
    {
        if (si.InitialActivityValue == null || si.InitialActivityValue <= 0) return;

        var isotope = si.Radioisotope ?? db.Radioisotopes.Find(si.RadioisotopeId);
        var unitId = si.ActivityUnitId ?? fallbackUnitId;
        var unit = si.ActivityUnit ?? (unitId != null ? db.ActivityUnits.Find(unitId) : null);

        if (isotope == null || unit == null) return;

        // استخدام تاريخ المعايرة الخاص بالنظير أو تاريخ المصدر أو التاريخ الحالي كملاذ أخير
        var calibDate = si.CalibrationDate ?? parentCalibrationDate ?? si.Source?.CalibrationDate ?? DateTime.Now;

        // تحويل النشاط الابتدائي إلى Bq
        var initialBq = si.InitialActivityValue.Value * unit.ConversionToBq;

        // حساب النشاط الحالي بالـ Bq باستخدام محرك الاضمحلال
        var currentBq = _decayService.CalculateCurrentActivity(initialBq, isotope.HalfLife, isotope.HalfLifeUnit, calibDate);

        // التحويل من Bq إلى وحدة العرض
        si.CurrentActivityValue = _decayService.ConvertFromBq(currentBq, unit.ConversionToBq);
    }



    public int GetTotalSourcesCount()
    {
        using var db = _dbFactory.CreateDbContext();
        return db.Sources.Count();
    }

    public List<Source> GetLowActivitySources(double thresholdPercent = 10)
    {
        using var db = _dbFactory.CreateDbContext();
        return db.Sources
            .AsSplitQuery()
            .Include(s => s.Radioisotope)
            .Include(s => s.InitialActivityUnit)
            .Include(s => s.CurrentActivityUnit)
            .Include(s => s.Location)
            .Include(s => s.SourceIsotopes).ThenInclude(si => si.Radioisotope)
            .Where(s => s.Status == "InUse" || s.Status == "Storage")
            .ToList()
            .DistinctBy(s => s.Id)
            .Where(s =>
            {
                if (s.InitialActivityValue <= 0) return false;
                var ratio = (s.CurrentActivityValue / s.InitialActivityValue) * 100;
                return ratio <= thresholdPercent;
            })
            .ToList();
    }
}
