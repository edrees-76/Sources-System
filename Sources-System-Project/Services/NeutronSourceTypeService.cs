using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Sources.Data;
using Sources.Models;

namespace Sources.Services;

/// <summary>
/// خدمة إدارة أنواع المصادر النيترونية
/// </summary>
public class NeutronSourceTypeService : INeutronSourceTypeService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly IAuditService _auditService;
    private readonly IUserService _userService;

    public NeutronSourceTypeService(IDbContextFactory<AppDbContext> dbFactory, IAuditService auditService, IUserService userService)
    {
        _dbFactory = dbFactory;
        _auditService = auditService;
        _userService = userService;
    }

    /// <summary>جلب جميع أنواع المصادر النيترونية النشطة</summary>
    public List<NeutronSourceType> GetAll()
    {
        using var db = _dbFactory.CreateDbContext();
        return db.NeutronSourceTypes
            .AsNoTracking()
            .Include(t => t.AddedByUser)
            .OrderBy(t => t.Code)
            .ToList();
    }

    /// <summary>جلب أنواع المصادر النيترونية المحذوفة</summary>
    public List<NeutronSourceType> GetDeleted()
    {
        using var db = _dbFactory.CreateDbContext();
        return db.NeutronSourceTypes
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Include(t => t.DeletedByUser)
            .Include(t => t.AddedByUser)
            .Where(t => t.IsDeleted)
            .OrderByDescending(t => t.DeletedAt)
            .ToList();
    }

    /// <summary>جلب نوع مصدر نيتروني بالمعرف</summary>
    public NeutronSourceType? GetById(Guid id)
    {
        using var db = _dbFactory.CreateDbContext();
        return db.NeutronSourceTypes.Include(t => t.AddedByUser).FirstOrDefault(t => t.Id == id);
    }

    /// <summary>إنشاء نوع مصدر نيتروني جديد</summary>
    public (bool Success, string Message) Create(NeutronSourceType item)
    {
        if (item == null) return (false, "بيانات نوع المصدر غير صالحة");
        if (string.IsNullOrWhiteSpace(item.Code)) return (false, "رمز نوع المصدر مطلوب");
        if (string.IsNullOrWhiteSpace(item.NameEn)) return (false, "الاسم بالإنجليزية مطلوب");
        if (item.HalfLife <= 0) return (false, "نصف العمر يجب أن يكون أكبر من صفر");

        using var db = _dbFactory.CreateDbContext();
        var trimmedCode = item.Code.Trim();
        var lowerCode = trimmedCode.ToLower();
        if (db.NeutronSourceTypes.Any(t => t.Code.ToLower() == lowerCode))
            return (false, "رمز نوع المصدر موجود بالفعل");

        item.Code = trimmedCode;
        var addedByUserId = _userService.CurrentUser?.Id;
        item.AddedBy = (addedByUserId.HasValue && db.Users.Any(u => u.Id == addedByUserId.Value))
            ? addedByUserId.Value
            : null;
        item.CreatedAt = DateTime.Now;

        db.NeutronSourceTypes.Add(item);
        db.SaveChanges();
        _auditService.Log("Create", "NeutronSourceTypes", item.Id, $"إضافة نوع مصدر نيتروني: {item.Code}");
        return (true, "تم إضافة نوع المصدر النيتروني بنجاح");
    }

    /// <summary>تحديث نوع مصدر نيتروني</summary>
    public (bool Success, string Message) Update(NeutronSourceType item)
    {
        if (item == null) return (false, "بيانات نوع المصدر غير صالحة");
        if (string.IsNullOrWhiteSpace(item.Code)) return (false, "رمز نوع المصدر مطلوب");
        if (string.IsNullOrWhiteSpace(item.NameEn)) return (false, "الاسم بالإنجليزية مطلوب");
        if (item.HalfLife <= 0) return (false, "نصف العمر يجب أن يكون أكبر من صفر");

        using var db = _dbFactory.CreateDbContext();
        var existing = db.NeutronSourceTypes.Find(item.Id);
        if (existing == null) return (false, "نوع المصدر غير موجود");

        var trimmedCode = item.Code.Trim();
        var lowerCode = trimmedCode.ToLower();
        if (db.NeutronSourceTypes.Any(t => t.Id != item.Id && t.Code.ToLower() == lowerCode))
            return (false, "رمز نوع المصدر موجود بالفعل");

        existing.Code = trimmedCode;
        existing.NameEn = item.NameEn.Trim();
        existing.NameAr = item.NameAr?.Trim() ?? string.Empty;
        existing.ReactionType = item.ReactionType?.Trim() ?? string.Empty;
        existing.TargetMaterial = item.TargetMaterial?.Trim();
        existing.ParentNuclide = item.ParentNuclide?.Trim();
        existing.HalfLife = item.HalfLife;
        existing.HalfLifeUnit = string.IsNullOrWhiteSpace(item.HalfLifeUnit) ? "years" : item.HalfLifeUnit.Trim();
        existing.AverageNeutronEnergyMeV = item.AverageNeutronEnergyMeV;
        existing.TypicalNeutronYield = item.TypicalNeutronYield;
        existing.Notes = item.Notes;

        db.SaveChanges();
        _auditService.Log("Update", "NeutronSourceTypes", item.Id, $"تعديل نوع مصدر نيتروني: {item.Code}");
        return (true, "تم تحديث نوع المصدر النيتروني");
    }

    /// <summary>حذف نوع مصدر نيتروني</summary>
    public (bool Success, string Message) Delete(Guid id)
    {
        using var db = _dbFactory.CreateDbContext();
        var item = db.NeutronSourceTypes.Include(t => t.NeutronSources).FirstOrDefault(t => t.Id == id);
        if (item == null) return (false, "نوع المصدر غير موجود");

        if (item.NeutronSources.Any() || db.NeutronSources.Any(n => n.NeutronSourceTypeId == id))
            return (false, "لا يمكن حذف نوع مصدر نيتروني مرتبط بمصادر نيترونية");

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
        _auditService.Log("Delete", "NeutronSourceTypes", id, $"حذف نوع مصدر نيتروني: {item.Code}");
        return (true, "تم حذف نوع المصدر النيتروني");
    }

    /// <summary>استرجاع نوع مصدر نيتروني محذوف</summary>
    public (bool Success, string Message) Restore(Guid id)
    {
        using var db = _dbFactory.CreateDbContext();
        var item = db.NeutronSourceTypes.IgnoreQueryFilters().FirstOrDefault(t => t.Id == id);
        if (item == null) return (false, "نوع المصدر غير موجود");
        if (!item.IsDeleted) return (false, "نوع المصدر غير محذوف أصلاً");

        var lowerCode = item.Code.Trim().ToLower();
        if (db.NeutronSourceTypes.Any(t => !t.IsDeleted && t.Id != id && t.Code.ToLower() == lowerCode))
            return (false, $"لا يمكن استرجاع نوع المصدر لوجود نوع نشط آخر بنفس الرمز ({item.Code})");

        item.IsDeleted = false;
        item.DeletedAt = null;
        item.DeletedBy = null;
        db.SaveChanges();

        _auditService.Log("Restore", "NeutronSourceTypes", id, $"استرجاع نوع مصدر نيتروني: {item.Code}");
        return (true, $"تم استرجاع نوع المصدر النيتروني {item.Code}");
    }
}
