using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Sources.Data;

namespace Sources.Services;

/// <summary>
/// خدمة النسخ الاحتياطي والاستعادة لقاعدة البيانات والشهادات
/// </summary>
public class BackupService : IBackupService
{
    private readonly string _dbPath;
    private readonly string _backupDir;
    private readonly string _certificatesFolder;

    public BackupService()
    {
        var appDataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Sources");
        _dbPath = Path.Combine(appDataDir, "Sources.db");
        _backupDir = Path.Combine(appDataDir, "Backups");
        _certificatesFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Certificates");

        if (!Directory.Exists(_backupDir))
            Directory.CreateDirectory(_backupDir);
    }

    public BackupService(string? customDbPath = null, string? customBackupDir = null, string? customCertificatesFolder = null)
    {
        var appDataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Sources");
        _dbPath = !string.IsNullOrEmpty(customDbPath) ? customDbPath : Path.Combine(appDataDir, "Sources.db");
        _backupDir = !string.IsNullOrEmpty(customBackupDir) ? customBackupDir : Path.Combine(appDataDir, "Backups");
        _certificatesFolder = !string.IsNullOrEmpty(customCertificatesFolder)
            ? customCertificatesFolder
            : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Certificates");

        if (!Directory.Exists(_backupDir))
            Directory.CreateDirectory(_backupDir);
    }

    public const string BackupFolderName = "النسخ الاحتياطي لمنظومة مصادر";
    public const string LegacyBackupFolderName = "النسخ الاحتياطى منظومة مسار";

    /// <summary>إنشاء نسخة احتياطية في المسار الافتراضي</summary>
    public (bool Success, string Message, string? BackupPath) CreateBackup()
    {
        return CreateBackup(_backupDir);
    }

    /// <summary>إنشاء نسخة احتياطية بصيغة ZIP تحتوي على DB ومجلد Certificates</summary>
    public (bool Success, string Message, string? BackupPath) CreateBackup(string customPath)
    {
        string? tempDbFile = null;
        try
        {
            if (!File.Exists(_dbPath))
                return (false, "قاعدة البيانات غير موجودة", null);

            var targetDir = (customPath.EndsWith(BackupFolderName, StringComparison.OrdinalIgnoreCase) ||
                             customPath.EndsWith(LegacyBackupFolderName, StringComparison.OrdinalIgnoreCase))
                ? customPath
                : Path.Combine(customPath, BackupFolderName);

            if (!Directory.Exists(targetDir))
                Directory.CreateDirectory(targetDir);

            var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            var zipFile = Path.Combine(targetDir, $"SOURCES_backup_{timestamp}.zip");

            if (File.Exists(zipFile))
            {
                File.Delete(zipFile);
            }

            // 1. أخذ نسخة ذرية مؤقتة عبر SQLite VACUUM INTO
            tempDbFile = Path.Combine(Path.GetTempPath(), $"temp_backup_{Guid.NewGuid():N}.db");
            if (File.Exists(tempDbFile))
            {
                File.Delete(tempDbFile);
            }

            var connStr = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder
            {
                DataSource = _dbPath,
                Mode = Microsoft.Data.Sqlite.SqliteOpenMode.ReadOnly,
                DefaultTimeout = 5
            }.ToString();

            using (var conn = new Microsoft.Data.Sqlite.SqliteConnection(connStr))
            {
                conn.Open();
                using var cmd = conn.CreateCommand();
                var escapedPath = tempDbFile.Replace("'", "''");
                cmd.CommandText = $"VACUUM INTO '{escapedPath}';";
                cmd.ExecuteNonQuery();
            }

            // 2. إنشاء أرشيف ZIP وتضمين DB و Certificates
            using (var zipArchive = ZipFile.Open(zipFile, ZipArchiveMode.Create))
            {
                // إضافة قاعدة البيانات
                zipArchive.CreateEntryFromFile(tempDbFile, "Sources.db", CompressionLevel.Optimal);

                // إضافة مجلد الشهادات كاملاً إن وجد
                if (Directory.Exists(_certificatesFolder))
                {
                    var certFiles = Directory.GetFiles(_certificatesFolder, "*.*", SearchOption.AllDirectories);
                    foreach (var certFile in certFiles)
                    {
                        var relativePath = Path.GetRelativePath(_certificatesFolder, certFile).Replace('\\', '/');
                        var entryName = $"Certificates/{relativePath}";
                        zipArchive.CreateEntryFromFile(certFile, entryName, CompressionLevel.Optimal);
                    }
                }
            }

            // 3. حذف ملف DB المؤقت
            if (File.Exists(tempDbFile))
            {
                try { File.Delete(tempDbFile); } catch { }
            }

            // حذف النسخ الأقدم من 30 يوماً
            CleanOldBackups(30, targetDir);

            LoggerService.LogInfo($"تم إنشاء نسخة احتياطية كاملة (ZIP): {zipFile}");
            return (true, $"تم إنشاء النسخة الاحتياطية بنجاح\n\u2066{zipFile}\u2069", zipFile);
        }
        catch (Exception ex)
        {
            if (tempDbFile != null && File.Exists(tempDbFile))
            {
                try { File.Delete(tempDbFile); } catch { }
            }

            LoggerService.LogError("خطأ أثناء إنشاء النسخة الاحتياطية", ex);
            return (false, $"خطأ: {ex.Message}", null);
        }
    }

