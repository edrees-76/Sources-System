using System;
using System.IO;
using System.Reflection;
using System.Text;
using Moq;
using Sources.Data;
using Sources.Services;
using Sources.Tests.Fakes;
using Xunit;

namespace Sources.Tests;

public class LegacyCertificatesImporterTests : IDisposable
{
    private readonly string _tempDir;

    public LegacyCertificatesImporterTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "Sources_LegacyCertImportTest_" + Guid.NewGuid().ToString("N"));
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
    public void Import_WhenLegacyFolderMissing_DoesNotCreateTarget()
    {
        // Arrange
        var legacyFolder = Path.Combine(_tempDir, "legacy_missing");
        var targetFolder = Path.Combine(_tempDir, "target");

        // Act
        LegacyCertificatesImporter.Import(legacyFolder, targetFolder);

        // Assert
        Assert.False(Directory.Exists(targetFolder));
    }

    [Fact]
    public void Import_WhenLegacyFolderEmpty_DoesNotCreateTarget()
    {
        // Arrange
        var legacyFolder = Path.Combine(_tempDir, "legacy_empty");
        Directory.CreateDirectory(legacyFolder);
        var targetFolder = Path.Combine(_tempDir, "target");

        // Act
        LegacyCertificatesImporter.Import(legacyFolder, targetFolder);

        // Assert
        Assert.False(Directory.Exists(targetFolder));
    }

    [Fact]
    public void Import_WhenLegacyHasFiles_CopiesThemToTarget()
    {
        // Arrange
        var legacyFolder = Path.Combine(_tempDir, "legacy");
        Directory.CreateDirectory(legacyFolder);
        var certBytes = Encoding.UTF8.GetBytes("CERTIFICATE_CONTENT");
        File.WriteAllBytes(Path.Combine(legacyFolder, "cert1.pdf"), certBytes);

        var targetFolder = Path.Combine(_tempDir, "target");

        // Act
        LegacyCertificatesImporter.Import(legacyFolder, targetFolder);

        // Assert
        Assert.True(File.Exists(Path.Combine(targetFolder, "cert1.pdf")));
        Assert.Equal(certBytes, File.ReadAllBytes(Path.Combine(targetFolder, "cert1.pdf")));
    }

    [Fact]
    public void Import_CopiesNestedSubdirectories()
    {
        // Arrange
        var legacyFolder = Path.Combine(_tempDir, "legacy");
        var nestedSubDir = Path.Combine(legacyFolder, "sub");
        Directory.CreateDirectory(nestedSubDir);
        var certBytes = Encoding.UTF8.GetBytes("NESTED_CERT");
        File.WriteAllBytes(Path.Combine(nestedSubDir, "cert2.pdf"), certBytes);

        var targetFolder = Path.Combine(_tempDir, "target");

        // Act
        LegacyCertificatesImporter.Import(legacyFolder, targetFolder);

        // Assert
        Assert.True(File.Exists(Path.Combine(targetFolder, "sub", "cert2.pdf")));
        Assert.Equal(certBytes, File.ReadAllBytes(Path.Combine(targetFolder, "sub", "cert2.pdf")));
    }

    [Fact]
    public void Import_DoesNotDeleteLegacySourceFiles()
    {
        // Arrange
        var legacyFolder = Path.Combine(_tempDir, "legacy");
        Directory.CreateDirectory(legacyFolder);
        var legacyFile = Path.Combine(legacyFolder, "cert1.pdf");
        File.WriteAllBytes(legacyFile, Encoding.UTF8.GetBytes("CERTIFICATE_CONTENT"));

        var targetFolder = Path.Combine(_tempDir, "target");

        // Act
        LegacyCertificatesImporter.Import(legacyFolder, targetFolder);

        // Assert — copy only, source must remain intact
        Assert.True(File.Exists(legacyFile));
        Assert.True(Directory.Exists(legacyFolder));
    }

    [Fact]
    public void Import_WhenTargetAlreadyHasFiles_DoesNotOverwriteOrDuplicate()
    {
        // Arrange
        var legacyFolder = Path.Combine(_tempDir, "legacy");
        Directory.CreateDirectory(legacyFolder);
        File.WriteAllBytes(Path.Combine(legacyFolder, "cert1.pdf"), Encoding.UTF8.GetBytes("LEGACY_CONTENT"));

        var targetFolder = Path.Combine(_tempDir, "target");
        Directory.CreateDirectory(targetFolder);
        var existingBytes = Encoding.UTF8.GetBytes("EXISTING_TARGET_CONTENT");
        File.WriteAllBytes(Path.Combine(targetFolder, "existing.pdf"), existingBytes);

        // Act
        LegacyCertificatesImporter.Import(legacyFolder, targetFolder);

        // Assert — target already had content, so import must be a no-op
        Assert.False(File.Exists(Path.Combine(targetFolder, "cert1.pdf")));
        Assert.Equal(existingBytes, File.ReadAllBytes(Path.Combine(targetFolder, "existing.pdf")));
    }

    [Fact]
    public void Import_WhenLegacyFileLocked_DoesNotThrow()
    {
        // Arrange
        var legacyFolder = Path.Combine(_tempDir, "legacy");
        Directory.CreateDirectory(legacyFolder);
        var lockedFile = Path.Combine(legacyFolder, "locked.pdf");
        File.WriteAllBytes(lockedFile, Encoding.UTF8.GetBytes("LOCKED_CONTENT"));

        var targetFolder = Path.Combine(_tempDir, "target");

        using (var fs = new FileStream(lockedFile, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            // Act & Assert — must not throw and must not block startup
            var exception = Record.Exception(() => LegacyCertificatesImporter.Import(legacyFolder, targetFolder));
            Assert.Null(exception);
        }
    }

    [Fact]
    public void ImportIfNeeded_UsesDefaultLegacyAndAppDataCertificatesPaths()
    {
        // Act & Assert — must not throw regardless of actual folder state on the test machine
        var exception = Record.Exception(() => LegacyCertificatesImporter.ImportIfNeeded());
        Assert.Null(exception);
    }

    [Fact]
    public void BackupService_DefaultCertificatesFolder_IsUnderAppDataDirectory()
    {
        // Arrange
        var sut = new BackupService(
            customDbPath: Path.Combine(_tempDir, "unused.db"),
            customBackupDir: Path.Combine(_tempDir, "Backups"),
            customCertificatesFolder: null,
            licenseService: new FakeLicenseService());

        var field = typeof(BackupService).GetField("_certificatesFolder", BindingFlags.NonPublic | BindingFlags.Instance);

        // Act
        var actualFolder = (string?)field!.GetValue(sut);

        // Assert
        Assert.Equal(Path.Combine(DatabasePaths.AppDataDirectory, "Certificates"), actualFolder);
    }

    [Fact]
    public void SourceCertificateService_DefaultCertificatesFolder_IsUnderAppDataDirectory()
    {
        // Arrange
        var sut = new SourceCertificateService(
            dbFactory: null!,
            auditService: Mock.Of<IAuditService>(),
            licenseService: new FakeLicenseService(),
            customCertificatesFolder: null);

        // Act
        var actualFolder = sut.GetCertificatesFolder();

        // Assert
        Assert.Equal(Path.Combine(DatabasePaths.AppDataDirectory, "Certificates"), actualFolder);
    }
}
