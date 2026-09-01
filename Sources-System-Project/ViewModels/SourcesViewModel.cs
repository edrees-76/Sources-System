using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Sources.Messages;
using Sources.Models;
using Sources.Services;
using Sources.Helpers;
using Sources.Data;
using Sources.Interfaces;
using Sources.Views;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Microsoft.Win32;
using Microsoft.Extensions.DependencyInjection;

namespace Sources.ViewModels;

/// <summary>
/// نموذج مساعد لإدخال نظير داخل مصدر متعدد النظائر
/// </summary>
public partial class IsotopeEntryViewModel : ObservableObject
{
    [ObservableProperty] private Guid? _radioisotopeId;
    [ObservableProperty] private double _initialActivity;
    [ObservableProperty] private string _initialActivityText = string.Empty;
    [ObservableProperty] private Guid? _activityUnitId;

    partial void OnInitialActivityTextChanged(string value)
    {
        if (double.TryParse(value, out double result))
        {
            InitialActivity = result;
        }
    }

    public SourceIsotope ToSourceIsotope()
    {
        return new SourceIsotope
        {
            RadioisotopeId = RadioisotopeId!.Value,
            InitialActivityValue = InitialActivity,
            ActivityUnitId = ActivityUnitId,
        };
    }
}

/// <summary>
/// صف مخصص لجدول المصادر المحذوفة لعرض الرقم التسلسلي # وثبات البيانات أثناء التمرير
/// </summary>
public class DeletedSourceRow
{
    public int RowNumber { get; set; }
    public Source Source { get; set; } = null!;
    public Guid Id => Source.Id;
    public string DisplaySourceCode => Source.DisplaySourceCode;
    public string SourceCode => Source.SourceCode;
    public string DisplayIsotopes => Source.DisplayIsotopes;
    public string CurrentActivityWithUnit => Source.CurrentActivityWithUnit;
    public string DisplayDoseRate => Source.DisplayDoseRate;
    public string DoseRateTooltip => Source.DoseRateTooltip;
    public Location? Location => Source.Location;
    public string ArabicStatus => Source.ArabicStatus;
    public DateTime? DeletedAt => Source.DeletedAt;
    public User? DeletedByUser => Source.DeletedByUser;
}

public partial class SourcesViewModel : ObservableObject, IEditableViewModel
{
    private readonly ISourceService _sourceService;
    private readonly IRadioisotopeService _isotopeService;
    private readonly ILocationService _locationService;
    private readonly IReportingService _reportingService;
    private readonly IDecayCalculationService _decayService;
    private readonly INeutronSourceService _neutronSourceService;
    private readonly INeutronSourceTypeService _neutronSourceTypeService;

    [ObservableProperty] private ObservableCollection<Source> _sources = new();
    [ObservableProperty] private ObservableCollection<Radioisotope> _radioisotopes = new();
    [ObservableProperty] private ObservableCollection<ActivityUnit> _activityUnits = new();
    [ObservableProperty] private ObservableCollection<Location> _locations = new();
    [ObservableProperty] private Source? _selectedSource;
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private string _statusFilter = "All";
    [ObservableProperty] private bool _hasMessage;
    [ObservableProperty] private bool _isEditing;
    [ObservableProperty] private string _message = string.Empty;

    // ─── تبويبات العرض (Active, Neutron, Deleted) ───
    [ObservableProperty] private string _selectedTab = "Active"; // "Active", "Neutron", "Deleted"
    [ObservableProperty] private bool _isNeutronSourcesView;
    [ObservableProperty] private ObservableCollection<NeutronSource> _neutronSources = new();
    [ObservableProperty] private ObservableCollection<NeutronSource> _pagedNeutronSources = new();
    [ObservableProperty] private ObservableCollection<NeutronSourceType> _neutronSourceTypes = new();
    [ObservableProperty] private NeutronSource? _selectedNeutronSource;
    [ObservableProperty] private int _neutronSourcesCount;
    [ObservableProperty] private bool _hasNeutronSources;
    [ObservableProperty] private int _neutronCurrentPage = 1;
    [ObservableProperty] private int _neutronTotalPages = 1;
    [ObservableProperty] private string _neutronPageStatusText = string.Empty;

    partial void OnSelectedTabChanged(string value)
    {
        IsNeutronSourcesView = value == "Neutron";
        IsDeletedSourcesView = value == "Deleted";
        if (value == "Neutron")
        {
            _ = LoadNeutronDataAsync();
        }
        else if (value == "Deleted")
        {
            _ = LoadDeletedDataAsync();
        }
    }

    // ─── خصائص التقسيم إلى صفحات (Pagination) ───
    [ObservableProperty] private int _currentPage = 1;
    [ObservableProperty] private int _pageSize = 16;
    [ObservableProperty] private int _totalPages = 1;
    [ObservableProperty] private ObservableCollection<Source> _pagedSources = new();
    [ObservableProperty] private string _pageStatusText = string.Empty;

    public double TotalActivityValue => Sources.Sum(s => s.CurrentActivityValue * (s.CurrentActivityUnit?.ConversionToBq ?? 1));

    [ObservableProperty] private ObservableCollection<TotalActivityItem> _totalActivityItems = new();

    // حقول النموذج الموحد
    [ObservableProperty] private bool _isNeutronForm;
    [ObservableProperty] private string _editSourceCode = string.Empty;
    [ObservableProperty] private Guid? _editRadioisotopeId;
    [ObservableProperty] private string _editSerialNumber = string.Empty;
    [ObservableProperty] private string _editManufacturer = string.Empty;
    [ObservableProperty] private string _editModel = string.Empty;
    [ObservableProperty] private double _editInitialActivity;
    [ObservableProperty] private string _editInitialActivityText = string.Empty;
    [ObservableProperty] private Guid? _editInitialUnitId;
    [ObservableProperty] private DateTime _editCalibrationDate = DateTime.Now;
    [ObservableProperty] private Guid? _editCurrentUnitId;
    [ObservableProperty] private Guid? _editLocationId;
    [ObservableProperty] private string _editStatus = "InUse";
    [ObservableProperty] private bool _editIsSealed = true;
    [ObservableProperty] private bool _isActivelyBorrowed;

    // حقول المصدر النيتروني الخاصة
    [ObservableProperty] private Guid? _editNeutronTypeId;
    [ObservableProperty] private double _editEmissionRate;
    [ObservableProperty] private string _editEmissionRateText = string.Empty;
    [ObservableProperty] private double? _editRelativeUncertaintyPercent;
    [ObservableProperty] private string _editRelativeUncertaintyText = string.Empty;

