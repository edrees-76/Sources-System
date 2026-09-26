using System;
using System.Linq;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sources.Data;
using Sources.Models;
using Sources.Helpers;

namespace Sources.Services;

public class UserService : IUserService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly IAuditService? _auditService;
    private readonly ILicenseService? _licenseService;
    private User? _currentUser;
    private readonly TimeProvider _timeProvider;

    /// <summary>أقصى عدد محاولات فاشلة قبل قفل الحساب</summary>
    private const int MaxFailedAttempts = 5;
    /// <summary>مدة قفل الحساب بالدقائق</summary>
    private const int LockoutDurationMinutes = 15;
    /// <summary>أقل طول مقبول لكلمة المرور (الجولة 209)</summary>
    public const int MinPasswordLength = 6;
    /// <summary>اسم حساب مدير النظام الأساسي المحمي من الحذف والتجميد والتخفيض</summary>
    private const string BaseAdminUsername = "admin";

    public User? CurrentUser => _currentUser;
    public bool IsLoggedIn => _currentUser != null;

    public UserService(IDbContextFactory<AppDbContext> dbFactory, IAuditService? auditService = null, ILicenseService? licenseService = null, TimeProvider? timeProvider = null)
    {
        _dbFactory = dbFactory;
        _auditService = auditService;
        _licenseService = licenseService;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public (bool Success, string Message) Login(string username, string password)
    {
        try
        {
            using var db = _dbFactory.CreateDbContext();
            var trimmedUsername = (username ?? string.Empty).Trim();
            var lowerUsername = trimmedUsername.ToLower();
            // المطابقة التامة أولاً، ثم غير الحساسة لحالة الأحرف: قواعد قديمة قد تحوي اسمين
            // يختلفان في حالة الأحرف فقط (مثل admin وAdmin) قبل فرض التفرّد غير الحساس (الجولة 209).
            var user = db.Users
                           .Include(u => u.Role)
                           .FirstOrDefault(u => u.Username == trimmedUsername)
                       ?? db.Users
                           .Include(u => u.Role)
                           .Where(u => u.Username.ToLower() == lowerUsername)
                           .OrderBy(u => u.CreatedAt)
                           .FirstOrDefault();

            if (user == null)
                return (false, TranslationHelper.GetString("MsgErrUsernameNotFound") ?? "اسم المستخدم غير موجود");

            if (!user.IsActive)
                return (false, TranslationHelper.GetString("MsgErrAccountFrozen") ?? "تم تجميد الحساب يرجى مراجعة مدير النظام");

            // ─── التحقق من قفل الحساب ───
            if (user.LockoutEnd.HasValue && user.LockoutEnd.Value > _timeProvider.LocalNow())
            {
                var remaining = (user.LockoutEnd.Value - _timeProvider.LocalNow()).Minutes + 1;
                LoggerService.LogInfo($"محاولة دخول لحساب مقفل: {username}");
                return (false, string.Format(TranslationHelper.GetString("MsgErrAccountLocked") ?? "الحساب مقفل. حاول مرة أخرى بعد {0} دقيقة", remaining));
            }

            if (!PasswordHelper.VerifyPassword(password, user.PasswordHash))
            {
                // ─── زيادة عداد المحاولات الفاشلة ───
                user.FailedLoginAttempts++;

                if (user.FailedLoginAttempts >= MaxFailedAttempts)
                {
                    user.LockoutEnd = _timeProvider.LocalNow().AddMinutes(LockoutDurationMinutes);
                    user.FailedLoginAttempts = 0;
                    db.SaveChanges();
                    LoggerService.LogInfo($"تم قفل حساب {username} بعد {MaxFailedAttempts} محاولات فاشلة");
                    return (false, string.Format(TranslationHelper.GetString("MsgErrAccountLockedNow") ?? "تم قفل الحساب لمدة {0} دقيقة بسبب محاولات دخول فاشلة متعددة", LockoutDurationMinutes));
                }

                db.SaveChanges();
                var attemptsLeft = MaxFailedAttempts - user.FailedLoginAttempts;
                LoggerService.LogInfo($"محاولة دخول فاشلة للمستخدم: {username} (متبقي {attemptsLeft} محاولات)");
                return (false, string.Format(TranslationHelper.GetString("MsgErrWrongPasswordAttemptsLeft") ?? "كلمة المرور غير صحيحة. متبقي {0} محاولات قبل قفل الحساب", attemptsLeft));
            }

            // ─── تسجيل دخول ناجح ───
            user.FailedLoginAttempts = 0;
            user.LockoutEnd = null;
            user.LastLoginDate = _timeProvider.LocalNow();
            db.SaveChanges();

            _currentUser = user;
            LoggerService.LogInfo($"تم تسجيل دخول المستخدم: {username}");
            return (true, TranslationHelper.GetString("MsgSuccessLogin") ?? "تم تسجيل الدخول بنجاح");
        }
        catch (Exception ex)
        {
            LoggerService.LogError("خطأ أثناء تسجيل الدخول", ex);
            return (false, TranslationHelper.GetString("MsgErrLoginTechnicalError") ?? "حدث خطأ فني أثناء تسجيل الدخول");
        }
    }

    public void Logout()
    {
        _currentUser = null;
    }

    /// <summary>التحقق من صلاحية معينة للمستخدم الحالي</summary>
    public bool HasPermission(string permission)
    {
        return _currentUser?.HasSectionPermission(permission) ?? false;
    }

    public List<User> GetAllUsers()
    {
        using var db = _dbFactory.CreateDbContext();
        return db.Users.Include(u => u.Role).OrderBy(u => u.FullName).ToList();
    }

    public User? GetUserById(Guid id)
    {
        using var db = _dbFactory.CreateDbContext();
        return db.Users.Include(u => u.Role).FirstOrDefault(u => u.Id == id);
    }

    public (bool Success, string Message) CreateUser(User user, string password)
    {
        var activation = AuthorizationGuard.RequireActivated(_licenseService!);
        if (!activation.Allowed) return (false, activation.Message);

        var guard = AuthorizationGuard.RequireAdmin(CurrentUser);
        if (!guard.Allowed) return (false, guard.Message);

        var passwordCheck = ValidateNewPassword(password);
        if (!passwordCheck.Valid) return (false, passwordCheck.Message);

        user.Username = (user.Username ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(user.Username))
            return (false, TranslationHelper.GetString("MsgErrUsernameRequired") ?? "اسم المستخدم مطلوب");

        using var db = _dbFactory.CreateDbContext();
        // التفرّد غير حساس لحالة الأحرف لأن تسجيل الدخول غير حساس لها (admin و Admin حساب واحد).
        var lowerNewUsername = user.Username.ToLower();
        if (db.Users.Any(u => u.Username.ToLower() == lowerNewUsername))
            return (false, TranslationHelper.GetString("MsgErrUsernameExists") ?? "اسم المستخدم موجود بالفعل");

        user.PasswordHash = PasswordHelper.HashPassword(password);
        db.Users.Add(user);
        db.SaveChanges();

        var newValuesObj = new
        {
            user.FullName,
            user.Username,
            user.Email,
            user.RoleId,
            user.IsActive,
            user.IsEditor,
            user.Permissions
        };

        _auditService?.LogWithChanges(
            action: "Create",
            tableName: "Users",
            recordId: user.Id,
            details: $"إنشاء مستخدم جديد: {user.FullName} ({user.Username})",
            oldValues: null,
            newValues: JsonSerializer.Serialize(newValuesObj)
        );

        return (true, TranslationHelper.GetString("MsgSuccessUserCreated") ?? "تم إنشاء المستخدم بنجاح");
    }

    public (bool Success, string Message) UpdateUser(User user)
    {
        var activation = AuthorizationGuard.RequireActivated(_licenseService!);
        if (!activation.Allowed) return (false, activation.Message);

        var guard = AuthorizationGuard.RequireAdmin(CurrentUser);
        if (!guard.Allowed) return (false, guard.Message);

        using var db = _dbFactory.CreateDbContext();
        var existing = db.Users.Include(u => u.Role).FirstOrDefault(u => u.Id == user.Id);
        if (existing == null)
            return (false, TranslationHelper.GetString("MsgErrUserNotFound") ?? "المستخدم غير موجود");

        var adminRoleId = db.Roles.Where(r => r.RoleName == RoleNames.Admin).Select(r => (Guid?)r.Id).FirstOrDefault();
        var wasActiveAdmin = existing.IsActive && adminRoleId.HasValue && existing.RoleId == adminRoleId.Value;
        var willBeActiveAdmin = user.IsActive && adminRoleId.HasValue && user.RoleId == adminRoleId.Value;

        // حساب مدير النظام الأساسي لا يُجمَّد ولا يُخفَّض دوره (نفس حماية ToggleUserFreeze و DeleteUser).
        if (IsBaseAdmin(existing) && !willBeActiveAdmin)
            return (false, TranslationHelper.GetString("MsgErrCannotDemoteOrFreezeBaseAdmin") ?? "لا يمكن تجميد حساب مدير النظام الأساسي أو تغيير دوره");

        // لا يُجمِّد المستخدم حسابه الحالي بنفسه.
        if (CurrentUser != null && existing.Id == CurrentUser.Id && !user.IsActive)
            return (false, TranslationHelper.GetString("MsgErrCannotFreezeOwnAccount") ?? "لا يمكنك تجميد حسابك الحالي");

        // يجب أن يبقى مدير نظام نشط واحد على الأقل.
        if (wasActiveAdmin && !willBeActiveAdmin &&
            !db.Users.Any(u => u.Id != existing.Id && u.IsActive && u.RoleId == adminRoleId!.Value))
            return (false, TranslationHelper.GetString("MsgErrLastActiveAdmin") ?? "لا يمكن تنفيذ العملية: يجب أن يبقى مدير نظام نشط واحد على الأقل");

        var oldValuesObj = new
        {
            existing.FullName,
            existing.Email,
            existing.RoleId,
            existing.IsActive,
            existing.IsEditor,
            existing.Permissions
        };

        existing.FullName = user.FullName;
        existing.Email = user.Email;
        existing.RoleId = user.RoleId;
        existing.IsActive = user.IsActive;
        existing.Permissions = user.Permissions;
        existing.IsEditor = user.IsEditor;
        db.SaveChanges();

        var newValuesObj = new
        {
            existing.FullName,
            existing.Email,
            existing.RoleId,
            existing.IsActive,
            existing.IsEditor,
            existing.Permissions
        };

        _auditService?.LogWithChanges(
            action: "Update",
            tableName: "Users",
            recordId: user.Id,
            details: $"تعديل بيانات المستخدم: {existing.FullName} ({existing.Username})",
            oldValues: JsonSerializer.Serialize(oldValuesObj),
            newValues: JsonSerializer.Serialize(newValuesObj)
        );

        return (true, TranslationHelper.GetString("MsgSuccessUserUpdated") ?? "تم تحديث بيانات المستخدم");
    }

    public (bool Success, string Message) ResetPassword(Guid userId, string newPassword)
    {
        // عمداً لا يُفحَص AuthorizationGuard.RequireActivated هنا (خلافاً لبقية دوال هذا الملف):
        // تغيير كلمة المرور عملية أمان حساب وليست "بيانات عمل" يُقصد بها حارس الوضع التجريبي.
        // فرض تغيير كلمة المرور الافتراضية (MustChangePassword) يحدث إلزامياً بعد أول تسجيل دخول
        // على أي جهاز جديد — أي قبل أي تفعيل ممكن أصلاً. فحص RequireActivated هنا كان يُنتج حلقة
        // مغلقة تامة تمنع أي عميل جديد من إكمال الإعداد (مُكتشَف واقعياً في الجولة 186؛ راجع
        // release-readiness.md لتفاصيل إعادة الإنتاج الكاملة).
        var guard = AuthorizationGuard.RequireAdmin(CurrentUser);
        if (!guard.Allowed) return (false, guard.Message);

        var passwordCheck = ValidateNewPassword(newPassword);
        if (!passwordCheck.Valid) return (false, passwordCheck.Message);

        using var db = _dbFactory.CreateDbContext();
        var user = db.Users.Find(userId);
        if (user == null) return (false, TranslationHelper.GetString("MsgErrUserNotFound") ?? "المستخدم غير موجود");

        if (user.Username == "admin" && CurrentUser?.Username != "admin")
            return (false, TranslationHelper.GetString("MsgErrCannotChangeAdminPasswordFromOther") ?? "لا يمكن تغيير كلمة مرور حساب مدير النظام الأساسي من حساب آخر");

        user.PasswordHash = PasswordHelper.HashPassword(newPassword);
        user.FailedLoginAttempts = 0;
        user.LockoutEnd = null;
        user.MustChangePassword = false;
        db.SaveChanges();
        SyncCurrentUserPassword(user);

        var auditService = _auditService ?? new AuditService(_dbFactory, this);
        auditService.Log("ResetPassword", "Users", userId, $"إعادة تعيين كلمة مرور المستخدم: {user.FullName} (@{user.Username})");

        return (true, TranslationHelper.GetString("MsgSuccessPasswordReset") ?? "تم إعادة تعيين كلمة المرور");
    }

    /// <summary>فك قفل حساب المستخدم</summary>
    // عمداً لا يُفحَص AuthorizationGuard.RequireActivated هنا (خلافاً لبقية دوال هذا الملف):
    // فك قفل الحساب عملية أمان حساب وليست "بيانات عمل" يُقصد بها حارس الوضع التجريبي.
    // القفل نفسه قد يحدث على جهاز جديد تماماً قبل أن يصبح التفعيل ممكناً أصلاً، فإبقاء الحارس هنا
    // يُنتج نفس الحلقة المغلقة المُكتشَفة في الجولة 186 لـResetPassword (راجع release-readiness.md).
    // نفس القرار المعماري طُبِّق هنا في الجولة 190.
    public (bool Success, string Message) UnlockAccount(Guid userId)
    {
        var guard = AuthorizationGuard.RequireAdmin(CurrentUser);
        if (!guard.Allowed) return (false, guard.Message);

        using var db = _dbFactory.CreateDbContext();
        var user = db.Users.Find(userId);
        if (user == null) return (false, TranslationHelper.GetString("MsgErrUserNotFound") ?? "المستخدم غير موجود");

        var oldValuesObj = new { user.FailedLoginAttempts, user.LockoutEnd };

        user.FailedLoginAttempts = 0;
        user.LockoutEnd = null;
        db.SaveChanges();

        var auditService = _auditService ?? new AuditService(_dbFactory, this);
        auditService.LogWithChanges(
            "UnlockAccount",
            "Users",
            userId,
            $"فك قفل حساب: {user.FullName} (@{user.Username})",
            JsonSerializer.Serialize(oldValuesObj),
            JsonSerializer.Serialize(new { FailedLoginAttempts = 0, LockoutEnd = (DateTime?)null }));

        return (true, TranslationHelper.GetString("MsgSuccessAccountUnlocked") ?? "تم فك قفل الحساب");
    }

    public (bool Success, string Message) DeleteUser(Guid userId)
    {
        var activation = AuthorizationGuard.RequireActivated(_licenseService!);
        if (!activation.Allowed) return (false, activation.Message);

        var guard = AuthorizationGuard.RequireAdmin(CurrentUser);
        if (!guard.Allowed) return (false, guard.Message);

        using var db = _dbFactory.CreateDbContext();
        var user = db.Users.Find(userId);
        if (user == null) return (false, TranslationHelper.GetString("MsgErrUserNotFound") ?? "المستخدم غير موجود");

        // منع حذف مستخدم admin الأساسي
        if (user.Username == "admin") return (false, TranslationHelper.GetString("MsgErrCannotDeleteAdmin") ?? "لا يمكن حذف حساب مدير النظام الأساسي");
        if (CurrentUser != null && user.Id == CurrentUser.Id)
            return (false, TranslationHelper.GetString("MsgErrCannotDeleteOwnAccount") ?? "لا يمكنك حذف حسابك الحالي");

        user.IsDeleted = true;
        user.IsActive = false;
        user.DeletedAt = DateTime.Now;
        var currentUserId = CurrentUser?.Id;
        if (currentUserId.HasValue && db.Users.Any(u => u.Id == currentUserId.Value))
        {
            user.DeletedBy = currentUserId.Value;
        }
        else
        {
            user.DeletedBy = null;
        }
        db.SaveChanges();

        var auditService = _auditService ?? new AuditService(_dbFactory, this);
        auditService.Log("Delete", "Users", userId, $"حذف مستخدم: {user.FullName} ({user.Username})");

        return (true, TranslationHelper.GetString("MsgSuccessUserDeleted") ?? "تم حذف المستخدم بنجاح");
    }

    public (bool Success, string Message) RestoreUser(Guid userId)
    {
        var activation = AuthorizationGuard.RequireActivated(_licenseService!);
        if (!activation.Allowed) return (false, activation.Message);

        var guard = AuthorizationGuard.RequireAdmin(CurrentUser);
        if (!guard.Allowed) return (false, guard.Message);

        using var db = _dbFactory.CreateDbContext();
        var user = db.Users.IgnoreQueryFilters().Include(u => u.Role).FirstOrDefault(u => u.Id == userId);
        if (user == null) return (false, TranslationHelper.GetString("MsgErrUserNotFound") ?? "المستخدم غير موجود");
        if (!user.IsDeleted) return (false, TranslationHelper.GetString("MsgErrUserNotDeleted") ?? "المستخدم غير محذوف أصلاً");

        // التحقق من فرادة اسم المستخدم بين الحسابات النشطة
        var lowerUsername = user.Username.Trim().ToLower();
        if (db.Users.Any(u => !u.IsDeleted && u.Id != userId && u.Username.ToLower() == lowerUsername))
            return (false, string.Format(TranslationHelper.GetString("MsgErrCannotRestoreUsernameConflict") ?? "لا يمكن استرجاع المستخدم لوجود حساب نشط آخر بنفس اسم المستخدم (@{0})", user.Username));

        user.IsDeleted = false;
        user.IsActive = true;
        user.DeletedAt = null;
        user.DeletedBy = null;
        db.SaveChanges();

        var auditService = _auditService ?? new AuditService(_dbFactory, this);
        auditService.Log("Restore", "Users", userId, $"استرجاع مستخدم: {user.FullName} (@{user.Username})");

        return (true, string.Format(TranslationHelper.GetString("MsgSuccessUserRestored") ?? "تم استرجاع حساب المستخدم {0}", user.FullName));
    }

    /// <summary>تجميد أو تنشيط حساب مستخدم</summary>
    public (bool Success, string Message) ToggleUserFreeze(Guid userId)
    {
        var activation = AuthorizationGuard.RequireActivated(_licenseService!);
        if (!activation.Allowed) return (false, activation.Message);

        var guard = AuthorizationGuard.RequireAdmin(CurrentUser);
        if (!guard.Allowed) return (false, guard.Message);

        using var db = _dbFactory.CreateDbContext();
        var user = db.Users.Find(userId);
        if (user == null) return (false, TranslationHelper.GetString("MsgErrUserNotFound") ?? "المستخدم غير موجود");
        if (user.Username == "admin") return (false, TranslationHelper.GetString("MsgErrCannotFreezeAdmin") ?? "لا يمكن تجميد حساب مدير النظام الأساسي");
        if (user.IsActive && CurrentUser != null && user.Id == CurrentUser.Id)
            return (false, TranslationHelper.GetString("MsgErrCannotFreezeOwnAccount") ?? "لا يمكنك تجميد حسابك الحالي");

        var oldValuesObj = new { user.IsActive };

        user.IsActive = !user.IsActive;
        db.SaveChanges();

        // السجل الرقابي يبقى عربياً دوماً بتصميم مقصود، بمعزل عن لغة الواجهة المستخدمة في الرسالة المُعادة أدناه
        string action = user.IsActive ? "تنشيط" : "تجميد";
        string translatedAction = user.IsActive
            ? (TranslationHelper.GetString("TextActivate") ?? "تنشيط")
            : (TranslationHelper.GetString("TextDeactivate") ?? "تجميد");

        var auditService = _auditService ?? new AuditService(_dbFactory, this);
        auditService.LogWithChanges(
            "ToggleUserFreeze",
            "Users",
            userId,
            $"{action} حساب: {user.FullName} (@{user.Username})",
            JsonSerializer.Serialize(oldValuesObj),
            JsonSerializer.Serialize(new { user.IsActive }));

        return (true, string.Format(TranslationHelper.GetString("MsgSuccessToggleFreeze") ?? "تم {0} حساب {1}", translatedAction, user.FullName));
    }

    /// <summary>
    /// تغيير المستخدم المسجَّل لكلمة مروره بنفسه بعد التحقق من كلمة المرور الحالية (الجولة 209).
    /// لا يتطلب صلاحية مدير ولا تفعيلاً: عملية أمان حساب وليست بيانات عمل، فتعمل قبل التفعيل أيضاً
    /// (نفس قرار الجولتين 186 و190 لـ ResetPassword و UnlockAccount). تغيير كلمة المرور الافتراضية
    /// يبقى اختيارياً بقرار الجولة 187؛ نجاح التغيير هنا يُصفِّر MustChangePassword فيزول التنبيه الدائم.
    /// </summary>
    public (bool Success, string Message) ChangeOwnPassword(string currentPassword, string newPassword)
    {
        if (CurrentUser == null)
            return (false, TranslationHelper.GetString("MsgErrNotLoggedIn") ?? "لا يمكن تنفيذ العملية: لا يوجد مستخدم مسجَّل الدخول.");

        var passwordCheck = ValidateNewPassword(newPassword);
        if (!passwordCheck.Valid) return (false, passwordCheck.Message);

        using var db = _dbFactory.CreateDbContext();
        var user = db.Users.Find(CurrentUser.Id);
        if (user == null) return (false, TranslationHelper.GetString("MsgErrUserNotFound") ?? "المستخدم غير موجود");

        if (!PasswordHelper.VerifyPassword(currentPassword, user.PasswordHash))
            return (false, TranslationHelper.GetString("MsgErrCurrentPasswordIncorrect") ?? "كلمة المرور الحالية غير صحيحة");

        if (PasswordHelper.VerifyPassword(newPassword, user.PasswordHash))
            return (false, TranslationHelper.GetString("MsgErrNewPasswordSameAsCurrent") ?? "كلمة المرور الجديدة يجب أن تختلف عن كلمة المرور الحالية");

        user.PasswordHash = PasswordHelper.HashPassword(newPassword);
        user.MustChangePassword = false;
        user.FailedLoginAttempts = 0;
        user.LockoutEnd = null;
        db.SaveChanges();
        SyncCurrentUserPassword(user);

        var auditService = _auditService ?? new AuditService(_dbFactory, this);
        auditService.Log("ChangeOwnPassword", "Users", user.Id, $"تغيير المستخدم لكلمة مروره: {user.FullName} (@{user.Username})");

        return (true, TranslationHelper.GetString("MsgSuccessOwnPasswordChanged") ?? "تم تغيير كلمة المرور بنجاح");
    }

    private static (bool Valid, string Message) ValidateNewPassword(string? password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < MinPasswordLength)
            return (false, string.Format(TranslationHelper.GetString("MsgErrPasswordTooShort") ?? "كلمة المرور يجب ألا تقل عن {0} أحرف", MinPasswordLength));
        return (true, string.Empty);
    }

    private static bool IsBaseAdmin(User user) =>
        string.Equals(user.Username, BaseAdminUsername, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// مزامنة نسخة المستخدم المسجَّل في الذاكرة بعد تغيير كلمة مروره، كي لا تقبل نوافذ التحقق
    /// (مثل PasswordPromptDialog) كلمة المرور القديمة حتى إعادة تسجيل الدخول.
    /// </summary>
    private void SyncCurrentUserPassword(User updated)
    {
        if (_currentUser == null || _currentUser.Id != updated.Id) return;
        _currentUser.PasswordHash = updated.PasswordHash;
        _currentUser.MustChangePassword = updated.MustChangePassword;
        _currentUser.FailedLoginAttempts = updated.FailedLoginAttempts;
        _currentUser.LockoutEnd = updated.LockoutEnd;
    }

    /// <summary>استرجاع سجل التدقيق مع فلاتر اختيارية</summary>
    public List<AuditLog> GetAuditLogs(Guid? userId = null, DateTime? from = null, DateTime? to = null)
    {
        var auditService = _auditService ?? new AuditService(_dbFactory, this);
        return auditService.GetAuditLogs(page: 1, pageSize: 200, userFilter: userId, fromDate: from, toDate: to);
    }

    public List<Role> GetAllRoles()
    {
        using var db = _dbFactory.CreateDbContext();
        return db.Roles.OrderBy(r => r.RoleName).ToList();
    }
}
