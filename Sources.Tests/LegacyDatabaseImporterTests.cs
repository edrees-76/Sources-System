using System;
using System.IO;
using System.Text;
using Sources.Data;
using Xunit;

namespace Sources.Tests;

public class LegacyDatabaseImporterTests : IDisposable
{
    private readonly string _tempDir;

    public LegacyDatabaseImporterTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "Sources_LegacyImportTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, recursive: true);
            }
        }
        catch { }
    }

    [Fact]
    public void Import_WhenLegacyFileMissing_ReturnsFalseAndCreatesNothing()
    {
        // Arrange
        var legacyPath = Path.Combine(_tempDir, "missing_legacy.db");
        var targetPath = Path.Combine(_tempDir, "target.db");

        // Act
        var result = LegacyDatabaseImporter.Import(legacyPath, targetPath);

        // Assert
        Assert.False(result);
        Assert.False(File.Exists(targetPath));
    }

    [Fact]
    public void Import_WhenTargetAlreadyExists_ReturnsFalseAndLeavesTargetUnchanged()
    {
        // Arrange
        var legacyPath = Path.Combine(_tempDir, "legacy.db");
        var targetPath = Path.Combine(_tempDir, "target.db");

        var legacyBytes = Encoding.UTF8.GetBytes("LEGACY_DATA_ORIGINAL");
        var targetBytes = Encoding.UTF8.GetBytes("TARGET_DATA_EXISTING");

        File.WriteAllBytes(legacyPath, legacyBytes);
        File.WriteAllBytes(targetPath, targetBytes);

        // Act
        var result = LegacyDatabaseImporter.Import(legacyPath, targetPath);

        // Assert
        Assert.False(result);
        Assert.Equal(targetBytes, File.ReadAllBytes(targetPath));
    }

    [Fact]
    public void Import_WhenLegacyFileExists_CopiesItToTargetWithIdenticalBytes()
    {
        // Arrange
        var legacyPath = Path.Combine(_tempDir, "legacy.db");
        var targetPath = Path.Combine(_tempDir, "target.db");

        var legacyBytes = new byte[] { 0x53, 0x51, 0x4C, 0x69, 0x74, 0x65, 0x20, 0x33, 0x00, 0xFF, 0xDE, 0xAD, 0xBE, 0xEF };
        File.WriteAllBytes(legacyPath, legacyBytes);

        // Act
        var result = LegacyDatabaseImporter.Import(legacyPath, targetPath);

        // Assert
        Assert.True(result);
        Assert.True(File.Exists(targetPath));
        Assert.Equal(legacyBytes, File.ReadAllBytes(targetPath));
    }

    [Fact]
    public void Import_CopiesWalAndShmSideFilesWhenPresent()
    {
        // Arrange
        var legacyPath = Path.Combine(_tempDir, "legacy.db");
        var targetPath = Path.Combine(_tempDir, "target.db");

        var dbBytes = Encoding.UTF8.GetBytes("MAIN_DB_CONTENT");
        var walBytes = Encoding.UTF8.GetBytes("WAL_FILE_CONTENT_123");
        var shmBytes = Encoding.UTF8.GetBytes("SHM_FILE_CONTENT_456");

        File.WriteAllBytes(legacyPath, dbBytes);
        File.WriteAllBytes(legacyPath + "-wal", walBytes);
        File.WriteAllBytes(legacyPath + "-shm", shmBytes);

        // Act
        var result = LegacyDatabaseImporter.Import(legacyPath, targetPath);

        // Assert
        Assert.True(result);
        Assert.True(File.Exists(targetPath));
        Assert.True(File.Exists(targetPath + "-wal"));
        Assert.True(File.Exists(targetPath + "-shm"));

        Assert.Equal(dbBytes, File.ReadAllBytes(targetPath));
        Assert.Equal(walBytes, File.ReadAllBytes(targetPath + "-wal"));
        Assert.Equal(shmBytes, File.ReadAllBytes(targetPath + "-shm"));
    }

    [Fact]
    public void Import_WhenSideFilesAbsent_DoesNotCreateThem()
    {
        // Arrange
        var legacyPath = Path.Combine(_tempDir, "legacy.db");
        var targetPath = Path.Combine(_tempDir, "target.db");

        File.WriteAllBytes(legacyPath, Encoding.UTF8.GetBytes("MAIN_DB_ONLY"));

        // Act
        var result = LegacyDatabaseImporter.Import(legacyPath, targetPath);

        // Assert
        Assert.True(result);
        Assert.True(File.Exists(targetPath));
        Assert.False(File.Exists(targetPath + "-wal"));
        Assert.False(File.Exists(targetPath + "-shm"));
    }

    [Fact]
    public void Import_CreatesTargetDirectoryWhenMissing()
    {
        // Arrange
        var legacyPath = Path.Combine(_tempDir, "legacy.db");
        var subDir = Path.Combine(_tempDir, "nested", "deep", "dir");
        var targetPath = Path.Combine(subDir, "target.db");

        var dbBytes = Encoding.UTF8.GetBytes("NESTED_TARGET_TEST");
        File.WriteAllBytes(legacyPath, dbBytes);

        Assert.False(Directory.Exists(subDir));

        // Act
        var result = LegacyDatabaseImporter.Import(legacyPath, targetPath);

        // Assert
        Assert.True(result);
        Assert.True(Directory.Exists(subDir));
        Assert.True(File.Exists(targetPath));
        Assert.Equal(dbBytes, File.ReadAllBytes(targetPath));
    }

    [Fact]
    public void Import_WhenLegacyFileLocked_ThrowsLegacyDatabaseImportException()
    {
        // Arrange
        var legacyPath = Path.Combine(_tempDir, "locked_legacy.db");
        var targetPath = Path.Combine(_tempDir, "target.db");

        File.WriteAllBytes(legacyPath, Encoding.UTF8.GetBytes("LOCKED_CONTENT"));

        using (var fs = new FileStream(legacyPath, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            // Act & Assert
            var ex = Assert.Throws<LegacyDatabaseImportException>(() =>
                LegacyDatabaseImporter.Import(legacyPath, targetPath));

            Assert.Contains(legacyPath, ex.Message);
            Assert.Contains(targetPath, ex.Message);
        }
    }

    [Fact]
    public void Import_WhenLegacyFileLocked_LeavesNoTargetDatabase()
    {
        // Arrange
        var legacyPath = Path.Combine(_tempDir, "locked_legacy.db");
        var targetPath = Path.Combine(_tempDir, "target.db");

        File.WriteAllBytes(legacyPath, Encoding.UTF8.GetBytes("LOCKED_CONTENT"));

        using (var fs = new FileStream(legacyPath, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            // Act & Assert
            Assert.Throws<LegacyDatabaseImportException>(() =>
                LegacyDatabaseImporter.Import(legacyPath, targetPath));
        }

        Assert.False(File.Exists(targetPath));
        Assert.False(File.Exists(targetPath + ".import-tmp"));
    }

    [Fact]
    public void Import_WhenLegacyFileLocked_RemovesAlreadyCopiedSideFiles()
    {
        // Arrange
        var legacyPath = Path.Combine(_tempDir, "locked_legacy.db");
        var targetPath = Path.Combine(_tempDir, "target.db");

        File.WriteAllBytes(legacyPath, Encoding.UTF8.GetBytes("LOCKED_CONTENT"));
        File.WriteAllBytes(legacyPath + "-wal", Encoding.UTF8.GetBytes("SIDE_WAL_CONTENT"));

        using (var fs = new FileStream(legacyPath, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            // Act & Assert
            Assert.Throws<LegacyDatabaseImportException>(() =>
                LegacyDatabaseImporter.Import(legacyPath, targetPath));
        }

        Assert.False(File.Exists(targetPath + "-wal"));
        Assert.False(File.Exists(targetPath));
        Assert.False(File.Exists(targetPath + ".import-tmp"));
    }

    [Fact]
    public void DatabasePaths_DbPath_IsDatabaseFileNameUnderAppDataDirectory()
    {
        // Assert
        Assert.Equal(
            Path.Combine(DatabasePaths.AppDataDirectory, DatabasePaths.DatabaseFileName),
            DatabasePaths.DbPath);
    }

    [Fact]
    public void DatabasePaths_BackupsDirectory_IsUnderAppDataDirectory()
    {
        // Assert
        Assert.Equal(
            Path.Combine(DatabasePaths.AppDataDirectory, "Backups"),
            DatabasePaths.BackupsDirectory);
    }

    [Fact]
    public void DatabasePaths_LegacyDbPath_IsBesideExecutable()
    {
        // Assert
        Assert.Equal(
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, DatabasePaths.DatabaseFileName),
            DatabasePaths.LegacyDbPath);
    }
}