    private bool _isUpdatingEmissionRate;

    partial void OnEditEmissionRateTextChanged(string value)
    {
        if (_isUpdatingEmissionRate) return;
        try
        {
            _isUpdatingEmissionRate = true;
            if (ScientificNotationParser.TryParse(value, out double result))
            {
                EditEmissionRate = result;
            }
            else
            {
                EditEmissionRate = 0;
            }
        }
        finally
        {
            _isUpdatingEmissionRate = false;
        }
    }

    partial void OnEditEmissionRateChanged(double value)
    {
        if (_isUpdatingEmissionRate) return;
        try
        {
            _isUpdatingEmissionRate = true;
            if (value > 0 && (string.IsNullOrWhiteSpace(EditEmissionRateText) || (ScientificNotationParser.TryParse(EditEmissionRateText, out double r) && Math.Abs(r - value) > 0.0001)))
            {
                EditEmissionRateText = value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }
        }
        finally
        {
            _isUpdatingEmissionRate = false;
        }
    }

    partial void OnEditRelativeUncertaintyTextChanged(string value)
    {
        string clean = value.Replace("%", "").Trim();
        if (double.TryParse(clean, out double result))
        {
            EditRelativeUncertaintyPercent = result;
        }
        else
        {
            EditRelativeUncertaintyPercent = null;
        }
    }

    partial void OnEditInitialActivityTextChanged(string value)
    {
        if (double.TryParse(value, out double result))
        {
            EditInitialActivity = result;
        }
    }
    [ObservableProperty] private string _editNotes = string.Empty;
    [ObservableProperty] private string? _editImagePath;
    [ObservableProperty] private bool _isNew;
    [ObservableProperty] private int _currentStep = 1;
    public int TotalSteps => 3;

    // ─── حقول النظائر المتعددة ───
    [ObservableProperty] private bool _isMultiIsotope;
    [ObservableProperty] private ObservableCollection<IsotopeEntryViewModel> _isotopeEntries = new();

    private Guid? _editingId;

    [ObservableProperty] private bool _isDeletedSourcesView;
    [ObservableProperty] private ObservableCollection<Source> _deletedSources = new();
    [ObservableProperty] private ObservableCollection<DeletedSourceRow> _pagedDeletedSources = new();
    [ObservableProperty] private int _deletedCurrentPage = 1;
    [ObservableProperty] private int _deletedTotalPages = 1;
    [ObservableProperty] private string _deletedPageStatusText = string.Empty;
    [ObservableProperty] private int _activeSourcesCount;
    [ObservableProperty] private int _deletedSourcesCount;
    [ObservableProperty] private bool _hasActiveSources;
    [ObservableProperty] private bool _hasDeletedSources;

    public SourcesViewModel(
        ISourceService sourceService, 
        IRadioisotopeService isotopeService, 
        ILocationService locationService, 
        IReportingService reportingService, 
        IDecayCalculationService? decayService = null,
        INeutronSourceService? neutronSourceService = null,
        INeutronSourceTypeService? neutronSourceTypeService = null)
    {
        _sourceService = sourceService;
        _isotopeService = isotopeService;
        _locationService = locationService;
        _reportingService = reportingService;
        _decayService = decayService ?? new DecayCalculationService();
        _neutronSourceService = neutronSourceService ?? App.ServiceProvider?.GetService<INeutronSourceService>()!;
        _neutronSourceTypeService = neutronSourceTypeService ?? App.ServiceProvider?.GetService<INeutronSourceTypeService>()!;
        _ = LoadDataAsync();

        WeakReferenceMessenger.Default.Register<NavigateToSearchResultMessage>(this, (r, m) =>
        {
            if (m.Category == SearchCategory.Sources)
            {
                SelectSourceById(m.EntityId);
            }
        });
    }

    public void SelectSourceById(Guid sourceId)
    {
        IsDeletedSourcesView = false;
        SearchText = string.Empty;
        StatusFilter = "All";

        var source = Sources.FirstOrDefault(s => s.Id == sourceId);
        if (source == null)
        {
            var all = _sourceService.GetAllSources();
            source = all.FirstOrDefault(s => s.Id == sourceId);
            if (source != null && !Sources.Any(s => s.Id == sourceId))
            {
                Sources.Insert(0, source);
            }
        }

        if (source != null)
        {
            int index = Sources.IndexOf(source);
            if (index >= 0)
            {
                CurrentPage = (index / PageSize) + 1;
                UpdatePagination();
            }

            SelectedSource = source;
            ViewSourceDetails(source);
        }
    }

    [RelayCommand]
    public async Task RefreshAsync()
    {
        await LoadDataAsync();
    }

    [RelayCommand]
    public async Task SwitchToActiveSourcesAsync()
    {
        SelectedTab = "Active";
        IsDeletedSourcesView = false;
        IsNeutronSourcesView = false;
        await LoadDataAsync();
    }

    [RelayCommand]
    public async Task SwitchToNeutronSourcesAsync()
    {
        SelectedTab = "Neutron";
        IsDeletedSourcesView = false;
        IsNeutronSourcesView = true;
        await LoadNeutronDataAsync();
    }

    [RelayCommand]
    public async Task SwitchToDeletedSourcesAsync()
    {
        SelectedTab = "Deleted";
        IsDeletedSourcesView = true;
        IsNeutronSourcesView = false;
        await LoadDeletedDataAsync();
    }

    [RelayCommand]
    public async Task LoadDataAsync()
    {
        if (IsDeletedSourcesView)
        {
            await LoadDeletedDataAsync();
            return;
        }

        if (IsNeutronSourcesView)
        {
            await LoadNeutronDataAsync();
            return;
        }

        var allSources = await Task.Run(() => _sourceService.GetAllSources());
        ActiveSourcesCount = allSources.Count;
        var deletedList = await Task.Run(() => _sourceService.GetDeletedSources());
        DeletedSourcesCount = deletedList.Count;
        var neutronList = await Task.Run(() => _neutronSourceService?.GetAll() ?? new List<NeutronSource>()) ?? new List<NeutronSource>();
        NeutronSourcesCount = neutronList.Count;

        // تطبيق الفلاتر
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var searchLower = SearchText.ToLower();
            allSources = allSources.Where(s =>
                (s.SourceCode?.ToLower().Contains(searchLower) ?? false) ||
                (s.Radioisotope?.Name?.ToLower().Contains(searchLower) ?? false) ||
                (s.Radioisotope?.Symbol?.ToLower().Contains(searchLower) ?? false) ||
                (s.SerialNumber?.ToLower().Contains(searchLower) ?? false) ||
                (s.DisplayIsotopes?.ToLower().Contains(searchLower) ?? false) ||
                (s.Location?.LocationName?.ToLower().Contains(searchLower) ?? false) ||
                (s.Manufacturer?.ToLower().Contains(searchLower) ?? false) ||
                (s.Model?.ToLower().Contains(searchLower) ?? false) ||
                (s.Status?.ToLower().Contains(searchLower) ?? false) ||
                s.CalibrationDate.ToString("yyyy-MM-dd").Contains(searchLower) ||
                s.InitialActivityValue.ToString().Contains(searchLower) ||
                s.CurrentActivityValue.ToString().Contains(searchLower)
            ).ToList();
        }

