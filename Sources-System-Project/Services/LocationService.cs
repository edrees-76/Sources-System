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
        return db.Locations.OrderBy(l => l.LocationName).ToList();
    }

    public Location? GetById(Guid id)
    {
        using var db = _dbFactory.CreateDbContext();
        return db.Locations.Find(id);
    }

    public (bool Success, string Message) Create(Location item)
    {
        if (item == null) return (false, "بيانات الموقع غير صالحة");
        if (string.IsNullOrWhiteSpace(item.LocationName)) return (false, "اسم الموقع مطلوب");

        using var db = _dbFactory.CreateDbContext();
        var trimmedName = item.LocationName.Trim();
        if (db.Locations.Any(l => l.LocationName == trimmedName))
            return (false, "اسم الموقع موجود بالفعل");

        item.LocationName = trimmedName;
        item.AddedBy = _userService.CurrentUser?.FullName;
        db.Locations.Add(item);
        db.SaveChanges();
        _auditService.Log("Create", "Locations", item.Id, $"إضافة موقع: {item.LocationName}");
        return (true, "تم إضافة الموقع بنجاح");
    }

    public (bool Success, string Message) Update(Location item)
    {
        if (item == null) return (false, "بيانات الموقع غير صالحة");
        if (string.IsNullOrWhiteSpace(item.LocationName)) return (false, "اسم الموقع مطلوب");

        using var db = _dbFactory.CreateDbContext();
        var existing = db.Locations.Find(item.Id);
        if (existing == null) return (false, "الموقع غير موجود");

        var trimmedName = item.LocationName.Trim();
        if (db.Locations.Any(l => l.Id != item.Id && l.LocationName == trimmedName))
            return (false, "اسم الموقع موجود بالفعل");

        existing.LocationName = trimmedName;
        existing.LocationType = item.LocationType;
        existing.Building = item.Building;
        existing.Room = item.Room;
        existing.ResponsiblePerson = item.ResponsiblePerson;
        db.SaveChanges();
        _auditService.Log("Update", "Locations", item.Id, $"تعديل موقع: {existing.LocationName}");
        return (true, "تم تحديث الموقع");
    }

    public (bool Success, string Message) Delete(Guid id)
    {
        using var db = _dbFactory.CreateDbContext();
        var item = db.Locations.Include(l => l.Sources).FirstOrDefault(l => l.Id == id);
        if (item == null) return (false, "الموقع غير موجود");
        if (item.Sources.Any()) return (false, "لا يمكن حذف موقع يحتوي على مصادر");
        item.IsDeleted = true;
        db.SaveChanges();
        _auditService.Log("Delete", "Locations", id, $"حذف موقع: {item.LocationName}");
        return (true, "تم حذف الموقع");
    }

    public int GetCount()
    {
        using var db = _dbFactory.CreateDbContext();
        return db.Locations.Count();
    }
}
