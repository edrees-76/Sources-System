using System;
using System.Collections.Generic;
using System.Linq;
using Moq;
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
}
