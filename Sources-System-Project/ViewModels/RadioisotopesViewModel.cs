using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sources.Models;
using Sources.Services;
using Sources.Interfaces;
using System;
using System.Collections.ObjectModel;

namespace Sources.ViewModels;

public partial class RadioisotopesViewModel : ObservableObject, IEditableViewModel
{
    private readonly IRadioisotopeService _service;

    [ObservableProperty] private ObservableCollection<Radioisotope> _radioisotopes = new();
    [ObservableProperty] 
    [NotifyCanExecuteChangedFor(nameof(EditCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteCommand))]
    private Radioisotope? _selected;
    [ObservableProperty] private bool _isEditing;
    [ObservableProperty] private bool _isNew;
    [ObservableProperty] private string _message = string.Empty;
    [ObservableProperty] private bool _hasMessage;
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private int _currentStep = 1;
    public int TotalSteps => 2;

    private List<Radioisotope> _allRadioisotopes = new();

    [ObservableProperty] private string _editName = string.Empty;
    [ObservableProperty] private string _editArabicName = string.Empty;
    [ObservableProperty] private string _editSymbol = string.Empty;
    [ObservableProperty] private string _editRadiationType = string.Empty;
    [ObservableProperty] private double _editHalfLife;
    [ObservableProperty] private string _editHalfLifeText = string.Empty;
    [ObservableProperty] private string _editHalfLifeUnit = "years";
    [ObservableProperty] private double _editEnergy;
    [ObservableProperty] private string _editEnergyText = string.Empty;
    [ObservableProperty] private double _editYield;
    [ObservableProperty] private string _editYieldText = string.Empty;
    [ObservableProperty] private string _editNotes = string.Empty;
    [ObservableProperty] private string _editEnglishNotes = string.Empty;

    partial void OnEditHalfLifeTextChanged(string value)
    {
        if (double.TryParse(value, out double result)) EditHalfLife = result;
    }

    partial void OnEditEnergyTextChanged(string value)
    {
        if (double.TryParse(value, out double result)) EditEnergy = result;
    }

    partial void OnEditYieldTextChanged(string value)
    {
        // التعامل مع العلامة المئوية إذا وجدت
        string clean = value.Replace("%", "").Trim();
        if (double.TryParse(clean, out double result))
        {
            EditYield = value.Contains("%") ? result / 100 : result;
        }
    }
    private Guid? _editingId;

    public RadioisotopesViewModel(IRadioisotopeService service)
    {
        _service = service;
        LoadData();
    }


    [RelayCommand]
    public void LoadData()
    {
        _allRadioisotopes = _service.GetAll();
        RefreshList();
    }

    partial void OnSearchTextChanged(string value)
    {
        RefreshList();
    }

    private void RefreshList()
    {
        var filtered = string.IsNullOrWhiteSpace(SearchText)
            ? _allRadioisotopes
            : _allRadioisotopes.Where(r => 
                r.Symbol.Contains(SearchText, StringComparison.OrdinalIgnoreCase) || 
                r.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                (r.ArabicName?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false)
              ).ToList();

        for (int i = 0; i < filtered.Count; i++)
        {
            filtered[i].No = i + 1;
        }
        Radioisotopes = new ObservableCollection<Radioisotope>(filtered);
    }

    [RelayCommand]
    private void AddNew()
    {
        IsNew = true;
        _editingId = null;
        ClearForm();
        CurrentStep = 1;
        IsEditing = true;
    }

    [RelayCommand(CanExecute = nameof(CanEdit))]
    private void Edit()
    {
        if (Selected == null) return;
        IsNew = false;
        _editingId = Selected.Id;
        EditName = Selected.Name;
        EditArabicName = Selected.ArabicName ?? "";
        EditSymbol = Selected.Symbol;
        EditRadiationType = Selected.RadiationType;
        EditHalfLife = Selected.HalfLife;
        EditHalfLifeText = Selected.HalfLife.ToString();
        EditHalfLifeUnit = Selected.HalfLifeUnit;
        EditEnergy = Selected.Energy;
        EditEnergyText = Selected.Energy.ToString();
        EditYield = Selected.Yield ?? 0;
        EditYieldText = (Selected.Yield ?? 0).ToString();
        EditNotes = Selected.Notes ?? "";
        EditEnglishNotes = Selected.EnglishNotes ?? "";
        CurrentStep = 1;
        IsEditing = true;
    }

    [RelayCommand]
    private void Save()
    {
        if (string.IsNullOrWhiteSpace(EditName) || string.IsNullOrWhiteSpace(EditSymbol))
        {
            ShowMsg(Helpers.TranslationHelper.GetString("MsgErrFillRequired")); return;
        }
        var item = new Radioisotope
        {
            Id = IsNew ? Guid.NewGuid() : _editingId!.Value,
            Name = EditName, ArabicName = EditArabicName, Symbol = EditSymbol, RadiationType = EditRadiationType,
            HalfLife = EditHalfLife, HalfLifeUnit = EditHalfLifeUnit,
            Energy = EditEnergy, Yield = EditYield, 
            Notes = EditNotes, EnglishNotes = EditEnglishNotes
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

    [RelayCommand]
    private void CancelEdit() { IsEditing = false; ClearForm(); }

    [RelayCommand]
    private void NextStep()
    {
        if (CurrentStep < TotalSteps) CurrentStep++;
    }

    [RelayCommand]
    private void PreviousStep()
    {
        if (CurrentStep > 1) CurrentStep--;
    }

    private void ClearForm()
    {
        EditName = EditArabicName = EditSymbol = EditRadiationType = EditNotes = EditEnglishNotes = string.Empty;
        EditHalfLife = EditEnergy = EditYield = 0;
        EditHalfLifeText = EditEnergyText = EditYieldText = string.Empty;
        EditHalfLifeUnit = "years";
    }

    private void ShowMsg(string m) { Message = m; HasMessage = true; }

    private bool CanEdit() => Selected != null;
}
