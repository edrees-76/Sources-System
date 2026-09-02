using System;
using System.Windows;
using Sources.Helpers;
using Sources.Models;
using Sources.Services;

namespace Sources.Views;

public partial class IsotopeDetailsWindow : Window
{
    private readonly IsotopeReferenceEntry _entry;
    private readonly IIsotopeLibraryService _libraryService;
    private readonly IClipboardService _clipboard;

    public IsotopeReferenceEntry Entry => _entry;

    public IsotopeDetailsWindow(IsotopeReferenceEntry entry, IIsotopeLibraryService libraryService, IClipboardService? clipboard = null)
    {
        _entry = entry ?? throw new ArgumentNullException(nameof(entry));
        _libraryService = libraryService ?? throw new ArgumentNullException(nameof(libraryService));
        _clipboard = clipboard ?? new ClipboardService();

        DataContext = this;
        InitializeComponent();
    }

    private void BtnCopyDetails_Click(object sender, RoutedEventArgs e)
    {
        var text = _entry.GetFormattedDetailsText();
        if (!string.IsNullOrWhiteSpace(text))
        {
            ClipboardCopyHelper.CopyWithFeedback(
                _clipboard, text,
                TranslationHelper.GetString("MsgCopyDetailsSuccess") ?? "تم نسخ كافة بيانات النظير إلى الحافظة بنجاح.",
                TranslationHelper.GetString("TitleCopySuccess") ?? "تم النسخ",
                "IsotopeDetailsWindow.CopyDetails");
        }
    }

    private void BtnOpenPdf_Click(object sender, RoutedEventArgs e)
    {
        bool success;
        if (_entry.IsOrnlSource)
        {
            success = _libraryService.OpenReferencePdf(_entry.PageNumber);
        }
        else
        {
            success = _libraryService.OpenIcrpPdf();
        }

        if (!success)
        {
            DialogHelper.ShowWarning(
                TranslationHelper.GetString(_entry.IsOrnlSource ? "MsgErrOpenRefPdf" : "MsgErrOpenIcrpPdf") ?? (_entry.IsOrnlSource ? "تعذر فتح ملف المرجع" : "تعذر فتح تقرير ICRP"),
                TranslationHelper.GetString(_entry.IsOrnlSource ? "TitleReferencePdf" : "TitleIcrpPdf") ?? (_entry.IsOrnlSource ? "مرجع النظير" : "تقرير ICRP")
            );
        }
    }

    private void BtnCopyValue_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.Tag != null)
        {
            var value = element.Tag.ToString();
            if (string.IsNullOrWhiteSpace(value) || value == "—") return;

            ClipboardCopyHelper.CopyWithFeedback(
                _clipboard, value.Trim(),
                TranslationHelper.GetString("MsgCopyValueSuccess") ?? "تم نسخ القيمة إلى الحافظة بنجاح.",
                TranslationHelper.GetString("TitleCopySuccess") ?? "تم النسخ",
                "IsotopeDetailsWindow.CopyValue");
        }
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
