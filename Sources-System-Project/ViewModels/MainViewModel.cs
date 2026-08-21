using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Sources.Helpers;
using Sources.Services;
using Sources.Interfaces;
using Sources.Models;
using System;
using System.Linq;
using System.Windows;

namespace Sources.ViewModels;

public partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly IUserService _userService;
    private readonly IAlertService _alertService;
    private readonly ISystemSettingsService _settingsService;

    [ObservableProperty] private ObservableObject? _currentView;
    [ObservableProperty] private string _currentViewName = "Dashboard";
    [ObservableProperty] private bool _isLoggedIn;
    [ObservableProperty] private string _currentUserName = string.Empty;
    [ObservableProperty] private string _currentUserRole = string.Empty;
    [ObservableProperty] private bool _isDarkMode;
    [ObservableProperty] private bool _isSidebarCollapsed;
    
    // ─── التنبيهات ───
    [ObservableProperty] private System.Collections.ObjectModel.ObservableCollection<Sources.Models.AlertNotification> _notifications = new();
    [ObservableProperty] private int _unreadNotificationsCount;
    private System.Windows.Threading.DispatcherTimer? _alertTimer;

    public MainViewModel(IUserService userService, IAlertService alertService, ISystemSettingsService settingsService)
    {
        _userService = userService;
        _alertService = alertService;
        _settingsService = settingsService;
        IsDarkMode = SettingsHelper.IsDarkMode;
        
        // التسجيل لاستقبال رسائل تحديث المصادر وتحديث التنبيهات فورياً
        WeakReferenceMessenger.Default.Register<Sources.Messages.SourcesUpdatedMessage>(this, (r, m) =>
        {
            RunOnUI(RefreshNotifications);
        });

        InitializeUserSession();
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

    public void InitializeUserSession()
    {
        if (_userService.IsLoggedIn)
        {
            OnLoginSuccess();
        }
    }

    private void OnLoginSuccess()
    {
        IsLoggedIn = true;
        CurrentUserName = _userService.CurrentUser?.FullName ?? "";
        CurrentUserRole = _userService.CurrentUser?.Role?.RoleName ?? "";

        // تطبيق تفضيلات المظهر الخاصة بالمستخدم
        var username = _userService.CurrentUser?.Username;
        IsDarkMode = SettingsHelper.GetUserTheme(username);
        var accentColor = SettingsHelper.GetUserAccentColor(username);
        App.ApplyTheme(IsDarkMode);
        App.ApplyAccentColor(accentColor);

        RefreshNotifications();
        StartAlertCheckTimer();
        RefreshSidebarPermissions();
        NavigateTo("Dashboard");
    }

    // ─── صلاحيات القائمة الجانبية ───
    [ObservableProperty] private bool _canSeeRadioisotopes = true;
    [ObservableProperty] private bool _canSeeSources = true;
    [ObservableProperty] private bool _canSeeLocations = true;
    [ObservableProperty] private bool _canSeeBorrowing = true;
    [ObservableProperty] private bool _canSeeReports = true;
    [ObservableProperty] private bool _canSeeUsers = true;
    [ObservableProperty] private bool _canSeeSettings = true;
    [ObservableProperty] private bool _canSeeCalculator = true;
    [ObservableProperty] private bool _canSeeAlerts = true;
    [ObservableProperty] private bool _canSeeHelp = true;

    private void RefreshSidebarPermissions()
    {
        var user = _userService.CurrentUser;
        if (user == null) return;
        CanSeeRadioisotopes = user.HasSectionPermission("Radioisotopes");
        CanSeeSources = user.HasSectionPermission("Sources");
        CanSeeLocations = user.HasSectionPermission("Locations");
        CanSeeBorrowing = user.HasSectionPermission("Borrowing");
        CanSeeReports = user.HasSectionPermission("Reports");
        CanSeeUsers = user.HasSectionPermission("Users");
        CanSeeSettings = user.HasSectionPermission("Settings");
        CanSeeCalculator = user.HasSectionPermission("ActivityCalculator");
        CanSeeAlerts = user.HasSectionPermission("Alerts");
    }

    [RelayCommand]
    public void RefreshNotifications()
    {
        if (!IsLoggedIn) return;
        _ = Task.Run(() =>
        {
            try
            {
                var alerts = _alertService.GenerateAlerts();
                var unread = _alertService.GetUnreadCount();

                void updateUi()
                {
                    Notifications = new System.Collections.ObjectModel.ObservableCollection<Sources.Models.AlertNotification>(alerts);
                    UnreadNotificationsCount = unread;
                }

                if (Application.Current?.Dispatcher != null && !Application.Current.Dispatcher.CheckAccess())
                {
                    Application.Current.Dispatcher.Invoke(updateUi);
                }
                else
                {
                    updateUi();
                }
            }
            catch (Exception ex)
            {
                LoggerService.LogError("MainViewModel: RefreshNotifications background task failed", ex);
            }
        });
    }

    [RelayCommand]
    public void MarkNotificationAsRead(Sources.Models.AlertNotification notification)
    {
        if (notification == null) return;
        // Optimistic UI: تحديث الحالة فوراً على الواجهة دون انتظار عملية قاعدة البيانات
        notification.IsRead = true;
        if (UnreadNotificationsCount > 0)
        {
            UnreadNotificationsCount--;
        }
        _ = Task.Run(() =>
        {
            try
            {
                _alertService.MarkAsRead(notification.Id);
                var unread = _alertService.GetUnreadCount();
                if (Application.Current?.Dispatcher != null && !Application.Current.Dispatcher.CheckAccess())
                {
                    Application.Current.Dispatcher.Invoke(() => UnreadNotificationsCount = unread);
                }
                else
                {
                    UnreadNotificationsCount = unread;
                }
            }
            catch (Exception ex)
            {
                LoggerService.LogError("MainViewModel: MarkNotificationAsRead failed", ex);
            }
        });
    }

    [RelayCommand]
    public void DismissNotification(Sources.Models.AlertNotification notification)
    {
        if (notification == null) return;
        if (DialogHelper.ShowConfirmation(TranslationHelper.GetString("MsgConfirmDismissAlert"), TranslationHelper.GetString("TitleDismissAlert")))
        {
            // Optimistic UI: إزالة التنبيه فوراً من القائمة المعروضة
            if (!notification.IsRead && UnreadNotificationsCount > 0)
            {
                UnreadNotificationsCount--;
            }
            Notifications.Remove(notification);

            _ = Task.Run(() =>
            {
                try
                {
                    _alertService.DismissAlert(notification.Id);
                    var alerts = _alertService.GetActiveAlerts();
                    var unread = _alertService.GetUnreadCount();
                    void updateUi()
                    {
                        Notifications = new System.Collections.ObjectModel.ObservableCollection<Sources.Models.AlertNotification>(alerts);
                        UnreadNotificationsCount = unread;
                    }
                    if (Application.Current?.Dispatcher != null && !Application.Current.Dispatcher.CheckAccess())
                    {
                        Application.Current.Dispatcher.Invoke(updateUi);
                    }
                    else
                    {
                        updateUi();
                    }
                }
                catch (Exception ex)
                {
                    LoggerService.LogError("MainViewModel: DismissNotification failed", ex);
                }
            });
        }
    }

    [RelayCommand]
    public void MarkAllNotificationsAsRead()
    {
        // Optimistic UI: تعليم جميع التنبيهات كمقروءة فوراً
        foreach (var n in Notifications)
        {
            n.IsRead = true;
        }
        UnreadNotificationsCount = 0;

        _ = Task.Run(() =>
        {
            try
            {
                _alertService.MarkAllAsRead();
                var unread = _alertService.GetUnreadCount();
                if (Application.Current?.Dispatcher != null && !Application.Current.Dispatcher.CheckAccess())
                {
                    Application.Current.Dispatcher.Invoke(() => UnreadNotificationsCount = unread);
                }
                else
                {
                    UnreadNotificationsCount = unread;
                }
            }
            catch (Exception ex)
            {
                LoggerService.LogError("MainViewModel: MarkAllNotificationsAsRead failed", ex);
            }
        });
    }

    [RelayCommand]
    public void NavigateTo(string viewName)
    {
        // التحقق من حالة التحرير في المنظور الحالي
        if (CurrentView is IEditableViewModel editable && editable.IsEditing)
        {
            DialogHelper.ShowWarning(TranslationHelper.GetString("MsgErrSavePending"), TranslationHelper.GetString("TitlePendingChanges"));
            return;
        }

        CurrentViewName = viewName;
        CurrentView = viewName switch
        {
            "Dashboard" => App.ServiceProvider?.GetService(typeof(DashboardViewModel)) as ObservableObject,
            "Radioisotopes" => App.ServiceProvider?.GetService(typeof(RadioisotopesViewModel)) as ObservableObject,
            "Sources" => App.ServiceProvider?.GetService(typeof(SourcesViewModel)) as ObservableObject,
            "Locations" => App.ServiceProvider?.GetService(typeof(LocationsViewModel)) as ObservableObject,
            "Borrowing" => App.ServiceProvider?.GetService(typeof(BorrowViewModel)) as ObservableObject,
            "Reports" => App.ServiceProvider?.GetService(typeof(ReportsViewModel)) as ObservableObject,
            "Alerts" => App.ServiceProvider?.GetService(typeof(AlertsViewModel)) as ObservableObject,
            "Users" => App.ServiceProvider?.GetService(typeof(UsersViewModel)) as ObservableObject,
            "Settings" => App.ServiceProvider?.GetService(typeof(SettingsViewModel)) as ObservableObject,
            "ActivityCalculator" => App.ServiceProvider?.GetService(typeof(ActivityCalculatorViewModel)) as ObservableObject,
            "Help" => App.ServiceProvider?.GetService(typeof(HelpViewModel)) as ObservableObject,
            "AboutSystem" => App.ServiceProvider?.GetService(typeof(AboutSystemViewModel)) as ObservableObject,
            _ => CurrentView
        };
    }

    [RelayCommand]
    private void ToggleSidebar()
    {
        IsSidebarCollapsed = !IsSidebarCollapsed;
    }

    [RelayCommand]
    private void ToggleTheme()
    {
        IsDarkMode = !IsDarkMode;
        SettingsHelper.IsDarkMode = IsDarkMode;
        App.ApplyTheme(IsDarkMode);
    }

    [RelayCommand]
    private void Logout()
    {
        if (CurrentView is IEditableViewModel editable && editable.IsEditing)
        {
            DialogHelper.ShowWarning(
                TranslationHelper.GetString("MsgErrSavePending"),
                TranslationHelper.GetString("TitlePendingChanges")
            );
            return;
        }

        if (DialogHelper.ShowConfirmation(TranslationHelper.GetString("MsgConfirmLogout"), TranslationHelper.GetString("TitleLogout")))
        {
            ForceLogout();
        }
    }

    public void ForceLogout()
    {
        StopInactivityTimer();
        StopAlertCheckTimer();
        _userService.Logout();
        CurrentUserName = string.Empty;
        CurrentUserRole = string.Empty;
        IsLoggedIn = false;

        RunOnUI(() =>
        {
            var loginWindow = App.ServiceProvider?.GetService(typeof(Views.LoginWindow)) as Views.LoginWindow;
            loginWindow?.Show();
            loginWindow?.Activate();

            if (Application.Current != null)
            {
                Application.Current.MainWindow = loginWindow;
            }

            var mainWin = Application.Current?.Windows.OfType<MainWindow>().FirstOrDefault();
            if (mainWin != null)
            {
                mainWin.Close();
            }
        });
    }

    public void StartAlertCheckTimer()
    {
        var intervalMinutes = _settingsService.GetSetting<int>("NotificationCheckIntervalMinutes", 60);
        if (intervalMinutes < 1) intervalMinutes = 1;

        if (_alertTimer == null)
        {
            _alertTimer = new System.Windows.Threading.DispatcherTimer();
            _alertTimer.Tick += (s, e) => RefreshNotifications();
        }
        _alertTimer.Interval = TimeSpan.FromMinutes(intervalMinutes);
        _alertTimer.Start();
    }

    public void UpdateAlertCheckInterval(int intervalMinutes)
    {
        if (intervalMinutes < 1) intervalMinutes = 1;
        if (_alertTimer != null)
        {
            _alertTimer.Interval = TimeSpan.FromMinutes(intervalMinutes);
        }
    }

    public void StopAlertCheckTimer()
    {
        _alertTimer?.Stop();
    }

    // ─── Inactivity Timer (شاشة التوقف بعد 15 دقيقة خمول) ───
    private System.Windows.Threading.DispatcherTimer? _inactivityTimer;
    private DateTime _lastActivityTime;
    private readonly int _autoLockMinutes = 15; // 15 دقيقة خمول
    
    public event EventHandler? LockRequested;

    public void StartInactivityTimer()
    {
        _lastActivityTime = DateTime.Now;
        if (_inactivityTimer == null)
        {
            _inactivityTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(5) // فحص دوري كل 5 ثوانٍ
            };
            _inactivityTimer.Tick += InactivityTimer_Tick;
        }
        _inactivityTimer.Start();
    }

    private void InactivityTimer_Tick(object? sender, EventArgs e)
    {
        if (!IsLoggedIn) return;
        var inactiveTime = DateTime.Now - _lastActivityTime;
        if (inactiveTime.TotalMinutes >= _autoLockMinutes)
        {
            _inactivityTimer?.Stop();
            LockRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    public void ResetInactivity()
    {
        _lastActivityTime = DateTime.Now;
    }

    public void StopInactivityTimer()
    {
        _inactivityTimer?.Stop();
    }

    public void Dispose()
    {
        _alertTimer?.Stop();
        _inactivityTimer?.Stop();
        WeakReferenceMessenger.Default.UnregisterAll(this);
    }
}
