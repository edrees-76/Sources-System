using System;
using Sources.Helpers;
using Xunit;

namespace Sources.Tests;

/// <summary>
/// الجولة 156 (ب5 — الدفعة 2، موسَّعة): يتحقق أن رسالة نجاح/خطأ رئيسية واحدة على الأقل من كل ملف
/// من الملفات الخمسة المُترجَمة في هذه الجولة (Dashboard, LeakTests, Settings, SourceDetails, Borrow)
/// تُرجِع فعلياً القيمة الإنجليزية المترجَمة (وليس فقط أن المفتاح موجود في القاموس)، باستخدام نفس
/// آلية تبديل القاموس النشط المعتمدة في الجولات 130-136 (استبدال Strings.ar.xaml بـ Strings.en.xaml
/// عبر Uri مطلق، مطابقاً لآلية App.ApplyLanguage الإنتاجية).
/// </summary>
public class Round156TranslationTests
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

    [Fact]
    public void DashboardViewModel_SeverityAndSourceUnitLabels_UseEnglishStrings_WhenEnglishLanguageActive()
    {
        WithEnglishDictionary(() =>
        {
            // مطابق تماماً لمسار الكود في DashboardViewModel.SeverityLabel و tooltip formatters
            var critical = TranslationHelper.GetString("LabelSeverityCritical") ?? "Critical";
            var warning = TranslationHelper.GetString("LabelSeverityWarning") ?? "Warning";
            var sourceUnit = TranslationHelper.GetString("TextSourceUnit") ?? "مصدر";

            Assert.Equal("Critical", critical);
            Assert.Equal("Warning", warning);
            Assert.Equal("source", sourceUnit);
            Assert.NotEqual("حرج", critical);
            Assert.NotEqual("تحذير", warning);
            Assert.NotEqual("مصدر", sourceUnit);
        });
    }

    [Fact]
    public void LeakTestsViewModel_ValidationAndPageStatusMessages_UseEnglishStrings_WhenEnglishLanguageActive()
    {
        WithEnglishDictionary(() =>
        {
            // مطابق لمسار الكود في LeakTestsViewModel.SaveRecordAsync (رسالة تحقق فشل)
            var futureDateMsg = TranslationHelper.GetString("MsgErrFutureTestDate") ?? "لا يمكن أن يكون تاريخ الفحص في المستقبل";
            Assert.Equal("The test date cannot be in the future", futureDateMsg);
            Assert.NotEqual("لا يمكن أن يكون تاريخ الفحص في المستقبل", futureDateMsg);

            // مطابق لمسار الكود في LeakTestsViewModel.UpdatePagedView (رسالة نجاح/حالة صفحات، بصيغة GetFormat)
            var pageStatus = TranslationHelper.GetFormat("MsgPageStatus", 12, 40, 1, 4);
            Assert.Equal("Showing 12 of 40 records (page 1 of 4)", pageStatus);
        });
    }

    [Fact]
    public void SettingsViewModel_BackupFileFilter_UsesEnglishString_WhenEnglishLanguageActive()
    {
        WithEnglishDictionary(() =>
        {
            // مطابق لمسار الكود في SettingsViewModel.BrowseRestoreFileAsync (فلتر ملفات الاستعادة)
            var filter = TranslationHelper.GetString("FilterBackupFiles") ?? "ملفات النسخ الاحتياطي (*.zip;*.db)|*.zip;*.db|Zip Archives (*.zip)|*.zip|Database files (*.db)|*.db";
            Assert.Equal("Backup Files (*.zip;*.db)|*.zip;*.db|Zip Archives (*.zip)|*.zip|Database files (*.db)|*.db", filter);
            Assert.DoesNotContain("ملفات النسخ الاحتياطي", filter);
        });
    }

    [Fact]
    public void SourceDetailsViewModel_SealedTypeAndDoseRateMessages_UseEnglishStrings_WhenEnglishLanguageActive()
    {
        WithEnglishDictionary(() =>
        {
            // مطابق لمسار الكود في SourceDetailsViewModel.SourceTypeDisplay
            var sealedText = TranslationHelper.GetString("LabelSealedSourceType") ?? "مصدر مختوم";
            var unsealedText = TranslationHelper.GetString("LabelUnsealedSourceType") ?? "غير مختوم";
            Assert.Equal("Sealed Source", sealedText);
            Assert.Equal("Unsealed", unsealedText);

            // مطابق لمسار الكود في DoseRateWarningText (حالة عدم وجود مساهمة غاما)
            var noContribution = TranslationHelper.GetString("MsgDoseRateNoContributionAt1m") ?? "لا توجد مساهمة إشعاعية غاما عند 1 متر";
            Assert.Equal("No gamma radiation contribution at 1 meter", noContribution);
        });
    }

    [Fact]
    public void BorrowViewModel_DeliveryConfirmationAndExportMessages_UseEnglishStrings_WhenEnglishLanguageActive()
    {
        WithEnglishDictionary(() =>
        {
            // مطابق لمسار الكود في BorrowViewModel.SubmitAsync (رسالة تأكيد التسليم، بصيغة GetFormat)
            var confirmMsg = TranslationHelper.GetFormat("MsgConfirmDeliverSourceFormat", "S-001", "Ahmed");
            Assert.Equal("The source (S-001) will be delivered to (Ahmed).\nAre you sure you want to continue?", confirmMsg);

            // مطابق لمسار الكود في BorrowViewModel.ExportPdfAsync (رسالة نجاح التصدير)
            var successMsg = TranslationHelper.GetString("MsgSuccessExportPdf") ?? "تم تصدير التقرير كملف PDF بنجاح.";
            Assert.Equal("The report was successfully exported as a PDF file.", successMsg);
        });
    }
}
