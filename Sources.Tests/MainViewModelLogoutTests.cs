using System;
using CommunityToolkit.Mvvm.ComponentModel;
using Moq;
using Sources.Helpers;
using Sources.Interfaces;
using Sources.Models;
using Sources.Services;
using Sources.ViewModels;
using Xunit;

namespace Sources.Tests;

public class MainViewModelLogoutTests : IDisposable
{
    private readonly Mock<IUserService> _mockUserService;
    private readonly Mock<IAlertService> _mockAlertService;
    private readonly Mock<ISystemSettingsService> _mockSettingsService;

    public MainViewModelLogoutTests()
    {
        DialogHelper.IsTestMode = true;
        _mockUserService = new Mock<IUserService>();
        _mockAlertService = new Mock<IAlertService>();
        _mockSettingsService = new Mock<ISystemSettingsService>();

        _mockUserService.Setup(u => u.IsLoggedIn).Returns(true);
        _mockUserService.Setup(u => u.CurrentUser).Returns(new User
        {
            Id = Guid.NewGuid(),
            FullName = "مدير النظام",
            Username = "admin",
            Role = new Role { RoleName = "Admin" }
        });
    }

    private MainViewModel CreateViewModel()
    {
        return new MainViewModel(_mockUserService.Object, _mockAlertService.Object, _mockSettingsService.Object);
    }

    [Fact]
    public void Logout_WhenCurrentViewIsEditing_ShowsWarningAndDoesNotLogout()
    {
        Fixtures.WpfStaFixture.RunInSta(() =>
        {
            // Arrange
            using var vm = CreateViewModel();
            Assert.True(vm.IsLoggedIn);

            var mockEditable = new MockEditableView(isEditing: true);
            vm.CurrentView = mockEditable;

            // Act
            vm.LogoutCommand.Execute(null);

            // Assert: التحقق من ظهور رسالة التعديلات المعلقة وعدم تسجيل الخروج
            Assert.Equal(TranslationHelper.GetString("TitlePendingChanges"), DialogHelper.LastTitle);
            Assert.Equal(TranslationHelper.GetString("MsgErrSavePending"), DialogHelper.LastMessage);
            Assert.True(vm.IsLoggedIn);
            _mockUserService.Verify(u => u.Logout(), Times.Never);
        });
    }

    [Fact]
    public void Logout_WhenCurrentViewIsNotEditing_PromptsConfirmationAndProceeds()
    {
        Fixtures.WpfStaFixture.RunInSta(() =>
        {
            // Arrange
            using var vm = CreateViewModel();
            Assert.True(vm.IsLoggedIn);

            var mockEditable = new MockEditableView(isEditing: false);
            vm.CurrentView = mockEditable;

            // Act
            vm.LogoutCommand.Execute(null);

            // Assert: في وضع الاختبار ShowConfirmation يرجع true ويُطلب تأكيد الخروج
            Assert.Equal(TranslationHelper.GetString("TitleLogout"), DialogHelper.LastTitle);
            Assert.Equal(TranslationHelper.GetString("MsgConfirmLogout"), DialogHelper.LastMessage);
        });
    }

    [Theory]
    [InlineData("Sources")]
    [InlineData("Locations")]
    [InlineData("Borrowing")]
    [InlineData("Users")]
    [InlineData("Radioisotopes")]
    public void Logout_WithAnyActiveEditableViewModelInEditingState_BlocksLogout(string screenName)
    {
        Fixtures.WpfStaFixture.RunInSta(() =>
        {
            // Arrange
            using var vm = CreateViewModel();
            var mockView = new MockNamedEditableView(screenName, isEditing: true);
            vm.CurrentView = mockView;

            // Act
            vm.LogoutCommand.Execute(null);

            // Assert
            Assert.Equal(TranslationHelper.GetString("TitlePendingChanges"), DialogHelper.LastTitle);
            Assert.Equal(TranslationHelper.GetString("MsgErrSavePending"), DialogHelper.LastMessage);
            Assert.True(vm.IsLoggedIn);
        });
    }

    public void Dispose()
    {
        DialogHelper.IsTestMode = false;
        DialogHelper.LastTitle = null;
        DialogHelper.LastMessage = null;
    }

    private sealed partial class MockEditableView : ObservableObject, IEditableViewModel
    {
        public bool IsEditing { get; set; }

        public MockEditableView(bool isEditing)
        {
            IsEditing = isEditing;
        }
    }

    private sealed partial class MockNamedEditableView : ObservableObject, IEditableViewModel
    {
        public string ScreenName { get; }
        public bool IsEditing { get; set; }

        public MockNamedEditableView(string screenName, bool isEditing)
        {
            ScreenName = screenName;
            IsEditing = isEditing;
        }
    }
}
