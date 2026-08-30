using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sources.Helpers;
using Sources.Models;
using Sources.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace Sources.ViewModels;

public partial class LocationDetailsViewModel : ObservableObject
{
    private readonly IReportingService? _reportingService;
    private readonly INeutronSourceService? _neutronSourceService;
    private readonly List<LocationSourceRow> _allSourceRows = new();
    private readonly List<LocationNeutronSourceRow> _allNeutronSourceRows = new();

    public Location Location { get; }

    public string LocationName => Location.LocationName;
    public string? LocationType => Location.LocationType;
    public string? Building => Location.Building;
    public string? Room => Location.Room;
    public string? ResponsiblePerson => Location.ResponsiblePerson;
    public string? AddedBy => Location.AddedBy;

    [ObservableProperty]
    private ObservableCollection<LocationSourceRow> _filteredSources = new();

    [ObservableProperty]
    private ObservableCollection<LocationNeutronSourceRow> _filteredNeutronSources = new();

    [ObservableProperty]
    private ObservableCollection<TotalActivityItem> _totalActivityItems = new();

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _selectedStatusFilter = "الكل";

    [ObservableProperty]
    private bool _hasSources;

    [ObservableProperty]
    private bool _hasFilteredSources;

    [ObservableProperty]
    private int _totalSourcesCount;

    [ObservableProperty]
    private int _filteredSourcesCount;

    [ObservableProperty]
    private bool _hasNeutronSources;

    [ObservableProperty]
    private bool _hasFilteredNeutronSources;

    [ObservableProperty]
    private int _totalNeutronSourcesCount;

    [ObservableProperty]
    private int _filteredNeutronSourcesCount;

    public List<string> StatusFilterOptions { get; } = new()
    {
        "الكل",
        "قيد الاستخدام",
        "مخزن",
        "نفايات",
        "قيد النقل"
    };

    public LocationDetailsViewModel(
        Location location,
        IEnumerable<Source> sources,
        IReportingService? reportingService = null,
        IEnumerable<NeutronSource>? neutronSources = null,
        INeutronSourceService? neutronSourceService = null)
    {
        Location = location ?? throw new ArgumentNullException(nameof(location));
        _reportingService = reportingService ?? (App.ServiceProvider?.GetService(typeof(IReportingService)) as IReportingService);
        _neutronSourceService = neutronSourceService ?? (App.ServiceProvider?.GetService(typeof(INeutronSourceService)) as INeutronSourceService);

        var sourceList = sources?.ToList() ?? new List<Source>();
        _allSourceRows = sourceList.Select((src, index) => new LocationSourceRow
        {
            RowNumber = index + 1,
            Source = src
        }).ToList();

        TotalSourcesCount = _allSourceRows.Count;
        HasSources = TotalSourcesCount > 0;

        var neutronList = neutronSources?.ToList() ?? 
            (_neutronSourceService?.GetByLocation(location.Id) ?? new List<NeutronSource>());

        _allNeutronSourceRows = neutronList.Select((ns, index) => new LocationNeutronSourceRow
        {
            RowNumber = index + 1,
            NeutronSource = ns
        }).ToList();

        TotalNeutronSourcesCount = _allNeutronSourceRows.Count;
        HasNeutronSources = TotalNeutronSourcesCount > 0;

        CalculateTotalActivity(sourceList);
        ApplyFilters();
    }

    partial void OnSearchTextChanged(string value)
    {
        ApplyFilters();
    }

    partial void OnSelectedStatusFilterChanged(string value)
    {
        ApplyFilters();
    }

