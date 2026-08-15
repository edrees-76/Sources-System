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
using System.Text.Json;
using System.Threading.Tasks;

namespace Sources.ViewModels;

public class AuditDiffItem
{
    public string FieldName { get; set; } = string.Empty;
    public string OldValue { get; set; } = "-";
    public string NewValue { get; set; } = "-";
    public bool HasChanged { get; set; } = true;
}

public class RoleSummaryItem
{
    public Role Role { get; set; } = null!;
    public int UsersCount { get; set; }
    public string Description { get; set; } = string.Empty;
    public List<string> GrantedSections { get; set; } = new();
}

public partial class UsersViewModel : ObservableObject, IEditableViewModel
{
    private readonly IUserService _userService;
    private readonly IReportingService _reportingService;

    // ─── إدارة التبويبات ───
    [ObservableProperty] private string _selectedTab = "UsersManagement"; // UsersManagement, AuditLog, RolesPermissions

    // ─── القوائم الأساسية ───
    [ObservableProperty] private ObservableCollection<User> _users = new();
    [ObservableProperty] private ObservableCollection<User> _filteredUsers = new();
    [ObservableProperty] private ObservableCollection<Role> _roles = new();
    [ObservableProperty] private ObservableCollection<AuditLog> _auditLogs = new();
    [ObservableProperty] private ObservableCollection<RoleSummaryItem> _roleSummaries = new();

    // ─── البحث والتصفية للمستخدمين ───
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private string _selectedStatusFilter = "All"; // All, Active, Frozen, Locked
    [ObservableProperty] private Guid? _selectedRoleFilter;

    // ─── التحديد والتحرير ───
    [ObservableProperty] private User? _selected;
    [ObservableProperty] private bool _isEditing;
    [ObservableProperty] private bool _isNew;
    [ObservableProperty] private string _message = string.Empty;
    [ObservableProperty] private bool _hasMessage;

    // ─── بطاقات إحصائية للمستخدمين ───
    [ObservableProperty] private int _totalUsersCount;
    [ObservableProperty] private int _activeUsersCount;
    [ObservableProperty] private int _adminUsersCount;
    [ObservableProperty] private int _lockedUsersCount;

    // ─── إحصائيات سجل النشاط ───
    [ObservableProperty] private int _activitiesTodayCount;
    [ObservableProperty] private int _modificationsTodayCount;
    [ObservableProperty] private string _mostActiveUser = "—";

    // ─── فلاتر سجل التدقيق ───
    [ObservableProperty] private User? _selectedUserFilter;
    [ObservableProperty] private string _selectedActionFilter = "All";
    [ObservableProperty] private DateTime? _filterStartDate;
    [ObservableProperty] private DateTime? _filterEndDate;

    // ─── عارض الفروقات لسجل التدقيق (Diff Viewer) ───
    [ObservableProperty] private AuditLog? _selectedAuditLog;
    [ObservableProperty] private ObservableCollection<AuditDiffItem> _auditDiffItems = new();
    [ObservableProperty] private bool _isDiffViewerOpen;

    // ─── حقول نموذج الإضافة / التعديل ───
    [ObservableProperty] private string _editFullName = string.Empty;
    [ObservableProperty] private string _editUsername = string.Empty;
    [ObservableProperty] private string _editPassword = string.Empty;
    [ObservableProperty] private string _editEmail = string.Empty;
    [ObservableProperty] private Guid? _editRoleId;
    [ObservableProperty] private bool _editIsActive = true;
    [ObservableProperty] private bool _editIsEditor = true;
    private Guid? _editingId;

    // ─── صلاحيات الأقسام (Checkboxes) ───
    [ObservableProperty] private bool _permRadioisotopes = true;
    [ObservableProperty] private bool _permSources = true;
    [ObservableProperty] private bool _permLocations = true;
    [ObservableProperty] private bool _permBorrowing = true;
    [ObservableProperty] private bool _permReports = true;
    [ObservableProperty] private bool _permUsers;
    [ObservableProperty] private bool _permSettings;
    [ObservableProperty] private bool _permCalculator = true;

    // ─── رؤية قسم الصلاحيات ───
    [ObservableProperty] private bool _isPermissionsSectionVisible;

    public UsersViewModel(IUserService userService, IReportingService reportingService)
    {
        _userService = userService;
        _reportingService = reportingService;
        LoadData();
    }

