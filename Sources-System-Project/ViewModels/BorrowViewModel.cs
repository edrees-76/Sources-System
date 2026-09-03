using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.EntityFrameworkCore;
using Sources.Data;
using Sources.Helpers;
using Sources.Interfaces;
using Sources.Messages;
using Sources.Models;
using Sources.Services;

namespace Sources.ViewModels;

/// <summary>
/// صف عرض مخصص لجدول استعارة المصادر يضمن ثبات الرقم التسلسلي # أثناء التمرير والفلترة
/// </summary>
public class BorrowRequestRow
{
    public int RowNumber { get; set; }
    public BorrowRequest Request { get; set; } = null!;
    public Guid Id => Request.Id;
    public Source? Source => Request.Source;
    public string DisplaySourceCode => Request.DisplaySourceCode;
    public string DisplayIsotopes => Source?.DisplayIsotopes ?? "-";
    public string CurrentActivityWithUnit => Source?.CurrentActivityWithUnit ?? "-";
    public string DisplayDoseRate => Source?.DisplayDoseRate ?? "-";
    public string DoseRateTooltip => Source?.DoseRateTooltip ?? string.Empty;
    public string BorrowerName => Request.BorrowerName;
    public string DisplayBorrowerName => Request.DisplayBorrowerName;
    public DateTime RequestDate => Request.RequestDate;
    public DateTime ExpectedReturnDate => Request.ExpectedReturnDate;
    public DateTime? ActualReturnDate => Request.ActualReturnDate;
    public string Purpose => Request.Purpose;
    public string Status => Request.Status;
    public string ArabicStatus => Request.ArabicStatus;
    public string? Notes => Request.Notes;
    public string? AddedByName => Request.AddedByName;
}

public sealed partial class BorrowViewModel : ObservableObject, IEditableViewModel, IDisposable
{
    private readonly IBorrowService _borrowService;
    private readonly ISourceService _sourceService;
    private readonly IUserService _userService;
    private readonly IReportingService _reportingService;
    private readonly IDbContextFactory<AppDbContext>? _dbFactory;
    private readonly IMessenger _messenger;

    public void Dispose()
    {
        _messenger.UnregisterAll(this);
    }

    // ─── مجموعات البيانات ───
    private System.Collections.Generic.List<BorrowRequest> _allRequests = new();

    [ObservableProperty]
    private ObservableCollection<BorrowRequestRow> _requests = new();

    [ObservableProperty]
    private ObservableCollection<Source> _availableSources = new();

    [ObservableProperty]
    private ObservableCollection<User> _availableBorrowers = new();

    // ─── المشهد المزدوج والتنقل ───
    [ObservableProperty]
    private bool _isEditing;

    [ObservableProperty]
    private bool _isNew;

    [ObservableProperty]
    private int _currentStep = 1;

    [ObservableProperty]
    private BorrowRequest? _selectedRequest;

    // ─── حقول وضع الإضافة (IsNew = true) ───
    [ObservableProperty]
    private Source? _selectedSourceForNew;

    [ObservableProperty]
    private string _selectedSourceInfo = string.Empty;

    [ObservableProperty]
    private string _newBorrowerName = string.Empty;

    [ObservableProperty]
    private string _newPurpose = string.Empty;

    [ObservableProperty]
    private DateTime _newExpectedReturnDate = DateTime.Now.AddDays(7);

    [ObservableProperty]
    private string? _newNotes = string.Empty;

    // ─── حقول وضع العرض والإرجاع (IsNew = false) ───
    [ObservableProperty]
    private DateTime _newActualReturnDate = DateTime.Now;

    [ObservableProperty]
    private User? _selectedReturnedBy;

    [ObservableProperty]
    private string? _returnNotes = string.Empty;

    // ─── الإحصائيات ───
    [ObservableProperty]
    private int _totalCount;

    [ObservableProperty]
    private int _activeCount;

    [ObservableProperty]
    private int _borrowedCount;

    [ObservableProperty]
    private int _overdueCount;

    [ObservableProperty]
    private int _dueSoonCount;