    /// <summary>استعادة من نسخة احتياطية (يدعم ZIP الجديد و DB القديم)</summary>
    public (bool Success, string Message) RestoreBackup(string backupFilePath)
    {
        string? safetyCertDir = null;
        try
        {
            if (!File.Exists(backupFilePath))
                return (false, "ملف النسخة الاحتياطية غير موجود");

            var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");

            // 1. نسخة أمان وقائية مزدوجة قبل أي استبدال
            var safetyBackupDb = Path.Combine(_backupDir, $"SOURCES_pre_restore_{timestamp}.db");
            if (File.Exists(_dbPath))
            {
                File.Copy(_dbPath, safetyBackupDb, overwrite: true);
            }

            var certParent = Path.GetDirectoryName(_certificatesFolder) ?? AppDomain.CurrentDomain.BaseDirectory;
            safetyCertDir = Path.Combine(certParent, $"Certificates_pre_restore_{timestamp}");
            if (Directory.Exists(_certificatesFolder))
            {
                CopyDirectory(_certificatesFolder, safetyCertDir);
            }

            var isZip = backupFilePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);

            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

            if (isZip)
            {
                // استعادة من ملف ZIP
                using (var archive = ZipFile.OpenRead(backupFilePath))
                {
                    // أ. استخراج واستبدال قاعدة البيانات
                    var dbEntry = archive.Entries.FirstOrDefault(e =>
                        e.FullName.Equals("Sources.db", StringComparison.OrdinalIgnoreCase) ||
                        e.Name.EndsWith(".db", StringComparison.OrdinalIgnoreCase));

                    if (dbEntry == null)
                        return (false, "ملف النسخة الاحتياطية المضغوط لا يحتوي على ملف قاعدة البيانات.");

                    var tempExtractedDb = Path.Combine(Path.GetTempPath(), $"temp_restore_{Guid.NewGuid():N}.db");
                    dbEntry.ExtractToFile(tempExtractedDb, overwrite: true);

                    // استبدال ملف قاعدة البيانات الحالي
                    File.Copy(tempExtractedDb, _dbPath, overwrite: true);
                    try { File.Delete(tempExtractedDb); } catch { }

                    // ب. استبدال مجلد Certificates بالكامل
                    if (!Directory.Exists(_certificatesFolder))
                    {
                        Directory.CreateDirectory(_certificatesFolder);
                    }
                    else
                    {
                        // مسح الملفات الحالية داخل مجلد Certificates
                        var currentFiles = Directory.GetFiles(_certificatesFolder, "*.*", SearchOption.AllDirectories);
                        foreach (var f in currentFiles)
                        {
                            try { File.Delete(f); } catch { }
                        }
                    }

                    // استخراج ملفات الشهادات من الـ ZIP
                    var certEntries = archive.Entries
                        .Where(e => e.FullName.StartsWith("Certificates/", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(e.Name))
                        .ToList();

                    foreach (var entry in certEntries)
                    {
                        var relativePath = entry.FullName.Substring("Certificates/".Length);
                        var destinationFile = Path.Combine(_certificatesFolder, relativePath.Replace('/', Path.DirectorySeparatorChar));
                        var destFolder = Path.GetDirectoryName(destinationFile);
                        if (!string.IsNullOrEmpty(destFolder) && !Directory.Exists(destFolder))
                        {
                            Directory.CreateDirectory(destFolder);
                        }
                        entry.ExtractToFile(destinationFile, overwrite: true);
                    }
                }
            }
            else
            {
                // استعادة ملف .db مباشر (نسخ سابقة - Backward Compatibility)
                File.Copy(backupFilePath, _dbPath, overwrite: true);
            }

            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

            // 2. التحقق من توافق المخطط عبر فحص جدول __EFMigrationsHistory بـ SQLite مباشر
            bool isCompatible = false;
            try
            {
                var connStr = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder
                {
                    DataSource = _dbPath,
                    Mode = Microsoft.Data.Sqlite.SqliteOpenMode.ReadOnly,
                    DefaultTimeout = 5
                }.ToString();

                using (var conn = new Microsoft.Data.Sqlite.SqliteConnection(connStr))
                {
                    conn.Open();
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='__EFMigrationsHistory';";
                    var tableCount = Convert.ToInt64(cmd.ExecuteScalar());
                    if (tableCount > 0)
                    {
                        cmd.CommandText = "SELECT COUNT(*) FROM \"__EFMigrationsHistory\" WHERE \"MigrationId\" LIKE '%InitialSchema%';";
                        var migrationCount = Convert.ToInt64(cmd.ExecuteScalar());
                        isCompatible = migrationCount > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                LoggerService.LogWarning($"فشل فحص توافق المخطط في النسخة المستعادة: {ex.Message}");
                isCompatible = false;
            }

            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

            if (!isCompatible)
            {
                // استرجاع النسخة الوقائية السابقة لقاعدة البيانات
                if (File.Exists(safetyBackupDb))
                {
                    File.Copy(safetyBackupDb, _dbPath, overwrite: true);
                }

                // استرجاع النسخة الوقائية السابقة لمجلد الشهادات
                if (safetyCertDir != null && Directory.Exists(safetyCertDir))
                {
                    if (Directory.Exists(_certificatesFolder))
                    {
                        try { Directory.Delete(_certificatesFolder, recursive: true); } catch { }
                    }
                    CopyDirectory(safetyCertDir, _certificatesFolder);
                }

                Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

                string incompatibleMsg = "النسخة الاحتياطية أُنشئت بإصدار أقدم من المنظومة وبنية قاعدة البيانات تغيّرت، ولا يمكن استعادتها.";
                LoggerService.LogWarning(incompatibleMsg);
                return (false, incompatibleMsg);
            }

            // بعد نجاح الاستعادة الكاملة بدون أخطاء: حذف مجلد النسخة الاحتياطية المؤقتة للشهادات
            if (safetyCertDir != null && Directory.Exists(safetyCertDir))
            {
                try { Directory.Delete(safetyCertDir, recursive: true); } catch { }
            }

            LoggerService.LogInfo($"تمت الاستعادة بنجاح من: {backupFilePath}");
            return (true, "تمت الاستعادة بنجاح. يُرجى إعادة تشغيل التطبيق.");
        }
        catch (Exception ex)
        {
            // في حالة حدوث أي خطأ: يتم الإبقاء على مجلد safetyCertDir كما هو
            LoggerService.LogError("خطأ أثناء الاستعادة", ex);
            return (false, $"خطأ أثناء الاستعادة: {ex.Message}");
        }
    }

    /// <summary>جلب قائمة النسخ الاحتياطية (يشمل ZIP و DB مرتبة زمنياً بتأريخ الإنشاء)</summary>
    public List<BackupInfo> GetBackups()
    {
        if (!Directory.Exists(_backupDir))
            return new List<BackupInfo>();

        var files = Directory.GetFiles(_backupDir, "*.*")
            .Where(f => (f.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".db", StringComparison.OrdinalIgnoreCase))
                        && Path.GetFileName(f).Contains("_backup_", StringComparison.OrdinalIgnoreCase))
            .Select(f => new FileInfo(f))
            .OrderByDescending(f => f.CreationTime)
            .Select(f => new BackupInfo
            {
                FilePath = f.FullName,
                FileName = f.Name,
                CreatedAt = f.CreationTime,
                SizeBytes = f.Length,
                SizeDisplay = FormatSize(f.Length)
            })
            .ToList();

        return files;
    }

    /// <summary>حذف النسخ الأقدم من عدد أيام محدد</summary>
    private void CleanOldBackups(int maxAgeDays, string? dir = null)
    {
        try
        {
            var targetDir = dir ?? _backupDir;
            if (!Directory.Exists(targetDir)) return;

            var cutoff = DateTime.Now.AddDays(-maxAgeDays);
            var oldFiles = Directory.GetFiles(targetDir, "*.*")
                .Where(f => (f.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".db", StringComparison.OrdinalIgnoreCase))
                            && Path.GetFileName(f).Contains("_backup_", StringComparison.OrdinalIgnoreCase))
                .Select(f => new FileInfo(f))
                .Where(f => f.CreationTime < cutoff);

            foreach (var file in oldFiles)
            {
                try { file.Delete(); } catch { }
            }
        }
        catch { }
    }

    private static void CopyDirectory(string sourceDir, string destinationDir)
    {
        if (!Directory.Exists(destinationDir))
        {
            Directory.CreateDirectory(destinationDir);
        }

        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var destFile = Path.Combine(destinationDir, Path.GetFileName(file));
            File.Copy(file, destFile, overwrite: true);
        }

        foreach (var subDir in Directory.GetDirectories(sourceDir))
        {
            var destSubDir = Path.Combine(destinationDir, Path.GetFileName(subDir));
            CopyDirectory(subDir, destSubDir);
        }
    }

    private string FormatSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        return $"{bytes / (1024.0 * 1024.0):F1} MB";
    }
}

public class BackupInfo
{
    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public long SizeBytes { get; set; }
    public string SizeDisplay { get; set; } = string.Empty;
}