    [RelayCommand]
    public void SelectTab(string tabName)
    {
        if (string.IsNullOrWhiteSpace(tabName)) return;
        SelectedTab = tabName;
        if (tabName == "AuditLog")
        {
            LoadAuditLogs();
        }
    }

    [RelayCommand]
    public void LoadData()
    {
        var usersList = _userService.GetAllUsers();
        Users = new ObservableCollection<User>(usersList);
        Roles = new ObservableCollection<Role>(_userService.GetAllRoles());

        UpdateStats();
        ApplyUsersFilter();
        LoadAuditLogs();
        UpdateRoleSummaries();
    }

    private void UpdateStats()
    {
        TotalUsersCount = Users.Count;
        ActiveUsersCount = Users.Count(u => u.IsActive);
        AdminUsersCount = Users.Count(u => u.Role?.RoleName == "مدير النظام");
        LockedUsersCount = Users.Count(u => u.LockoutEnd.HasValue && u.LockoutEnd.Value > DateTime.Now);
    }

    private void UpdateRoleSummaries()
    {
        var list = new List<RoleSummaryItem>();
        foreach (var role in Roles)
        {
            var count = Users.Count(u => u.RoleId == role.Id);
            var sections = new List<string>();
            if (role.RoleName == "مدير النظام")
            {
                sections.Add("كافة أقسام المنظومة (صلاحيات كاملة)");
            }
            else
            {
                sections.Add("المصادر، النظائر، المواقع، الاستعارة، التقارير، الحاسبة");
            }

            list.Add(new RoleSummaryItem
            {
                Role = role,
                UsersCount = count,
                Description = role.Description ?? (role.RoleName == "مدير النظام" ? "صلاحيات إدارية كاملة للتحكم في كافة موارد النظام والمستخدمين" : "صلاحيات تشغيلية واستعراض للبيانات والأقسام المسموح بها"),
                GrantedSections = sections
            });
        }
        RoleSummaries = new ObservableCollection<RoleSummaryItem>(list);
    }

    // ─── تصفية قائمة المستخدمين ───
    partial void OnSearchTextChanged(string value) => ApplyUsersFilter();
    partial void OnSelectedStatusFilterChanged(string value) => ApplyUsersFilter();
    partial void OnSelectedRoleFilterChanged(Guid? value) => ApplyUsersFilter();

    public void ApplyUsersFilter()
    {
        var query = Users.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var term = SearchText.Trim().ToLower();
            query = query.Where(u =>
                (!string.IsNullOrEmpty(u.FullName) && u.FullName.ToLower().Contains(term)) ||
                (!string.IsNullOrEmpty(u.Username) && u.Username.ToLower().Contains(term)) ||
                (!string.IsNullOrEmpty(u.Email) && u.Email.ToLower().Contains(term)) ||
                (u.Role != null && u.Role.DisplayName.ToLower().Contains(term)));
        }

        if (SelectedStatusFilter == "Active")
        {
            query = query.Where(u => u.IsActive && (!u.LockoutEnd.HasValue || u.LockoutEnd.Value <= DateTime.Now));
        }
        else if (SelectedStatusFilter == "Frozen")
        {
            query = query.Where(u => !u.IsActive);
        }
        else if (SelectedStatusFilter == "Locked")
        {
            query = query.Where(u => u.LockoutEnd.HasValue && u.LockoutEnd.Value > DateTime.Now);
        }

        if (SelectedRoleFilter.HasValue && SelectedRoleFilter.Value != Guid.Empty)
        {
            query = query.Where(u => u.RoleId == SelectedRoleFilter.Value);
        }

