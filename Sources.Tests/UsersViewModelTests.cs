using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.Messaging;
using Moq;
using Sources.Helpers;
using Sources.Models;
using Sources.Services;
using Sources.ViewModels;
using Xunit;

namespace Sources.Tests;

public class UsersViewModelTests
{
    [Fact]
    public void LoadData_ForNonAdminRole_DoesNotClaimUniformSectionsList()
    {
        // Arrange
        var adminRole = new Role { Id = Guid.NewGuid(), RoleName = "مدير النظام", Permissions = "All" };
        var userRole = new Role { Id = Guid.NewGuid(), RoleName = "مستخدم", Permissions = "" };

        var mockUserService = new Mock<IUserService>();
        mockUserService.Setup(s => s.GetAllUsers()).Returns(new List<User>());
        mockUserService.Setup(s => s.GetAllRoles()).Returns(new List<Role> { adminRole, userRole });
        mockUserService.Setup(s => s.GetAuditLogs(null, null, null)).Returns(new List<AuditLog>());

        var mockReportingService = new Mock<IReportingService>();

        // Act
        var vm = new UsersViewModel(mockUserService.Object, mockReportingService.Object);

        // Assert: دور المدير يبقى بصلاحياته الكاملة المعلنة
        var adminSummary = vm.RoleSummaries.First(r => r.Role.RoleName == "مدير النظام");
        Assert.Contains("كافة أقسام المنظومة", string.Join(" ", adminSummary.GrantedSections));

        // دور «مستخدم» لا يدّعي قائمة أقسام موحّدة؛ يوضّح أن الصلاحية فردية
        var userSummary = vm.RoleSummaries.First(r => r.Role.RoleName == "مستخدم");
        Assert.DoesNotContain(userSummary.GrantedSections, s => s.Contains("المصادر، النظائر، المواقع"));
        Assert.Contains(userSummary.GrantedSections, s => s.Contains("فردي"));
    }

    /// <summary>
    /// الجولة 151: كلا المسارين هنا متزامنان بالكامل (ToggleUserFreeze لا تمرّ بأي await
    /// عند تجميد الحساب الحالي نفسه، وDelete مع رفض التأكيد لا تصل لـDeleteUser/LoadData)
    /// — تعمداً لتفادي مخاطر الجمود (deadlock) داخل Dispatcher.Invoke المتزامن الذي
    /// تستعمله WpfStaFixture.RunInSta.
    /// </summary>
    [Fact]
    public void UsersViewModel_KeyMessages_UseEnglishStrings_WhenEnglishLanguageActive()
    {
        // Arrange
        var currentUser = new User { Id = Guid.NewGuid(), Username = "self_user", FullName = "Self User" };

        var mockUserService = new Mock<IUserService>();
        mockUserService.Setup(s => s.GetAllUsers()).Returns(new List<User> { currentUser });
        mockUserService.Setup(s => s.GetAllRoles()).Returns(new List<Role>());
        mockUserService.Setup(s => s.GetAuditLogs(null, null, null)).Returns(new List<AuditLog>());
        mockUserService.Setup(s => s.CurrentUser).Returns(currentUser);

        var mockReportingService = new Mock<IReportingService>();

        var vm = new UsersViewModel(mockUserService.Object, mockReportingService.Object);

        int arabicDictIndex = -1;
        Sources.Tests.Fixtures.WpfStaFixture.RunInSta(() =>
        {
            var dicts = System.Windows.Application.Current.Resources.MergedDictionaries;
            for (int i = 0; i < dicts.Count; i++)
            {
                var src = dicts[i].Source?.OriginalString;
                if (src != null && src.Contains("Strings.ar.xaml"))
                {
                    arabicDictIndex = i;
                    break;
                }
            }
            Assert.True(arabicDictIndex >= 0, "Strings.ar.xaml dictionary must already be loaded by WpfStaFixture.");

            var previousTestMode = DialogHelper.IsTestMode;
            DialogHelper.IsTestMode = true;
            try
            {
                dicts[arabicDictIndex] = new System.Windows.ResourceDictionary
                {
                    Source = new Uri("pack://application:,,,/Sources;component/Resources/Strings.en.xaml", UriKind.Absolute)
                };

                // Act & Assert: failure representative — cannot freeze own account (synchronous, no await/dialog)
                vm.ToggleUserFreezeCommand.Execute(currentUser);
                Assert.Equal("You cannot freeze your own account", vm.Message);

                // Act & Assert: confirmation-prompt representative — delete confirmation text (declined, so no DB call)
                vm.Selected = currentUser;
                DialogHelper.ShowConfirmationResult = false;
                vm.DeleteCommand.Execute(null);
                Assert.Equal("Are you sure you want to delete this user?", DialogHelper.LastMessage);
                Assert.Equal("Confirm Deletion", DialogHelper.LastTitle);
                Assert.DoesNotContain("هل أنت متأكد", DialogHelper.LastMessage);
            }
            finally
            {
                DialogHelper.IsTestMode = previousTestMode;
                DialogHelper.ShowConfirmationResult = null;
                DialogHelper.LastMessage = null;
                DialogHelper.LastTitle = null;
                dicts[arabicDictIndex] = new System.Windows.ResourceDictionary
                {
                    Source = new Uri("pack://application:,,,/Sources;component/Resources/Strings.ar.xaml", UriKind.Absolute)
                };
            }
        });
    }