        if (!string.IsNullOrWhiteSpace(StatusFilter) && StatusFilter != "All")
        {
            allSources = allSources.Where(s => s.Status == StatusFilter).ToList();
        }

        Sources = new ObservableCollection<Source>(allSources);
        HasActiveSources = Sources.Count > 0;
        OnPropertyChanged(nameof(TotalActivityValue));
        
        // تحديث معلومات الصفحات
        CurrentPage = 1;
        UpdatePagination();

        // تنبيه المستخدم إذا لم توجد نتائج بعد البحث
        if (Sources.Count == 0 && !string.IsNullOrWhiteSpace(SearchText))
        {
            DialogHelper.ShowInfo(TranslationHelper.GetString("MsgNoSearchSource"), TranslationHelper.GetString("TitleSearchResult"));
        }

        Radioisotopes = new ObservableCollection<Radioisotope>(_isotopeService.GetAll());
        Locations = new ObservableCollection<Location>(_locationService.GetAll());
        NeutronSourceTypes = new ObservableCollection<NeutronSourceType>(_neutronSourceTypeService?.GetAll() ?? new List<NeutronSourceType>());

        // تحميل وحدات النشاط
        using var db = App.CreateDbContext();
        ActivityUnits = new ObservableCollection<ActivityUnit>(db.ActivityUnits.OrderBy(u => u.UnitName).ToList());

