using System;
using Xunit;

namespace Sources.Tests;

public class SingleInstanceMutexTests : IDisposable
{
    public void Dispose()
    {
        // لا شيء لتنظيفه — الاختبار لا يُغيّر حالة مشتركة.
    }

    [Fact]
    public void GetAlreadyRunningMessage_WhenResourceLookupReturnsNull_ReturnsArabicFallback()
    {
        // نحقن دالة بحث تعيد null لمحاكاة تعذّر إيجاد المورد، بدلاً من الاعتماد على
        // System.Windows.Application.Current الذي قد لا يكون فارغًا حسب ترتيب تنفيذ
        // الاختبارات الأخرى داخل نفس عملية xunit (WpfStaFixture تُنشئ Application حقيقيًا
        // ولا تُعيده إلى null). هذا يجعل الاختبار حتميًا بغضّ النظر عن ترتيب التنفيذ.
        var message = Sources.App.GetAlreadyRunningMessage(_ => null);

        Assert.Equal("المنظومة تعمل بالفعل على هذا الجهاز.", message);
    }
}
