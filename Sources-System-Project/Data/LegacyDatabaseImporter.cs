using System;
using System.Collections.Generic;
using System.IO;

namespace Sources.Data;

/// <summary>يُرمى عند فشل استيراد القاعدة من المسار القديم. رسالته تُعرض للمستخدم كما هي.</summary>
public class LegacyDatabaseImportException : Exception
{
    public LegacyDatabaseImportException(string message, Exception innerException)
        : base(message, innerException) { }
}

/// <summary>
/// استيراد قاعدة البيانات لمرة واحدة من المسار القديم (بجوار الملف التنفيذي) إلى LocalAppData.
/// يُستدعى صراحةً عند الإقلاع. ممنوع استدعاؤه من OnConfiguring.
/// </summary>
public static class LegacyDatabaseImporter
{
    private static readonly string[] SideFileSuffixes = { "-wal", "-shm" };

    /// <summary>يستورد من المسارات الافتراضية. يُرجع true إن تمّ استيراد فعلي.</summary>
    public static bool ImportIfNeeded() => Import(DatabasePaths.LegacyDbPath, DatabasePaths.DbPath);

    /// <summary>
    /// يُرجع false بلا أثر إن كانت الوجهة موجودة أو المصدر غير موجود.
    /// يرمي <see cref="LegacyDatabaseImportException"/> عند أي فشل، بعد إزالة ما كُتب.
    /// </summary>
    public static bool Import(string legacyDbPath, string targetDbPath)
    {
        if (File.Exists(targetDbPath)) return false;
        if (!File.Exists(legacyDbPath)) return false;

        var targetDirectory = Path.GetDirectoryName(targetDbPath);
        if (!string.IsNullOrEmpty(targetDirectory)) Directory.CreateDirectory(targetDirectory);

        var written = new List<string>();
        var tempPath = targetDbPath + ".import-tmp";

        try
        {
            // ملفات WAL أولاً وملف القاعدة أخيراً بنقل ذرّي:
            // لو فشل الأخير لا يوجد ملف قاعدة في الوجهة، فتُعاد المحاولة في الإقلاع التالي
            // بدل أن تُهجر بيانات الـ WAL صامتة.
            foreach (var suffix in SideFileSuffixes)
            {
                var sideSource = legacyDbPath + suffix;
                if (!File.Exists(sideSource)) continue;
                var sideTarget = targetDbPath + suffix;
                File.Copy(sideSource, sideTarget, overwrite: true);
                written.Add(sideTarget);
            }

            if (File.Exists(tempPath)) File.Delete(tempPath);
            File.Copy(legacyDbPath, tempPath);
            written.Add(tempPath);

            File.Move(tempPath, targetDbPath);
            return true;
        }
        catch (Exception ex)
        {
            var cleanupFailures = new List<string>();
            foreach (var path in written)
            {
                try { if (File.Exists(path)) File.Delete(path); }
                catch (Exception cleanupEx) { cleanupFailures.Add($"{path} ({cleanupEx.Message})"); }
            }

            var message =
                "تعذّر استيراد قاعدة البيانات من المسار القديم، ولم يُفتح البرنامج على قاعدة فارغة.\n" +
                $"المصدر: {legacyDbPath}\n" +
                $"الوجهة: {targetDbPath}\n" +
                $"السبب: {ex.Message}\n" +
                "انسخ ملف القاعدة يدوياً إلى مجلد الوجهة ثم أعد التشغيل، أو راجع صلاحيات المجلد.";

            if (cleanupFailures.Count > 0)
                message += "\nملفات مؤقتة تعذّر حذفها ويلزم حذفها يدوياً: " + string.Join("، ", cleanupFailures);

            throw new LegacyDatabaseImportException(message, ex);
        }
    }
}
