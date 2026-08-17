using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sources.Models;
using Sources.Services;
using Sources.Helpers;
using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace Sources.ViewModels;

public partial class ReportsViewModel : ObservableObject
{
    private readonly ISourceService _sourceService;
    private readonly IBorrowService _borrowService;
    private readonly IReportingService _reportingService;
    private readonly ISystemSettingsService _settingsService;

    [ObservableProperty] private string _selectedReport = "InventoryReport";
    [ObservableProperty] private ObservableCollection<Source> _inventoryData = new();
    [ObservableProperty] private ObservableCollection<BorrowRequest> _borrowingData = new();
    [ObservableProperty] private ObservableCollection<Source> _activityData = new();
    [ObservableProperty] private ObservableCollection<Source> _lowActivityData = new();
    [ObservableProperty] private double _lowActivityThreshold = 10;
    [ObservableProperty] private ObservableCollection<Source> _lowActivityAlertData = new();

    public ReportsViewModel(
        ISourceService sourceService, 
        IBorrowService borrowService, 
        IReportingService reportingService,
        ISystemSettingsService settingsService)
    {
        _sourceService = sourceService;
        _borrowService = borrowService;
        _reportingService = reportingService;
        _settingsService = settingsService;
        
        // جلب عتبة النشاط المنخفض من الإعدادات
        LowActivityThreshold = _settingsService.GetSetting("LowActivityThresholdPercent", 10.0);
        
        LoadReport();
    }

    [RelayCommand]
    public void LoadReport()
    {
        _sourceService.UpdateAllCurrentActivities();
        var allSources = _sourceService.GetAllSources();
        
        switch (SelectedReport)
        {
            case "InventoryReport":
                InventoryData = new ObservableCollection<Source>(allSources);
                break;
            case "BorrowingReport":
                BorrowingData = new ObservableCollection<BorrowRequest>(_borrowService.GetAll());
                break;
            case "ActivityReport":
                ActivityData = new ObservableCollection<Source>(allSources.Where(s => s.Status == "InUse" || s.Status == "Storage"));
                break;
            case "LowActivityReport":
                LowActivityData = new ObservableCollection<Source>(_sourceService.GetLowActivitySources(LowActivityThreshold));
                break;
            case "LowActivityAlertReport":
                // تصفية وتصنيف المصادر التي تجاوزت عتبات نصف العمر (T½)
                LowActivityAlertData = new ObservableCollection<Source>(GetLowActivityAlertSources(allSources));
                break;
            case "GeneralReport":
                InventoryData = new ObservableCollection<Source>(allSources);
                BorrowingData = new ObservableCollection<BorrowRequest>(_borrowService.GetAll());
                ActivityData = new ObservableCollection<Source>(allSources.Where(s => s.Status == "InUse" || s.Status == "Storage"));
                LowActivityData = new ObservableCollection<Source>(_sourceService.GetLowActivitySources(LowActivityThreshold));
                LowActivityAlertData = new ObservableCollection<Source>(GetLowActivityAlertSources(allSources));
                break;
        }
    }

    private static List<Source> GetLowActivityAlertSources(List<Source> allSources)
    {
        return allSources
            .Where(s => s.Status == "InUse" || s.Status == "Storage")
            .Select(s =>
            {
                var (maxHalfLives, worstIsotope) = CalculateMaxHalfLivesElapsed(s);
                s.AlertHalfLivesElapsed = maxHalfLives;
                s.AlertWorstIsotope = !string.IsNullOrEmpty(worstIsotope) ? worstIsotope : s.DisplayIsotopes;
                s.AlertSeverity = maxHalfLives >= 6.0 ? "Critical" : (maxHalfLives >= 5.0 ? "Warning" : null);
                return s;
            })
            .Where(s => (s.AlertHalfLivesElapsed ?? -1) >= 5.0)
            .OrderByDescending(s => s.AlertSeverity == "Critical" ? 2 : 1)
            .ThenByDescending(s => s.AlertHalfLivesElapsed ?? 0)
            .ToList();
    }

    [RelayCommand]
    private void SelectReport(string reportName)
    {
        SelectedReport = reportName;
        LoadReport();
    }

