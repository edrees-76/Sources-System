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

    public IsotopeReferenceEntry Entry => _entry;

    public IsotopeDetailsWindow(IsotopeReferenceEntry entry, IIsotopeLibraryService libraryService)
    {
        _entry = entry ?? throw new ArgumentNullException(nameof(entry));
        _libraryService = libraryService ?? throw new ArgumentNullException(nameof(libraryService));

        DataContext = this;
        InitializeComponent();
    }

    private void BtnCopyDetails_Click(object sender, RoutedEventArgs e)
    {
        var text = _entry.GetFormattedDetailsText();
        if (!string.IsNullOrWhiteSpace(text))
        {
            try
            {
                Clipboard.SetText(text);
                DialogHelper.ShowInfo(
                    TranslationHelper.GetString("MsgCopyDetailsSuccess"),
                    TranslationHelper.GetString("TitleCopySuccess")
                );
            }
            catch (Exception ex)
            {
                LoggerService.LogError("IsotopeDetailsWindow: Failed to copy to clipboard", ex);
            }
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
                TranslationHelper.GetString(_entry.IsOrnlSource ? "MsgErrOpenRefPdf" : "MsgErrOpenIcrpPdf"),
                TranslationHelper.GetString(_entry.IsOrnlSource ? "TitleReferencePdf" : "TitleIcrpPdf")
            );
        }
    }

    private void BtnCopyValue_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.Tag != null)
        {
            var value = element.Tag.ToString();
            if (string.IsNullOrWhiteSpace(value) || value == "—") return;

            try
            {
                Clipboard.SetText(value.Trim());
                DialogHelper.ShowInfo(
                    TranslationHelper.GetString("MsgCopyValueSuccess"),
                    TranslationHelper.GetString("TitleCopySuccess")
                );
            }
            catch (Exception ex)
            {
                LoggerService.LogError("IsotopeDetailsWindow: Failed to copy value to clipboard", ex);
            }
        }
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
