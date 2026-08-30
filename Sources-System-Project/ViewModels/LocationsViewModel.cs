using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Sources.Models;
using Sources.Services;
using Sources.Interfaces;
using Sources.Helpers;
using Sources.Messages;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace Sources.ViewModels;

/// <summary>
/// صف عرض مخصص لجدول المواقع يضمن ثبات الرقم التسلسلي # أثناء التمرير وإعادة تدوير الصفوف
/// </summary>
public class LocationRow
{
    public int RowNumber { get; set; }
    public Location Location { get; set; } = null!;
    public Guid Id => Location.Id;
    public string LocationName => Location.LocationName;
    public string? LocationType => Location.LocationType;
    public string? Building => Location.Building;
    public string? Room => Location.Room;
    public string? ResponsiblePerson => Location.ResponsiblePerson;
    public string? AddedBy => Location.AddedBy;
    public int SourceCount => Location.SourceCount;
}

/// <summary>
/// صف عرض مخصص لقائمة المصادر المرتبطة بموقع معين في نافذة التفاصيل
/// </summary>
public class LocationSourceRow
{
    public int RowNumber { get; set; }
    public Source Source { get; set; } = null!;
    public Guid Id => Source.Id;
    public string DisplaySourceCode => Source.DisplaySourceCode;
    public string SourceCode => Source.DisplaySourceCode;
    public string DisplayIsotopes => Source.DisplayIsotopes;
    public string CurrentActivityWithUnit => Source.CurrentActivityWithUnit;
    public string DisplayDoseRate => Source.DisplayDoseRate;
    public string DoseRateTooltip => Source.DoseRateTooltip;
    public string ArabicStatus => Source.ArabicStatus;
    public DateTime CalibrationDate => Source.CalibrationDate;
    public string? SerialNumber => Source.SerialNumber;
    public string? Manufacturer => Source.Manufacturer;
}

/// <summary>
/// صف عرض مخصص لقائمة المصادر النيترونية المرتبطة بموقع معين في نافذة التفاصيل
/// </summary>
public class LocationNeutronSourceRow
{
    public int RowNumber { get; set; }
    public NeutronSource NeutronSource { get; set; } = null!;
    public Guid Id => NeutronSource.Id;
    public string SourceCode => NeutronSource.SourceCode;
    public string TypeCode => NeutronSource.NeutronSourceType?.Code ?? "-";
    public string TypeNameAr => NeutronSource.NeutronSourceType?.NameAr ?? "-";
    public string EmissionRateFormatted => $"{NeutronSource.EmissionRate:N2} n/s";
    public string UncertaintyFormatted => NeutronSource.RelativeExpandedUncertaintyPercent.HasValue ? $"{NeutronSource.RelativeExpandedUncertaintyPercent.Value:N1}%" : "-";
    public string ArabicStatus => NeutronSource.ArabicStatus;
    public string StatusColor => NeutronSource.StatusColor;
    public string CalibrationDateFormatted => NeutronSource.CalibrationDate?.ToString("yyyy-MM-dd") ?? "-";
    public string SerialNumber => !string.IsNullOrWhiteSpace(NeutronSource.SerialNumber) ? NeutronSource.SerialNumber : "-";
}

public partial class LocationsViewModel : ObservableObject, IEditableViewModel
{
    private readonly ILocationService _service;
    private readonly IReportingService? _reportingService;
    private readonly INeutronSourceService? _neutronSourceService;

    [ObservableProperty] private ObservableCollection<LocationRow> _locations = new();
    [ObservableProperty] private LocationRow? _selected;
    [ObservableProperty] private bool _isEditing;
    [ObservableProperty] private bool _isNew;
    [ObservableProperty] private string _message = string.Empty;
    [ObservableProperty] private bool _hasMessage;

    [ObservableProperty] private string _editName = string.Empty;
    [ObservableProperty] private string _editType = string.Empty;
    [ObservableProperty] private string _editBuilding = string.Empty;
    [ObservableProperty] private string _editRoom = string.Empty;
    [ObservableProperty] private string _editPerson = string.Empty;
    private Guid? _editingId;

