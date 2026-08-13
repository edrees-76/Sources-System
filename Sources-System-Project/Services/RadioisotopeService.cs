using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Sources.Data;
using Sources.Models;
using Sources.Helpers;

namespace Sources.Services;

public class RadioisotopeService : IRadioisotopeService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly IAuditService _auditService;
    private readonly IUserService _userService;

    public RadioisotopeService(IDbContextFactory<AppDbContext> dbFactory, IAuditService auditService, IUserService userService)
    {
        _dbFactory = dbFactory;
        _auditService = auditService;
        _userService = userService;
    }

    public List<Radioisotope> GetAll()
    {
        using var db = _dbFactory.CreateDbContext();
        return db.Radioisotopes.OrderBy(r => r.Name).ToList();
    }

    public Radioisotope? GetById(Guid id)
    {
        using var db = _dbFactory.CreateDbContext();
        return db.Radioisotopes.Find(id);
    }

    public (bool Success, string Message) Create(Radioisotope item)
    {
        using var db = _dbFactory.CreateDbContext();
        if (db.Radioisotopes.Any(r => r.Symbol == item.Symbol))
            return (false, "رمز النظير موجود بالفعل");
        
        if (string.IsNullOrEmpty(item.ArabicName))
            item.ArabicName = IsotopeHelper.GetArabicNameFromSymbol(item.Symbol);

        item.AddedBy = _userService.CurrentUser?.FullName ?? "غير معروف";

        db.Radioisotopes.Add(item);
        db.SaveChanges();
        _auditService.Log("Create", "Radioisotopes", item.Id, $"إضافة نظير: {item.Name}");
        return (true, "تم إضافة النظير بنجاح");
    }

    public (bool Success, string Message) Update(Radioisotope item)
    {
        using var db = _dbFactory.CreateDbContext();
        var existing = db.Radioisotopes.Find(item.Id);
        if (existing == null) return (false, "النظير غير موجود");
        existing.Name = item.Name;
        existing.ArabicName = string.IsNullOrEmpty(item.ArabicName) ? IsotopeHelper.GetArabicNameFromSymbol(item.Symbol) : item.ArabicName;
        existing.Symbol = item.Symbol;
        existing.RadiationType = item.RadiationType;
        existing.HalfLife = item.HalfLife;
        existing.HalfLifeUnit = item.HalfLifeUnit;
        existing.Energy = item.Energy;
        existing.Yield = item.Yield;
        existing.Notes = item.Notes;
        db.SaveChanges();
        _auditService.Log("Update", "Radioisotopes", item.Id, $"تعديل نظير: {item.Name}");
        return (true, "تم تحديث النظير");
    }

    public (bool Success, string Message) Delete(Guid id)
    {
        using var db = _dbFactory.CreateDbContext();
        var item = db.Radioisotopes.Include(r => r.Sources).FirstOrDefault(r => r.Id == id);
        if (item == null) return (false, "النظير غير موجود");
        if (item.Sources.Any()) return (false, "لا يمكن حذف نظير مرتبط بمصادر");
        item.IsDeleted = true;
        db.SaveChanges();
        _auditService.Log("Delete", "Radioisotopes", id, $"حذف نظير: {item.Name}");
        return (true, "تم حذف النظير");
    }
}
