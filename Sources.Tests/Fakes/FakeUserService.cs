using System;
using System.Collections.Generic;
using Sources.Models;
using Sources.Services;

namespace Sources.Tests.Fakes;

public class FakeUserService : IUserService
{
    public User? CurrentUser { get; set; }
    public bool IsLoggedIn => CurrentUser != null;

    public FakeUserService(User? initialUser = null)
    {
        CurrentUser = initialUser ?? new User
        {
            Id = Guid.NewGuid(),
            FullName = "مستخدم اختباري",
            Username = "testuser",
            IsActive = true,
            RoleId = Guid.NewGuid()
        };
    }

    public (bool Success, string Message) Login(string username, string password) => (true, "تم تسجيل الدخول بنجاح");
    public void Logout() => CurrentUser = null;
    public bool HasPermission(string permission) => true;
    public List<User> GetAllUsers() => CurrentUser != null ? new List<User> { CurrentUser } : new List<User>();
    public User? GetUserById(Guid id) => CurrentUser?.Id == id ? CurrentUser : null;
    public (bool Success, string Message) CreateUser(User user, string password) => (true, "تم إنشاء المستخدم");
    public (bool Success, string Message) UpdateUser(User user) => (true, "تم تحديث المستخدم");
    public (bool Success, string Message) ResetPassword(Guid userId, string newPassword) => (true, "تم إعادة تعيين كلمة المرور");
    public (bool Success, string Message) UnlockAccount(Guid userId) => (true, "تم إلغاء القفل");
    public (bool Success, string Message) DeleteUser(Guid userId) => (true, "تم الحذف");
    public (bool Success, string Message) RestoreUser(Guid userId) => (true, "تم الاسترجاع");
    public (bool Success, string Message) ToggleUserFreeze(Guid userId) => (true, "تم التعديل");
    public List<AuditLog> GetAuditLogs(Guid? userId = null, DateTime? from = null, DateTime? to = null) => new();
    public List<Role> GetAllRoles() => new();
}
