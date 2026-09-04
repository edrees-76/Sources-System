using System.Runtime.CompilerServices;
using Sources.Helpers;

namespace Sources.Tests;

internal static class TestModuleInitializer
{
    [ModuleInitializer]
    internal static void EnsureDialogTestMode()
    {
        // مصدر الحقيقة الوحيد لوضع الاختبار: يُثبَّت مرة عند تحميل وحدة تجميعة الاختبار،
        // قبل أي fixture أو اختبار، فلا يُطفأ العلم ولا يُسرَّب بين الفئات المتسلسلة.
        DialogHelper.IsTestMode = true;
    }
}
