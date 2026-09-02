using System;
using System.IO;
using System.Text;
using Sources.Helpers;
using Xunit;

namespace Sources.Tests;

public class SettingsHelperMigrationTests : IDisposable
{
    private readonly string _tempDir;

    public SettingsHelperMigrationTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "Sources_SettingsMigrationTest_" + Guid.NewGuid().ToString("N"));
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
    public void MigrateLegacySettings_WhenLegacyFileMissing_ReturnsNullAndCreatesNothing()
    {
        var legacyPath = Path.Combine(_tempDir, "missing_settings.ini");
        var targetPath = Path.Combine(_tempDir, "target_settings.ini");

        var warning = SettingsHelper.MigrateLegacySettings(legacyPath, targetPath);

        Assert.Null(warning);
        Assert.False(File.Exists(targetPath));
    }

    [Fact]
    public void MigrateLegacySettings_WhenTargetExists_ReturnsNullAndLeavesTargetUnchanged()
    {
        var legacyPath = Path.Combine(_tempDir, "legacy_settings.ini");
        var targetPath = Path.Combine(_tempDir, "target_settings.ini");

        var legacyBytes = Encoding.UTF8.GetBytes("Theme=Dark\nLanguage=ar");
        var targetBytes = Encoding.UTF8.GetBytes("Theme=Light\nLanguage=en");

        File.WriteAllBytes(legacyPath, legacyBytes);
        File.WriteAllBytes(targetPath, targetBytes);

        var warning = SettingsHelper.MigrateLegacySettings(legacyPath, targetPath);

        Assert.Null(warning);
        Assert.Equal(targetBytes, File.ReadAllBytes(targetPath));
    }

    [Fact]
    public void MigrateLegacySettings_WhenLegacyExists_CopiesItWithIdenticalBytes()
    {
        var legacyPath = Path.Combine(_tempDir, "legacy_settings.ini");
        var targetPath = Path.Combine(_tempDir, "target_settings.ini");

        var legacyBytes = Encoding.UTF8.GetBytes("Theme=Dark\nAccentColor=#1F5A66\nLanguage=ar");
        File.WriteAllBytes(legacyPath, legacyBytes);

        var warning = SettingsHelper.MigrateLegacySettings(legacyPath, targetPath);

        Assert.Null(warning);
        Assert.True(File.Exists(targetPath));
        Assert.Equal(legacyBytes, File.ReadAllBytes(targetPath));
    }

    [Fact]
    public void MigrateLegacySettings_WhenLegacyFileLocked_ReturnsWarningContainingBothPaths()
    {
        var legacyPath = Path.Combine(_tempDir, "locked_legacy_settings.ini");
        var targetPath = Path.Combine(_tempDir, "target_settings.ini");

        File.WriteAllText(legacyPath, "Theme=Dark");

        using (new FileStream(legacyPath, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            var warning = SettingsHelper.MigrateLegacySettings(legacyPath, targetPath);

            Assert.NotNull(warning);
            Assert.Contains(legacyPath, warning);
            Assert.Contains(targetPath, warning);
            Assert.False(File.Exists(targetPath));
        }
    }

    [Fact]
    public void MigrateLegacySettings_WhenLegacyFileLocked_DoesNotThrow()
    {
        var legacyPath = Path.Combine(_tempDir, "locked_legacy_settings_nothrow.ini");
        var targetPath = Path.Combine(_tempDir, "target_settings_nothrow.ini");

        File.WriteAllText(legacyPath, "Theme=Dark");

        using (new FileStream(legacyPath, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            var exception = Record.Exception(() => SettingsHelper.MigrateLegacySettings(legacyPath, targetPath));
            Assert.Null(exception);
        }
    }
}
