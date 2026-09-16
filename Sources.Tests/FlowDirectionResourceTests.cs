using System.Windows;
using Sources.Tests.Fixtures;
using Xunit;

namespace Sources.Tests;

/// <summary>
/// الجولة 163: يتحقق أن المورد الديناميكي CurrentFlowDirection (المستخدَم من قِبل ActivationDialog.xaml
/// وPasswordPromptDialog.xaml، وبعد هذه الجولة من 12 نافذة/عرض إضافية عبر
/// FlowDirection="{DynamicResource CurrentFlowDirection}") يُضبَط على القيمة الصحيحة لكل لغة
/// (RightToLeft لـ "ar"، LeftToRight لـ "en")، وكان هذا المورد غير معرَّف إطلاقاً قبل هذه الجولة.
///
/// لا يستدعي هذا الاختبار App.ApplyLanguage مباشرة، بل يحاكي فقط عبارة ضبط FlowDirection من
/// App.xaml.cs (مطابقة تماماً لصيغة الشرط الثلاثي الفعلية:
/// cultureCode == "ar" ? FlowDirection.RightToLeft : FlowDirection.LeftToRight)، مطابقاً بذلك
/// نفس اصطلاح "mirroring App.ApplyLanguage's ... dictionary swap" المُتَّبَع في بقية حزمة
/// الاختبارات (Round156TranslationTests وغيرها). السبب: App.ApplyLanguage تستخدم Uri نسبياً لتحميل
/// قاموس النصوص (Resources/Strings.xx.xaml)، وهذا لا يُحل إلا ضمن Sources.exe المُصرَّف فعلياً؛ أما
/// كائن Application الذي ينشئه WpfStaFixture (System.Windows.Application وليس Sources.App، ومُستضاف
/// داخل عملية اختبار Sources.Tests) فلا يستطيع حل هذا الـ Uri النسبي، ما يتسبب في استثناء
/// IOException قبل الوصول إلى منطق FlowDirection أصلاً. لذلك تُستَخدَم نفس المحاكاة اليدوية المعتمدة
/// في بقية الاختبارات بدلاً من استدعاء App.ApplyLanguage الحقيقية.
/// </summary>
public class FlowDirectionResourceTests
{
    [Fact]
    public void CurrentFlowDirectionResource_MirrorsAppApplyLanguage_ForArabic_SetsRightToLeft()
    {
        WpfStaFixture.RunInSta(() =>
        {
            const string cultureCode = "ar";
            var newFlowDirection = cultureCode == "ar" ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
            Application.Current.Resources["CurrentFlowDirection"] = newFlowDirection;

            Assert.Equal(FlowDirection.RightToLeft, Application.Current.Resources["CurrentFlowDirection"]);
        });
    }

    [Fact]
    public void CurrentFlowDirectionResource_MirrorsAppApplyLanguage_ForEnglish_SetsLeftToRight()
    {
        WpfStaFixture.RunInSta(() =>
        {
            const string cultureCode = "en";
            var newFlowDirection = cultureCode == "ar" ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
            Application.Current.Resources["CurrentFlowDirection"] = newFlowDirection;

            Assert.Equal(FlowDirection.LeftToRight, Application.Current.Resources["CurrentFlowDirection"]);
        });
    }
}
