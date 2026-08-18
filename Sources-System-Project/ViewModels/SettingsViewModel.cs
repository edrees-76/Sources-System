using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sources.Helpers;
using Sources.Services;
using System;
using System.IO;
using System.Linq;
using System.Windows;

namespace Sources.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly IBackupService _backupService;
    private readonly ISystemSettingsService _settingsService;
    private readonly IAutoBackupService? _autoBackupService;
    private readonly IUserService? _userService;
    private readonly ISystemResetService? _resetService;

    // ─── التحكم في التبويبات ───
    [ObservableProperty] private string _selectedTab = "General";

    // ─── تبويب العام ───
    [ObservableProperty] private string _language;

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
    [ObservableProperty] private string _facilityName = string.Empty;
    [ObservableProperty] private string _facilityAddress = string.Empty;
    [ObservableProperty] private string _technicalDirector = string.Empty;

    // ─── تبويب الوضع الافتراضي (Factory Reset) ───
    public bool IsAdmin => _userService?.CurrentUser?.IsAdmin == true;
    public string RequiredResetPhrase => "إعادة ضبط المنظومة";

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
        ISystemResetService? resetService = null)
    {
        _backupService = backupService;
        _settingsService = settingsService;
        _autoBackupService = autoBackupService;
        _userService = userService;
        _resetService = resetService;

        Language = SettingsHelper.Language;

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

        _settingsService.SaveSetting("LowActivityThresholdPercent", LowActivityThresholdPercent.ToString());
        _settingsService.SaveSetting("NotificationCheckIntervalMinutes", NotificationCheckIntervalMinutes.ToString());
        _settingsService.SaveSetting("DueSoonDaysThreshold", DueSoonDaysThreshold.ToString());
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
}
