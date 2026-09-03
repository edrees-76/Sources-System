using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Moq;
using Sources.Data;
using Sources.Helpers;
using Sources.Models;
using Sources.Services;
using Sources.Tests.Fakes;
using Sources.Tests.Fixtures;
using Xunit;

namespace Sources.Tests;

public class AuthorizationEnforcementTests : IClassFixture<SqliteInMemoryFixture>, IDisposable
{
    private readonly SqliteInMemoryFixture _fixture;
    private readonly FakeAuditService _auditService;
    private readonly Role _adminRole;
    private readonly Role _userRole;

    public AuthorizationEnforcementTests(SqliteInMemoryFixture fixture)
    {
        _fixture = fixture;
        _fixture.ResetDatabase();
        _auditService = new FakeAuditService();

        _adminRole = new Role { Id = Guid.NewGuid(), RoleName = "مدير النظام", Permissions = "All" };
        _userRole = new Role { Id = Guid.NewGuid(), RoleName = "مستخدم", Permissions = "Sources" };

        using var db = _fixture.CreateContext();
        db.Roles.AddRange(_adminRole, _userRole);
        db.SaveChanges();
    }

    public void Dispose()
    {
        _fixture.ResetDatabase();
    }

    private User CreateAdminUser(string username = "admin_auth")
    {
        using var db = _fixture.CreateContext();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = username,
            FullName = "مدير الاختبار",
            PasswordHash = PasswordHelper.HashPassword("AdminPass123!"),
            RoleId = _adminRole.Id,
            Permissions = "All",
            IsActive = true,
            IsEditor = true,
            CreatedAt = DateTime.Now
        };
        db.Users.Add(user);
        db.SaveChanges();
        return user;
    }

    private User CreateNormalUser(string username = "user_auth", string? permissions = null, bool isEditor = false)
    {
        using var db = _fixture.CreateContext();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = username,
            FullName = "مستخدم عادي",
            PasswordHash = PasswordHelper.HashPassword("UserPass123!"),
            RoleId = _userRole.Id,
            Permissions = permissions,
            IsActive = true,
            IsEditor = isEditor,
            CreatedAt = DateTime.Now
        };
        db.Users.Add(user);
        db.SaveChanges();
        return user;
    }

    #region 1. فحص صنف AuthorizationGuard المستقل

    [Fact]
    public void RequireEditor_WhenUserIsNull_ReturnsNotLoggedIn()
    {
        var result = AuthorizationGuard.RequireEditor(null, "Sources");
        Assert.False(result.Allowed);
        Assert.Contains("لا يوجد مستخدم مسجَّل الدخول", result.Message);
    }

    [Fact]
    public void RequireEditor_WhenUserIsNotEditor_ReturnsReadOnlyMessage()
    {
        var user = new User { Username = "reader", IsEditor = false, Permissions = null };
        var result = AuthorizationGuard.RequireEditor(user, "Sources");
        Assert.False(result.Allowed);
        Assert.Contains("للاطّلاع فقط", result.Message);
    }

    [Fact]
    public void RequireEditor_WhenEditorLacksSectionPermission_ReturnsNoPermissionMessage()
    {
        var user = new User { Username = "editor_reports", IsEditor = true, Permissions = "Reports,Locations" };
        var result = AuthorizationGuard.RequireEditor(user, "Sources");
        Assert.False(result.Allowed);
        Assert.Contains("لا تملك صلاحية الوصول", result.Message);
    }

    [Fact]
    public void RequireEditor_WhenEditorHasSectionPermission_ReturnsAllowed()
    {
        var user = new User { Username = "editor_sources", IsEditor = true, Permissions = "Sources,Reports" };
        var result = AuthorizationGuard.RequireEditor(user, "Sources");
        Assert.True(result.Allowed);
        Assert.Empty(result.Message);
    }

    [Fact]
    public void RequireEditor_WhenAdminHasAllPermission_ReturnsAllowed()
    {
        var user = new User { Username = "admin_user", Role = new Role { RoleName = "مدير النظام" }, Permissions = "All" };
        var result = AuthorizationGuard.RequireEditor(user, "Sources");
        Assert.True(result.Allowed);
        Assert.Empty(result.Message);
    }

    [Fact]
    public void RequireAdmin_WhenUserIsNull_ReturnsNotLoggedIn()
    {
        var result = AuthorizationGuard.RequireAdmin(null);
        Assert.False(result.Allowed);
        Assert.Contains("لا يوجد مستخدم مسجَّل الدخول", result.Message);
    }

    [Fact]
    public void RequireAdmin_WhenUserIsNotAdmin_ReturnsOperationAdminOnly()
    {
        var user = new User { Username = "normal", Role = new Role { RoleName = "مشغل" } };
        var result = AuthorizationGuard.RequireAdmin(user);
        Assert.False(result.Allowed);
        Assert.Contains("مقصورة على مدير النظام", result.Message);
    }

    [Fact]
    public void RequireAdmin_WhenUserIsAdmin_ReturnsAllowed()
    {
        var user = new User { Username = "super_admin", Role = new Role { RoleName = "مدير النظام" } };
        var result = AuthorizationGuard.RequireAdmin(user);
        Assert.True(result.Allowed);
        Assert.Empty(result.Message);
    }

    #endregion

    #region 2. حراسة عمليات إدارة المستخدمين UserService (7 دوال)

    [Fact]
    public void UserService_CreateUser_EnforcesAdminGuard()
    {
        var userService = new UserService(_fixture.ContextFactory, _auditService);
        var newUser = new User { Username = "u_new", FullName = "New", RoleId = _userRole.Id };

        // 1. Null user
        var (s1, m1) = userService.CreateUser(newUser, "Pass123!");
        Assert.False(s1);
        Assert.Contains("لا يوجد مستخدم مسجَّل الدخول", m1);

        // 2. Non-admin user
        var normal = CreateNormalUser("normal_c");
        userService.Login("normal_c", "UserPass123!");
        var (s2, m2) = userService.CreateUser(newUser, "Pass123!");
        Assert.False(s2);
        Assert.Contains("مقصورة على مدير النظام", m2);

        // 3. Admin user
        var admin = CreateAdminUser("admin_c");
        userService.Login("admin_c", "AdminPass123!");
        var (s3, m3) = userService.CreateUser(newUser, "Pass123!");
        Assert.True(s3);
        Assert.Equal("تم إنشاء المستخدم بنجاح", m3);
    }

    [Fact]
    public void UserService_UpdateUser_EnforcesAdminGuard()
    {
        var userService = new UserService(_fixture.ContextFactory, _auditService);
        var target = CreateNormalUser("target_up");

        // 1. Null user
        var (s1, m1) = userService.UpdateUser(target);
        Assert.False(s1);
        Assert.Contains("لا يوجد مستخدم مسجَّل الدخول", m1);

        // 2. Non-admin user
        var normal = CreateNormalUser("normal_up");
        userService.Login("normal_up", "UserPass123!");
        var (s2, m2) = userService.UpdateUser(target);
        Assert.False(s2);
        Assert.Contains("مقصورة على مدير النظام", m2);

        // 3. Admin user
        var admin = CreateAdminUser("admin_up");
        userService.Login("admin_up", "AdminPass123!");
        target.FullName = "Updated Full Name";
        var (s3, m3) = userService.UpdateUser(target);
        Assert.True(s3);
        Assert.Equal("تم تحديث بيانات المستخدم", m3);
    }

    [Fact]
    public void UserService_ResetPassword_EnforcesAdminGuard_AndPreventsPrivilegeEscalation()
    {
        var userService = new UserService(_fixture.ContextFactory, _auditService);
        var target = CreateNormalUser("target_reset");

        // 1. Null user
        var (s1, m1) = userService.ResetPassword(target.Id, "NewPass123!");
        Assert.False(s1);
        Assert.Contains("لا يوجد مستخدم مسجَّل الدخول", m1);

        // 2. Non-admin user
        var normal = CreateNormalUser("normal_reset");
        userService.Login("normal_reset", "UserPass123!");
        var (s2, m2) = userService.ResetPassword(target.Id, "NewPass123!");
        Assert.False(s2);
        Assert.Contains("مقصورة على مدير النظام", m2);

        // 3. Admin user
        var admin = CreateAdminUser("admin_reset_actor");
        userService.Login("admin_reset_actor", "AdminPass123!");
        var (s3, m3) = userService.ResetPassword(target.Id, "NewPass123!");
        Assert.True(s3);
        Assert.Equal("تم إعادة تعيين كلمة المرور", m3);
    }

    [Fact]
    public void ResetPassword_WhenCallerIsNotAdmin_DoesNotChangeAdminPassword()
    {
        var userService = new UserService(_fixture.ContextFactory, _auditService);
        var adminUser = CreateAdminUser("admin_target");
        string originalHash = adminUser.PasswordHash;

        // Caller is normal user
        var attacker = CreateNormalUser("attacker_user");
        userService.Login("attacker_user", "UserPass123!");

        // Attempt privilege escalation
        var (success, message) = userService.ResetPassword(adminUser.Id, "HackedPassword123!");

        Assert.False(success);
        Assert.Contains("مقصورة على مدير النظام", message);

        using var db = _fixture.CreateContext();
        var refreshedAdmin = db.Users.Find(adminUser.Id)!;
        Assert.Equal(originalHash, refreshedAdmin.PasswordHash);
    }

    [Fact]
    public void UserService_UnlockAccount_EnforcesAdminGuard()
    {
        var userService = new UserService(_fixture.ContextFactory, _auditService);
        var locked = CreateNormalUser("locked_user");
        using (var db = _fixture.CreateContext())
        {
            var u = db.Users.Find(locked.Id)!;
            u.LockoutEnd = DateTime.Now.AddHours(1);
            u.FailedLoginAttempts = 5;
            db.SaveChanges();
        }

        // 1. Null user
        var (s1, m1) = userService.UnlockAccount(locked.Id);
        Assert.False(s1);
        Assert.Contains("لا يوجد مستخدم مسجَّل الدخول", m1);

        // 2. Non-admin user
        var normal = CreateNormalUser("normal_unlock");
        userService.Login("normal_unlock", "UserPass123!");
        var (s2, m2) = userService.UnlockAccount(locked.Id);
        Assert.False(s2);
        Assert.Contains("مقصورة على مدير النظام", m2);

        // 3. Admin user
        var admin = CreateAdminUser("admin_unlock_actor");
        userService.Login("admin_unlock_actor", "AdminPass123!");
        var (s3, m3) = userService.UnlockAccount(locked.Id);
        Assert.True(s3);
        Assert.Equal("تم فك قفل الحساب", m3);
    }

    [Fact]
    public void UserService_DeleteUser_EnforcesAdminGuard()
    {
        var userService = new UserService(_fixture.ContextFactory, _auditService);
        var toDelete = CreateNormalUser("to_delete_user");

        // 1. Null user
        var (s1, m1) = userService.DeleteUser(toDelete.Id);
        Assert.False(s1);
        Assert.Contains("لا يوجد مستخدم مسجَّل الدخول", m1);

        // 2. Non-admin user
        var normal = CreateNormalUser("normal_del");
        userService.Login("normal_del", "UserPass123!");
        var (s2, m2) = userService.DeleteUser(toDelete.Id);
        Assert.False(s2);
        Assert.Contains("مقصورة على مدير النظام", m2);

        // 3. Admin user
        var admin = CreateAdminUser("admin_del_actor");
        userService.Login("admin_del_actor", "AdminPass123!");
        var (s3, m3) = userService.DeleteUser(toDelete.Id);
        Assert.True(s3);
        Assert.Equal("تم حذف المستخدم بنجاح", m3);
    }

    [Fact]
    public void UserService_RestoreUser_EnforcesAdminGuard()
    {
        var userService = new UserService(_fixture.ContextFactory, _auditService);
        var toRestore = CreateNormalUser("to_restore_user");
        using (var db = _fixture.CreateContext())
        {
            var u = db.Users.Find(toRestore.Id)!;
            u.IsDeleted = true;
            u.DeletedAt = DateTime.Now.AddDays(-1);
            db.SaveChanges();
        }

        // 1. Null user
        var (s1, m1) = userService.RestoreUser(toRestore.Id);
        Assert.False(s1);
        Assert.Contains("لا يوجد مستخدم مسجَّل الدخول", m1);

        // 2. Non-admin user
        var normal = CreateNormalUser("normal_res");
        userService.Login("normal_res", "UserPass123!");
        var (s2, m2) = userService.RestoreUser(toRestore.Id);
        Assert.False(s2);
        Assert.Contains("مقصورة على مدير النظام", m2);

        // 3. Admin user
        var admin = CreateAdminUser("admin_res_actor");
        userService.Login("admin_res_actor", "AdminPass123!");
        var (s3, m3) = userService.RestoreUser(toRestore.Id);
        Assert.True(s3);
        Assert.Contains("تم استرجاع", m3);
    }

    [Fact]
    public void UserService_ToggleUserFreeze_EnforcesAdminGuard()
    {
        var userService = new UserService(_fixture.ContextFactory, _auditService);
        var toFreeze = CreateNormalUser("to_freeze_user");

        // 1. Null user
        var (s1, m1) = userService.ToggleUserFreeze(toFreeze.Id);
        Assert.False(s1);
        Assert.Contains("لا يوجد مستخدم مسجَّل الدخول", m1);

        // 2. Non-admin user
        var normal = CreateNormalUser("normal_frz");
        userService.Login("normal_frz", "UserPass123!");
        var (s2, m2) = userService.ToggleUserFreeze(toFreeze.Id);
        Assert.False(s2);
        Assert.Contains("مقصورة على مدير النظام", m2);

        // 3. Admin user
        var admin = CreateAdminUser("admin_frz_actor");
        userService.Login("admin_frz_actor", "AdminPass123!");
        var (s3, m3) = userService.ToggleUserFreeze(toFreeze.Id);
        Assert.True(s3);
        Assert.Contains("تجميد", m3);
    }

    #endregion

    #region 3. حراسة SourceService (DeleteSource, RestoreSource)

    [Fact]
    public void SourceService_DeleteSource_And_RestoreSource_EnforcesEditorGuard()
    {
        var fakeUser = new FakeUserService();
        fakeUser.CurrentUser = null;
        var sourceService = new SourceService(_fixture.ContextFactory, new DecayCalculationService(), _auditService, fakeUser);

        var loc = new Location { Id = Guid.NewGuid(), LocationName = "موقع 1" };
        var iso = new Radioisotope { Id = Guid.NewGuid(), Symbol = "Cs-137", Name = "Cesium-137", HalfLife = 30.17, RadiationType = "Gamma" };
        var unit = new ActivityUnit { Id = Guid.NewGuid(), UnitName = "MBq", UnitSymbol = "MBq", ConversionToBq = 1e6 };
        var src = new Source
        {
            Id = Guid.NewGuid(),
            SourceCode = "SRC-TEST-01",
            LocationId = loc.Id,
            RadioisotopeId = iso.Id,
            InitialActivityValue = 100,
            InitialActivityUnitId = unit.Id,
            CurrentActivityValue = 100,
            CurrentActivityUnitId = unit.Id,
            CalibrationDate = DateTime.Now
        };

        using (var db = _fixture.CreateContext())
        {
            db.Locations.Add(loc);
            db.Radioisotopes.Add(iso);
            db.ActivityUnits.Add(unit);
            db.Sources.Add(src);
            db.SaveChanges();
        }

        // 1. Null user
        var (d1, mD1) = sourceService.DeleteSource(src.Id);
        Assert.False(d1);
        Assert.Contains("لا يوجد مستخدم مسجَّل الدخول", mD1);

        var (r1, mR1) = sourceService.RestoreSource(src.Id);
        Assert.False(r1);
        Assert.Contains("لا يوجد مستخدم مسجَّل الدخول", mR1);

        // 2. User without Sources permission
        fakeUser.CurrentUser = new User { Id = Guid.NewGuid(), Username = "no_src", IsEditor = true, Permissions = "Reports" };
        var (d2, mD2) = sourceService.DeleteSource(src.Id);
        Assert.False(d2);
        Assert.Contains("لا تملك صلاحية الوصول", mD2);

        var (r2, mR2) = sourceService.RestoreSource(src.Id);
        Assert.False(r2);
        Assert.Contains("لا تملك صلاحية الوصول", mR2);

        // 3. Authorized Editor
        fakeUser.CurrentUser = new User { Id = Guid.NewGuid(), Username = "editor_src", IsEditor = true, Permissions = "Sources" };
        var (d3, mD3) = sourceService.DeleteSource(src.Id);
        Assert.True(d3);
        Assert.Equal("تم حذف المصدر بنجاح", mD3);

        var (r3, mR3) = sourceService.RestoreSource(src.Id);
        Assert.True(r3);
        Assert.Contains("تم استرجاع المصدر", mR3);
    }

    #endregion

    #region 4. حراسة NeutronSourceService (Delete, Restore)

    [Fact]
    public void NeutronSourceService_Delete_And_Restore_EnforcesEditorGuard()
    {
        var fakeUser = new FakeUserService();
        fakeUser.CurrentUser = null;
        var neutronService = new NeutronSourceService(_fixture.ContextFactory, _auditService, fakeUser);

        var loc = new Location { Id = Guid.NewGuid(), LocationName = "موقع نيوتروني" };
        var type = new NeutronSourceType { Id = Guid.NewGuid(), Code = "Am-Be", NameEn = "Americium Beryllium" };
        var nSrc = new NeutronSource { Id = Guid.NewGuid(), SerialNumber = "NS-001", NeutronSourceTypeId = type.Id, LocationId = loc.Id, Status = "Storage", CalibrationDate = DateTime.Now };

        using (var db = _fixture.CreateContext())
        {
            db.Locations.Add(loc);
            db.NeutronSourceTypes.Add(type);
            db.NeutronSources.Add(nSrc);
            db.SaveChanges();
        }

        // 1. Null user
        var (d1, mD1) = neutronService.Delete(nSrc.Id);
        Assert.False(d1);
        Assert.Contains("لا يوجد مستخدم مسجَّل الدخول", mD1);

        var (r1, mR1) = neutronService.Restore(nSrc.Id);
        Assert.False(r1);
        Assert.Contains("لا يوجد مستخدم مسجَّل الدخول", mR1);

        // 2. Unauthorized user
        fakeUser.CurrentUser = new User { Id = Guid.NewGuid(), Username = "no_perm", IsEditor = true, Permissions = "Locations" };
        var (d2, mD2) = neutronService.Delete(nSrc.Id);
        Assert.False(d2);
        Assert.Contains("لا تملك صلاحية الوصول", mD2);

        var (r2, mR2) = neutronService.Restore(nSrc.Id);
        Assert.False(r2);
        Assert.Contains("لا تملك صلاحية الوصول", mR2);

        // 3. Authorized Editor
        fakeUser.CurrentUser = new User { Id = Guid.NewGuid(), Username = "auth_user", IsEditor = true, Permissions = "Sources" };
        var (d3, mD3) = neutronService.Delete(nSrc.Id);
        Assert.True(d3);
        Assert.Equal("تم حذف المصدر النيتروني", mD3);

        var (r3, mR3) = neutronService.Restore(nSrc.Id);
        Assert.True(r3);
        Assert.Contains("تم استرجاع المصدر النيتروني", mR3);
    }

    #endregion

    #region 5. حراسة RadioisotopeService (Delete, Restore)

    [Fact]
    public void RadioisotopeService_Delete_And_Restore_EnforcesEditorGuard()
    {
        var fakeUser = new FakeUserService();
        fakeUser.CurrentUser = null;
        var isoService = new RadioisotopeService(_fixture.ContextFactory, _auditService, fakeUser);

        var iso = new Radioisotope { Id = Guid.NewGuid(), Symbol = "Na-22", Name = "Sodium-22", HalfLife = 2.6, RadiationType = "Beta+" };
        using (var db = _fixture.CreateContext())
        {
            db.Radioisotopes.Add(iso);
            db.SaveChanges();
        }

        // 1. Null user
        var (d1, mD1) = isoService.Delete(iso.Id);
        Assert.False(d1);
        Assert.Contains("لا يوجد مستخدم مسجَّل الدخول", mD1);

        var (r1, mR1) = isoService.Restore(iso.Id);
        Assert.False(r1);
        Assert.Contains("لا يوجد مستخدم مسجَّل الدخول", mR1);

        // 2. Unauthorized user
        fakeUser.CurrentUser = new User { Id = Guid.NewGuid(), Username = "no_iso_perm", IsEditor = true, Permissions = "Sources" };
        var (d2, mD2) = isoService.Delete(iso.Id);
        Assert.False(d2);
        Assert.Contains("لا تملك صلاحية الوصول", mD2);

        var (r2, mR2) = isoService.Restore(iso.Id);
        Assert.False(r2);
        Assert.Contains("لا تملك صلاحية الوصول", mR2);

        // 3. Authorized Editor
        fakeUser.CurrentUser = new User { Id = Guid.NewGuid(), Username = "auth_iso_editor", IsEditor = true, Permissions = "Radioisotopes" };
        var (d3, mD3) = isoService.Delete(iso.Id);
        Assert.True(d3);
        Assert.Equal("تم حذف النظير", mD3);

        var (r3, mR3) = isoService.Restore(iso.Id);
        Assert.True(r3);
        Assert.Contains("تم استرجاع النظير", mR3);
    }

    #endregion

    #region 6. حراسة LocationService (Delete, Restore)

    [Fact]
    public void LocationService_Delete_And_Restore_EnforcesEditorGuard()
    {
        var fakeUser = new FakeUserService();
        fakeUser.CurrentUser = null;
        var locService = new LocationService(_fixture.ContextFactory, _auditService, fakeUser);

        var loc = new Location { Id = Guid.NewGuid(), LocationName = "غرفة التخزين 102" };
        using (var db = _fixture.CreateContext())
        {
            db.Locations.Add(loc);
            db.SaveChanges();
        }

        // 1. Null user
        var (d1, mD1) = locService.Delete(loc.Id);
        Assert.False(d1);
        Assert.Contains("لا يوجد مستخدم مسجَّل الدخول", mD1);

        var (r1, mR1) = locService.Restore(loc.Id);
        Assert.False(r1);
        Assert.Contains("لا يوجد مستخدم مسجَّل الدخول", mR1);

        // 2. Unauthorized user
        fakeUser.CurrentUser = new User { Id = Guid.NewGuid(), Username = "no_loc_perm", IsEditor = true, Permissions = "Sources" };
        var (d2, mD2) = locService.Delete(loc.Id);
        Assert.False(d2);
        Assert.Contains("لا تملك صلاحية الوصول", mD2);

        var (r2, mR2) = locService.Restore(loc.Id);
        Assert.False(r2);
        Assert.Contains("لا تملك صلاحية الوصول", mR2);

        // 3. Authorized Editor
        fakeUser.CurrentUser = new User { Id = Guid.NewGuid(), Username = "auth_loc_editor", IsEditor = true, Permissions = "Locations" };
        var (d3, mD3) = locService.Delete(loc.Id);
        Assert.True(d3);
        Assert.Equal("تم حذف الموقع", mD3);

        var (r3, mR3) = locService.Restore(loc.Id);
        Assert.True(r3);
        Assert.Contains("تم استرجاع الموقع", mR3);
    }

    #endregion

    #region 7. حراسة NeutronSourceTypeService (Delete, Restore)

    [Fact]
    public void NeutronSourceTypeService_Delete_And_Restore_EnforcesEditorGuard()
    {
        var fakeUser = new FakeUserService();
        fakeUser.CurrentUser = null;
        var typeService = new NeutronSourceTypeService(_fixture.ContextFactory, _auditService, fakeUser);

        var type = new NeutronSourceType { Id = Guid.NewGuid(), Code = "Cf-252", NameEn = "Californium-252" };
        using (var db = _fixture.CreateContext())
        {
            db.NeutronSourceTypes.Add(type);
            db.SaveChanges();
        }

        // 1. Null user
        var (d1, mD1) = typeService.Delete(type.Id);
        Assert.False(d1);
        Assert.Contains("لا يوجد مستخدم مسجَّل الدخول", mD1);

        var (r1, mR1) = typeService.Restore(type.Id);
        Assert.False(r1);
        Assert.Contains("لا يوجد مستخدم مسجَّل الدخول", mR1);

        // 2. Unauthorized user
        fakeUser.CurrentUser = new User { Id = Guid.NewGuid(), Username = "no_src_type_perm", IsEditor = true, Permissions = "Locations" };
        var (d2, mD2) = typeService.Delete(type.Id);
        Assert.False(d2);
        Assert.Contains("لا تملك صلاحية الوصول", mD2);

        var (r2, mR2) = typeService.Restore(type.Id);
        Assert.False(r2);
        Assert.Contains("لا تملك صلاحية الوصول", mR2);

        // 3. Authorized Editor (قسم Sources)
        fakeUser.CurrentUser = new User { Id = Guid.NewGuid(), Username = "auth_type_editor", IsEditor = true, Permissions = "Sources" };
        var (d3, mD3) = typeService.Delete(type.Id);
        Assert.True(d3);
        Assert.Equal("تم حذف نوع المصدر النيتروني", mD3);

        var (r3, mR3) = typeService.Restore(type.Id);
        Assert.True(r3);
        Assert.Contains("تم استرجاع نوع المصدر النيتروني", mR3);
    }

    #endregion

    #region 8. فحص تحويل UserService.HasPermission للاعتماد على User.HasSectionPermission

    [Fact]
    public void UserService_HasPermission_DelegatesToCurrentUser_HasSectionPermission()
    {
        var userService = new UserService(_fixture.ContextFactory, _auditService);

        // 1. No user logged in -> false
        Assert.False(userService.HasPermission("Sources"));

        // 2. Normal user with specific permissions
        var user = CreateNormalUser("has_perm_user", permissions: "Sources,Reports", isEditor: true);
        userService.Login("has_perm_user", "UserPass123!");

        Assert.True(userService.HasPermission("Sources"));
        Assert.True(userService.HasPermission("Reports"));
        Assert.False(userService.HasPermission("Locations"));
        Assert.False(userService.HasPermission("Users"));

        // 3. Admin user with All -> true for any section
        var admin = CreateAdminUser("admin_perm_user");
        userService.Login("admin_perm_user", "AdminPass123!");

        Assert.True(userService.HasPermission("Sources"));
        Assert.True(userService.HasPermission("Locations"));
        Assert.True(userService.HasPermission("Users"));
        Assert.True(userService.HasPermission("All"));
    }

    #endregion
}
