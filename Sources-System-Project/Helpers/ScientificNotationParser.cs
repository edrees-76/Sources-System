using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Sources.Helpers;

/// <summary>
/// فئة مساعدة لتفسير الأرقام بالصيغة العشرية العادية أو الصيغ العلمية المتعددة
/// (مثل 1.1E7, 1.1e+7, 1.1x10^7, 1.1×10^7, 1.1×10⁷, 11000000, 11,000,000)
/// </summary>
public static class ScientificNotationParser
{
    private static readonly Dictionary<char, char> SuperscriptDigits = new()
    {
        ['⁰'] = '0', ['¹'] = '1', ['²'] = '2', ['³'] = '3', ['⁴'] = '4',
        ['⁵'] = '5', ['⁶'] = '6', ['⁷'] = '7', ['⁸'] = '8', ['⁹'] = '9',
        ['⁺'] = '+', ['⁻'] = '-'
    };

    private static readonly Dictionary<char, char> EasternArabicDigits = new()
    {
        ['٠'] = '0', ['١'] = '1', ['٢'] = '2', ['٣'] = '3', ['٤'] = '4',
        ['٥'] = '5', ['٦'] = '6', ['٧'] = '7', ['٨'] = '8', ['٩'] = '9'
    };

    /// <summary>
    /// يحاول تحويل النص المدخل إلى رقم double يدعم كافة الصيغ العلمية والعادية
    /// </summary>
    public static bool TryParse(string? input, out double result)
    {
        result = 0;
        if (string.IsNullOrWhiteSpace(input)) return false;

        string normalized = Normalize(input);
        if (string.IsNullOrWhiteSpace(normalized)) return false;

        // 1. محاولة التحويل المباشر القياسي (يدعم 1.1E7, 1.1e+7, 1.1E-3, 11000000)
        if (double.TryParse(normalized, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out double directVal) 
            && !double.IsNaN(directVal) && !double.IsInfinity(directVal))
        {
            result = directVal;
            return true;
        }

        // 2. محاولة التحويل عبر regex لصيغ الضرب: base × 10^exp أو 10^exp
        // تدعم: 1.1x10^7, 1.1X10^7, 1.1×10^7, 1.1*10^7, 10^7, 10^-3, 1.1x10+7
        var match = Regex.Match(normalized, @"^(?:(?<base>[-+]?[0-9]+(?:\.[0-9]+)?)\s*(?:[xX\*×]|\\times|\*)\s*)?10(?:\^|\s*)(?<exp>[-+]?[0-9]+)$", RegexOptions.IgnoreCase);
        if (match.Success)
        {
            double baseVal = 1.0;
            if (match.Groups["base"].Success && !string.IsNullOrWhiteSpace(match.Groups["base"].Value))
            {
                if (!double.TryParse(match.Groups["base"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out baseVal))
                {
                    return false;
                }
            }

            if (int.TryParse(match.Groups["exp"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int expVal))
            {
                if (expVal > 308 || expVal < -308) return false;
                result = baseVal * Math.Pow(10, expVal);
                return !double.IsNaN(result) && !double.IsInfinity(result);
            }
        }

        // 3. صيغة الأس العام مثل base^exp
        var matchPow = Regex.Match(normalized, @"^(?<base>[-+]?[0-9]+(?:\.[0-9]+)?)\s*\^\s*(?<exp>[-+]?[0-9]+)$");
        if (matchPow.Success)
        {
            if (double.TryParse(matchPow.Groups["base"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double b) &&
                double.TryParse(matchPow.Groups["exp"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double e))
            {
                result = Math.Pow(b, e);
                return !double.IsNaN(result) && !double.IsInfinity(result);
            }
        }

        // 4. محاولة أخيرة عبر الثقافة الحالية
        if (double.TryParse(normalized, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.CurrentCulture, out double currentCultureVal)
            && !double.IsNaN(currentCultureVal) && !double.IsInfinity(currentCultureVal))
        {
            result = currentCultureVal;
            return true;
        }

        return false;
    }

    /// <summary>
    /// يحاول تحويل النص المدخل ويتأكد أن القيمة الناتجة موجبة أكبر من صفر
    /// </summary>
    public static bool TryParsePositive(string? input, out double result, out string? errorMessage)
    {
        result = 0;
        errorMessage = null;

        if (string.IsNullOrWhiteSpace(input))
        {
            errorMessage = TranslationHelper.GetString("MsgErrFieldReq") ?? "هذا الحقل مطلوب";
            return false;
        }

        if (!TryParse(input, out result))
        {
            errorMessage = TranslationHelper.GetString("MsgErrInvalidScientificNumber") 
                ?? "صيغة الرقم غير صحيحة. الصيغ المقبولة: أرقام عادية مثل 11000000، أو بصيغة الأس مثل 1.1E7 أو 1.1x10^7";
            return false;
        }

        if (result <= 0)
        {
            errorMessage = TranslationHelper.GetString("MsgErrPositiveNumberRequired") 
                ?? "القيمة المدخلة يجب أن تكون رقماً موجباً أكبر من صفر";
            return false;
        }

        return true;
    }

    /// <summary>
    /// تسوية وتوحيد النص الرقمي (تحويل الأرقام المشرقية، فك الأسس المرتفعة، تنظيف الفواصل والمسافات)
    /// </summary>
    private static string Normalize(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;

        var sb = new StringBuilder(input.Length + 4);
        bool inSuperscript = false;

        for (int i = 0; i < input.Length; i++)
        {
            char c = input[i];

            // 1. معالجة الأرقام المشرقية
            if (EasternArabicDigits.TryGetValue(c, out char westernDigit))
            {
                sb.Append(westernDigit);
                inSuperscript = false;
                continue;
            }

            // 2. معالجة الأسس المرتفعة (Superscripts مثل ⁷ أو ⁻³)
            if (SuperscriptDigits.TryGetValue(c, out char supDigit))
            {
                if (!inSuperscript)
                {
                    // أضف علامة الأس إذا لم تكن موجودة قبل أول رقم مرتفع
                    if (sb.Length > 0 && sb[sb.Length - 1] != '^')
                    {
                        sb.Append('^');
                    }
                    inSuperscript = true;
                }
                sb.Append(supDigit);
                continue;
            }

            inSuperscript = false;

            // 3. معالجة الفواصل العربية والإنجليزية
            if (c == '٫') // فاصلة عشرية عربية
            {
                sb.Append('.');
                continue;
            }
            if (c == '٬') // فاصلة آلاف عربية
            {
                continue;
            }

            sb.Append(c);
        }

        string str = sb.ToString().Trim();

        // إزالة المسافات
        str = str.Replace(" ", "");

        // معالجة فواصل الآلاف الإنجليزية مثل 11,000,000 أو 1,234.56
        if (str.Contains(',') && str.Contains('.'))
        {
            // الفاصلة هنا فاصلة آلاف
            str = str.Replace(",", "");
        }
        else if (str.Count(ch => ch == ',') > 1) // أكثر من فاصلة واحدة مثل 11,000,000
        {
            str = str.Replace(",", "");
        }
        else if (str.Contains(','))
        {
            int commaIdx = str.IndexOf(',');
            // إذا كانت الفاصلة في صيغة علمية مثل 1,1x10^7 أو 1,1E7 -> هي فاصلة عشرية
            if (str.Contains('x', StringComparison.OrdinalIgnoreCase) || 
                str.Contains('e', StringComparison.OrdinalIgnoreCase) || 
                str.Contains('×') || 
                str.Contains('*') ||
                str.Contains('^'))
            {
                str = str.Replace(',', '.');
            }
            else if (str.Length - commaIdx == 4 && commaIdx > 0 && char.IsDigit(str[commaIdx - 1]))
            {
                // مثل 11,000 -> فاصلة آلاف
                str = str.Replace(",", "");
            }
            else
            {
                // مثل 1,5 -> فاصلة عشرية
                str = str.Replace(',', '.');
            }
        }

        return str;
    }
}
