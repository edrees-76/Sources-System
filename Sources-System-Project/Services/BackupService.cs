using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Sources.Data;
using Sources.Helpers;

namespace Sources.Services;

/// <summary>
/// خدمة النسخ الاحتياطي والاستعادة لقاعدة البيانات والشهادات
/// </summary>
public class BackupService : IBackupService
{
    private readonly string _dbPath;
    private readonly string _backupDir;
    private readonly string _certificatesFolder;
    private readonly ILicenseService? _licenseService;
    private readonly IUserService? _userService;

    public BackupService() : this(null, null, null, null, null) { }

    public BackupService(string? customDbPath = null, string? customBackupDir = null, string? customCertificatesFolder = null, ILicenseService? licenseService = null, IUserService? userService = null)
    {
        _licenseService = licenseService;
        _userService = userService;
        _dbPath = !string.IsNullOrEmpty(customDbPath) ? customDbPath : DatabasePaths.DbPath;
        _backupDir = !string.IsNullOrEmpty(customBackupDir) ? customBackupDir : DatabasePaths.BackupsDirectory;
        _certificatesFolder = !string.IsNullOrEmpty(customCertificatesFolder)
            ? customCertificatesFolder
            : Path.Combine(DatabasePaths.AppDataDirectory, "Certificates");

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

    /// <summary>إنشاء نسخة احتياطية إجبارية دائمة قبل إعادة ضبط المنظومة (لا تخضع للحذف التلقائي)</summary>
    public (bool Success, string Message, string? BackupPath) CreatePreResetBackup()
    {
        return CreateBackup(_backupDir, isPermanent: true);
    }

    /// <summary>إنشاء نسخة احتياطية بصيغة ZIP تحتوي على DB ومجلد Certificates</summary>
    public (bool Success, string Message, string? BackupPath) CreateBackup(string customPath)
    {
        return CreateBackup(customPath, isPermanent: false);
    }

    public (bool Success, string Message, string? BackupPath) CreateBackup(string customPath, bool isPermanent)
    {
        string? tempDbFile = null;
        try
        {
            if (!File.Exists(_dbPath))
                return (false, TranslationHelper.GetString("MsgErrDatabaseNotFound") ?? "قاعدة البيانات غير موجودة", null);

            var targetDir = (customPath.EndsWith(BackupFolderName, StringComparison.OrdinalIgnoreCase) ||
                             customPath.EndsWith(LegacyBackupFolderName, StringComparison.OrdinalIgnoreCase))
                ? customPath
                : Path.Combine(customPath, BackupFolderName);

            if (!Directory.Exists(targetDir))
                Directory.CreateDirectory(targetDir);

            var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            var prefix = isPermanent ? "SOURCES_pre_reset_" : "SOURCES_backup_";
            var zipFile = Path.Combine(targetDir, $"{prefix}{timestamp}.zip");

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

            if (!isPermanent)
            {
                // حذف النسخ الأقدم من 30 يوماً
                CleanOldBackups(30, targetDir);
            }

            LoggerService.LogInfo($"تم إنشاء نسخة احتياطية كاملة (ZIP): {zipFile}");
            return (true, $"{TranslationHelper.GetString("MsgSuccessBackupCreated") ?? "تم إنشاء النسخة الاحتياطية بنجاح"}\n\u2066{zipFile}\u2069", zipFile);
        }
        catch (Exception ex)
        {
            if (tempDbFile != null && File.Exists(tempDbFile))
            {
                try { File.Delete(tempDbFile); } catch { }
            }

            LoggerService.LogError("خطأ أثناء إنشاء النسخة الاحتياطية", ex);
            return (false, TranslationHelper.GetFormat("MsgErrGeneral", ex.Message), null);
        }
    }

    /// <summary>
    /// استعادة من نسخة احتياطية (يدعم ZIP الجديد و DB القديم).
    /// المراحل (الجولة 209):
    ///   1. التجهيز: استخراج قاعدة البيانات والشهادات إلى مجلد مؤقت معزول، مع رفض أي مدخل ZIP
    ///      يخرج مساره عن مجلد الشهادات (حماية Zip Slip).
    ///   2. التحقق: فحص توافق المخطط على النسخة المُجهَّزة — القاعدة الحية لا تُلمس إن رُفضت النسخة.
    ///   3. نسخ أمان وقائية للقاعدة والشهادات الحالية.
    ///   4. الاستبدال: وأي استثناء بعد بدء الاستبدال يُعيد القاعدة والشهادات لحالتها السابقة.
    /// </summary>
    public (bool Success, string Message) RestoreBackup(string backupFilePath)
    {
        var activation = AuthorizationGuard.RequireActivated(_licenseService!);
        if (!activation.Allowed) return (false, activation.Message);

        // الاستعادة تستبدل قاعدة البيانات كاملة بما فيها جدول المستخدمين والأدوار، فهي عملية مدير نظام حصراً.
        var adminGuard = AuthorizationGuard.RequireAdmin(_userService?.CurrentUser);
        if (!adminGuard.Allowed) return (false, adminGuard.Message);

        string? stagingDir = null;
        string? safetyCertDir = null;
        string? safetyBackupDb = null;
        var dbExistedBefore = false;
        var swapStarted = false;
        try
        {
            if (!File.Exists(backupFilePath))
                return (false, TranslationHelper.GetString("MsgErrBackupFileNotFound") ?? "ملف النسخة الاحتياطية غير موجود");

            CleanOldPreRestoreSafetyCopies(30);

            var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            var isZip = backupFilePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);

            // ─── 1. التجهيز في مجلد مؤقت معزول ───
            // إغلاق الاتصالات المجمَّعة أولاً: إغلاق آخر اتصال بملف .db في وضع WAL يدمج بياناته المُلتزمة
            // في الملف الرئيسي، فيُنسخ الملف كاملاً لا الملف الرئيسي وحده بدون محتوى -wal.
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            stagingDir = Path.Combine(Path.GetTempPath(), $"sources_restore_{Guid.NewGuid():N}");
            Directory.CreateDirectory(stagingDir);
            var stagedDb = Path.Combine(stagingDir, DatabasePaths.DatabaseFileName);
            var stagedCertsDir = Path.Combine(stagingDir, "Certificates");

            if (isZip)
            {
                using var archive = ZipFile.OpenRead(backupFilePath);

                // قاعدة البيانات يجب أن تكون في جذر الأرشيف: Sources.db أولاً، ثم أي ملف .db في الجذر (توافق قديم).
                var dbEntry = archive.Entries.FirstOrDefault(e =>
                                  e.FullName.Equals(DatabasePaths.DatabaseFileName, StringComparison.OrdinalIgnoreCase))
                              ?? archive.Entries.FirstOrDefault(e =>
                                  e.FullName == e.Name && e.Name.EndsWith(".db", StringComparison.OrdinalIgnoreCase));

                if (dbEntry == null)
                    return (false, TranslationHelper.GetString("MsgErrBackupZipMissingDb") ?? "ملف النسخة الاحتياطية المضغوط لا يحتوي على ملف قاعدة البيانات.");

                dbEntry.ExtractToFile(stagedDb, overwrite: true);

                Directory.CreateDirectory(stagedCertsDir);
                var certEntries = archive.Entries
                    .Where(e => e.FullName.StartsWith("Certificates/", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(e.Name))
                    .ToList();

                foreach (var entry in certEntries)
                {
                    var relativePath = entry.FullName.Substring("Certificates/".Length);
                    var destinationFile = Path.GetFullPath(Path.Combine(stagedCertsDir, relativePath.Replace('/', Path.DirectorySeparatorChar)));
                    if (!IsPathInsideDirectory(destinationFile, stagedCertsDir))
                    {
                        LoggerService.LogWarning($"رُفضت الاستعادة: مدخل ZIP بمسار يخرج عن مجلد الشهادات «{entry.FullName}» في «{backupFilePath}».");
                        return (false, TranslationHelper.GetString("MsgErrBackupUnsafeEntry")
                            ?? "ملف النسخة الاحتياطية يحتوي على مسار ملف غير آمن، ولا يمكن استعادته.");
                    }

                    var destFolder = Path.GetDirectoryName(destinationFile);
                    if (!string.IsNullOrEmpty(destFolder))
                        Directory.CreateDirectory(destFolder);
                    entry.ExtractToFile(destinationFile, overwrite: true);
                }
            }
            else
            {
                // استعادة ملف .db مباشر (نسخ سابقة - Backward Compatibility)
                File.Copy(backupFilePath, stagedDb, overwrite: true);
            }

            // ─── 2. التحقق من توافق المخطط على النسخة المُجهَّزة قبل لمس القاعدة الحية ───
            var (isCompatible, incompatibleReason) = CheckSchemaCompatibility(stagedDb);
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (!isCompatible)
            {
                var incompatibleMsg = incompatibleReason
                    ?? TranslationHelper.GetString("MsgErrBackupIncompatibleSchema")
                    ?? "النسخة الاحتياطية غير متوافقة مع بنية قاعدة البيانات الحالية، ولا يمكن استعادتها.";
                LoggerService.LogWarning(incompatibleMsg);
                return (false, incompatibleMsg);
            }

            // ─── 3. نسخة أمان وقائية مزدوجة قبل أي استبدال ───
            // تُستخدم VACUUM INTO بدلاً من File.Copy الخام لضمان تضمين أي بيانات مُلتزمة (committed)
            // موجودة حالياً في ملفات WAL/SHM ولم تُدمج بعد في الملف الرئيسي.
            dbExistedBefore = File.Exists(_dbPath);
            if (dbExistedBefore)
            {
                safetyBackupDb = Path.Combine(_backupDir, $"SOURCES_pre_restore_{timestamp}.db");
                Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
                var safetyConnStr = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder
                {
                    DataSource = _dbPath,
                    Mode = Microsoft.Data.Sqlite.SqliteOpenMode.ReadOnly,
                    DefaultTimeout = 5
                }.ToString();

                using (var safetyConn = new Microsoft.Data.Sqlite.SqliteConnection(safetyConnStr))
                {
                    safetyConn.Open();
                    using var safetyCmd = safetyConn.CreateCommand();
                    var escapedSafetyPath = safetyBackupDb.Replace("'", "''");
                    safetyCmd.CommandText = $"VACUUM INTO '{escapedSafetyPath}';";
                    safetyCmd.ExecuteNonQuery();
                }
            }

            if (isZip && Directory.Exists(_certificatesFolder))
            {
                var certParent = Path.GetDirectoryName(_certificatesFolder) ?? AppDomain.CurrentDomain.BaseDirectory;
                safetyCertDir = Path.Combine(certParent, $"Certificates_pre_restore_{timestamp}");
                CopyDirectory(_certificatesFolder, safetyCertDir);
            }

            // ─── 4. الاستبدال ───
            swapStarted = true;
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

            File.Copy(stagedDb, _dbPath, overwrite: true);
            // حذف ملفات WAL/SHM القديمة العائدة لقاعدة البيانات السابقة كي لا يتم تطبيق
            // إطارات WAL من جيل قاعدة بيانات مختلف فوق الملف الرئيسي الجديد
            DeleteStaleWalShmFiles();

            if (isZip)
            {
                // استبدال محتوى مجلد Certificates بالكامل بمحتوى النسخة
                ClearDirectoryFiles(_certificatesFolder);
                Directory.CreateDirectory(_certificatesFolder);
                CopyDirectory(stagedCertsDir, _certificatesFolder);
            }

            swapStarted = false;
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

            // بعد نجاح الاستعادة الكاملة: حذف مجلد النسخة الوقائية المؤقتة للشهادات
            if (safetyCertDir != null && Directory.Exists(safetyCertDir))
            {
                try
                {
                    Directory.Delete(safetyCertDir, recursive: true);
                }
                catch (Exception ex)
                {
                    LoggerService.LogWarning($"تعذّر حذف مجلد الأمان المؤقت للشهادات: {safetyCertDir}. السبب: {ex.Message}");
                }
            }

            LoggerService.LogInfo($"تمت الاستعادة بنجاح من: {backupFilePath}");
            return (true, TranslationHelper.GetString("MsgSuccessRestoreCompleted") ?? "تمت الاستعادة بنجاح. يُرجى إعادة تشغيل التطبيق.");
        }
        catch (Exception ex)
        {
            LoggerService.LogError("خطأ أثناء الاستعادة", ex);
            if (swapStarted)
            {
                RollBackFailedSwap(dbExistedBefore, safetyBackupDb, safetyCertDir);
            }
            return (false, TranslationHelper.GetFormat("MsgErrRestoreFailed", ex.Message));
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (stagingDir != null && Directory.Exists(stagingDir))
            {
                try
                {
                    Directory.Delete(stagingDir, recursive: true);
                }
                catch (Exception ex)
                {
                    LoggerService.LogWarning($"تعذّر حذف مجلد التجهيز المؤقت للاستعادة: {stagingDir}. السبب: {ex.Message}");
                }
            }
        }
    }

    /// <summary>
    /// فحص توافق مخطط قاعدة بيانات مُجهَّزة عبر جدول __EFMigrationsHistory: يجب أن يوجد الجدول،
    /// وأن يحتوي سجلاً واحداً على الأقل، وألا يحتوي أي ترحيل غير معروف لهذا الإصدار من المنظومة.
    /// </summary>
    private static (bool IsCompatible, string? Reason) CheckSchemaCompatibility(string dbFilePath)
    {
        try
        {
            var knownMigrations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite($"Data Source={dbFilePath}")
                .Options;
            using (var appDb = new AppDbContext(options))
            {
                foreach (var m in appDb.Database.GetMigrations())
                {
                    knownMigrations.Add(m);
                }
            }

            var connStr = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder
            {
                DataSource = dbFilePath,
                Mode = Microsoft.Data.Sqlite.SqliteOpenMode.ReadOnly,
                DefaultTimeout = 5
            }.ToString();

            using var conn = new Microsoft.Data.Sqlite.SqliteConnection(connStr);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='__EFMigrationsHistory';";
            var tableCount = Convert.ToInt64(cmd.ExecuteScalar());
            if (tableCount == 0)
                return (false, TranslationHelper.GetString("MsgErrBackupInvalidNoMigrationTable") ?? "النسخة الاحتياطية غير صالحة ولا تحتوي على جدول ترحيلات المنظومة.");

            cmd.CommandText = "SELECT \"MigrationId\" FROM \"__EFMigrationsHistory\";";
            var restoredMigrations = new List<string>();
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    restoredMigrations.Add(reader.GetString(0));
                }
            }

            if (restoredMigrations.Count == 0)
                return (false, TranslationHelper.GetString("MsgErrBackupNoMigrationHistory") ?? "النسخة الاحتياطية لا تحتوي على أي سجل ترحيلات معتمد، ولا يمكن استعادتها.");

            var unknownMigrations = restoredMigrations.Where(m => !knownMigrations.Contains(m)).ToList();
            if (unknownMigrations.Any())
                return (false, TranslationHelper.GetFormat("MsgErrBackupNewerVersionUnknownMigrations", string.Join(", ", unknownMigrations)));

            return (true, null);
        }
        catch (Exception ex)
        {
            LoggerService.LogWarning($"فشل فحص توافق المخطط في النسخة المستعادة: {ex.Message}");
            return (false, TranslationHelper.GetFormat("MsgErrBackupCompatibilityCheckFailed", ex.Message));
        }
    }

    /// <summary>
    /// التراجع بعد فشل أثناء الاستبدال: إعادة القاعدة من نسخة الأمان الوقائية (أو حذف القاعدة الجديدة إن
    /// لم تكن هناك قاعدة قبل الاستعادة)، وإعادة مجلد الشهادات من نسخته الوقائية.
    /// فشل التراجع نفسه يُسجَّل خطأً ولا يُخفى، وتبقى النسخ الوقائية على القرص للاسترجاع اليدوي.
    /// </summary>
    private void RollBackFailedSwap(bool dbExistedBefore, string? safetyBackupDb, string? safetyCertDir)
    {
        try
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (dbExistedBefore && safetyBackupDb != null && File.Exists(safetyBackupDb))
            {
                File.Copy(safetyBackupDb, _dbPath, overwrite: true);
            }
            else if (!dbExistedBefore && File.Exists(_dbPath))
            {
                File.Delete(_dbPath);
            }
            DeleteStaleWalShmFiles();
        }
        catch (Exception ex)
        {
            LoggerService.LogError($"فشل التراجع عن قاعدة البيانات بعد فشل الاستعادة. نسخة الأمان الوقائية: {safetyBackupDb}", ex);
        }

        if (safetyCertDir != null && Directory.Exists(safetyCertDir))
        {
            try
            {
                if (Directory.Exists(_certificatesFolder))
                    Directory.Delete(_certificatesFolder, recursive: true);
                CopyDirectory(safetyCertDir, _certificatesFolder);
            }
            catch (Exception ex)
            {
                LoggerService.LogError($"فشل التراجع عن مجلد الشهادات بعد فشل الاستعادة. النسخة الوقائية باقية في: {safetyCertDir}", ex);
            }
        }
    }

    /// <summary>هل المسار الكامل المُعطى يقع داخل المجلد المُعطى (لا عليه ولا خارجه).</summary>
    internal static bool IsPathInsideDirectory(string candidateFullPath, string directory)
    {
        var root = Path.GetFullPath(directory);
        if (!root.EndsWith(Path.DirectorySeparatorChar))
            root += Path.DirectorySeparatorChar;
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        return Path.GetFullPath(candidateFullPath).StartsWith(root, comparison);
    }

    /// <summary>حذف كل الملفات داخل مجلد (مع المجلدات الفرعية) مع تسجيل أي ملف تعذّر حذفه.</summary>
    private static void ClearDirectoryFiles(string directory)
    {
        if (!Directory.Exists(directory)) return;
        foreach (var f in Directory.GetFiles(directory, "*.*", SearchOption.AllDirectories))
        {
            try
            {
                File.Delete(f);
            }
            catch (Exception ex)
            {
                LoggerService.LogWarning($"تعذّر حذف ملف الشهادة الحالي أثناء الاستعادة: {f}. السبب: {ex.Message}");
            }
        }
    }

    /// <summary>حذف نسخ الأمان الوقائية السابقة للاستعادة (SOURCES_pre_restore_*.db) الأقدم من عدد أيام محدد.</summary>
    private void CleanOldPreRestoreSafetyCopies(int maxAgeDays)
    {
        try
        {
            if (!Directory.Exists(_backupDir)) return;
            var cutoff = DateTime.Now.AddDays(-maxAgeDays);
            foreach (var file in Directory.GetFiles(_backupDir, "SOURCES_pre_restore_*.db").Select(f => new FileInfo(f)))
            {
                if (file.CreationTime >= cutoff) continue;
                try
                {
                    file.Delete();
                }
                catch (Exception ex)
                {
                    LoggerService.LogWarning($"تعذّر حذف نسخة الأمان الوقائية القديمة «{file.FullName}»: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            LoggerService.LogError("تعذّر تنظيف نسخ الأمان الوقائية القديمة للاستعادة", ex);
        }
    }

    /// <summary>جلب قائمة النسخ الاحتياطية (يشمل ZIP و DB مرتبة زمنياً بتأريخ الإنشاء)</summary>
    public List<BackupInfo> GetBackups()
    {
        if (!Directory.Exists(_backupDir))
            return new List<BackupInfo>();

        var files = Directory.GetFiles(_backupDir, "*.*")
            .Where(f => (f.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".db", StringComparison.OrdinalIgnoreCase))
                        && (Path.GetFileName(f).Contains("_backup_", StringComparison.OrdinalIgnoreCase)
                            || Path.GetFileName(f).Contains("_pre_reset_", StringComparison.OrdinalIgnoreCase)))
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
                try
                {
                    file.Delete();
                }
                catch (Exception ex)
                {
                    LoggerService.LogWarning($"تعذّر حذف ملف النسخة الاحتياطية القديم «{file.FullName}»: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            LoggerService.LogError("تعذّر تنظيف النسخ الاحتياطية القديمة كلياً", ex);
        }
    }

    /// <summary>
    /// حذف ملفات WAL/SHM المتبقية من جيل قاعدة بيانات سابق بعد استبدال أو استرجاع ملف قاعدة البيانات الرئيسي.
    /// فشل حذف أحد الملفين لا يوقف عملية الاستعادة، ويُسجَّل تحذير فقط.
    /// </summary>
    private void DeleteStaleWalShmFiles()
    {
        var walFile = _dbPath + "-wal";
        if (File.Exists(walFile))
        {
            try
            {
                File.Delete(walFile);
            }
            catch (Exception ex)
            {
                LoggerService.LogWarning($"تعذّر حذف ملف WAL القديم بعد الاستعادة: {walFile}. السبب: {ex.Message}");
            }
        }

        var shmFile = _dbPath + "-shm";
        if (File.Exists(shmFile))
        {
            try
            {
                File.Delete(shmFile);
            }
            catch (Exception ex)
            {
                LoggerService.LogWarning($"تعذّر حذف ملف SHM القديم بعد الاستعادة: {shmFile}. السبب: {ex.Message}");
            }
        }
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
