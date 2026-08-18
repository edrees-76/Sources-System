using System;
using System.Collections.Generic;
using System.Linq;
using Sources.Data;
using Sources.Models;
using Microsoft.EntityFrameworkCore;

namespace Sources.Services;

public class SystemSettingsService : ISystemSettingsService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private static Dictionary<string, string>? _cache;

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
        catch
        {
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
    }

    public void ResetToDefaults()
    {
        SaveSettings(SystemSettingsDefaults.AllDefaults);
    }

    public void ClearCache()
    {
        _cache = null;
    }
}
