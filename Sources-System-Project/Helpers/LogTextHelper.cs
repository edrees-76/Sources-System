using System;

namespace Sources.Helpers;

/// <summary>
/// اقتطاع النصوص المُدرَجة في سطور السجل. القيمة المخزَّنة قد تكون فاسدة بأي طول
/// فلا يجوز أن تُغرق السطر. العقد: الناتج لا يتجاوز maxLength محرفاً بأي حال،
/// وعلامة الاقتطاع «…» جزء من هذا الطول لا زيادة عليه.
/// </summary>
public static class LogTextHelper
{
    /// <summary>الطول الأقصى الافتراضي لقيمة تُدرَج في نص تحذير.</summary>
    public const int DefaultMaxLength = 50;

    /// <summary>يُرجع نصاً طوله &lt;= maxLength. لا يرمي إلا على maxLength غير صالح.</summary>
    public static string Truncate(string? value, int maxLength = DefaultMaxLength)
    {
        if (maxLength < 1)
            throw new ArgumentOutOfRangeException(nameof(maxLength), "الطول الأقصى يجب أن يكون 1 على الأقل.");

        if (string.IsNullOrEmpty(value)) return string.Empty;
        if (value.Length <= maxLength) return value;

        var cut = maxLength - 1; // محرف واحد محجوز لعلامة الاقتطاع

        // منع بتر زوج بديل (surrogate pair): بتره يُدخل نصف محرف غير صالح في ملف السجل.
        if (cut > 0 && char.IsHighSurrogate(value[cut - 1])) cut--;

        return value.Substring(0, cut) + "…";
    }
}
