using System;
using System.Text;
using System.Text.RegularExpressions;

namespace Sources.Helpers;

public static class TextNormalizer
{
    private static readonly Regex ArabicDiacriticsAndTatweelRegex = new(@"[\u064B-\u065F\u0670\u0640]", RegexOptions.Compiled);

    /// <summary>
    /// تطبيع النصوص للبحث المرن:
    /// - توحيد الهمزات (أ، إ، آ، ٱ -> ا)
    /// - توحيد الياء والألف المقصورة (ى -> ي)
    /// - توحيد التاء المربوطة والهاء (ة -> ه)
    /// - إزالة التشكيل وحركات الإعراب والتطويل (ـ)
    /// - تحويل الأحرف الإنجليزية إلى lowercase
    /// </summary>
    public static string Normalize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;

        var text = input.Trim().ToLowerInvariant();
        text = ArabicDiacriticsAndTatweelRegex.Replace(text, "");

        var sb = new StringBuilder(text.Length);
        foreach (char c in text)
        {
            switch (c)
            {
                case 'أ':
                case 'إ':
                case 'آ':
                case 'ٱ':
                    sb.Append('ا');
                    break;
                case 'ى':
                    sb.Append('ي');
                    break;
                case 'ة':
                    sb.Append('ه');
                    break;
                default:
                    sb.Append(c);
                    break;
            }
        }
        return sb.ToString();
    }

    /// <summary>
    /// التحقق مما إذا كان النص المصدر يحتوي على نص البحث بعد تطبيع الاثنين
    /// </summary>
    public static bool ContainsNormalized(string? source, string? normalizedQuery)
    {
        if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(normalizedQuery))
            return false;

        var normalizedSource = Normalize(source);
        return normalizedSource.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase);
    }
}
