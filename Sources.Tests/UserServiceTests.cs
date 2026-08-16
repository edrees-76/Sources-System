using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Sources.Data;
using Sources.Helpers;
using Sources.Models;
using Sources.Services;
using Sources.Tests.Fixtures;
using Xunit;

namespace Sources.Tests;

public class UserServiceTests : IClassFixture<SqliteInMemoryFixture>, IDisposable
{
    private readonly SqliteInMemoryFixture _fixture;
    private readonly UserService _userService;

    private Role _adminRole = null!;
    private Role _userRole = null!;

    public UserServiceTests(SqliteInMemoryFixture fixture)
    {
        _fixture = fixture;
        _fixture.ResetDatabase();

        _userService = new UserService(_fixture.ContextFactory);

        SeedRoles();
    }

    private void SeedRoles()
    {
        using var context = _fixture.CreateContext();

        _adminRole = new Role
        {
            Id = Guid.NewGuid(),
            RoleName = "مدير النظام",
            Description = "صلاحيات كاملة لإدارة النظام",
            Permissions = "All,Sources,Reports,Users,Settings,Locations,Borrowing,ActivityCalculator,Radioisotopes"
        };

        _userRole = new Role
        {
            Id = Guid.NewGuid(),
            RoleName = "مستخدم",
            Description = "مستخدم عادي في المنظومة",
            Permissions = "Sources,Reports"
        };

        context.Roles.AddRange(_adminRole, _userRole);
        context.SaveChanges();
    }

    private User CreateTestUser(
        string username = "testuser",
        string password = "Password123!",
        string fullName = "مستخدم تجريبي",
        Role? role = null,
        bool isActive = true,
        int failedAttempts = 0,
        DateTime? lockoutEnd = null,
        string? permissions = null,
        bool isEditor = true)
    {
        using var context = _fixture.CreateContext();

        var userRole = role ?? _userRole;
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = username,
            FullName = fullName,
            Email = $"{username}@test.local",
            PasswordHash = PasswordHelper.HashPassword(password),
            RoleId = userRole.Id,
            IsActive = isActive,
            IsDeleted = false,
            FailedLoginAttempts = failedAttempts,
            LockoutEnd = lockoutEnd,
            Permissions = permissions,
            IsEditor = isEditor,
            CreatedAt = DateTime.Now
        };