    // ─── البحث والتصفية ───
    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private string _selectedStatusFilter = "الكل";

    public ObservableCollection<string> StatusFilters { get; } = new()
    {
        "الكل", "تم التسليم", "تم الإرجاع", "متأخر", "قريبة الإرجاع"
    };

    public BorrowViewModel(
        IBorrowService borrowService, 
        ISourceService sourceService, 
        IUserService userService, 
        IReportingService reportingService,
        IDbContextFactory<AppDbContext>? dbFactory = null,
        IMessenger? messenger = null)
    {
        _messenger = messenger ?? WeakReferenceMessenger.Default;
        _borrowService = borrowService;
        _sourceService = sourceService;
        _userService = userService;
        _reportingService = reportingService;
        _dbFactory = dbFactory ?? (App.ServiceProvider?.GetService(typeof(IDbContextFactory<AppDbContext>)) as IDbContextFactory<AppDbContext>);

        // الاستماع لرسالة تحديث المصادر لتحديث قائمة المصادر المتاحة تلقائياً
        _messenger.Register<SourcesUpdatedMessage>(this, (r, m) =>
        {
            RunOnUI(() =>
            {
                LoadAvailableSources();
            });
        });

        _ = LoadDataAsync();
    }

    private static void RunOnUI(Action action)
    {
        if (Application.Current?.Dispatcher != null && !Application.Current.Dispatcher.CheckAccess())
        {
            Application.Current.Dispatcher.Invoke(action);
        }
        else
        {
            action();
        }
    }

    [RelayCommand]
    public async Task LoadDataAsync()
    {
        await Task.Run(() =>
        {
            _borrowService.CheckAndUpdateOverdue();
            var all = _borrowService.GetAll() ?? new System.Collections.Generic.List<BorrowRequest>();
            _allRequests = all;

            void updateUi()
            {
                var list = new ObservableCollection<BorrowRequestRow>();
                for (int i = 0; i < all.Count; i++)
                {
                    list.Add(new BorrowRequestRow
                    {
                        RowNumber = i + 1,
                        Request = all[i]
                    });
                }
                Requests = list;
                UpdateStatistics(all);
            }

            if (App.Current?.Dispatcher != null)
            {
                App.Current.Dispatcher.Invoke(updateUi);
            }
            else
            {
                updateUi();
            }
        });
    }

    private void UpdateStatistics(System.Collections.Generic.List<BorrowRequest> all)
    {
        TotalCount = all.Count;
        ActiveCount = all.Count(r => r.Status == "Delivered" || r.Status == "Overdue");
        BorrowedCount = all.Count(r => r.Status == "Delivered");
        OverdueCount = all.Count(r => r.Status == "Overdue");
        DueSoonCount = _borrowService.GetDueSoonCount(all);
    }

    // ─── أوامر التنقل بين المشهدين والخطوات ───

    [RelayCommand]
    private void AddNew()
    {
        IsNew = true;
        CurrentStep = 1;
        ClearForm();
        LoadAvailableSources();
        IsEditing = true;
    }

    [RelayCommand]
    private void OpenCreateDialog() => AddNew();

    [RelayCommand]
    private void Edit(object? param = null)
    {
        BorrowRequest? request = param switch
        {
            BorrowRequestRow r => r.Request,
            BorrowRequest br => br,
            _ => SelectedRequest
        };
        if (request == null) return;
        SelectedRequest = request;
        IsNew = false;
        CurrentStep = 1;
        NewActualReturnDate = DateTime.Now;
        ReturnNotes = string.Empty;

        if (request.Status == "Delivered" || request.Status == "Overdue" || request.Status == "Approved")
        {
            LoadAvailableBorrowers(request.BorrowerUserId);
        }

        IsEditing = true;
    }

    [RelayCommand]
    private void NextStep()
    {
        if (SelectedSourceForNew == null || string.IsNullOrWhiteSpace(NewBorrowerName) || string.IsNullOrWhiteSpace(NewPurpose))
        {
            DialogHelper.ShowError(TranslationHelper.GetString("MsgErrStep1Incomplete") ?? "الرجاء اختيار المصدر وتحديد اسم المستعير والغرض للمتابعة.");
            return;
        }
        CurrentStep = 2;
    }

