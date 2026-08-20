using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sources.Models;
using Sources.Services;
using Sources.Interfaces;
using Sources.Helpers;
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
    public string SourceCode => Source.SourceCode;
    public string DisplayIsotopes => Source.DisplayIsotopes;
    public string CurrentActivityWithUnit => Source.CurrentActivityWithUnit;
    public string ArabicStatus => Source.ArabicStatus;
    public DateTime CalibrationDate => Source.CalibrationDate;
    public string? SerialNumber => Source.SerialNumber;
    public string? Manufacturer => Source.Manufacturer;
}

public partial class LocationsViewModel : ObservableObject, IEditableViewModel
{
    private readonly ILocationService _service;
    private readonly IReportingService? _reportingService;

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

    public LocationsViewModel(ILocationService service, IReportingService? reportingService = null)
    {
        _service = service;
        _reportingService = reportingService ?? (App.ServiceProvider?.GetService(typeof(IReportingService)) as IReportingService);
        LoadData();
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
        if (target == null) return;
        var r = _service.Delete(target.Id);
        ShowMsg(r.Message);
        if (r.Success) LoadData();
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
    private void ClearForm() { EditName = EditType = EditBuilding = EditRoom = EditPerson = string.Empty; }
    private void ShowMsg(string m) { Message = m; HasMessage = true; }
}
