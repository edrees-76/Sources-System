using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using Sources.Data;
using Sources.Models;
using Sources.Services;
using Sources.Helpers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace Sources.ViewModels;

/// <summary>
/// صفوف العرض المخصصة لجداول مركز التقارير لضمان ثبات الترقيم التسلسلي # لكل تقرير
/// </summary>
public class ReportInventoryRow
{
    public int RowNumber { get; set; }
    public Source Source { get; set; } = null!;
    public string SourceCode => Source.SourceCode;
    public string DisplayIsotopes => Source.DisplayIsotopes;
    public string InitialActivityWithUnit => Source.InitialActivityWithUnit;
    public string CurrentActivityWithUnit => Source.CurrentActivityWithUnit;
    public string DisplayDoseRate => Source.DisplayDoseRate;
    public string DoseRateTooltip => Source.DoseRateTooltip;
    public Location? Location => Source.Location;
    public string Status => Source.Status;
}

public class ReportBorrowingRow
{
    public int RowNumber { get; set; }
    public BorrowRequest Request { get; set; } = null!;
    public Source? Source => Request.Source;
    public string SourceCode => Request.DisplaySourceCode;
    public string DisplayBorrowerName => Request.DisplayBorrowerName;
    public DateTime RequestDate => Request.RequestDate;
    public DateTime ExpectedReturnDate => Request.ExpectedReturnDate;
    public string ArabicStatus => Request.ArabicStatus;
}

public class ReportActivityRow
{
    public int RowNumber { get; set; }
    public Source Source { get; set; } = null!;
    public string SourceCode => Source.SourceCode;
    public string DisplayIsotopes => Source.DisplayIsotopes;
    public string InitialActivityWithUnit => Source.InitialActivityWithUnit;
    public string CurrentActivityWithUnit => Source.CurrentActivityWithUnit;
    public string DisplayDoseRate => Source.DisplayDoseRate;
    public string DoseRateTooltip => Source.DoseRateTooltip;
    public DateTime CalibrationDate => Source.CalibrationDate;
}

public class ReportLowActivityRow
{
    public int RowNumber { get; set; }
    public Source Source { get; set; } = null!;
    public string SourceCode => Source.SourceCode;
    public string DisplayIsotopes => Source.DisplayIsotopes;
    public string CurrentActivityWithUnit => Source.CurrentActivityWithUnit;
    public string DisplayDoseRate => Source.DisplayDoseRate;
    public string DoseRateTooltip => Source.DoseRateTooltip;
    public Location? Location => Source.Location;
}

public class ReportLowActivityAlertRow
{
    public int RowNumber { get; set; }
    public Source Source { get; set; } = null!;
    public string SourceCode => Source.SourceCode;
    public string? AlertWorstIsotope => Source.AlertWorstIsotope;
    public string? AlertSeverity => Source.AlertSeverity;
    public string AlertSeverityDisplay => Source.AlertSeverityDisplay;
    public DateTime CalibrationDate => Source.CalibrationDate;
    public string ArabicStatus => Source.ArabicStatus;
    public string CurrentActivityWithUnit => Source.CurrentActivityWithUnit;
    public string DisplayDoseRate => Source.DisplayDoseRate;
    public string DoseRateTooltip => Source.DoseRateTooltip;
}

public class ReportFailedLeakTestRow
{
    public int RowNumber { get; set; }
    public Source Source { get; set; } = null!;
    public LeakTestRecord LeakTestRecord { get; set; } = null!;
    public string SourceCode => Source.SourceCode;
    public string DisplayIsotopes => Source.DisplayIsotopes;
    public Location? Location => Source.Location;
    public DateTime FailedTestDate => LeakTestRecord.TestDate;
    public string ArabicStatus => Source.ArabicStatus;
    public string Status => Source.Status;
    public string? TestNotes => LeakTestRecord.Notes;
    public string DisplayDoseRate => Source.DisplayDoseRate;
    public string DoseRateTooltip => Source.DoseRateTooltip;
}

