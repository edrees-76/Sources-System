using System;

namespace Sources.Helpers;

public static class PasswordHelper
{
    /// <summary>
    /// تشفير كلمة المرور باستخدام BCrypt مع Salt عشوائي تلقائياً
    /// </summary>
    public static string HashPassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password)) return string.Empty;
        return BCrypt.Net.BCrypt.HashPassword(password);
    }

    /// <summary>
    /// التحقق من مطابقة كلمة المرور للتشفير المخزن
    /// </summary>
    public static bool VerifyPassword(string password, string hash)
    {
        if (string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(hash)) 
            return false;
            
        try
        {
            return BCrypt.Net.BCrypt.Verify(password, hash);
        }
        catch
        {
            // في حال كان التشفير القديم (SHA256) موجوداً، قد يفشل BCrypt
            return false;
        }
    }
}
