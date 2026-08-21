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
            .AsNoTracking()
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
        var source = db.Sources
            .AsNoTracking()
            .Include(s => s.Radioisotope)
            .Include(s => s.InitialActivityUnit)
            .Include(s => s.CurrentActivityUnit)
            .Include(s => s.Location)
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
        using var db = _dbFactory.CreateDbContext();
        if (db.Sources.Any(s => s.SourceCode == source.SourceCode))
            return (false, "كود المصدر موجود بالفعل");

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
            }
            
            var initialUnit = source.InitialActivityUnit ?? (unitsDict.TryGetValue(source.InitialActivityUnitId, out var iu) ? iu : null);
            double initConvFactor = initialUnit?.ConversionToBq ?? 1;

            source.InitialActivityValue = _decayService.ConvertFromBq(totalInitialBq, initConvFactor);
            source.CurrentActivityValue = _decayService.ConvertFromBq(totalCurrentBq, curConvFactor);
        }
        else
        {
            // المصدر المفرد
            var isotope = source.Radioisotope ?? (isotopesDict.TryGetValue(source.RadioisotopeId, out var ri) ? ri : null);
            var initialUnit = source.InitialActivityUnit ?? (unitsDict.TryGetValue(source.InitialActivityUnitId, out var iu) ? iu : null);

            if (isotope == null || initialUnit == null || currentUnit == null) return;

            var currentBq = _decayService.CalculateCurrentActivityForSource(source, isotope, initialUnit);
            source.CurrentActivityValue = _decayService.ConvertFromBq(currentBq, currentUnit.ConversionToBq);
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
            .ToList();
    }
}
