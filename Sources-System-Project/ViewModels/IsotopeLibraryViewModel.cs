using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sources.Helpers;
using Sources.Services;
using Sources.Models;

namespace Sources.ViewModels;

public partial class IsotopeLibraryViewModel : ObservableObject
{
    private readonly IIsotopeLibraryService _libraryService;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private ObservableCollection<IsotopeReferenceEntry> _filteredEntries = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOrnlSelected))]
    [NotifyPropertyChangedFor(nameof(IsIcrpSelected))]
    private IsotopeReferenceEntry? _selectedEntry;

    public bool IsOrnlSelected => SelectedEntry?.IsOrnlSource == true;
    public bool IsIcrpSelected => SelectedEntry?.IsIcrpSource == true;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _hasResults = true;

    [ObservableProperty]
    private bool _isNotFound;

    [ObservableProperty]
    private int _totalCount;

    [ObservableProperty]
    private int _ornlCount;

    [ObservableProperty]
    private int _icrpCount;

    [ObservableProperty]
    private int _resultsCount;

    public IsotopeLibraryViewModel(IIsotopeLibraryService libraryService)
    {
        _libraryService = libraryService;
        _ = InitializeAsync();
    }

    public async Task InitializeAsync()
    {
        IsLoading = true;
        try
        {
            var all = await _libraryService.GetAllEntriesAsync();
            TotalCount = all.Count;
            OrnlCount = all.Count(x => x.IsOrnlSource);
            IcrpCount = all.Count(x => x.IsIcrpSource);

            for (int i = 0; i < all.Count; i++)
            {
                all[i].ItemIndex = i + 1;
            }

            FilteredEntries = new ObservableCollection<IsotopeReferenceEntry>(all);
            ResultsCount = FilteredEntries.Count;
            HasResults = ResultsCount > 0;
            IsNotFound = false;
            SelectedEntry = FilteredEntries.FirstOrDefault();
        }
        catch (Exception ex)
        {
            LoggerService.LogError("IsotopeLibraryViewModel: Initialization failed", ex);
        }
        finally
        {
            IsLoading = false;
        }
    }

    partial void OnSearchTextChanged(string value)
    {
        _ = PerformSearchAsync(value);
    }

    [RelayCommand]
    public async Task PerformSearchAsync(string? query)
    {
        IsLoading = true;
        try
        {
            var results = await _libraryService.SearchAsync(query ?? string.Empty);
            for (int i = 0; i < results.Count; i++)
            {
                results[i].ItemIndex = i + 1;
            }

            FilteredEntries = new ObservableCollection<IsotopeReferenceEntry>(results);
            ResultsCount = FilteredEntries.Count;
            HasResults = ResultsCount > 0;
            IsNotFound = !HasResults && !string.IsNullOrWhiteSpace(query);

            if (SelectedEntry == null || !FilteredEntries.Contains(SelectedEntry))
            {
                SelectedEntry = FilteredEntries.FirstOrDefault();
            }
        }
        catch (Exception ex)
        {
            LoggerService.LogError("IsotopeLibraryViewModel: Search failed", ex);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public void ClearSearch()
    {
        SearchText = string.Empty;
    }

    [RelayCommand]
    public void SelectEntry(IsotopeReferenceEntry? entry)
    {
        if (entry != null)
        {
            SelectedEntry = entry;
        }
    }

    [RelayCommand]
    public void OpenReferencePdf()
    {
        var page = SelectedEntry?.PageNumber ?? 0;
        var success = _libraryService.OpenReferencePdf(page);
        if (!success)
        {
            DialogHelper.ShowWarning(
                TranslationHelper.GetString("MsgErrOpenRefPdf") ?? "تعذر فتح ملف المرجع الأصلي. تأكد من وجود ملف 14724519.pdf وتوفر برنامج لقراءة ملفات PDF على جهازك.",
                TranslationHelper.GetString("TitleReferencePdf") ?? "الملف المرجعي ORNL"
            );
        }
    }

    [RelayCommand]
    public void OpenReferencePdfAtPage(int pageNumber)
    {
        var success = _libraryService.OpenReferencePdf(pageNumber);
        if (!success)
        {
            DialogHelper.ShowWarning(
                TranslationHelper.GetString("MsgErrOpenRefPdf") ?? "تعذر فتح ملف المرجع الأصلي. تأكد من وجود ملف 14724519.pdf وتوفر برنامج لقراءة ملفات PDF على جهازك.",
                TranslationHelper.GetString("TitleReferencePdf") ?? "الملف المرجعي ORNL"
            );
        }
    }

    [RelayCommand]
    public void OpenIcrpPdf()
    {
        var success = _libraryService.OpenIcrpPdf();
        if (!success)
        {
            DialogHelper.ShowWarning(
                TranslationHelper.GetString("MsgErrOpenIcrpPdf") ?? "تعذر فتح ملف مرجع ICRP 107. تأكد من وجود ملف ANIB_38_3.pdf وتوفر برنامج لقراءة ملفات PDF على جهازك.",
                TranslationHelper.GetString("TitleIcrpPdf") ?? "مرجع ICRP 107"
            );
        }
    }

    [RelayCommand]
    public void CopyDetails(IsotopeReferenceEntry? entry = null)
    {
        var target = entry ?? SelectedEntry;
        if (target == null) return;

        var text = target.GetFormattedDetailsText();
        if (!string.IsNullOrWhiteSpace(text))
        {
            try
            {
                System.Windows.Clipboard.SetText(text);
                DialogHelper.ShowInfo(
                    TranslationHelper.GetString("MsgCopyDetailsSuccess") ?? "تم نسخ كافة بيانات النظير إلى الحافظة بنجاح.",
                    TranslationHelper.GetString("TitleCopySuccess") ?? "تم النسخ"
                );
            }
            catch (Exception ex)
            {
                LoggerService.LogError("IsotopeLibraryViewModel: Failed to copy to clipboard", ex);
            }
        }
    }

    [RelayCommand]
    public void OpenDetailsDialog(IsotopeReferenceEntry? entry = null)
    {
        var target = entry ?? SelectedEntry;
        if (target == null) return;

        try
        {
            var dialog = new Views.IsotopeDetailsWindow(target, _libraryService);
            if (System.Windows.Application.Current?.MainWindow != null && System.Windows.Application.Current.MainWindow.IsLoaded)
            {
                dialog.Owner = System.Windows.Application.Current.MainWindow;
            }
            dialog.ShowDialog();
        }
        catch (Exception ex)
        {
            LoggerService.LogError("IsotopeLibraryViewModel: Failed to open IsotopeDetailsWindow", ex);
        }
    }

    [RelayCommand]
    public void CopyValue(object? value)
    {
        if (value == null) return;
        var s = value.ToString();
        if (string.IsNullOrWhiteSpace(s) || s == "—") return;

        try
        {
            System.Windows.Clipboard.SetText(s.Trim());
            DialogHelper.ShowInfo(
                TranslationHelper.GetString("MsgCopyValueSuccess") ?? "تم نسخ القيمة إلى الحافظة بنجاح.",
                TranslationHelper.GetString("TitleCopySuccess") ?? "تم النسخ"
            );
        }
        catch (Exception ex)
        {
            LoggerService.LogError("IsotopeLibraryViewModel: Failed to copy value to clipboard", ex);
        }
    }
}
