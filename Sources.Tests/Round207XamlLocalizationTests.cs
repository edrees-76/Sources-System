using System;
using Sources.Helpers;
using Xunit;

namespace Sources.Tests;

public class Round207XamlLocalizationTests
{
    [Fact]
    public void XamlResourceKeys_ReturnCorrectValues_InArabicAndEnglish()
    {
        Fixtures.WpfStaFixture.RunInSta(() =>
        {
            // 1. In Arabic
            Assert.Equal("مصادر", TranslationHelper.GetString("BrandAppTitle"));
            Assert.Equal("د. أحمد علي", TranslationHelper.GetString("HintInspectorName"));
            Assert.Equal("أي ملاحظات حول إجراءات المسح والمسحة القطنية...", TranslationHelper.GetString("HintLeakTestNotes"));

            // 2. In English
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

                Assert.Equal("Sources", TranslationHelper.GetString("BrandAppTitle"));
                Assert.Equal("e.g. Dr. Ahmed Ali", TranslationHelper.GetString("HintInspectorName"));
                Assert.Equal("Any notes about the wipe test procedure...", TranslationHelper.GetString("HintLeakTestNotes"));
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
