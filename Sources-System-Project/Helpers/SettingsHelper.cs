using System;
using System.IO;
using Sources.Data;

namespace Sources.Helpers;

public static class SettingsHelper
{
    private static readonly string SettingsDir = DatabasePaths.AppDataDirectory;
    private static readonly string SettingsFile;

    /// <summary>
    /// رسالة تحذير إن فشل نقل ملف الإعدادات من المسار القديم. null إن نجح النقل أو لم يلزم.
    /// يقرأها App عند الإقلاع ويسجّلها؛ ممنوع ابتلاع الفشل صامتاً.
    /// </summary>
    public static string? MigrationWarning { get; private set; }

    static SettingsHelper()
    {
        Directory.CreateDirectory(SettingsDir);
        SettingsFile = Path.Combine(SettingsDir, "settings.ini");

        var legacyFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.ini");
        MigrationWarning = MigrateLegacySettings(legacyFile, SettingsFile);
    }

    /// <summary>
    /// ينقل ملف الإعدادات القديم لمرة واحدة. يُرجع null عند النجاح أو عدم الحاجة،
    /// ونص خطأ عند الفشل — ولا يرمي، لأن فقدان التفضيلات لا يبرر منع الإقلاع.
    /// </summary>
    public static string? MigrateLegacySettings(string legacyFile, string targetFile)
    {
        if (File.Exists(targetFile)) return null;
        if (!File.Exists(legacyFile)) return null;

        try
        {
            File.Copy(legacyFile, targetFile);
            return null;
        }
        catch (Exception ex)
        {
            return $"تعذّر نقل ملف الإعدادات من {legacyFile} إلى {targetFile}: {ex.Message}. " +
                   "ستُستعمل الإعدادات الافتراضية، ولم تتأثر قاعدة البيانات.";
        }
    }

    public static string DefaultAccentColor => "#1F5A66";

    public static bool IsDarkMode
    {
        get => Read("Theme") == "Dark";
        set => Write("Theme", value ? "Dark" : "Light");
    }

    public static string AccentColor
    {
        get => Read("AccentColor") ?? DefaultAccentColor;
        set => Write("AccentColor", string.IsNullOrWhiteSpace(value) ? DefaultAccentColor : value);
    }

    public static bool GetUserTheme(string? username)
    {
        if (string.IsNullOrWhiteSpace(username))
            return IsDarkMode;

        var userKey = $"User_{username.Trim().ToLower()}_Theme";
        var val = Read(userKey);
        if (val != null)
            return val == "Dark";

        return IsDarkMode;
    }

    public static void SetUserTheme(string? username, bool isDark)
    {
        if (!string.IsNullOrWhiteSpace(username))
        {
            var userKey = $"User_{username.Trim().ToLower()}_Theme";
            Write(userKey, isDark ? "Dark" : "Light");
        }
        else
        {
            IsDarkMode = isDark;
        }
    }

    public static string GetUserAccentColor(string? username)
    {
        if (string.IsNullOrWhiteSpace(username))
            return AccentColor;

        var userKey = $"User_{username.Trim().ToLower()}_AccentColor";
        var val = Read(userKey);
        if (!string.IsNullOrWhiteSpace(val))
            return val;

        return AccentColor;
    }

    public static void SetUserAccentColor(string? username, string hexColor)
    {
        var color = string.IsNullOrWhiteSpace(hexColor) ? DefaultAccentColor : hexColor.Trim();
        if (!string.IsNullOrWhiteSpace(username))
        {
            var userKey = $"User_{username.Trim().ToLower()}_AccentColor";
            Write(userKey, color);
        }
        else
        {
            AccentColor = color;
        }
    }

    public static string Language
    {
        get => Read("Language") ?? "ar";
        set => Write("Language", value);
    }

    public static bool RememberMe
    {
        get => Read("RememberMe") == "True";
        set => Write("RememberMe", value ? "True" : "False");
    }

    public static string SavedUsername
    {
        get => Read("SavedUsername") ?? string.Empty;
        set => Write("SavedUsername", value);
    }

    public static void ClearAllUserSettingsForTesting()
    {
        if (File.Exists(SettingsFile))
        {
            try { File.Delete(SettingsFile); } catch { }
        }
    }

    private static string? Read(string key)
    {
        if (!File.Exists(SettingsFile)) return null;
        foreach (var line in File.ReadAllLines(SettingsFile))
        {
            var parts = line.Split('=', 2);
            if (parts.Length == 2 && parts[0].Trim() == key)
                return parts[1].Trim();
        }
        return null;
    }

    private static void Write(string key, string value)
    {
        var lines = File.Exists(SettingsFile) ? File.ReadAllLines(SettingsFile).ToList() : new List<string>();
        var found = false;
        for (int i = 0; i < lines.Count; i++)
        {
            if (lines[i].StartsWith(key + "="))
            {
                lines[i] = $"{key}={value}";
                found = true;
                break;
            }
        }
        if (!found) lines.Add($"{key}={value}");
        File.WriteAllLines(SettingsFile, lines);
    }
}
