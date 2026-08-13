using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sources.Models;
using Sources.Services;
using Sources.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Sources.ViewModels;

public sealed partial class BorrowViewModel : ObservableObject
{
    private readonly IBorrowService _borrowService;
    private readonly ISourceService _sourceService;
    private readonly IUserService _userService;
    private readonly IReportingService _reportingService;
    
    [ObservableProperty]
    private ObservableCollection<BorrowRequest> _requests = new();

    [ObservableProperty]
    private ObservableCollection<Source> _availableSources = new();

    [ObservableProperty]
    private ObservableCollection<User> _availableBorrowers = new();

    [ObservableProperty]
    private int _activeCount;

    [ObservableProperty]
    private int _borrowedCount;

    [ObservableProperty]
    private int _overdueCount;

    [ObservableProperty]
    private int _dueSoonCount;

    [ObservableProperty]
    private bool _isCreateDialogOpen;

    [ObservableProperty]
    private bool _isActionDialogOpen;

    [ObservableProperty]
    private Source? _selectedSourceForNew;

    [ObservableProperty]
    private string _newBorrowerName = string.Empty;

    [ObservableProperty]
    private string _selectedSourceInfo = string.Empty;

    [ObservableProperty]
    private string _newPurpose = string.Empty;

    [ObservableProperty]
    private DateTime _newExpectedReturnDate = DateTime.Now.AddDays(7);
    
    // Action Dialogs (Approve/Reject/Return/Deliver)
    [ObservableProperty]
    private BorrowRequest? _actionRequest;
    
    [ObservableProperty]
    private string _rejectionReason = string.Empty;

    [ObservableProperty]
    private User? _selectedReturnedBy;

    // Filters
    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private string _selectedStatusFilter = "الكل";

    public ObservableCollection<string> StatusFilters { get; } = new() 
    { 
        "الكل", "تم التسليم", "تم الإرجاع", "متأخر" 
    };

    public BorrowViewModel(IBorrowService borrowService, ISourceService sourceService, IUserService userService, IReportingService reportingService)
    {
        _borrowService = borrowService;
        _sourceService = sourceService;
        _userService = userService;
        _reportingService = reportingService;
        _ = LoadDataAsync();
    }

    [RelayCommand]
    public async Task LoadDataAsync()
    {
        await Task.Run(() =>
        {
            _borrowService.CheckAndUpdateOverdue();
            var all = _borrowService.GetAll();
            
            App.Current.Dispatcher.Invoke(() =>
            {
                Requests.Clear();
                foreach (var r in all) Requests.Add(r);
                UpdateStatistics(all);
            });
        });
    }

    private void UpdateStatistics(System.Collections.Generic.List<BorrowRequest> all)
    {
        ActiveCount = all.Count(r => r.Status == "Delivered" || r.Status == "Overdue");
        BorrowedCount = all.Count(r => r.Status == "Delivered");
        OverdueCount = all.Count(r => r.Status == "Overdue");

        var soonDate = DateTime.Now.AddDays(2);
        DueSoonCount = all.Count(r => (r.Status == "Delivered" || r.Status == "Approved") 
                                    && r.ExpectedReturnDate <= soonDate && r.ExpectedReturnDate >= DateTime.Now);
    }

