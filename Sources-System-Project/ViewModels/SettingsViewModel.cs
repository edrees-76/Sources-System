using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using Sources.Data;
using Sources.Helpers;
using Sources.Models;
using Sources.Services;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace Sources.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly IBackupService _backupService;
    private readonly ISystemSettingsService _settingsService;
    private readonly IAutoBackupService? _autoBackupService;
    private readonly IUserService? _userService;
    private readonly ISystemResetService? _resetService;
    private readonly IDbContextFactory<AppDbContext>? _dbFactory;
    private readonly IDecayCalculationService? _decayService;
    private readonly IAlertService? _alertService;

    // ─── التحكم في التبويبات ───
    [ObservableProperty] private string _selectedTab = "General";

    // ─── تبويب العام ───
    [ObservableProperty] private string _language;

    // ─── تبويب المظهر ───
    [ObservableProperty] private bool _isDarkMode;
    [ObservableProperty] private string _selectedAccentColor = SettingsHelper.DefaultAccentColor;
    public System.Collections.ObjectModel.ObservableCollection<AccentColorOption> AvailableAccentColors { get; } = new()
    {
        new AccentColorOption
        {
            NameKey = "AccentColorPetroleumBlue",
            DescriptionKey = "AccentColorPetroleumBlueDesc",
            HexColor = "#1F5A66",
            IsDefault = true
        },
        new AccentColorOption
        {
            NameKey = "AccentColorRoyalNavy",
            DescriptionKey = "AccentColorRoyalNavyDesc",
            HexColor = "#1E3F66",
            IsDefault = false
        },
        new AccentColorOption
        {
            NameKey = "AccentColorForestGreen",
            DescriptionKey = "AccentColorForestGreenDesc",
            HexColor = "#3D5A47",
            IsDefault = false
        },
        new AccentColorOption
        {
            NameKey = "AccentColorSlate",
            DescriptionKey = "AccentColorSlateDesc",
            HexColor = "#433E52",
            IsDefault = false
        }
    };

    // ─── تبويب النسخ الاحتياطي ───
    [ObservableProperty] private string _backupPath = string.Empty;
    [ObservableProperty] private bool _autoBackupEnabled;
    [ObservableProperty] private string _selectedFrequency = "Daily";
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _busyMessage = string.Empty;
    [ObservableProperty] private string _lastBackupInfo = string.Empty;

    // ─── تبويب إعدادات النظام ───
    [ObservableProperty] private double _lowActivityThresholdPercent = 10.0;
    [ObservableProperty] private int _notificationCheckIntervalMinutes = 60;
    [ObservableProperty] private int _dueSoonDaysThreshold = 7;
    [ObservableProperty] private int _leakTestIntervalMonths = 6;
    [ObservableProperty] private int _leakTestWarningDaysThreshold = 30;
    [ObservableProperty] private string _facilityName = string.Empty;
    [ObservableProperty] private string _facilityAddress = string.Empty;
    [ObservableProperty] private string _technicalDirector = string.Empty;

    // ─── تبويب الوضع الافتراضي (Factory Reset) ───
    public bool IsAdmin => _userService?.CurrentUser?.IsAdmin == true;
    public string RequiredResetPhrase => "إعادة ضبط المنظومة";

#if DEBUG
    public bool IsDebugMode => true;
    public bool CanShowTestData => IsAdmin;
#else
    public bool IsDebugMode => false;
    public bool CanShowTestData => false;
