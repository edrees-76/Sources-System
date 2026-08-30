using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Sources.Data;
using Sources.Models;

namespace Sources.Services;

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

    public List<NeutronSource> GetAll()
    {
        using var db = _dbFactory.CreateDbContext();
        return db.NeutronSources
            .AsNoTracking()
            .Include(n => n.NeutronSourceType)
            .Include(n => n.Location)
            .OrderBy(n => n.SourceCode)
            .ToList();
    }

    public List<NeutronSource> GetDeleted()
    {
        using var db = _dbFactory.CreateDbContext();
        return db.NeutronSources
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Include(n => n.NeutronSourceType)
            .Include(n => n.Location)
            .Include(n => n.DeletedByUser)
            .Where(n => n.IsDeleted)
            .OrderByDescending(n => n.DeletedAt)
            .ToList();
    }

    public NeutronSource? GetById(Guid id)
    {
        using var db = _dbFactory.CreateDbContext();
        return db.NeutronSources
            .Include(n => n.NeutronSourceType)
            .Include(n => n.Location)
            .FirstOrDefault(n => n.Id == id);
    }

    public NeutronSource? GetByCode(string sourceCode)
    {
        if (string.IsNullOrWhiteSpace(sourceCode)) return null;
        using var db = _dbFactory.CreateDbContext();
        var lowerCode = sourceCode.Trim().ToLower();
        return db.NeutronSources
            .Include(n => n.NeutronSourceType)
            .Include(n => n.Location)
            .FirstOrDefault(n => n.SourceCode.ToLower() == lowerCode);
    }

    public List<NeutronSource> GetByLocation(Guid locationId)
    {
        using var db = _dbFactory.CreateDbContext();
        return db.NeutronSources
            .AsNoTracking()
            .Include(n => n.NeutronSourceType)
            .Include(n => n.Location)
            .Where(n => n.LocationId == locationId)
            .OrderBy(n => n.SourceCode)
            .ToList();
    }

    public int GetTotalCount()
    {
        using var db = _dbFactory.CreateDbContext();
        return db.NeutronSources.Count();
    }

    public (bool Success, string Message) Create(NeutronSource item)
    {
        if (item == null) return (false, "بيانات المصدر النيتروني غير صالحة");
        if (string.IsNullOrWhiteSpace(item.SourceCode)) return (false, "كود المصدر مطلوب");
        if (item.EmissionRate <= 0) return (false, "معدل انبعاث النيترونات يجب أن يكون أكبر من صفر");
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
        item.AddedBy = _userService.CurrentUser?.Id;
        item.CreatedAt = DateTime.Now;

        db.NeutronSources.Add(item);
        db.SaveChanges();

        _auditService.Log("Create", "NeutronSources", item.Id, $"إضافة مصدر نيتروني: {item.SourceCode}");
        return (true, "تم إضافة المصدر النيتروني بنجاح");
    }

    public (bool Success, string Message) Update(NeutronSource item)
    {
        if (item == null) return (false, "بيانات المصدر النيتروني غير صالحة");
        if (string.IsNullOrWhiteSpace(item.SourceCode)) return (false, "كود المصدر مطلوب");
        if (item.EmissionRate <= 0) return (false, "معدل انبعاث النيترونات يجب أن يكون أكبر من صفر");
        if (item.NeutronSourceTypeId == Guid.Empty) return (false, "نوع المصدر النيتروني مطلوب");

        using var db = _dbFactory.CreateDbContext();
        var existing = db.NeutronSources.Find(item.Id);
        if (existing == null) return (false, "المصدر النيتروني غير موجود");

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
        existing.EmissionRate = item.EmissionRate;
        existing.RelativeExpandedUncertaintyPercent = item.RelativeExpandedUncertaintyPercent;
        existing.CalibrationDate = item.CalibrationDate;
        existing.Status = string.IsNullOrWhiteSpace(item.Status) ? "Storage" : item.Status.Trim();
        existing.Notes = item.Notes;

        db.SaveChanges();

        _auditService.Log("Update", "NeutronSources", item.Id, $"تعديل مصدر نيتروني: {item.SourceCode}");
        return (true, "تم تحديث المصدر النيتروني");
    }

    public (bool Success, string Message) Delete(Guid id)
    {
        using var db = _dbFactory.CreateDbContext();
        var item = db.NeutronSources.Find(id);
        if (item == null) return (false, "المصدر النيتروني غير موجود");

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
        _auditService.Log("Delete", "NeutronSources", id, $"حذف مصدر نيتروني: {item.SourceCode}");
        return (true, "تم حذف المصدر النيتروني");
    }

    public (bool Success, string Message) Restore(Guid id)
    {
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

        _auditService.Log("Restore", "NeutronSources", id, $"استرجاع مصدر نيتروني: {item.SourceCode}");
        return (true, $"تم استرجاع المصدر النيتروني {item.SourceCode}");
    }
}
