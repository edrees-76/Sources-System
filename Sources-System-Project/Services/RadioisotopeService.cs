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
        return db.Radioisotopes
            .AsNoTracking()
            .Include(r => r.AddedByUser)
            .OrderBy(r => r.Name)
            .ToList();
    }

    public Radioisotope? GetById(Guid id)
    {
        using var db = _dbFactory.CreateDbContext();
        return db.Radioisotopes.Include(r => r.AddedByUser).FirstOrDefault(r => r.Id == id);
    }

    public (bool Success, string Message) Create(Radioisotope item)
    {
        if (item == null) return (false, "بيانات النظير غير صالحة");
        if (!double.IsFinite(item.HalfLife))
            return (false, TranslationHelper.GetString("MsgErrInvalidHalfLifeFinite") ?? "قيمة نصف العمر غير صالحة (يجب أن تكون رقماً منتهياً)");
        if (item.HalfLife <= 0) return (false, "نصف العمر يجب أن يكون أكبر من صفر");
        if (!double.IsFinite(item.Energy))
            return (false, TranslationHelper.GetString("MsgErrInvalidEnergyFinite") ?? "قيمة الطاقة غير صالحة (يجب أن تكون رقماً منتهياً)");
        if (item.Energy < 0) return (false, "قيمة الطاقة غير صالحة");
        if (item.Yield.HasValue && !double.IsFinite(item.Yield.Value))
            return (false, TranslationHelper.GetString("MsgErrInvalidYieldFinite") ?? "قيمة المردود غير صالحة (يجب أن تكون رقماً منتهياً)");
        if (item.ExemptionLimit.HasValue && !double.IsFinite(item.ExemptionLimit.Value))
            return (false, TranslationHelper.GetString("MsgErrInvalidExemptionLimitFinite") ?? "قيمة حد الإعفاء غير صالحة (يجب أن تكون رقماً منتهياً)");
        if (item.GammaConstant.HasValue && !double.IsFinite(item.GammaConstant.Value))
            return (false, TranslationHelper.GetString("MsgErrInvalidGammaConstantFinite") ?? "قيمة ثابت غاما غير صالحة (يجب أن تكون رقماً منتهياً)");

        using var db = _dbFactory.CreateDbContext();
        if (db.Radioisotopes.Any(r => r.Symbol == item.Symbol))
            return (false, "رمز النظير موجود بالفعل");
        
        if (string.IsNullOrEmpty(item.ArabicName))
            item.ArabicName = IsotopeHelper.GetArabicNameFromSymbol(item.Symbol);

        var addedByUserId = _userService.CurrentUser?.Id;
        item.AddedBy = (addedByUserId.HasValue && db.Users.Any(u => u.Id == addedByUserId.Value))
            ? addedByUserId.Value
            : null;

        db.Radioisotopes.Add(item);
        db.SaveChanges();

        var newValuesObj = new
        {
            item.Name,
            item.ArabicName,
            item.Symbol,
            item.RadiationType,
            item.HalfLife,
            item.HalfLifeUnit,
            item.Energy,
            item.Yield,
            item.Category,
            item.ExemptionLimit,
            item.GammaConstant,
            item.Notes,
            item.EnglishNotes
        };
        _auditService.LogWithChanges("Create", "Radioisotopes", item.Id, $"إضافة نظير: {item.Name}", oldValues: null, newValues: System.Text.Json.JsonSerializer.Serialize(newValuesObj));
        return (true, "تم إضافة النظير بنجاح");
    }

    public (bool Success, string Message) Update(Radioisotope item)
    {
        if (item == null) return (false, "بيانات النظير غير صالحة");
        if (!double.IsFinite(item.HalfLife))
            return (false, TranslationHelper.GetString("MsgErrInvalidHalfLifeFinite") ?? "قيمة نصف العمر غير صالحة (يجب أن تكون رقماً منتهياً)");
        if (item.HalfLife <= 0) return (false, "نصف العمر يجب أن يكون أكبر من صفر");
        if (!double.IsFinite(item.Energy))
            return (false, TranslationHelper.GetString("MsgErrInvalidEnergyFinite") ?? "قيمة الطاقة غير صالحة (يجب أن تكون رقماً منتهياً)");
        if (item.Energy < 0) return (false, "قيمة الطاقة غير صالحة");
        if (item.Yield.HasValue && !double.IsFinite(item.Yield.Value))
            return (false, TranslationHelper.GetString("MsgErrInvalidYieldFinite") ?? "قيمة المردود غير صالحة (يجب أن تكون رقماً منتهياً)");
        if (item.ExemptionLimit.HasValue && !double.IsFinite(item.ExemptionLimit.Value))
            return (false, TranslationHelper.GetString("MsgErrInvalidExemptionLimitFinite") ?? "قيمة حد الإعفاء غير صالحة (يجب أن تكون رقماً منتهياً)");
        if (item.GammaConstant.HasValue && !double.IsFinite(item.GammaConstant.Value))
            return (false, TranslationHelper.GetString("MsgErrInvalidGammaConstantFinite") ?? "قيمة ثابت غاما غير صالحة (يجب أن تكون رقماً منتهياً)");

        using var db = _dbFactory.CreateDbContext();
        var existing = db.Radioisotopes.Find(item.Id);
        if (existing == null) return (false, "النظير غير موجود");

        var oldValuesObj = new
        {
            existing.Name,
            existing.ArabicName,
            existing.Symbol,
            existing.RadiationType,
            existing.HalfLife,
            existing.HalfLifeUnit,
            existing.Energy,
            existing.Yield,
            existing.Category,
            existing.ExemptionLimit,
            existing.GammaConstant,
            existing.Notes,
            existing.EnglishNotes
        };
        string oldValuesJson = System.Text.Json.JsonSerializer.Serialize(oldValuesObj);

        if (db.Radioisotopes.Any(r => r.Symbol == item.Symbol && r.Id != item.Id))
            return (false, "رمز النظير موجود بالفعل");

        existing.Name = item.Name;
        existing.ArabicName = string.IsNullOrEmpty(item.ArabicName) ? IsotopeHelper.GetArabicNameFromSymbol(item.Symbol) : item.ArabicName;
        existing.Symbol = item.Symbol;
        existing.RadiationType = item.RadiationType;
        existing.HalfLife = item.HalfLife;
        existing.HalfLifeUnit = item.HalfLifeUnit;
        existing.Energy = item.Energy;
        existing.Yield = item.Yield;
        existing.Category = item.Category;
        existing.ExemptionLimit = item.ExemptionLimit;
        existing.GammaConstant = item.GammaConstant;
        existing.Notes = item.Notes;
        existing.EnglishNotes = item.EnglishNotes;
        db.SaveChanges();

        var newValuesObj = new
        {
            existing.Name,
            existing.ArabicName,
            existing.Symbol,
            existing.RadiationType,
            existing.HalfLife,
            existing.HalfLifeUnit,
            existing.Energy,
            existing.Yield,
            existing.Category,
            existing.ExemptionLimit,
            existing.GammaConstant,
            existing.Notes,
            existing.EnglishNotes
        };
        string newValuesJson = System.Text.Json.JsonSerializer.Serialize(newValuesObj);

        _auditService.LogWithChanges("Update", "Radioisotopes", item.Id, $"تعديل نظير: {item.Name}", oldValuesJson, newValuesJson);
        return (true, "تم تحديث النظير");
    }

    public (bool Success, string Message) Delete(Guid id)
    {
        var guard = AuthorizationGuard.RequireEditor(_userService.CurrentUser, "Radioisotopes");
        if (!guard.Allowed) return (false, guard.Message);

        using var db = _dbFactory.CreateDbContext();
        var item = db.Radioisotopes.Include(r => r.Sources).FirstOrDefault(r => r.Id == id);
        if (item == null) return (false, "النظير غير موجود");
        if (item.Sources.Any() || db.SourceIsotopes.Any(si => si.RadioisotopeId == id))
            return (false, "لا يمكن حذف نظير مرتبط بمصادر");

        var oldValuesObj = new
        {
            item.Name,
            item.ArabicName,
            item.Symbol,
            item.RadiationType,
            item.HalfLife,
            item.HalfLifeUnit,
            item.Energy,
            item.Yield,
            item.Category,
            item.ExemptionLimit,
            item.GammaConstant,
            item.Notes,
            item.EnglishNotes
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
        _auditService.LogWithChanges("Delete", "Radioisotopes", id, $"حذف نظير: {item.Name}", oldValuesJson, null);
        return (true, "تم حذف النظير");
    }

    public (bool Success, string Message) Restore(Guid id)
    {
        var guard = AuthorizationGuard.RequireEditor(_userService.CurrentUser, "Radioisotopes");
        if (!guard.Allowed) return (false, guard.Message);

        using var db = _dbFactory.CreateDbContext();
        var item = db.Radioisotopes.IgnoreQueryFilters().FirstOrDefault(r => r.Id == id);
        if (item == null) return (false, "النظير غير موجود");
        if (!item.IsDeleted) return (false, "النظير غير محذوف أصلاً");

        var lowerSymbol = item.Symbol.Trim().ToLower();
        if (db.Radioisotopes.Any(r => !r.IsDeleted && r.Id != id && r.Symbol.ToLower() == lowerSymbol))
            return (false, $"لا يمكن استرجاع النظير لوجود نظير نشط آخر بنفس الرمز ({item.Symbol})");

        item.IsDeleted = false;
        item.DeletedAt = null;
        item.DeletedBy = null;
        db.SaveChanges();

        var newValuesObj = new
        {
            item.Name,
            item.ArabicName,
            item.Symbol,
            item.RadiationType,
            item.HalfLife,
            item.HalfLifeUnit,
            item.Energy,
            item.Yield,
            item.Category,
            item.ExemptionLimit,
            item.GammaConstant,
            item.Notes,
            item.EnglishNotes
        };
        string newValuesJson = System.Text.Json.JsonSerializer.Serialize(newValuesObj);

        _auditService.LogWithChanges("Restore", "Radioisotopes", id, $"استرجاع نظير: {item.DisplayName ?? item.Symbol}", null, newValuesJson);
        return (true, $"تم استرجاع النظير {item.DisplayName ?? item.Symbol}");
    }
}