    [RelayCommand]
    private async Task ExportToPdf()
    {
        var sfd = new Microsoft.Win32.SaveFileDialog { Filter = "PDF Files (*.pdf)|*.pdf", FileName = $"{SelectedReport}_{DateTime.Now:yyyyMMdd}" };
        if (sfd.ShowDialog() == true)
        {
            try
            {
                if (SelectedReport == "LowActivityAlertReport")
                {
                    await _reportingService.GenerateLowActivityAlertReportPdfAsync(LowActivityAlertData, sfd.FileName);
                }
                else if (SelectedReport == "GeneralReport")
                {
                    await _reportingService.GenerateGeneralReportPdfAsync(InventoryData, BorrowingData, LowActivityData, LowActivityAlertData, sfd.FileName);
                }
                else if (SelectedReport == "InventoryReport" || SelectedReport == "ActivityReport" || SelectedReport == "LowActivityReport")
                {
                    var data = SelectedReport switch {
                        "InventoryReport" => InventoryData,
                        "ActivityReport" => ActivityData,
                        _ => LowActivityData
                    };

                    string title = SelectedReport switch {
                        "InventoryReport" => "تقرير جرد المصادر المشعة",
                        "ActivityReport" => "تقرير النشاط الإشعاعي للمصادر",
                        "LowActivityReport" => "تقرير المصادر منخفضة النشاط",
                        _ => "تقرير المصادر"
                    };

                    await _reportingService.GenerateInventoryReportPdfAsync(data, sfd.FileName, title);
                }
                else if (SelectedReport == "BorrowingReport")
                {
                    await _reportingService.GenerateBorrowHistoryPdfAsync(BorrowingData, sfd.FileName);
                }

                FileHelper.OpenFile(sfd.FileName);
            }
            catch (Exception ex)
            {
                DialogHelper.ShowError(TranslationHelper.GetFormat("MsgErrExportPdf", ex.Message));
            }
        }
    }

    [RelayCommand]
    private async Task ExportToExcel()
    {
        var sfd = new Microsoft.Win32.SaveFileDialog { Filter = "Excel Files (*.xlsx)|*.xlsx", FileName = $"{SelectedReport}_{DateTime.Now:yyyyMMdd}" };
        if (sfd.ShowDialog() == true)
        {
            try
            {
                if (SelectedReport == "LowActivityAlertReport")
                {
                    await _reportingService.GenerateLowActivityAlertReportExcelAsync(LowActivityAlertData, sfd.FileName);
                }
                else if (SelectedReport == "GeneralReport")
                {
                    await _reportingService.GenerateGeneralReportExcelAsync(InventoryData, BorrowingData, LowActivityData, LowActivityAlertData, sfd.FileName);
                }
                else if (SelectedReport == "InventoryReport" || SelectedReport == "ActivityReport" || SelectedReport == "LowActivityReport")
                {
                    var data = SelectedReport switch {
                        "InventoryReport" => InventoryData,
                        "ActivityReport" => ActivityData,
                        _ => LowActivityData
                    };

                    string title = SelectedReport switch {
                        "InventoryReport" => "جرد المصادر",
                        "ActivityReport" => "نشاط المصادر",
                        "LowActivityReport" => "المصادر المنخفضة",
                        _ => "تقرير المصادر"
                    };

                    await _reportingService.GenerateInventoryReportExcelAsync(data, sfd.FileName, title);
                }
                else if (SelectedReport == "BorrowingReport")
                {
                    await _reportingService.GenerateBorrowHistoryExcelAsync(BorrowingData, sfd.FileName);
                }

                FileHelper.OpenFile(sfd.FileName);
            }
            catch (Exception ex)
            {
                DialogHelper.ShowError(TranslationHelper.GetFormat("MsgErrExportExcel", ex.Message));
            }
        }
    }

    // ───────────── دالة مساعدة: احتساب أعلى عدد فترات نصف عمر منقضية ورمز النظير الأسوأ ─────────────
    public static (double MaxHalfLives, string WorstIsotopeSymbol) CalculateMaxHalfLivesElapsed(Source source)
    {
        double max = -1;
        string worstIsotope = string.Empty;

        if (source.HasDetailedIsotopes &&
            source.SourceIsotopes != null &&
            source.SourceIsotopes.Any(si => si.Radioisotope != null))
        {
            foreach (var si in source.SourceIsotopes.Where(si => si.Radioisotope != null))
            {
                var isotope = si.Radioisotope!;
                var calibDate = si.CalibrationDate ?? source.CalibrationDate;
                if (calibDate == default) continue;

                double halfLifeSec = ConvertHalfLifeToSeconds(isotope.HalfLife, isotope.HalfLifeUnit);
                if (halfLifeSec <= 0) continue;

                double elapsed = Math.Max(0, (DateTime.Now - calibDate).TotalSeconds);
                double hl = elapsed / halfLifeSec;
                if (hl > max)
                {
                    max = hl;
                    worstIsotope = isotope.Symbol;
                }
            }
        }
        else if (source.Radioisotope != null && source.CalibrationDate != default)
        {
            double halfLifeSec = ConvertHalfLifeToSeconds(
                source.Radioisotope.HalfLife, source.Radioisotope.HalfLifeUnit);
            if (halfLifeSec > 0)
            {
                double elapsed = Math.Max(0, (DateTime.Now - source.CalibrationDate).TotalSeconds);
                max = elapsed / halfLifeSec;
                worstIsotope = source.Radioisotope.Symbol;
            }
        }

        return (max, worstIsotope);
    }

    public static double ConvertHalfLifeToSeconds(double value, string? unit) =>
        unit?.ToLower() switch
        {
            "seconds" or "second" or "s" => value,
            "minutes" or "minute" or "min" or "m" => value * 60,
            "hours" or "hour" or "h" => value * 3600,
            "days" or "day" or "d" => value * 86400,
            "months" or "month" or "mo" => value * 30 * 86400,
            "years" or "year" or "yr" or "y" => value * 365.25 * 86400,
            _ => value * 365.25 * 86400
        };
}
