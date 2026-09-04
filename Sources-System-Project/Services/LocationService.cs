using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Sources.Data;
using Sources.Models;

namespace Sources.Services;

public class LocationService : ILocationService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly IAuditService _auditService;
    private readonly IUserService _userService;

    public LocationService(IDbContextFactory<AppDbContext> dbFactory, IAuditService auditService, IUserService userService)
    {
        _dbFactory = dbFactory;
        _auditService = auditService;
        _userService = userService;
    }

    public List<Location> GetAll()
    {
        using var db = _dbFactory.CreateDbContext();
        var locations = db.Locations
            .AsNoTracking()
            .Include(l => l.AddedByUser)
            .OrderBy(l => l.LocationName)
            .ToList();

        var counts = db.Sources
            .AsNoTracking()
            .Where(s => s.LocationId != null)
            .GroupBy(s => s.LocationId!.Value)
            .Select(g => new { LocationId = g.Key, Count = g.Count() })
            .ToDictionary(x => x.LocationId, x => x.Count);

        foreach (var loc in locations)
        {
            loc.SourceCount = counts.TryGetValue(loc.Id, out int c) ? c : 0;
        }

        return locations;
    }

    public Location? GetById(Guid id)
    {
        using var db = _dbFactory.CreateDbContext();
        var location = db.Locations.Include(l => l.AddedByUser).FirstOrDefault(l => l.Id == id);
        if (location != null)
        {
            location.SourceCount = db.Sources.Count(s => s.LocationId == id);
        }
        return location;
    }

    public (bool Success, string Message) Create(Location item)
    {
        if (item == null) return (false, "بيانات الموقع غير صالحة");
        if (string.IsNullOrWhiteSpace(item.LocationName)) return (false, "اسم الموقع مطلوب");

        using var db = _dbFactory.CreateDbContext();
        var trimmedName = item.LocationName.Trim();
        var lowerName = trimmedName.ToLower();
        if (db.Locations.Any(l => l.LocationName.ToLower() == lowerName))
            return (false, "اسم الموقع موجود بالفعل");

        item.LocationName = trimmedName;
        var addedByUserId = _userService.CurrentUser?.Id;
        item.AddedBy = (addedByUserId.HasValue && db.Users.Any(u => u.Id == addedByUserId.Value))
            ? addedByUserId.Value
            : null;
        db.Locations.Add(item);
        db.SaveChanges();

        var newValuesObj = new
        {
            item.LocationName,
            item.LocationType,
            item.Building,
            item.Room,
            item.ResponsiblePerson
        };
        _auditService.LogWithChanges("Create", "Locations", item.Id, $"إضافة موقع: {item.LocationName}", oldValues: null, newValues: System.Text.Json.JsonSerializer.Serialize(newValuesObj));
        return (true, "تم إضافة الموقع بنجاح");
    }

    public (bool Success, string Message) Update(Location item)
    {
        if (item == null) return (false, "بيانات الموقع غير صالحة");
        if (string.IsNullOrWhiteSpace(item.LocationName)) return (false, "اسم الموقع مطلوب");

        using var db = _dbFactory.CreateDbContext();
        var existing = db.Locations.Find(item.Id);
        if (existing == null) return (false, "الموقع غير موجود");

        var oldValuesObj = new
        {
            existing.LocationName,
            existing.LocationType,
            existing.Building,
            existing.Room,
            existing.ResponsiblePerson
        };
        string oldValuesJson = System.Text.Json.JsonSerializer.Serialize(oldValuesObj);

        var trimmedName = item.LocationName.Trim();
        var lowerName = trimmedName.ToLower();
        if (db.Locations.Any(l => l.Id != item.Id && l.LocationName.ToLower() == lowerName))
            return (false, "اسم الموقع موجود بالفعل");

        existing.LocationName = trimmedName;
        existing.LocationType = item.LocationType;
        existing.Building = item.Building;
        existing.Room = item.Room;
        existing.ResponsiblePerson = item.ResponsiblePerson;
        db.SaveChanges();

        var newValuesObj = new
        {
            existing.LocationName,
            existing.LocationType,
            existing.Building,
            existing.Room,
            existing.ResponsiblePerson
        };
        string newValuesJson = System.Text.Json.JsonSerializer.Serialize(newValuesObj);

        _auditService.LogWithChanges("Update", "Locations", item.Id, $"تعديل موقع: {existing.LocationName}", oldValuesJson, newValuesJson);
        return (true, "تم تحديث الموقع");
    }

    public (bool Success, string Message) Delete(Guid id)
    {
        var guard = AuthorizationGuard.RequireEditor(_userService.CurrentUser, "Locations");
        if (!guard.Allowed) return (false, guard.Message);

        using var db = _dbFactory.CreateDbContext();
        var item = db.Locations.Include(l => l.Sources).FirstOrDefault(l => l.Id == id);
        if (item == null) return (false, "الموقع غير موجود");
        if (item.Sources.Any() || db.NeutronSources.Any(ns => ns.LocationId == id)) return (false, $"لا يمكن حذف الموقع \"{item.LocationName}\" لاحتوائه على مصادر مرتبطة به");

        var oldValuesObj = new
        {
            item.LocationName,
            item.LocationType,
            item.Building,
            item.Room,
            item.ResponsiblePerson
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
        _auditService.LogWithChanges("Delete", "Locations", id, $"حذف موقع: {item.LocationName}", oldValuesJson, null);
        return (true, "تم حذف الموقع");
    }

    public (bool Success, string Message) Restore(Guid id)
    {
        var guard = AuthorizationGuard.RequireEditor(_userService.CurrentUser, "Locations");
        if (!guard.Allowed) return (false, guard.Message);

        using var db = _dbFactory.CreateDbContext();
        var item = db.Locations.IgnoreQueryFilters().FirstOrDefault(l => l.Id == id);
        if (item == null) return (false, "الموقع غير موجود");
        if (!item.IsDeleted) return (false, "الموقع غير محذوف أصلاً");

        var lowerName = item.LocationName.Trim().ToLower();
        if (db.Locations.Any(l => !l.IsDeleted && l.Id != id && l.LocationName.ToLower() == lowerName))
            return (false, $"لا يمكن استرجاع الموقع لوجود موقع نشط آخر بنفس الاسم (\"{item.LocationName}\")");

        item.IsDeleted = false;
        item.DeletedAt = null;
        item.DeletedBy = null;
        db.SaveChanges();

        var newValuesObj = new
        {
            item.LocationName,
            item.LocationType,
            item.Building,
            item.Room,
            item.ResponsiblePerson
        };
        string newValuesJson = System.Text.Json.JsonSerializer.Serialize(newValuesObj);

        _auditService.LogWithChanges("Restore", "Locations", id, $"استرجاع موقع: {item.LocationName}", null, newValuesJson);
        return (true, $"تم استرجاع الموقع {item.LocationName}");
    }

    public int GetCount()
    {
        using var db = _dbFactory.CreateDbContext();
        return db.Locations.Count();
    }

    public List<Source> GetSourcesLinkedToLocation(Guid locationId)
    {
        using var db = _dbFactory.CreateDbContext();

        var currentSourceIds = db.Sources
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(s => s.LocationId == locationId)
            .Select(s => s.Id)
            .ToList();

        var historicalSourceIds = db.SourceLocationHistories
            .AsNoTracking()
            .Where(h => h.LocationId == locationId)
            .Select(h => h.SourceId)
            .ToList();

        var allSourceIds = currentSourceIds.Concat(historicalSourceIds).Distinct().ToList();

        if (!allSourceIds.Any())
            return new List<Source>();

        return db.Sources
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Include(s => s.Radioisotope)
            .Include(s => s.InitialActivityUnit)
            .Include(s => s.CurrentActivityUnit)
            .Include(s => s.Location)
            .Include(s => s.SourceIsotopes).ThenInclude(si => si.Radioisotope)
            .Include(s => s.SourceIsotopes).ThenInclude(si => si.ActivityUnit)
            .Where(s => allSourceIds.Contains(s.Id))
            .OrderBy(s => s.SourceCode)
            .ToList();
    }
}