#endif

    [ObservableProperty] private string _resetPhrase = string.Empty;
    [ObservableProperty] private bool _isStage1Passed;
    [ObservableProperty] private string _resetPassword = string.Empty;
    [ObservableProperty] private bool _isStage2Passed;
    [ObservableProperty] private bool _isFinalConfirmed;

    public SettingsViewModel(
        IBackupService backupService, 
        ISystemSettingsService settingsService,
        IAutoBackupService? autoBackupService = null,
        IUserService? userService = null,
        ISystemResetService? resetService = null,
        IDbContextFactory<AppDbContext>? dbFactory = null,
        IDecayCalculationService? decayService = null,
        IAlertService? alertService = null)
    {
        _backupService = backupService;
        _settingsService = settingsService;
        _autoBackupService = autoBackupService;
        _userService = userService;
        _resetService = resetService;
        _dbFactory = dbFactory;
        _decayService = decayService;
        _alertService = alertService;

        Language = SettingsHelper.Language;

        // تحميل إعدادات المظهر الخاصة بالمستخدم
        var currentUsername = _userService?.CurrentUser?.Username;
        IsDarkMode = SettingsHelper.GetUserTheme(currentUsername);
        SelectedAccentColor = SettingsHelper.GetUserAccentColor(currentUsername);

        // تحميل إعدادات النسخ الاحتياطي المحفوظة
        BackupPath = _settingsService.GetSetting("BackupPath", string.Empty);
        AutoBackupEnabled = _settingsService.GetSetting<bool>("AutoBackupEnabled", false);
        
        var freq = _settingsService.GetSetting("AutoBackupFrequency", "Daily");
        if (freq == "يومي") freq = "Daily";
        if (freq == "أسبوعي") freq = "Weekly";
        if (freq == "شهري") freq = "Monthly";
        SelectedFrequency = freq;

        // تحميل إعدادات النظام العامة
        LowActivityThresholdPercent = _settingsService.GetSetting("LowActivityThresholdPercent", 10.0);
        NotificationCheckIntervalMinutes = _settingsService.GetSetting("NotificationCheckIntervalMinutes", 60);
        DueSoonDaysThreshold = _settingsService.GetSetting("DueSoonDaysThreshold", 7);
        LeakTestIntervalMonths = _settingsService.GetSetting(SystemSettingsDefaults.LeakTestIntervalMonthsKey, 6);
        if (LeakTestIntervalMonths <= 0) LeakTestIntervalMonths = 6;
        LeakTestWarningDaysThreshold = _settingsService.GetSetting(SystemSettingsDefaults.LeakTestWarningDaysThresholdKey, 30);
        if (LeakTestWarningDaysThreshold <= 0) LeakTestWarningDaysThreshold = 30;
        FacilityName = _settingsService.GetSetting("FacilityName", string.Empty);
        FacilityAddress = _settingsService.GetSetting("FacilityAddress", string.Empty);
        TechnicalDirector = _settingsService.GetSetting("TechnicalDirector", string.Empty);


        UpdateLastBackupInfo();

        // الاشتراك في حدث اكتمال النسخ الاحتياطي التلقائي لتحديث الواجهة فوراً
        if (_autoBackupService != null)
        {
            _autoBackupService.BackupCompleted += (s, e) =>
            {
                Application.Current?.Dispatcher.InvokeAsync(UpdateLastBackupInfo);
            };
        }
    }

    // ─── أوامر التبديل بين التبويبات ───
    [RelayCommand]
    private void SelectTab(string tabName)
    {
        if (!string.IsNullOrWhiteSpace(tabName))
        {
            SelectedTab = tabName;
            if (tabName != "FactoryReset")
            {
                ResetFactoryResetStages();
            }
        }
    }

    public void UpdateLastBackupInfo()
    {
        try
        {
            var targetFolders = new System.Collections.Generic.List<string>();

            if (!string.IsNullOrWhiteSpace(BackupPath) && Directory.Exists(BackupPath))
            {
                targetFolders.Add(Path.Combine(BackupPath, BackupService.BackupFolderName));
                targetFolders.Add(Path.Combine(BackupPath, BackupService.LegacyBackupFolderName));
                targetFolders.Add(BackupPath);
            }

            var defaultAppDataDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Sources", "Backups");
            if (Directory.Exists(defaultAppDataDir))
            {
                targetFolders.Add(defaultAppDataDir);
            }

            FileInfo? latestFile = null;

            foreach (var folder in targetFolders.Distinct())
            {
                if (!Directory.Exists(folder)) continue;

                var files = Directory.GetFiles(folder, "*_backup_*.db")
                    .Select(f => new FileInfo(f))
                    .OrderByDescending(f => f.CreationTime)
                    .ToList();

                if (files.Count > 0)
                {
                    var file = files[0];
                    if (latestFile == null || file.CreationTime > latestFile.CreationTime)
                    {
                        latestFile = file;
                    }
                }
            }

            if (latestFile != null)
            {
                var sizeDisplay = latestFile.Length < 1024 * 1024
                    ? $"{latestFile.Length / 1024.0:F1} KB"
                    : $"{latestFile.Length / (1024.0 * 1024.0):F1} MB";
                LastBackupInfo = $"{latestFile.CreationTime:yyyy/MM/dd HH:mm} ({sizeDisplay})";
            }
            else
            {
                LastBackupInfo = TranslationHelper.GetString("NoBackupsYet");
            }
        }
        catch
        {
            LastBackupInfo = TranslationHelper.GetString("NoBackupsYet");
        }
    }

    // ─── أوامر العام واللغة ───
    [RelayCommand]
    private void SetArabic()
    {
        Language = "ar";
        SettingsHelper.Language = "ar";
        App.ApplyLanguage("ar");
        
        DialogHelper.ShowInfo(
            TranslationHelper.GetString("MsgRestartRequiredForLang"), 
            TranslationHelper.GetString("TitleLanguageChange"));
    }

    [RelayCommand]
    private void SetEnglish()
    {
        Language = "en";
        SettingsHelper.Language = "en";
        App.ApplyLanguage("en");
        
        DialogHelper.ShowInfo(
            TranslationHelper.GetString("MsgRestartRequiredForLang"), 
            TranslationHelper.GetString("TitleLanguageChange"));
    }

    // ─── أوامر النسخ الاحتياطي ───
    [RelayCommand]
    private void BrowseBackupPath()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = TranslationHelper.GetString("BrowseBackupTitle"),
            InitialDirectory = Directory.Exists(BackupPath) ? BackupPath : string.Empty
        };

        if (dialog.ShowDialog() == true)
        {
            BackupPath = dialog.FolderName;
        }
    }

    [RelayCommand]
    private void SaveBackupSettings()
    {
        _settingsService.SaveSetting("BackupPath", BackupPath);
        _settingsService.SaveSetting("AutoBackupEnabled", AutoBackupEnabled.ToString());
        _settingsService.SaveSetting("AutoBackupFrequency", SelectedFrequency);

        // إشعار الخدمة الخلفية بتحديث الجدولة فوراً
        _autoBackupService?.TriggerImmediateCheck();

        DialogHelper.ShowInfo(
            TranslationHelper.GetString("MsgSettingsSaved"),
            TranslationHelper.GetString("TabBackup"));
    }

    [RelayCommand]
    private void CreateBackup()
    {
        if (string.IsNullOrWhiteSpace(BackupPath))
        {
            DialogHelper.ShowWarning(
                TranslationHelper.GetString("MsgSelectBackupPath"),
                TranslationHelper.GetString("BackupTitle"));
            return;
        }

        IsBusy = true;
        BusyMessage = TranslationHelper.GetString("MsgCreatingBackup");

        try
        {
            var result = _backupService.CreateBackup(BackupPath);

            if (result.Success)
            {
                // حفظ المسار في الإعدادات تلقائياً بعد نجاح النسخ
                _settingsService.SaveSetting("BackupPath", BackupPath);
                UpdateLastBackupInfo();
                DialogHelper.ShowInfo(result.Message, TranslationHelper.GetString("BackupTitle"));
            }
            else
            {
                DialogHelper.ShowError(result.Message, TranslationHelper.GetString("BackupTitle"));
            }
        }
        catch (Exception ex)
        {
            LoggerService.LogError("Manual backup failed", ex);
            DialogHelper.ShowError(
                TranslationHelper.GetString("MsgBackupError"),
                TranslationHelper.GetString("BackupTitle"));
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void RestoreBackup()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = TranslationHelper.GetString("RestoreBackupTitle"),
            Filter = "Database files (*.db)|*.db",
            InitialDirectory = Directory.Exists(BackupPath) ? BackupPath : string.Empty
        };

        if (dialog.ShowDialog() != true) return;

        var confirmed = DialogHelper.ShowConfirmation(
            TranslationHelper.GetString("MsgRestoreWarning"),
            TranslationHelper.GetString("RestoreBackupTitle"));

        if (!confirmed) return;

        IsBusy = true;
        BusyMessage = TranslationHelper.GetString("MsgRestoringBackup");

        try
        {
            var result = _backupService.RestoreBackup(dialog.FileName);

            if (result.Success)
            {
                DialogHelper.ShowInfo(result.Message, TranslationHelper.GetString("RestoreBackupTitle"));
            }
            else
            {
                DialogHelper.ShowError(result.Message, TranslationHelper.GetString("RestoreBackupTitle"));
            }
        }
        catch (Exception ex)
        {
            LoggerService.LogError("Restore backup failed", ex);
            DialogHelper.ShowError(
                TranslationHelper.GetString("MsgRestoreError"),
                TranslationHelper.GetString("RestoreBackupTitle"));
        }
        finally
        {
            IsBusy = false;
        }
    }

    // ─── أوامر إعدادات النظام ───
    [RelayCommand]
    private void SaveSystemSettings()
    {
        if (LowActivityThresholdPercent <= 0 || LowActivityThresholdPercent > 100)
        {
            DialogHelper.ShowWarning("يجب أن تكون نسبة عتبة النشاط بين 0.1% و 100%", TranslationHelper.GetString("TabSystemSettings"));
            return;
        }

        if (NotificationCheckIntervalMinutes < 1 || NotificationCheckIntervalMinutes > 1440)
        {
            DialogHelper.ShowWarning("يجب أن تكون فترة فحص التنبيهات بين 1 دقيقة و 1440 دقيقة (24 ساعة)", TranslationHelper.GetString("TabSystemSettings"));
            return;
        }

        if (DueSoonDaysThreshold < 1 || DueSoonDaysThreshold > 365)
        {
            DialogHelper.ShowWarning(TranslationHelper.GetString("MsgErrDueSoonThresholdRange"), TranslationHelper.GetString("TabSystemSettings"));
            return;
        }

        if (LeakTestIntervalMonths < 1 || LeakTestIntervalMonths > 120)
        {
            DialogHelper.ShowWarning("يجب أن تكون دورية فحص التسرب بين 1 شهر و 120 شهراً", TranslationHelper.GetString("TabSystemSettings"));
            return;
        }

        if (LeakTestWarningDaysThreshold < 1 || LeakTestWarningDaysThreshold > 365)
        {
            DialogHelper.ShowWarning("يجب أن تكون مهلة التنبيه بفحص التسرب بين 1 يوم و 365 يوماً", TranslationHelper.GetString("TabSystemSettings"));
            return;
        }

        _settingsService.SaveSetting("LowActivityThresholdPercent", LowActivityThresholdPercent.ToString());
        _settingsService.SaveSetting("NotificationCheckIntervalMinutes", NotificationCheckIntervalMinutes.ToString());
        _settingsService.SaveSetting("DueSoonDaysThreshold", DueSoonDaysThreshold.ToString());
        _settingsService.SaveSetting(SystemSettingsDefaults.LeakTestIntervalMonthsKey, LeakTestIntervalMonths.ToString());
        _settingsService.SaveSetting(SystemSettingsDefaults.LeakTestWarningDaysThresholdKey, LeakTestWarningDaysThreshold.ToString());
        _settingsService.SaveSetting("FacilityName", FacilityName ?? string.Empty);
        _settingsService.SaveSetting("FacilityAddress", FacilityAddress ?? string.Empty);
        _settingsService.SaveSetting("TechnicalDirector", TechnicalDirector ?? string.Empty);

        // تحديث دورية فحص التنبيهات في MainViewModel إن كان نشطاً
        if (App.ServiceProvider?.GetService(typeof(MainViewModel)) is MainViewModel mainVm)
        {
            mainVm.UpdateAlertCheckInterval(NotificationCheckIntervalMinutes);
        }

        DialogHelper.ShowInfo(
            TranslationHelper.GetString("MsgSystemSettingsSaved"),
            TranslationHelper.GetString("TabSystemSettings"));
    }

    // ─── أوامر الوضع الافتراضي (Factory Reset) ───

    public void ResetFactoryResetStages()
    {
        ResetPhrase = string.Empty;
        IsStage1Passed = false;
        ResetPassword = string.Empty;
        IsStage2Passed = false;
        IsFinalConfirmed = false;
    }

    [RelayCommand]
    private void VerifyResetPhrase()
    {
        if (string.IsNullOrWhiteSpace(ResetPhrase) || ResetPhrase.Trim() != RequiredResetPhrase)
        {
            IsStage1Passed = false;
            DialogHelper.ShowWarning(
                TranslationHelper.GetString("MsgErrIncorrectPhrase"),
                TranslationHelper.GetString("TitleFactoryReset"));
            return;
        }

        IsStage1Passed = true;
    }

    [RelayCommand]
    private void VerifyResetPassword()
    {
        if (!IsStage1Passed)
        {
            DialogHelper.ShowWarning(
                TranslationHelper.GetString("MsgErrIncorrectPhrase"),
                TranslationHelper.GetString("TitleFactoryReset"));
            return;
        }

        var currentUser = _userService?.CurrentUser;
        if (currentUser == null)
        {
            IsStage2Passed = false;
            DialogHelper.ShowError("لا يوجد مستخدم مسجل حالياً", TranslationHelper.GetString("TitleFactoryReset"));
            return;
        }

        if (string.IsNullOrEmpty(ResetPassword) || !PasswordHelper.VerifyPassword(ResetPassword, currentUser.PasswordHash))
        {
            IsStage2Passed = false;
            DialogHelper.ShowWarning(
                TranslationHelper.GetString("MsgErrIncorrectAdminPassword"),
                TranslationHelper.GetString("TitleFactoryReset"));
            return;
        }

        IsStage2Passed = true;
    }

    [RelayCommand]
    private async Task ExecuteFactoryResetAsync()
    {
        if (!IsAdmin)
        {
            DialogHelper.ShowError("غير مصرح: هذه العملية مخصصة لمدير النظام فقط", TranslationHelper.GetString("TitleFactoryReset"));
            return;
        }

        if (!IsStage1Passed || !IsStage2Passed)
        {
            DialogHelper.ShowWarning("يرجى إكمال المرحلتين السابقتين أولاً", TranslationHelper.GetString("TitleFactoryReset"));
            return;
        }

        if (!DialogHelper.ShowConfirmation(
            TranslationHelper.GetString("MsgFactoryResetWarningDescription"),
            TranslationHelper.GetString("TitleFactoryResetWarning")))
        {
            return;
        }

        if (_resetService == null)
        {
            DialogHelper.ShowError("خدمة إعادة ضبط المنظومة غير متوفرة", TranslationHelper.GetString("TitleFactoryReset"));
            return;
        }

        IsBusy = true;
        BusyMessage = "جاري أخذ نسخة احتياطية إجبارية وتصفير المنظومة...";

        try
        {
            var result = await _resetService.ResetSystemAsync(_userService?.CurrentUser?.Username ?? "Admin");

            if (result.Success)
            {
                DialogHelper.ShowInfo(
                    TranslationHelper.GetString("MsgFactoryResetSuccess"),
                    TranslationHelper.GetString("TitleFactoryReset"));

                // تسجيل الخروج القسري عبر MainViewModel
                if (App.ServiceProvider?.GetService(typeof(MainViewModel)) is MainViewModel mainVm)
                {
                    mainVm.ForceLogout();
                }
            }
            else
            {
                DialogHelper.ShowError(
                    string.Format(TranslationHelper.GetString("MsgFactoryResetFailed"), result.Message),
                    TranslationHelper.GetString("TitleFactoryReset"));
            }
        }
        catch (Exception ex)
        {
            DialogHelper.ShowError(
                string.Format(TranslationHelper.GetString("MsgFactoryResetFailed"), ex.Message),
                TranslationHelper.GetString("TitleFactoryReset"));
        }
        finally
        {
            IsBusy = false;
        }
    }

