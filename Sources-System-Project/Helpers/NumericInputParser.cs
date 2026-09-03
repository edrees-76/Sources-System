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

        if (trimmed.Equals("NaN", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Contains("Infinity", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Contains("∞"))
        {
            return false;
        }

        // إذا احتوى النص على الفاصلة والنقطة معاً (تحديد فاصل الآلاف والفاصل العشري)
        if (trimmed.Contains(',') && trimmed.Contains('.'))
        {
            var lastComma = trimmed.LastIndexOf(',');
            var lastDot = trimmed.LastIndexOf('.');

            if (lastComma < lastDot)
            {
                // مثل 1,000.5 -> الفاصلة للآلاف والنقطة عشرية
                var normalized = trimmed.Replace(",", "");
                if (double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out var val)
                    && double.IsFinite(val))
                {
                    result = val;
                    return true;
                }
            }
            else
            {
                // مثل 1.000,5 -> النقطة للآلاف والفاصلة عشرية
                var normalized = trimmed.Replace(".", "").Replace(',', '.');
                if (double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out var val)
                    && double.IsFinite(val))
                {
                    result = val;
                    return true;
                }
            }

            return false;
        }

        // إذا احتوى النص على فاصلة فقط بدون نقطة
        if (trimmed.Contains(','))
        {
            // نمط فاصل الآلاف الصريح: مجموعات ثلاثية بعد الفاصلة مثل 1,000 أو 1,000,000
            if (System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"^-?\d{1,3}(,\d{3})+$"))
            {
                var withoutThousands = trimmed.Replace(",", "");
                if (double.TryParse(withoutThousands, NumberStyles.Float, CultureInfo.InvariantCulture, out var thousandsVal)
                    && double.IsFinite(thousandsVal))
                {
                    result = thousandsVal;
                    return true;
                }
            }

            // معالجة الفاصلة كفاصل عشري صريح ("1,5" -> "1.5" أو "0,25" -> "0.25")
            var asDecimal = trimmed.Replace(',', '.');
            if (double.TryParse(asDecimal, NumberStyles.Float, CultureInfo.InvariantCulture, out var decimalVal)
                && double.IsFinite(decimalVal))
            {
                result = decimalVal;
                return true;
            }

            return false;
        }

        // الصيغة القياسية بالثقافة الثابتة
        if (double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out var invariantValue)
            && double.IsFinite(invariantValue))
        {
            result = invariantValue;
            return true;
        }

        return false;
    }
}
