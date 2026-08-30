using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sources.Helpers;
using Sources.Models;
using Sources.Services;

namespace Sources.ViewModels;

public partial class NeutronSourceTypesViewModel : ObservableObject
{
    private readonly INeutronSourceTypeService _service;

    [ObservableProperty] private ObservableCollection<NeutronSourceType> _types = new();
    [ObservableProperty] private NeutronSourceType? _selectedType;
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private bool _isEditing;
    [ObservableProperty] private bool _isNew;
    [ObservableProperty] private string _message = string.Empty;
    [ObservableProperty] private bool _hasMessage;

    // Form fields
    [ObservableProperty] private string _editCode = string.Empty;
    [ObservableProperty] private string _editNameAr = string.Empty;
    [ObservableProperty] private string _editNameEn = string.Empty;
    [ObservableProperty] private string _editReactionType = "(α,n)";
    [ObservableProperty] private string _editTargetMaterial = string.Empty;
    [ObservableProperty] private string _editParentNuclide = string.Empty;
    [ObservableProperty] private double _editHalfLife;
    [ObservableProperty] private string _editHalfLifeText = string.Empty;
    [ObservableProperty] private string _editHalfLifeUnit = "years";
    [ObservableProperty] private double? _editAverageEnergyMev;
    [ObservableProperty] private string _editAverageEnergyText = string.Empty;
    [ObservableProperty] private double? _editNeutronYield;
    [ObservableProperty] private string _editNeutronYieldText = string.Empty;
    [ObservableProperty] private string _editNotes = string.Empty;

    private Guid? _editingId;

    public List<string> CommonUnits { get; } = new() { "years", "days", "hours", "minutes", "seconds" };
    public List<string> CommonReactions { get; } = new() { "(α,n)", "Spontaneous Fission", "(γ,n)", "(d,n)", "Other" };

    public NeutronSourceTypesViewModel(INeutronSourceTypeService service)
    {
        _service = service;
        LoadData();
    }

    public void LoadData()
    {
        var all = _service.GetAll();
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var search = SearchText.Trim().ToLower();
            all = all.Where(t =>
                t.Code.ToLower().Contains(search) ||
                (t.NameAr?.ToLower().Contains(search) ?? false) ||
                (t.NameEn?.ToLower().Contains(search) ?? false) ||
                (t.ReactionType?.ToLower().Contains(search) ?? false) ||
                (t.TargetMaterial?.ToLower().Contains(search) ?? false) ||
                (t.ParentNuclide?.ToLower().Contains(search) ?? false)
            ).ToList();
        }

