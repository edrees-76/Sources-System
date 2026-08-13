using System.Collections.Generic;

namespace Sources.Helpers;

public static class IsotopeHelper
{
    private static readonly Dictionary<string, string> ElementArabicNames = new(System.StringComparer.OrdinalIgnoreCase)
    {
        { "H", "هيدروجين" }, { "He", "هيليوم" }, { "Li", "ليثيوم" }, { "Be", "بيريليوم" },
        { "B", "بورون" }, { "C", "كربون" }, { "N", "نيتروجين" }, { "O", "أكسجين" },
        { "F", "فلور" }, { "Ne", "نيون" }, { "Na", "صوديوم" }, { "Mg", "ماغنسيوم" },
        { "Al", "ألومنيوم" }, { "Si", "سيليكون" }, { "P", "فوسفور" }, { "S", "كبريت" },
        { "Cl", "كلور" }, { "Ar", "أرجون" }, { "K", "بوتاسيوم" }, { "Ca", "كالسيوم" },
        { "Sc", "سكانديوم" }, { "Ti", "تيتانيوم" }, { "V", "فاناديوم" }, { "Cr", "كروم" },
        { "Mn", "منغنيز" }, { "Fe", "حديد" }, { "Co", "كوبالت" }, { "Ni", "نيكل" },
        { "Cu", "نحاس" }, { "Zn", "زنك" }, { "Ga", "غاليوم" }, { "Ge", "جرمانيوم" },
        { "As", "زرنيخ" }, { "Se", "سيلينيوم" }, { "Br", "بروم" }, { "Kr", "كريبتون" },
        { "Rb", "روبيديوم" }, { "Sr", "سترونشيوم" }, { "Y", "يتريوم" }, { "Zr", "زركونيوم" },
        { "Nb", "نيوبيوم" }, { "Mo", "موليبدينوم" }, { "Tc", "تكنشيوم" }, { "Ru", "روثينيوم" },
        { "Rh", "روديوم" }, { "Pd", "بالاديوم" }, { "Ag", "فضة" }, { "Cd", "كادميوم" },
        { "In", "إنديوم" }, { "Sn", "قصدير" }, { "Sb", "أنتيمون" }, { "Te", "تيلوريوم" },
        { "I", "يود" }, { "Xe", "زينون" }, { "Cs", "سيزيوم" }, { "Ba", "باريوم" },
        { "La", "لانثانوم" }, { "Ce", "سيريوم" }, { "Pr", "براسيوديميوم" }, { "Nd", "نيوديميوم" },
        { "Pm", "بروميثيوم" }, { "Sm", "ساماريوم" }, { "Eu", "أوروبيوم" }, { "Gd", "جادولينيوم" },
        { "Tb", "تيربيوم" }, { "Dy", "ديسبروزيوم" }, { "Ho", "هولميوم" }, { "Er", "إربيوم" },
        { "Tm", "ثوليوم" }, { "Yb", "إيتربيوم" }, { "Lu", "لوتيشيوم" }, { "Hf", "هافنيوم" },
        { "Ta", "تانتالوم" }, { "W", "تنجستن" }, { "Re", "رينيوم" }, { "Os", "أوسميوم" },
        { "Ir", "إيريديوم" }, { "Pt", "بلاتين" }, { "Au", "ذهب" }, { "Hg", "زئبق" },
        { "Tl", "ثاليوم" }, { "Pb", "رصاص" }, { "Bi", "بزموت" }, { "Po", "بولونيوم" },
        { "At", "أستاتين" }, { "Rn", "رادون" }, { "Fr", "فرانسيوم" }, { "Ra", "راديوم" },
        { "Ac", "أكتينيوم" }, { "Th", "ثوريوم" }, { "Pa", "بروتاكتينيوم" }, { "U", "يورانيوم" },
        { "Np", "نبتونيوم" }, { "Pu", "بلوتونيوم" }, { "Am", "أمريشيوم" }, { "Cm", "كوريوم" },
        { "Bk", "بيركليوم" }, { "Cf", "كاليفورنيوم" }, { "Es", "أينشتاينيوم" }, { "Fm", "فيرميوم" },
        { "Md", "مندليفيوم" }, { "No", "نوبليوم" }, { "Lr", "لورنسيوم" }
    };

    public static string GetArabicNameFromSymbol(string fullSymbol)
    {
        if (string.IsNullOrWhiteSpace(fullSymbol)) return string.Empty;

        var parts = fullSymbol.Trim().Split(new[] { '-', ' ', '/' }, System.StringSplitOptions.RemoveEmptyEntries);
        
        string element = string.Empty;
        string massNumber = string.Empty;

        if (parts.Length >= 1)
        {
            element = parts[0].Trim();
            massNumber = parts.Length > 1 ? parts[1].Trim() : "";

            if (ElementArabicNames.TryGetValue(element, out var arabicName))
            {
                return string.IsNullOrEmpty(massNumber) ? arabicName : $"{massNumber}-{arabicName}";
            }
        }
        
        var match = System.Text.RegularExpressions.Regex.Match(fullSymbol.Trim(), @"^([a-zA-Z]+)(.*)$");
        if (match.Success)
        {
            element = match.Groups[1].Value.Trim();
            massNumber = match.Groups[2].Value.Trim().TrimStart('-', ' ', '/');
            if (ElementArabicNames.TryGetValue(element, out var arabicName))
            {
                return string.IsNullOrEmpty(massNumber) ? arabicName : $"{massNumber}-{arabicName}";
            }
        }

        return fullSymbol;
    }
}
