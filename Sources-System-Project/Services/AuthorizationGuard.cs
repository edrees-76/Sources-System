using Sources.Helpers;
using Sources.Models;

namespace Sources.Services;

/// <summary>
/// حارس الصلاحيات في طبقة الخدمة. الواجهة تُخفي الأزرار لتحسين التجربة،
/// وهذا يمنع التنفيذ فعلياً — فشاشة تنسى الإخفاء لا تفتح ثغرة.
/// يعتمد الآليات القائمة على User: IsAdmin و IsEditor و HasSectionPermission.
/// </summary>
public static class AuthorizationGuard
{
    /// <summary>يتطلب مستخدماً مسجَّلاً بصلاحية تعديل، ووصولاً للقسم المحدد.</summary>
    public static (bool Allowed, string Message) RequireEditor(User? user, string section)
    {
        if (user == null)
            return (false, TranslationHelper.GetString("MsgErrNotLoggedIn")
                ?? "لا يمكن تنفيذ العملية: لا يوجد مستخدم مسجَّل الدخول.");

        if (!user.IsEditor)
            return (false, TranslationHelper.GetString("MsgErrReadOnlyUser")
                ?? "لا تملك صلاحية التعديل. حسابك للاطّلاع فقط.");

        if (!user.HasSectionPermission(section))
            return (false, TranslationHelper.GetString("MsgErrNoSectionPermission")
                ?? "لا تملك صلاحية الوصول إلى هذا القسم.");

        return (true, string.Empty);
    }

    /// <summary>يتطلب دور «مدير النظام». يُستعمل لإدارة المستخدمين والأدوار.</summary>
    public static (bool Allowed, string Message) RequireAdmin(User? user)
    {
        if (user == null)
            return (false, TranslationHelper.GetString("MsgErrNotLoggedIn")
                ?? "لا يمكن تنفيذ العملية: لا يوجد مستخدم مسجَّل الدخول.");

        if (!user.IsAdmin)
            return (false, TranslationHelper.GetString("MsgErrOperationAdminOnly")
                ?? "هذه العملية مقصورة على مدير النظام.");

        return (true, string.Empty);
    }
}