        // تحديث إجمالي النشاط بجميع الوحدات (بعد تحميل الوحدات)
        UpdateTotalActivityItems();
    }

    public async Task LoadNeutronDataAsync()
    {
        var allNeutrons = await Task.Run(() => _neutronSourceService?.GetAll() ?? new List<NeutronSource>()) ?? new List<NeutronSource>();
        NeutronSourcesCount = allNeutrons.Count;
        var activeList = await Task.Run(() => _sourceService?.GetAllSources() ?? new List<Source>()) ?? new List<Source>();
        ActiveSourcesCount = activeList.Count;
        var deletedList = await Task.Run(() => _sourceService?.GetDeletedSources() ?? new List<Source>()) ?? new List<Source>();
        DeletedSourcesCount = deletedList.Count;

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var searchLower = SearchText.ToLower();
            allNeutrons = allNeutrons.Where(n =>
                (n.SourceCode?.ToLower().Contains(searchLower) ?? false) ||
                (n.NeutronSourceType?.Code?.ToLower().Contains(searchLower) ?? false) ||
                (n.NeutronSourceType?.NameAr?.ToLower().Contains(searchLower) ?? false) ||
                (n.NeutronSourceType?.NameEn?.ToLower().Contains(searchLower) ?? false) ||
                (n.SerialNumber?.ToLower().Contains(searchLower) ?? false) ||
                (n.Location?.LocationName?.ToLower().Contains(searchLower) ?? false) ||
                (n.Status?.ToLower().Contains(searchLower) ?? false) ||
                (n.CalibrationDate.HasValue && n.CalibrationDate.Value.ToString("yyyy-MM-dd").Contains(searchLower)) ||
                n.EmissionRate.ToString().Contains(searchLower)
            ).ToList();
        }

        if (!string.IsNullOrWhiteSpace(StatusFilter) && StatusFilter != "All")
        {
            allNeutrons = allNeutrons.Where(n => n.Status == StatusFilter).ToList();
        }

        NeutronSources = new ObservableCollection<NeutronSource>(allNeutrons);
        HasNeutronSources = NeutronSources.Count > 0;
        NeutronCurrentPage = 1;
        UpdateNeutronPagination();

        if (NeutronSources.Count == 0 && !string.IsNullOrWhiteSpace(SearchText))
        {
            DialogHelper.ShowInfo(TranslationHelper.GetString("MsgNoSearchNeutronSource") ?? TranslationHelper.GetString("MsgNoSearchSource") ?? "لم يتم العثور على مصادر نيترونية تطابق معايير البحث.", TranslationHelper.GetString("TitleSearchResult") ?? "نتائج البحث");
        }

        Locations = new ObservableCollection<Location>(_locationService?.GetAll() ?? new List<Location>());
        NeutronSourceTypes = new ObservableCollection<NeutronSourceType>(_neutronSourceTypeService?.GetAll() ?? new List<NeutronSourceType>());
    }

    private bool CanGoToPreviousNeutronPage => NeutronCurrentPage > 1;
    private bool CanGoToNextNeutronPage => NeutronCurrentPage < NeutronTotalPages;

    [RelayCommand(CanExecute = nameof(CanGoToPreviousNeutronPage))]
    private void FirstNeutronPage()
    {
        if (NeutronCurrentPage > 1)
        {
            NeutronCurrentPage = 1;
            UpdatePagedNeutronSources();
        }
    }

    [RelayCommand(CanExecute = nameof(CanGoToPreviousNeutronPage))]
    private void PreviousNeutronPage()
    {
        if (NeutronCurrentPage > 1)
        {
            NeutronCurrentPage--;
            UpdatePagedNeutronSources();
        }
    }

    [RelayCommand(CanExecute = nameof(CanGoToNextNeutronPage))]
    private void NextNeutronPage()
    {
        if (NeutronCurrentPage < NeutronTotalPages)
        {
            NeutronCurrentPage++;
            UpdatePagedNeutronSources();
        }
    }

    [RelayCommand(CanExecute = nameof(CanGoToNextNeutronPage))]
    private void LastNeutronPage()
    {
        if (NeutronCurrentPage < NeutronTotalPages)
        {
            NeutronCurrentPage = NeutronTotalPages;
            UpdatePagedNeutronSources();
        }
    }

    private void UpdateNeutronPagination()
    {
        NeutronTotalPages = (int)Math.Ceiling((double)NeutronSources.Count / PageSize);
        if (NeutronTotalPages == 0) NeutronTotalPages = 1;
        if (NeutronCurrentPage > NeutronTotalPages) NeutronCurrentPage = NeutronTotalPages;
        UpdatePagedNeutronSources();
    }

    private void UpdatePagedNeutronSources()
    {
        var items = NeutronSources.Skip((NeutronCurrentPage - 1) * PageSize).Take(PageSize).ToList();
        PagedNeutronSources = new ObservableCollection<NeutronSource>(items);
        NeutronPageStatusText = TranslationHelper.GetFormat("PageStatusFormat", NeutronCurrentPage, NeutronTotalPages, NeutronSources.Count);
        FirstNeutronPageCommand.NotifyCanExecuteChanged();
        PreviousNeutronPageCommand.NotifyCanExecuteChanged();
        NextNeutronPageCommand.NotifyCanExecuteChanged();
        LastNeutronPageCommand.NotifyCanExecuteChanged();
    }

    public async Task LoadDeletedDataAsync()
    {
        var deleted = await Task.Run(() => _sourceService?.GetDeletedSources() ?? new List<Source>()) ?? new List<Source>();
        DeletedSourcesCount = deleted.Count;
        var activeList = await Task.Run(() => _sourceService?.GetAllSources() ?? new List<Source>()) ?? new List<Source>();
        ActiveSourcesCount = activeList.Count;
        var neutronList = await Task.Run(() => _neutronSourceService?.GetAll() ?? new List<NeutronSource>()) ?? new List<NeutronSource>();
        NeutronSourcesCount = neutronList.Count;

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var searchLower = SearchText.ToLower();
            deleted = deleted.Where(s =>
                (s.SourceCode?.ToLower().Contains(searchLower) ?? false) ||
                (s.Radioisotope?.Name?.ToLower().Contains(searchLower) ?? false) ||
                (s.Radioisotope?.Symbol?.ToLower().Contains(searchLower) ?? false) ||
                (s.SerialNumber?.ToLower().Contains(searchLower) ?? false) ||
                (s.DisplayIsotopes?.ToLower().Contains(searchLower) ?? false) ||
                (s.Location?.LocationName?.ToLower().Contains(searchLower) ?? false) ||
                (s.DeletedByUser?.FullName?.ToLower().Contains(searchLower) ?? false) ||
                (s.Manufacturer?.ToLower().Contains(searchLower) ?? false) ||
                (s.Model?.ToLower().Contains(searchLower) ?? false) ||
                (s.Status?.ToLower().Contains(searchLower) ?? false) ||
                (s.DeletedAt?.ToString("yyyy-MM-dd").Contains(searchLower) ?? false)
            ).ToList();
        }

        DeletedSources = new ObservableCollection<Source>(deleted);
        HasDeletedSources = DeletedSources.Count > 0;
        DeletedCurrentPage = 1;
        UpdateDeletedPagination();

        if (DeletedSources.Count == 0 && !string.IsNullOrWhiteSpace(SearchText))
        {
            DialogHelper.ShowInfo(TranslationHelper.GetString("MsgNoSearchSource"), TranslationHelper.GetString("TitleSearchResult"));
        }
    }

    private bool CanGoToPreviousDeletedPage => DeletedCurrentPage > 1;
    private bool CanGoToNextDeletedPage => DeletedCurrentPage < DeletedTotalPages;

    [RelayCommand(CanExecute = nameof(CanGoToPreviousDeletedPage))]
    private void FirstDeletedPage()
    {
        if (DeletedCurrentPage > 1)
        {
            DeletedCurrentPage = 1;
            UpdatePagedDeletedSources();
        }
    }

    [RelayCommand(CanExecute = nameof(CanGoToPreviousDeletedPage))]
    private void PreviousDeletedPage()
    {
        if (DeletedCurrentPage > 1)
        {
            DeletedCurrentPage--;
            UpdatePagedDeletedSources();
        }
    }

    [RelayCommand(CanExecute = nameof(CanGoToNextDeletedPage))]
    private void NextDeletedPage()
    {
        if (DeletedCurrentPage < DeletedTotalPages)
        {
            DeletedCurrentPage++;
            UpdatePagedDeletedSources();
        }
    }

    [RelayCommand(CanExecute = nameof(CanGoToNextDeletedPage))]
    private void LastDeletedPage()
    {
        if (DeletedCurrentPage < DeletedTotalPages)
        {
            DeletedCurrentPage = DeletedTotalPages;
            UpdatePagedDeletedSources();
        }
    }

    private void UpdateDeletedPagination()
    {
        DeletedTotalPages = (int)Math.Ceiling((double)DeletedSources.Count / PageSize);
        if (DeletedTotalPages == 0) DeletedTotalPages = 1;
        if (DeletedCurrentPage > DeletedTotalPages) DeletedCurrentPage = DeletedTotalPages;
        UpdatePagedDeletedSources();
    }

    private void UpdatePagedDeletedSources()
    {
        int startRank = (DeletedCurrentPage - 1) * PageSize;
        var items = DeletedSources.Skip(startRank).Take(PageSize)
            .Select((src, index) => new DeletedSourceRow
            {
                RowNumber = startRank + index + 1,
                Source = src
            }).ToList();
        PagedDeletedSources = new ObservableCollection<DeletedSourceRow>(items);
        DeletedPageStatusText = TranslationHelper.GetFormat("PageStatusFormat", DeletedCurrentPage, DeletedTotalPages, DeletedSources.Count);
        FirstDeletedPageCommand.NotifyCanExecuteChanged();
        PreviousDeletedPageCommand.NotifyCanExecuteChanged();
        NextDeletedPageCommand.NotifyCanExecuteChanged();
        LastDeletedPageCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    public void AddNew()
    {
        IsNeutronForm = false;
        IsNew = true;
        _editingId = null;
        IsActivelyBorrowed = false;
        ClearForm();
        CurrentStep = 1;
        IsEditing = true;
    }

    [RelayCommand]
    public void AddNewNeutron()
    {
        IsNeutronForm = true;
        IsNew = true;
        _editingId = null;
        IsActivelyBorrowed = false;
        ClearForm();
        CurrentStep = 1;
        IsEditing = true;
    }

    private void ClearForm()
    {
        EditSourceCode = string.Empty;
        EditRadioisotopeId = null;
        EditSerialNumber = string.Empty;
        EditManufacturer = string.Empty;
        EditModel = string.Empty;
        EditInitialActivity = 0;
        EditInitialActivityText = string.Empty;
        EditInitialUnitId = ActivityUnits.FirstOrDefault()?.Id;
        EditCalibrationDate = DateTime.Now;
        EditCurrentUnitId = ActivityUnits.FirstOrDefault()?.Id;
        EditLocationId = Locations.FirstOrDefault()?.Id;
        EditStatus = "InUse";
        EditIsSealed = true;
        EditNotes = string.Empty;
        IsMultiIsotope = false;
        EditImagePath = null;
        IsActivelyBorrowed = false;
        IsotopeEntries.Clear();

        // Neutron form fields
        EditNeutronTypeId = NeutronSourceTypes.FirstOrDefault()?.Id;
        EditEmissionRate = 0;
        EditEmissionRateText = string.Empty;
        EditRelativeUncertaintyPercent = null;
        EditRelativeUncertaintyText = string.Empty;
    }

    [RelayCommand]
    private async Task ResetSearchAsync()
    {
        SearchText = string.Empty;
        StatusFilter = "All";
        if (IsNeutronSourcesView)
            await LoadNeutronDataAsync();
        else if (IsDeletedSourcesView)
            await LoadDeletedDataAsync();
        else
            await LoadDataAsync();
    }
    
    [RelayCommand]
    private async Task SearchAsync()
    {
        if (IsNeutronSourcesView)
            await LoadNeutronDataAsync();
        else if (IsDeletedSourcesView)
            await LoadDeletedDataAsync();
        else
            await LoadDataAsync();
    }
    

    [RelayCommand]
    public void EditNeutronSource(NeutronSource? source)
    {
        var target = source ?? SelectedNeutronSource;
        if (target == null) return;

        SelectedNeutronSource = target;
        IsNeutronForm = true;
        IsNew = false;
        _editingId = target.Id;
        IsActivelyBorrowed = false;

        EditSourceCode = target.SourceCode;
        EditNeutronTypeId = target.NeutronSourceTypeId;
        EditSerialNumber = target.SerialNumber ?? "";
        EditEmissionRate = target.EmissionRate;
        EditEmissionRateText = target.EmissionRate.ToString();
        EditRelativeUncertaintyPercent = target.RelativeExpandedUncertaintyPercent;
        EditRelativeUncertaintyText = target.RelativeExpandedUncertaintyPercent?.ToString() ?? "";
        EditCalibrationDate = target.CalibrationDate ?? DateTime.Today;
        EditLocationId = target.LocationId;
        EditStatus = target.Status;
        EditNotes = target.Notes ?? "";
        EditImagePath = null;
        CurrentStep = 1;

        IsEditing = true;
    }

    [RelayCommand]
    public async Task DeleteNeutronSourceAsync(NeutronSource? source)
    {
        var target = source ?? SelectedNeutronSource;
        if (target == null) return;

        string confirmMsg = TranslationHelper.GetString("MsgConfirmDeleteNeutronSource") ?? "هل أنت متأكد من حذف هذا المصدر النيتروني؟";
        string confirmTitle = TranslationHelper.GetString("AlertConfirmation") ?? "تأكيد الحذف";
        if (!DialogHelper.ShowConfirmation(confirmMsg, confirmTitle)) return;

        var result = _neutronSourceService?.Delete(target.Id) ?? (false, "خدمة المصادر النيترونية غير متاحة");
        if (!result.Success)
        {
            ShowMessage(result.Message);
        }
        else
        {
            Message = result.Message;
            HasMessage = true;
            await LoadNeutronDataAsync();
            WeakReferenceMessenger.Default.Send(new SourcesUpdatedMessage());
        }
    }

    [RelayCommand]
    public void ViewNeutronSourceDetails(object? param)
    {
        SourceNavigationHelper.OpenNeutronSourceDetails(param);
    }

    [RelayCommand]
    public void OpenNeutronSourceTypesManagement()
    {
        var window = new NeutronSourceTypesWindow(new NeutronSourceTypesViewModel(_neutronSourceTypeService));
        if (System.Windows.Application.Current?.MainWindow != null && System.Windows.Application.Current.MainWindow.IsVisible)
        {
            window.Owner = System.Windows.Application.Current.MainWindow;
        }
        window.ShowDialog();
        NeutronSourceTypes = new ObservableCollection<NeutronSourceType>(_neutronSourceTypeService?.GetAll() ?? new List<NeutronSourceType>());
    }

    [RelayCommand]
    private void EditSource(Source source)
    {
        if (source == null) return;
        SelectedSource = source;
        IsNeutronForm = false;
        IsNew = false;
        _editingId = source.Id;
        IsActivelyBorrowed = _sourceService.HasActiveBorrow(source.Id);
        EditSourceCode = source.SourceCode;
        EditRadioisotopeId = SelectedSource.RadioisotopeId;
        EditSerialNumber = SelectedSource.SerialNumber ?? "";
        EditManufacturer = SelectedSource.Manufacturer ?? "";
        EditModel = SelectedSource.Model ?? "";
        EditInitialActivity = SelectedSource.InitialActivityValue;
        EditInitialActivityText = SelectedSource.InitialActivityValue.ToString();
        EditInitialUnitId = SelectedSource.InitialActivityUnitId;
        EditCalibrationDate = SelectedSource.CalibrationDate;
        EditCurrentUnitId = SelectedSource.CurrentActivityUnitId;
        EditLocationId = SelectedSource.LocationId;
        EditStatus = SelectedSource.Status;
        EditIsSealed = SelectedSource.IsSealed;
        EditNotes = SelectedSource.Notes ?? "";
        EditImagePath = SelectedSource.ImagePath;
        CurrentStep = 1;

        // تحميل النظائر المتعددة
        IsMultiIsotope = SelectedSource.HasDetailedIsotopes;
        IsotopeEntries.Clear();
        if (SelectedSource.HasDetailedIsotopes && SelectedSource.SourceIsotopes.Any())
        {
            foreach (var si in SelectedSource.SourceIsotopes)
            {
                IsotopeEntries.Add(new IsotopeEntryViewModel
                {
                    RadioisotopeId = si.RadioisotopeId,
                    InitialActivity = si.InitialActivityValue ?? 0,
                    InitialActivityText = (si.InitialActivityValue ?? 0).ToString(),
                    ActivityUnitId = si.ActivityUnitId
                });
            }
        }

        IsEditing = true;
    }

    [RelayCommand]
    private void ManageCertificates()
    {
        if (IsNew) return;

        if (IsNeutronForm && SelectedNeutronSource != null)
        {
            var userService = App.ServiceProvider?.GetService(typeof(IUserService)) as IUserService;
            var certService = App.ServiceProvider?.GetService(typeof(ISourceCertificateService)) as ISourceCertificateService;
            var vm = new NeutronSourceDetailsViewModel(SelectedNeutronSource, userService, certService)
            {
                SelectedTabIndex = 1
            };
            var win = new NeutronSourceDetailsWindow(vm);
            if (System.Windows.Application.Current?.MainWindow != null && System.Windows.Application.Current.MainWindow.IsVisible)
            {
                win.Owner = System.Windows.Application.Current.MainWindow;
            }
            win.ShowDialog();
        }
        else if (SelectedSource != null)
        {
            var certService = App.ServiceProvider?.GetService(typeof(ISourceCertificateService)) as ISourceCertificateService;
            var userService = App.ServiceProvider?.GetService(typeof(IUserService)) as IUserService;
            var vm = new SourceDetailsViewModel(SelectedSource, certService, userService)
            {
                SelectedTabIndex = 1
            };
            var win = new SourceDetailsWindow(vm);
            if (System.Windows.Application.Current?.MainWindow != null && System.Windows.Application.Current.MainWindow.IsVisible)
            {
                win.Owner = System.Windows.Application.Current.MainWindow;
            }
            win.ShowDialog();
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        try
        {
            if (IsNeutronForm)
            {
                if (string.IsNullOrWhiteSpace(EditSourceCode))
                {
                    ShowMessage(TranslationHelper.GetString("MsgErrSourceCodeReq"));
                    return;
                }
                if (EditNeutronTypeId == null)
                {
                    ShowMessage(TranslationHelper.GetString("MsgErrNeutronTypeReq") ?? "نوع المصدر النيتروني مطلوب");
                    return;
                }
                
                double finalRate = 0;
                if (!string.IsNullOrWhiteSpace(EditEmissionRateText))
                {
                    if (!ScientificNotationParser.TryParsePositive(EditEmissionRateText, out double parsedRate, out string? rateError))
                    {
                        ShowMessage(rateError ?? TranslationHelper.GetString("MsgErrInvalidScientificNumber") ?? "صيغة معدل انبعاث النيترونات غير صحيحة");
                        return;
                    }
                    finalRate = parsedRate;
                }
                else if (EditEmissionRate > 0)
                {
                    finalRate = EditEmissionRate;
                }
                else
                {
                    ShowMessage(TranslationHelper.GetString("MsgErrEmissionRateReq") ?? "معدل انبعاث النيترونات يجب أن يكون قيمة موجبة");
                    return;
                }
                EditEmissionRate = finalRate;

                if (EditCalibrationDate == default)
                {
                    ShowMessage(TranslationHelper.GetString("MsgErrCalibrationDateReq"));
                    return;
                }
                if (EditCalibrationDate.Date > DateTime.Today)
                {
                    ShowMessage(TranslationHelper.GetString("MsgErrCalibrationDateFuture"));
                    return;
                }
                if (EditLocationId == null)
                {
                    ShowMessage(TranslationHelper.GetString("MsgErrLocationReq"));
                    return;
                }
                if (string.IsNullOrWhiteSpace(EditStatus))
                {
                    ShowMessage(TranslationHelper.GetString("MsgErrStatusReq"));
                    return;
                }

                var neutronSource = new NeutronSource
                {
                    Id = IsNew ? Guid.NewGuid() : _editingId!.Value,
                    SourceCode = EditSourceCode.Trim(),
                    NeutronSourceTypeId = EditNeutronTypeId.Value,
                    SerialNumber = EditSerialNumber?.Trim(),
                    EmissionRate = EditEmissionRate,
                    RelativeExpandedUncertaintyPercent = EditRelativeUncertaintyPercent,
                    CalibrationDate = EditCalibrationDate,
                    LocationId = EditLocationId,
                    Status = EditStatus,
                    Notes = EditNotes?.Trim()
                };

                var neutronResult = IsNew 
                    ? (_neutronSourceService?.Create(neutronSource) ?? (false, "خدمة المصادر النيترونية غير متاحة"))
                    : (_neutronSourceService?.Update(neutronSource) ?? (false, "خدمة المصادر النيترونية غير متاحة"));

                if (neutronResult.Success)
                {
                    Message = neutronResult.Message;
                    HasMessage = true;
                    IsEditing = false;
                    DialogHelper.ShowInfo(neutronResult.Message, TranslationHelper.GetString("TitleSuccess") ?? "نجاح العملية");
                    await LoadNeutronDataAsync();
                    WeakReferenceMessenger.Default.Send(new SourcesUpdatedMessage());
                }
                else
                {
                    ShowMessage(neutronResult.Message);
                }
                return;
            }
            // 1. التحقق من الحقول الأساسية العامة (دائماً مطلوبة)
            if (string.IsNullOrWhiteSpace(EditSourceCode))
            {
                ShowMessage(TranslationHelper.GetString("MsgErrSourceCodeReq"));
                return;
            }
            if (EditCalibrationDate == default)
            {
                ShowMessage(TranslationHelper.GetString("MsgErrCalibrationDateReq"));
                return;
            }
            if (EditCalibrationDate.Date > DateTime.Today)
            {
                ShowMessage(TranslationHelper.GetString("MsgErrCalibrationDateFuture"));
                return;
            }
            if (EditCurrentUnitId == null)
            {
                ShowMessage(TranslationHelper.GetString("MsgErrUnitReq"));
                return;
            }
            if (string.IsNullOrWhiteSpace(EditStatus))
            {
                ShowMessage(TranslationHelper.GetString("MsgErrStatusReq"));
                return;
            }
            if (EditLocationId == null)
            {
                ShowMessage(TranslationHelper.GetString("MsgErrLocationReq"));
                return;
            }

            // التحقق من منع تعديل الموقع أو الحالة لمصدر قيد الاستعارة النشطة
            if (!IsNew && _editingId.HasValue && _sourceService.HasActiveBorrow(_editingId.Value))
            {
                var originalSource = _sourceService.GetSourceById(_editingId.Value) ?? SelectedSource;
                if (originalSource != null && (originalSource.LocationId != EditLocationId || originalSource.Status != EditStatus))
                {
                    ShowMessage("لا يمكن تعديل الموقع أو الحالة لمصدر قيد الاستعارة النشطة حالياً");
                    return;
                }
            }

            // التحقق من عدم تعطيل النظائر المتعددة لمصدر يحتوي على أكثر من نظير محفوظ
            if (!IsNew && _editingId.HasValue && !IsMultiIsotope)
            {
                var originalSource = _sourceService.GetSourceById(_editingId.Value) ?? SelectedSource;
                if (originalSource?.SourceIsotopes != null && originalSource.SourceIsotopes.Count > 1)
                {
                    ShowMessage(TranslationHelper.GetString("MsgErrCannotDisableMultiIsotope"));
                    return;
                }
            }

            // 2. التحقق بناءً على نوع المصدر (مفرد أم خليط)
            if (!IsMultiIsotope)
            {
                // مصدر مفرد
                if (EditRadioisotopeId == null)
                {
                    ShowMessage(TranslationHelper.GetString("MsgErrIsotopeReq"));
                    return;
                }
                if (string.IsNullOrWhiteSpace(EditInitialActivityText) || EditInitialActivity <= 0)
                {
                    ShowMessage(TranslationHelper.GetString("MsgErrInitialActivityReq"));
                    return;
                }
                if (EditInitialUnitId == null)
                {
                    ShowMessage(TranslationHelper.GetString("MsgErrInitialUnitReq"));
                    return;
                }
            }
            else
            {
                // مصدر متعدد النظائر (خليط)
                if (!IsotopeEntries.Any())
                {
                    ShowMessage(TranslationHelper.GetString("MsgErrMixIsotopeReq"));
                    return;
                }
                if (IsotopeEntries.Any(e => e.RadioisotopeId == null))
                {
                    ShowMessage(TranslationHelper.GetString("MsgErrMixIsotopeItemReq"));
                    return;
                }
                if (IsotopeEntries.Any(e => string.IsNullOrWhiteSpace(e.InitialActivityText) || e.InitialActivity <= 0))
                {
                    ShowMessage(TranslationHelper.GetString("MsgErrMixActivityReq"));
                    return;
                }
                if (IsotopeEntries.Any(e => e.ActivityUnitId == null))
                {
                    ShowMessage(TranslationHelper.GetString("MsgErrMixUnitReq"));
                    return;
                }
            }

            // للمصادر المتعددة: RadioisotopeId = أول نظير (للتوافق مع قاعدة البيانات)
            var primaryIsotopeId = IsMultiIsotope
                ? IsotopeEntries.First().RadioisotopeId!.Value
                : EditRadioisotopeId!.Value;

            var source = new Source
            {
                Id = IsNew ? Guid.NewGuid() : _editingId!.Value,
                SourceCode = EditSourceCode,
                RadioisotopeId = primaryIsotopeId,
                SerialNumber = EditSerialNumber,
                Manufacturer = EditManufacturer,
                Model = EditModel,
                InitialActivityValue = EditInitialActivity,
                InitialActivityUnitId = EditInitialUnitId!.Value,
                CalibrationDate = EditCalibrationDate,
                CurrentActivityUnitId = EditCurrentUnitId.Value,
                LocationId = EditLocationId,
                Status = EditStatus,
                IsSealed = EditIsSealed,
                Notes = EditNotes,
                HasDetailedIsotopes = IsMultiIsotope,
                ImagePath = EditImagePath
            };

            // تجميع النظائر المتعددة
            List<SourceIsotope>? isotopes = null;
            if (IsMultiIsotope && IsotopeEntries.Any())
            {
                isotopes = IsotopeEntries.Select(e => e.ToSourceIsotope()).ToList();
            }

            var result = IsNew ? _sourceService.CreateSource(source, isotopes) : _sourceService.UpdateSource(source, isotopes);
            if (result.Success)
            {
                Message = result.Message;
                HasMessage = true;
                IsEditing = false;
                DialogHelper.ShowInfo(result.Message, "نجاح العملية");
            }
            else
            {
                ShowMessage(result.Message);
                return;
            }

        }
        catch (Exception ex)
        {
            ShowMessage(TranslationHelper.GetFormat("MsgErrGeneral", ex.Message));
            return;
        }

        // 3. إجراءات ما بعد الحفظ (مفصولة تماماً عن كتلة الحفظ الأساسية)
        try
        {
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            LoggerService.LogError("SourcesViewModel: Failed to reload data after save", ex);
        }

        try
        {
            WeakReferenceMessenger.Default.Send(new SourcesUpdatedMessage());
        }
        catch (Exception ex)
        {
            LoggerService.LogError("SourcesViewModel: Failed to broadcast SourcesUpdatedMessage after save", ex);
        }
    }

    [RelayCommand]
    private async Task ExportToPdfAsync()
    {
        var sfd = new SaveFileDialog { Filter = "PDF Files (*.pdf)|*.pdf", FileName = $"Inventory_{DateTime.Now:yyyyMMdd}" };
        if (sfd.ShowDialog() == true)
        {
            try 
            { 
                await _reportingService.GenerateInventoryReportPdfAsync(Sources, sfd.FileName, "تقرير جرد المصادر المشعة");
                FileHelper.OpenFile(sfd.FileName);
            }
            catch (Exception ex) { DialogHelper.ShowError(TranslationHelper.GetFormat("MsgErrExportPdf", ex.Message)); }
        }
    }

    [RelayCommand]
    private async Task ExportToExcelAsync()
    {
        var sfd = new SaveFileDialog { Filter = "Excel Files (*.xlsx)|*.xlsx", FileName = $"Inventory_{DateTime.Now:yyyyMMdd}" };
        if (sfd.ShowDialog() == true)
        {
            try 
            { 
                await _reportingService.GenerateInventoryReportExcelAsync(Sources, sfd.FileName, "جرد المصادر");
                FileHelper.OpenFile(sfd.FileName);
            }
            catch (Exception ex) { DialogHelper.ShowError(TranslationHelper.GetFormat("MsgErrExportExcel", ex.Message)); }
        }
    }

    [RelayCommand]
    private async Task DeleteSourceAsync(Source source)
    {
        if (source == null) return;

        string confirmMsg = TranslationHelper.GetString("MsgConfirmDeleteSource") ?? "هل أنت متأكد من حذف هذا المصدر المشع؟";
        string confirmTitle = TranslationHelper.GetString("AlertConfirmation") ?? "تأكيد الحذف";
        if (!DialogHelper.ShowConfirmation(confirmMsg, confirmTitle)) return;

        var result = _sourceService.DeleteSource(source.Id);
        if (!result.Success)
        {
            ShowMessage(result.Message);
        }
        else
        {
            Message = result.Message;
            HasMessage = true;
            try
            {
                await LoadDataAsync();
            }
            catch (Exception ex)
            {
                LoggerService.LogError("SourcesViewModel: Failed to reload data after delete", ex);
            }

            try
            {
                WeakReferenceMessenger.Default.Send(new SourcesUpdatedMessage());
            }
            catch (Exception ex)
            {
                LoggerService.LogError("SourcesViewModel: Failed to broadcast SourcesUpdatedMessage after delete", ex);
            }
        }
    }

    [RelayCommand]
    private void ViewSourceDetails(object? param)
    {
        SourceNavigationHelper.OpenSourceDetails(param);
    }

    [RelayCommand]
    private void CancelEdit()
    {
        IsEditing = false;
        ClearForm();
    }


    // ─── أوامر النظائر المتعددة ───
    [RelayCommand]
    private void AddIsotopeEntry()
    {
        var entry = new IsotopeEntryViewModel
        {
            ActivityUnitId = ActivityUnits.FirstOrDefault()?.Id
        };
        IsotopeEntries.Add(entry);
    }

    [RelayCommand]
    private void RemoveIsotopeEntry(IsotopeEntryViewModel entry)
    {
        IsotopeEntries.Remove(entry);
    }

    private void ShowMessage(string msg)
    {
        Message = msg;
        HasMessage = true;
        if (!string.IsNullOrWhiteSpace(msg))
        {
            DialogHelper.ShowWarning(msg, TranslationHelper.GetString("TitleWarning") ?? "تنبيه");
        }
    }

    [RelayCommand]
    private void PickImage()
    {
        var openFileDialog = new OpenFileDialog
        {
            Filter = "Image Files (*.jpg;*.jpeg;*.png;*.bmp)|*.jpg;*.jpeg;*.png;*.bmp",
            Title = TranslationHelper.GetString("TitleSelectImage")
        };

        if (openFileDialog.ShowDialog() == true)
        {
            try
            {
                string sourceFile = openFileDialog.FileName;
                string extension = Path.GetExtension(sourceFile);
                string fileName = $"{Guid.NewGuid()}{extension}";
                string destinationPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "SourceImages");
                
                if (!Directory.Exists(destinationPath))
                    Directory.CreateDirectory(destinationPath);

                string finalPath = Path.Combine(destinationPath, fileName);
                File.Copy(sourceFile, finalPath, true);

                // حفظ المسار النسبي لسهولة النقل مع توحيد الفواصل لتكون '/'
                EditImagePath = Path.Combine("Assets", "SourceImages", fileName).Replace("\\", "/");
            }
            catch (Exception ex)
            {
                DialogHelper.ShowError(TranslationHelper.GetFormat("MsgErrImageLoad", ex.Message));
            }
        }
    }
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

    // ─── أوامر التنقل بين الصفحات ───
    private bool CanGoToPreviousPage => CurrentPage > 1;
    private bool CanGoToNextPage => CurrentPage < TotalPages;

    [RelayCommand(CanExecute = nameof(CanGoToPreviousPage))]
    private void FirstPage()
    {
        if (CurrentPage > 1)
        {
            CurrentPage = 1;
            UpdatePagedSources();
        }
    }

    [RelayCommand(CanExecute = nameof(CanGoToPreviousPage))]
    private void PreviousPage()
    {
        if (CurrentPage > 1)
        {
            CurrentPage--;
            UpdatePagedSources();
        }
    }

    [RelayCommand(CanExecute = nameof(CanGoToNextPage))]
    private void NextPage()
    {
        if (CurrentPage < TotalPages)
        {
            CurrentPage++;
            UpdatePagedSources();
        }
    }

    [RelayCommand(CanExecute = nameof(CanGoToNextPage))]
    private void LastPage()
    {
        if (CurrentPage < TotalPages)
        {
            CurrentPage = TotalPages;
            UpdatePagedSources();
        }
    }

    private void UpdatePagination()
    {
        TotalPages = (int)Math.Ceiling((double)Sources.Count / PageSize);
        if (TotalPages == 0) TotalPages = 1;
        if (CurrentPage > TotalPages) CurrentPage = TotalPages;
        UpdatePagedSources();
    }

    private void UpdatePagedSources()
    {
        var items = Sources.Skip((CurrentPage - 1) * PageSize).Take(PageSize).ToList();
        PagedSources = new ObservableCollection<Source>(items);
        PageStatusText = TranslationHelper.GetFormat("PageStatusFormat", CurrentPage, TotalPages, Sources.Count);
        FirstPageCommand.NotifyCanExecuteChanged();
        PreviousPageCommand.NotifyCanExecuteChanged();
        NextPageCommand.NotifyCanExecuteChanged();
        LastPageCommand.NotifyCanExecuteChanged();
    }


    /// <summary>
    /// تحديث إجمالي النشاط بجميع الوحدات المتاحة
    /// </summary>
    private void UpdateTotalActivityItems()
    {
        try
        {
            // حساب الإجمالي بالـ Bq (تحويل كل مصدر من وحدته إلى Bq)
            double totalBq = 0;
            foreach (var source in Sources)
            {
                var unit = source.CurrentActivityUnit;
                if (unit != null)
                    totalBq += source.CurrentActivityValue * unit.ConversionToBq;
                else
                    totalBq += source.CurrentActivityValue;
            }

            // تحويل الإجمالي إلى جميع الوحدات المتاحة
            var items = new ObservableCollection<TotalActivityItem>();
            foreach (var unit in ActivityUnits)
            {
                double convertedValue = totalBq / unit.ConversionToBq;
                items.Add(new TotalActivityItem
                {
                    UnitSymbol = unit.UnitSymbol,
                    Value = convertedValue,
                    DisplayValue = FormatActivityValue(convertedValue, unit.UnitSymbol)
                });
            }

            TotalActivityItems = items;
        }
        catch (Exception)
        {
            // في حالة خطأ، عرض القيمة الافتراضية
        }
    }

    private string FormatActivityValue(double value, string unitSymbol)
    {
        if (value == 0) return $"0 {unitSymbol}";
        if (Math.Abs(value) >= 1e9) return $"{value:E3} {unitSymbol}";
        if (Math.Abs(value) >= 1e6) return $"{value:N0} {unitSymbol}";
        if (Math.Abs(value) >= 1000) return $"{value:N2} {unitSymbol}";
        if (Math.Abs(value) >= 1) return $"{value:N4} {unitSymbol}";
        return $"{value:E3} {unitSymbol}";
    }
}

/// <summary>
/// نموذج لعرض إجمالي النشاط بوحدة معينة
/// </summary>
public class TotalActivityItem
{
    public string UnitSymbol { get; set; } = string.Empty;
    public double Value { get; set; }
    public string DisplayValue { get; set; } = string.Empty;
}

