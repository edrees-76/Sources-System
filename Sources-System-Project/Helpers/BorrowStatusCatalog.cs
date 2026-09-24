using System;
using System.Collections.Generic;

namespace Sources.Helpers;

/// <summary>
/// أكواد حالة طلب الاستعارة كما هي مخزَّنة فعلياً في العمود BorrowRequest.Status.
/// Unknown تُستخدم للقيم غير المعروفة أو الفارغة.
/// </summary>
public enum BorrowStatusCode
{
    Unknown,
    Pending,
    Approved,
    Rejected,
    Delivered,
    Returned,
    Overdue
}

/// <summary>
/// مصدر واحد للحقيقة لعرض حالة طلب الاستعارة ومنطق تجميعاتها (الجولة 201) — يماثل تصميم
/// StatusCatalog تماماً. لا يغيّر القيم المخزَّنة. يُستخدم في مواقع العرض (DISPLAY) وفي مواقع
/// المنطق (LOGIC) التي تُجمِّع أكثر من حالة (نشطة/قابلة للإرجاع) عبر Set Helpers أدناه؛ الاستعلامات
/// المُترجَمة إلى EF تستمر باستخدام الثوابت inline وليس Parse حفاظاً على نفس SQL المولَّد.
/// </summary>
public static class BorrowStatusCatalog
{
    public const string Pending = "Pending";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
    public const string Delivered = "Delivered";
    public const string Returned = "Returned";
    public const string Overdue = "Overdue";

    private sealed record Entry(string Stored, BorrowStatusCode Code, string ResourceKey, string Arabic, string English, string ColorHex);

    private static readonly Entry[] Entries =
    {
        new(Pending, BorrowStatusCode.Pending, "BorrowStatusDisplayPending", "معلّق", "Pending", "#4F7FA3"),
        new(Approved, BorrowStatusCode.Approved, "BorrowStatusDisplayApproved", "تمت الموافقة", "Approved", "#4F7FA3"),
        new(Rejected, BorrowStatusCode.Rejected, "BorrowStatusDisplayRejected", "مرفوض", "Rejected", "#4F7FA3"),
        new(Delivered, BorrowStatusCode.Delivered, "BorrowStatusDisplayDelivered", "تم التسليم", "Delivered", "#3FAE7A"),
        new(Returned, BorrowStatusCode.Returned, "BorrowStatusDisplayReturned", "تم الإرجاع", "Returned", "#4F7FA3"),
        new(Overdue, BorrowStatusCode.Overdue, "BorrowStatusDisplayOverdue", "متأخر", "Overdue", "#C25B4A"),
    };

    private const string UnknownColorHex = "#9E9E9E";

    private static readonly Dictionary<string, Entry> ByStored = BuildByStored();

    private static Dictionary<string, Entry> BuildByStored()
    {
        var map = new Dictionary<string, Entry>(StringComparer.Ordinal);
        foreach (var entry in Entries)
        {
            map[entry.Stored] = entry;
        }
        return map;
    }

    private static Entry? Find(string? stored)
    {
        if (string.IsNullOrEmpty(stored))
        {
            return null;
        }
        return ByStored.TryGetValue(stored, out var entry) ? entry : null;
    }

    /// <summary>يحوّل القيمة المخزّنة إلى كود؛ لا يرمي أبداً. غير المعروف/الفارغ/null -> Unknown.</summary>
    public static BorrowStatusCode Parse(string? stored) => Find(stored)?.Code ?? BorrowStatusCode.Unknown;

    /// <summary>النص العربي — مطابق حرفياً لما تنتجه BorrowRequest.ArabicStatus الحالية (بما فيها الرجوع للقيمة الخام).</summary>
    public static string GetArabicText(string? stored)
    {
        var entry = Find(stored);
        return entry != null ? entry.Arabic : (stored ?? "");
    }

    /// <summary>النص المعروض حسب لغة الواجهة الحالية؛ الرجوع للنص العربي إذا كان المفتاح غير موجود؛ غير المعروف -> القيمة الخام.</summary>
    public static string GetDisplayText(string? stored, Func<string, string?>? lookup = null)
    {
        var entry = Find(stored);
        if (entry == null)
        {
            return stored ?? "";
        }

        lookup ??= TranslationHelper.GetString;
        var localized = lookup(entry.ResourceKey);
        return string.IsNullOrEmpty(localized) ? entry.Arabic : localized;
    }

    /// <summary>لون الحالة الموحّد (Hex)؛ غير المعروف -> الرمادي الموحَّد.</summary>
    public static string GetColorHex(string? stored)
    {
        var entry = Find(stored);
        return entry?.ColorHex ?? UnknownColorHex;
    }

    // ─── Set Helpers (تجميعات LOGIC الحالية بالضبط — بلا أي تغيير سلوكي) ───

    /// <summary>استعارة نشطة حالياً: Delivered أو Overdue.</summary>
    public static bool IsActiveBorrow(string? stored) => stored == Delivered || stored == Overdue;

    /// <summary>قابل للإرجاع: Delivered أو Approved أو Overdue.</summary>
    public static bool IsReturnable(string? stored) => stored == Delivered || stored == Approved || stored == Overdue;

    /// <summary>يحظر حذف المصدر (معلّق أو نشط): Pending/Approved/Delivered/Overdue.</summary>
    public static bool BlocksSourceDeletion(string? stored) =>
        stored == Pending || stored == Approved || stored == Delivered || stored == Overdue;

    /// <summary>مؤهّل للتحديث الدوري إلى "متأخر": Delivered أو Approved (وتجاوز تاريخ الإرجاع المتوقع).</summary>
    public static bool IsOverdueSweepCandidate(string? stored) => stored == Delivered || stored == Approved;
}
