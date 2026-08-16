using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
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

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testRoot))
            {
                Directory.Delete(_testRoot, recursive: true);
            }
        }
        catch
        {
            // Ignore cleanup errors in temp folder
        }
    }

    [Fact]
    public void ParameterlessConstructor_InitializesWithoutException()
    {
        // Act
        var service = new BackupService();

        // Assert
        Assert.NotNull(service);
    }

    [Fact]
    public void Constructor_WithNullParameters_InitializesWithoutException()
    {
        // Act
        var service = new BackupService(null, null);

        // Assert
        Assert.NotNull(service);
    }

    [Fact]
    public void CreateBackup_DefaultLocation_CreatesBackupWithTimestampAndExactContent()
    {
        // Arrange
        var dummyData = Encoding.UTF8.GetBytes("SQLite format 3\0 Test DB Content " + Guid.NewGuid());
        File.WriteAllBytes(_dbPath, dummyData);

        // Act
        var result = _sut.CreateBackup();

        // Assert
        Assert.True(result.Success, $"Expected Success=true but got Message: {result.Message}");
        Assert.NotNull(result.BackupPath);
        Assert.True(File.Exists(result.BackupPath), "Backup file should exist on disk");

        var fileName = Path.GetFileName(result.BackupPath);
        Assert.Matches(@"^SOURCES_backup_\d{4}-\d{2}-\d{2}_\d{2}-\d{2}-\d{2}\.db$", fileName);

        var backupBytes = File.ReadAllBytes(result.BackupPath);
        Assert.Equal(dummyData, backupBytes);
    }

    [Fact]
    public void CreateBackup_CustomLocation_CreatesFolderAndBackupFile()
    {
        // Arrange
        var customFolder = Path.Combine(_testRoot, "CustomTargetFolder");
        var dummyData = Encoding.UTF8.GetBytes("CUSTOM_BACKUP_TEST_DATA");
        File.WriteAllBytes(_dbPath, dummyData);

        // Act
        var result = _sut.CreateBackup(customFolder);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.BackupPath);
        Assert.True(File.Exists(result.BackupPath));

        var expectedParentFolder = Path.Combine(customFolder, BackupService.BackupFolderName);
        Assert.Equal(expectedParentFolder, Path.GetDirectoryName(result.BackupPath));

        var backupBytes = File.ReadAllBytes(result.BackupPath);
        Assert.Equal(dummyData, backupBytes);
    }

    [Fact]
    public void CreateBackup_CustomPathAlreadyEndingWithBackupFolderName_DoesNotNestFolderName()
    {
        // Arrange
        var targetDir = Path.Combine(_testRoot, BackupService.BackupFolderName);
        var dummyData = Encoding.UTF8.GetBytes("NESTING_TEST_DATA");
        File.WriteAllBytes(_dbPath, dummyData);

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
        var dummyData = Encoding.UTF8.GetBytes("AUTO_CREATE_DIR_DATA");
        File.WriteAllBytes(_dbPath, dummyData);

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
    public void RestoreBackup_ValidBackupFile_CreatesPreRestoreSafetyBackupAndRestoresDatabase()
    {
        // Arrange
        var originalData = Encoding.UTF8.GetBytes("ORIGINAL_DATABASE_STATE");
        var restoredData = Encoding.UTF8.GetBytes("RESTORED_DATABASE_STATE");

        File.WriteAllBytes(_dbPath, originalData);

        var backupFilePath = Path.Combine(_backupDir, "SOURCES_backup_2026-08-16_12-00-00.db");
        File.WriteAllBytes(backupFilePath, restoredData);

        // Act
        var result = _sut.RestoreBackup(backupFilePath);

        // Assert
        Assert.True(result.Success);
        Assert.Contains("تمت الاستعادة بنجاح", result.Message);

        // Verify restored content
        var currentDbBytes = File.ReadAllBytes(_dbPath);
        Assert.Equal(restoredData, currentDbBytes);

        // Verify pre_restore safety backup was created in backup directory
        var safetyFiles = Directory.GetFiles(_backupDir, "SOURCES_pre_restore_*.db");
        Assert.Single(safetyFiles);

        var safetyBytes = File.ReadAllBytes(safetyFiles[0]);
        Assert.Equal(originalData, safetyBytes);
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
        var restoredData = Encoding.UTF8.GetBytes("RESTORE_NO_EXISTING_DB");
        var backupFilePath = Path.Combine(_backupDir, "SOURCES_backup_restore_test.db");
        File.WriteAllBytes(backupFilePath, restoredData);

        // Act
        var result = _sut.RestoreBackup(backupFilePath);

        // Assert
        Assert.True(result.Success);
        Assert.True(File.Exists(_dbPath));
        Assert.Equal(restoredData, File.ReadAllBytes(_dbPath));
    }

    [Fact]
    public void GetBackups_MultipleBackupFiles_ReturnsCorrectListOrderedByCreationTimeDescending()
    {
        // Arrange
        var baseDate = DateTime.Now;

        var file1 = Path.Combine(_backupDir, "SOURCES_backup_2026-08-01_10-00-00.db");
        var file2 = Path.Combine(_backupDir, "SOURCES_backup_2026-08-05_10-00-00.db");
        var file3 = Path.Combine(_backupDir, "SOURCES_backup_2026-08-10_10-00-00.db");
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
        var oldFile2 = Path.Combine(targetDir, "SOURCES_backup_2026-07-01_10-00-00.db");
        var recentFile1 = Path.Combine(targetDir, "SOURCES_backup_2026-08-01_10-00-00.db");
        var recentFile2 = Path.Combine(targetDir, "SOURCES_backup_2026-08-14_10-00-00.db");

        File.WriteAllBytes(oldFile1, new byte[64]);
        File.SetCreationTime(oldFile1, DateTime.Now.AddDays(-45));

        File.WriteAllBytes(oldFile2, new byte[64]);
        File.SetCreationTime(oldFile2, DateTime.Now.AddDays(-32));

        File.WriteAllBytes(recentFile1, new byte[64]);
        File.SetCreationTime(recentFile1, DateTime.Now.AddDays(-15));

        File.WriteAllBytes(recentFile2, new byte[64]);
        File.SetCreationTime(recentFile2, DateTime.Now.AddDays(-2));

        // Create dummy source db
        File.WriteAllBytes(_dbPath, new byte[128]);

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