        Types = new ObservableCollection<NeutronSourceType>(all);
    }

    partial void OnSearchTextChanged(string value) => LoadData();

    partial void OnEditHalfLifeTextChanged(string value)
    {
        if (double.TryParse(value, out double res)) EditHalfLife = res;
    }

    partial void OnEditAverageEnergyTextChanged(string value)
    {
        if (double.TryParse(value, out double res)) EditAverageEnergyMev = res;
        else EditAverageEnergyMev = null;
    }

    partial void OnEditNeutronYieldTextChanged(string value)
    {
        if (double.TryParse(value, out double res)) EditNeutronYield = res;
        else EditNeutronYield = null;
    }

    [RelayCommand]
    public void AddNew()
    {
        IsNew = true;
        _editingId = null;
        ClearForm();
        IsEditing = true;
    }

    [RelayCommand]
    public void Edit(NeutronSourceType? target)
    {
        if (target == null) return;

        IsNew = false;
        _editingId = target.Id;
        EditCode = target.Code;
        EditNameAr = target.NameAr ?? string.Empty;
        EditNameEn = target.NameEn ?? string.Empty;
        EditReactionType = target.ReactionType ?? "(α,n)";
        EditTargetMaterial = target.TargetMaterial ?? string.Empty;
        EditParentNuclide = target.ParentNuclide ?? string.Empty;
        EditHalfLife = target.HalfLife;
        EditHalfLifeText = target.HalfLife.ToString();
        EditHalfLifeUnit = target.HalfLifeUnit;
        EditAverageEnergyMev = target.AverageNeutronEnergyMeV;
        EditAverageEnergyText = target.AverageNeutronEnergyMeV?.ToString() ?? string.Empty;
        EditNeutronYield = target.TypicalNeutronYield;
        EditNeutronYieldText = target.TypicalNeutronYield?.ToString() ?? string.Empty;
        EditNotes = target.Notes ?? string.Empty;

        IsEditing = true;
    }

    [RelayCommand]
    public void CancelEdit()
    {
        IsEditing = false;
        ClearForm();
    }

    [RelayCommand]
    public void Save()
    {
        if (string.IsNullOrWhiteSpace(EditCode))
        {
            DialogHelper.ShowWarning("كود النوع المرجعي مطلوب", "تنبيه");
            return;
        }

        if (EditHalfLife <= 0 && !string.IsNullOrWhiteSpace(EditHalfLifeText) && double.TryParse(EditHalfLifeText, out double hl))
        {
            EditHalfLife = hl;
        }

        if (EditHalfLife <= 0)
        {
            DialogHelper.ShowWarning("يجب إدخال قيمة عمر نصف موجبة وأكبر من صفر", "تنبيه");
            return;
        }

        if (IsNew)
        {
            var newType = new NeutronSourceType
            {
                Code = EditCode.Trim(),
                NameAr = string.IsNullOrWhiteSpace(EditNameAr) ? EditCode.Trim() : EditNameAr.Trim(),
                NameEn = string.IsNullOrWhiteSpace(EditNameEn) ? EditCode.Trim() : EditNameEn.Trim(),
                ReactionType = EditReactionType?.Trim() ?? "(α,n)",
                TargetMaterial = EditTargetMaterial?.Trim(),
                ParentNuclide = EditParentNuclide?.Trim(),
                HalfLife = EditHalfLife,
                HalfLifeUnit = EditHalfLifeUnit ?? "years",
                AverageNeutronEnergyMeV = EditAverageEnergyMev,
                TypicalNeutronYield = EditNeutronYield,
                Notes = EditNotes?.Trim()
            };

            var res = _service.Create(newType);
            if (res.Success)
            {
                DialogHelper.ShowInfo(res.Message, "نجاح");
                IsEditing = false;
                ClearForm();
                LoadData();
            }
            else
            {
                DialogHelper.ShowWarning(res.Message, "تنبيه");
            }
        }
        else if (_editingId.HasValue)
        {
            var existing = _service.GetById(_editingId.Value);
            if (existing == null)
            {
                DialogHelper.ShowWarning("النوع المرجعي غير موجود", "خطأ");
                return;
            }

            existing.Code = EditCode.Trim();
            existing.NameAr = string.IsNullOrWhiteSpace(EditNameAr) ? EditCode.Trim() : EditNameAr.Trim();
            existing.NameEn = string.IsNullOrWhiteSpace(EditNameEn) ? EditCode.Trim() : EditNameEn.Trim();
            existing.ReactionType = EditReactionType?.Trim() ?? "(α,n)";
            existing.TargetMaterial = EditTargetMaterial?.Trim();
            existing.ParentNuclide = EditParentNuclide?.Trim();
            existing.HalfLife = EditHalfLife;
            existing.HalfLifeUnit = EditHalfLifeUnit ?? "years";
            existing.AverageNeutronEnergyMeV = EditAverageEnergyMev;
            existing.TypicalNeutronYield = EditNeutronYield;
            existing.Notes = EditNotes?.Trim();

            var res = _service.Update(existing);
            if (res.Success)
            {
                DialogHelper.ShowInfo(res.Message, "نجاح");
                IsEditing = false;
                ClearForm();
                LoadData();
            }
            else
            {
                DialogHelper.ShowWarning(res.Message, "تنبيه");
            }
        }
    }

    [RelayCommand]
    public void Delete(NeutronSourceType? item)
    {
        var target = item ?? SelectedType;
        if (target == null) return;

        bool confirm = DialogHelper.ShowConfirmation($"هل أنت متأكد من حذف النوع المرجعي '{target.Code}'؟", "تأكيد الحذف");
        if (!confirm) return;

        var res = _service.Delete(target.Id);
        if (res.Success)
        {
            DialogHelper.ShowInfo(res.Message, "تم الحذف");
            LoadData();
        }
        else
        {
            DialogHelper.ShowWarning(res.Message, "تعذر الحذف");
        }
    }

    private void ClearForm()
    {
        EditCode = string.Empty;
        EditNameAr = string.Empty;
        EditNameEn = string.Empty;
        EditReactionType = "(α,n)";
        EditTargetMaterial = string.Empty;
        EditParentNuclide = string.Empty;
        EditHalfLife = 0;
        EditHalfLifeText = string.Empty;
        EditHalfLifeUnit = "years";
        EditAverageEnergyMev = null;
        EditAverageEnergyText = string.Empty;
        EditNeutronYield = null;
        EditNeutronYieldText = string.Empty;
        EditNotes = string.Empty;
    }
}