    /// <summary>
    /// الجولة 195-A-fix: يتحقق أن مفتاحي فاصل قائمة الصلاحيات يحافظان على المسافة
    /// اللاصقة بهما بالعربية والإنجليزية (يتطلب xml:space="preserve" في كلا القاموسين).
    /// </summary>
    [Fact]
    public void TextPermissionSeparators_PreserveSpaces_InArabicAndEnglish()
    {
        Sources.Tests.Fixtures.WpfStaFixture.RunInSta(() =>
        {
            var dicts = System.Windows.Application.Current.Resources.MergedDictionaries;
            int arabicDictIndex = -1;
            for (int i = 0; i < dicts.Count; i++)
            {
                var src = dicts[i].Source?.OriginalString;
                if (src != null && src.Contains("Strings.ar.xaml"))
                {
                    arabicDictIndex = i;
                    break;
                }
            }
            Assert.True(arabicDictIndex >= 0, "Strings.ar.xaml dictionary must already be loaded by WpfStaFixture.");

            // Arabic (default-loaded dictionary)
            Assert.Equal(" ، ", TranslationHelper.GetString("TextPermissionDeltaSeparator"));
            Assert.Equal("، ", TranslationHelper.GetString("TextPermissionListSeparator"));

            try
            {
                dicts[arabicDictIndex] = new System.Windows.ResourceDictionary
                {
                    Source = new Uri("pack://application:,,,/Sources;component/Resources/Strings.en.xaml", UriKind.Absolute)
                };

                Assert.Equal(", ", TranslationHelper.GetString("TextPermissionDeltaSeparator"));
                Assert.Equal(", ", TranslationHelper.GetString("TextPermissionListSeparator"));
            }
            finally
            {
                dicts[arabicDictIndex] = new System.Windows.ResourceDictionary
                {
                    Source = new Uri("pack://application:,,,/Sources;component/Resources/Strings.ar.xaml", UriKind.Absolute)
                };
            }
        });
    }

    /// <summary>
    /// الجولة 198-A: عند فشل تحديث بيانات المستخدم في مسار التعديل، يجب ألا يُستدعى
    /// ResetPassword إطلاقاً حتى لو كانت كلمة مرور جديدة قد أُدخلت.
    /// </summary>
    [Fact]
    public void Save_EditPath_UpdateUserFails_NeverCallsResetPassword()
    {
        var existingUser = new User { Id = Guid.NewGuid(), Username = "target_user", FullName = "Target User" };
        var role = new Role { Id = Guid.NewGuid(), RoleName = "مستخدم", Permissions = "" };

        var mockUserService = new Mock<IUserService>();
        mockUserService.Setup(s => s.GetAllUsers()).Returns(new List<User> { existingUser });
        mockUserService.Setup(s => s.GetAllRoles()).Returns(new List<Role> { role });
        mockUserService.Setup(s => s.GetAuditLogs(null, null, null)).Returns(new List<AuditLog>());
        mockUserService.Setup(s => s.UpdateUser(It.IsAny<User>())).Returns((false, "فشل التحديث"));

        var mockReportingService = new Mock<IReportingService>();

        var vm = new UsersViewModel(mockUserService.Object, mockReportingService.Object, messenger: new WeakReferenceMessenger());
        vm.Selected = existingUser;
        vm.EditCommand.Execute(null);
        vm.EditFullName = "Target User Updated";
        vm.EditPassword = "NewP@ssw0rd1";

        vm.SaveCommand.Execute(null);

        mockUserService.Verify(s => s.ResetPassword(It.IsAny<Guid>(), It.IsAny<string>()), Times.Never);
        Assert.Equal("فشل التحديث", vm.Message);
        Assert.True(vm.IsEditing);
    }

