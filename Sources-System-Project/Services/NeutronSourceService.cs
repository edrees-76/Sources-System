using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Sources.Data;
using Sources.Models;
using Sources.Helpers;

namespace Sources.Services;

/// <summary>
/// خدمة إدارة المصادر النيترونية
/// </summary>
public class NeutronSourceService : INeutronSourceService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly IAuditService _auditService;
    private readonly IUserService _userService;

    public NeutronSourceService(IDbContextFactory<AppDbContext> dbFactory, IAuditService auditService, IUserService userService)
    {
        _dbFactory = dbFactory;
        _auditService = auditService;
        _userService = userService;
    }

    /// <summary>جلب جميع المصادر النيترونية النشطة</summary>
    public List<NeutronSource> GetAll()
    {
        using var db = _dbFactory.CreateDbContext();
        return db.NeutronSources
            .AsNoTracking()
            .Include(n => n.NeutronSourceType)
            .Include(n => n.Location)
            .Include(n => n.AddedByUser)
            .OrderBy(n => n.SourceCode)
            .ToList();
    }

    /// <summary>جلب المصادر النيترونية المحذوفة</summary>
    public List<NeutronSource> GetDeleted()
    {
        using var db = _dbFactory.CreateDbContext();
        return db.NeutronSources
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Include(n => n.NeutronSourceType)
            .Include(n => n.Location)
            .Include(n => n.DeletedByUser)
            .Include(n => n.AddedByUser)
            .Where(n => n.IsDeleted)
            .OrderByDescending(n => n.DeletedAt)
            .ToList();
    }

    /// <summary>جلب مصدر نيتروني بالمعرف</summary>
    public NeutronSource? GetById(Guid id)
    {
        using var db = _dbFactory.CreateDbContext();
        return db.NeutronSources
            .Include(n => n.NeutronSourceType)
            .Include(n => n.Location)
            .Include(n => n.AddedByUser)
            .FirstOrDefault(n => n.Id == id);
    }

    /// <summary>جلب مصدر نيتروني بالكود</summary>
    public NeutronSource? GetByCode(string sourceCode)
    {
        if (string.IsNullOrWhiteSpace(sourceCode)) return null;
        using var db = _dbFactory.CreateDbContext();
        var lowerCode = sourceCode.Trim().ToLower();
        return db.NeutronSources
            .Include(n => n.NeutronSourceType)
            .Include(n => n.Location)
            .Include(n => n.AddedByUser)
            .FirstOrDefault(n => n.SourceCode.ToLower() == lowerCode);
    }

    /// <summary>جلب المصادر النيترونية حسب الموقع</summary>
    public List<NeutronSource> GetByLocation(Guid locationId)
    {
        using var db = _dbFactory.CreateDbContext();
        return db.NeutronSources
            .AsNoTracking()
            .Include(n => n.NeutronSourceType)
            .Include(n => n.Location)
            .Include(n => n.AddedByUser)
            .Where(n => n.LocationId == locationId)
            .OrderBy(n => n.SourceCode)
            .ToList();
    }

    /// <summary>عدد المصادر النيترونية الكلي</summary>
    public int GetTotalCount()
    {
        using var db = _dbFactory.CreateDbContext();
        return db.NeutronSources.Count();
    }

    /// <summary>إنشاء مصدر نيتروني جديد</summary>
    public (bool Success, string Message) Create(NeutronSource item)
    {
        if (item == null) return (false, "بيانات المصدر النيتروني غير صالحة");
        if (string.IsNullOrWhiteSpace(item.SourceCode)) return (false, "كود المصدر مطلوب");
        if (!double.IsFinite(item.CalibratedEmissionRate))
            return (false, TranslationHelper.GetString("MsgErrInvalidEmissionRateFinite") ?? "معدل انبعاث النيترونات غير صالح (يجب أن يكون رقماً منتهياً)");
        if (item.CalibratedEmissionRate <= 0) return (false, "معدل انبعاث النيترونات يجب أن يكون أكبر من صفر");
        if (item.AnisotropyFactor.HasValue && !double.IsFinite(item.AnisotropyFactor.Value))
            return (false, TranslationHelper.GetString("MsgErrInvalidAnisotropyFactorFinite") ?? "معامل اللاتماثل الزاوي غير صالح (يجب أن يكون رقماً منتهياً)");
        if (item.RelativeExpandedUncertaintyPercent.HasValue && !double.IsFinite(item.RelativeExpandedUncertaintyPercent.Value))
            return (false, TranslationHelper.GetString("MsgErrInvalidUncertaintyFinite") ?? "نسبة عدم اليقين غير صالحة (يجب أن تكون رقماً منتهياً)");
        if (item.CalibrationDate.HasValue && item.CalibrationDate.Value.Date > DateTime.Today)
            return (false, TranslationHelper.GetString("MsgErrCalibrationDateFuture") ?? "لا يمكن أن يكون تاريخ المعايرة في المستقبل.");
        if (item.EmissionCalibrationDate.HasValue && item.EmissionCalibrationDate.Value.Date > DateTime.Today)
            return (false, TranslationHelper.GetString("MsgErrEmissionCalibrationDateFuture") ?? "تاريخ معايرة الانبعاث لا يمكن أن يكون في المستقبل");
        if (item.NeutronSourceTypeId == Guid.Empty) return (false, "نوع المصدر النيتروني مطلوب");

        using var db = _dbFactory.CreateDbContext();
        var trimmedCode = item.SourceCode.Trim();
        var lowerCode = trimmedCode.ToLower();

        if (db.NeutronSources.Any(n => n.SourceCode.ToLower() == lowerCode))
            return (false, "كود المصدر موجود بالفعل");

        if (!db.NeutronSourceTypes.Any(t => t.Id == item.NeutronSourceTypeId))
            return (false, "نوع المصدر النيتروني المحدد غير موجود");

        if (item.LocationId.HasValue && !db.Locations.Any(l => l.Id == item.LocationId.Value))
            return (false, "الموقع المحدد غير موجود");

        item.SourceCode = trimmedCode;
        item.SerialNumber = item.SerialNumber?.Trim();
        item.Status = string.IsNullOrWhiteSpace(item.Status) ? "Storage" : item.Status.Trim();
        var addedByUserId = _userService.CurrentUser?.Id;
        item.AddedBy = (addedByUserId.HasValue && db.Users.Any(u => u.Id == addedByUserId.Value))
            ? addedByUserId.Value
            : null;
        item.CreatedAt = DateTime.Now;

        db.NeutronSources.Add(item);
        db.SaveChanges();

        var newValuesObj = new
        {
            item.SourceCode,
            item.SerialNumber,
            item.NeutronSourceTypeId,
            item.LocationId,
            item.CalibratedEmissionRate,
            item.RelativeExpandedUncertaintyPercent,
            CalibrationDate = item.CalibrationDate?.ToString("yyyy-MM-dd"),
            EmissionCalibrationDate = item.EmissionCalibrationDate?.ToString("yyyy-MM-dd"),
            item.CalibrationReference,
            item.AnisotropyFactor,
            item.Status,
            item.Notes
        };
        _auditService.LogWithChanges("Create", "NeutronSources", item.Id, $"إضافة مصدر نيتروني: {item.SourceCode}", oldValues: null, newValues: System.Text.Json.JsonSerializer.Serialize(newValuesObj));
        return (true, "تم إضافة المصدر النيتروني بنجاح");
    }

    /// <summary>تحديث مصدر نيتروني</summary>
    public (bool Success, string Message) Update(NeutronSource item)
    {
        if (item == null) return (false, "بيانات المصدر النيتروني غير صالحة");
        if (string.IsNullOrWhiteSpace(item.SourceCode)) return (false, "كود المصدر مطلوب");
        if (!double.IsFinite(item.CalibratedEmissionRate))
            return (false, TranslationHelper.GetString("MsgErrInvalidEmissionRateFinite") ?? "معدل انبعاث النيترونات غير صالح (يجب أن يكون رقماً منتهياً)");
        if (item.CalibratedEmissionRate <= 0) return (false, "معدل انبعاث النيترونات يجب أن يكون أكبر من صفر");
        if (item.AnisotropyFactor.HasValue && !double.IsFinite(item.AnisotropyFactor.Value))
            return (false, TranslationHelper.GetString("MsgErrInvalidAnisotropyFactorFinite") ?? "معامل اللاتماثل الزاوي غير صالح (يجب أن يكون رقماً منتهياً)");
        if (item.RelativeExpandedUncertaintyPercent.HasValue && !double.IsFinite(item.RelativeExpandedUncertaintyPercent.Value))
            return (false, TranslationHelper.GetString("MsgErrInvalidUncertaintyFinite") ?? "نسبة عدم اليقين غير صالحة (يجب أن تكون رقماً منتهياً)");
        if (item.CalibrationDate.HasValue && item.CalibrationDate.Value.Date > DateTime.Today)
            return (false, TranslationHelper.GetString("MsgErrCalibrationDateFuture") ?? "لا يمكن أن يكون تاريخ المعايرة في المستقبل.");
        if (item.EmissionCalibrationDate.HasValue && item.EmissionCalibrationDate.Value.Date > DateTime.Today)
            return (false, TranslationHelper.GetString("MsgErrEmissionCalibrationDateFuture") ?? "تاريخ معايرة الانبعاث لا يمكن أن يكون في المستقبل");
        if (item.NeutronSourceTypeId == Guid.Empty) return (false, "نوع المصدر النيتروني مطلوب");

        using var db = _dbFactory.CreateDbContext();
        var existing = db.NeutronSources.Find(item.Id);
        if (existing == null) return (false, "المصدر النيتروني غير موجود");

        var oldValuesObj = new
        {
            existing.SourceCode,
            existing.SerialNumber,
            existing.NeutronSourceTypeId,
            existing.LocationId,
            existing.CalibratedEmissionRate,
            existing.RelativeExpandedUncertaintyPercent,
            CalibrationDate = existing.CalibrationDate?.ToString("yyyy-MM-dd"),
            EmissionCalibrationDate = existing.EmissionCalibrationDate?.ToString("yyyy-MM-dd"),
            existing.CalibrationReference,
            existing.AnisotropyFactor,
            existing.Status,
            existing.Notes
        };
        string oldValuesJson = System.Text.Json.JsonSerializer.Serialize(oldValuesObj);

        var trimmedCode = item.SourceCode.Trim();
        var lowerCode = trimmedCode.ToLower();

        if (db.NeutronSources.Any(n => n.Id != item.Id && n.SourceCode.ToLower() == lowerCode))
            return (false, "كود المصدر موجود بالفعل");

        if (!db.NeutronSourceTypes.Any(t => t.Id == item.NeutronSourceTypeId))
            return (false, "نوع المصدر النيتروني المحدد غير موجود");

        if (item.LocationId.HasValue && !db.Locations.Any(l => l.Id == item.LocationId.Value))
            return (false, "الموقع المحدد غير موجود");

        existing.SourceCode = trimmedCode;
        existing.SerialNumber = item.SerialNumber?.Trim();
        existing.NeutronSourceTypeId = item.NeutronSourceTypeId;
        existing.LocationId = item.LocationId;
        existing.CalibratedEmissionRate = item.CalibratedEmissionRate;
        existing.RelativeExpandedUncertaintyPercent = item.RelativeExpandedUncertaintyPercent;
        existing.CalibrationDate = item.CalibrationDate;
        existing.EmissionCalibrationDate = item.EmissionCalibrationDate;
        existing.CalibrationReference = item.CalibrationReference;
        existing.AnisotropyFactor = item.AnisotropyFactor;
        existing.Status = string.IsNullOrWhiteSpace(item.Status) ? "Storage" : item.Status.Trim();
        existing.Notes = item.Notes;

        db.SaveChanges();

        var newValuesObj = new
        {
            existing.SourceCode,
            existing.SerialNumber,
            existing.NeutronSourceTypeId,
            existing.LocationId,
            existing.CalibratedEmissionRate,
            existing.RelativeExpandedUncertaintyPercent,
            CalibrationDate = existing.CalibrationDate?.ToString("yyyy-MM-dd"),
            EmissionCalibrationDate = existing.EmissionCalibrationDate?.ToString("yyyy-MM-dd"),
            existing.CalibrationReference,
            existing.AnisotropyFactor,
            existing.Status,
            existing.Notes
        };
        string newValuesJson = System.Text.Json.JsonSerializer.Serialize(newValuesObj);

        _auditService.LogWithChanges("Update", "NeutronSources", item.Id, $"تعديل مصدر نيتروني: {item.SourceCode}", oldValuesJson, newValuesJson);
        return (true, "تم تحديث المصدر النيتروني");
    }

    /// <summary>حذف مصدر نيتروني</summary>
    public (bool Success, string Message) Delete(Guid id)
    {
        var guard = AuthorizationGuard.RequireEditor(_userService.CurrentUser, "Sources");
        if (!guard.Allowed) return (false, guard.Message);

        using var db = _dbFactory.CreateDbContext();
        var item = db.NeutronSources.Find(id);
        if (item == null) return (false, "المصدر النيتروني غير موجود");

        var oldValuesObj = new
        {
            item.SourceCode,
            item.SerialNumber,
            item.NeutronSourceTypeId,
            item.LocationId,
            item.CalibratedEmissionRate,
            item.RelativeExpandedUncertaintyPercent,
            CalibrationDate = item.CalibrationDate?.ToString("yyyy-MM-dd"),
            EmissionCalibrationDate = item.EmissionCalibrationDate?.ToString("yyyy-MM-dd"),
            item.CalibrationReference,
            item.AnisotropyFactor,
            item.Status,
            item.Notes
        };
        string oldValuesJson = System.Text.Json.JsonSerializer.Serialize(oldValuesObj);

        item.IsDeleted = true;
        item.DeletedAt = DateTime.Now;
        var currentUserId = _userService.CurrentUser?.Id;
        if (currentUserId.HasValue && db.Users.Any(u => u.Id == currentUserId.Value))
        {
            item.DeletedBy = currentUserId.Value;
        }
        else
        {
            item.DeletedBy = null;
        }

        db.SaveChanges();
        _auditService.LogWithChanges("Delete", "NeutronSources", id, $"حذف مصدر نيتروني: {item.SourceCode}", oldValuesJson, null);
        return (true, "تم حذف المصدر النيتروني");
    }

    /// <summary>استرجاع مصدر نيتروني محذوف</summary>
    public (bool Success, string Message) Restore(Guid id)
    {
        var guard = AuthorizationGuard.RequireEditor(_userService.CurrentUser, "Sources");
        if (!guard.Allowed) return (false, guard.Message);

        using var db = _dbFactory.CreateDbContext();
        var item = db.NeutronSources.IgnoreQueryFilters().FirstOrDefault(n => n.Id == id);
        if (item == null) return (false, "المصدر النيتروني غير موجود");
        if (!item.IsDeleted) return (false, "المصدر النيتروني غير محذوف أصلاً");

        var lowerCode = item.SourceCode.Trim().ToLower();
        if (db.NeutronSources.Any(n => !n.IsDeleted && n.Id != id && n.SourceCode.ToLower() == lowerCode))
            return (false, $"لا يمكن استرجاع المصدر النيتروني لوجود مصدر نشط آخر بنفس الكود ({item.SourceCode})");

        item.IsDeleted = false;
        item.DeletedAt = null;
        item.DeletedBy = null;
        db.SaveChanges();

        var newValuesObj = new
        {
            item.SourceCode,
            item.SerialNumber,
            item.NeutronSourceTypeId,
            item.LocationId,
            item.CalibratedEmissionRate,
            item.RelativeExpandedUncertaintyPercent,
            CalibrationDate = item.CalibrationDate?.ToString("yyyy-MM-dd"),
            EmissionCalibrationDate = item.EmissionCalibrationDate?.ToString("yyyy-MM-dd"),
            item.CalibrationReference,
            item.AnisotropyFactor,
            item.Status,
            item.Notes
        };
        string newValuesJson = System.Text.Json.JsonSerializer.Serialize(newValuesObj);

        _auditService.LogWithChanges("Restore", "NeutronSources", id, $"استرجاع مصدر نيتروني: {item.SourceCode}", null, newValuesJson);
        return (true, $"تم استرجاع المصدر النيتروني {item.SourceCode}");
    }
}
