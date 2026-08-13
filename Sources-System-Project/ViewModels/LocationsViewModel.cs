using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sources.Models;
using Sources.Services;
using Sources.Interfaces;
using Sources.Helpers;
using System;
using System.Collections.ObjectModel;

namespace Sources.ViewModels;

public partial class LocationsViewModel : ObservableObject, IEditableViewModel
{
    private readonly ILocationService _service;
    [ObservableProperty] private ObservableCollection<Location> _locations = new();
    [ObservableProperty] private Location? _selected;
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

    public LocationsViewModel(ILocationService service) { _service = service; LoadData(); }

    [RelayCommand] public void LoadData() => Locations = new ObservableCollection<Location>(_service.GetAll());

    [RelayCommand] private void AddNew() { IsNew = true; _editingId = null; ClearForm(); IsEditing = true; }

    [RelayCommand]
    private void Edit()
    {
        if (Selected == null) return;
        IsNew = false; _editingId = Selected.Id;
        EditName = Selected.LocationName; EditType = Selected.LocationType ?? "";
        EditBuilding = Selected.Building ?? ""; EditRoom = Selected.Room ?? "";
        EditPerson = Selected.ResponsiblePerson ?? "";
        IsEditing = true;
    }

    [RelayCommand]
    private void Save()
    {
        if (string.IsNullOrWhiteSpace(EditName)) { ShowMsg(TranslationHelper.GetString("MsgErrLocationNameReq")); return; }
        var item = new Location
        {
            Id = IsNew ? Guid.NewGuid() : _editingId!.Value,
            LocationName = EditName, LocationType = EditType, Building = EditBuilding,
            Room = EditRoom, ResponsiblePerson = EditPerson
        };
        var r = IsNew ? _service.Create(item) : _service.Update(item);
        ShowMsg(r.Message);
        if (r.Success) { IsEditing = false; LoadData(); }
    }

    [RelayCommand]
    private void Delete()
    {
        if (Selected == null) return;
        var r = _service.Delete(Selected.Id);
        ShowMsg(r.Message);
        if (r.Success) LoadData();
    }

    [RelayCommand] private void CancelEdit() { IsEditing = false; ClearForm(); }
    private void ClearForm() { EditName = EditType = EditBuilding = EditRoom = EditPerson = string.Empty; }
    private void ShowMsg(string m) { Message = m; HasMessage = true; }
}
