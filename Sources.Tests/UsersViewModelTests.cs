using System;
using System.Collections.Generic;
using System.Linq;
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
}
