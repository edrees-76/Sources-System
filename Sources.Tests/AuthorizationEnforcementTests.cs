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
    private readonly FakeLicenseService _fakeLicenseService = new();
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
        Assert.Contains("مخصصة لمدير النظام فقط", result.Message);
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
        var userService = new UserService(_fixture.ContextFactory, _auditService, _fakeLicenseService);
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
        Assert.Contains("مخصصة لمدير النظام فقط", m2);

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
        var userService = new UserService(_fixture.ContextFactory, _auditService, _fakeLicenseService);
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
        Assert.Contains("مخصصة لمدير النظام فقط", m2);

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
        var userService = new UserService(_fixture.ContextFactory, _auditService, _fakeLicenseService);
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
        Assert.Contains("مخصصة لمدير النظام فقط", m2);

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
        var userService = new UserService(_fixture.ContextFactory, _auditService, _fakeLicenseService);
        var adminUser = CreateAdminUser("admin_target");
        string originalHash = adminUser.PasswordHash;

        // Caller is normal user
        var attacker = CreateNormalUser("attacker_user");
        userService.Login("attacker_user", "UserPass123!");

        // Attempt privilege escalation
        var (success, message) = userService.ResetPassword(adminUser.Id, "HackedPassword123!");

        Assert.False(success);
        Assert.Contains("مخصصة لمدير النظام فقط", message);

        using var db = _fixture.CreateContext();
        var refreshedAdmin = db.Users.Find(adminUser.Id)!;
        Assert.Equal(originalHash, refreshedAdmin.PasswordHash);
    }

    [Fact]
    public void UserService_UnlockAccount_EnforcesAdminGuard()
    {
        var userService = new UserService(_fixture.ContextFactory, _auditService, _fakeLicenseService);
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
        Assert.Contains("مخصصة لمدير النظام فقط", m2);

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
        var userService = new UserService(_fixture.ContextFactory, _auditService, _fakeLicenseService);
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
        Assert.Contains("مخصصة لمدير النظام فقط", m2);

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
        var userService = new UserService(_fixture.ContextFactory, _auditService, _fakeLicenseService);
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
        Assert.Contains("مخصصة لمدير النظام فقط", m2);

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
        var userService = new UserService(_fixture.ContextFactory, _auditService, _fakeLicenseService);
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
        Assert.Contains("مخصصة لمدير النظام فقط", m2);

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
        var sourceService = new SourceService(_fixture.ContextFactory, new DecayCalculationService(), _auditService, fakeUser, _fakeLicenseService);

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
        var neutronService = new NeutronSourceService(_fixture.ContextFactory, _auditService, fakeUser, _fakeLicenseService);

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
        var isoService = new RadioisotopeService(_fixture.ContextFactory, _auditService, fakeUser, _fakeLicenseService);

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
        var locService = new LocationService(_fixture.ContextFactory, _auditService, fakeUser, _fakeLicenseService);

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
        var typeService = new NeutronSourceTypeService(_fixture.ContextFactory, _auditService, fakeUser, _fakeLicenseService);

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
        var userService = new UserService(_fixture.ContextFactory, _auditService, _fakeLicenseService);

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

    #region 9. حراسة إنشاء وتعديل السجلات (Round 161 — Create/Update Authorization Gap)

    [Fact]
    public void SourceService_CreateSource_And_UpdateSource_EnforcesEditorGuard()
    {
        var fakeUser = new FakeUserService();
        fakeUser.CurrentUser = null;
        var sourceService = new SourceService(_fixture.ContextFactory, new DecayCalculationService(), _auditService, fakeUser, _fakeLicenseService);

        var loc = new Location { Id = Guid.NewGuid(), LocationName = "موقع اختبار 161" };
        var iso = new Radioisotope { Id = Guid.NewGuid(), Symbol = "Co-60", Name = "Cobalt-60", HalfLife = 5.27, RadiationType = "Gamma" };
        var unit = new ActivityUnit { Id = Guid.NewGuid(), UnitName = "MBq", UnitSymbol = "MBq", ConversionToBq = 1e6 };

        using (var db = _fixture.CreateContext())
        {
            db.Locations.Add(loc);
            db.Radioisotopes.Add(iso);
            db.ActivityUnits.Add(unit);
            db.SaveChanges();
        }

        var newSource = new Source
        {
            Id = Guid.NewGuid(),
            SourceCode = "SRC-161-01",
            LocationId = loc.Id,
            RadioisotopeId = iso.Id,
            InitialActivityValue = 100,
            InitialActivityUnitId = unit.Id,
            CurrentActivityValue = 100,
            CurrentActivityUnitId = unit.Id,
            CalibrationDate = DateTime.Now
        };

        // 1. Null user
        var (c1, mC1) = sourceService.CreateSource(newSource);
        Assert.False(c1);
        Assert.Contains("لا يوجد مستخدم مسجَّل الدخول", mC1);

        // 2. Editor without Sources permission
        fakeUser.CurrentUser = new User { Id = Guid.NewGuid(), Username = "no_src_create", IsEditor = true, Permissions = "Locations" };
        var (c2, mC2) = sourceService.CreateSource(newSource);
        Assert.False(c2);
        Assert.Contains("لا تملك صلاحية الوصول", mC2);

        // 3. Authorized editor -> actually creates
        fakeUser.CurrentUser = new User { Id = Guid.NewGuid(), Username = "editor_src_create", IsEditor = true, Permissions = "Sources" };
        var (c3, mC3) = sourceService.CreateSource(newSource);
        Assert.True(c3);

        // Now test UpdateSource with the same guard cases
        newSource.Notes = "ملاحظة معدَّلة";

        fakeUser.CurrentUser = null;
        var (u1, mU1) = sourceService.UpdateSource(newSource);
        Assert.False(u1);
        Assert.Contains("لا يوجد مستخدم مسجَّل الدخول", mU1);

        fakeUser.CurrentUser = new User { Id = Guid.NewGuid(), Username = "no_src_update", IsEditor = true, Permissions = "Locations" };
        var (u2, mU2) = sourceService.UpdateSource(newSource);
        Assert.False(u2);
        Assert.Contains("لا تملك صلاحية الوصول", mU2);

        fakeUser.CurrentUser = new User { Id = Guid.NewGuid(), Username = "editor_src_update", IsEditor = true, Permissions = "Sources" };
        var (u3, mU3) = sourceService.UpdateSource(newSource);
        Assert.True(u3);
    }

    [Fact]
    public void NeutronSourceService_Create_And_Update_EnforcesEditorGuard()
    {
        var fakeUser = new FakeUserService();
        fakeUser.CurrentUser = null;
        var neutronService = new NeutronSourceService(_fixture.ContextFactory, _auditService, fakeUser, _fakeLicenseService);

        var loc = new Location { Id = Guid.NewGuid(), LocationName = "موقع نيوتروني 161" };
        var type = new NeutronSourceType { Id = Guid.NewGuid(), Code = "Pu-Be-161", NameEn = "Plutonium Beryllium" };
        using (var db = _fixture.CreateContext())
        {
            db.Locations.Add(loc);
            db.NeutronSourceTypes.Add(type);
            db.SaveChanges();
        }

        var newItem = new NeutronSource
        {
            Id = Guid.NewGuid(),
            SourceCode = "NS-161-01",
            NeutronSourceTypeId = type.Id,
            LocationId = loc.Id,
            CalibratedEmissionRate = 1000,
            CalibrationDate = DateTime.Now
        };

        // 1. Null user
        var (c1, mC1) = neutronService.Create(newItem);
        Assert.False(c1);
        Assert.Contains("لا يوجد مستخدم مسجَّل الدخول", mC1);

        // 2. Editor without permission for this section
        fakeUser.CurrentUser = new User { Id = Guid.NewGuid(), Username = "no_ns_create", IsEditor = true, Permissions = "Locations" };
        var (c2, mC2) = neutronService.Create(newItem);
        Assert.False(c2);
        Assert.Contains("لا تملك صلاحية الوصول", mC2);

        // 3. Authorized editor -> actually creates
        fakeUser.CurrentUser = new User { Id = Guid.NewGuid(), Username = "editor_ns_create", IsEditor = true, Permissions = "Sources" };
        var (c3, mC3) = neutronService.Create(newItem);
        Assert.True(c3);

        newItem.Notes = "ملاحظة معدَّلة";

        fakeUser.CurrentUser = null;
        var (u1, mU1) = neutronService.Update(newItem);
        Assert.False(u1);
        Assert.Contains("لا يوجد مستخدم مسجَّل الدخول", mU1);

        fakeUser.CurrentUser = new User { Id = Guid.NewGuid(), Username = "no_ns_update", IsEditor = true, Permissions = "Locations" };
        var (u2, mU2) = neutronService.Update(newItem);
        Assert.False(u2);
        Assert.Contains("لا تملك صلاحية الوصول", mU2);

        fakeUser.CurrentUser = new User { Id = Guid.NewGuid(), Username = "editor_ns_update", IsEditor = true, Permissions = "Sources" };
        var (u3, mU3) = neutronService.Update(newItem);
        Assert.True(u3);
    }

    [Fact]
    public void RadioisotopeService_Create_And_Update_EnforcesEditorGuard()
    {
        var fakeUser = new FakeUserService();
        fakeUser.CurrentUser = null;
        var isoService = new RadioisotopeService(_fixture.ContextFactory, _auditService, fakeUser, _fakeLicenseService);

        var newItem = new Radioisotope { Id = Guid.NewGuid(), Symbol = "Ir-192-161", Name = "Iridium-192", HalfLife = 73.8, RadiationType = "Gamma" };

        // 1. Null user
        var (c1, mC1) = isoService.Create(newItem);
        Assert.False(c1);
        Assert.Contains("لا يوجد مستخدم مسجَّل الدخول", mC1);

        // 2. Editor without Radioisotopes permission
        fakeUser.CurrentUser = new User { Id = Guid.NewGuid(), Username = "no_iso_create", IsEditor = true, Permissions = "Sources" };
        var (c2, mC2) = isoService.Create(newItem);
        Assert.False(c2);
        Assert.Contains("لا تملك صلاحية الوصول", mC2);

        // 3. Authorized editor -> actually creates
        fakeUser.CurrentUser = new User { Id = Guid.NewGuid(), Username = "editor_iso_create", IsEditor = true, Permissions = "Radioisotopes" };
        var (c3, mC3) = isoService.Create(newItem);
        Assert.True(c3);

        newItem.Notes = "ملاحظة معدَّلة";

        fakeUser.CurrentUser = null;
        var (u1, mU1) = isoService.Update(newItem);
        Assert.False(u1);
        Assert.Contains("لا يوجد مستخدم مسجَّل الدخول", mU1);

        fakeUser.CurrentUser = new User { Id = Guid.NewGuid(), Username = "no_iso_update", IsEditor = true, Permissions = "Sources" };
        var (u2, mU2) = isoService.Update(newItem);
        Assert.False(u2);
        Assert.Contains("لا تملك صلاحية الوصول", mU2);

        fakeUser.CurrentUser = new User { Id = Guid.NewGuid(), Username = "editor_iso_update", IsEditor = true, Permissions = "Radioisotopes" };
        var (u3, mU3) = isoService.Update(newItem);
        Assert.True(u3);
    }

    [Fact]
    public void LocationService_Create_And_Update_EnforcesEditorGuard()
    {
        var fakeUser = new FakeUserService();
        fakeUser.CurrentUser = null;
        var locService = new LocationService(_fixture.ContextFactory, _auditService, fakeUser, _fakeLicenseService);

        var newItem = new Location { Id = Guid.NewGuid(), LocationName = "غرفة اختبار 161" };

        // 1. Null user
        var (c1, mC1) = locService.Create(newItem);
        Assert.False(c1);
        Assert.Contains("لا يوجد مستخدم مسجَّل الدخول", mC1);

        // 2. Editor without Locations permission
        fakeUser.CurrentUser = new User { Id = Guid.NewGuid(), Username = "no_loc_create", IsEditor = true, Permissions = "Sources" };
        var (c2, mC2) = locService.Create(newItem);
        Assert.False(c2);
        Assert.Contains("لا تملك صلاحية الوصول", mC2);

        // 3. Authorized editor -> actually creates
        fakeUser.CurrentUser = new User { Id = Guid.NewGuid(), Username = "editor_loc_create", IsEditor = true, Permissions = "Locations" };
        var (c3, mC3) = locService.Create(newItem);
        Assert.True(c3);

        newItem.Room = "101-معدَّل";

        fakeUser.CurrentUser = null;
        var (u1, mU1) = locService.Update(newItem);
        Assert.False(u1);
        Assert.Contains("لا يوجد مستخدم مسجَّل الدخول", mU1);

        fakeUser.CurrentUser = new User { Id = Guid.NewGuid(), Username = "no_loc_update", IsEditor = true, Permissions = "Sources" };
        var (u2, mU2) = locService.Update(newItem);
        Assert.False(u2);
        Assert.Contains("لا تملك صلاحية الوصول", mU2);

        fakeUser.CurrentUser = new User { Id = Guid.NewGuid(), Username = "editor_loc_update", IsEditor = true, Permissions = "Locations" };
        var (u3, mU3) = locService.Update(newItem);
        Assert.True(u3);
    }

    [Fact]
    public void BorrowService_CreateRequest_And_MarkReturned_EnforcesEditorGuard()
    {
        var fakeUser = new FakeUserService();
        fakeUser.CurrentUser = null;
        var borrowService = new BorrowService(_fixture.ContextFactory, _auditService, fakeUser, _fakeLicenseService);

        var loc = new Location { Id = Guid.NewGuid(), LocationName = "موقع استعارة 161" };
        var iso = new Radioisotope { Id = Guid.NewGuid(), Symbol = "Cs-137-161", Name = "Cesium-137", HalfLife = 30.17, RadiationType = "Gamma" };
        var unit = new ActivityUnit { Id = Guid.NewGuid(), UnitName = "MBq", UnitSymbol = "MBq", ConversionToBq = 1e6 };
        var src = new Source
        {
            Id = Guid.NewGuid(),
            SourceCode = "SRC-BORROW-161",
            LocationId = loc.Id,
            RadioisotopeId = iso.Id,
            InitialActivityValue = 100,
            InitialActivityUnitId = unit.Id,
            CurrentActivityValue = 100,
            CurrentActivityUnitId = unit.Id,
            CalibrationDate = DateTime.Now,
            Status = "Storage"
        };

        using (var db = _fixture.CreateContext())
        {
            db.Locations.Add(loc);
            db.Radioisotopes.Add(iso);
            db.ActivityUnits.Add(unit);
            db.Sources.Add(src);
            db.SaveChanges();
        }

        var request = new BorrowRequest { Id = Guid.NewGuid(), SourceId = src.Id, BorrowerName = "مستعير اختبار" };

        // 1. Null user
        var (c1, mC1) = borrowService.CreateRequest(request);
        Assert.False(c1);
        Assert.Contains("لا يوجد مستخدم مسجَّل الدخول", mC1);

        // 2. Editor without Borrowing permission
        fakeUser.CurrentUser = new User { Id = Guid.NewGuid(), Username = "no_borrow_create", IsEditor = true, Permissions = "Sources" };
        var (c2, mC2) = borrowService.CreateRequest(request);
        Assert.False(c2);
        Assert.Contains("لا تملك صلاحية الوصول", mC2);

        // 3. Authorized editor -> actually creates
        fakeUser.CurrentUser = new User { Id = Guid.NewGuid(), Username = "editor_borrow_create", IsEditor = true, Permissions = "Borrowing" };
        var (c3, mC3) = borrowService.CreateRequest(request);
        Assert.True(c3);

        // MarkReturned guard cases
        fakeUser.CurrentUser = null;
        var (r1, mR1) = borrowService.MarkReturned(request.Id, Guid.NewGuid(), DateTime.Now);
        Assert.False(r1);
        Assert.Contains("لا يوجد مستخدم مسجَّل الدخول", mR1);

        fakeUser.CurrentUser = new User { Id = Guid.NewGuid(), Username = "no_borrow_return", IsEditor = true, Permissions = "Sources" };
        var (r2, mR2) = borrowService.MarkReturned(request.Id, Guid.NewGuid(), DateTime.Now);
        Assert.False(r2);
        Assert.Contains("لا تملك صلاحية الوصول", mR2);

        var returnedByUser = CreateNormalUser("returned_by_161", permissions: "Borrowing", isEditor: true);
        fakeUser.CurrentUser = new User { Id = Guid.NewGuid(), Username = "editor_borrow_return", IsEditor = true, Permissions = "Borrowing" };
        var (r3, mR3) = borrowService.MarkReturned(request.Id, returnedByUser.Id, DateTime.Now);
        Assert.True(r3);
    }

    [Fact]
    public void LeakTestService_AddRecord_UpdateRecord_DeleteRecord_EnforcesEditorGuard()
    {
        var fakeUser = new FakeUserService();
        fakeUser.CurrentUser = null;
        var settingsService = new SystemSettingsService(_fixture.ContextFactory, _fakeLicenseService);
        var leakTestService = new LeakTestService(_fixture.ContextFactory, _auditService, fakeUser, settingsService, _fakeLicenseService);

        var loc = new Location { Id = Guid.NewGuid(), LocationName = "موقع فحص تسرب 161" };
        var iso = new Radioisotope { Id = Guid.NewGuid(), Symbol = "Am-241-161", Name = "Americium-241", HalfLife = 432.6, RadiationType = "Alpha" };
        var unit = new ActivityUnit { Id = Guid.NewGuid(), UnitName = "MBq", UnitSymbol = "MBq", ConversionToBq = 1e6 };
        var src = new Source
        {
            Id = Guid.NewGuid(),
            SourceCode = "SRC-LEAK-161",
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

        var record = new LeakTestRecord { Id = Guid.NewGuid(), SourceId = src.Id, TestDate = DateTime.Now, Result = "Pass" };

        // 1. Null user - AddRecord
        var (a1, mA1, recA1) = leakTestService.AddRecord(record);
        Assert.False(a1);
        Assert.Contains("لا يوجد مستخدم مسجَّل الدخول", mA1);
        Assert.Null(recA1);

        // 2. Editor without LeakTests permission - AddRecord
        fakeUser.CurrentUser = new User { Id = Guid.NewGuid(), Username = "no_leak_add", IsEditor = true, Permissions = "Sources" };
        var (a2, mA2, recA2) = leakTestService.AddRecord(record);
        Assert.False(a2);
        Assert.Contains("لا تملك صلاحية الوصول", mA2);
        Assert.Null(recA2);

        // 3. Authorized editor -> actually creates
        fakeUser.CurrentUser = CreateNormalUser("editor_leak_add_161", permissions: "LeakTests", isEditor: true);
        var (a3, mA3, recA3) = leakTestService.AddRecord(record);
        Assert.True(a3);
        Assert.NotNull(recA3);

        // UpdateRecord guard cases
        record.Result = "Fail";

        fakeUser.CurrentUser = null;
        var (u1, mU1) = leakTestService.UpdateRecord(record);
        Assert.False(u1);
        Assert.Contains("لا يوجد مستخدم مسجَّل الدخول", mU1);

        fakeUser.CurrentUser = new User { Id = Guid.NewGuid(), Username = "no_leak_update", IsEditor = true, Permissions = "Sources" };
        var (u2, mU2) = leakTestService.UpdateRecord(record);
        Assert.False(u2);
        Assert.Contains("لا تملك صلاحية الوصول", mU2);

        fakeUser.CurrentUser = new User { Id = Guid.NewGuid(), Username = "editor_leak_update", IsEditor = true, Permissions = "LeakTests" };
        var (u3, mU3) = leakTestService.UpdateRecord(record);
        Assert.True(u3);

        // DeleteRecord guard cases
        fakeUser.CurrentUser = null;
        var (d1, mD1) = leakTestService.DeleteRecord(record.Id);
        Assert.False(d1);
        Assert.Contains("لا يوجد مستخدم مسجَّل الدخول", mD1);

        fakeUser.CurrentUser = new User { Id = Guid.NewGuid(), Username = "no_leak_delete", IsEditor = true, Permissions = "Sources" };
        var (d2, mD2) = leakTestService.DeleteRecord(record.Id);
        Assert.False(d2);
        Assert.Contains("لا تملك صلاحية الوصول", mD2);

        fakeUser.CurrentUser = new User { Id = Guid.NewGuid(), Username = "editor_leak_delete", IsEditor = true, Permissions = "LeakTests" };
        var (d3, mD3) = leakTestService.DeleteRecord(record.Id);
        Assert.True(d3);
    }

    #endregion
}