    // ─── تفاصيل الموقع والمصادر المرتبطة ───
    [ObservableProperty] private ObservableCollection<LocationSourceRow> _linkedSourcesForDetails = new();
    [ObservableProperty] private Location? _selectedLocationForDetails;
    [ObservableProperty] private bool _isLocationDetailsOpen;
    [ObservableProperty] private bool _hasLinkedSources;

    public LocationsViewModel(
        ILocationService service, 
        IReportingService? reportingService = null,
        INeutronSourceService? neutronSourceService = null)
    {
        _service = service;
        _reportingService = reportingService ?? (App.ServiceProvider?.GetService(typeof(IReportingService)) as IReportingService);
        _neutronSourceService = neutronSourceService ?? (App.ServiceProvider?.GetService(typeof(INeutronSourceService)) as INeutronSourceService);
        LoadData();

        CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default.Register<Sources.Messages.NavigateToSearchResultMessage>(this, (r, m) =>
        {
            if (m.Category == SearchCategory.Locations)
            {
                SelectLocationById(m.EntityId);
            }
        });
    }

    public void SelectLocationById(Guid locationId)
    {
        var locRow = Locations.FirstOrDefault(l => l.Id == locationId);
        if (locRow == null)
        {
            LoadData();
            locRow = Locations.FirstOrDefault(l => l.Id == locationId);
        }

        if (locRow != null)
        {
            Selected = locRow;
            ViewLocationDetails(locRow);
        }
    }

    [RelayCommand]
    public void LoadData()
    {
        var raw = _service.GetAll();
        Locations = new ObservableCollection<LocationRow>(
            raw.Select((loc, index) => new LocationRow
            {
                RowNumber = index + 1,
                Location = loc
            }));
    }

    [RelayCommand]
    private void AddNew()
    {
        IsNew = true;
        _editingId = null;
        ClearForm();
        IsEditing = true;
    }

    [RelayCommand]
    private void Edit(object? param = null)
    {
        Location? target = param switch
        {
            LocationRow lr => lr.Location,
            Location l => l,
            _ => Selected?.Location
        };
        if (target == null) return;
        IsNew = false;
        _editingId = target.Id;
        EditName = target.LocationName;
        EditType = target.LocationType ?? "";
        EditBuilding = target.Building ?? "";
        EditRoom = target.Room ?? "";
        EditPerson = target.ResponsiblePerson ?? "";
        IsEditing = true;
    }

    [RelayCommand]
    private void Save()
    {
        if (string.IsNullOrWhiteSpace(EditName)) { ShowMsg(TranslationHelper.GetString("MsgErrLocationNameReq")); return; }
        var item = new Location
        {
            Id = IsNew ? Guid.NewGuid() : _editingId!.Value,
            LocationName = EditName,
            LocationType = EditType,
            Building = EditBuilding,
            Room = EditRoom,
            ResponsiblePerson = EditPerson
        };
        var r = IsNew ? _service.Create(item) : _service.Update(item);
        ShowMsg(r.Message);
        if (r.Success) { IsEditing = false; LoadData(); }
    }

    [RelayCommand]
    private void Delete(object? param = null)
    {
        Location? target = param switch
        {
            LocationRow lr => lr.Location,
            Location l => l,
            _ => Selected?.Location
        };
        if (target == null)
        {
            DialogHelper.ShowWarning(TranslationHelper.GetString("MsgSelectLocationFirst") ?? "الرجاء تحديد موقع أولاً");
            return;
        }

        string confirmMsg = TranslationHelper.GetString("MsgConfirmDeleteLocation") ?? "هل أنت متأكد من حذف هذا الموقع؟";
        string confirmTitle = TranslationHelper.GetString("AlertConfirmation") ?? "تأكيد الحذف";
        if (!DialogHelper.ShowConfirmation(confirmMsg, confirmTitle)) return;

        var r = _service.Delete(target.Id);
        ShowMsg(r.Message);
        if (!r.Success)
        {
            DialogHelper.ShowError(r.Message);
        }
        else
        {
            LoadData();
        }
    }

