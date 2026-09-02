using System.Globalization;

namespace Sources.Helpers;

/// <summary>
/// تحويل نصي إلى double يرفض القيم غير المنتهية.
/// السبب: double.TryParse يقبل "NaN" و "Infinity"، و NaN <= 0 تساوي false في IEEE 754،
/// فتعبر حُرّاس التحقق وتُحفظ في القاعدة وتدخل حسابات النشاط ومعدل الجرعة.
/// </summary>
public static class NumericInputParser
{
    /// <summary>يُرجع true فقط لعدد حقيقي منتهٍ. يجرّب الثقافة الثابتة ثم الحالية.</summary>
    public static bool TryParseFinite(string? input, out double result)
    {
        result = 0;
        if (string.IsNullOrWhiteSpace(input)) return false;

        var trimmed = input.Trim();

        if (double.TryParse(trimmed, NumberStyles.Float | NumberStyles.AllowThousands,
                CultureInfo.InvariantCulture, out var invariantValue)
            && double.IsFinite(invariantValue))
        {
            result = invariantValue;
            return true;
        }

        if (double.TryParse(trimmed, NumberStyles.Float | NumberStyles.AllowThousands,
                CultureInfo.CurrentCulture, out var currentValue)
            && double.IsFinite(currentValue))
        {
            result = currentValue;
            return true;
        }

        return false;
    }
}
