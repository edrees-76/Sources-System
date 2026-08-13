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
    private readonly IDecayCalculationService _decayService;
    private readonly IReportingService _reportingService;
    private readonly ISystemSettingsService _settingsService;

    [ObservableProperty] private string _selectedReport = "InventoryReport";
    [ObservableProperty] private ObservableCollection<Source> _inventoryData = new();
    [ObservableProperty] private ObservableCollection<BorrowRequest> _borrowingData = new();
    [ObservableProperty] private ObservableCollection<Source> _activityData = new();
    [ObservableProperty] private ObservableCollection<Source> _lowActivityData = new();
    [ObservableProperty] private double _lowActivityThreshold = 10;
    [ObservableProperty] private ObservableCollection<Source> _calibrationData = new();

    public ReportsViewModel(
        ISourceService sourceService, 
        IBorrowService borrowService, 
        IDecayCalculationService decayService, 
        IReportingService reportingService,
        ISystemSettingsService settingsService)
    {
        _sourceService = sourceService;
        _borrowService = borrowService;
        _decayService = decayService;
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
                ActivityData = new ObservableCollection<Source>(allSources.Where(s => s.Status == "Active" || s.Status == "Storage" || s.Status == "Borrowed"));
                break;
            case "LowActivityReport":
                LowActivityData = new ObservableCollection<Source>(_sourceService.GetLowActivitySources(LowActivityThreshold));
                break;
            case "CalibrationReport":
                // تصفية المصادر النشطة التي انتهت معايرتها أو تقترب من الانتهاء (استخدام العتبة الديناميكية)
                var today = DateTime.Now;
                var calibThreshold = _settingsService.GetSetting("CalibrationThresholdDays", 730);
                
                CalibrationData = new ObservableCollection<Source>(
                    allSources.Where(s => (s.Status == "Active" || s.Status == "Storage" || s.Status == "Borrowed") && s.CalibrationDate != default &&
                                         (today - s.CalibrationDate).TotalDays >= (calibThreshold - 60)) 
                              .OrderBy(s => s.CalibrationDate));
                break;
            case "GeneralReport":
                InventoryData = new ObservableCollection<Source>(allSources);
                BorrowingData = new ObservableCollection<BorrowRequest>(_borrowService.GetAll());
                ActivityData = new ObservableCollection<Source>(allSources.Where(s => s.Status == "Active" || s.Status == "Storage" || s.Status == "Borrowed"));
                LowActivityData = new ObservableCollection<Source>(_sourceService.GetLowActivitySources(LowActivityThreshold));
                
                var t = DateTime.Now;
                var cThreshold = _settingsService.GetSetting("CalibrationThresholdDays", 730);
                CalibrationData = new ObservableCollection<Source>(
                    allSources.Where(s => (s.Status == "Active" || s.Status == "Storage" || s.Status == "Borrowed") && s.CalibrationDate != default &&
                                         (t - s.CalibrationDate).TotalDays >= (cThreshold - 60)) 
                              .OrderBy(s => s.CalibrationDate));
                break;
        }
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
                if (SelectedReport == "CalibrationReport")
                {
                    await _reportingService.GenerateCalibrationReportPdfAsync(CalibrationData, sfd.FileName);
                }
                else if (SelectedReport == "GeneralReport")
                {
                    await _reportingService.GenerateGeneralReportPdfAsync(InventoryData, BorrowingData, LowActivityData, CalibrationData, sfd.FileName);
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
                if (SelectedReport == "CalibrationReport")
                {
                    await _reportingService.GenerateCalibrationReportExcelAsync(CalibrationData, sfd.FileName);
                }
                else if (SelectedReport == "GeneralReport")
                {
                    await _reportingService.GenerateGeneralReportExcelAsync(InventoryData, BorrowingData, LowActivityData, CalibrationData, sfd.FileName);
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
}