/// <summary>
/// صف عرض مخصص لجدول تقرير جرد المصادر النيترونية لضمان ثبات الترقيم التسلسلي #
/// </summary>
public class ReportNeutronInventoryRow
{
    public int RowNumber { get; set; }
    public NeutronSource Source { get; set; } = null!;
    public string SourceCode => Source.SourceCode;
    public string TypeCode => Source.NeutronSourceType?.Code ?? "-";
    public string TypeName => Source.NeutronSourceType?.NameAr ?? Source.NeutronSourceType?.NameEn ?? "-";
    public double EmissionRate => Source.EmissionRate;
    public string RelativeUncertainty => Source.RelativeExpandedUncertaintyPercent.HasValue ? $"{Source.RelativeExpandedUncertaintyPercent:N1}%" : "-";
    public Location? Location => Source.Location;
    public string Status => Source.Status;
    public string ArabicStatus => Source.ArabicStatus;
    public DateTime? CalibrationDate => Source.CalibrationDate;
}

public partial class ReportsViewModel : ObservableObject
{
    private readonly ISourceService _sourceService;
    private readonly IBorrowService _borrowService;
    private readonly IReportingService _reportingService;
    private readonly ISystemSettingsService _settingsService;
    private readonly IDbContextFactory<AppDbContext>? _dbFactory;
    private readonly INeutronSourceService? _neutronSourceService;

    [ObservableProperty] private string _selectedReport = "InventoryReport";
    [ObservableProperty] private ObservableCollection<ReportInventoryRow> _inventoryData = new();
    [ObservableProperty] private ObservableCollection<ReportBorrowingRow> _borrowingData = new();
    [ObservableProperty] private ObservableCollection<ReportActivityRow> _activityData = new();
    [ObservableProperty] private ObservableCollection<ReportLowActivityRow> _lowActivityData = new();
    [ObservableProperty] private ObservableCollection<ReportNeutronInventoryRow> _neutronInventoryData = new();
    [ObservableProperty] private double _lowActivityThreshold = 10;
    [ObservableProperty] private ObservableCollection<ReportLowActivityAlertRow> _lowActivityAlertData = new();
    [ObservableProperty] private ObservableCollection<ReportFailedLeakTestRow> _failedLeakTestsData = new();

    public ReportsViewModel(
        ISourceService sourceService, 
        IBorrowService borrowService, 
        IReportingService reportingService,
        ISystemSettingsService settingsService,
        IDbContextFactory<AppDbContext>? dbFactory = null,
        INeutronSourceService? neutronSourceService = null)
    {
        _sourceService = sourceService;
        _borrowService = borrowService;
        _reportingService = reportingService;
        _settingsService = settingsService;
        _dbFactory = dbFactory;
        _neutronSourceService = neutronSourceService;
        
        // جلب عتبة النشاط المنخفض من الإعدادات
        LowActivityThreshold = _settingsService.GetSetting("LowActivityThresholdPercent", 10.0);
        
        LoadReport();
    }

    partial void OnSelectedReportChanged(string value) => LoadReport();

