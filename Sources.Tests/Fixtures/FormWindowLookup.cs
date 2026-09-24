using System.Linq;
using System.Windows;
using Xunit;

namespace Sources.Tests.Fixtures;

/// <summary>
/// يحدد نافذة النموذج المفتوحة فعلياً بالضبط بدل الالتقاط الأول عبر
/// Application.Current.Windows.OfType&lt;T&gt;().FirstOrDefault() (الجولة 199 — R199-B-fix2):
/// الالتقاط الأول كان يمكن أن يصيب نافذة أخرى من نفس النوع باقية على خيط STA المشترك من اختبار
/// سابق (سبب تعليق متقطع فعلي وُثِّق في R199-B-fix)، حتى لو أُغلقت كل نافذة صراحة الآن. التطابق
/// على المالك (Owner، الذي تضبطه كل شاشة صراحة على Window.GetWindow(this)) + الظهور (IsVisible)
/// يضمن أن الاختبار يمسك نافذته هو بالتحديد لا نافذة عابرة، ويفشل بوضوح (Assert.Single) إن وُجد
/// أكثر من واحدة بدل التقاط أيّها ضمناً.
/// </summary>
public static class FormWindowLookup
{
    public static T FindOpenFormOwnedBy<T>(Window host) where T : Window
    {
        var matches = Application.Current.Windows.OfType<T>()
            .Where(w => ReferenceEquals(w.Owner, host) && w.IsVisible)
            .ToList();
        Assert.Single(matches);
        return matches[0];
    }
}
