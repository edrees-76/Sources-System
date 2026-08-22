using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Moq;
using Sources.Helpers;
using Sources.Models;
using Sources.Services;
using Sources.ViewModels;
using Xunit;

namespace Sources.Tests;

public class SettingsViewModelFactoryResetTests : IDisposable
{
    private readonly Mock<IBackupService> _mockBackupService;
    private readonly Mock<ISystemSettingsService> _mockSettingsService;
    private readonly Mock<IUserService> _mockUserService;
    private readonly Mock<ISystemResetService> _mockResetService;

    public SettingsViewModelFactoryResetTests()
    {
        DialogHelper.IsTestMode = true;
        _mockBackupService = new Mock<IBackupService>();
        _mockSettingsService = new Mock<ISystemSettingsService>();
        _mockSettingsService.Setup(s => s.GetAllSettings()).Returns(new Dictionary<string, string>());
        _mockUserService = new Mock<IUserService>();
        _mockResetService = new Mock<ISystemResetService>();
    }

    public void Dispose()
    {
        DialogHelper.IsTestMode = false;
        DialogHelper.LastMessage = null;
        DialogHelper.LastTitle = null;
    }

    [Fact]
    public void IsAdmin_ReturnsTrue_WhenUserIsAdmin()
    {
        // Arrange
        var adminRole = new Role { RoleName = "مدير النظام" };
        var adminUser = new User { Username = "admin", Role = adminRole };
        _mockUserService.Setup(u => u.CurrentUser).Returns(adminUser);

        var vm = new SettingsViewModel(
            _mockBackupService.Object,
            _mockSettingsService.Object,
            null,
            _mockUserService.Object,
            _mockResetService.Object);

        // Assert
        Assert.True(vm.IsAdmin);
    }

    [Fact]
    public void IsAdmin_ReturnsFalse_WhenUserIsNotAdmin()
    {
        // Arrange
        var operatorRole = new Role { RoleName = "مشغل" };
        var operatorUser = new User { Username = "operator", Role = operatorRole };
        _mockUserService.Setup(u => u.CurrentUser).Returns(operatorUser);

        var vm = new SettingsViewModel(
            _mockBackupService.Object,
            _mockSettingsService.Object,
            null,
            _mockUserService.Object,
            _mockResetService.Object);

        // Assert
        Assert.False(vm.IsAdmin);
    }

    [Fact]
    public void VerifyResetPhrase_ExactMatch_PassesStage1()
    {
        // Arrange
        var vm = new SettingsViewModel(
            _mockBackupService.Object,
            _mockSettingsService.Object,
            null,
            _mockUserService.Object,
            _mockResetService.Object);

        // Act
        vm.ResetPhrase = "إعادة ضبط المنظومة";
        vm.VerifyResetPhraseCommand.Execute(null);

        // Assert
        Assert.True(vm.IsStage1Passed);
    }

    [Fact]
    public void VerifyResetPhrase_IncorrectPhrase_FailsStage1()
    {
        // Arrange
        var vm = new SettingsViewModel(
            _mockBackupService.Object,
            _mockSettingsService.Object,
            null,
            _mockUserService.Object,
            _mockResetService.Object);

        // Act
        vm.ResetPhrase = "إعادة ضبط";
        vm.VerifyResetPhraseCommand.Execute(null);

        // Assert
        Assert.False(vm.IsStage1Passed);
    }

    [Fact]
    public void VerifyResetPassword_CorrectPassword_PassesStage2()
    {
        // Arrange
        var rawPassword = "AdminSecretPassword123";
        var hash = PasswordHelper.HashPassword(rawPassword);
        var adminUser = new User { Username = "admin", PasswordHash = hash };
        _mockUserService.Setup(u => u.CurrentUser).Returns(adminUser);

        var vm = new SettingsViewModel(
            _mockBackupService.Object,
            _mockSettingsService.Object,
            null,
            _mockUserService.Object,
            _mockResetService.Object);

        vm.ResetPhrase = "إعادة ضبط المنظومة";
        vm.VerifyResetPhraseCommand.Execute(null);
        Assert.True(vm.IsStage1Passed);

        // Act
        vm.ResetPassword = rawPassword;
        vm.VerifyResetPasswordCommand.Execute(null);

        // Assert
        Assert.True(vm.IsStage2Passed);
    }

    [Fact]
    public void VerifyResetPassword_IncorrectPassword_FailsStage2()
    {
        // Arrange
        var rawPassword = "AdminSecretPassword123";
        var hash = PasswordHelper.HashPassword(rawPassword);
        var adminUser = new User { Username = "admin", PasswordHash = hash };
        _mockUserService.Setup(u => u.CurrentUser).Returns(adminUser);

        var vm = new SettingsViewModel(
            _mockBackupService.Object,
            _mockSettingsService.Object,
            null,
            _mockUserService.Object,
            _mockResetService.Object);

        vm.ResetPhrase = "إعادة ضبط المنظومة";
        vm.VerifyResetPhraseCommand.Execute(null);

        // Act
        vm.ResetPassword = "WrongPassword";
        vm.VerifyResetPasswordCommand.Execute(null);

        // Assert
        Assert.False(vm.IsStage2Passed);
    }
}