    [RelayCommand]
    public void LoadReport()
    {
        var allSources = _sourceService.GetAllSources() ?? new List<Source>();
        
        switch (SelectedReport)
        {
            case "InventoryReport":
                InventoryData = new ObservableCollection<ReportInventoryRow>(
                    allSources.OrderBy(s => s.SourceCode).Select((s, index) => new ReportInventoryRow { RowNumber = index + 1, Source = s }));
                break;
            case "BorrowingReport":
                var borrows = _borrowService.GetAll() ?? new List<BorrowRequest>();
                BorrowingData = new ObservableCollection<ReportBorrowingRow>(
                    borrows.Select((b, index) => new ReportBorrowingRow { RowNumber = index + 1, Request = b }));
                break;
            case "ActivityReport":
                var activeSources = allSources.Where(s => s.Status == "InUse" || s.Status == "Storage").OrderBy(s => s.SourceCode).ToList();
                ActivityData = new ObservableCollection<ReportActivityRow>(
                    activeSources.Select((s, index) => new ReportActivityRow { RowNumber = index + 1, Source = s }));
                break;
            case "LowActivityReport":
                var lowSources = _sourceService.GetLowActivitySources(LowActivityThreshold) ?? new List<Source>();
                LowActivityData = new ObservableCollection<ReportLowActivityRow>(
                    lowSources.Select((s, index) => new ReportLowActivityRow { RowNumber = index + 1, Source = s }));
                break;
            case "LowActivityAlertReport":
                // تصفية وتصنيف المصادر التي تجاوزت عتبات نصف العمر (T½)
                var alertSources = GetLowActivityAlertSources(allSources);
                LowActivityAlertData = new ObservableCollection<ReportLowActivityAlertRow>(
                    alertSources.Select((s, index) => new ReportLowActivityAlertRow { RowNumber = index + 1, Source = s }));
                break;
            case "FailedLeakTestsReport":
                FailedLeakTestsData = new ObservableCollection<ReportFailedLeakTestRow>(GetFailedLeakTestsRows());
                break;
            case "NeutronInventoryReport":
                var neutronSources = _neutronSourceService?.GetAll() ?? new List<NeutronSource>();
                NeutronInventoryData = new ObservableCollection<ReportNeutronInventoryRow>(
                    neutronSources.OrderBy(s => s.SourceCode).Select((s, index) => new ReportNeutronInventoryRow { RowNumber = index + 1, Source = s }));
                break;
            case "GeneralReport":
                InventoryData = new ObservableCollection<ReportInventoryRow>(
                    allSources.OrderBy(s => s.SourceCode).Select((s, index) => new ReportInventoryRow { RowNumber = index + 1, Source = s }));
                BorrowingData = new ObservableCollection<ReportBorrowingRow>(
                    (_borrowService.GetAll() ?? new List<BorrowRequest>()).Select((b, index) => new ReportBorrowingRow { RowNumber = index + 1, Request = b }));
                ActivityData = new ObservableCollection<ReportActivityRow>(
                    allSources.Where(s => s.Status == "InUse" || s.Status == "Storage").OrderBy(s => s.SourceCode).Select((s, index) => new ReportActivityRow { RowNumber = index + 1, Source = s }));
                LowActivityData = new ObservableCollection<ReportLowActivityRow>(
                    (_sourceService.GetLowActivitySources(LowActivityThreshold) ?? new List<Source>()).Select((s, index) => new ReportLowActivityRow { RowNumber = index + 1, Source = s }));
                LowActivityAlertData = new ObservableCollection<ReportLowActivityAlertRow>(
                    GetLowActivityAlertSources(allSources).Select((s, index) => new ReportLowActivityAlertRow { RowNumber = index + 1, Source = s }));
                FailedLeakTestsData = new ObservableCollection<ReportFailedLeakTestRow>(GetFailedLeakTestsRows());
                break;
        }
    }

    private List<ReportFailedLeakTestRow> GetFailedLeakTestsRows()
    {
        if (_dbFactory != null)
        {
            using var db = _dbFactory.CreateDbContext();
            var activeSources = db.Sources
                .AsNoTracking()
                .Include(s => s.Location)
                .Include(s => s.Radioisotope)
                .Include(s => s.SourceIsotopes).ThenInclude(si => si.Radioisotope)
                .Include(s => s.SourceIsotopes).ThenInclude(si => si.ActivityUnit)
                .Include(s => s.InitialActivityUnit)
                .Include(s => s.CurrentActivityUnit)
                .Where(s => !s.IsDeleted)
                .OrderBy(s => s.SourceCode)
                .ToList();

            var sourceIds = activeSources.Select(s => s.Id).ToList();
            var allTests = db.LeakTestRecords
                .AsNoTracking()
                .Where(r => sourceIds.Contains(r.SourceId))
                .ToList();

            var latestTestsBySource = allTests
                .GroupBy(r => r.SourceId)
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderByDescending(r => r.TestDate).ThenByDescending(r => r.CreatedAt).FirstOrDefault()
                );

            var rows = new List<ReportFailedLeakTestRow>();
            int index = 1;
            foreach (var s in activeSources)
            {
                if (latestTestsBySource.TryGetValue(s.Id, out var latest) && latest != null && latest.Result == "Fail")
                {
                    latest.Source = s;
                    rows.Add(new ReportFailedLeakTestRow
                    {
                        RowNumber = index++,
                        Source = s,
                        LeakTestRecord = latest
                    });
                }
            }
            return rows;
        }