        context.Users.Add(user);
        context.SaveChanges();
        return user;
    }

    public void Dispose()
    {
        _userService.Logout();
    }

    #region أ. تسجيل الدخول الناجح (Successful Login)

    [Fact]
    public void Login_WithValidCredentials_ReturnsSuccessAndUpdatesUserStats()
    {
        // Arrange
        var user = CreateTestUser(username: "ahmed", password: "SecurePassword123");
        var beforeLogin = DateTime.Now.AddSeconds(-2);

        // Act
        var (success, message) = _userService.Login("ahmed", "SecurePassword123");

        // Assert
        Assert.True(success);
        Assert.Equal("تم تسجيل الدخول بنجاح", message);
        Assert.True(_userService.IsLoggedIn);
        Assert.NotNull(_userService.CurrentUser);
        Assert.Equal("ahmed", _userService.CurrentUser!.Username);

        // التحقق من حالة قاعدة البيانات
        using var context = _fixture.CreateContext();
        var dbUser = context.Users.Find(user.Id)!;
        Assert.Equal(0, dbUser.FailedLoginAttempts);
        Assert.Null(dbUser.LockoutEnd);
        Assert.NotNull(dbUser.LastLoginDate);
        Assert.True(dbUser.LastLoginDate >= beforeLogin);
    }

    [Fact]
    public void Login_WithCaseInsensitiveUsername_Succeeds()
    {
        // Arrange
        CreateTestUser(username: "UserCase", password: "Password123!");

        // Act
        var (success, _) = _userService.Login("usercase", "Password123!");

        // Assert
        Assert.True(success);
        Assert.True(_userService.IsLoggedIn);
    }

    [Fact]
    public void Login_WhenUserHadPreviousFailedAttempts_ResetsCounterToZeroCompletely()
    {
        // Arrange: مستخدم لديه 3 محاولات فاشلة سابقة
        var user = CreateTestUser(username: "retryuser", password: "Password123!", failedAttempts: 3);

        // Act: تسجيل دخول ناجح
        var (success, message) = _userService.Login("retryuser", "Password123!");

        // Assert
        Assert.True(success);
        Assert.Equal("تم تسجيل الدخول بنجاح", message);

        using var context = _fixture.CreateContext();
        var dbUser = context.Users.Find(user.Id)!;
        Assert.Equal(0, dbUser.FailedLoginAttempts);
        Assert.Null(dbUser.LockoutEnd);
    }

    #endregion

    #region ب. تسجيل الدخول الفاشل وزيادة العداد (Failed Login & Counter Increment)

    [Fact]
    public void Login_WithWrongPassword_IncrementsFailedAttemptsAndShowsRemaining()
    {
        // Arrange
        var user = CreateTestUser(username: "singlefail", password: "CorrectPassword", failedAttempts: 0);

        // Act
        var (success, message) = _userService.Login("singlefail", "WrongPassword");

        // Assert
        Assert.False(success);
        Assert.Contains("كلمة المرور غير صحيحة", message);
        Assert.Contains("متبقي 4 محاولات", message);
        Assert.False(_userService.IsLoggedIn);

        using var context = _fixture.CreateContext();
        var dbUser = context.Users.Find(user.Id)!;
        Assert.Equal(1, dbUser.FailedLoginAttempts);
        Assert.Null(dbUser.LockoutEnd);
    }

    [Fact]
    public void Login_ConsecutiveWrongPasswords_IncrementsCounterSequentiallyUpToFour()
    {
        // Arrange
        var user = CreateTestUser(username: "multifail", password: "CorrectPassword", failedAttempts: 0);

        // Act & Assert: تكرار المحاولات 1, 2, 3, 4
        for (int attempt = 1; attempt <= 4; attempt++)
        {
            var (success, message) = _userService.Login("multifail", $"WrongPassword_{attempt}");

            Assert.False(success);
            int expectedRemaining = 5 - attempt;
            Assert.Contains($"متبقي {expectedRemaining} محاولات", message);

            using var context = _fixture.CreateContext();
            var dbUser = context.Users.Find(user.Id)!;
            Assert.Equal(attempt, dbUser.FailedLoginAttempts);
            Assert.Null(dbUser.LockoutEnd);
        }
    }

    [Fact]
    public void Login_WithNonExistentUsername_ReturnsFailureWithoutModifyingUsers()
    {
        // Act
        var (success, message) = _userService.Login("ghost_user", "AnyPassword");

        // Assert
        Assert.False(success);
        Assert.Equal("اسم المستخدم غير موجود", message);
        Assert.False(_userService.IsLoggedIn);
    }

    #endregion

    #region ج. الوصول لحد القفل - المحاولة الخامسة بالضبط (5th Attempt Lockout)

    [Fact]
    public void Login_OnFifthFailedAttempt_SetsLockoutEndToFifteenMinutesAndResetsCounterToZero()
    {
        // Arrange: مستخدم لديه 4 محاولات فاشلة سابقة
        var user = CreateTestUser(username: "lockoutuser", password: "CorrectPassword", failedAttempts: 4);
        var beforeAttempt = DateTime.Now;

        // Act: المحاولة الخامسة الفاشلة
        var (success, message) = _userService.Login("lockoutuser", "WrongPassword");

        // Assert
        Assert.False(success);
        Assert.Contains("تم قفل الحساب لمدة 15 دقيقة", message);
        Assert.False(_userService.IsLoggedIn);

        using var context = _fixture.CreateContext();
        var dbUser = context.Users.Find(user.Id)!;

        // التأكد من توقيت القفل (15 دقيقة من الآن بفارق سماحية بضع ثوانٍ)
        Assert.NotNull(dbUser.LockoutEnd);
        var expectedLockoutMin = beforeAttempt.AddMinutes(15).AddSeconds(-5);
        var expectedLockoutMax = DateTime.Now.AddMinutes(15).AddSeconds(5);
        Assert.InRange(dbUser.LockoutEnd.Value, expectedLockoutMin, expectedLockoutMax);

        // سلوك مقصود وموثق: يتم تصفير FailedLoginAttempts فوراً إلى 0 عند تفعيل القفل
        Assert.Equal(0, dbUser.FailedLoginAttempts);
    }

    #endregion

    #region د. الرفض أثناء سريان القفل (Rejection During Active Lockout)

    [Fact]
    public void Login_DuringActiveLockout_WithWrongPassword_RejectsImmediatelyWithoutChangingCounter()
    {
        // Arrange: حساب مقفول ينتهي قفله بعد 10 دقائق
        var futureLockout = DateTime.Now.AddMinutes(10);
        var user = CreateTestUser(
            username: "active_locked_1",
            password: "CorrectPassword",
            failedAttempts: 0,
            lockoutEnd: futureLockout);

        // Act: محاولة بكلمة مرور خاطئة
        var (success, message) = _userService.Login("active_locked_1", "WrongPassword");

        // Assert
        Assert.False(success);
        Assert.Contains("الحساب مقفل", message);
        Assert.Contains("دقيقة", message);
        Assert.False(_userService.IsLoggedIn);

        using var context = _fixture.CreateContext();
        var dbUser = context.Users.Find(user.Id)!;
        // العداد لا يتغير إطلاقاً أثناء القفل
        Assert.Equal(0, dbUser.FailedLoginAttempts);
        Assert.Equal(futureLockout, dbUser.LockoutEnd);
    }

    [Fact]
    public void Login_DuringActiveLockout_WithCorrectPassword_RejectsImmediately()
    {
        // Arrange: حساب مقفول ينتهي قفله بعد 12 دقيقة
        var futureLockout = DateTime.Now.AddMinutes(12);
        var user = CreateTestUser(
            username: "active_locked_2",
            password: "CorrectPassword",
            failedAttempts: 0,
            lockoutEnd: futureLockout);

        // Act: محاولة بكلمة المرور الصحيحة
        var (success, message) = _userService.Login("active_locked_2", "CorrectPassword");

        // Assert
        Assert.False(success);
        Assert.Contains("الحساب مقفل", message);
        Assert.False(_userService.IsLoggedIn);
        Assert.Null(_userService.CurrentUser);
    }

    [Fact]
    public void Login_WhenLockoutPeriodExpired_WithCorrectPassword_AllowsLoginAndClearsLockout()
    {
        // Arrange: قفل انتهت مدته منذ دقيقة
        var pastLockout = DateTime.Now.AddMinutes(-1);
        var user = CreateTestUser(
            username: "expired_lockout",
            password: "ValidPassword123",
            failedAttempts: 0,
            lockoutEnd: pastLockout);

        // Act: تسجيل دخول بكلمة صحيحة بعد انتهاء القفل
        var (success, message) = _userService.Login("expired_lockout", "ValidPassword123");

        // Assert
        Assert.True(success);
        Assert.Equal("تم تسجيل الدخول بنجاح", message);
        Assert.True(_userService.IsLoggedIn);

        using var context = _fixture.CreateContext();
        var dbUser = context.Users.Find(user.Id)!;
        Assert.Null(dbUser.LockoutEnd);
        Assert.Equal(0, dbUser.FailedLoginAttempts);
        Assert.NotNull(dbUser.LastLoginDate);
    }

    [Fact]
    public void Login_WhenLockoutPeriodExpired_WithWrongPassword_EvaluatesPasswordAndIncrementsCounter()
    {
        // Arrange: قفل منتهي منذ دقيقة
        var pastLockout = DateTime.Now.AddMinutes(-1);
        var user = CreateTestUser(
            username: "expired_wrong_pw",
            password: "ValidPassword123",
            failedAttempts: 0,
            lockoutEnd: pastLockout);

        // Act: محاولة فاشلة بعد انتهاء القفل
        var (success, message) = _userService.Login("expired_wrong_pw", "IncorrectPassword");

        // Assert
        Assert.False(success);
        Assert.Contains("كلمة المرور غير صحيحة", message);
        Assert.Contains("متبقي 4 محاولات", message);

        using var context = _fixture.CreateContext();
        var dbUser = context.Users.Find(user.Id)!;
        Assert.Equal(1, dbUser.FailedLoginAttempts);
    }

    #endregion

    #region هـ. الحسابات المجمّدة (Frozen Accounts)

    [Fact]
    public void Login_WhenAccountIsFrozen_RejectsWithAppropriateMessageRegardlessOfPassword()
    {
        // Arrange: مستخدم مجمّد (IsActive = false)
        CreateTestUser(username: "frozen_user", password: "SecretPassword123", isActive: false);

        // Act 1: محاولة بكلمة مرور صحيحة
        var (success1, message1) = _userService.Login("frozen_user", "SecretPassword123");

        // Act 2: محاولة بكلمة مرور خاطئة
        var (success2, message2) = _userService.Login("frozen_user", "WrongPassword");

        // Assert
        Assert.False(success1);
        Assert.Equal("تم تجميد الحساب يرجى مراجعة مدير النظام", message1);

        Assert.False(success2);
        Assert.Equal("تم تجميد الحساب يرجى مراجعة مدير النظام", message2);

        Assert.False(_userService.IsLoggedIn);
    }

    #endregion

    #region و. إلغاء القفل والصلاحيات الإدارية (Admin Actions & Unlock)

    [Fact]
    public void UnlockAccount_WhenUserIsLocked_ClearsBothLockoutEndAndFailedAttempts()
    {
        // Arrange: مستخدم مقفول ولديه محاولات
        var user = CreateTestUser(
            username: "to_unlock",
            password: "Password123",
            failedAttempts: 3,
            lockoutEnd: DateTime.Now.AddMinutes(15));

        // Act
        var (success, message) = _userService.UnlockAccount(user.Id);

        // Assert
        Assert.True(success);
        Assert.Equal("تم فك قفل الحساب", message);

        using var context = _fixture.CreateContext();
        var dbUser = context.Users.Find(user.Id)!;
        Assert.Null(dbUser.LockoutEnd);
        Assert.Equal(0, dbUser.FailedLoginAttempts);

        // التأكد من إمكانية تسجيل الدخول الآن
        var (loginSuccess, _) = _userService.Login("to_unlock", "Password123");
        Assert.True(loginSuccess);
    }

    [Fact]
    public void UnlockAccount_WithNonExistentUser_ReturnsNotFound()
    {
        // Act
        var (success, message) = _userService.UnlockAccount(Guid.NewGuid());

        // Assert
        Assert.False(success);
        Assert.Equal("المستخدم غير موجود", message);
    }

    [Fact]
    public void ResetPassword_ChangesPasswordHashAndClearsLockoutAndCounter()
    {
        // Arrange: مستخدم بكلمة مرور قديمة ومقفول
        var user = CreateTestUser(
            username: "pass_reset_user",
            password: "OldPassword123",
            failedAttempts: 4,
            lockoutEnd: DateTime.Now.AddMinutes(15));

        string oldHash = user.PasswordHash;

        // Act
        var (success, message) = _userService.ResetPassword(user.Id, "NewPassword456");

        // Assert
        Assert.True(success);
        Assert.Equal("تم إعادة تعيين كلمة المرور", message);

        using var context = _fixture.CreateContext();
        var dbUser = context.Users.Find(user.Id)!;
        Assert.NotEqual(oldHash, dbUser.PasswordHash);
        Assert.Null(dbUser.LockoutEnd);
        Assert.Equal(0, dbUser.FailedLoginAttempts);

        // كلمة المرور القديمة تفشل
        var (oldLoginSuccess, _) = _userService.Login("pass_reset_user", "OldPassword123");
        Assert.False(oldLoginSuccess);

        // كلمة المرور الجديدة تنجح
        var (newLoginSuccess, _) = _userService.Login("pass_reset_user", "NewPassword456");
        Assert.True(newLoginSuccess);
    }

    [Fact]
    public void ResetPassword_WithNonExistentUser_ReturnsNotFound()
    {
        // Act
        var (success, message) = _userService.ResetPassword(Guid.NewGuid(), "AnyPass");

        // Assert
        Assert.False(success);
        Assert.Equal("المستخدم غير موجود", message);
    }

    [Fact]
    public void ToggleUserFreeze_OnAdminUser_ProtectsAdminFromFreezing()
    {
        // Arrange: مستخدم مدير النظام الرئيسي "admin"
        var adminUser = CreateTestUser(username: "admin", password: "AdminPassword", role: _adminRole, isActive: true);

        // Act
        var (success, message) = _userService.ToggleUserFreeze(adminUser.Id);

        // Assert
        Assert.False(success);
        Assert.Equal("لا يمكن تجميد حساب مدير النظام الأساسي", message);

        using var context = _fixture.CreateContext();
        var dbAdmin = context.Users.Find(adminUser.Id)!;
        Assert.True(dbAdmin.IsActive);
    }

    [Fact]
    public void DeleteUser_OnAdminUser_ProtectsAdminFromDeletion()
    {
        // Arrange: مستخدم "admin"
        var adminUser = CreateTestUser(username: "admin", password: "AdminPassword", role: _adminRole);

        // Act
        var (success, message) = _userService.DeleteUser(adminUser.Id);

        // Assert
        Assert.False(success);
        Assert.Equal("لا يمكن حذف حساب مدير النظام الأساسي", message);

        using var context = _fixture.CreateContext();
        var dbAdmin = context.Users.Find(adminUser.Id)!;
        Assert.False(dbAdmin.IsDeleted);
    }

    [Fact]
    public void ToggleUserFreeze_OnNormalUser_TogglesActiveStateSuccessfully()
    {
        // Arrange
        var user = CreateTestUser(username: "normal_freeze", password: "Pass", isActive: true);

        // Act 1: تجميد الحساب
        var (success1, message1) = _userService.ToggleUserFreeze(user.Id);

        // Assert 1
        Assert.True(success1);
        Assert.Contains("تجميد", message1);
        using (var context = _fixture.CreateContext())
        {
            Assert.False(context.Users.Find(user.Id)!.IsActive);
        }

        // Act 2: إعادة تنشيط الحساب
        var (success2, message2) = _userService.ToggleUserFreeze(user.Id);

        // Assert 2
        Assert.True(success2);
        Assert.Contains("تنشيط", message2);
        using (var context = _fixture.CreateContext())
        {
            Assert.True(context.Users.Find(user.Id)!.IsActive);
        }
    }

    [Fact]
    public void DeleteUser_OnNormalUser_PerformsSoftDeleteSuccessfully()
    {
        // Arrange
        var user = CreateTestUser(username: "to_delete", password: "Pass");

        // Act
        var (success, message) = _userService.DeleteUser(user.Id);

        // Assert
        Assert.True(success);
        Assert.Equal("تم حذف المستخدم بنجاح", message);

        using var context = _fixture.CreateContext();
        // الكائن محذوف منطقياً ولذلك لا يظهر بالاستعلام العادي بسبب الـ Global Query Filter
        var normalDbUser = context.Users.Find(user.Id);
        Assert.Null(normalDbUser);

        // وباستخدام IgnoreQueryFilters يظهر مع IsDeleted = true و IsActive = false
        var dbUser = context.Users.IgnoreQueryFilters().FirstOrDefault(u => u.Id == user.Id)!;
        Assert.NotNull(dbUser);
        Assert.True(dbUser.IsDeleted);
        Assert.False(dbUser.IsActive);
    }

    [Fact]
    public void ToggleUserFreeze_And_DeleteUser_WithNonExistentUser_ReturnsNotFound()
    {
        var nonExistentId = Guid.NewGuid();

        var (freezeSuccess, freezeMsg) = _userService.ToggleUserFreeze(nonExistentId);
        var (delSuccess, delMsg) = _userService.DeleteUser(nonExistentId);

        Assert.False(freezeSuccess);
        Assert.Equal("المستخدم غير موجود", freezeMsg);

        Assert.False(delSuccess);
        Assert.Equal("المستخدم غير موجود", delMsg);
    }

    #endregion

    #region ز. فحص الصلاحيات (Permissions & Section Checks)

    [Fact]
    public void HasPermission_WhenAdminUserLoggedIn_ReturnsTrueForGrantedRolePermissions()
    {
        // Arrange
        CreateTestUser(username: "admin_perm", password: "Pass", role: _adminRole);
        _userService.Login("admin_perm", "Pass");

        // Act & Assert
        Assert.True(_userService.HasPermission("All"));
        Assert.True(_userService.HasPermission("Sources"));
        Assert.True(_userService.HasPermission("Reports"));
        Assert.True(_userService.HasPermission("Users"));
    }

    [Fact]
    public void HasPermission_WhenNormalUserLoggedIn_ReturnsTrueOnlyForAssignedPermissions()
    {
        // Arrange: دور المستخدم يحتوي فقط على "Sources,Reports"
        CreateTestUser(username: "normal_perm", password: "Pass", role: _userRole);
        _userService.Login("normal_perm", "Pass");

        // Act & Assert
        Assert.True(_userService.HasPermission("Sources"));
        Assert.True(_userService.HasPermission("Reports"));
        Assert.False(_userService.HasPermission("Users"));
        Assert.False(_userService.HasPermission("Settings"));
    }

    [Fact]
    public void HasPermission_WhenNoUserLoggedIn_ReturnsFalse()
    {
        // Arrange: تسجيل خروج
        _userService.Logout();

        // Act & Assert
        Assert.False(_userService.HasPermission("Sources"));
        Assert.False(_userService.HasPermission("All"));
    }

    [Fact]
    public void User_HasSectionPermission_OnAdminUser_AlwaysReturnsTrueForAnySection()
    {
        // Arrange: كائن مستخدم يحمل دور "مدير النظام"
        var adminEntity = new User
        {
            Username = "admin_check",
            Role = new Role { RoleName = "مدير النظام" },
            Permissions = null
        };

        // Act & Assert
        Assert.True(adminEntity.IsAdmin);
        Assert.True(adminEntity.HasSectionPermission("Sources"));
        Assert.True(adminEntity.HasSectionPermission("Users"));
        Assert.True(adminEntity.HasSectionPermission("Settings"));
        Assert.True(adminEntity.HasSectionPermission("AnyRandomNonExistentSection"));
    }

    [Fact]
    public void User_HasSectionPermission_OnNormalUser_ReturnsTrueOnlyForCommaSeparatedAssignedSections()
    {
        // Arrange: مستخدم بصلاحيات أقسام محددة
        var userEntity = new User
        {
            Username = "section_user",
            Role = new Role { RoleName = "مستخدم" },
            Permissions = "Sources, Locations, Borrowing, ActivityCalculator"
        };

        // Act & Assert (مع فحص عدم حساسية حالة الأحرف والمسافات)
        Assert.False(userEntity.IsAdmin);
        Assert.True(userEntity.HasSectionPermission("Sources"));
        Assert.True(userEntity.HasSectionPermission("sources"));
        Assert.True(userEntity.HasSectionPermission("Locations"));
        Assert.True(userEntity.HasSectionPermission("Borrowing"));
        Assert.True(userEntity.HasSectionPermission("ActivityCalculator"));

        Assert.False(userEntity.HasSectionPermission("Users"));
        Assert.False(userEntity.HasSectionPermission("Settings"));
        Assert.False(userEntity.HasSectionPermission("Reports"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void User_HasSectionPermission_WhenPermissionsEmpty_ReturnsFalse(string? emptyPermissions)
    {
        var userEntity = new User
        {
            Username = "no_perms",
            Role = new Role { RoleName = "مستخدم" },
            Permissions = emptyPermissions
        };

        Assert.False(userEntity.HasSectionPermission("Sources"));
    }

    #endregion

    #region ح. عمليات إدارة المستخدمين الإضافية (CRUD & Lookup Operations)

    [Fact]
    public void CreateUser_WithUniqueUsername_CreatesAndHashesPassword()
    {
        // Arrange
        var newUser = new User
        {
            FullName = "مستخدم جديد",
            Username = "new_unique_user",
            RoleId = _userRole.Id,
            Email = "new@test.local",
            IsActive = true
        };

        // Act
        var (success, message) = _userService.CreateUser(newUser, "StrongP@ssw0rd");

        // Assert
        Assert.True(success);
        Assert.Equal("تم إنشاء المستخدم بنجاح", message);

        using var context = _fixture.CreateContext();
        var created = context.Users.FirstOrDefault(u => u.Username == "new_unique_user");
        Assert.NotNull(created);
        Assert.True(PasswordHelper.VerifyPassword("StrongP@ssw0rd", created.PasswordHash));
    }

    [Fact]
    public void CreateUser_WithDuplicateUsername_ReturnsError()
    {
        // Arrange
        CreateTestUser(username: "duplicate_user", password: "Password123");

        var duplicateUser = new User
        {
            FullName = "مستخدم مكرر",
            Username = "duplicate_user",
            RoleId = _userRole.Id
        };

        // Act
        var (success, message) = _userService.CreateUser(duplicateUser, "AnyPass");

        // Assert
        Assert.False(success);
        Assert.Equal("اسم المستخدم موجود بالفعل", message);
    }

    [Fact]
    public void UpdateUser_WithValidData_UpdatesUserProperties()
    {
        // Arrange
        var user = CreateTestUser(username: "to_update", password: "Password123");

        // Act
        user.FullName = "الاسم المحدث بالكامل";
        user.Email = "updated@test.local";
        user.IsEditor = false;
        user.Permissions = "Reports,Locations";
        var (success, message) = _userService.UpdateUser(user);

        // Assert
        Assert.True(success);
        Assert.Equal("تم تحديث بيانات المستخدم", message);

        using var context = _fixture.CreateContext();
        var updated = context.Users.Find(user.Id)!;
        Assert.Equal("الاسم المحدث بالكامل", updated.FullName);
        Assert.Equal("updated@test.local", updated.Email);
        Assert.False(updated.IsEditor);
        Assert.Equal("Reports,Locations", updated.Permissions);
    }

    [Fact]
    public void UpdateUser_WithNonExistentUser_ReturnsNotFound()
    {
        var nonExistentUser = new User
        {
            Id = Guid.NewGuid(),
            Username = "non_existent",
            FullName = "Non Existent"
        };

        var (success, message) = _userService.UpdateUser(nonExistentUser);

        Assert.False(success);
        Assert.Equal("المستخدم غير موجود", message);
    }

    [Fact]
    public void GetAllUsers_And_GetUserById_IncludeRoleAndReturnCorrectData()
    {
        // Arrange
        var user = CreateTestUser(username: "lookup_user", password: "Password123", role: _userRole);

        // Act 1: GetAllUsers
        var allUsers = _userService.GetAllUsers();

        // Act 2: GetUserById
        var fetchedUser = _userService.GetUserById(user.Id);

        // Assert
        Assert.NotEmpty(allUsers);
        Assert.Contains(allUsers, u => u.Id == user.Id && u.Role != null && u.Role.RoleName == "مستخدم");

        Assert.NotNull(fetchedUser);
        Assert.Equal("lookup_user", fetchedUser!.Username);
        Assert.NotNull(fetchedUser.Role);
        Assert.Equal("مستخدم", fetchedUser.Role!.RoleName);
    }

    [Fact]
    public void GetAllRoles_ReturnsAllSeededRoles()
    {
        // Act
        var roles = _userService.GetAllRoles();

        // Assert
        Assert.NotNull(roles);
        Assert.True(roles.Count >= 2);
        Assert.Contains(roles, r => r.RoleName == "مدير النظام");
        Assert.Contains(roles, r => r.RoleName == "مستخدم");
    }

    [Fact]
    public void GetAuditLogs_WithFilters_ReturnsMatchingLogs()
    {
        // Arrange: إنشاء مستخدم وإضافة سجلات تدقيق تجريبية
        var user1 = CreateTestUser(username: "audit_user", password: "Password123");

        using (var context = _fixture.CreateContext())
        {
            context.AuditLogs.AddRange(
                new AuditLog
                {
                    Id = Guid.NewGuid(),
                    UserId = user1.Id,
                    Action = "Insert",
                    TableName = "Sources",
                    ActionDate = DateTime.Now.AddDays(-2),
                    Details = "إضافة مصدر تجريبي"
                },
                new AuditLog
                {
                    Id = Guid.NewGuid(),
                    UserId = user1.Id,
                    Action = "Update",
                    TableName = "Locations",
                    ActionDate = DateTime.Now,
                    Details = "تعديل موقع"
                }
            );
            context.SaveChanges();
        }

        // Act 1: بدون فلتر
        var allLogs = _userService.GetAuditLogs();

        // Act 2: فلتر حسب المستخدم
        var userLogs = _userService.GetAuditLogs(userId: user1.Id);

        // Act 3: فلتر حسب التاريخ
        var recentLogs = _userService.GetAuditLogs(from: DateTime.Now.AddDays(-1));

        // Assert
        Assert.True(allLogs.Count >= 2);
        Assert.All(userLogs, log => Assert.Equal(user1.Id, log.UserId));
        Assert.Single(recentLogs);
        Assert.Equal("Update", recentLogs[0].Action);
    }

    [Fact]
    public void Logout_ClearsCurrentUserAndIsLoggedInState()
    {
        // Arrange
        CreateTestUser(username: "logout_user", password: "Password123");
        _userService.Login("logout_user", "Password123");
        Assert.True(_userService.IsLoggedIn);

        // Act
        _userService.Logout();

        // Assert
        Assert.False(_userService.IsLoggedIn);
        Assert.Null(_userService.CurrentUser);
    }

    #endregion
}
