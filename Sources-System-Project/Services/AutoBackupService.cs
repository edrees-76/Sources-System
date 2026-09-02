using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Timers;
using System.Windows;

namespace Sources.Services;

public class AutoBackupService : IAutoBackupService, IDisposable
{
    private readonly IBackupService _backupService;
    private readonly ISystemSettingsService _settingsService;
    private System.Timers.Timer? _timer;
    private bool _isChecking;

    public event EventHandler? BackupCompleted;

    public AutoBackupService(IBackupService backupService, ISystemSettingsService settingsService)
    {
        _backupService = backupService;
        _settingsService = settingsService;
    }

    public void Start()
    {
        if (_timer != null) return;

        // Check every 30 minutes in the background
        _timer = new System.Timers.Timer(TimeSpan.FromMinutes(30).TotalMilliseconds);
        _timer.Elapsed += (s, e) => CheckAndPerformAutoBackup();
        _timer.AutoReset = true;
        _timer.Start();

        // Perform an initial check asynchronously shortly after startup
        System.Threading.Tasks.Task.Run(async () =>
        {
            await System.Threading.Tasks.Task.Delay(5000); // Wait 5 seconds after startup
            CheckAndPerformAutoBackup();
        });
    }

    public void Stop()
    {
        _timer?.Stop();
        _timer?.Dispose();
        _timer = null;
    }

    public void TriggerImmediateCheck()
    {
        System.Threading.Tasks.Task.Run(() => CheckAndPerformAutoBackup());
    }

    private void CheckAndPerformAutoBackup()
    {
        if (_isChecking) return;
        _isChecking = true;

        try
        {
            var isEnabled = _settingsService.GetSetting<bool>("AutoBackupEnabled", false);
            if (!isEnabled)
            {
                LoggerService.LogInfo("فحص النسخ الاحتياطي التلقائي: تم الفحص — النسخ التلقائي معطل حالياً (AutoBackupEnabled = false).");
                return;
            }

            var frequency = _settingsService.GetSetting("AutoBackupFrequency", "Daily");
            var backupPath = _settingsService.GetSetting("BackupPath", string.Empty);

            var requiredInterval = frequency switch
            {
                "Weekly" => TimeSpan.FromDays(7),
                "Monthly" => TimeSpan.FromDays(30),
                _ => TimeSpan.FromDays(1) // Default Daily
            };

            var scan = BackupFolderScanner.ScanLatest(BuildTargetFolders(backupPath));

            if (scan.Outcome == BackupScanOutcome.ScanFailed)
            {
                LoggerService.LogWarning(
                    "فحص النسخ الاحتياطي التلقائي: تعذّر مسح مجلد النسخ ولم يُعثر على أي نسخة، فتُخطّى هذه الدورة. " +
                    "لا تُنفَّذ نسخة على الشك تفادياً لتكرارها كل دورة. راجع صلاحيات المجلد أو اتصال القرص. " +
                    $"مسار النسخ المحفوظ: {(string.IsNullOrWhiteSpace(backupPath) ? Sources.Data.DatabasePaths.BackupsDirectory : backupPath)}");
                return;
            }

            var lastBackupDate = scan.Latest;
            var shouldBackup = !lastBackupDate.HasValue || (DateTime.Now - lastBackupDate.Value) >= requiredInterval;

            var elapsedText = lastBackupDate.HasValue
                ? $"{(DateTime.Now - lastBackupDate.Value).TotalHours:F1} ساعة"
                : "لا توجد نسخ سابقة";

            LoggerService.LogInfo($"فحص النسخ الاحتياطي التلقائي: الجدولة={frequency} (الفاصل={requiredInterval.TotalHours:F1} ساعة)، آخر نسخة={(lastBackupDate.HasValue ? lastBackupDate.Value.ToString("yyyy-MM-dd HH:mm:ss") : "لا يوجد")}، المنقضي={elapsedText}، القرار={(shouldBackup ? "تنفيذ نسخة احتياطية الآن" : "تخطي — لم يحن الموعد بعد")}");

            if (shouldBackup)
            {
                LoggerService.LogInfo($"بدء تنفيذ النسخ الاحتياطي التلقائي (الجدولة: {frequency})...");

                var result = string.IsNullOrWhiteSpace(backupPath)
                    ? _backupService.CreateBackup()
                    : _backupService.CreateBackup(backupPath);

                if (result.Success)
                {
                    LoggerService.LogInfo($"نجح النسخ الاحتياطي التلقائي بنجاح: {result.BackupPath}");
                    
                    try
                    {
                        BackupCompleted?.Invoke(this, EventArgs.Empty);
                    }
                    catch (Exception ex)
                    {
                        LoggerService.LogError("خطأ أثناء إشعار اكتمال النسخ التلقائي", ex);
                    }
                }
                else
                {
                    LoggerService.LogWarning($"تعذر إتمام النسخ الاحتياطي التلقائي: {result.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            LoggerService.LogError("خطأ غير معالج أثناء محاولة النسخ الاحتياطي التلقائي", ex);
        }
        finally
        {
            _isChecking = false;
        }
    }

    private static List<string> BuildTargetFolders(string backupPath)
    {
        var targetFolders = new List<string>();

        if (!string.IsNullOrWhiteSpace(backupPath) && Directory.Exists(backupPath))
        {
            targetFolders.Add(Path.Combine(backupPath, BackupService.BackupFolderName));
            targetFolders.Add(Path.Combine(backupPath, BackupService.LegacyBackupFolderName));
            targetFolders.Add(backupPath);
        }

        targetFolders.Add(Sources.Data.DatabasePaths.BackupsDirectory);
        return targetFolders;
    }

    public void Dispose()
    {
        Stop();
    }
}
