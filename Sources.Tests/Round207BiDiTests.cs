using System;
using Sources.Helpers;
using Xunit;

namespace Sources.Tests;

public class Round207BiDiTests
{
    [Fact]
    public void BiDiKeys_ContainLrmBeforeOpeningParentheses_InArabic()
    {
        Fixtures.WpfStaFixture.RunInSta(() =>
        {
            var keysAndExpected = new (string Key, string ExpectedArabic)[]
            {
                ("HeaderEmissionRate", "معدل الانبعاث المُعاير\u200e (n/s)"),
                ("ColDoseRate", "معدل الجرعة\u200e (1م)"),
                ("ColEnergyKeV", "الطاقة\u200e (keV)"),
                ("ColGammaConstant", "ثابت غاما\u200e (Γ)"),
                ("ColAverageEnergyMev", "متوسط الطاقة\u200e (MeV)")
            };

            foreach (var (key, expectedArabic) in keysAndExpected)
            {
                var val = TranslationHelper.GetString(key);
                Assert.NotNull(val);
                Assert.Equal(expectedArabic, val);
                Assert.Contains("\u200e(", val);
            }
        });
    }

    [Fact]
    public void BiDiKeys_ExistAndAreValid_InEnglish()
    {
        Fixtures.WpfStaFixture.RunInSta(() =>
        {
            var dicts = System.Windows.Application.Current.Resources.MergedDictionaries;
            var arabicDictIndex = -1;
            for (int i = 0; i < dicts.Count; i++)
            {
                var src = dicts[i].Source?.OriginalString;
                if (src != null && src.Contains("Strings.ar.xaml"))
                {
                    arabicDictIndex = i;
                    break;
                }
            }
            Assert.True(arabicDictIndex >= 0);

            try
            {
                dicts[arabicDictIndex] = new System.Windows.ResourceDictionary
                {
                    Source = new Uri("pack://application:,,,/Sources;component/Resources/Strings.en.xaml", UriKind.Absolute)
                };

                var keys = new[] { "HeaderEmissionRate", "ColDoseRate", "ColEnergyKeV", "ColGammaConstant", "ColAverageEnergyMev" };
                foreach (var key in keys)
                {
                    var val = TranslationHelper.GetString(key);
                    Assert.NotNull(val);
                    Assert.NotEmpty(val);
                }
            }
            finally
            {
                dicts[arabicDictIndex] = new System.Windows.ResourceDictionary
                {
                    Source = new Uri("pack://application:,,,/Sources;component/Resources/Strings.ar.xaml", UriKind.Absolute)
                };
            }
        });
    }
}
