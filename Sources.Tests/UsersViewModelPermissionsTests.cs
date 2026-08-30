using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Moq;
using Sources.Data;
using Sources.Models;
using Sources.Services;
using Sources.ViewModels;
using Xunit;

namespace Sources.Tests;

public class UsersViewModelPermissionsTests
{
    private readonly Mock<IUserService> _mockUserService;
    private readonly Mock<IReportingService> _mockReportingService;

    public UsersViewModelPermissionsTests()
    {
        _mockUserService = new Mock<IUserService>();
        _mockReportingService = new Mock<IReportingService>();

        _mockUserService.Setup(s => s.GetAllUsers()).Returns(new List<User>());
        _mockUserService.Setup(s => s.GetAllRoles()).Returns(new List<Role>
        {
            new Role { Id = Guid.NewGuid(), RoleName = "مدير النظام" },
            new Role { Id = Guid.NewGuid(), RoleName = "مستخدم عادي" }
        });
        _mockUserService.Setup(s => s.GetAuditLogs(It.IsAny<Guid?>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
                        .Returns(new List<AuditLog>());
    }

    private UsersViewModel CreateViewModel()
    {
        return new UsersViewModel(_mockUserService.Object, _mockReportingService.Object);
    }

    #region 1. Unpack & Pack Tests (Fix 1, 2, 3)

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void UnpackPermissions_WhenNullOrEmpty_SetsAllPermissionsToFalse(string? emptyPerms)
    {
        // Arrange
        var vm = CreateViewModel();
        var user = new User
        {
            Id = Guid.NewGuid(),
            FullName = "Test Zero Perms",
            Username = "zeroperms",
            Permissions = emptyPerms
        };
        vm.Selected = user;

        // Act
        vm.EditCommand.Execute(null);

        // Assert - كل الصلاحيات يجب أن تكون false تماماً
        Assert.False(vm.PermRadioisotopes);
        Assert.False(vm.PermSources);
        Assert.False(vm.PermLocations);
        Assert.False(vm.PermBorrowing);
        Assert.False(vm.PermReports);
        Assert.False(vm.PermUsers);
        Assert.False(vm.PermSettings);
        Assert.False(vm.PermCalculator);
        Assert.False(vm.PermDeletions);
    }

    [Theory]
    [InlineData("All")]
    [InlineData("all")]
    [InlineData("ALL")]
    [InlineData(" All ")]
    public void UnpackPermissions_WhenAllCaseInsensitive_SetsAllPermissionsToTrue(string allPerms)
    {
        // Arrange
        var vm = CreateViewModel();
        var user = new User
        {
            Id = Guid.NewGuid(),
            FullName = "Test All Perms",
            Username = "allperms",
            Permissions = allPerms
        };
        vm.Selected = user;

        // Act
        vm.EditCommand.Execute(null);

        // Assert - كافة الصلاحيات التسعة يجب أن تكون true
        Assert.True(vm.PermRadioisotopes);
        Assert.True(vm.PermSources);
        Assert.True(vm.PermLocations);
        Assert.True(vm.PermBorrowing);
        Assert.True(vm.PermReports);
        Assert.True(vm.PermUsers);
        Assert.True(vm.PermSettings);
        Assert.True(vm.PermCalculator);
        Assert.True(vm.PermDeletions);
    }

    [Fact]
    public void UnpackAndPack_WhenCustomOrLegacyPermissionsPresent_PreservesThemOnSave()
    {
        // Arrange
        var vm = CreateViewModel();
        var normalRoleId = vm.Roles.First(r => r.RoleName == "مستخدم عادي").Id;
        var user = new User
        {
            Id = Guid.NewGuid(),
            FullName = "Custom Perm User",
            Username = "customuser",
            RoleId = normalRoleId,
            Permissions = "Sources,Reports,CustomSectionX,LegacyPermY"
        };
        vm.Selected = user;

        // Act - فتح شاشة التعديل
        vm.EditCommand.Execute(null);

        // Assert Unpack
        Assert.True(vm.PermSources);
        Assert.True(vm.PermReports);
        Assert.False(vm.PermLocations);

        // Act - تفعيل الموقع وحفظ
        vm.PermLocations = true;

        User? updatedUser = null;
        _mockUserService.Setup(s => s.UpdateUser(It.IsAny<User>()))
                        .Callback<User>(u => updatedUser = u)
                        .Returns((true, "تم التحديث"));

        vm.SaveCommand.Execute(null);

        // Assert Pack
        Assert.NotNull(updatedUser);
        Assert.Contains("Sources", updatedUser!.Permissions!);
        Assert.Contains("Reports", updatedUser.Permissions!);
        Assert.Contains("Locations", updatedUser.Permissions!);
        Assert.Contains("CustomSectionX", updatedUser.Permissions!);
        Assert.Contains("LegacyPermY", updatedUser.Permissions!);
    }

    #endregion

    #region 2. User.HasSectionPermission Tests (Fix 2)

    [Theory]
    [InlineData("all")]
    [InlineData("ALL")]
    [InlineData(" All ")]
    public void User_HasSectionPermission_WhenPermissionsIsAllCaseInsensitive_ReturnsTrue(string allValue)
    {
        // Arrange
        var user = new User
        {
            Username = "case_user",
            Role = new Role { RoleName = "مستخدم عادي" },
            Permissions = allValue
        };

        // Act & Assert
        Assert.True(user.HasSectionPermission("Sources"));
        Assert.True(user.HasSectionPermission("Reports"));
        Assert.True(user.HasSectionPermission("Radioisotopes"));
    }

    #endregion

    #region 3. Diff Viewer Tests for Permissions (Fix 4)

    [Fact]
    public void OpenDiffViewer_WhenPermissionsChanged_FormatsAddedAndRemovedPermissionsWithTranslation()
    {
        // Arrange
        var vm = CreateViewModel();
        var log = new AuditLog
        {
            Action = "Update",
            TableName = "Users",
            OldValues = JsonSerializer.Serialize(new { Permissions = "Sources,Reports" }),
            NewValues = JsonSerializer.Serialize(new { Permissions = "Sources,Locations,ActivityCalculator" })
        };

        // Act
        vm.OpenDiffViewerCommand.Execute(log);

        // Assert
        Assert.True(vm.IsDiffViewerOpen);
        var permDiff = vm.AuditDiffItems.FirstOrDefault(d => d.FieldName == "صلاحيات الوصول للأقسام");
        Assert.NotNull(permDiff);
        Assert.True(permDiff!.HasChanged);

        // القديم يجب أن يحتوي على المصادر والتقارير
        Assert.Contains("المصادر المشعة", permDiff.OldValue);
        Assert.Contains("التقارير", permDiff.OldValue);

        // الجديد يجب أن يوضح الصلاحيات المضافة (+) والمحذوفة (-)
        Assert.Contains("+ المواقع", permDiff.NewValue);
        Assert.Contains("+ الحاسبة الإشعاعية", permDiff.NewValue);
        Assert.Contains("- التقارير", permDiff.NewValue);
    }

    [Fact]
    public void OpenDiffViewer_WhenPermissionsIdentical_HasChangedIsFalse()
    {
        // Arrange
        var vm = CreateViewModel();
        var log = new AuditLog
        {
            Action = "Update",
            TableName = "Users",
            OldValues = JsonSerializer.Serialize(new { Permissions = "Sources,Reports" }),
            NewValues = JsonSerializer.Serialize(new { Permissions = "Sources,Reports" })
        };

        // Act
        vm.OpenDiffViewerCommand.Execute(log);

        // Assert
        var permDiff = vm.AuditDiffItems.FirstOrDefault(d => d.FieldName == "صلاحيات الوصول للأقسام");
        Assert.NotNull(permDiff);
        Assert.False(permDiff!.HasChanged);
    }

    #endregion

    #region 4. Deletions Permission & Sidebar Decoupling Tests (Round 83)

    [Fact]
    public void SidebarPermissions_UserWithDeletionsPermissionOnly_CanSeeDeletionsIsTrue()
    {
        // Arrange
        var mockUserService = new Mock<IUserService>();
        var user = new User
        {
            FullName = "مسؤول المحذوفات",
            Username = "del_manager",
            Role = new Role { RoleName = "مستخدم عادي" },
            Permissions = "Deletions"
        };
        mockUserService.Setup(u => u.CurrentUser).Returns(user);
        mockUserService.Setup(u => u.IsLoggedIn).Returns(true);

        var mockAlertService = new Mock<IAlertService>();
        var mockSettingsService = new Mock<ISystemSettingsService>();

        // Act
        var mainVm = new MainViewModel(mockUserService.Object, mockAlertService.Object, mockSettingsService.Object);

        // Assert - يرى رابط المحذوفات حصراً
        Assert.True(mainVm.CanSeeDeletions);
        Assert.False(mainVm.CanSeeSettings);
        Assert.False(mainVm.CanSeeUsers);
    }

    [Fact]
    public void SidebarPermissions_UserWithSettingsPermissionOnly_CanSeeDeletionsIsFalse()
    {
        // Arrange
        var mockUserService = new Mock<IUserService>();
        var user = new User
        {
            FullName = "مستخدم الإعدادات فقط",
            Username = "settings_only_user",
            Role = new Role { RoleName = "مستخدم عادي" },
            Permissions = "Settings" // يملك صلاحية الإعدادات فقط بدون Deletions
        };
        mockUserService.Setup(u => u.CurrentUser).Returns(user);
        mockUserService.Setup(u => u.IsLoggedIn).Returns(true);

        var mockAlertService = new Mock<IAlertService>();
        var mockSettingsService = new Mock<ISystemSettingsService>();

        // Act
        var mainVm = new MainViewModel(mockUserService.Object, mockAlertService.Object, mockSettingsService.Object);

        // Assert - يرى الإعدادات ولكن لا يرى المحذوفات إطلاقاً (كسر الاقتران القديم)
        Assert.True(mainVm.CanSeeSettings);
        Assert.False(mainVm.CanSeeDeletions);
    }

    [Fact]
    public void SidebarPermissions_UserWithUsersPermissionOnly_CanSeeDeletionsIsFalse()
    {
        // Arrange
        var mockUserService = new Mock<IUserService>();
        var user = new User
        {
            FullName = "مستخدم إدارة المستخدمين فقط",
            Username = "users_only_user",
            Role = new Role { RoleName = "مستخدم عادي" },
            Permissions = "Users" // يملك صلاحية المستخدمين فقط بدون Deletions
        };
        mockUserService.Setup(u => u.CurrentUser).Returns(user);
        mockUserService.Setup(u => u.IsLoggedIn).Returns(true);

        var mockAlertService = new Mock<IAlertService>();
        var mockSettingsService = new Mock<ISystemSettingsService>();

        // Act
        var mainVm = new MainViewModel(mockUserService.Object, mockAlertService.Object, mockSettingsService.Object);

        // Assert - يرى إدارة المستخدمين ولكن لا يرى المحذوفات إطلاقاً
        Assert.True(mainVm.CanSeeUsers);
        Assert.False(mainVm.CanSeeDeletions);
    }

    [Fact]
    public void SidebarPermissions_AdminUser_AlwaysCanSeeDeletions_RegardlessOfExplicitPermissions()
    {
        // Arrange
        var mockUserService = new Mock<IUserService>();
        var adminUser = new User
        {
            FullName = "مدير النظام الشامل",
            Username = "sys_admin",
            Role = new Role { RoleName = "مدير النظام" },
            Permissions = null // لا توجد صلاحيات نصية صريحة، دوره كمدير نظام يمنح كل شيء
        };
        mockUserService.Setup(u => u.CurrentUser).Returns(adminUser);
        mockUserService.Setup(u => u.IsLoggedIn).Returns(true);

        var mockAlertService = new Mock<IAlertService>();
        var mockSettingsService = new Mock<ISystemSettingsService>();

        // Act
        var mainVm = new MainViewModel(mockUserService.Object, mockAlertService.Object, mockSettingsService.Object);

        // Assert - مدير النظام يرى رابط المحذوفات دائماً
        Assert.True(mainVm.CanSeeDeletions);
        Assert.True(mainVm.CanSeeSettings);
        Assert.True(mainVm.CanSeeUsers);
        Assert.True(mainVm.CanSeeSources);
    }

    [Fact]
    public void UsersViewModel_DiffViewer_TranslatesDeletionsPermissionCorrectly()
    {
        // Arrange
        var vm = CreateViewModel();
        var log = new AuditLog
        {
            Action = "Update",
            TableName = "Users",
            OldValues = JsonSerializer.Serialize(new { Permissions = "Sources,Settings" }),
            NewValues = JsonSerializer.Serialize(new { Permissions = "Sources,Settings,Deletions" })
        };

        // Act
        vm.OpenDiffViewerCommand.Execute(log);

        // Assert
        Assert.True(vm.IsDiffViewerOpen);
        var permDiff = vm.AuditDiffItems.FirstOrDefault(d => d.FieldName == "صلاحيات الوصول للأقسام");
        Assert.NotNull(permDiff);
        Assert.True(permDiff!.HasChanged);
        Assert.Contains("+ المحذوفات", permDiff.NewValue);
    }

    #endregion
}
