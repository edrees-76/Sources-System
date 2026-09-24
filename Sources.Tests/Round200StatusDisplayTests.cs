using System;
using Sources.Converters;
using Sources.Helpers;
using Sources.Models;
using Sources.ViewModels;
using Xunit;

namespace Sources.Tests;

/// <summary>
/// الجولة 200: يتحقق أن مواقع العرض الموجَّهة عبر StatusCatalog تُظهر النص الإنجليزي في الواجهة
/// الإنجليزية والنص العربي الدقيق في الواجهة العربية، مع بقاء ArabicStatus عربياً دائماً
/// (قيد تدقيق/منطق ثابت) بغض النظر عن لغة الواجهة النشطة.
/// </summary>
public class Round200StatusDisplayTests
{
    private static void WithEnglishDictionary(Action assertions)
    {
        Sources.Tests.Fixtures.WpfStaFixture.RunInSta(() =>
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
            Assert.True(arabicDictIndex >= 0, "Strings.ar.xaml dictionary must already be loaded by WpfStaFixture.");

            try
            {
                dicts[arabicDictIndex] = new System.Windows.ResourceDictionary
                {
                    Source = new Uri("pack://application:,,,/Sources;component/Resources/Strings.en.xaml", UriKind.Absolute)
                };

                assertions();
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

    private static void WithArabicDictionary(Action assertions)
    {
        // القاموس العربي محمَّل افتراضياً بواسطة WpfStaFixture.
        Sources.Tests.Fixtures.WpfStaFixture.RunInSta(assertions);
    }

    // ─── قائمة المصادر النيترونية (NeutronSourceListRow يمرر مباشرة من NeutronSource.StatusDisplay) ───

    [Fact]
    public void NeutronSourceListRow_StatusDisplay_IsEnglish_WhenEnglishLanguageActive()
    {
        WithEnglishDictionary(() =>
        {
            var neutron = new NeutronSource { Status = "Storage" };
            var row = new NeutronSourceListRow { NeutronSource = neutron };

            Assert.Equal("In Storage", row.StatusDisplay);
            Assert.Equal("مخزن", row.ArabicStatus); // يبقى عربياً دائماً (قيد منطقي/تدقيقي)
        });
    }

    [Fact]
    public void NeutronSourceListRow_StatusDisplay_IsArabic_WhenArabicLanguageActive()
    {
        WithArabicDictionary(() =>
        {
            var neutron = new NeutronSource { Status = "Storage" };
            var row = new NeutronSourceListRow { NeutronSource = neutron };

            Assert.Equal("مخزن", row.StatusDisplay);
            Assert.Equal("مخزن", row.ArabicStatus);
        });
    }

    // ─── تفاصيل المصدر النيوتروني ───

    [Fact]
    public void NeutronSourceDetailsViewModel_StatusDisplay_IsEnglish_WhenEnglishLanguageActive()
    {
        WithEnglishDictionary(() =>
        {
            var neutron = new NeutronSource { Status = "Waste" };
            var vm = new NeutronSourceDetailsViewModel(neutron);

            Assert.Equal("Waste", vm.StatusDisplay);
            Assert.Equal("نفايات", vm.StatusArabic);
        });
    }

    [Fact]
    public void NeutronSourceDetailsViewModel_StatusDisplay_IsArabic_WhenArabicLanguageActive()
    {
        WithArabicDictionary(() =>
        {
            var neutron = new NeutronSource { Status = "Waste" };
            var vm = new NeutronSourceDetailsViewModel(neutron);

            Assert.Equal("نفايات", vm.StatusDisplay);
            Assert.Equal("نفايات", vm.StatusArabic);
        });
    }

    // ─── قائمة المصادر (تربط XAML مباشرة بخاصية Source.StatusDisplay) ───

    [Fact]
    public void Source_StatusDisplay_IsEnglish_WhenEnglishLanguageActive()
    {
        WithEnglishDictionary(() =>
        {
            var source = new Source { Status = "InUse" };

            Assert.Equal("In Use", source.StatusDisplay);
            Assert.Equal("قيد الاستخدام", source.ArabicStatus);
        });
    }

    [Fact]
    public void Source_StatusDisplay_IsArabic_WhenArabicLanguageActive()
    {
        WithArabicDictionary(() =>
        {
            var source = new Source { Status = "InUse" };

            Assert.Equal("قيد الاستخدام", source.StatusDisplay);
            Assert.Equal("قيد الاستخدام", source.ArabicStatus);
        });
    }

    // ─── قائمة فلتر الحالة في لوحة التحكم (المحوّل الموحَّد StatusToArabicConverter) ───

    [Theory]
    [InlineData("InUse", "In Use")]
    [InlineData("Storage", "In Storage")]
    [InlineData("Waste", "Waste")]
    [InlineData("Transfer", "In Transfer")]
    [InlineData("", "")]
    public void DashboardStatusFilter_ConverterOutput_IsEnglish_WhenEnglishLanguageActive(string status, string expectedEnglish)
    {
        WithEnglishDictionary(() =>
        {
            var converter = new StatusToArabicConverter();
            var result = converter.Convert(status, typeof(string), null!, System.Globalization.CultureInfo.InvariantCulture);
            Assert.Equal(expectedEnglish, result);
        });
    }

    [Theory]
    [InlineData("InUse", "قيد الاستخدام")]
    [InlineData("Storage", "مخزن")]
    [InlineData("Waste", "نفايات")]
    [InlineData("Transfer", "قيد النقل")]
    [InlineData("", "")]
    public void DashboardStatusFilter_ConverterOutput_IsArabic_WhenArabicLanguageActive(string status, string expectedArabic)
    {
        WithArabicDictionary(() =>
        {
            var converter = new StatusToArabicConverter();
            var result = converter.Convert(status, typeof(string), null!, System.Globalization.CultureInfo.InvariantCulture);
            Assert.Equal(expectedArabic, result);
        });
    }
}
