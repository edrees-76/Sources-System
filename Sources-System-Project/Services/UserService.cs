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
    private User? _currentUser;

    /// <summary>أقصى عدد محاولات فاشلة قبل قفل الحساب</summary>
    private const int MaxFailedAttempts = 5;
    /// <summary>مدة قفل الحساب بالدقائق</summary>
    private const int LockoutDurationMinutes = 15;

    public User? CurrentUser => _currentUser;
    public bool IsLoggedIn => _currentUser != null;

    public UserService(IDbContextFactory<AppDbContext> dbFactory, IAuditService? auditService = null)
    {
        _dbFactory = dbFactory;
        _auditService = auditService;
    }

    public (bool Success, string Message) Login(string username, string password)
    {
        try
        {
            using var db = _dbFactory.CreateDbContext();
            var lowerUsername = username.ToLower();
            var user = db.Users
                .Include(u => u.Role)
                .FirstOrDefault(u => u.Username.ToLower() == lowerUsername);

            if (user == null)
                return (false, "اسم المستخدم غير موجود");

            if (!user.IsActive)
                return (false, "تم تجميد الحساب يرجى مراجعة مدير النظام");

            // ─── التحقق من قفل الحساب ───
            if (user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTime.Now)
            {
                var remaining = (user.LockoutEnd.Value - DateTime.Now).Minutes + 1;
                LoggerService.LogInfo($"محاولة دخول لحساب مقفل: {username}");
                return (false, $"الحساب مقفل. حاول مرة أخرى بعد {remaining} دقيقة");
            }

            if (!PasswordHelper.VerifyPassword(password, user.PasswordHash))
            {
                // ─── زيادة عداد المحاولات الفاشلة ───
                user.FailedLoginAttempts++;

                if (user.FailedLoginAttempts >= MaxFailedAttempts)
                {
                    user.LockoutEnd = DateTime.Now.AddMinutes(LockoutDurationMinutes);
                    user.FailedLoginAttempts = 0;
                    db.SaveChanges();
                    LoggerService.LogInfo($"تم قفل حساب {username} بعد {MaxFailedAttempts} محاولات فاشلة");
                    return (false, $"تم قفل الحساب لمدة {LockoutDurationMinutes} دقيقة بسبب محاولات دخول فاشلة متعددة");
                }

                db.SaveChanges();
                var attemptsLeft = MaxFailedAttempts - user.FailedLoginAttempts;
                LoggerService.LogInfo($"محاولة دخول فاشلة للمستخدم: {username} (متبقي {attemptsLeft} محاولات)");
                return (false, $"كلمة المرور غير صحيحة. متبقي {attemptsLeft} محاولات قبل قفل الحساب");
            }

            // ─── تسجيل دخول ناجح ───
            user.FailedLoginAttempts = 0;
            user.LockoutEnd = null;
            user.LastLoginDate = DateTime.Now;
            db.SaveChanges();

            _currentUser = user;
            LoggerService.LogInfo($"تم تسجيل دخول المستخدم: {username}");
            return (true, "تم تسجيل الدخول بنجاح");
        }
        catch (Exception ex)
        {
            LoggerService.LogError("خطأ أثناء تسجيل الدخول", ex);
            return (false, "حدث خطأ فني أثناء تسجيل الدخول");
        }
    }

    public void Logout()
    {
        _currentUser = null;
    }

    /// <summary>التحقق من صلاحية معينة للمستخدم الحالي</summary>
    public bool HasPermission(string permission)
    {
        if (_currentUser?.Role?.Permissions == null) return false;
        return _currentUser.Role.Permissions.Contains(permission);
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
        using var db = _dbFactory.CreateDbContext();
        if (db.Users.Any(u => u.Username == user.Username))
            return (false, "اسم المستخدم موجود بالفعل");

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

        return (true, "تم إنشاء المستخدم بنجاح");
    }

    public (bool Success, string Message) UpdateUser(User user)
    {
        using var db = _dbFactory.CreateDbContext();
        var existing = db.Users.Find(user.Id);
        if (existing == null)
            return (false, "المستخدم غير موجود");

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

        return (true, "تم تحديث بيانات المستخدم");
    }

    public (bool Success, string Message) ResetPassword(Guid userId, string newPassword)
    {
        using var db = _dbFactory.CreateDbContext();
        var user = db.Users.Find(userId);
        if (user == null) return (false, "المستخدم غير موجود");

        user.PasswordHash = PasswordHelper.HashPassword(newPassword);
        user.FailedLoginAttempts = 0;
        user.LockoutEnd = null;
        db.SaveChanges();
        return (true, "تم إعادة تعيين كلمة المرور");
    }

    /// <summary>فك قفل حساب المستخدم</summary>
    public (bool Success, string Message) UnlockAccount(Guid userId)
    {
        using var db = _dbFactory.CreateDbContext();
        var user = db.Users.Find(userId);
        if (user == null) return (false, "المستخدم غير موجود");

        user.FailedLoginAttempts = 0;
        user.LockoutEnd = null;
        db.SaveChanges();
        return (true, "تم فك قفل الحساب");
    }

    public (bool Success, string Message) DeleteUser(Guid userId)
    {
        using var db = _dbFactory.CreateDbContext();
        var user = db.Users.Find(userId);
        if (user == null) return (false, "المستخدم غير موجود");
        
        // منع حذف مستخدم admin الأساسي
        if (user.Username == "admin") return (false, "لا يمكن حذف حساب مدير النظام الأساسي");

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

        return (true, "تم حذف المستخدم بنجاح");
    }

    /// <summary>تجميد أو تنشيط حساب مستخدم</summary>
    public (bool Success, string Message) ToggleUserFreeze(Guid userId)
    {
        using var db = _dbFactory.CreateDbContext();
        var user = db.Users.Find(userId);
        if (user == null) return (false, "المستخدم غير موجود");
        if (user.Username == "admin") return (false, "لا يمكن تجميد حساب مدير النظام الأساسي");

        user.IsActive = !user.IsActive;
        db.SaveChanges();

        string action = user.IsActive ? "تنشيط" : "تجميد";
        return (true, $"تم {action} حساب {user.FullName}");
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