#if DEBUG
    // ─── أوامر توليد البيانات التجريبية (Debug Only) ───
    [RelayCommand]
    private async Task GenerateTestDataAsync()
    {
        if (!IsAdmin)
        {
            DialogHelper.ShowError("غير مصرح: هذه العملية مخصصة لمدير النظام فقط", "توليد بيانات تجريبية");
            return;
        }

        if (!DialogHelper.ShowConfirmation(
            "هل تريد بالتأكيد توليد بيانات تجريبية واقعية؟\n\n" +
            "• سيتم استكمال المواقع إلى 20 موقعاً واقعياً.\n" +
            "• سيتم إضافة 300 مصدراً مشعاً (توزيع واقعي للنشاط والانحلال، مصادر متعددة النظائر، وحالات تحذير وحرجة).\n" +
            "• سيتم إضافة 100 طلب استعارة (مرتجع، قيد الاستعارة، متأخر، معلّق).\n\n" +
            "ملاحظة: العملية مؤطرة ضمن \u2066Transaction\u2069 لضمان سلامة قاعدة البيانات.",
            "تأكيد توليد البيانات التجريبية (DEBUG)"))
        {
            return;
        }

        if (_dbFactory == null || _decayService == null)
        {
            DialogHelper.ShowError("خدمات قاعدة البيانات أو الحساب غير متوفرة", "خطأ في التهيئة");
            return;
        }

        IsBusy = true;
        BusyMessage = "جاري توليد البيانات التجريبية وحساب النشاط الإشعاعي...";

        try
        {
            var result = await TestDataGeneratorService.GenerateRealisticDataAsync(
                _dbFactory,
                _decayService,
                _alertService,
                _userService);

            if (result.Success)
            {
                string summary = "تمت عملية توليد البيانات التجريبية بنجاح!\n\n" +
                    $"• المواقع الإجمالية: \u2066{result.TotalLocations}\u2069 موقعاً (تم إضافة \u2066{result.AddedLocations}\u2069)\n" +
                    $"• المصادر المشعة: \u2066{result.TotalSources}\u2069 مصدراً (منها \u2066{result.MultiIsotopeSources}\u2069 متعددة النظائر)\n" +
                    $"• مصادر التنبيهات: \u2066{result.WarningAlertSources}\u2069 تحذير + \u2066{result.CriticalAlertSources}\u2069 حرج\n" +
                    $"• طلبات الاستعارة: \u2066{result.TotalBorrowRequests}\u2069 طلباً:\n" +
                    $"   - \u2066{result.ReturnedBorrows}\u2069 مسترجع (\u2066Returned\u2069)\n" +
                    $"   - \u2066{result.DeliveredBorrows}\u2069 جاري التسليم / نشط (\u2066Delivered\u2069)\n" +
                    $"   - \u2066{result.OverdueBorrows}\u2069 متأخر (\u2066Overdue\u2069)\n" +
                    $"   - \u2066{result.PendingOrApprovedBorrows}\u2069 معلّق / معتمد (\u2066Pending/Approved\u2069)";

                DialogHelper.ShowInfo(summary, "ملخص توليد البيانات التجريبية");
            }
            else
            {
                DialogHelper.ShowError(result.Message, "فشل التوليد");
            }
        }
        catch (Exception ex)
        {
            DialogHelper.ShowError($"حدث خطأ أثناء توليد البيانات: {ex.Message}", "خطأ");
        }
        finally
        {
            IsBusy = false;
        }
    }
#endif

    // ─── أوامر تبويب المظهر ───
    [RelayCommand]
    private void SetLightMode() => SetThemeMode(false);

    [RelayCommand]
    private void SetDarkMode() => SetThemeMode(true);

    [RelayCommand]
    private void SetThemeMode(bool isDark)
    {
        IsDarkMode = isDark;
        var username = _userService?.CurrentUser?.Username;
        SettingsHelper.SetUserTheme(username, isDark);
        App.ApplyTheme(isDark);
        App.ApplyAccentColor(SelectedAccentColor);
    }

    [RelayCommand]
    private void SelectAccentColor(string hexColor)
    {
        if (string.IsNullOrWhiteSpace(hexColor)) return;
        SelectedAccentColor = hexColor;
        var username = _userService?.CurrentUser?.Username;
        SettingsHelper.SetUserAccentColor(username, hexColor);
        App.ApplyAccentColor(hexColor);
    }
}

public class AccentColorOption : ObservableObject
{
    public string NameKey { get; set; } = string.Empty;
    public string DescriptionKey { get; set; } = string.Empty;
    public string HexColor { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
}