        FilteredUsers = new ObservableCollection<User>(query.ToList());
    }

    // ─── سجل التدقيق والنشاطات ───
    [RelayCommand]
    private void LoadAuditLogs()
    {
        var logs = _userService.GetAuditLogs(SelectedUserFilter?.Id, FilterStartDate, FilterEndDate);

        if (SelectedActionFilter != "All" && !string.IsNullOrEmpty(SelectedActionFilter))
        {
            logs = logs.Where(l => l.Action.Equals(SelectedActionFilter, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        AuditLogs = new ObservableCollection<AuditLog>(logs);

        var today = DateTime.Today;
        ActivitiesTodayCount = logs.Count(l => l.ActionDate.Date == today);
        ModificationsTodayCount = logs.Count(l => l.ActionDate.Date == today &&
            (l.Action.Contains("Update") || l.Action.Contains("Delete") || l.Action.Contains("تعديل") || l.Action.Contains("حذف")));
        MostActiveUser = logs.GroupBy(l => l.User?.FullName ?? "—")
            .OrderByDescending(g => g.Count())
            .FirstOrDefault()?.Key ?? "—";
    }

    [RelayCommand]
    private void ClearFilters()
    {
        SelectedUserFilter = null;
        SelectedActionFilter = "All";
        FilterStartDate = null;
        FilterEndDate = null;
        LoadAuditLogs();
    }

    partial void OnSelectedUserFilterChanged(User? value) => LoadAuditLogs();
    partial void OnSelectedActionFilterChanged(string value) => LoadAuditLogs();
    partial void OnFilterStartDateChanged(DateTime? value) => LoadAuditLogs();
    partial void OnFilterEndDateChanged(DateTime? value) => LoadAuditLogs();

    // ─── عارض الفروقات لسجل التدقيق ───
    [RelayCommand]
    private void OpenDiffViewer(AuditLog? log)
    {
        var target = log ?? SelectedAuditLog;
        if (target == null) return;

        SelectedAuditLog = target;
        var diffList = new List<AuditDiffItem>();

        try
        {
            Dictionary<string, string> oldDict = new();
            Dictionary<string, string> newDict = new();

            if (!string.IsNullOrWhiteSpace(target.OldValues))
            {
                using var doc = JsonDocument.Parse(target.OldValues);
                foreach (var prop in doc.RootElement.EnumerateObject())
                {
                    oldDict[prop.Name] = prop.Value.ToString();
                }
            }

            if (!string.IsNullOrWhiteSpace(target.NewValues))
            {
                using var doc = JsonDocument.Parse(target.NewValues);
                foreach (var prop in doc.RootElement.EnumerateObject())
                {
                    newDict[prop.Name] = prop.Value.ToString();
                }
            }

            var allKeys = oldDict.Keys.Union(newDict.Keys).ToList();

            if (allKeys.Any())
            {
                foreach (var key in allKeys)
                {
                    oldDict.TryGetValue(key, out var oldVal);
                    newDict.TryGetValue(key, out var newVal);

                    diffList.Add(new AuditDiffItem
                    {
                        FieldName = TranslateFieldName(key),
                        OldValue = string.IsNullOrEmpty(oldVal) ? "(فارغ / لم يحدد)" : oldVal,
                        NewValue = string.IsNullOrEmpty(newVal) ? "(فارغ / لم يحدد)" : newVal,
                        HasChanged = oldVal != newVal
                    });
                }
            }
            else
            {
                diffList.Add(new AuditDiffItem
                {
                    FieldName = "التفاصيل المسجلة",
                    OldValue = "-",
                    NewValue = target.Details ?? "لا توجد تفاصيل إضافية",
                    HasChanged = false
                });
            }
        }
        catch
        {
            diffList.Add(new AuditDiffItem
            {
                FieldName = "التفاصيل",
                OldValue = target.OldValues ?? "-",
                NewValue = target.NewValues ?? (target.Details ?? "-"),
                HasChanged = true
            });
        }

        AuditDiffItems = new ObservableCollection<AuditDiffItem>(diffList);
        IsDiffViewerOpen = true;
    }

    [RelayCommand]
    private void CloseDiffViewer()
    {
        IsDiffViewerOpen = false;
        SelectedAuditLog = null;
        AuditDiffItems.Clear();
    }

    private static string TranslateFieldName(string key)
    {
        return key switch
        {
            "SourceCode" => "رقم المصدر",
            "InitialActivity" => "النشاط الابتدائي",
            "CurrentActivity" => "النشاط الحالي",
            "ActivityUnit" => "الوحدة",
            "LocationId" => "معرّف الموقع",
            "Status" => "الحالة",
            "FullName" => "الاسم الكامل",
            "Username" => "اسم المستخدم",
            "Email" => "البريد الإلكتروني",
            "RoleId" => "معرّف الدور",
            "IsActive" => "الحساب نشط",
            "IsEditor" => "صلاحية التعديل",
            "Purpose" => "الغرض",
            "ExpectedReturnDate" => "تاريخ الإرجاع المتوقع",
            _ => key
        };
    }

    // ─── إجراءات فك القفل والتجميد ───
    [RelayCommand]
    private void UnlockAccount(User? user)
    {
        var target = user ?? Selected;
        if (target == null) return;

        if (!DialogHelper.ShowConfirmation(TranslationHelper.GetString("MsgConfirmUnlock"), TranslationHelper.GetString("BtnUnlockAccount")))
            return;

        var r = _userService.UnlockAccount(target.Id);
        ShowMsg(r.Success ? TranslationHelper.GetString("MsgAccountUnlocked") : r.Message);
        if (r.Success)
        {
            LoadData();
        }
    }

    [RelayCommand]
    private void ToggleUserFreeze(User? user)
    {
        var target = user ?? Selected;
        if (target == null) return;
        if (target.Id == _userService.CurrentUser?.Id)
        {
            ShowMsg("لا يمكنك تجميد حسابك الحالي");
            return;
        }

        var r = _userService.ToggleUserFreeze(target.Id);
        ShowMsg(r.Message);
        if (r.Success) LoadData();
    }

    // ─── تصدير التقارير ───
    [RelayCommand]
    private async Task ExportUsersToPdf()
    {
        var sfd = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "PDF Files (*.pdf)|*.pdf",
            FileName = $"UsersReport_{DateTime.Now:yyyyMMdd_HHmm}"
        };
        if (sfd.ShowDialog() == true)
        {
            try
            {
                await _reportingService.GenerateUsersReportPdfAsync(FilteredUsers, sfd.FileName);
                FileHelper.OpenFile(sfd.FileName);
            }
            catch (Exception ex)
            {
                DialogHelper.ShowError(TranslationHelper.GetFormat("MsgErrExportPdf", ex.Message));
            }
        }
    }

    [RelayCommand]
    private async Task ExportUsersToExcel()
    {
        var sfd = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "Excel Files (*.xlsx)|*.xlsx",
            FileName = $"UsersReport_{DateTime.Now:yyyyMMdd_HHmm}"
        };
        if (sfd.ShowDialog() == true)
        {
            try
            {
                await _reportingService.GenerateUsersReportExcelAsync(FilteredUsers, sfd.FileName);
                FileHelper.OpenFile(sfd.FileName);
            }
            catch (Exception ex)
            {
                DialogHelper.ShowError(TranslationHelper.GetFormat("MsgErrExportExcel", ex.Message));
            }
        }
    }

    [RelayCommand]
    private async Task ExportAuditLogsToPdf()
    {
        var sfd = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "PDF Files (*.pdf)|*.pdf",
            FileName = $"AuditLogsReport_{DateTime.Now:yyyyMMdd_HHmm}"
        };
        if (sfd.ShowDialog() == true)
        {
            try
            {
                await _reportingService.GenerateAuditLogsPdfAsync(AuditLogs, sfd.FileName);
                FileHelper.OpenFile(sfd.FileName);
            }
            catch (Exception ex)
            {
                DialogHelper.ShowError(TranslationHelper.GetFormat("MsgErrExportPdf", ex.Message));
            }
        }
    }

    [RelayCommand]
    private async Task ExportAuditLogsToExcel()
    {
        var sfd = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "Excel Files (*.xlsx)|*.xlsx",
            FileName = $"AuditLogsReport_{DateTime.Now:yyyyMMdd_HHmm}"
        };
        if (sfd.ShowDialog() == true)
        {
            try
            {
                await _reportingService.GenerateAuditLogsExcelAsync(AuditLogs, sfd.FileName);
                FileHelper.OpenFile(sfd.FileName);
            }
            catch (Exception ex)
            {
                DialogHelper.ShowError(TranslationHelper.GetFormat("MsgErrExportExcel", ex.Message));
            }
        }
    }

    // ─── إضافة / تعديل / حذف ───
    [RelayCommand]
    private void AddNew()
    {
        IsNew = true; _editingId = null;
        ClearForm();
        IsEditing = true;
    }

    [RelayCommand]
    private void Edit()
    {
        if (Selected == null) return;
        IsNew = false; _editingId = Selected.Id;
        EditFullName = Selected.FullName;
        EditUsername = Selected.Username;
        EditEmail = Selected.Email ?? "";
        EditRoleId = Selected.RoleId;
        EditIsActive = Selected.IsActive;
        EditIsEditor = Selected.IsEditor;
        EditPassword = string.Empty;

        // تحميل الصلاحيات
        UnpackPermissions(Selected.Permissions);
        UpdatePermissionsVisibility();
        IsEditing = true;
    }

    [RelayCommand]
    private void Save()
    {
        if (string.IsNullOrWhiteSpace(EditFullName) || string.IsNullOrWhiteSpace(EditUsername) || EditRoleId == null)
        {
            ShowMsg(TranslationHelper.GetString("MsgErrFillRequired"));
            return;
        }

        string perms = PackPermissions();

        if (IsNew)
        {
            if (string.IsNullOrWhiteSpace(EditPassword))
            {
                ShowMsg(TranslationHelper.GetString("MsgErrPasswordReq"));
                return;
            }

            var user = new User
            {
                FullName = EditFullName,
                Username = EditUsername,
                Email = EditEmail,
                RoleId = EditRoleId.Value,
                IsActive = EditIsActive,
                IsEditor = EditIsEditor,
                Permissions = perms
            };
            var r = _userService.CreateUser(user, EditPassword);
            ShowMsg(r.Message);
            if (r.Success)
            {
                IsEditing = false;
                LoadData();
            }
        }
        else
        {
            var user = new User
            {
                Id = _editingId!.Value,
                FullName = EditFullName,
                Email = EditEmail,
                RoleId = EditRoleId.Value,
                IsActive = EditIsActive,
                IsEditor = EditIsEditor,
                Permissions = perms
            };
            var r = _userService.UpdateUser(user);
            if (!string.IsNullOrWhiteSpace(EditPassword))
                _userService.ResetPassword(_editingId!.Value, EditPassword);
            ShowMsg(r.Message);
            if (r.Success)
            {
                IsEditing = false;
                LoadData();
            }
        }
    }

    [RelayCommand]
    private void Delete()
    {
        if (Selected == null) return;
        if (!DialogHelper.ShowConfirmation("هل أنت متأكد من حذف هذا المستخدم؟", "تأكيد الحذف")) return;
        var r = _userService.DeleteUser(Selected.Id);
        ShowMsg(r.Message);
        if (r.Success) LoadData();
    }

    [RelayCommand]
    private void CancelEdit()
    {
        IsEditing = false;
        ClearForm();
    }

    // ─── مساعدات ───
    private void ClearForm()
    {
        EditFullName = EditUsername = EditPassword = EditEmail = string.Empty;
        EditRoleId = null; EditIsActive = true; EditIsEditor = true;
        PermRadioisotopes = PermSources = PermLocations = PermBorrowing = PermReports = PermCalculator = true;
        PermUsers = PermSettings = false;
        UpdatePermissionsVisibility();
    }

    private string PackPermissions()
    {
        var selectedRole = Roles.FirstOrDefault(r => r.Id == EditRoleId);
        if (selectedRole?.RoleName == "مدير النظام") return "All";

        var perms = new List<string>();
        if (PermRadioisotopes) perms.Add("Radioisotopes");
        if (PermSources) perms.Add("Sources");
        if (PermLocations) perms.Add("Locations");
        if (PermBorrowing) perms.Add("Borrowing");
        if (PermReports) perms.Add("Reports");
        if (PermUsers) perms.Add("Users");
        if (PermSettings) perms.Add("Settings");
        if (PermCalculator) perms.Add("ActivityCalculator");
        return string.Join(",", perms);
    }

    private void UnpackPermissions(string? perms)
    {
        if (string.IsNullOrEmpty(perms) || perms == "All")
        {
            PermRadioisotopes = PermSources = PermLocations = PermBorrowing = PermReports = PermCalculator = true;
            PermUsers = PermSettings = perms == "All";
            return;
        }
        var set = perms.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(p => p.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        PermRadioisotopes = set.Contains("Radioisotopes");
        PermSources = set.Contains("Sources");
        PermLocations = set.Contains("Locations");
        PermBorrowing = set.Contains("Borrowing");
        PermReports = set.Contains("Reports");
        PermUsers = set.Contains("Users");
        PermSettings = set.Contains("Settings");
        PermCalculator = set.Contains("ActivityCalculator");
    }

    partial void OnEditRoleIdChanged(Guid? value) => UpdatePermissionsVisibility();

    private void UpdatePermissionsVisibility()
    {
        var selectedRole = Roles.FirstOrDefault(r => r.Id == EditRoleId);
        IsPermissionsSectionVisible = selectedRole?.RoleName != "مدير النظام";
    }

    private void ShowMsg(string m) { Message = m; HasMessage = true; }
}
