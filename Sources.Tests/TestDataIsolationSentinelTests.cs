using System;
using System.IO;
using System.Reflection;
using Sources.Data;
using Sources.Services;
using Xunit;

namespace Sources.Tests;

/// <summary>
/// الحارس (sentinel) لعزل بيانات الاختبار (الجولة 199): يتأكد أن كل مسار مشتق من
/// DatabasePaths.AppDataDirectory — القاعدة، النسخ الاحتياطي، السجلات، الإعدادات، ومجلدات
/// الشهادات الافتراضية للخدمات — يقع خارج مجلد %LOCALAPPDATA%\Sources الحقيقي أثناء تشغيل
/// الاختبارات، وأن التوجيه البديل مضبوط فعلاً. يجب أن يمر هذا الاختبار قبل السماح بتشغيل أي
/// مجموعة اختبارات أخرى تلمس DatabasePaths / LoggerService / BackupService / SettingsHelper.
/// </summary>
public class TestDataIsolationSentinelTests
{
    private static string RealRoot => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Sources");

    private static bool IsUnderRealRoot(string path)
    {
        var full = Path.GetFullPath(path);
        var real = Path.GetFullPath(RealRoot);
        return full.StartsWith(real, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AppDataDirectoryOverride_IsSet_ForTestAssembly()
    {
        Assert.NotNull(DatabasePaths.AppDataDirectoryOverride);
    }

    [Fact]
    public void DatabasePaths_AllDerivedPaths_AreOutsideRealAppData()
    {
        Assert.False(IsUnderRealRoot(DatabasePaths.AppDataDirectory),
            $"AppDataDirectory resolved under the real folder: {DatabasePaths.AppDataDirectory}");
        Assert.False(IsUnderRealRoot(DatabasePaths.DbPath),
            $"DbPath resolved under the real folder: {DatabasePaths.DbPath}");
        Assert.False(IsUnderRealRoot(DatabasePaths.BackupsDirectory),
            $"BackupsDirectory resolved under the real folder: {DatabasePaths.BackupsDirectory}");
        Assert.False(IsUnderRealRoot(DatabasePaths.LogsDirectory),
            $"LogsDirectory resolved under the real folder: {DatabasePaths.LogsDirectory}");
    }

    [Fact]
    public void LoggerService_LogDir_IsOutsideRealAppData()
    {
        var field = typeof(LoggerService).GetField("LogDir", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(field);
        var logDir = (string)field!.GetValue(null)!;
        Assert.False(IsUnderRealRoot(logDir), $"LoggerService.LogDir resolved under the real folder: {logDir}");
    }

    [Fact]
    public void SettingsHelper_SettingsDirAndFile_AreOutsideRealAppData()
    {
        var settingsHelperType = Type.GetType("Sources.Helpers.SettingsHelper, Sources");
        Assert.NotNull(settingsHelperType);

        var dirField = settingsHelperType!.GetField("SettingsDir", BindingFlags.NonPublic | BindingFlags.Static);
        var fileField = settingsHelperType.GetField("SettingsFile", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(dirField);
        Assert.NotNull(fileField);

        var settingsDir = (string)dirField!.GetValue(null)!;
        var settingsFile = (string)fileField!.GetValue(null)!;

        Assert.False(IsUnderRealRoot(settingsDir), $"SettingsHelper.SettingsDir resolved under the real folder: {settingsDir}");
        Assert.False(IsUnderRealRoot(settingsFile), $"SettingsHelper.SettingsFile resolved under the real folder: {settingsFile}");
    }

    [Fact]
    public void BackupService_DefaultCertificatesFolder_IsOutsideRealAppData()
    {
        var sut = new Sources.Services.BackupService();
        var field = typeof(Sources.Services.BackupService).GetField("_certificatesFolder", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(field);
        var certsFolder = (string)field!.GetValue(sut)!;
        Assert.False(IsUnderRealRoot(certsFolder), $"BackupService default certificates folder resolved under the real folder: {certsFolder}");
    }

    [Fact]
    public void SourceCertificateService_DefaultCertificatesFolder_IsOutsideRealAppData()
    {
        var sut = new Sources.Services.SourceCertificateService(
            dbFactory: null!,
            auditService: Moq.Mock.Of<Sources.Services.IAuditService>(),
            licenseService: new Fakes.FakeLicenseService());
        var certsFolder = sut.GetCertificatesFolder();
        Assert.False(IsUnderRealRoot(certsFolder), $"SourceCertificateService default certificates folder resolved under the real folder: {certsFolder}");
    }
}