    /// <summary>
    /// الجولة 198-A: عند نجاح تحديث بيانات المستخدم وفشل إعادة تعيين كلمة المرور،
    /// يجب أن تظهر رسالة تحذيرية توضح أن البيانات حُفظت لكن كلمة المرور لم تتغيّر.
    /// </summary>
    [Fact]
    public void Save_EditPath_UpdateSucceeds_ResetPasswordFails_ShowsPartialSaveWarning()
    {
        var existingUser = new User { Id = Guid.NewGuid(), Username = "target_user", FullName = "Target User" };
        var role = new Role { Id = Guid.NewGuid(), RoleName = "مستخدم", Permissions = "" };

        var mockUserService = new Mock<IUserService>();
        mockUserService.Setup(s => s.GetAllUsers()).Returns(new List<User> { existingUser });
        mockUserService.Setup(s => s.GetAllRoles()).Returns(new List<Role> { role });
        mockUserService.Setup(s => s.GetAuditLogs(null, null, null)).Returns(new List<AuditLog>());
        mockUserService.Setup(s => s.UpdateUser(It.IsAny<User>())).Returns((true, "تم الحفظ"));
        mockUserService.Setup(s => s.ResetPassword(It.IsAny<Guid>(), It.IsAny<string>())).Returns((false, "لا يمكن تعديل حساب المدير الأساسي"));

        var mockReportingService = new Mock<IReportingService>();

        var vm = new UsersViewModel(mockUserService.Object, mockReportingService.Object, messenger: new WeakReferenceMessenger());
        vm.Selected = existingUser;
        vm.EditCommand.Execute(null);
        vm.EditFullName = "Target User Updated";
        vm.EditPassword = "NewP@ssw0rd1";

        vm.SaveCommand.Execute(null);

        mockUserService.Verify(s => s.ResetPassword(existingUser.Id, "NewP@ssw0rd1"), Times.Once);
        var expectedMessage = string.Format(
            TranslationHelper.GetString("MsgWarnUserSavedPasswordNotChanged") ?? "تم حفظ بيانات المستخدم، لكن لم تُغيَّر كلمة المرور: {0}",
            "لا يمكن تعديل حساب المدير الأساسي");
        Assert.Equal(expectedMessage, vm.Message);
        Assert.Equal(expectedMessage, DialogHelper.LastMessage);
        Assert.False(vm.IsEditing);
    }

    /// <summary>
    /// الجولة 198-A: مسار النجاح الكامل (تحديث + إعادة تعيين كلمة المرور) يبقى دون تغيير سلوكي.
    /// </summary>
    [Fact]
    public void Save_EditPath_UpdateAndResetSucceed_ClosesFormWithSuccessMessage()
    {
        var existingUser = new User { Id = Guid.NewGuid(), Username = "target_user", FullName = "Target User" };
        var role = new Role { Id = Guid.NewGuid(), RoleName = "مستخدم", Permissions = "" };

        var mockUserService = new Mock<IUserService>();
        mockUserService.Setup(s => s.GetAllUsers()).Returns(new List<User> { existingUser });
        mockUserService.Setup(s => s.GetAllRoles()).Returns(new List<Role> { role });
        mockUserService.Setup(s => s.GetAuditLogs(null, null, null)).Returns(new List<AuditLog>());
        mockUserService.Setup(s => s.UpdateUser(It.IsAny<User>())).Returns((true, "تم حفظ بيانات المستخدم بنجاح"));
        mockUserService.Setup(s => s.ResetPassword(It.IsAny<Guid>(), It.IsAny<string>())).Returns((true, "تم تغيير كلمة المرور"));

        var mockReportingService = new Mock<IReportingService>();

        var vm = new UsersViewModel(mockUserService.Object, mockReportingService.Object, messenger: new WeakReferenceMessenger());
        vm.Selected = existingUser;
        vm.EditCommand.Execute(null);
        vm.EditFullName = "Target User Updated";
        vm.EditPassword = "NewP@ssw0rd1";

        vm.SaveCommand.Execute(null);

        mockUserService.Verify(s => s.ResetPassword(existingUser.Id, "NewP@ssw0rd1"), Times.Once);
        Assert.Equal("تم حفظ بيانات المستخدم بنجاح", vm.Message);
        Assert.False(vm.IsEditing);
    }
}