    [RelayCommand]
    private void PreviousStep()
    {
        CurrentStep = 1;
    }

    [RelayCommand]
    private void CancelEdit()
    {
        IsEditing = false;
        ClearForm();
    }

    private void ClearForm()
    {
        SelectedSourceForNew = null;
        SelectedSourceInfo = string.Empty;
        NewBorrowerName = string.Empty;
        NewPurpose = string.Empty;
        NewExpectedReturnDate = DateTime.Now.AddDays(7);
        NewNotes = string.Empty;
        ReturnNotes = string.Empty;
        NewActualReturnDate = DateTime.Now;
        SelectedReturnedBy = null;
        SelectedRequest = null;
    }

    public void LoadAvailableSources()
    {
        if (_dbFactory == null) return;
        using var db = _dbFactory.CreateDbContext();
        var candidateSources = db.Sources
            .AsNoTracking()
            .Include(s => s.SourceIsotopes)
                .ThenInclude(si => si.Radioisotope)
            .Include(s => s.SourceIsotopes)
                .ThenInclude(si => si.ActivityUnit)
            .Include(s => s.Location)
            .Include(s => s.Radioisotope)
            .Include(s => s.InitialActivityUnit)
            .Where(s => !s.IsDeleted && s.Status == "Storage")
            .OrderBy(s => s.SourceCode)
            .ToList();

        var sourceIds = candidateSources.Select(s => s.Id).ToList();
        var allTests = db.LeakTestRecords
            .AsNoTracking()
            .Where(r => sourceIds.Contains(r.SourceId))
            .ToList();

        var failedSourceIds = allTests
            .GroupBy(r => r.SourceId)
            .Select(g => g.OrderByDescending(r => r.TestDate).ThenByDescending(r => r.CreatedAt).FirstOrDefault())
            .Where(r => r != null && r.Result == "Fail")
            .Select(r => r!.SourceId)
            .ToHashSet();

        var sources = candidateSources.Where(s => !failedSourceIds.Contains(s.Id)).ToList();

        AvailableSources.Clear();
        foreach (var s in sources) AvailableSources.Add(s);
    }

    private void LoadAvailableBorrowers(Guid? borrowerUserId)
    {
        if (_dbFactory == null) return;
        using var db = _dbFactory.CreateDbContext();
        var users = db.Users.AsNoTracking().Where(u => u.IsActive).ToList();

        AvailableBorrowers.Clear();
        foreach (var u in users) AvailableBorrowers.Add(u);

        // الافتراضي للمستلم هو المستخدم الحالي المسجل دخوله بالنظام (أمين المخزن/المشغل)
        SelectedReturnedBy = AvailableBorrowers.FirstOrDefault(u => u.Id == _userService.CurrentUser?.Id) 
            ?? AvailableBorrowers.FirstOrDefault(u => u.Id == borrowerUserId);
    }

    partial void OnSelectedSourceForNewChanged(Source? value)
    {
        if (value == null)
        {
            SelectedSourceInfo = string.Empty;
            return;
        }

        var lines = new System.Collections.Generic.List<string>();
        string lre = "\u202A";
        string pdf = "\u202C";

        if (value.HasDetailedIsotopes && value.SourceIsotopes != null && value.SourceIsotopes.Any())
        {
            foreach (var si in value.SourceIsotopes)
            {
                string isotopeName = si.Radioisotope?.Symbol ?? "غير محدد";
                string activity = si.InitialActivityValue?.ToString("N4") ?? "0";
                string unit = si.ActivityUnit?.UnitSymbol ?? "";
                lines.Add($"• {lre}{isotopeName}: {activity} {unit}{pdf}");
            }
        }
        else
        {
            string isotopeName = value.Radioisotope?.Symbol ?? "غير محدد";
            string activity = value.InitialActivityValue.ToString("N4") ?? "0";
            string unit = value.InitialActivityUnit?.UnitSymbol ?? "";
            lines.Add($"• {lre}{isotopeName}: {activity} {unit}{pdf}");
        }

        string location = value.Location?.LocationName ?? "غير محدد";
        lines.Add($"📍 الموقع: {lre}{location}{pdf}");

        SelectedSourceInfo = string.Join("\n", lines);
    }

