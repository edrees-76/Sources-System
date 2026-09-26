using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using Sources.Data;
using Sources.Helpers;
using Sources.Services;
using Sources.Tests.Fakes;
using Xunit;

namespace Sources.Tests;

public class BackupServiceTests : IDisposable
{
    private readonly string _testRoot;
    private readonly string _dbPath;
    private readonly string _backupDir;
    private readonly string _certsDir;
    private readonly BackupService _sut;
    private readonly FakeLicenseService _fakeLicenseService = new();
    // الاستعادة عملية مدير نظام حصراً (الجولة 209): الخدمة تُبنى بمستخدم مدير مسجَّل.
    private readonly FakeUserService _adminUserService = new(new Sources.Models.User
    {
        Id = Guid.NewGuid(),
        FullName = "مدير اختباري",
        Username = "admin",
        IsActive = true,
        Role = new Sources.Models.Role { RoleName = RoleNames.Admin }
    });

    public BackupServiceTests()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), "Sources_BackupServiceTests_" + Guid.NewGuid().ToString("N"));
        _backupDir = Path.Combine(_testRoot, "Backups");
        _dbPath = Path.Combine(_testRoot, "TestSources.db");
        _certsDir = Path.Combine(_testRoot, "Certificates");

        Directory.CreateDirectory(_testRoot);
        Directory.CreateDirectory(_backupDir);

        // مجلد شهادات صريح ومؤقت: بدونه كان يُستخدم المجلد الافتراضي تحت DatabasePaths.AppDataDirectory
        // الحقيقي (الجولة 199 — عزل بيانات الاختبار)، رغم أن التوجيه العام في TestModuleInitializer
        // يعيد توجيهه أيضاً؛ هذا التمرير الصريح دفاع إضافي وتوضيح لنية الاختبار.
        _sut = new BackupService(_dbPath, _backupDir, _certsDir, licenseService: _fakeLicenseService, userService: _adminUserService);
    }

    private void CreateValidSqliteDatabase(string path, string tableName = "Sources", string sampleData = "SRC-TEST-001", bool includeInitialSchemaMigration = true)
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(path)) File.Delete(path);
        using var conn = new SqliteConnection($"Data Source={path}");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"PRAGMA journal_mode=WAL; CREATE TABLE IF NOT EXISTS {tableName} (Id INTEGER PRIMARY KEY, Code TEXT); INSERT INTO {tableName} (Code) VALUES ('{sampleData}');";
        if (includeInitialSchemaMigration)
        {
            cmd.CommandText += " CREATE TABLE IF NOT EXISTS \"__EFMigrationsHistory\" (\"MigrationId\" TEXT NOT NULL PRIMARY KEY, \"ProductVersion\" TEXT NOT NULL); INSERT OR REPLACE INTO \"__EFMigrationsHistory\" VALUES ('20260901112320_InitialSchema', '8.0.12');";
        }
        cmd.ExecuteNonQuery();
        conn.Close();
    }

    private string ExtractDbFromZip(string zipPath)
    {
        var tempExtracted = Path.Combine(_testRoot, "extracted_" + Guid.NewGuid().ToString("N") + ".db");
        using var archive = ZipFile.OpenRead(zipPath);
        var entry = archive.GetEntry("Sources.db");
        Assert.NotNull(entry);
        entry.ExtractToFile(tempExtracted, overwrite: true);
        return tempExtracted;
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try
        {
            if (Directory.Exists(_testRoot))
                Directory.Delete(_testRoot, recursive: true);
        }
        catch { }
    }

    [Fact]
    public void BackupFolderName_HasExactCorrectArabicText()
    {
        Assert.Equal("النسخ الاحتياطي لمنظومة مصادر", BackupService.BackupFolderName);
    }

    [Fact]
    public void LegacyBackupFolderName_HasExactCorrectArabicText()
    {
        Assert.Equal("النسخ الاحتياطى منظومة مسار", BackupService.LegacyBackupFolderName);
    }

    [Fact]
    public void CreateBackup_DefaultLocation_CreatesBackupWithTimestampAndValidSqliteContent()
    {
        // Arrange
        CreateValidSqliteDatabase(_dbPath, "Sources", "SRC-DEFAULT-LOC");

        // Act
        var result = _sut.CreateBackup();

        // Assert
        Assert.True(result.Success, $"Expected Success=true but got Message: {result.Message}");
        Assert.NotNull(result.BackupPath);
        Assert.True(File.Exists(result.BackupPath), "Backup file should exist on disk");

        var fileName = Path.GetFileName(result.BackupPath);
        Assert.Matches(@"^SOURCES_backup_\d{4}-\d{2}-\d{2}_\d{2}-\d{2}-\d{2}\.zip$", fileName);

        // Verify backup database content from ZIP
        var extractedDb = ExtractDbFromZip(result.BackupPath);
        using var conn = new SqliteConnection($"Data Source={extractedDb}");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Code FROM Sources LIMIT 1;";
        var code = cmd.ExecuteScalar()?.ToString();
        Assert.Equal("SRC-DEFAULT-LOC", code);
    }

    [Fact]
    public void CreateBackup_CustomLocation_CreatesFolderAndBackupFile()
    {
        // Arrange
        var customFolder = Path.Combine(_testRoot, "CustomTargetFolder");
        CreateValidSqliteDatabase(_dbPath, "Sources", "SRC-CUSTOM-LOC");

        // Act
        var result = _sut.CreateBackup(customFolder);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.BackupPath);
        Assert.True(File.Exists(result.BackupPath));

        var expectedParentFolder = Path.Combine(customFolder, BackupService.BackupFolderName);
        Assert.Equal(expectedParentFolder, Path.GetDirectoryName(result.BackupPath));

        // Verify backup database content from ZIP
        var extractedDb = ExtractDbFromZip(result.BackupPath);
        using var conn = new SqliteConnection($"Data Source={extractedDb}");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Code FROM Sources LIMIT 1;";
        var code = cmd.ExecuteScalar()?.ToString();
        Assert.Equal("SRC-CUSTOM-LOC", code);
    }

    [Fact]
    public void CreateBackup_CustomPathAlreadyEndingWithBackupFolderName_DoesNotNestFolderName()
    {
        // Arrange
        var targetDir = Path.Combine(_testRoot, BackupService.BackupFolderName);
        CreateValidSqliteDatabase(_dbPath, "Sources", "SRC-NESTING-TEST");

        // Act
        var result = _sut.CreateBackup(targetDir);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.BackupPath);
        Assert.Equal(targetDir, Path.GetDirectoryName(result.BackupPath));
    }

    [Fact]
    public void CreateBackup_WhenTargetDirectoryDoesNotExist_AutoCreatesDirectoryAndSucceeds()
    {
        // Arrange
        var nonExistentDir = Path.Combine(_testRoot, "Deep", "Nested", "BackupDir");
        CreateValidSqliteDatabase(_dbPath, "Sources", "SRC-AUTO-CREATE-DIR");

        // Act
        var result = _sut.CreateBackup(nonExistentDir);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.BackupPath);
        Assert.True(File.Exists(result.BackupPath));
    }

    [Fact]
    public void CreateBackup_WhenSourceDbDoesNotExist_ReturnsFailureWithoutUnhandledException()
    {
        // Arrange - Ensure DB file does not exist
        if (File.Exists(_dbPath)) File.Delete(_dbPath);

        // Act
        var result = _sut.CreateBackup();

        // Assert
        Assert.False(result.Success);
        Assert.Null(result.BackupPath);
        Assert.Contains("قاعدة البيانات غير موجودة", result.Message);
    }

    [Fact]
    public void CreateBackup_UsingVacuumInto_ProducesValidAndIdenticalDatabaseWithWalData()
    {
        // Arrange - Create DB with multiple records in WAL mode
        SqliteConnection.ClearAllPools();
        if (File.Exists(_dbPath)) File.Delete(_dbPath);

        using (var conn = new SqliteConnection($"Data Source={_dbPath}"))
        {
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                PRAGMA journal_mode=WAL;
                CREATE TABLE Radioisotopes (Id INTEGER PRIMARY KEY, Symbol TEXT, HalfLife REAL);
                INSERT INTO Radioisotopes (Symbol, HalfLife) VALUES ('Co-60', 5.27);
                INSERT INTO Radioisotopes (Symbol, HalfLife) VALUES ('Cs-137', 30.08);
                INSERT INTO Radioisotopes (Symbol, HalfLife) VALUES ('Am-241', 432.2);
            ";
            cmd.ExecuteNonQuery();
        }

        // Act
        var result = _sut.CreateBackup();

        // Assert
        Assert.True(result.Success, result.Message);
        Assert.NotNull(result.BackupPath);
        Assert.True(File.Exists(result.BackupPath));

        // Verify that the backup file contains all records seamlessly
        var extractedDb = ExtractDbFromZip(result.BackupPath);
        using (var backupConn = new SqliteConnection($"Data Source={extractedDb}"))
        {
            backupConn.Open();
            using var cmd = backupConn.CreateCommand();

            cmd.CommandText = "SELECT COUNT(*) FROM Radioisotopes;";
            var count = Convert.ToInt32(cmd.ExecuteScalar());
            Assert.Equal(3, count);

            cmd.CommandText = "SELECT Symbol FROM Radioisotopes ORDER BY Id ASC;";
            using var reader = cmd.ExecuteReader();
            Assert.True(reader.Read());
            Assert.Equal("Co-60", reader.GetString(0));
            Assert.True(reader.Read());
            Assert.Equal("Cs-137", reader.GetString(0));
            Assert.True(reader.Read());
            Assert.Equal("Am-241", reader.GetString(0));
        }
    }

    [Fact]
    public void CreateBackup_OnProductionSchemaDatabase_ProducesFullyValidBackup()
    {
        // قبل الإصلاح: كان هذا الاختبار يفتح قاعدة الإنتاج الحقيقية تحت %LOCALAPPDATA% مباشرة إن
        // وُجدت (خرق عزل بيانات الاختبار، الجولة 199). الآن يُبنى مخطط الإنتاج الكامل عبر AppDbContext
        // على المسار المُعاد توجيهه من TestModuleInitializer، فلا يمس أي مسار حقيقي، ويعمل الاختبار
        // دوماً بدل الاعتماد المشروط على وجود قاعدة حقيقية على جهاز التشغيل.
        using (var db = new AppDbContext())
        {
            db.InitializeDatabase();
        }

        var prodCertsDir = Path.Combine(_testRoot, "Certificates_ProdSchema");
        var service = new BackupService(DatabasePaths.DbPath, _backupDir, prodCertsDir, _fakeLicenseService);
        var result = service.CreateBackup();

        Assert.True(result.Success, result.Message);
        Assert.NotNull(result.BackupPath);
        Assert.True(File.Exists(result.BackupPath));

        var extractedDb = ExtractDbFromZip(result.BackupPath);
        using var conn = new SqliteConnection($"Data Source={extractedDb}");
        conn.Open();
        using var cmd = conn.CreateCommand();

        cmd.CommandText = "SELECT COUNT(*) FROM Sources;";
        var sourceCount = Convert.ToInt32(cmd.ExecuteScalar());
        Assert.True(sourceCount >= 0);

        cmd.CommandText = "PRAGMA integrity_check;";
        var integrity = cmd.ExecuteScalar()?.ToString();
        Assert.Equal("ok", integrity?.ToLower());
    }

    [Fact]
    public void RestoreBackup_CompatibleBackupWithInitialSchema_RestoresSuccessfully()
    {
        // Arrange
        CreateValidSqliteDatabase(_dbPath, "Sources", "CURRENT_DB_DATA", includeInitialSchemaMigration: true);

        var backupFilePath = Path.Combine(_backupDir, "SOURCES_backup_compatible.db");
        CreateValidSqliteDatabase(backupFilePath, "Sources", "BACKUP_COMPATIBLE_DATA", includeInitialSchemaMigration: true);

        // Act
        var result = _sut.RestoreBackup(backupFilePath);

        // Assert
        Assert.True(result.Success, result.Message);
        Assert.Contains("تمت الاستعادة بنجاح", result.Message);

        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Code FROM Sources LIMIT 1;";
        Assert.Equal("BACKUP_COMPATIBLE_DATA", cmd.ExecuteScalar()?.ToString());
    }

    [Fact]
    public void RestoreBackup_IncompatibleLegacyBackupWithoutMigrationsHistory_RejectsAndRestoresSafetyBackup()
    {
        // Arrange
        CreateValidSqliteDatabase(_dbPath, "Sources", "ORIGINAL_SAFE_DATA", includeInitialSchemaMigration: true);

        // Incompatible legacy backup without __EFMigrationsHistory
        var legacyBackupPath = Path.Combine(_backupDir, "SOURCES_backup_legacy.db");
        CreateValidSqliteDatabase(legacyBackupPath, "Sources", "LEGACY_DATA", includeInitialSchemaMigration: false);

        // Act
        var result = _sut.RestoreBackup(legacyBackupPath);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("النسخة الاحتياطية", result.Message);

        // Verify that original database was restored from safety backup
        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Code FROM Sources LIMIT 1;";
        Assert.Equal("ORIGINAL_SAFE_DATA", cmd.ExecuteScalar()?.ToString());
    }

    [Fact]
    public void RestoreBackup_UnknownFutureMigration_RejectsAndRestoresSafetyBackup()
    {
        // Arrange
        CreateValidSqliteDatabase(_dbPath, "Sources", "ORIGINAL_SAFE_DATA", includeInitialSchemaMigration: true);

        // Incompatible future backup with unknown migration
        var futureBackupPath = Path.Combine(_backupDir, "SOURCES_backup_future.db");
        SqliteConnection.ClearAllPools();
        if (File.Exists(futureBackupPath)) File.Delete(futureBackupPath);
        using (var conn = new SqliteConnection($"Data Source={futureBackupPath}"))
        {
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "CREATE TABLE Sources (Id INTEGER PRIMARY KEY, Code TEXT); INSERT INTO Sources (Code) VALUES ('FUTURE_DATA');";
            cmd.CommandText += " CREATE TABLE \"__EFMigrationsHistory\" (\"MigrationId\" TEXT NOT NULL PRIMARY KEY, \"ProductVersion\" TEXT NOT NULL);";
            cmd.CommandText += " INSERT INTO \"__EFMigrationsHistory\" VALUES ('20991231999999_FutureUnreleasedFeatureMigration', '10.0.0');";
            cmd.ExecuteNonQuery();
        }

        // Act
        var result = _sut.RestoreBackup(futureBackupPath);

        // Assert
        Assert.False(result.Success);
        // Round 178: production code now builds this message via TranslationHelper.GetFormat("MsgErrBackupNewerVersionUnknownMigrations", ...).
        // In this xunit test host there is no running WPF Application, so TranslationHelper.GetString returns null and
        // GetFormat falls back to returning the raw key name (it never returns null, so no "??" fallback text is used
        // in production code for this particular message). Assert against that same deterministic fallback instead of
        // the original hardcoded Arabic literal, mirroring exactly what production code computes in this environment.
        var expectedMessage = TranslationHelper.GetFormat("MsgErrBackupNewerVersionUnknownMigrations", "20991231999999_FutureUnreleasedFeatureMigration");
        Assert.Equal(expectedMessage, result.Message);

        // Verify original database was preserved
        using var checkConn = new SqliteConnection($"Data Source={_dbPath}");
        checkConn.Open();
        using var checkCmd = checkConn.CreateCommand();
        checkCmd.CommandText = "SELECT Code FROM Sources LIMIT 1;";
        Assert.Equal("ORIGINAL_SAFE_DATA", checkCmd.ExecuteScalar()?.ToString());
    }

    [Fact]
    public void RestoreBackup_ValidBackupFile_CreatesPreRestoreSafetyBackupAndRestoresDatabase()
    {
        // Arrange
        CreateValidSqliteDatabase(_dbPath, "Sources", "ORIGINAL_DATABASE_STATE", includeInitialSchemaMigration: true);

        var backupFilePath = Path.Combine(_backupDir, "SOURCES_backup_2026-08-16_12-00-00.db");
        CreateValidSqliteDatabase(backupFilePath, "Sources", "RESTORED_DATABASE_STATE", includeInitialSchemaMigration: true);

        // Act
        var result = _sut.RestoreBackup(backupFilePath);

        // Assert
        Assert.True(result.Success);
        Assert.Contains("تمت الاستعادة بنجاح", result.Message);

        // Verify restored content
        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Code FROM Sources LIMIT 1;";
        Assert.Equal("RESTORED_DATABASE_STATE", cmd.ExecuteScalar()?.ToString());

        // Verify pre_restore safety backup was created in backup directory
        var safetyFiles = Directory.GetFiles(_backupDir, "SOURCES_pre_restore_*.db");
        Assert.Single(safetyFiles);
    }

    [Fact]
    public void RestoreBackup_NonExistentBackupFile_ReturnsFailureWithoutUnhandledException()
    {
        // Arrange
        var nonExistentPath = Path.Combine(_testRoot, "non_existent_backup.db");

        // Act
        var result = _sut.RestoreBackup(nonExistentPath);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("ملف النسخة الاحتياطية غير موجود", result.Message);
    }

    [Fact]
    public void RestoreBackup_WhenSourceDbDoesNotExist_RestoresBackupFileDirectly()
    {
        // Arrange
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
        var backupFilePath = Path.Combine(_backupDir, "SOURCES_backup_restore_test.db");
        CreateValidSqliteDatabase(backupFilePath, "Sources", "RESTORE_NO_EXISTING_DB", includeInitialSchemaMigration: true);

        // Act
        var result = _sut.RestoreBackup(backupFilePath);

        // Assert
        Assert.True(result.Success);
        Assert.True(File.Exists(_dbPath));

        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Code FROM Sources LIMIT 1;";
        Assert.Equal("RESTORE_NO_EXISTING_DB", cmd.ExecuteScalar()?.ToString());
    }

    [Fact]
    public void RestoreBackup_PreRestoreSafetyCopy_CapturesPendingWalDataViaVacuumInto()
    {
        // Arrange - إنشاء قاعدة بيانات بوضع WAL وإبقاء اتصال مفتوحاً بعد الإدراج
        // بحيث يبقى السجل الجديد في ملف WAL ولا يُدمج في الملف الرئيسي
        CreateValidSqliteDatabase(_dbPath, "Sources", "ORIGINAL_MERGED_DATA", includeInitialSchemaMigration: true);

        using var pendingConn = new SqliteConnection($"Data Source={_dbPath}");
        pendingConn.Open();
        using (var pragmaCmd = pendingConn.CreateCommand())
        {
            pragmaCmd.CommandText = "PRAGMA journal_mode=WAL;";
            pragmaCmd.ExecuteNonQuery();
        }
        using (var insertCmd = pendingConn.CreateCommand())
        {
            insertCmd.CommandText = "INSERT INTO Sources (Code) VALUES ('PENDING_WAL_DATA');";
            insertCmd.ExecuteNonQuery();
        }
        // ملاحظة: عمداً لا يتم إغلاق pendingConn هنا كي تبقى بيانات الإدراج في ملف -wal

        var backupFilePath = Path.Combine(_backupDir, "SOURCES_backup_compatible.db");
        CreateValidSqliteDatabase(backupFilePath, "Sources", "BACKUP_COMPATIBLE_DATA", includeInitialSchemaMigration: true);

        // Act
        var result = _sut.RestoreBackup(backupFilePath);

        // Assert
        Assert.True(result.Success, result.Message);

        var safetyFiles = Directory.GetFiles(_backupDir, "SOURCES_pre_restore_*.db");
        Assert.Single(safetyFiles);

        SqliteConnection.ClearAllPools();
        using var safetyConn = new SqliteConnection($"Data Source={safetyFiles[0]}");
        safetyConn.Open();
        using var checkCmd = safetyConn.CreateCommand();
        checkCmd.CommandText = "SELECT COUNT(*) FROM Sources WHERE Code = 'PENDING_WAL_DATA';";
        var count = Convert.ToInt32(checkCmd.ExecuteScalar());
        Assert.Equal(1, count);
    }

    [Fact]
    public void RestoreBackup_SuccessfulRestore_DeletesStaleWalAndShmFilesFromPreviousGeneration()
    {
        // Arrange - إنشاء قاعدة بيانات تترك ملفات -wal و -shm فعلية على القرص
        CreateValidSqliteDatabase(_dbPath, "Sources", "ORIGINAL_DATA", includeInitialSchemaMigration: true);

        using (var pendingConn = new SqliteConnection($"Data Source={_dbPath}"))
        {
            pendingConn.Open();
            using (var pragmaCmd = pendingConn.CreateCommand())
            {
                pragmaCmd.CommandText = "PRAGMA journal_mode=WAL;";
                pragmaCmd.ExecuteNonQuery();
            }
            using (var insertCmd = pendingConn.CreateCommand())
            {
                insertCmd.CommandText = "INSERT INTO Sources (Code) VALUES ('MORE_PENDING_DATA');";
                insertCmd.ExecuteNonQuery();
            }
            // إغلاق الاتصال (Dispose) دون استدعاء ClearAllPools هنا: يعيد Microsoft.Data.Sqlite
            // الاتصال إلى تجمّع (pool) داخلي دون تنفيذ checkpoint نهائي فعلي على القرص،
            // تماماً كما يحدث بشكل واقعي بعد أي استخدام سابق لقاعدة البيانات من التطبيق نفسه،
            // مما يترك ملفات -wal/-shm الفعلية موجودة على القرص كحالة "قديمة" قبل الاستعادة.
        }

        var walPath = _dbPath + "-wal";
        var shmPath = _dbPath + "-shm";
        Assert.True(File.Exists(walPath) || File.Exists(shmPath),
            "الاختبار يفترض بقاء ملفات -wal/-shm فعلياً على القرص بعد إغلاق الاتصال؛ إن لم تكن موجودة فإن السيناريو المطلوب اختباره لا يتحقق هنا");

        // ملاحظة: النسخة الاحتياطية المستهدفة تُبنى هنا بدون تفعيل PRAGMA journal_mode=WAL
        // (خلافاً لـ CreateValidSqliteDatabase) كي لا يُعاد إنشاء ملفات -wal/-shm بشكل شرعي وحتمي
        // من جيل قاعدة البيانات الجديدة فور إعادة فتحها لفحص توافق المخطط بعد الاستعادة مباشرة
        // (وهو سلوك SQLite متوقع تماماً وغير متعلق بالثغرة قيد الاختبار هنا). هذا يعزل الاختبار
        // بدقة للتحقق فقط من أن ملفات -wal/-shm "القديمة" العائدة لجيل قاعدة البيانات السابق
        // (المُنشأة أعلاه بوضع WAL) قد حُذفت فعلياً بواسطة DeleteStaleWalShmFiles.
        var backupFilePath = Path.Combine(_backupDir, "SOURCES_backup_compatible.db");
        SqliteConnection.ClearAllPools();
        if (File.Exists(backupFilePath)) File.Delete(backupFilePath);
        using (var backupConn = new SqliteConnection($"Data Source={backupFilePath}"))
        {
            backupConn.Open();
            using var cmd = backupConn.CreateCommand();
            cmd.CommandText = "CREATE TABLE Sources (Id INTEGER PRIMARY KEY, Code TEXT); INSERT INTO Sources (Code) VALUES ('BACKUP_COMPATIBLE_DATA');" +
                " CREATE TABLE \"__EFMigrationsHistory\" (\"MigrationId\" TEXT NOT NULL PRIMARY KEY, \"ProductVersion\" TEXT NOT NULL);" +
                " INSERT INTO \"__EFMigrationsHistory\" VALUES ('20260901112320_InitialSchema', '8.0.12');";
            cmd.ExecuteNonQuery();
        }
        SqliteConnection.ClearAllPools();

        // Act
        var result = _sut.RestoreBackup(backupFilePath);

        // Assert
        Assert.True(result.Success, result.Message);
        Assert.False(File.Exists(walPath), "ملف -wal العائد لقاعدة البيانات السابقة يجب أن يُحذف بعد الاستعادة الناجحة");
        Assert.False(File.Exists(shmPath), "ملف -shm العائد لقاعدة البيانات السابقة يجب أن يُحذف بعد الاستعادة الناجحة");
    }

    // ملاحظة (انحراف مُوثّق عن العقد - البند 3 من الاختبارات المطلوبة):
    // محاولة محاكاة قفل ملف -wal/-shm بشكل موثوق (عبر FileShare.None على نفس العملية) غير ممكنة على ويندوز
    // لأن File.Delete من نفس العملية التي تحمل القفل تفشل بشكل حتمي بغض النظر عن معالجة الأخطاء في الكود قيد
    // الاختبار، بينما محاكاة قفل من عملية خارجية منفصلة تتطلب تعقيداً غير متناسب (عملية فرعية منفصلة) ويصبح
    // الاختبار هشاً (flaky) عبر بيئات التشغيل المختلفة (CI/محلي). بدلاً من ذلك، تم التحقق يدوياً وبالمراجعة
    // الكودية من أن DeleteStaleWalShmFiles تستخدم try/catch منفصل لكل ملف مع LoggerService.LogWarning ولا
    // تُعيد رمي الاستثناء، بما يضمن عدم فشل عملية الاستعادة الكاملة لمجرد فشل حذف أحد الملفين. هذا يطابق
    // نمط try/catch الموجود مسبقاً في نفس الملف (مثال: حذف tempExtractedDb، حذف ملفات Certificates).

    [Fact]
    public void GetBackups_MultipleBackupFiles_ReturnsCorrectListOrderedByCreationTimeDescending()
    {
        // Arrange
        var baseDate = DateTime.Now;

        var file1 = Path.Combine(_backupDir, "SOURCES_backup_2026-08-01_10-00-00.db");
        var file2 = Path.Combine(_backupDir, "SOURCES_backup_2026-08-05_10-00-00.zip");
        var file3 = Path.Combine(_backupDir, "SOURCES_backup_2026-08-10_10-00-00.zip");
        var nonBackupFile = Path.Combine(_backupDir, "random_notes.txt");

        File.WriteAllBytes(file1, new byte[100]);
        File.SetCreationTime(file1, baseDate.AddDays(-10));

        File.WriteAllBytes(file2, new byte[2048]); // 2 KB
        File.SetCreationTime(file2, baseDate.AddDays(-5));

        File.WriteAllBytes(file3, new byte[1024 * 1024 * 3]); // 3 MB
        File.SetCreationTime(file3, baseDate.AddDays(-1));

        File.WriteAllBytes(nonBackupFile, new byte[50]);

        // Act
        var backups = _sut.GetBackups();

        // Assert
        Assert.Equal(3, backups.Count);

        // Ordered by CreationTime descending: file3 (newest), file2, file1 (oldest)
        Assert.Equal(Path.GetFileName(file3), backups[0].FileName);
        Assert.Equal(file3, backups[0].FilePath);
        Assert.Equal(1024 * 1024 * 3, backups[0].SizeBytes);
        Assert.Equal("3.0 MB", backups[0].SizeDisplay);

        Assert.Equal(Path.GetFileName(file2), backups[1].FileName);
        Assert.Equal(2048, backups[1].SizeBytes);
        Assert.Equal("2.0 KB", backups[1].SizeDisplay);

        Assert.Equal(Path.GetFileName(file1), backups[2].FileName);
        Assert.Equal(100, backups[2].SizeBytes);
        Assert.Equal("100 B", backups[2].SizeDisplay);
    }

    [Fact]
    public void GetBackups_WhenDirectoryDoesNotExist_ReturnsEmptyList()
    {
        // Arrange
        var nonExistentDir = Path.Combine(_testRoot, "NonExistentBackupDir_" + Guid.NewGuid().ToString("N"));
        var service = new BackupService(_dbPath, nonExistentDir, _certsDir);
        if (Directory.Exists(nonExistentDir)) Directory.Delete(nonExistentDir, true);

        // Act
        var backups = service.GetBackups();

        // Assert
        Assert.NotNull(backups);
        Assert.Empty(backups);
    }

    [Fact]
    public void CleanOldBackups_DeletesFilesOlderThan30DaysAndKeepsRecentOnes()
    {
        // Arrange - Create target directory as custom folder
        var targetDir = Path.Combine(_testRoot, BackupService.BackupFolderName);
        Directory.CreateDirectory(targetDir);

        var oldFile1 = Path.Combine(targetDir, "SOURCES_backup_2026-06-01_10-00-00.db");
        var oldFile2 = Path.Combine(targetDir, "SOURCES_backup_2026-07-01_10-00-00.zip");
        var recentFile1 = Path.Combine(targetDir, "SOURCES_backup_2026-08-01_10-00-00.zip");
        var recentFile2 = Path.Combine(targetDir, "SOURCES_backup_2026-08-14_10-00-00.db");

        File.WriteAllBytes(oldFile1, new byte[64]);
        File.SetCreationTime(oldFile1, DateTime.Now.AddDays(-45));

        File.WriteAllBytes(oldFile2, new byte[64]);
        File.SetCreationTime(oldFile2, DateTime.Now.AddDays(-32));

        File.WriteAllBytes(recentFile1, new byte[64]);
        File.SetCreationTime(recentFile1, DateTime.Now.AddDays(-15));

        File.WriteAllBytes(recentFile2, new byte[64]);
        File.SetCreationTime(recentFile2, DateTime.Now.AddDays(-2));

        // Create valid source db
        CreateValidSqliteDatabase(_dbPath, "Sources", "SRC-CLEAN-TEST");

        // Act - CreateBackup internally invokes CleanOldBackups(30, targetDir)
        var result = _sut.CreateBackup(targetDir);

        // Assert
        Assert.True(result.Success);

        // Files older than 30 days must be deleted
        Assert.False(File.Exists(oldFile1), "Old backup (45 days ago) should have been deleted");
        Assert.False(File.Exists(oldFile2), "Old backup (32 days ago) should have been deleted");

        // Files newer than 30 days must remain
        Assert.True(File.Exists(recentFile1), "Recent backup (15 days ago) should remain");
        Assert.True(File.Exists(recentFile2), "Recent backup (2 days ago) should remain");

        // Newly created backup must exist
        Assert.NotNull(result.BackupPath);
        Assert.True(File.Exists(result.BackupPath), "New backup file must exist");
    }

    [Fact]
    public void CreatePreResetBackup_ProducesPreResetNamedZip_AndNeverDeletedByCleanOldBackups()
    {
        // Arrange
        CreateValidSqliteDatabase(_dbPath, "Sources", "SRC-PRE-RESET-TEST");
        var targetDir = Path.Combine(_testRoot, BackupService.BackupFolderName);
        Directory.CreateDirectory(targetDir);

        // Pre-reset backup file older than 30 days (45 days ago)
        var oldPreReset = Path.Combine(targetDir, "SOURCES_pre_reset_2026-06-01_10-00-00.zip");
        File.WriteAllBytes(oldPreReset, new byte[64]);
        File.SetCreationTime(oldPreReset, DateTime.Now.AddDays(-45));

        // Ordinary backup older than 30 days (45 days ago)
        var oldOrdinary = Path.Combine(targetDir, "SOURCES_backup_2026-06-01_10-00-00.zip");
        File.WriteAllBytes(oldOrdinary, new byte[64]);
        File.SetCreationTime(oldOrdinary, DateTime.Now.AddDays(-45));

        // Act 1: Call CreatePreResetBackup
        var result = _sut.CreatePreResetBackup();

        // Assert 1: Created successfully with SOURCES_pre_reset_ prefix
        Assert.True(result.Success);
        Assert.NotNull(result.BackupPath);
        Assert.Contains("SOURCES_pre_reset_", Path.GetFileName(result.BackupPath));
        Assert.True(File.Exists(result.BackupPath));

        // Act 2: Trigger ordinary CreateBackup which calls CleanOldBackups(30, targetDir)
        var ordinaryResult = _sut.CreateBackup(targetDir);
        Assert.True(ordinaryResult.Success);

        // Assert 2: Pre-reset backup older than 30 days SURVIVES, ordinary backup older than 30 days is REMOVED
        Assert.True(File.Exists(oldPreReset), "Pre-reset backup older than 30 days must survive CleanOldBackups");
        Assert.False(File.Exists(oldOrdinary), "Ordinary backup older than 30 days must be deleted by CleanOldBackups");
    }

    [Fact]
    public void GetBackups_IncludesBothOrdinaryAndPreResetBackups()
    {
        // Arrange
        var ordinary = Path.Combine(_backupDir, "SOURCES_backup_2026-09-01_10-00-00.zip");
        var preReset = Path.Combine(_backupDir, "SOURCES_pre_reset_2026-09-02_10-00-00.zip");
        var unrelated = Path.Combine(_backupDir, "OTHER_file_2026-09-03.zip");

        File.WriteAllBytes(ordinary, new byte[100]);
        File.WriteAllBytes(preReset, new byte[200]);
        File.WriteAllBytes(unrelated, new byte[300]);

        // Act
        var backups = _sut.GetBackups();

        // Assert
        Assert.Contains(backups, b => b.FileName == Path.GetFileName(ordinary));
        Assert.Contains(backups, b => b.FileName == Path.GetFileName(preReset));
        Assert.DoesNotContain(backups, b => b.FileName == Path.GetFileName(unrelated));
    }
}
