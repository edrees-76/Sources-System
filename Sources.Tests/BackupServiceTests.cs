using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using Sources.Services;
using Xunit;

namespace Sources.Tests;

public class BackupServiceTests : IDisposable
{
    private readonly string _testRoot;
    private readonly string _dbPath;
    private readonly string _backupDir;
    private readonly BackupService _sut;

    public BackupServiceTests()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), "Sources_BackupServiceTests_" + Guid.NewGuid().ToString("N"));
        _backupDir = Path.Combine(_testRoot, "Backups");
        _dbPath = Path.Combine(_testRoot, "TestSources.db");

        Directory.CreateDirectory(_testRoot);
        Directory.CreateDirectory(_backupDir);

        _sut = new BackupService(_dbPath, _backupDir);
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
    public void CreateBackup_OnRealLocalAppDataDatabase_ProducesFullyValidBackup()
    {
        var appDataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Sources");
        var realDbPath = Path.Combine(appDataDir, "Sources.db");

        if (File.Exists(realDbPath))
        {
            var realService = new BackupService(realDbPath, _backupDir);
            var result = realService.CreateBackup();

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
        Assert.Contains("النسخة الاحتياطية أُنشئت بإصدار أقدم من المنظومة", result.Message);

        // Verify that original database was restored from safety backup
        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Code FROM Sources LIMIT 1;";
        Assert.Equal("ORIGINAL_SAFE_DATA", cmd.ExecuteScalar()?.ToString());
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
        var service = new BackupService(_dbPath, nonExistentDir);
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
}