    // ─── تنفيذ حفظ الاستعارة الجديدة ───
    [RelayCommand]
    private async Task SubmitAsync()
    {
        if (SelectedSourceForNew == null || string.IsNullOrWhiteSpace(NewBorrowerName) || string.IsNullOrWhiteSpace(NewPurpose))
        {
            DialogHelper.ShowError(TranslationHelper.GetString("MsgErrStep1Incomplete") ?? "الرجاء اختيار المصدر وتحديد اسم المستعير والغرض للمتابعة.");
            return;
        }

        if (NewExpectedReturnDate.Date < DateTime.Today)
        {
            DialogHelper.ShowError(TranslationHelper.GetString("MsgErrExpectedReturnPast") ?? "تاريخ الإرجاع المتوقع لا يمكن أن يكون في الماضي.");
            return;
        }

        if (NewExpectedReturnDate.Date > DateTime.Today.AddYears(2))
        {
            DialogHelper.ShowError(TranslationHelper.GetString("MsgErrExpectedReturnTooFar") ?? "تاريخ الإرجاع المتوقع بعيد جداً (الحد الأقصى هو سنتان من اليوم).");
            return;
        }

        string confirmMsg = $"سيتم تسليم المصدر (\u2066{SelectedSourceForNew.SourceCode}\u2069) إلى (\u2066{NewBorrowerName}\u2069).\nهل أنت متأكد من المتابعة؟";
        if (!DialogHelper.ShowConfirmation(confirmMsg, TranslationHelper.GetString("AddNewBorrowRequestTitle") ?? "طلب استعارة مصدر جديد"))
            return;

        // محاولة مطابقة المستعير مع مستخدم مسجل بالنظام، وإلا يبقى null ويُعتمد على BorrowerName
        var matchedUser = AvailableBorrowers.FirstOrDefault(u =>
            string.Equals(u.FullName, NewBorrowerName.Trim(), StringComparison.OrdinalIgnoreCase) ||
            string.Equals(u.Username, NewBorrowerName.Trim(), StringComparison.OrdinalIgnoreCase));

        var req = new BorrowRequest
        {
            SourceId = SelectedSourceForNew.Id,
            BorrowerName = NewBorrowerName.Trim(),
            BorrowerUserId = matchedUser?.Id,
            Purpose = NewPurpose.Trim(),
            ExpectedReturnDate = NewExpectedReturnDate,
            Notes = string.IsNullOrWhiteSpace(NewNotes) ? null : NewNotes.Trim()
        };

        var result = _borrowService.CreateRequest(req);
        if (result.Success)
        {
            IsEditing = false;
            ClearForm();
            DialogHelper.ShowInfo(result.Message);
            await LoadDataAsync();
        }
        else
        {
            DialogHelper.ShowError(result.Message);
        }
    }

    // ─── تنفيذ إرجاع المصدر ───
    [RelayCommand]
    private async Task MarkReturnedAsync()
    {
        if (SelectedRequest == null) return;
        if (SelectedReturnedBy == null)
        {
            DialogHelper.ShowError(TranslationHelper.GetString("MsgErrRecipientRequired") ?? "الرجاء تحديد من قام باستلام/إرجاع المصدر.");
            return;
        }

        if (NewActualReturnDate.Date < SelectedRequest.RequestDate.Date)
        {
            DialogHelper.ShowError(TranslationHelper.GetString("MsgErrActualReturnBeforeRequest") ?? "تاريخ الإرجاع الفعلي لا يمكن أن يسبق تاريخ الاستعارة.");
            return;
        }

        if (NewActualReturnDate.Date > DateTime.Today)
        {
            DialogHelper.ShowError(TranslationHelper.GetString("MsgErrActualReturnFuture") ?? "لا يمكن أن يكون تاريخ الإرجاع الفعلي في المستقبل.");
            return;
        }

        var result = _borrowService.MarkReturned(SelectedRequest.Id, SelectedReturnedBy.Id, NewActualReturnDate, ReturnNotes);
        if (result.Success)
        {
            IsEditing = false;
            ClearForm();
            DialogHelper.ShowInfo(result.Message);
            await LoadDataAsync();
        }
        else
        {
            DialogHelper.ShowError(result.Message);
        }
    }

