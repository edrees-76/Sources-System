using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sources.Models;
using Sources.Services;
using Sources.Interfaces;
using Sources.Helpers;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;

namespace Sources.ViewModels;

public partial class UsersViewModel : ObservableObject, IEditableViewModel
{
    private readonly IUserService _userService;

    // ─── القوائم ───
    [ObservableProperty] private ObservableCollection<User> _users = new();
    [ObservableProperty] private ObservableCollection<Role> _roles = new();
    [ObservableProperty] private ObservableCollection<AuditLog> _auditLogs = new();

    // ─── التحديد والتحرير ───
    [ObservableProperty] private User? _selected;
    [ObservableProperty] private bool _isEditing;
    [ObservableProperty] private bool _isNew;
    [ObservableProperty] private string _message = string.Empty;
    [ObservableProperty] private bool _hasMessage;

    // ─── بطاقات إحصائية ───
    [ObservableProperty] private int _totalUsersCount;
    [ObservableProperty] private int _activeUsersCount;
    [ObservableProperty] private int _adminUsersCount;

    // ─── إحصائيات سجل النشاط ───
    [ObservableProperty] private int _activitiesTodayCount;
    [ObservableProperty] private int _modificationsTodayCount;
    [ObservableProperty] private string _mostActiveUser = "—";

    // ─── فلاتر سجل التدقيق ───
    [ObservableProperty] private User? _selectedUserFilter;
    [ObservableProperty] private DateTime? _filterStartDate;
    [ObservableProperty] private DateTime? _filterEndDate;

    // ─── حقول النموذج ───
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

    public UsersViewModel(IUserService userService)
    {
        _userService = userService;
        LoadData();
    }

    [RelayCommand]
    public void LoadData()
    {
        Users = new ObservableCollection<User>(_userService.GetAllUsers());
        Roles = new ObservableCollection<Role>(_userService.GetAllRoles());
        UpdateStats();
        LoadAuditLogs();
    }

    private void UpdateStats()
    {
        TotalUsersCount = Users.Count;
        ActiveUsersCount = Users.Count(u => u.IsActive);
        AdminUsersCount = Users.Count(u => u.Role?.RoleName == "مدير النظام");
    }

    [RelayCommand]
    private void LoadAuditLogs()
    {
        var logs = _userService.GetAuditLogs(SelectedUserFilter?.Id, FilterStartDate, FilterEndDate);
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
        FilterStartDate = null;
        FilterEndDate = null;
        LoadAuditLogs();
    }

    partial void OnSelectedUserFilterChanged(User? value) => LoadAuditLogs();
    partial void OnFilterStartDateChanged(DateTime? value) => LoadAuditLogs();
    partial void OnFilterEndDateChanged(DateTime? value) => LoadAuditLogs();

    // ─── إضافة / تعديل ───
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
        { ShowMsg(TranslationHelper.GetString("MsgErrFillRequired")); return; }

        string perms = PackPermissions();

        if (IsNew)
        {
            if (string.IsNullOrWhiteSpace(EditPassword))
            { ShowMsg(TranslationHelper.GetString("MsgErrPasswordReq")); return; }

            var user = new User
            {
                FullName = EditFullName, Username = EditUsername,
                Email = EditEmail, RoleId = EditRoleId.Value,
                IsActive = EditIsActive, IsEditor = EditIsEditor,
                Permissions = perms
            };
            var r = _userService.CreateUser(user, EditPassword);
            ShowMsg(r.Message);
            if (r.Success) { IsEditing = false; LoadData(); }
        }
        else
        {
            var user = new User
            {
                Id = _editingId!.Value, FullName = EditFullName,
                Email = EditEmail, RoleId = EditRoleId.Value,
                IsActive = EditIsActive, IsEditor = EditIsEditor,
                Permissions = perms
            };
            var r = _userService.UpdateUser(user);
            if (!string.IsNullOrWhiteSpace(EditPassword))
                _userService.ResetPassword(_editingId!.Value, EditPassword);
            ShowMsg(r.Message);
            if (r.Success) { IsEditing = false; LoadData(); }
        }
    }

    [RelayCommand]
    private void ToggleUserFreeze(User? user)
    {
        var target = user ?? Selected;
        if (target == null) return;
        if (target.Id == _userService.CurrentUser?.Id)
        { ShowMsg("لا يمكنك تجميد حسابك الحالي"); return; }

        var r = _userService.ToggleUserFreeze(target.Id);
        ShowMsg(r.Message);
        if (r.Success) LoadData();
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

    [RelayCommand] private void CancelEdit() { IsEditing = false; ClearForm(); }

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
        // المدير لديه كل الصلاحيات تلقائياً
        var selectedRole = Roles.FirstOrDefault(r => r.Id == EditRoleId);
        if (selectedRole?.RoleName == "مدير النظام") return "All";

        var perms = new System.Collections.Generic.List<string>();
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