        // Fallback using _sourceService when dbFactory is unavailable (e.g. mocked unit tests)
        var allSourcesFallback = _sourceService.GetAllSources() ?? new List<Source>();
        var activeSourcesFallback = allSourcesFallback.Where(s => !s.IsDeleted).OrderBy(s => s.SourceCode).ToList();
        var fallbackRows = new List<ReportFailedLeakTestRow>();
        int fallbackIndex = 1;
        foreach (var s in activeSourcesFallback)
        {
            var latest = s.LeakTestRecords?
                .OrderByDescending(r => r.TestDate)
                .ThenByDescending(r => r.CreatedAt)
                .FirstOrDefault();
            if (latest != null && latest.Result == "Fail")
            {
                latest.Source = s;
                fallbackRows.Add(new ReportFailedLeakTestRow
                {
                    RowNumber = fallbackIndex++,
                    Source = s,
                    LeakTestRecord = latest
                });
            }
        }
        return fallbackRows;
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
            .ThenBy(s => s.SourceCode)
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
                    await _reportingService.GenerateLowActivityAlertReportPdfAsync(LowActivityAlertData.Select(r => r.Source), sfd.FileName);
                }
                else if (SelectedReport == "FailedLeakTestsReport")
                {
                    await _reportingService.GenerateFailedLeakTestsReportPdfAsync(FailedLeakTestsData.Select(r => r.LeakTestRecord), sfd.FileName);
                }
                else if (SelectedReport == "GeneralReport")
                {
                    await _reportingService.GenerateGeneralReportPdfAsync(
                        InventoryData.Select(r => r.Source),
                        BorrowingData.Select(r => r.Request),
                        LowActivityData.Select(r => r.Source),
                        LowActivityAlertData.Select(r => r.Source),
                        sfd.FileName);
                }
                else if (SelectedReport == "InventoryReport" || SelectedReport == "ActivityReport" || SelectedReport == "LowActivityReport")
                {
                    IEnumerable<Source> data = SelectedReport switch {
                        "InventoryReport" => InventoryData.Select(r => r.Source),
                        "ActivityReport" => ActivityData.Select(r => r.Source),
                        _ => LowActivityData.Select(r => r.Source)
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
                    await _reportingService.GenerateBorrowHistoryPdfAsync(BorrowingData.Select(r => r.Request), sfd.FileName);
                }
                else if (SelectedReport == "NeutronInventoryReport")
                {
                    await _reportingService.GenerateNeutronInventoryReportPdfAsync(NeutronInventoryData.Select(r => r.Source), sfd.FileName);
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
                    await _reportingService.GenerateLowActivityAlertReportExcelAsync(LowActivityAlertData.Select(r => r.Source), sfd.FileName);
                }
                else if (SelectedReport == "FailedLeakTestsReport")
                {
                    await _reportingService.GenerateFailedLeakTestsReportExcelAsync(FailedLeakTestsData.Select(r => r.LeakTestRecord), sfd.FileName);
                }
                else if (SelectedReport == "GeneralReport")
                {
                    await _reportingService.GenerateGeneralReportExcelAsync(
                        InventoryData.Select(r => r.Source),
                        BorrowingData.Select(r => r.Request),
                        LowActivityData.Select(r => r.Source),
                        LowActivityAlertData.Select(r => r.Source),
                        sfd.FileName);
                }
                else if (SelectedReport == "InventoryReport" || SelectedReport == "ActivityReport" || SelectedReport == "LowActivityReport")
                {
                    IEnumerable<Source> data = SelectedReport switch {
                        "InventoryReport" => InventoryData.Select(r => r.Source),
                        "ActivityReport" => ActivityData.Select(r => r.Source),
                        _ => LowActivityData.Select(r => r.Source)
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
                    await _reportingService.GenerateBorrowHistoryExcelAsync(BorrowingData.Select(r => r.Request), sfd.FileName);
                }
                else if (SelectedReport == "NeutronInventoryReport")
                {
                    await _reportingService.GenerateNeutronInventoryReportExcelAsync(NeutronInventoryData.Select(r => r.Source), sfd.FileName);
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

    [RelayCommand]
    private void ViewSourceDetails(object? parameter)
    {
        SourceNavigationHelper.OpenSourceDetails(parameter);
    }
}
