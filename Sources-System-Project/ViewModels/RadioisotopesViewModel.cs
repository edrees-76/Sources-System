using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using MaterialDesignThemes.Wpf;
using Sources.Models;
using Sources.Services;
using Sources.Interfaces;
using Sources.Messages;
using System;
using System.Collections.ObjectModel;

namespace Sources.ViewModels;

public partial class RadioisotopesViewModel : ObservableObject, IEditableViewModel
{
    private readonly IRadioisotopeService _service;
    private readonly IIsotopeLibraryService _libraryService;

    public ISnackbarMessageQueue? MessageQueue { get; }

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
    [ObservableProperty] private double? _editGammaConstant;
    [ObservableProperty] private string _editGammaConstantText = string.Empty;

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

    partial void OnEditGammaConstantTextChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            EditGammaConstant = null;
        }
        else if (double.TryParse(value, out double result) && result > 0)
        {
            EditGammaConstant = result;
        }
    }
    private Guid? _editingId;

    public RadioisotopesViewModel(IRadioisotopeService service, IIsotopeLibraryService? libraryService = null)
    {
        _service = service;
        _libraryService = libraryService ?? new IsotopeLibraryService();

        if (System.Windows.Application.Current?.Dispatcher != null)
        {
            MessageQueue = new SnackbarMessageQueue(TimeSpan.FromSeconds(3), System.Windows.Application.Current.Dispatcher);
        }
        else
        {
            try
            {
                MessageQueue = new SnackbarMessageQueue(TimeSpan.FromSeconds(3), System.Windows.Threading.Dispatcher.CurrentDispatcher);
            }
            catch
            {
                MessageQueue = null;
            }
        }

        LoadData();

        CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default.Register<Sources.Messages.NavigateToSearchResultMessage>(this, (r, m) =>
        {
            if (m.Category == SearchCategory.Radioisotopes)
            {
                SelectRadioisotopeById(m.EntityId);
            }
        });
    }

    [RelayCommand]
    private async Task SuggestGammaConstant()
    {
        if (string.IsNullOrWhiteSpace(EditSymbol))
        {
            ShowMsg(GetString("MsgErrSymbolRequiredFirst", "أدخل رمز النظير أولاً في خطوة الهوية"));
            return;
        }

        try
        {
            var entries = await _libraryService.GetAllEntriesAsync();
            var searchKey = NormalizeKey(EditSymbol);
            var reversedKey = GetReversedKey(searchKey);

            var entry = entries.FirstOrDefault(e =>
                e.SourceType == ReferenceSourceType.ORNL_RSIC_45_R1 &&
                e.SpecificGammaConstantValue.HasValue &&
                (NormalizeKey(e.DisplaySymbol) == searchKey ||
                 NormalizeKey(e.NuclideSymbol) == searchKey ||
                 (reversedKey != null && (NormalizeKey(e.DisplaySymbol) == reversedKey || NormalizeKey(e.NuclideSymbol) == reversedKey))));

            if (entry != null && entry.SpecificGammaConstantValue.HasValue)
            {
                // تحويل القيمة من (mSv/h)/MBq (وحدة ORNL) إلى µSv·m²/(MBq·h) (الوحدة التشغيلية) عبر الضرب في 1000
                double operationalVal = entry.SpecificGammaConstantValue.Value * 1000.0;
                EditGammaConstant = operationalVal;
                EditGammaConstantText = operationalVal.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture);

                string pageStr = entry.PageNumber > 0 ? entry.PageNumber.ToString() : "-";
                string msgTemplate = GetString("MsgOrnlSuggestSuccess", "تم الاقتراح من مرجع ORNL/RSIC-45/R1 (صفحة {0})");
                ShowMsg(string.Format(msgTemplate, pageStr));
            }
            else
            {
                ShowMsg(GetString("MsgOrnlNotFound", "لم يُعثر على هذا النظير في مرجع ORNL — يمكنك إدخال القيمة يدوياً أو البحث في مكتبة النظائر المرجعية إن كانت متوفرة من مصدر آخر (مثل ICRP)"));
            }
        }
        catch (Exception ex)
        {
            ShowMsg(ex.Message);
        }
    }

    private static string GetString(string key, string fallback)
    {
        if (System.Windows.Application.Current != null && System.Windows.Application.Current.Resources.Contains(key))
        {
            return System.Windows.Application.Current.FindResource(key) as string ?? fallback;
        }
        return fallback;
    }

    private static string NormalizeKey(string? symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol)) return string.Empty;
        return symbol.Replace("-", "").Replace(" ", "").Trim().ToLowerInvariant();
    }

    private static string? GetReversedKey(string compactKey) => IsotopeLibraryService.GetReversedNuclideKey(compactKey);

    public void SelectRadioisotopeById(Guid isotopeId)
    {
        SearchText = string.Empty;
        var isotope = Radioisotopes.FirstOrDefault(r => r.Id == isotopeId);
        if (isotope == null)
        {
            LoadData();
            isotope = Radioisotopes.FirstOrDefault(r => r.Id == isotopeId);
        }

        if (isotope != null)
        {
            Selected = isotope;
        }
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
        EditGammaConstant = Selected.GammaConstant;
        EditGammaConstantText = Selected.GammaConstant.HasValue ? Selected.GammaConstant.Value.ToString() : string.Empty;
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

        if (!string.IsNullOrWhiteSpace(EditGammaConstantText))
        {
            if (!double.TryParse(EditGammaConstantText, out double gc) || gc <= 0)
            {
                ShowMsg(Helpers.TranslationHelper.GetString("MsgErrGammaConstantPositive") ?? "يجب أن تكون قيمة ثابت غاما رقماً موجباً أكبر من الصفر");
                return;
            }
            EditGammaConstant = gc;
        }
        else
        {
            EditGammaConstant = null;
        }

        var item = new Radioisotope
        {
            Id = (IsNew || !_editingId.HasValue) ? Guid.NewGuid() : _editingId.Value,
            Name = EditName, ArabicName = EditArabicName, Symbol = EditSymbol, RadiationType = EditRadiationType,
            HalfLife = EditHalfLife, HalfLifeUnit = EditHalfLifeUnit,
            Energy = EditEnergy, Yield = EditYield,
            GammaConstant = EditGammaConstant,
            Notes = EditNotes, EnglishNotes = EditEnglishNotes
        };
        var r = IsNew ? _service.Create(item) : _service.Update(item);
        if (r.Success)
        {
            IsEditing = false;
            LoadData();
            ShowMsg(r.Message);
        }
        else
        {
            ShowMsg(r.Message);
        }
    }

    [RelayCommand]
    private void Delete()
    {
        if (Selected == null) return;
        var r = _service.Delete(Selected.Id);
        if (r.Success) LoadData();
        ShowMsg(r.Message);
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

    [RelayCommand]
    private void DismissMessage()
    {
        _msgCts?.Cancel();
        HasMessage = false;
    }

    private void ClearForm()
    {
        EditName = EditArabicName = EditSymbol = EditRadiationType = EditNotes = EditEnglishNotes = string.Empty;
        EditHalfLife = EditEnergy = EditYield = 0;
        EditHalfLifeText = EditEnergyText = EditYieldText = EditGammaConstantText = string.Empty;
        EditGammaConstant = null;
        EditHalfLifeUnit = "years";
        _msgCts?.Cancel();
        HasMessage = false;
        Message = string.Empty;
    }

    private System.Threading.CancellationTokenSource? _msgCts;

    private void ShowMsg(string m)
    {
        _msgCts?.Cancel();
        Message = m;
        HasMessage = !string.IsNullOrWhiteSpace(m);
        if (!string.IsNullOrWhiteSpace(m))
        {
            MessageQueue?.Enqueue(m);
        }

        var cts = new System.Threading.CancellationTokenSource();
        _msgCts = cts;
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(5000, cts.Token);
                if (!cts.Token.IsCancellationRequested)
                {
                    System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                    {
                        if (_msgCts == cts)
                        {
                            HasMessage = false;
                        }
                    });
                }
            }
            catch { }
        });
    }

    private bool CanEdit() => Selected != null;
}
