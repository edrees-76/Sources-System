using System;
using System.Collections.Generic;
using System.Text;

namespace Sources.Helpers;

/// <summary>
/// معالج تشكيل وتوصيل الحروف العربية وتصحيح اتجاهها لكانفاس SkiaSharp / LiveChartsCore
/// يقوم بربط الحروف العربية بأشكالها السياقية (بداية/وسط/نهاية/منفصلة) وعكس ترتيب الكلمات العربية للعرض السليم
/// </summary>
public static class ArabicReshaper
{
    private struct GlyphForms
    {
        public char Isolated;
        public char Final;
        public char Initial;
        public char Medial;

        public GlyphForms(char isolated, char finalForm, char initial, char medial)
        {
            Isolated = isolated;
            Final = finalForm;
            Initial = initial;
            Medial = medial;
        }

        public GlyphForms(char isolated, char finalForm)
        {
            Isolated = isolated;
            Final = finalForm;
            Initial = isolated;
            Medial = finalForm;
        }
    }

    // جدول الحروف العربية الأساسية وأشكالها في Unicode Presentation Forms-B
    private static readonly Dictionary<char, GlyphForms> ArabicMap = new()
    {
        { 'ء', new GlyphForms('\uFE80', '\uFE80') },
        { 'آ', new GlyphForms('\uFE81', '\uFE82') },
        { 'أ', new GlyphForms('\uFE83', '\uFE84') },
        { 'ؤ', new GlyphForms('\uFE85', '\uFE86') },
        { 'إ', new GlyphForms('\uFE87', '\uFE88') },
        { 'ئ', new GlyphForms('\uFE89', '\uFE8A', '\uFE8B', '\uFE8C') },
        { 'ا', new GlyphForms('\uFE8D', '\uFE8E') },
        { 'ب', new GlyphForms('\uFE8F', '\uFE90', '\uFE91', '\uFE92') },
        { 'ة', new GlyphForms('\uFE93', '\uFE94') },
        { 'ت', new GlyphForms('\uFE95', '\uFE96', '\uFE97', '\uFE98') },
        { 'ث', new GlyphForms('\uFE99', '\uFE9A', '\uFE9B', '\uFE9C') },
        { 'ج', new GlyphForms('\uFE9D', '\uFE9E', '\uFE9F', '\uFEA0') },
        { 'ح', new GlyphForms('\uFEA1', '\uFEA2', '\uFEA3', '\uFEA4') },
        { 'خ', new GlyphForms('\uFEA5', '\uFEA6', '\uFEA7', '\uFEA8') },
        { 'د', new GlyphForms('\uFEA9', '\uFEAA') },
        { 'ذ', new GlyphForms('\uFEAB', '\uFEAC') },
        { 'ر', new GlyphForms('\uFEAD', '\uFEAE') },
        { 'ز', new GlyphForms('\uFEAF', '\uFEB0') },
        { 'س', new GlyphForms('\uFEB1', '\uFEB2', '\uFEB3', '\uFEB4') },
        { 'ش', new GlyphForms('\uFEB5', '\uFEB6', '\uFEB7', '\uFEB8') },
        { 'ص', new GlyphForms('\uFEB9', '\uFEBA', '\uFEBB', '\uFEBC') },
        { 'ض', new GlyphForms('\uFEBD', '\uFEBE', '\uFEBF', '\uFEC0') },
        { 'ط', new GlyphForms('\uFEC1', '\uFEC2', '\uFEC3', '\uFEC4') },
        { 'ظ', new GlyphForms('\uFEC5', '\uFEC6', '\uFEC7', '\uFEC8') },
        { 'ع', new GlyphForms('\uFEC9', '\uFECA', '\uFECB', '\uFECC') },
        { 'غ', new GlyphForms('\uFECD', '\uFECE', '\uFECF', '\uFED0') },
        { 'ف', new GlyphForms('\uFED1', '\uFED2', '\uFED3', '\uFED4') },
        { 'ق', new GlyphForms('\uFED5', '\uFED6', '\uFED7', '\uFED8') },
        { 'ك', new GlyphForms('\uFED9', '\uFEDA', '\uFEDB', '\uFEDC') },
        { 'ل', new GlyphForms('\uFEDD', '\uFEDE', '\uFEDF', '\uFEE0') },
        { 'م', new GlyphForms('\uFEE1', '\uFEE2', '\uFEE3', '\uFEE4') },
        { 'ن', new GlyphForms('\uFEE5', '\uFEE6', '\uFEE7', '\uFEE8') },
        { 'ه', new GlyphForms('\uFEE9', '\uFEEA', '\uFEEB', '\uFEEC') },
        { 'و', new GlyphForms('\uFEED', '\uFEEE') },
        { 'ى', new GlyphForms('\uFEEF', '\uFEF0') },
        { 'ي', new GlyphForms('\uFEF1', '\uFEF2', '\uFEF3', '\uFEF4') }
    };

