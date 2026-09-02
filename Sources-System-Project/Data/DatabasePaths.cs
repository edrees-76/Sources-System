using System;
using System.IO;

namespace Sources.Data;

/// <summary>
/// المصدر الوحيد لمسارات قاعدة البيانات ومجلداتها.
/// أي موضع يحتاج مسار القاعدة أو مجلد النسخ الاحتياطي يأخذه من هنا، ولا يعيد تركيبه.
/// </summary>
public static class DatabasePaths
{
    public const string DatabaseFileName = "Sources.db";

    /// <summary>مجلد بيانات البرنامج في LocalAppData — لا يحتاج صلاحيات مدير.</summary>
    public static string AppDataDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Sources");

    public static string DbPath => Path.Combine(AppDataDirectory, DatabaseFileName);

    public static string BackupsDirectory => Path.Combine(AppDataDirectory, "Backups");

    /// <summary>المسار القديم للقاعدة بجوار الملف التنفيذي، قبل الانتقال إلى LocalAppData.</summary>
    public static string LegacyDbPath => Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, DatabaseFileName);

    public static string EnsureAppDataDirectory()
    {
        Directory.CreateDirectory(AppDataDirectory);
        return AppDataDirectory;
    }
}
