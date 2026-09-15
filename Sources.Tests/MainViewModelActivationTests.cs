using System;
using System.IO;
using Moq;
using Sources.Helpers;
using Sources.Models;
using Sources.Services;
using Sources.Tests.Fakes;
using Sources.ViewModels;
using Xunit;

namespace Sources.Tests;

/// <summary>
/// اختبارات وحدة لخاصية IsTrialMode وأمر OpenActivationCommand في MainViewModel (الجولة 157).
/// تستعمل نفس آلية DialogHelper.IsTestMode المعتمدة في اختبارات PasswordPromptDialog
/// (انظر DeletionsAndAdminPromptTests) عبر خاصية MainViewModel.TestActivationSerialOverride الجديدة
/// لتفعيل المنظومة دون فتح ActivationDialog الفعلية في بيئة الاختبارات الآلية.
/// </summary>
public class MainViewModelActivationTests : IDisposable
{
    private const string ValidSerial = "SOURCES-2026-TRIAL-ACTIVATE";

    private readonly Mock<IUserService> _mockUserService;
    private readonly Mock<IAlertService> _mockAlertService;
    private readonly Mock<ISystemSettingsService> _mockSettingsService;

    public MainViewModelActivationTests()
    {
        _mockUserService = new Mock<IUserService>();
        _mockAlertService = new Mock<IAlertService>();
        _mockSettingsService = new Mock<ISystemSettingsService>();
        _mockUserService.Setup(u => u.IsLoggedIn).Returns(false);
    }

    public void Dispose()
    {
        MainViewModel.TestActivationSerialOverride = null;
    }

    [Fact]
    public void IsTrialMode_WhenLicenseNotActivated_IsTrue()
    {
        var license = new FakeLicenseService { IsActivated = false };
        var vm = new MainViewModel(_mockUserService.Object, _mockAlertService.Object, _mockSettingsService.Object, license);

        Assert.True(vm.IsTrialMode);
    }

    [Fact]
    public void IsTrialMode_WhenLicenseAlreadyActivated_IsFalse()
    {
        var license = new FakeLicenseService { IsActivated = true };
        var vm = new MainViewModel(_mockUserService.Object, _mockAlertService.Object, _mockSettingsService.Object, license);

        Assert.False(vm.IsTrialMode);
    }

    [Fact]
    public void OpenActivationCommand_WithValidSerialOverride_ActivatesAndClearsTrialMode()
    {
        // يُستعمل هنا LicenseService الحقيقي (بملف مؤقت معزول) بدل FakeLicenseService،
        // لأن FakeLicenseService.Activate ينجح دوماً بغض النظر عن المُدخل، ولا يصلح
        // للتحقق من مسار الرفض الفعلي عند رقم خاطئ في الاختبار التالي.
        var tempFile = Path.Combine(Path.GetTempPath(), $"mvm_license_test_{Guid.NewGuid():N}.dat");
        try
        {
            var license = new LicenseService(tempFile);
            var vm = new MainViewModel(_mockUserService.Object, _mockAlertService.Object, _mockSettingsService.Object, license);
            Assert.True(vm.IsTrialMode);

            MainViewModel.TestActivationSerialOverride = ValidSerial;

            vm.OpenActivationCommand.Execute(null);

            Assert.False(vm.IsTrialMode);
            Assert.True(license.IsActivated);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void OpenActivationCommand_WithInvalidSerialOverride_KeepsTrialModeTrue()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"mvm_license_test_{Guid.NewGuid():N}.dat");
        try
        {
            var license = new LicenseService(tempFile);
            var vm = new MainViewModel(_mockUserService.Object, _mockAlertService.Object, _mockSettingsService.Object, license);

            MainViewModel.TestActivationSerialOverride = "INVALID-SERIAL";

            vm.OpenActivationCommand.Execute(null);

            Assert.True(vm.IsTrialMode);
            Assert.False(license.IsActivated);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }
}
