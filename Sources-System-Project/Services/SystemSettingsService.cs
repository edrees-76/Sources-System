using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Sources.Data;
using Sources.Models;
using Microsoft.EntityFrameworkCore;

namespace Sources.Services;

public class SystemSettingsService : ISystemSettingsService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private Dictionary<string, string>? _cache;
    private readonly ConcurrentDictionary<string, byte> _corruptedKeys = new();

    public IReadOnlyCollection<string> CorruptedKeys => _corruptedKeys.Keys.ToList().AsReadOnly();

    public SystemSettingsService(IDbContextFactory<AppDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public Dictionary<string, string> GetAllSettings()
    {
        if (_cache != null) return _cache;

        using var db = _dbFactory.CreateDbContext();
        _cache = db.AppSettings.ToDictionary(s => s.Key, s => s.Value);
        return _cache;
    }

    public string GetSetting(string key, string defaultValue = "")
    {
        var settings = GetAllSettings();
        return settings.TryGetValue(key, out var value) ? value : defaultValue;
    }

    public T GetSetting<T>(string key, T defaultValue = default!)
    {
        var value = GetSetting(key, string.Empty);
        if (string.IsNullOrEmpty(value)) return defaultValue;

        try
        {
            var result = Convert.ChangeType(value, typeof(T));
            return result != null ? (T)result : defaultValue;
        }
        catch (Exception ex)
        {
            // التسجيل مرة واحدة لكل مفتاح: الدالة تُستدعى في كل دورة فحص فيغرق السجل.
            if (_corruptedKeys.TryAdd(key, 0))
            {
                LoggerService.LogWarning(
                    $"قيمة الإعداد «{key}» المخزَّنة في قاعدة البيانات تالفة وتعذّر تحويلها إلى {typeof(T).Name}: " +
                    $"القيمة «{value}»، السبب: {ex.Message}. سيُستعمل الافتراضي {defaultValue} حتى تُصحَّح. " +
                    "تنبيه: بعض هذه الإعدادات عتبات سلامة، فقد تختلف القيمة العاملة عمّا يظنه المسؤول.");
            }
            return defaultValue;
        }
    }

    public void SaveSetting(string key, string value)
    {
        using var db = _dbFactory.CreateDbContext();
        var setting = db.AppSettings.Find(key);
        if (setting == null)
        {
            db.AppSettings.Add(new AppSetting { Key = key, Value = value });
        }
        else
        {
            setting.Value = value;
        }
        db.SaveChanges();
        _cache = null; // Invalidate cache
        _corruptedKeys.TryRemove(key, out _);
    }

    public void SaveSettings(Dictionary<string, string> settings)
    {
        using var db = _dbFactory.CreateDbContext();
        foreach (var kvp in settings)
        {
            var setting = db.AppSettings.Find(kvp.Key);
            if (setting == null)
            {
                db.AppSettings.Add(new AppSetting { Key = kvp.Key, Value = kvp.Value });
            }
            else
            {
                setting.Value = kvp.Value;
            }
        }
        db.SaveChanges();
        _cache = null;
        foreach (var key in settings.Keys)
        {
            _corruptedKeys.TryRemove(key, out _);
        }
    }

    public void ResetToDefaults()
    {
        SaveSettings(SystemSettingsDefaults.AllDefaults);
    }

    public void ClearCache()
    {
        _cache = null;
        _corruptedKeys.Clear();
    }
}