    // ─── تصدير التقارير ───
    [RelayCommand]
    private async Task ExportPdfAsync()
    {
        var saveFileDialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "PDF Files (*.pdf)|*.pdf",
            DefaultExt = "pdf",
            FileName = $"استعارة_المصادر_{DateTime.Now:yyyyMMdd}.pdf"
        };

        if (saveFileDialog.ShowDialog() == true)
        {
            try
            {
                var viewableRequests = Requests.Select(r => r.Request).ToList();
                await _reportingService.GenerateBorrowHistoryPdfAsync(viewableRequests, saveFileDialog.FileName);
                FileHelper.OpenFile(saveFileDialog.FileName);
                DialogHelper.ShowInfo("تم تصدير التقرير كملف PDF بنجاح.");
            }
            catch (Exception ex)
            {
                DialogHelper.ShowError($"خطأ في التصدير: {ex.Message}");
            }
        }
    }

    [RelayCommand]
    private async Task ExportExcelAsync()
    {
        var saveFileDialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "Excel Files (*.xlsx)|*.xlsx",
            DefaultExt = "xlsx",
            FileName = $"استعارة_المصادر_{DateTime.Now:yyyyMMdd}.xlsx"
        };

        if (saveFileDialog.ShowDialog() == true)
        {
            try
            {
                var viewableRequests = Requests.Select(r => r.Request).ToList();
                await _reportingService.GenerateBorrowHistoryExcelAsync(viewableRequests, saveFileDialog.FileName);
                FileHelper.OpenFile(saveFileDialog.FileName);
                DialogHelper.ShowInfo("تم تصدير البيانات إلى ملف Excel بنجاح.");
            }
            catch (Exception ex)
            {
                DialogHelper.ShowError($"خطأ في التصدير: {ex.Message}");
            }
        }
    }

    [RelayCommand]
    private void PerformSearch()
    {
        var filtered = _allRequests.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(SearchQuery))
        {
            filtered = filtered.Where(r =>
                (r.Source?.SourceCode?.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (r.BorrowerName?.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (r.BorrowerUser?.FullName?.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (r.Purpose?.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        if (SelectedStatusFilter != "الكل")
        {
            if (SelectedStatusFilter == "قريبة الإرجاع")
            {
                var thresholdDays = _borrowService.GetDueSoonDaysThreshold();
                var today = DateTime.Today;
                var maxDate = today.AddDays(thresholdDays + 1).Date;
                filtered = filtered.Where(r => r.Status == "Delivered" 
                    && r.ExpectedReturnDate.Date >= today 
                    && r.ExpectedReturnDate.Date < maxDate);
            }
            else
            {
                string enStatus = SelectedStatusFilter switch
                {
                    "تم التسليم" => "Delivered",
                    "تم الإرجاع" => "Returned",
                    "متأخر" => "Overdue",
                    _ => ""
                };
                if (!string.IsNullOrEmpty(enStatus))
                    filtered = filtered.Where(r => r.Status == enStatus);
            }
        }

        var list = filtered.ToList();
        Requests.Clear();
        for (int i = 0; i < list.Count; i++)
        {
            Requests.Add(new BorrowRequestRow
            {
                RowNumber = i + 1,
                Request = list[i]
            });
        }
    }

    [RelayCommand]
    private void ViewSourceDetails(object? parameter)
    {
        SourceNavigationHelper.OpenSourceDetails(parameter);
    }
}