    public void ApplyFilters()
    {
        // 1. تصفية المصادر المشعة العادية
        IEnumerable<LocationSourceRow> query = _allSourceRows;
        if (!string.IsNullOrWhiteSpace(SelectedStatusFilter) && SelectedStatusFilter != "الكل")
        {
            query = query.Where(r => r.ArabicStatus.Equals(SelectedStatusFilter, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var search = SearchText.Trim();
            query = query.Where(r =>
                (r.DisplaySourceCode?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (r.SourceCode?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (r.DisplayIsotopes?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (r.SerialNumber?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (r.Manufacturer?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false)
            );
        }

        var results = query.ToList();
        for (int i = 0; i < results.Count; i++)
        {
            results[i].RowNumber = i + 1;
        }

        FilteredSources = new ObservableCollection<LocationSourceRow>(results);
        FilteredSourcesCount = results.Count;
        HasFilteredSources = FilteredSourcesCount > 0;

        // 2. تصفية المصادر النيترونية
        IEnumerable<LocationNeutronSourceRow> neutronQuery = _allNeutronSourceRows;
        if (!string.IsNullOrWhiteSpace(SelectedStatusFilter) && SelectedStatusFilter != "الكل")
        {
            neutronQuery = neutronQuery.Where(r => r.ArabicStatus.Equals(SelectedStatusFilter, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var search = SearchText.Trim();
            neutronQuery = neutronQuery.Where(r =>
                (r.SourceCode?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (r.TypeCode?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (r.TypeNameAr?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (r.SerialNumber?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false)
            );
        }

        var neutronResults = neutronQuery.ToList();
        for (int i = 0; i < neutronResults.Count; i++)
        {
            neutronResults[i].RowNumber = i + 1;
        }

        FilteredNeutronSources = new ObservableCollection<LocationNeutronSourceRow>(neutronResults);
        FilteredNeutronSourcesCount = neutronResults.Count;
        HasFilteredNeutronSources = FilteredNeutronSourcesCount > 0;
    }

    [RelayCommand]
    public void ClearFilters()
    {
        SearchText = string.Empty;
        SelectedStatusFilter = "الكل";
        ApplyFilters();
    }

    /// <summary>
    /// حساب إجمالي النشاط الإشعاعي للمصادر النشطة المتواجدة فعلياً بالموقع حالياً (غير محذوفة وتتبع الموقع الحالي)
    /// </summary>
    public void CalculateTotalActivity(IEnumerable<Source> sources)
    {
        try
        {
            double totalBq = 0;
            var activeCurrentSources = (sources ?? Enumerable.Empty<Source>())
                .Where(s => !s.IsDeleted && (Location == null || s.LocationId == Location.Id));

            foreach (var src in activeCurrentSources)
            {
                var unit = src.CurrentActivityUnit;
                if (unit != null && unit.ConversionToBq > 0)
                {
                    totalBq += src.CurrentActivityValue * unit.ConversionToBq;
                }
                else
                {
                    totalBq += src.CurrentActivityValue;
                }
            }

            var units = new List<(string Symbol, double Factor)>
            {
                ("Ci", 3.7e10),
                ("mCi", 3.7e7),
                ("µCi", 3.7e4),
                ("Bq", 1.0)
            };

            var items = new ObservableCollection<TotalActivityItem>();
            foreach (var (symbol, factor) in units)
            {
                double converted = totalBq / factor;
                items.Add(new TotalActivityItem
                {
                    UnitSymbol = symbol,
                    Value = converted,
                    DisplayValue = FormatActivityValue(converted, symbol)
                });
            }

            TotalActivityItems = items;
        }
        catch (Exception ex)
        {
            LoggerService.LogError("LocationDetailsViewModel: CalculateTotalActivity failed", ex);
            TotalActivityItems = new ObservableCollection<TotalActivityItem>();
        }
    }

    public static string FormatActivityValue(double value, string unitSymbol)
    {
        if (value == 0) return $"0 {unitSymbol}";
        if (Math.Abs(value) >= 1e9) return $"{value:E3} {unitSymbol}";
        if (Math.Abs(value) >= 1e6) return $"{value:N0} {unitSymbol}";
        if (Math.Abs(value) >= 1000) return $"{value:N2} {unitSymbol}";
        if (Math.Abs(value) >= 1) return $"{value:N4} {unitSymbol}";
        return $"{value:E3} {unitSymbol}";
    }

    [RelayCommand]
    public async Task ExportToPdfAsync()
    {
        if (_reportingService == null) return;
        var sfd = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "PDF Files (*.pdf)|*.pdf",
            FileName = $"تقرير_مصادر_{LocationName}_{DateTime.Now:yyyyMMdd}.pdf"
        };

        if (sfd.ShowDialog() == true)
        {
            try
            {
                var list = FilteredSources.Select(r => r.Source).ToList();
                string title = $"تقرير مصادر الموقع: {LocationName}";
                await _reportingService.GenerateInventoryReportPdfAsync(list, sfd.FileName, title);
                FileHelper.OpenFile(sfd.FileName);
                DialogHelper.ShowInfo(TranslationHelper.GetString("MsgExportSuccess") ?? "تم تصدير التقرير كملف PDF بنجاح.");
            }
            catch (Exception ex)
            {
                DialogHelper.ShowError(TranslationHelper.GetFormat("MsgErrExportPdf", ex.Message));
            }
        }
    }

    [RelayCommand]
    public async Task ExportToExcelAsync()
    {
        if (_reportingService == null) return;
        var sfd = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "Excel Files (*.xlsx)|*.xlsx",
            FileName = $"تقرير_مصادر_{LocationName}_{DateTime.Now:yyyyMMdd}.xlsx"
        };

        if (sfd.ShowDialog() == true)
        {
            try
            {
                var list = FilteredSources.Select(r => r.Source).ToList();
                string title = $"تقرير مصادر الموقع: {LocationName}";
                await _reportingService.GenerateInventoryReportExcelAsync(list, sfd.FileName, title);
                FileHelper.OpenFile(sfd.FileName);
                DialogHelper.ShowInfo(TranslationHelper.GetString("MsgExportSuccess") ?? "تم تصدير البيانات إلى ملف Excel بنجاح.");
            }
            catch (Exception ex)
            {
                DialogHelper.ShowError(TranslationHelper.GetFormat("MsgErrExportExcel", ex.Message));
            }
        }
    }

    [RelayCommand]
    private void ViewSourceDetails(object? parameter)
    {
        SourceNavigationHelper.OpenSourceDetails(parameter);
    }

    [RelayCommand]
    private void ViewNeutronSourceDetails(object? parameter)
    {
        SourceNavigationHelper.OpenNeutronSourceDetails(parameter);
    }
}
