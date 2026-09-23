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

    /// <summary>مجلد ملفات السجل (Logs).</summary>
    public static string LogsDirectory => Path.Combine(AppDataDirectory, "Logs");

    /// <summary>المسار القديم للقاعدة بجوار الملف التنفيذي، قبل الانتقال إلى LocalAppData.</summary>
    public static string LegacyDbPath => Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, DatabaseFileName);

    public static string EnsureAppDataDirectory()
    {
        Directory.CreateDirectory(AppDataDirectory);
        return AppDataDirectory;
    }

    /// <summary>مجلد بيانات الترخيص المشتركة بين كل المستخدمين على الجهاز (ProgramData) — يحتاج صلاحيات مدير للكتابة أول مرة فقط عبر المثبِّت.</summary>
    public static string LicenseDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "Sources");

    /// <summary>مسار ملف علامة التفعيل المشفَّرة (DPAPI، نطاق الجهاز).</summary>
    public static string LicenseFilePath => Path.Combine(LicenseDirectory, "license.dat");

    public static string EnsureLicenseDirectory()
    {
        Directory.CreateDirectory(LicenseDirectory);
        return LicenseDirectory;
    }
}
