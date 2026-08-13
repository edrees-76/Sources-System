using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sources.Helpers;
using Sources.Services;
using System;
using System.Windows;

namespace Sources.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly IBackupService _backupService;
    private readonly ISystemSettingsService _settingsService;

    [ObservableProperty] private bool _isDarkMode;
    [ObservableProperty] private string _language;
    [ObservableProperty] private string _generalMessage = string.Empty;
    [ObservableProperty] private bool _hasGeneralMessage;

    // ─── النسخ الاحتياطي ───
    [ObservableProperty] private string _backupPath = string.Empty;
    [ObservableProperty] private bool _autoBackupEnabled;
    [ObservableProperty] private string _selectedFrequency = "Daily";
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _busyMessage = string.Empty;
    [ObservableProperty] private string _lastBackupInfo = string.Empty;
    
    [ObservableProperty] private string _backupMessage = string.Empty;
    [ObservableProperty] private bool _hasBackupMessage;

    public SettingsViewModel(IBackupService backupService, ISystemSettingsService settingsService)
    {
        _backupService = backupService;
        _settingsService = settingsService;

        IsDarkMode = SettingsHelper.IsDarkMode;
        Language = SettingsHelper.Language;

        // تحميل إعدادات النسخ الاحتياطي المحفوظة
        BackupPath = _settingsService.GetSetting("BackupPath", string.Empty);
        AutoBackupEnabled = _settingsService.GetSetting<bool>("AutoBackupEnabled", false);
        
        // تغيير القديم لو وجد (يومي -> Daily)
        var freq = _settingsService.GetSetting("AutoBackupFrequency", "Daily");
        if (freq == "يومي") freq = "Daily";
        if (freq == "أسبوعي") freq = "Weekly";
        if (freq == "شهري") freq = "Monthly";
        SelectedFrequency = freq;

        UpdateLastBackupInfo();
    }

    private void UpdateLastBackupInfo()
    {
        try
        {
            // البحث في المسار المخصص أولاً
            var customFolder = string.IsNullOrWhiteSpace(BackupPath)
                ? string.Empty
                : System.IO.Path.Combine(BackupPath, "النسخ الاحتياطى منظومة مسار");

            if (!string.IsNullOrEmpty(customFolder) && System.IO.Directory.Exists(customFolder))
            {
                var files = System.IO.Directory.GetFiles(customFolder, "MASAR_backup_*.db")
                    .Select(f => new System.IO.FileInfo(f))
                    .OrderByDescending(f => f.CreationTime)
                    .ToList();

                if (files.Count > 0)
                {
                    var latest = files[0];
                    var sizeDisplay = latest.Length < 1024 * 1024
                        ? $"{latest.Length / 1024.0:F1} KB"
                        : $"{latest.Length / (1024.0 * 1024.0):F1} MB";
                    LastBackupInfo = $"{latest.CreationTime:yyyy/MM/dd HH:mm} ({sizeDisplay})";
                    return;
                }
            }

            // إذا لم يوجد بيانات في المسار المخصص، نبحث في المسار الافتراضي
            var backups = _backupService.GetBackups();
            if (backups.Count > 0)
            {
                var latest = backups[0];
                LastBackupInfo = $"{latest.CreatedAt:yyyy/MM/dd HH:mm} ({latest.SizeDisplay})";
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

    [RelayCommand]
    private void ToggleTheme()
    {
        IsDarkMode = !IsDarkMode;
        SettingsHelper.IsDarkMode = IsDarkMode;
        App.ApplyTheme(IsDarkMode);
        ShowGeneralMsg(TranslationHelper.GetString(IsDarkMode ? "MsgThemeDark" : "MsgThemeLight"));
    }

    [RelayCommand]
    private void SetArabic()
    {
        Language = "ar";
        SettingsHelper.Language = "ar";
        App.ApplyLanguage("ar");
        ShowGeneralMsg(TranslationHelper.GetString("MsgLangArabic"));
        
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
        ShowGeneralMsg(TranslationHelper.GetString("MsgLangEnglish"));
        
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
            InitialDirectory = System.IO.Directory.Exists(BackupPath) ? BackupPath : string.Empty
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

        ShowBackupMsg(TranslationHelper.GetString("MsgSettingsSaved"));
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
                ShowBackupMsg(result.Message);
                UpdateLastBackupInfo();
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
            InitialDirectory = System.IO.Directory.Exists(BackupPath) ? BackupPath : string.Empty
        };

        if (dialog.ShowDialog() != true) return;

        var confirmResult = MessageBox.Show(
            TranslationHelper.GetString("MsgRestoreWarning"),
            TranslationHelper.GetString("RestoreBackupTitle"),
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No,
            MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading);

        if (confirmResult != MessageBoxResult.Yes) return;

        IsBusy = true;
        BusyMessage = TranslationHelper.GetString("MsgRestoringBackup");

        try
        {
            var result = _backupService.RestoreBackup(dialog.FileName);

            if (result.Success)
            {
                ShowBackupMsg(result.Message);
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

    private void ShowGeneralMsg(string m) { GeneralMessage = m; HasGeneralMessage = true; }
    private void ShowBackupMsg(string m) { BackupMessage = m; HasBackupMessage = true; }
}
