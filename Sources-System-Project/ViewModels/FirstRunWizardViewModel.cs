using System;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sources.Helpers;
using Sources.Services;

namespace Sources.ViewModels;

/// <summary>
/// نموذج عرض معالج الإعداد الأول (First-Run Setup Wizard). يظهر مرة واحدة فقط قبل
/// شاشة تسجيل الدخول لطلب مجلد الحفظ الاحتياطي وتفعيل النسخ التلقائي (اختياري، افتراضياً معطّل).
/// لا يستعمل WeakReferenceMessenger، لذا لا يلزم التخلص (Dispose) منه في الاختبارات.
/// </summary>
public partial class FirstRunWizardViewModel : ObservableObject
{
    private readonly ISystemSettingsService _settingsService;
    private readonly ILicenseService _licenseService;

    [ObservableProperty] private string _backupPath = string.Empty;
    [ObservableProperty] private bool _autoBackupEnabled = false;

    /// <summary>يُطلَق عند اكتمال المعالج (إنهاء أو تخطٍّ) طلباً لإغلاق النافذة المضيفة.</summary>
    public event EventHandler? CloseRequested;

    public FirstRunWizardViewModel(ISystemSettingsService settingsService, ILicenseService licenseService)
    {
        _settingsService = settingsService;
        _licenseService = licenseService;
    }

    [RelayCommand]
    private void BrowseFolder()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = TranslationHelper.GetString("BrowseBackupTitle") ?? "اختر مجلد الحفظ الاحتياطي",
            InitialDirectory = Directory.Exists(BackupPath) ? BackupPath : string.Empty
        };

        if (dialog.ShowDialog() == true)
        {
            BackupPath = dialog.FolderName;
        }
    }

    [RelayCommand]
    private void Finish()
    {
        // منع تفعيل النسخ التلقائي دون مسار حفظ محدد
        if (AutoBackupEnabled && string.IsNullOrWhiteSpace(BackupPath))
        {
            DialogHelper.ShowWarning(
                TranslationHelper.GetString("MsgSelectBackupPath") ?? "يرجى تحديد مسار الحفظ أولاً",
                TranslationHelper.GetString("BackupTitle") ?? "النسخ الاحتياطي");
            return;
        }

        // في الوضع التجريبي (غير مفعّل)، SaveSetting تتجاهل الطلب صامتة عبر AuthorizationGuard.RequireActivated
        // لذا لا نستدعيها إطلاقاً هنا تجنباً لرسالة نجاح كاذبة
        if (!_licenseService.IsActivated)
        {
            DialogHelper.ShowInfo(
                TranslationHelper.GetString("FirstRunWizardTrialNoticeMessage") ?? "سيلزم ضبط إعدادات النسخ الاحتياطي من شاشة الإعدادات بعد تفعيل المنظومة.",
                TranslationHelper.GetString("FirstRunWizardTrialNoticeTitle") ?? "الوضع التجريبي");

            SettingsHelper.FirstRunWizardCompleted = true;
            CloseRequested?.Invoke(this, EventArgs.Empty);
            return;
        }

        _settingsService.SaveSetting("BackupPath", BackupPath);
        _settingsService.SaveSetting("AutoBackupEnabled", AutoBackupEnabled.ToString());

        SettingsHelper.FirstRunWizardCompleted = true;
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void Skip()
    {
        SettingsHelper.FirstRunWizardCompleted = true;
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }
}
