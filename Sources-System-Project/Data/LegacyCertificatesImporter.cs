using System;
using System.IO;
using Sources.Services;

namespace Sources.Data;

/// <summary>
/// استيراد مجلد الشهادات لمرة واحدة من المسار القديم (بجوار الملف التنفيذي) إلى LocalAppData.
/// يُستدعى صراحةً عند الإقلاع، بعد <see cref="LegacyDatabaseImporter"/> مباشرة.
/// بخلاف استيراد القاعدة، فشل استيراد الشهادات (كلياً أو جزئياً) لا يجب أن يمنع فتح البرنامج:
/// يُسجَّل تحذير عبر <see cref="LoggerService.LogWarning"/> ولا يُرمى أي استثناء يوقف الإقلاع.
/// النسخ فقط، لا نقل: المجلد القديم يبقى كما هو ولا يُحذف.
/// </summary>
public static class LegacyCertificatesImporter
{
    /// <summary>يستورد من المسارات الافتراضية.</summary>
    public static void ImportIfNeeded() =>
        Import(
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Certificates"),
            Path.Combine(DatabasePaths.AppDataDirectory, "Certificates"));

    /// <summary>
    /// ينسخ محتوى مجلد الشهادات القديم إلى الجديد فقط إذا كان القديم موجوداً وبه ملفات
    /// والجديد فارغاً أو غير موجود. لا يحذف المصدر القديم. فشل نسخ ملف واحد لا يوقف البقية
    /// ولا يرمي استثناء؛ يُسجَّل تحذيراً فقط.
    /// </summary>
    public static void Import(string legacyCertificatesFolder, string targetCertificatesFolder)
    {
        try
        {
            if (!Directory.Exists(legacyCertificatesFolder)) return;

            var legacyFiles = Directory.GetFiles(legacyCertificatesFolder, "*", SearchOption.AllDirectories);
            if (legacyFiles.Length == 0) return;

            if (Directory.Exists(targetCertificatesFolder) &&
                Directory.GetFiles(targetCertificatesFolder, "*", SearchOption.AllDirectories).Length > 0)
                return;

            Directory.CreateDirectory(targetCertificatesFolder);

            foreach (var sourceFile in legacyFiles)
            {
                try
                {
                    var relativePath = Path.GetRelativePath(legacyCertificatesFolder, sourceFile);
                    var targetFile = Path.Combine(targetCertificatesFolder, relativePath);

                    var targetFileDirectory = Path.GetDirectoryName(targetFile);
                    if (!string.IsNullOrEmpty(targetFileDirectory))
                        Directory.CreateDirectory(targetFileDirectory);

                    File.Copy(sourceFile, targetFile, overwrite: false);
                }
                catch (Exception fileEx)
                {
                    LoggerService.LogWarning(
                        $"تعذّر نسخ ملف الشهادة من المسار القديم: {sourceFile} — السبب: {fileEx.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            LoggerService.LogWarning(
                $"تعذّر استيراد مجلد الشهادات من المسار القديم ({legacyCertificatesFolder}) إلى ({targetCertificatesFolder}) — السبب: {ex.Message}");
        }
    }
}
