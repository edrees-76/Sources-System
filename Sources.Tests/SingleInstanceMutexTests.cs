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
    public void GetAlreadyRunningMessage_WhenApplicationCurrentIsNull_ReturnsArabicFallback()
    {
        // في بيئة الاختبار Application.Current فارغ (لا واجهة WPF حقيقية تعمل)،
        // لذا TranslationHelper.GetString لا يجد المورد ويجب أن يعود نص الارتداد العربي.
        Assert.Null(System.Windows.Application.Current);

        var message = Sources.App.GetAlreadyRunningMessage();

        Assert.Equal("المنظومة تعمل بالفعل على هذا الجهاز.", message);
    }
}
