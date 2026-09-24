namespace Sources.Helpers;

/// <summary>
/// الجولة 201 — تعريف مركزي واحد للقيم المخزَّنة لاسم الدور (Role.RoleName) بدل تكرار السلسلتين
/// النصيتين عبر الملفات. القيم المخزَّنة نفسها بلا أي تغيير (نفس السلاسل الحرفية الحالية تماماً).
/// </summary>
public static class RoleNames
{
    public const string Admin = "مدير النظام";
    public const string User = "مستخدم";

    /// <summary>
    /// يعيد إنتاج Role.DisplayName الحالية بالضبط: مفتاح RoleAdmin لدور المدير، وRoleUser لأي دور آخر
    /// (بما في ذلك أدوار غير متوقعة)، مع نفس نصوص الرجوع (fallback) الحالية عند غياب المفتاح.
    /// </summary>
    public static string GetDisplayName(string? roleName)
    {
        bool isAdmin = roleName == Admin;
        string key = isAdmin ? "RoleAdmin" : "RoleUser";
        string fallback = isAdmin ? Admin : "مستخدم عادي";
        return TranslationHelper.GetString(key) ?? fallback;
    }
}
