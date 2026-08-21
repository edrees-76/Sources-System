using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Sources.Data;
using Sources.Helpers;
using Sources.Messages;
using Sources.Models;
using Sources.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;

namespace Sources.ViewModels;

/// <summary>
/// صف عرض مخصص لجدول التنبيهات الذكية يضمن ثبات الرقم التسلسلي والحسابات الدقيقة
/// </summary>
public class AlertRow
{
    public int RowNumber { get; set; }
    public AlertNotification Alert { get; set; } = null!;
    public Guid Id => Alert.Id;
    public string AlertType => Alert.AlertType;
    public string Severity => Alert.Severity;
    public string Message => Alert.Message;
    public DateTime CreatedAt => Alert.CreatedAt;
    public bool IsRead => Alert.IsRead;
    public bool IsDismissed => Alert.IsDismissed;
    public Guid? SourceId => Alert.SourceId;
    public Source? Source => Alert.Source;
    public string SourceCode => Alert.Source?.SourceCode ?? "—";
    public string LocationName => Alert.Source?.Location?.LocationName ?? "—";
    public string DisplayIsotopes => Alert.Source?.DisplayIsotopes ?? Alert.Source?.Radioisotope?.Symbol ?? "—";

    public double HalfLivesElapsed { get; set; } = -1;
    public string HalfLivesDisplay => HalfLivesElapsed >= 0 ? $"{HalfLivesElapsed:F1} T½" : "—";
    public string WorstIsotopeSymbol { get; set; } = string.Empty;

    public string SeverityColor => Severity switch
    {
        "Critical" => "#C25B4A",
        "Warning" => "#E0A93E",
        _ => "#4F7FA3"
    };

    public string SeverityBadgeBackground => Severity switch
    {
        "Critical" => "#1AC25B4A",
        "Warning" => "#1AE0A93E",
        _ => "#1A4F7FA3"
    };

    public string SeverityLabel => Severity switch
    {
        "Critical" => TranslationHelper.GetString("LabelCritical"),
        "Warning" => TranslationHelper.GetString("LabelWarning"),
        _ => Severity
    };

    public string StatusDisplay => IsDismissed
        ? TranslationHelper.GetString("StatusDismissed")
        : (IsRead ? TranslationHelper.GetString("StatusRead") : TranslationHelper.GetString("StatusUnread"));
}

public partial class AlertsViewModel : ObservableObject, IDisposable
{
    private readonly IAlertService _alertService;
    private readonly ILocationService _locationService;
    private readonly ISourceService? _sourceService;

    public void Dispose()
    {
        WeakReferenceMessenger.Default.UnregisterAll(this);
    }

    [ObservableProperty] private ObservableCollection<AlertRow> _alerts = new();
    [ObservableProperty] private ObservableCollection<AlertRow> _pagedAlerts = new();
    [ObservableProperty] private AlertRow? _selectedAlert;

    // ─── الفلاتر والبحث ───
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private string _selectedSeverityFilter = "All";
    [ObservableProperty] private string _selectedLocationFilter = string.Empty;
    [ObservableProperty] private DateTime? _filterStartDate;
    [ObservableProperty] private DateTime? _filterEndDate;
    [ObservableProperty] private bool _showDismissed;
    [ObservableProperty] private ObservableCollection<string> _availableLocations = new();
    [ObservableProperty] private ObservableCollection<string> _availableSeverities = new();

    // ─── الإحصائيات السريعة ───
    [ObservableProperty] private int _totalAlertsCount;
    [ObservableProperty] private int _criticalAlertsCount;
    [ObservableProperty] private int _warningAlertsCount;
    [ObservableProperty] private int _unreadAlertsCount;

    // ─── تقسيم الصفحات (Pagination) ───
    [ObservableProperty] private int _currentPage = 1;
    [ObservableProperty] private int _pageSize = 20;
    [ObservableProperty] private int _totalPages = 1;
    [ObservableProperty] private string _pageInfo = "1 / 1";
    [ObservableProperty] private ObservableCollection<int> _availablePageSizes = new(new[] { 10, 20, 50, 100 });

    partial void OnPageSizeChanged(int value)
    {
        CurrentPage = 1;
        ApplyFiltersAndPagination();
    }

    // ─── رسائل النظام ───
    [ObservableProperty] private string _message = string.Empty;
    [ObservableProperty] private bool _hasMessage;

    private List<AlertRow> _allAlertRows = new();

    public AlertsViewModel(IAlertService alertService, ILocationService locationService, ISourceService? sourceService = null)
    {
        _alertService = alertService;
        _locationService = locationService;
        _sourceService = sourceService;

        AvailableSeverities = new ObservableCollection<string>(new[] { "All", "Critical", "Warning" });

        // الاستماع لأي تحديث في المصادر لإعادة تحميل التنبيهات
        WeakReferenceMessenger.Default.Register<SourcesUpdatedMessage>(this, (r, m) =>
        {
            RunOnUI(LoadData);
        });

        LoadLocations();
        LoadData();
    }