    [RelayCommand]
    private void OpenCreateDialog()
    {
        var dbFactory = App.ServiceProvider.GetService(typeof(Microsoft.EntityFrameworkCore.IDbContextFactory<Data.AppDbContext>)) 
            as Microsoft.EntityFrameworkCore.IDbContextFactory<Data.AppDbContext>;
        
        if (dbFactory == null) return;
        
        using var db = dbFactory.CreateDbContext();
        var sources = db.Sources
            .Include(s => s.SourceIsotopes)
                .ThenInclude(si => si.Radioisotope)
            .Include(s => s.SourceIsotopes)
                .ThenInclude(si => si.ActivityUnit)
            .Include(s => s.Location)
            .Include(s => s.Radioisotope)
            .Include(s => s.InitialActivityUnit)
            .Where(s => s.Status == "Storage").ToList();
        
        AvailableSources.Clear();
        foreach (var s in sources) AvailableSources.Add(s);

        SelectedSourceForNew = null;
        NewBorrowerName = string.Empty;
        NewPurpose = string.Empty;
        NewExpectedReturnDate = DateTime.Now.AddDays(7);
        SelectedSourceInfo = string.Empty;

        IsCreateDialogOpen = true;
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

    [RelayCommand]
    private async Task SubmitCreateRequestAsync()
    {
        if (SelectedSourceForNew == null || string.IsNullOrWhiteSpace(NewBorrowerName) || string.IsNullOrWhiteSpace(NewPurpose))
        {
            DialogHelper.ShowError("الرجاء إكمال جميع الحقول المطلوبة.");
            return;
        }

        if (NewExpectedReturnDate.Date < DateTime.Now.Date)
        {
            DialogHelper.ShowError("تاريخ الإرجاع المتوقع لا يمكن أن يكون في الماضي.");
            return;
        }

        // رسالة تأكيد قبل الحفظ
        string confirmMsg = $"سيتم تسليم المصدر ({SelectedSourceForNew.SourceCode}) إلى ({NewBorrowerName}).\nهل أنت متأكد؟";
        if (!DialogHelper.ShowConfirmation(confirmMsg, "تأكيد الاستعارة"))
            return;

        var req = new BorrowRequest
        {
            SourceId = SelectedSourceForNew.Id,
            BorrowerName = NewBorrowerName,
            BorrowerUserId = _userService.CurrentUser?.Id, // ربط الطلب بالمستخدم الذي قام بالعملية
            Purpose = NewPurpose,
            ExpectedReturnDate = NewExpectedReturnDate
        };

        var result = _borrowService.CreateRequest(req);
        if (result.Success)
        {
            IsCreateDialogOpen = false;
            DialogHelper.ShowInfo(result.Message);
            await LoadDataAsync();
        }
        else
        {
            DialogHelper.ShowError(result.Message);
        }
    }

    [RelayCommand]
    private void PrepareAction(BorrowRequest request)
    {
        ActionRequest = request;
        RejectionReason = string.Empty;
        
        if (request.Status == "Delivered" || request.Status == "Overdue" || request.Status == "Approved")
        {
            var dbFactory = App.ServiceProvider.GetService(typeof(Microsoft.EntityFrameworkCore.IDbContextFactory<Data.AppDbContext>)) 
                as Microsoft.EntityFrameworkCore.IDbContextFactory<Data.AppDbContext>;
            if (dbFactory == null) return;
            using var db = dbFactory.CreateDbContext();
            
            var users = db.Users.Where(u => u.IsActive).ToList();
            AvailableBorrowers.Clear();
            foreach (var u in users) AvailableBorrowers.Add(u);
            
            SelectedReturnedBy = AvailableBorrowers.FirstOrDefault(u => u.Id == request.BorrowerUserId);
        }
        
        IsActionDialogOpen = true;
    }

    [RelayCommand]
    private async Task ExecuteApproveAsync()
    {
        if (ActionRequest == null) return;
        var currentUser = _userService.CurrentUser;
        if (currentUser == null) return;

        var result = _borrowService.ApproveRequest(ActionRequest.Id, currentUser.Id);
        if (result.Success)
        {
            IsActionDialogOpen = false;
            DialogHelper.ShowInfo(result.Message);
            await LoadDataAsync();
        }
        else
            DialogHelper.ShowError(result.Message);
    }

    [RelayCommand]
    private async Task ExecuteRejectAsync()
    {
        if (ActionRequest == null) return;
        if (string.IsNullOrWhiteSpace(RejectionReason))
        {
            DialogHelper.ShowError("يجب كتابة سبب الرفض.");
            return;
        }

        var currentUser = _userService.CurrentUser;
        if (currentUser == null) return;

        var result = _borrowService.RejectRequest(ActionRequest.Id, currentUser.Id, RejectionReason);
        if (result.Success)
        {
            IsActionDialogOpen = false;
            DialogHelper.ShowInfo(result.Message);
            await LoadDataAsync();
        }
        else
            DialogHelper.ShowError(result.Message);
    }

    [RelayCommand]
    private async Task ExecuteDeliverAsync()
    {
        if (ActionRequest == null) return;

        var result = _borrowService.MarkDelivered(ActionRequest.Id);
        if (result.Success)
        {
            IsActionDialogOpen = false;
            DialogHelper.ShowInfo(result.Message);
            await LoadDataAsync();
        }
        else
            DialogHelper.ShowError(result.Message);
    }

    [RelayCommand]
    private async Task ExecuteReturnAsync()
    {
        if (ActionRequest == null) return;
        if (SelectedReturnedBy == null)
        {
            DialogHelper.ShowError("الرجاء تحديد من قام بإرجاع المصدر.");
            return;
        }

        var result = _borrowService.MarkReturned(ActionRequest.Id, SelectedReturnedBy.Id);
        if (result.Success)
        {
            IsActionDialogOpen = false;
            DialogHelper.ShowInfo(result.Message);
            await LoadDataAsync();
        }
        else
            DialogHelper.ShowError(result.Message);
    }

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
                var viewableRequests = Requests.ToList();
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
                var viewableRequests = Requests.ToList();
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
        var all = _borrowService.GetAll();
        
        var filtered = all.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(SearchQuery))
        {
            filtered = filtered.Where(r => 
                (r.Source?.SourceCode?.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (r.BorrowerName?.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (r.BorrowerUser?.FullName?.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        if (SelectedStatusFilter != "الكل")
        {
            string enStatus = SelectedStatusFilter switch
            {
                "معلّق" => "Pending",
                "تمت الموافقة" => "Approved",
                "مرفوض" => "Rejected",
                "تم التسليم" => "Delivered",
                "تم الإرجاع" => "Returned",
                "متأخر" => "Overdue",
                _ => ""
            };
            if (!string.IsNullOrEmpty(enStatus))
                filtered = filtered.Where(r => r.Status == enStatus);
        }

        Requests.Clear();
        foreach (var item in filtered.ToList())
        {
            Requests.Add(item);
        }
    }
}
