using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Win32;
using Sources.Helpers;
using Sources.Messages;
using Sources.Models;
using Sources.Services;

namespace Sources.ViewModels;

public partial class LeakTestsViewModel : ObservableObject, IRecipient<SourcesUpdatedMessage>
{
    private readonly ILeakTestService _leakTestService;
    private readonly ISourceService _sourceService;
    private readonly IReportingService _reportingService;
    private readonly IUserService _userService;
    private readonly ISystemSettingsService _settingsService;

    // ─── مجموعات العرض ───
    [ObservableProperty] private ObservableCollection<LeakTestRecord> _pagedRecords = new();
    [ObservableProperty] private List<LeakTestRecord> _allFilteredRecords = new();
    [ObservableProperty] private ObservableCollection<Source> _sealedSources = new();

    // ─── البحث والفلاتر ───
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private string _resultFilter = "All";
    [ObservableProperty] private string _dueStatusFilter = "All";

    // ─── ترقيم الصفحات ───
    [ObservableProperty] private int _currentPage = 1;
    [ObservableProperty] private int _totalPages = 1;
    [ObservableProperty] private int _pageSize = 12;
    [ObservableProperty] private int _totalRecordsCount;
    [ObservableProperty] private string _pageStatusText = string.Empty;

    // ─── حالة النموذج المنبثق ───
    [ObservableProperty] private bool _isModalOpen;
    [ObservableProperty] private bool _isEditingRecord;
    [ObservableProperty] private string _modalTitle = string.Empty;
    private Guid? _editingRecordId;

    // ─── حقول نموذج الفحص ───
    [ObservableProperty] private Guid? _formSourceId;
    [ObservableProperty] private DateTime _formTestDate = DateTime.Today;
    [ObservableProperty] private DateTime _formNextDueDate = DateTime.Today.AddMonths(6);
    [ObservableProperty] private string _formResult = "Pass";
    [ObservableProperty] private string _formMeasuredActivityText = string.Empty;
    [ObservableProperty] private string _formInspectorName = string.Empty;
    [ObservableProperty] private string _formCertificateNumber = string.Empty;
    [ObservableProperty] private string _formNotes = string.Empty;

    public bool HasRecords => PagedRecords.Count > 0;

    public LeakTestsViewModel(
        ILeakTestService leakTestService,
        ISourceService sourceService,
        IReportingService reportingService,
        IUserService userService,
        ISystemSettingsService settingsService)
    {
        _leakTestService = leakTestService;
        _sourceService = sourceService;
        _reportingService = reportingService;
        _userService = userService;
        _settingsService = settingsService;

        WeakReferenceMessenger.Default.Register<SourcesUpdatedMessage>(this);
    }

    public async Task InitializeAsync()
    {
        await LoadSealedSourcesAsync();
        await LoadDataAsync();
    }

    public void Receive(SourcesUpdatedMessage message)
    {
        // تحديث آمن على سياق الواجهة لقائمة المصادر وسجلات الفحص عند تعديل المصادر من شاشات أخرى
        _ = LoadSealedSourcesAsync();
    }

    private async Task LoadSealedSourcesAsync()
    {
        var sources = await Task.Run(() => _sourceService.GetAllSources());
        var sealedOnes = sources
            .Where(s => s.IsSealed && (s.Status == "InUse" || s.Status == "Storage"))
            .OrderBy(s => s.SourceCode)
            .ToList();

        SealedSources = new ObservableCollection<Source>(sealedOnes);
    }


    [RelayCommand]
    public async Task LoadDataAsync()
    {
        var records = await Task.Run(() => _leakTestService.GetAllRecords(ResultFilter, DueStatusFilter, SearchText));

        AllFilteredRecords = records;
        TotalRecordsCount = records.Count;
        TotalPages = Math.Max(1, (int)Math.Ceiling(records.Count / (double)PageSize));
        if (CurrentPage > TotalPages) CurrentPage = TotalPages;
        if (CurrentPage < 1) CurrentPage = 1;

        UpdatePagedView();
    }


    private void UpdatePagedView()
    {
        var paged = AllFilteredRecords
            .Skip((CurrentPage - 1) * PageSize)
            .Take(PageSize)
            .ToList();

        PagedRecords = new ObservableCollection<LeakTestRecord>(paged);
        OnPropertyChanged(nameof(HasRecords));

        PageStatusText = $"عرض {paged.Count} من أصل {TotalRecordsCount} سجل (الصفحة {CurrentPage} من {TotalPages})";
    }

    partial void OnResultFilterChanged(string value) => _ = LoadDataAsync();
    partial void OnDueStatusFilterChanged(string value) => _ = LoadDataAsync();

    partial void OnFormTestDateChanged(DateTime value)
    {
        if (!IsEditingRecord)
        {
            FormNextDueDate = _leakTestService.CalculateNextDueDate(value);
        }
    }


    [RelayCommand]
    private async Task SearchAsync()
    {
        CurrentPage = 1;
        await LoadDataAsync();
    }

