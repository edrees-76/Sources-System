using System;
using Moq;
using Sources.Helpers;
using Sources.Services;
using Sources.Tests.Fakes;
using Sources.ViewModels;
using Xunit;

namespace Sources.Tests;

/// <summary>
/// اختبارات معالج الإعداد الأول: خاصية الإكمال في SettingsHelper، وسلوك أوامر
/// FirstRunWizardViewModel في الحالات الثلاث (مفعّل، تجريبي، تحذير المسار الفارغ) وأمر التخطي.
/// </summary>
public class FirstRunWizardTests : IDisposable
{
    public FirstRunWizardTests()
    {
        SettingsHelper.ClearAllUserSettingsForTesting();
    }

    public void Dispose()
    {
        SettingsHelper.ClearAllUserSettingsForTesting();
    }

    #region SettingsHelper.FirstRunWizardCompleted

    [Fact]
    public void FirstRunWizardCompleted_DefaultsToFalse()
    {
        Assert.False(SettingsHelper.FirstRunWizardCompleted);
    }

    [Fact]
    public void FirstRunWizardCompleted_SetTrue_RoundTrips()
    {
        SettingsHelper.FirstRunWizardCompleted = true;

        Assert.True(SettingsHelper.FirstRunWizardCompleted);
    }

    [Fact]
    public void FirstRunWizardCompleted_SetFalseAfterTrue_RoundTrips()
    {
        SettingsHelper.FirstRunWizardCompleted = true;
        SettingsHelper.FirstRunWizardCompleted = false;

        Assert.False(SettingsHelper.FirstRunWizardCompleted);
    }

    #endregion

    #region FirstRunWizardViewModel.Finish()

    [Fact]
    public void Finish_Activated_SavesBothSettings_SetsFlag_AndCloses()
    {
        // Arrange
        var mockSettings = new Mock<ISystemSettingsService>(MockBehavior.Strict);
        mockSettings.Setup(s => s.SaveSetting("BackupPath", "D:\\Backups"));
        mockSettings.Setup(s => s.SaveSetting("AutoBackupEnabled", "True"));
        var fakeLicense = new FakeLicenseService { IsActivated = true };

        var vm = new FirstRunWizardViewModel(mockSettings.Object, fakeLicense)
        {
            BackupPath = "D:\\Backups",
            AutoBackupEnabled = true
        };

        var closeRaised = false;
        vm.CloseRequested += (s, e) => closeRaised = true;

        // Act
        vm.FinishCommand.Execute(null);

        // Assert
        mockSettings.Verify(s => s.SaveSetting("BackupPath", "D:\\Backups"), Times.Once);
        mockSettings.Verify(s => s.SaveSetting("AutoBackupEnabled", "True"), Times.Once);
        Assert.True(SettingsHelper.FirstRunWizardCompleted);
        Assert.True(closeRaised);
    }

    [Fact]
    public void Finish_Trial_DoesNotCallSaveSetting_ShowsInfo_SetsFlag_AndCloses()
    {
        // Arrange
        var mockSettings = new Mock<ISystemSettingsService>(MockBehavior.Strict);
        var fakeLicense = new FakeLicenseService { IsActivated = false };

        var vm = new FirstRunWizardViewModel(mockSettings.Object, fakeLicense)
        {
            BackupPath = string.Empty,
            AutoBackupEnabled = false
        };

        var closeRaised = false;
        vm.CloseRequested += (s, e) => closeRaised = true;

        // Act
        vm.FinishCommand.Execute(null);

        // Assert: لا استدعاء إطلاقاً لـ SaveSetting (Mock صارم Strict يرمي إن استُدعيت أي دالة غير مُعدّة)
        mockSettings.VerifyNoOtherCalls();
        Assert.True(SettingsHelper.FirstRunWizardCompleted);
        Assert.True(closeRaised);
    }

    [Fact]
    public void Finish_EmptyPathWithAutoBackupEnabled_ShowsWarning_DoesNotSetFlag_AndDoesNotClose()
    {
        // Arrange
        var mockSettings = new Mock<ISystemSettingsService>(MockBehavior.Strict);
        var fakeLicense = new FakeLicenseService { IsActivated = true };

        var vm = new FirstRunWizardViewModel(mockSettings.Object, fakeLicense)
        {
            BackupPath = "   ",
            AutoBackupEnabled = true
        };

        var closeRaised = false;
        vm.CloseRequested += (s, e) => closeRaised = true;

        // Act
        vm.FinishCommand.Execute(null);

        // Assert
        mockSettings.VerifyNoOtherCalls();
        Assert.False(SettingsHelper.FirstRunWizardCompleted);
        Assert.False(closeRaised);
    }

    #endregion

    #region FirstRunWizardViewModel.Skip()

    [Fact]
    public void Skip_SetsFlag_NeverCallsSaveSetting_AndCloses()
    {
        // Arrange
        var mockSettings = new Mock<ISystemSettingsService>(MockBehavior.Strict);
        var fakeLicense = new FakeLicenseService { IsActivated = true };

        var vm = new FirstRunWizardViewModel(mockSettings.Object, fakeLicense)
        {
            BackupPath = string.Empty,
            AutoBackupEnabled = false
        };

        var closeRaised = false;
        vm.CloseRequested += (s, e) => closeRaised = true;

        // Act
        vm.SkipCommand.Execute(null);

        // Assert
        mockSettings.VerifyNoOtherCalls();
        Assert.True(SettingsHelper.FirstRunWizardCompleted);
        Assert.True(closeRaised);
    }

    #endregion
}
