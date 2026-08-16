using System;
using System.IO;
using System.Linq;
using Sources.Data;

namespace Sources.Services;

/// <summary>
/// خدمة النسخ الاحتياطي والاستعادة لقاعدة البيانات
/// </summary>
public class BackupService : IBackupService
{
    private readonly string _dbPath;
    private readonly string _backupDir;

    public BackupService()
    {
        var appDataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Sources");
        _dbPath = Path.Combine(appDataDir, "Sources.db");
        _backupDir = Path.Combine(appDataDir, "Backups");
        
        if (!Directory.Exists(_backupDir))
            Directory.CreateDirectory(_backupDir);
    }

    public BackupService(string? customDbPath = null, string? customBackupDir = null)
    {
        var appDataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Sources");
        _dbPath = !string.IsNullOrEmpty(customDbPath) ? customDbPath : Path.Combine(appDataDir, "Sources.db");
        _backupDir = !string.IsNullOrEmpty(customBackupDir) ? customBackupDir : Path.Combine(appDataDir, "Backups");

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

    /// <summary>إنشاء نسخة احتياطية في مسار مخصص</summary>
    public (bool Success, string Message, string? BackupPath) CreateBackup(string customPath)
    {
        try
        {
            if (!File.Exists(_dbPath))
                return (false, "قاعدة البيانات غير موجودة", null);

            // إنشاء المجلد بالاسم المطلوب داخل المسار المختار (إن لم يكن هو نفسه مجلد النسخ الاحتياطي الحديث أو القديم)
            var targetDir = (customPath.EndsWith(BackupFolderName, StringComparison.OrdinalIgnoreCase) ||
                             customPath.EndsWith(LegacyBackupFolderName, StringComparison.OrdinalIgnoreCase))
                ? customPath
                : Path.Combine(customPath, BackupFolderName);

            if (!Directory.Exists(targetDir))
                Directory.CreateDirectory(targetDir);

            var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            var backupFile = Path.Combine(targetDir, $"SOURCES_backup_{timestamp}.db");

            File.Copy(_dbPath, backupFile, overwrite: true);

            // حذف النسخ الأقدم من 30 يوماً
            CleanOldBackups(30, targetDir);

            LoggerService.LogInfo($"تم إنشاء نسخة احتياطية: {backupFile}");
            return (true, $"تم إنشاء النسخة الاحتياطية بنجاح\n{backupFile}", backupFile);
        }
        catch (Exception ex)
        {
            LoggerService.LogError("خطأ أثناء إنشاء النسخة الاحتياطية", ex);
            return (false, $"خطأ: {ex.Message}", null);
        }
    }

    /// <summary>استعادة من نسخة احتياطية</summary>
    public (bool Success, string Message) RestoreBackup(string backupFilePath)
    {
        try
        {
            if (!File.Exists(backupFilePath))
                return (false, "ملف النسخة الاحتياطية غير موجود");

            // نسخة أمان قبل الاستعادة
            var safetyBackup = Path.Combine(_backupDir, $"SOURCES_pre_restore_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.db");
            if (File.Exists(_dbPath))
                File.Copy(_dbPath, safetyBackup, overwrite: true);

            File.Copy(backupFilePath, _dbPath, overwrite: true);

            LoggerService.LogInfo($"تمت الاستعادة من: {backupFilePath}");
            return (true, "تمت الاستعادة بنجاح. يُرجى إعادة تشغيل التطبيق.");
        }
        catch (Exception ex)
        {
            LoggerService.LogError("خطأ أثناء الاستعادة", ex);
            return (false, $"خطأ: {ex.Message}");
        }
    }

    /// <summary>جلب قائمة النسخ الاحتياطية</summary>
    public List<BackupInfo> GetBackups()
    {
        if (!Directory.Exists(_backupDir))
            return new List<BackupInfo>();

        var files = Directory.GetFiles(_backupDir, "*_backup_*.db")
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
            var cutoff = DateTime.Now.AddDays(-maxAgeDays);
            var oldFiles = Directory.GetFiles(targetDir, "*_backup_*.db")
                .Select(f => new FileInfo(f))
                .Where(f => f.CreationTime < cutoff);

            foreach (var file in oldFiles)
                file.Delete();
        }
        catch { }
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