    [RelayCommand]
    private async Task ResetSearchAsync()
    {
        SearchText = string.Empty;
        ResultFilter = "All";
        DueStatusFilter = "All";
        CurrentPage = 1;
        await LoadDataAsync();
    }

    [RelayCommand]
    private void NextPage()
    {
        if (CurrentPage < TotalPages)
        {
            CurrentPage++;
            UpdatePagedView();
        }
    }

    [RelayCommand]
    private void PreviousPage()
    {
        if (CurrentPage > 1)
        {
            CurrentPage--;
            UpdatePagedView();
        }
    }

    // ─── أوامر النموذج المنبثق ───
    [RelayCommand]
    public void OpenAddModal(Source? preselectedSource = null)
    {
        IsEditingRecord = false;
        _editingRecordId = null;
        ModalTitle = "تسجيل اختبار تسرب جديد";

        FormSourceId = preselectedSource?.Id ?? SealedSources.FirstOrDefault()?.Id;
        FormTestDate = DateTime.Today;
        FormNextDueDate = _leakTestService.CalculateNextDueDate(FormTestDate);
        FormResult = "Pass";
        FormMeasuredActivityText = string.Empty;
        FormInspectorName = string.Empty;
        FormCertificateNumber = string.Empty;
        FormNotes = string.Empty;

        IsModalOpen = true;
    }

    [RelayCommand]
    public void OpenEditModal(LeakTestRecord record)
    {
        if (record == null) return;

        IsEditingRecord = true;
        _editingRecordId = record.Id;
        ModalTitle = $"تعديل فحص تسرب — {record.Source?.SourceCode}";

        FormSourceId = record.SourceId;
        FormTestDate = record.TestDate;
        FormNextDueDate = record.NextDueDate;
        FormResult = record.Result;
        FormMeasuredActivityText = record.MeasuredActivityBq.HasValue ? record.MeasuredActivityBq.Value.ToString() : string.Empty;
        FormInspectorName = record.InspectorName ?? string.Empty;
        FormCertificateNumber = record.CertificateNumber ?? string.Empty;
        FormNotes = record.Notes ?? string.Empty;

        IsModalOpen = true;
    }

    [RelayCommand]
    private void CloseModal()
    {
        IsModalOpen = false;
    }

    [RelayCommand]
    private async Task SaveRecordAsync()
    {
        if (!FormSourceId.HasValue || FormSourceId.Value == Guid.Empty)
        {
            DialogHelper.ShowWarning("يرجى اختيار المصدر المشع الخاضع للفحص", "بيانات ناقصة");
            return;
        }

        if (FormTestDate > DateTime.Today.AddDays(1))
        {
            DialogHelper.ShowWarning("لا يمكن أن يكون تاريخ الفحص في المستقبل", "تاريخ غير صحيح");
            return;
        }

        if (FormNextDueDate < FormTestDate)
        {
            DialogHelper.ShowWarning("تاريخ الاستحقاق القادم يجب أن يكون بعد تاريخ الفحص الحالي", "تاريخ غير صحيح");
            return;
        }

        double? measuredBq = null;
        if (!string.IsNullOrWhiteSpace(FormMeasuredActivityText))
        {
            if (double.TryParse(FormMeasuredActivityText, out double parsedBq) && parsedBq >= 0)
            {
                measuredBq = parsedBq;
            }
            else
            {
                DialogHelper.ShowWarning("النشاط الإشعاعي المقاس يجب أن يكون رقماً موجباً", "قيمة غير صالحة");
                return;
            }
        }

        if (IsEditingRecord && _editingRecordId.HasValue)
        {
            var recordToUpdate = new LeakTestRecord
            {
                Id = _editingRecordId.Value,
                SourceId = FormSourceId.Value,
                TestDate = FormTestDate,
                NextDueDate = FormNextDueDate,
                Result = FormResult,
                MeasuredActivityBq = measuredBq,
                InspectorName = string.IsNullOrWhiteSpace(FormInspectorName) ? null : FormInspectorName.Trim(),
                CertificateNumber = string.IsNullOrWhiteSpace(FormCertificateNumber) ? null : FormCertificateNumber.Trim(),
                Notes = string.IsNullOrWhiteSpace(FormNotes) ? null : FormNotes.Trim()
            };

            var (success, message) = _leakTestService.UpdateRecord(recordToUpdate);
            if (success)
            {
                IsModalOpen = false;
                await LoadDataAsync();
                try
                {
                    WeakReferenceMessenger.Default.Send(new SourcesUpdatedMessage());
                }
                catch { }
                DialogHelper.ShowInfo(message, "نجاح العملية");
            }
            else
            {
                DialogHelper.ShowError(message);
            }
        }
        else
        {
            var newRecord = new LeakTestRecord
            {
                SourceId = FormSourceId.Value,
                TestDate = FormTestDate,
                NextDueDate = FormNextDueDate,
                Result = FormResult,
                MeasuredActivityBq = measuredBq,
                InspectorName = string.IsNullOrWhiteSpace(FormInspectorName) ? null : FormInspectorName.Trim(),
                CertificateNumber = string.IsNullOrWhiteSpace(FormCertificateNumber) ? null : FormCertificateNumber.Trim(),
                Notes = string.IsNullOrWhiteSpace(FormNotes) ? null : FormNotes.Trim()
            };

            var (success, message, _) = _leakTestService.AddRecord(newRecord);
            if (success)
            {
                IsModalOpen = false;
                await LoadDataAsync();
                try
                {
                    WeakReferenceMessenger.Default.Send(new SourcesUpdatedMessage());
                }
                catch { }
                DialogHelper.ShowInfo(message, "نجاح العملية");
            }
            else
            {
                DialogHelper.ShowError(message);
            }
        }
    }