    private static void RunOnUI(Action action)
    {
        if (Application.Current?.Dispatcher != null && !Application.Current.Dispatcher.CheckAccess())
        {
            Application.Current.Dispatcher.Invoke(action);
        }
        else
        {
            action();
        }
    }

    public void LoadLocations()
    {
        try
        {
            var locs = _locationService.GetAll().Select(l => l.LocationName).OrderBy(x => x).ToList();
            locs.Insert(0, string.Empty); // الكل
            AvailableLocations = new ObservableCollection<string>(locs);
        }
        catch
        {
            AvailableLocations = new ObservableCollection<string>();
        }
    }

    [RelayCommand]
    public void LoadData()
    {
        try
        {
            var rawAlerts = _alertService.GetAllAlerts(includeDismissed: true);

            _allAlertRows = rawAlerts.Select((alert, index) =>
            {
                var row = new AlertRow
                {
                    RowNumber = index + 1,
                    Alert = alert
                };

                if (alert.Source != null)
                {
                    var (hl, worstIso) = AlertService.CalculateMaxHalfLivesElapsed(alert.Source);
                    row.HalfLivesElapsed = hl;
                    row.WorstIsotopeSymbol = !string.IsNullOrEmpty(worstIso) 
                        ? worstIso 
                        : (alert.Source.DisplayIsotopes ?? alert.Source.Radioisotope?.Symbol ?? string.Empty);
                }

                return row;
            }).ToList();

            // تحديث الإحصائيات العامة
            TotalAlertsCount = _allAlertRows.Count(a => !a.IsDismissed);
            CriticalAlertsCount = _allAlertRows.Count(a => a.Severity == "Critical" && !a.IsDismissed);
            WarningAlertsCount = _allAlertRows.Count(a => a.Severity == "Warning" && !a.IsDismissed);
            UnreadAlertsCount = _allAlertRows.Count(a => !a.IsRead && !a.IsDismissed);

            ApplyFiltersAndPagination();
        }
        catch (Exception ex)
        {
            ShowMessage(TranslationHelper.GetFormat("MsgErrGeneral", ex.Message));
        }
    }

    [RelayCommand]
    public void RefreshAlerts()
    {
        _alertService.GenerateAlerts();
        LoadData();
        ShowMessage(TranslationHelper.GetString("MsgAlertsRefreshed"));
    }

    partial void OnSearchTextChanged(string value) => ApplyFiltersAndPagination();
    partial void OnSelectedSeverityFilterChanged(string value) => ApplyFiltersAndPagination();
    partial void OnSelectedLocationFilterChanged(string value) => ApplyFiltersAndPagination();
    partial void OnFilterStartDateChanged(DateTime? value) => ApplyFiltersAndPagination();
    partial void OnFilterEndDateChanged(DateTime? value) => ApplyFiltersAndPagination();
    partial void OnShowDismissedChanged(bool value) => ApplyFiltersAndPagination();

    public void ApplyFiltersAndPagination()
    {
        var filtered = _allAlertRows.AsEnumerable();

        // 1. فلتر الإخفاء
        if (!ShowDismissed)
        {
            filtered = filtered.Where(a => !a.IsDismissed);
        }

        // 2. فلتر مستوى الخطورة
        if (!string.IsNullOrWhiteSpace(SelectedSeverityFilter) && SelectedSeverityFilter != "All")
        {
            filtered = filtered.Where(a => a.Severity.Equals(SelectedSeverityFilter, StringComparison.OrdinalIgnoreCase));
        }

        // 3. فلتر الموقع
        if (!string.IsNullOrWhiteSpace(SelectedLocationFilter))
        {
            filtered = filtered.Where(a => a.LocationName.Equals(SelectedLocationFilter, StringComparison.OrdinalIgnoreCase));
        }

        // 4. فلتر التاريخ
        if (FilterStartDate.HasValue)
        {
            var start = FilterStartDate.Value.Date;
            filtered = filtered.Where(a => a.CreatedAt.Date >= start);
        }
        if (FilterEndDate.HasValue)
        {
            var end = FilterEndDate.Value.Date;
            filtered = filtered.Where(a => a.CreatedAt.Date <= end);
        }

        // 5. البحث النصي
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var term = SearchText.Trim().ToLower();
            filtered = filtered.Where(a =>
                a.Message.ToLower().Contains(term) ||
                a.SourceCode.ToLower().Contains(term) ||
                a.WorstIsotopeSymbol.ToLower().Contains(term) ||
                a.LocationName.ToLower().Contains(term));
        }

        var list = filtered.ToList();
        TotalPages = Math.Max(1, (int)Math.Ceiling((double)list.Count / PageSize));

        if (CurrentPage > TotalPages) CurrentPage = TotalPages;
        if (CurrentPage < 1) CurrentPage = 1;