    [RelayCommand]
    private void ViewLocationDetails(object? param = null)
    {
        Location? target = param switch
        {
            LocationRow lr => lr.Location,
            Location l => l,
            _ => Selected?.Location
        };
        if (target == null) return;

        SelectedLocationForDetails = target;
        var sources = _service.GetSourcesLinkedToLocation(target.Id);
        LinkedSourcesForDetails = new ObservableCollection<LocationSourceRow>(
            sources.Select((src, index) => new LocationSourceRow
            {
                RowNumber = index + 1,
                Source = src
            }));
        HasLinkedSources = LinkedSourcesForDetails.Count > 0;
        IsLocationDetailsOpen = true;

        OpenLocationDetailsWindow(target, sources);
    }

    public Action<Location, IEnumerable<Source>>? OpenDetailsWindowCustomAction { get; set; }

    private void OpenLocationDetailsWindow(Location target, IEnumerable<Source> sources)
    {
        if (OpenDetailsWindowCustomAction != null)
        {
            OpenDetailsWindowCustomAction(target, sources);
            return;
        }

        if (DialogHelper.IsTestMode) return;

        var app = System.Windows.Application.Current;
        if (app == null) return;

        if (app.Dispatcher != null && !app.Dispatcher.CheckAccess())
        {
            app.Dispatcher.BeginInvoke(() => OpenLocationDetailsWindow(target, sources));
            return;
        }

        try
        {
            if (app.MainWindow == null || !app.MainWindow.IsVisible) return;

            var neutronSources = _neutronSourceService?.GetByLocation(target.Id);
            var detailsVm = new LocationDetailsViewModel(target, sources, _reportingService, neutronSources, _neutronSourceService);
            var win = new Views.LocationDetailsWindow(detailsVm)
            {
                Owner = app.MainWindow
            };
            win.ShowDialog();
        }
        catch (Exception ex)
        {
            LoggerService.LogError("LocationsViewModel: Failed to open LocationDetailsWindow", ex);
        }
    }

    [RelayCommand]
    private void CloseLocationDetails()
    {
        IsLocationDetailsOpen = false;
        SelectedLocationForDetails = null;
        LinkedSourcesForDetails.Clear();
        HasLinkedSources = false;
    }

    [RelayCommand]
    private async Task ExportToPdfAsync()
    {
        if (_reportingService == null) return;
        var sfd = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "PDF Files (*.pdf)|*.pdf",
            FileName = $"المواقع_والمخازن_{DateTime.Now:yyyyMMdd}.pdf"
        };
        if (sfd.ShowDialog() == true)
        {
            try
            {
                var list = Locations.Select(lr => lr.Location).ToList();
                await _reportingService.GenerateLocationsReportPdfAsync(list, sfd.FileName);
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
    private async Task ExportToExcelAsync()
    {
        if (_reportingService == null) return;
        var sfd = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "Excel Files (*.xlsx)|*.xlsx",
            FileName = $"المواقع_والمخازن_{DateTime.Now:yyyyMMdd}.xlsx"
        };
        if (sfd.ShowDialog() == true)
        {
            try
            {
                var list = Locations.Select(lr => lr.Location).ToList();
                await _reportingService.GenerateLocationsReportExcelAsync(list, sfd.FileName);
                FileHelper.OpenFile(sfd.FileName);
                DialogHelper.ShowInfo(TranslationHelper.GetString("MsgExportSuccess") ?? "تم تصدير البيانات إلى ملف Excel بنجاح.");
            }
            catch (Exception ex)
            {
                DialogHelper.ShowError(TranslationHelper.GetFormat("MsgErrExportExcel", ex.Message));
            }
        }
    }

    [RelayCommand] private void CancelEdit() { IsEditing = false; ClearForm(); }
    [RelayCommand] private void CloseMessage() { HasMessage = false; Message = string.Empty; }
    private void ClearForm() { EditName = EditType = EditBuilding = EditRoom = EditPerson = string.Empty; }
    private void ShowMsg(string m) { Message = m; HasMessage = true; }
}
