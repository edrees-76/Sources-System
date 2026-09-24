using System;
using System.Collections.Generic;

namespace Sources.Helpers;

/// <summary>
/// أكواد حالة المصدر/المصدر النيوتروني كما هي مخزَّنة فعليًا في العمود Status.
/// Unknown تُستخدم للقيم غير المعروفة أو الفارغة أو القديمة (Active/Decayed/Disposed/Lost).
/// </summary>
public enum SourceStatusCode
{
    Unknown,
    InUse,
    Storage,
    Waste,
    Transfer
}

/// <summary>
/// مصدر واحد للحقيقة لعرض حالة المصدر/المصدر النيوتروني (نص عربي/إنجليزي ولون).
/// لا يغيّر القيم المخزَّنة ولا يُستخدم في أي منطق فلترة/مقارنة/تدقيق — عرض فقط.
/// </summary>
public static class StatusCatalog
{
    private sealed record Entry(string Stored, SourceStatusCode Code, string ResourceKey, string Arabic, string English, string ColorHex);

    private static readonly Entry[] Entries =
    {
        new("InUse", SourceStatusCode.InUse, "StatusDisplayInUse", "قيد الاستخدام", "In Use", "#3FAE7A"),
        new("Storage", SourceStatusCode.Storage, "StatusDisplayStorage", "مخزن", "In Storage", "#4F7FA3"),
        new("Waste", SourceStatusCode.Waste, "StatusDisplayWaste", "نفايات", "Waste", "#E0A93E"),
        new("Transfer", SourceStatusCode.Transfer, "StatusDisplayTransfer", "قيد النقل", "In Transfer", "#E0A93E"),
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

    /// <summary>يحوّل القيمة المخزّنة إلى كود؛ لا يرمي أبدًا. غير المعروف/الفارغ/الفارغ null -> Unknown.</summary>
    public static SourceStatusCode Parse(string? stored)
    {
        return Find(stored)?.Code ?? SourceStatusCode.Unknown;
    }

    /// <summary>النص العربي — مطابق حرفيًا لما تنتجه خاصية ArabicStatus الحالية في النماذج.</summary>
    public static string GetArabicText(string? stored)
    {
        var entry = Find(stored);
        if (entry != null)
        {
            return entry.Arabic;
        }
        return stored ?? "";
    }

    /// <summary>
    /// النص المعروض حسب اللغة الحالية عبر lookup (افتراضيًا TranslationHelper.GetString)، مع الرجوع
    /// إلى النص العربي الدقيق إذا كان المفتاح غير موجود. غير المعروف -> النص الخام كما هو مخزَّن.
    /// </summary>
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

    /// <summary>لون الحالة الموحّد (رمز Hex)؛ غير المعروف -> اللون الرمادي الموحَّد.</summary>
    public static string GetColorHex(string? stored)
    {
        var entry = Find(stored);
        return entry?.ColorHex ?? UnknownColorHex;
    }
}