        int skip = (CurrentPage - 1) * PageSize;
        var pagedList = list.Skip(skip).Take(PageSize).ToList();

        for (int i = 0; i < pagedList.Count; i++)
        {
            pagedList[i].RowNumber = skip + i + 1;
        }

        Alerts = new ObservableCollection<AlertRow>(list);
        PagedAlerts = new ObservableCollection<AlertRow>(pagedList);
        PageInfo = $"{CurrentPage} / {TotalPages}";
    }

    [RelayCommand]
    public void MarkAsRead(AlertRow? row)
    {
        if (row == null) return;
        _alertService.MarkAsRead(row.Id);
        LoadData();
    }

    [RelayCommand]
    public void DismissAlert(AlertRow? row)
    {
        if (row == null) return;
        _alertService.DismissAlert(row.Id);
        LoadData();
    }

    [RelayCommand]
    public void MarkAllAsRead()
    {
        _alertService.MarkAllAsRead();
        LoadData();
    }

    [RelayCommand]
    public void ResetFilters()
    {
        SearchText = string.Empty;
        SelectedSeverityFilter = "All";
        SelectedLocationFilter = string.Empty;
        FilterStartDate = null;
        FilterEndDate = null;
        ShowDismissed = false;
        CurrentPage = 1;
        ApplyFiltersAndPagination();
    }

    [RelayCommand]
    public void NextPage()
    {
        if (CurrentPage < TotalPages)
        {
            CurrentPage++;
            ApplyFiltersAndPagination();
        }
    }

    [RelayCommand]
    public void PreviousPage()
    {
        if (CurrentPage > 1)
        {
            CurrentPage--;
            ApplyFiltersAndPagination();
        }
    }

    [RelayCommand]
    public void ChangePageSize(string newSizeStr)
    {
        if (int.TryParse(newSizeStr, out int newSize) && newSize > 0)
        {
            PageSize = newSize;
        }
    }

    [RelayCommand]
    public void ViewSourceDetails(object? parameter)
    {
        Source? source = parameter switch
        {
            AlertRow row => row.Source ?? (row.SourceId.HasValue ? _sourceService?.GetSourceById(row.SourceId.Value) : null),
            Source s => s,
            Guid id => _sourceService?.GetSourceById(id),
            _ => null
        };

        if (source == null)
        {
            DialogHelper.ShowWarning(
                TranslationHelper.GetString("MsgSourceNotFound") ?? "المصدر غير موجود أو تم حذفه من المنظومة.",
                TranslationHelper.GetString("TitleSourceDetails") ?? "تفاصيل المصدر");
            return;
        }

        string lre = "\u202A";
        string pdf = "\u202C";

        string details = $"{TranslationHelper.GetString("LabelSourceCode")} {lre}{source.SourceCode}{pdf}\n" +
                         $"{TranslationHelper.GetString("LabelIsotope")} {lre}{source.DisplayIsotopes}{pdf}\n";

        if (source.HasDetailedIsotopes && source.SourceIsotopes != null && source.SourceIsotopes.Any())
        {
            details += $"{TranslationHelper.GetString("LabelActivityDetails")}\n";
            foreach (var si in source.SourceIsotopes)
            {
                string unitSymbol = si.ActivityUnit?.UnitSymbol
                                 ?? source.InitialActivityUnit?.UnitSymbol
                                 ?? "Bq";

                details += $"  - {lre}{si.Radioisotope?.Symbol ?? TranslationHelper.GetString("LabelIsotopeSymbol")}: {si.CurrentActivityValue:N4} {unitSymbol}{pdf}\n";
            }
        }
        else
        {
            details += $"{TranslationHelper.GetString("LabelCurrentActivity")} {lre}{source.CurrentActivityValue:N4} {source.CurrentActivityUnit?.UnitSymbol}{pdf}\n";
        }

        details += $"{TranslationHelper.GetString("LabelSerialNumber")} {lre}{source.SerialNumber ?? "N/A"}{pdf}\n" +
                  $"{TranslationHelper.GetString("LabelLocation")} {lre}{source.Location?.LocationName ?? "N/A"}{pdf}\n" +
                  $"{TranslationHelper.GetString("LabelCalibrationDate")} {lre}{source.CalibrationDate:yyyy-MM-dd}{pdf}\n" +
                  $"{TranslationHelper.GetString("LabelStatus")} {source.ArabicStatus}\n" +
                  $"{TranslationHelper.GetString("LabelManufacturer")} {lre}{source.Manufacturer ?? "N/A"}{pdf}\n" +
                  $"{TranslationHelper.GetString("LabelNotes")} {source.Notes ?? "N/A"}";

        DialogHelper.ShowInfo(details, TranslationHelper.GetString("TitleSourceDetails") ?? "تفاصيل المصدر", source.ImagePath);
    }

    private void ShowMessage(string msg)
    {
        Message = msg;
        HasMessage = true;
    }
}
