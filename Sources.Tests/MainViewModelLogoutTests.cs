using System;
using CommunityToolkit.Mvvm.ComponentModel;
using Moq;
using Sources.Helpers;
using Sources.Interfaces;
using Sources.Models;
using Sources.Services;
using Sources.Tests.Fakes;
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
        return new MainViewModel(_mockUserService.Object, _mockAlertService.Object, _mockSettingsService.Object, new FakeLicenseService());
    }

    [Fact]
    public void Logout_WhenCurrentViewIsEditing_AndFormIsOpen_ShowsWarningAndDoesNotLogout()
    {
        Fixtures.WpfStaFixture.RunInSta(() =>
        {
            // Arrange
            using var vm = CreateViewModel();
            Assert.True(vm.IsLoggedIn);

            var mockEditable = new MockEditableView(isEditing: true);
            vm.CurrentView = mockEditable;
            // الجولة 199 — حارس الشفاء الذاتي: يبقى الحجب كما هو فقط عندما تكون نافذة التحرير
            // مفتوحة فعلاً (EditingFormTracker.IsFormOpen يعيد true).
            EditingFormTracker.MarkOpen(mockEditable);

            try
            {
                // Act
                vm.LogoutCommand.Execute(null);

                // Assert: التحقق من ظهور رسالة التعديلات المعلقة وعدم تسجيل الخروج
                Assert.Equal(TranslationHelper.GetString("TitlePendingChanges"), DialogHelper.LastTitle);
                Assert.Equal(TranslationHelper.GetString("MsgErrSavePending"), DialogHelper.LastMessage);
                Assert.True(vm.IsLoggedIn);
                _mockUserService.Verify(u => u.Logout(), Times.Never);
            }
            finally
            {
                EditingFormTracker.MarkClosed(mockEditable);
            }
        });
    }

    [Fact]
    public void Logout_WhenCurrentViewIsEditing_ButNoFormIsOpen_ResetsWithoutSaving_AndProceeds()
    {
        Fixtures.WpfStaFixture.RunInSta(() =>
        {
            // Arrange: الجولة 199 — حارس الشفاء الذاتي. IsEditing عالق true بلا نافذة تحرير
            // فعلية مفتوحة (لم يُستدعَ EditingFormTracker.MarkOpen) يجب ألا يحجب تسجيل الخروج.
            DialogHelper.ShowConfirmationResult = true;
            using var vm = CreateViewModel();
            Assert.True(vm.IsLoggedIn);

            var mockEditable = new MockEditableView(isEditing: true);
            vm.CurrentView = mockEditable;

            // Act
            vm.LogoutCommand.Execute(null);

            // Assert: أُعيد ضبط IsEditing بلا حفظ، واستمرت عملية تسجيل الخروج
            Assert.False(mockEditable.IsEditing);
            _mockUserService.Verify(u => u.Logout(), Times.Once);

            DialogHelper.ShowConfirmationResult = null;
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
    public void Logout_WithAnyActiveEditableViewModelInEditingState_AndFormOpen_BlocksLogout(string screenName)
    {
        Fixtures.WpfStaFixture.RunInSta(() =>
        {
            // Arrange
            using var vm = CreateViewModel();
            var mockView = new MockNamedEditableView(screenName, isEditing: true);
            vm.CurrentView = mockView;
            // الجولة 199 — حارس الشفاء الذاتي: يبقى الحجب كما هو فقط عندما تكون نافذة التحرير
            // مفتوحة فعلاً.
            EditingFormTracker.MarkOpen(mockView);

            try
            {
                // Act
                vm.LogoutCommand.Execute(null);

                // Assert
                Assert.Equal(TranslationHelper.GetString("TitlePendingChanges"), DialogHelper.LastTitle);
                Assert.Equal(TranslationHelper.GetString("MsgErrSavePending"), DialogHelper.LastMessage);
                Assert.True(vm.IsLoggedIn);
            }
            finally
            {
                EditingFormTracker.MarkClosed(mockView);
            }
        });
    }

    [Fact]
    public void Logout_WhenCurrentUserMustChangePassword_ShowsReminderAndProceedsWithLogout()
    {
        Fixtures.WpfStaFixture.RunInSta(() =>
        {
            // Arrange
            _mockUserService.Setup(u => u.CurrentUser).Returns(new User
            {
                Id = Guid.NewGuid(),
                FullName = "مدير النظام",
                Username = "admin",
                Role = new Role { RoleName = "Admin" },
                MustChangePassword = true
            });
            DialogHelper.ShowConfirmationResult = true;

            using var vm = CreateViewModel();
            var mockEditable = new MockEditableView(isEditing: false);
            vm.CurrentView = mockEditable;

            // Act
            vm.LogoutCommand.Execute(null);

            // Assert: رسالة التذكير الأمني تظهر بعد التأكيد (تحدّث LastTitle/LastMessage)
            Assert.Equal(TranslationHelper.GetString("TitleReminderChangeDefaultPassword"), DialogHelper.LastTitle);
            Assert.Equal(TranslationHelper.GetString("MsgReminderChangeDefaultPassword"), DialogHelper.LastMessage);
            _mockUserService.Verify(u => u.Logout(), Times.Once);

            DialogHelper.ShowConfirmationResult = null;
        });
    }

    [Fact]
    public void Logout_WhenCurrentUserDoesNotNeedPasswordChange_DoesNotShowReminder()
    {
        Fixtures.WpfStaFixture.RunInSta(() =>
        {
            // Arrange
            _mockUserService.Setup(u => u.CurrentUser).Returns(new User
            {
                Id = Guid.NewGuid(),
                FullName = "مدير النظام",
                Username = "admin",
                Role = new Role { RoleName = "Admin" },
                MustChangePassword = false
            });
            DialogHelper.ShowConfirmationResult = true;

            using var vm = CreateViewModel();
            var mockEditable = new MockEditableView(isEditing: false);
            vm.CurrentView = mockEditable;

            // Act
            vm.LogoutCommand.Execute(null);

            // Assert: لا رسالة تذكير، فقط رسالة تأكيد الخروج نفسها هي آخر ما تم عرضه
            Assert.Equal(TranslationHelper.GetString("TitleLogout"), DialogHelper.LastTitle);
            Assert.Equal(TranslationHelper.GetString("MsgConfirmLogout"), DialogHelper.LastMessage);
            _mockUserService.Verify(u => u.Logout(), Times.Once);

            DialogHelper.ShowConfirmationResult = null;
        });
    }

    [Fact]
    public void NavigateTo_WhenCurrentViewIsEditing_AndFormIsOpen_ShowsWarningAndBlocksNavigation()
    {
        Fixtures.WpfStaFixture.RunInSta(() =>
        {
            using var vm = CreateViewModel();
            var mockEditable = new MockEditableView(isEditing: true);
            vm.CurrentView = mockEditable;
            vm.CurrentViewName = "Sources";
            EditingFormTracker.MarkOpen(mockEditable);

            try
            {
                vm.NavigateToCommand.Execute("Locations");

                Assert.Equal(TranslationHelper.GetString("TitlePendingChanges"), DialogHelper.LastTitle);
                Assert.Equal(TranslationHelper.GetString("MsgErrSavePending"), DialogHelper.LastMessage);
                Assert.Equal("Sources", vm.CurrentViewName);
                Assert.Same(mockEditable, vm.CurrentView);
            }
            finally
            {
                EditingFormTracker.MarkClosed(mockEditable);
            }
        });
    }

    [Fact]
    public void NavigateTo_WhenCurrentViewIsEditing_ButNoFormIsOpen_ResetsWithoutSaving_AndProceeds()
    {
        Fixtures.WpfStaFixture.RunInSta(() =>
        {
            using var vm = CreateViewModel();
            var mockEditable = new MockEditableView(isEditing: true);
            vm.CurrentView = mockEditable;
            vm.CurrentViewName = "Sources";

            // الجولة 199 — حارس الشفاء الذاتي: بلا EditingFormTracker.MarkOpen، أي IsEditing
            // عالق يُعاد ضبطه بلا حفظ وتُسمح المتابعة (لا حجب إلى الأبد).
            vm.NavigateToCommand.Execute("Locations");

            Assert.False(mockEditable.IsEditing);
            Assert.Equal("Locations", vm.CurrentViewName);
        });
    }

    public void Dispose()
    {
        DialogHelper.LastTitle = null;
        DialogHelper.LastMessage = null;
        DialogHelper.ShowConfirmationResult = null;
    }

    private sealed partial class MockEditableView : ObservableObject, IEditableViewModel
    {
        public bool IsEditing { get; set; }

        public MockEditableView(bool isEditing)
        {
            IsEditing = isEditing;
        }

        public void CancelEditing() => IsEditing = false;
    }

    private sealed partial class MockNamedEditableView : ObservableObject, IEditableViewModel
    {
        public string ScreenName { get; }
        public bool IsEditing { get; set; }

        public void CancelEditing() => IsEditing = false;

        public MockNamedEditableView(string screenName, bool isEditing)
        {
            ScreenName = screenName;
            IsEditing = isEditing;
        }
    }
}
