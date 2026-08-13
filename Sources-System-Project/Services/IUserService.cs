using System;
using System.Collections.Generic;
using Sources.Models;

namespace Sources.Services;

public interface IUserService
{
    User? CurrentUser { get; }
    bool IsLoggedIn { get; }
    (bool Success, string Message) Login(string username, string password);
    void Logout();
    bool HasPermission(string permission);
    List<User> GetAllUsers();
    User? GetUserById(Guid id);
    (bool Success, string Message) CreateUser(User user, string password);
    (bool Success, string Message) UpdateUser(User user);
    (bool Success, string Message) ResetPassword(Guid userId, string newPassword);
    (bool Success, string Message) UnlockAccount(Guid userId);
    (bool Success, string Message) DeleteUser(Guid userId);
    (bool Success, string Message) ToggleUserFreeze(Guid userId);
    List<AuditLog> GetAuditLogs(Guid? userId = null, DateTime? from = null, DateTime? to = null);
    List<Role> GetAllRoles();
}