    [RelayCommand]
    private async Task DeleteRecordAsync(LeakTestRecord record)
    {
        if (record == null) return;

        bool confirm = DialogHelper.ShowConfirmation(
            $"هل أنت متأكد من حذف سجل فحص التسرب للمصدر {record.Source?.SourceCode} بتاريخ {record.TestDate:yyyy/MM/dd}؟",
            "تأكيد الحذف");

        if (!confirm) return;

        var (success, message) = _leakTestService.DeleteRecord(record.Id);
        if (success)
        {
            await LoadDataAsync();
            try
            {
                WeakReferenceMessenger.Default.Send(new SourcesUpdatedMessage());
            }
            catch { }
            DialogHelper.ShowInfo(message, "نجاح الحذف");
        }
        else
        {
            DialogHelper.ShowError(message);
        }
    }


    [RelayCommand]
    private void ViewRecordDetails(LeakTestRecord record)
    {
        if (record == null) return;

        string lre = "\u202A";
        string pdf = "\u202C";

        string details = $"كود المصدر: {lre}{record.Source?.SourceCode ?? "—"}{pdf}\n" +
                         $"النظير المشع: {lre}{record.Source?.DisplayIsotopes ?? "—"}{pdf}\n" +
                         $"تاريخ الفحص: {lre}{record.TestDate:yyyy-MM-dd}{pdf}\n" +
                         $"تاريخ الاستحقاق القادم: {lre}{record.NextDueDate:yyyy-MM-dd}{pdf}\n" +
                         $"نتيجة الفحص: {record.ArabicResult}\n" +
                         $"النشاط المقاس: {lre}{(record.MeasuredActivityBq.HasValue ? record.MeasuredActivityBq.Value.ToString("N2") + " Bq" : "غير محدد")}{pdf}\n" +
                         $"القائم بالفحص / المفتش: {lre}{(!string.IsNullOrWhiteSpace(record.InspectorName) ? record.InspectorName : record.PerformedByUser?.FullName ?? "—")}{pdf}\n" +
                         $"رقم شهادة الفحص: {lre}{record.CertificateNumber ?? "—"}{pdf}\n" +
                         $"تاريخ الإدخال في النظام: {lre}{record.CreatedAt:yyyy-MM-dd HH:mm}{pdf}\n" +
                         $"ملاحظات: {record.Notes ?? "لا توجد"}";

        DialogHelper.ShowInfo(details, "تفاصيل سجل اختبار التسرب", record.Source?.ImagePath);
    }

    // ─── أوامر التصدير ───
    [RelayCommand]
    private async Task ExportToPdfAsync()
    {
        var sfd = new SaveFileDialog
        {
            Filter = "PDF Files (*.pdf)|*.pdf",
            FileName = $"LeakTests_Report_{DateTime.Now:yyyyMMdd_HHmm}"
        };

        if (sfd.ShowDialog() == true)
        {
            try
            {
                await _reportingService.GenerateLeakTestsReportPdfAsync(AllFilteredRecords, sfd.FileName, "تقرير اختبارات التسرب والمسح الإشعاعي الدوري");
                FileHelper.OpenFile(sfd.FileName);
            }
            catch (Exception ex)
            {
                DialogHelper.ShowError($"فشل تصدير ملف PDF: {ex.Message}");
            }
        }
    }

    [RelayCommand]
    private async Task ExportToExcelAsync()
    {
        var sfd = new SaveFileDialog
        {
            Filter = "Excel Files (*.xlsx)|*.xlsx",
            FileName = $"LeakTests_Report_{DateTime.Now:yyyyMMdd_HHmm}"
        };

        if (sfd.ShowDialog() == true)
        {
            try
            {
                await _reportingService.GenerateLeakTestsReportExcelAsync(AllFilteredRecords, sfd.FileName, "اختبارات التسرب");
                FileHelper.OpenFile(sfd.FileName);
            }
            catch (Exception ex)
            {
                DialogHelper.ShowError($"فشل تصدير ملف Excel: {ex.Message}");
            }
        }
    }
}
