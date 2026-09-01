using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Microsoft.Data.Sqlite;
using Moq;
using Sources.Data;
using Sources.Models;
using Sources.Services;
using Sources.Tests.Fixtures;
using Xunit;

namespace Sources.Tests;

public class SourceCertificateServiceTests : IClassFixture<SqliteInMemoryFixture>, IDisposable
{
    private readonly SqliteInMemoryFixture _fixture;
    private readonly Mock<IAuditService> _auditMock;
    private readonly string _testCertFolder;
    private readonly string _tempFilesFolder;
    private readonly SourceCertificateService _sut;

    public SourceCertificateServiceTests(SqliteInMemoryFixture fixture)
    {
        _fixture = fixture;
        _fixture.ResetDatabase();
        _auditMock = new Mock<IAuditService>();

        var testId = Guid.NewGuid().ToString("N");
        _testCertFolder = Path.Combine(Path.GetTempPath(), $"CertTest_Certificates_{testId}");
        _tempFilesFolder = Path.Combine(Path.GetTempPath(), $"CertTest_Temp_{testId}");

        Directory.CreateDirectory(_testCertFolder);
        Directory.CreateDirectory(_tempFilesFolder);

        _sut = new SourceCertificateService(_fixture.ContextFactory, _auditMock.Object, _testCertFolder);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testCertFolder))
                Directory.Delete(_testCertFolder, recursive: true);
            if (Directory.Exists(_tempFilesFolder))
                Directory.Delete(_tempFilesFolder, recursive: true);
        }
        catch { }
    }

    private string CreateDummyFile(string fileName, string content = "Sample Certificate Content")
    {
        var path = Path.Combine(_tempFilesFolder, fileName);
        File.WriteAllText(path, content);
        return path;
    }

    [Fact]
    public void AttachCertificate_ValidFile_CreatesRecordAndCopiesFile()
    {
        // Arrange
        var sourceId = Guid.NewGuid();
        var sampleFile = CreateDummyFile("Calibration_Report_2026.pdf", "PDF Header %PDF-1.4");

        // Act
        var cert = _sut.AttachCertificate(sourceId, "Standard", sampleFile, "المهندس علي");

        // Assert
        Assert.NotNull(cert);
        Assert.Equal(sourceId, cert.SourceId);
        Assert.Equal("Standard", cert.SourceType);
        Assert.Equal("Calibration_Report_2026.pdf", cert.OriginalFileName);
        Assert.EndsWith(".pdf", cert.StoredFileName, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("المهندس علي", cert.AttachedBy);

        var destinationOnDisk = Path.Combine(_testCertFolder, cert.StoredFileName);
        Assert.True(File.Exists(destinationOnDisk));
        Assert.Equal("PDF Header %PDF-1.4", File.ReadAllText(destinationOnDisk));

        using var db = _fixture.CreateContext();
        var dbRecord = db.SourceCertificates.FirstOrDefault(c => c.Id == cert.Id);
        Assert.NotNull(dbRecord);
        Assert.Equal(cert.OriginalFileName, dbRecord.OriginalFileName);

        _auditMock.Verify(a => a.Log("Create", "SourceCertificates", cert.Id, It.Is<string>(s => s.Contains("Calibration_Report_2026.pdf"))), Times.Once);
    }

    [Fact]
    public void DeleteCertificate_ExistingCert_DeletesFileAndRecord()
    {
        // Arrange
        var sourceId = Guid.NewGuid();
        var sampleFile = CreateDummyFile("DocToDelete.docx", "Word Document Test");
        var cert = _sut.AttachCertificate(sourceId, "Neutron", sampleFile, "المهندس سالم");

        var filePathOnDisk = Path.Combine(_testCertFolder, cert.StoredFileName);
        Assert.True(File.Exists(filePathOnDisk));

        // Act
        var deleted = _sut.DeleteCertificate(cert.Id, "المهندس سالم");

        // Assert
        Assert.True(deleted);
        Assert.False(File.Exists(filePathOnDisk));

        using var db = _fixture.CreateContext();
        var dbRecord = db.SourceCertificates.FirstOrDefault(c => c.Id == cert.Id);
        Assert.Null(dbRecord);

        _auditMock.Verify(a => a.Log("Delete", "SourceCertificates", cert.Id, It.Is<string>(s => s.Contains("DocToDelete.docx"))), Times.Once);
    }

    [Fact]
    public void GetCertificates_BySourceIdAndType_ReturnsCorrectResults()
    {
        // Arrange
        var source1 = Guid.NewGuid();
        var source2 = Guid.NewGuid();

        var file1 = CreateDummyFile("Cert1.pdf");
        var file2 = CreateDummyFile("Cert2.png");
        var file3 = CreateDummyFile("Cert3.pdf");

        _sut.AttachCertificate(source1, "Standard", file1, "User1");
        _sut.AttachCertificate(source1, "Standard", file2, "User2");
        _sut.AttachCertificate(source2, "Neutron", file3, "User3");

        // Act
        var standardList = _sut.GetCertificates(source1, "Standard");
        var neutronList = _sut.GetCertificates(source2, "Neutron");
        var emptyList = _sut.GetCertificates(source1, "Neutron");

        // Assert
        Assert.Equal(2, standardList.Count);
        Assert.Contains(standardList, c => c.OriginalFileName == "Cert1.pdf");
        Assert.Contains(standardList, c => c.OriginalFileName == "Cert2.png");

        Assert.Single(neutronList);
        Assert.Equal("Cert3.pdf", neutronList[0].OriginalFileName);

        Assert.Empty(emptyList);
    }

    [Fact]
    public void CreateBackup_WithCertificates_ProducesZipWithBothDbAndCertificates()
    {
        // Arrange
        var tempFolder = Path.Combine(Path.GetTempPath(), "BackupWithCertsTest_" + Guid.NewGuid().ToString("N"));
        var testDbPath = Path.Combine(tempFolder, "Sources.db");
        var testBackupsDir = Path.Combine(tempFolder, "Backups");
        var testCertsDir = Path.Combine(tempFolder, "Certificates");

        Directory.CreateDirectory(tempFolder);
        Directory.CreateDirectory(testBackupsDir);
        Directory.CreateDirectory(testCertsDir);

        // إنشاء قاعدة بيانات SQLite حقيقية
        using (var conn = new SqliteConnection($"Data Source={testDbPath}"))
        {
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "CREATE TABLE Sources (Id TEXT PRIMARY KEY, Code TEXT); INSERT INTO Sources VALUES ('1', 'SRC-001');";
            cmd.ExecuteNonQuery();
        }

        // إنشاء شهادات تجريبية في مجلد الشهادات
        File.WriteAllText(Path.Combine(testCertsDir, "guid1.pdf"), "Fake Certificate 1");
        File.WriteAllText(Path.Combine(testCertsDir, "guid2.docx"), "Fake Certificate 2");

        var backupService = new BackupService(testDbPath, testBackupsDir, testCertsDir);

        try
        {
            // Act
            var result = backupService.CreateBackup();

            // Assert
            Assert.True(result.Success);
            Assert.NotNull(result.BackupPath);
            Assert.EndsWith(".zip", result.BackupPath, StringComparison.OrdinalIgnoreCase);
            Assert.True(File.Exists(result.BackupPath));

            // تحقق من محتويات ملف الـ ZIP
            using (var zip = ZipFile.OpenRead(result.BackupPath))
            {
                var dbEntry = zip.GetEntry("Sources.db");
                Assert.NotNull(dbEntry);

                var cert1 = zip.GetEntry("Certificates/guid1.pdf");
                var cert2 = zip.GetEntry("Certificates/guid2.docx");

                Assert.NotNull(cert1);
                Assert.NotNull(cert2);
            }
        }
        finally
        {
            try { Directory.Delete(tempFolder, recursive: true); } catch { }
        }
    }

    [Fact]
    public void RestoreBackup_LegacyDbFile_RestoresSuccessfullyWithoutError()
    {
        // Arrange
        var tempFolder = Path.Combine(Path.GetTempPath(), "LegacyRestoreTest_" + Guid.NewGuid().ToString("N"));
        var testDbPath = Path.Combine(tempFolder, "CurrentSources.db");
        var legacyBackupDbPath = Path.Combine(tempFolder, "SOURCES_backup_2025-01-01_12-00-00.db");
        var testBackupsDir = Path.Combine(tempFolder, "Backups");
        var testCertsDir = Path.Combine(tempFolder, "Certificates");

        Directory.CreateDirectory(tempFolder);
        Directory.CreateDirectory(testBackupsDir);
        Directory.CreateDirectory(testCertsDir);

        // إنشاء قاعدة بيانات حالية
        using (var conn = new SqliteConnection($"Data Source={testDbPath}"))
        {
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "CREATE TABLE Info (Val TEXT); INSERT INTO Info VALUES ('CURRENT_STATE'); CREATE TABLE IF NOT EXISTS \"__EFMigrationsHistory\" (\"MigrationId\" TEXT NOT NULL PRIMARY KEY, \"ProductVersion\" TEXT NOT NULL); INSERT INTO \"__EFMigrationsHistory\" VALUES ('20260901112320_InitialSchema', '8.0.12');";
            cmd.ExecuteNonQuery();
        }

        // إنشاء قاعدة بيانات قديمة لاستعادتها
        using (var conn = new SqliteConnection($"Data Source={legacyBackupDbPath}"))
        {
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "CREATE TABLE Info (Val TEXT); INSERT INTO Info VALUES ('RESTORED_LEGACY_STATE'); CREATE TABLE IF NOT EXISTS \"__EFMigrationsHistory\" (\"MigrationId\" TEXT NOT NULL PRIMARY KEY, \"ProductVersion\" TEXT NOT NULL); INSERT INTO \"__EFMigrationsHistory\" VALUES ('20260901112320_InitialSchema', '8.0.12');";
            cmd.ExecuteNonQuery();
        }
        SqliteConnection.ClearAllPools();

        var backupService = new BackupService(testDbPath, testBackupsDir, testCertsDir);

        try
        {
            // Act
            var result = backupService.RestoreBackup(legacyBackupDbPath);

            // Assert
            Assert.True(result.Success);

            using (var conn = new SqliteConnection($"Data Source={testDbPath}"))
            {
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT Val FROM Info LIMIT 1;";
                var val = cmd.ExecuteScalar()?.ToString();
                Assert.Equal("RESTORED_LEGACY_STATE", val);
            }
        }
        finally
        {
            try { Directory.Delete(tempFolder, recursive: true); } catch { }
        }
    }

    [Fact]
    public void RestoreBackup_ZipFileWithCertificates_RestoresDbAndReplacesCertificatesFolder()
    {
        // Arrange
        var tempFolder = Path.Combine(Path.GetTempPath(), "ZipRestoreTest_" + Guid.NewGuid().ToString("N"));
        var testDbPath = Path.Combine(tempFolder, "Sources.db");
        var testBackupsDir = Path.Combine(tempFolder, "Backups");
        var testCertsDir = Path.Combine(tempFolder, "Certificates");
        var zipBackupPath = Path.Combine(tempFolder, "SOURCES_backup_2026-03-01_10-00-00.zip");

        Directory.CreateDirectory(tempFolder);
        Directory.CreateDirectory(testBackupsDir);
        Directory.CreateDirectory(testCertsDir);

        // حالة سابقة للمجلد الحالي
        File.WriteAllText(Path.Combine(testCertsDir, "old_file.txt"), "Should be removed");

        // إنشاء ملف ZIP يحتوي على DB وشهادات جديدة
        var tempSourceDb = Path.Combine(tempFolder, "temp_source.db");
        using (var conn = new SqliteConnection($"Data Source={tempSourceDb}"))
        {
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "CREATE TABLE Info (Val TEXT); INSERT INTO Info VALUES ('FROM_ZIP_BACKUP'); CREATE TABLE IF NOT EXISTS \"__EFMigrationsHistory\" (\"MigrationId\" TEXT NOT NULL PRIMARY KEY, \"ProductVersion\" TEXT NOT NULL); INSERT INTO \"__EFMigrationsHistory\" VALUES ('20260901112320_InitialSchema', '8.0.12');";
            cmd.ExecuteNonQuery();
        }
        SqliteConnection.ClearAllPools();

        using (var zip = ZipFile.Open(zipBackupPath, ZipArchiveMode.Create))
        {
            zip.CreateEntryFromFile(tempSourceDb, "Sources.db");
            var certTemp = Path.Combine(tempFolder, "new_cert.pdf");
            File.WriteAllText(certTemp, "Restored Certificate Body");
            zip.CreateEntryFromFile(certTemp, "Certificates/new_cert.pdf");
        }

        var backupService = new BackupService(testDbPath, testBackupsDir, testCertsDir);

        try
        {
            // Act
            var result = backupService.RestoreBackup(zipBackupPath);

            // Assert
            Assert.True(result.Success);

            // تأكد من استعادة DB
            using (var conn = new SqliteConnection($"Data Source={testDbPath}"))
            {
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT Val FROM Info LIMIT 1;";
                var val = cmd.ExecuteScalar()?.ToString();
                Assert.Equal("FROM_ZIP_BACKUP", val);
            }

            // تأكد من استبدال مجلد الشهادات
            Assert.False(File.Exists(Path.Combine(testCertsDir, "old_file.txt")));
            Assert.True(File.Exists(Path.Combine(testCertsDir, "new_cert.pdf")));
            Assert.Equal("Restored Certificate Body", File.ReadAllText(Path.Combine(testCertsDir, "new_cert.pdf")));
        }
        finally
        {
            try { Directory.Delete(tempFolder, recursive: true); } catch { }
        }
    }

    [Fact]
    public void RestoreBackup_FailureMidway_PreservesOriginalCertificatesFolder()
    {
        // Arrange
        var tempFolder = Path.Combine(Path.GetTempPath(), "FailurePreserveTest_" + Guid.NewGuid().ToString("N"));
        var testDbPath = Path.Combine(tempFolder, "Sources.db");
        var testBackupsDir = Path.Combine(tempFolder, "Backups");
        var testCertsDir = Path.Combine(tempFolder, "Certificates");
        var corruptZipPath = Path.Combine(tempFolder, "corrupt_backup.zip");

        Directory.CreateDirectory(tempFolder);
        Directory.CreateDirectory(testBackupsDir);
        Directory.CreateDirectory(testCertsDir);

        File.WriteAllText(Path.Combine(testCertsDir, "important_original_cert.pdf"), "Do not lose me!");

        // إنشاء ZIP بدون Sources.db ليتسبب في فشل مقصود
        using (var zip = ZipFile.Open(corruptZipPath, ZipArchiveMode.Create))
        {
            var dummyFile = Path.Combine(tempFolder, "dummy.txt");
            File.WriteAllText(dummyFile, "dummy");
            zip.CreateEntryFromFile(dummyFile, "dummy.txt");
        }

        var backupService = new BackupService(testDbPath, testBackupsDir, testCertsDir);

        try
        {
            // Act
            var result = backupService.RestoreBackup(corruptZipPath);

            // Assert
            Assert.False(result.Success);

            // يجب أن تظل الملفات الأصلية أو نسخة الأمان سليمة ومحفوظة
            var safetyDirs = Directory.GetDirectories(tempFolder, "Certificates_pre_restore_*");
            Assert.True(File.Exists(Path.Combine(testCertsDir, "important_original_cert.pdf")) || safetyDirs.Length > 0);
        }
        finally
        {
            try { Directory.Delete(tempFolder, recursive: true); } catch { }
        }
    }
}