    // حروف لا تتصل بما بعدها (فقط بما قبلها)
    private static readonly HashSet<char> NonNextConnectors = new()
    {
        'ء', 'آ', 'أ', 'ؤ', 'إ', 'ا', 'د', 'ذ', 'ر', 'ز', 'و', 'ى', 'ة'
    };

    public static bool IsArabicChar(char c)
    {
        return (c >= 0x0600 && c <= 0x06FF) || (c >= 0xFB50 && c <= 0xFDFF) || (c >= 0xFE70 && c <= 0xFEFC);
    }

    public static bool ContainsArabic(string? text)
    {
        if (string.IsNullOrEmpty(text)) return false;
        foreach (char c in text)
        {
            if (IsArabicChar(c)) return true;
        }
        return false;
    }

    /// <summary>
    /// تشكيل النص العربي وعكسه للعرض في SkiaSharp
    /// </summary>
    public static string ReshapeAndReverse(string? input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;

        // تنظيف محارف التوجيه القديمة LRE / PDF إن وجدت
        var cleanInput = input.Replace("\u202A", "").Replace("\u202C", "").Replace("\u200E", "").Replace("\u200F", "");

        if (!ContainsArabic(cleanInput))
            return cleanInput;

        var lines = cleanInput.Split('\n');
        var reshapedLines = new List<string>();

        foreach (var line in lines)
        {
            reshapedLines.Add(ReshapeLine(line));
        }

        return string.Join('\n', reshapedLines);
    }

    private static string ReshapeLine(string line)
    {
        if (string.IsNullOrEmpty(line)) return string.Empty;

        // تقسيم السطر إلى مقاطع عربية وغير عربية (BiDi Tokenizer)
        var tokens = new List<(string text, bool isArabic)>();
        var currentToken = new StringBuilder();
        bool? currentIsArabic = null;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            bool isAr = IsArabicChar(c);

            if (currentIsArabic == null)
            {
                currentIsArabic = isAr;
                currentToken.Append(c);
            }
            else if (currentIsArabic.Value == isAr)
            {
                currentToken.Append(c);
            }
            else
            {
                tokens.Add((currentToken.ToString(), currentIsArabic.Value));
                currentToken.Clear();
                currentIsArabic = isAr;
                currentToken.Append(c);
            }
        }

        if (currentToken.Length > 0 && currentIsArabic.HasValue)
        {
            tokens.Add((currentToken.ToString(), currentIsArabic.Value));
        }

        var result = new StringBuilder();
        for (int i = tokens.Count - 1; i >= 0; i--)
        {
            var token = tokens[i];
            if (token.isArabic)
            {
                string shaped = ReshapeArabicWord(token.text);
                var charArray = shaped.ToCharArray();
                Array.Reverse(charArray);
                result.Append(new string(charArray));
            }
            else
            {
                result.Append(token.text);
            }
        }

        return result.ToString();
    }

    private static string ReshapeArabicWord(string word)
    {
        if (string.IsNullOrEmpty(word)) return string.Empty;

        var chars = word.ToCharArray();
        var result = new StringBuilder();

        for (int i = 0; i < chars.Length; i++)
        {
            char c = chars[i];

            // فحص لام-ألف (Lam-Alef Ligatures)
            if (c == 'ل' && i + 1 < chars.Length)
            {
                char next = chars[i + 1];
                char? lamAlef = next switch
                {
                    'آ' => (i > 0 && ConnectsWithPrevious(chars[i - 1])) ? '\uFEF6' : '\uFEF5',
                    'أ' => (i > 0 && ConnectsWithPrevious(chars[i - 1])) ? '\uFEF8' : '\uFEF7',
                    'إ' => (i > 0 && ConnectsWithPrevious(chars[i - 1])) ? '\uFEFA' : '\uFEF9',
                    'ا' => (i > 0 && ConnectsWithPrevious(chars[i - 1])) ? '\uFEFC' : '\uFEFB',
                    _ => null
                };

                if (lamAlef.HasValue)
                {
                    result.Append(lamAlef.Value);
                    i++; // تجاوز حرف الألف
                    continue;
                }
            }

            if (!ArabicMap.TryGetValue(c, out var forms))
            {
                result.Append(c);
                continue;
            }

            bool prevConnects = i > 0 && ConnectsWithPrevious(chars[i - 1]);
            bool nextConnects = i + 1 < chars.Length && ConnectsWithNext(chars[i + 1]) && !NonNextConnectors.Contains(c);

            if (prevConnects && nextConnects)
            {
                result.Append(forms.Medial);
            }
            else if (prevConnects)
            {
                result.Append(forms.Final);
            }
            else if (nextConnects)
            {
                result.Append(forms.Initial);
            }
            else
            {
                result.Append(forms.Isolated);
            }
        }

        return result.ToString();
    }

    private static bool ConnectsWithPrevious(char c)
    {
        return ArabicMap.ContainsKey(c) && !NonNextConnectors.Contains(c);
    }

    private static bool ConnectsWithNext(char c)
    {
        return ArabicMap.ContainsKey(c);
    }
}
