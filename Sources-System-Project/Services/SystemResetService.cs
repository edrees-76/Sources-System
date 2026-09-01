using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sources.Data;
using Sources.Models;

namespace Sources.Services;

public class SystemResetService : ISystemResetService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly IBackupService _backupService;
    private readonly ISystemSettingsService? _settingsService;
    private readonly ISourceCertificateService? _certificateService;

    public SystemResetService(
        IDbContextFactory<AppDbContext> dbFactory,
        IBackupService backupService,
        ISystemSettingsService? settingsService = null,
        ISourceCertificateService? certificateService = null)
    {
        _dbFactory = dbFactory;
        _backupService = backupService;
        _settingsService = settingsService;
        _certificateService = certificateService;
    }

    public async Task<(bool Success, string Message, string? BackupPath)> ResetSystemAsync(string executedByUsername)
    {
        // 1. أخذ نسخة احتياطية كاملة إجبارية قبل أي تعديل
        var backupResult = _backupService.CreateBackup();
        if (!backupResult.Success || string.IsNullOrEmpty(backupResult.BackupPath))
        {
            return (false, $"فشل إنشاء النسخة الاحتياطية الإجبارية: {backupResult.Message}", null);
        }

        try
        {
            using var db = _dbFactory.CreateDbContext();
            using var transaction = await db.Database.BeginTransactionAsync();

            try
            {
                // 2. حذف الصفوف بالترتيب الآمن للمفاتيح الخارجية مع تجاوز مرشحات الحذف الناعم:
                // AlertNotifications → BorrowRequests → SourceLocationHistories → SourceIsotopes → LeakTestRecords → SourceCertificates → Sources → NeutronSources → Locations → AuditLogs
                db.AlertNotifications.RemoveRange(db.AlertNotifications);
                db.BorrowRequests.RemoveRange(db.BorrowRequests);
                db.SourceLocationHistories.RemoveRange(db.SourceLocationHistories);
                db.SourceIsotopes.RemoveRange(db.SourceIsotopes);
                db.LeakTestRecords.RemoveRange(db.LeakTestRecords);
                db.SourceCertificates.RemoveRange(db.SourceCertificates);
                db.Sources.RemoveRange(db.Sources.IgnoreQueryFilters());
                db.NeutronSources.RemoveRange(db.NeutronSources.IgnoreQueryFilters());
                db.Locations.RemoveRange(db.Locations.IgnoreQueryFilters());
                db.AuditLogs.RemoveRange(db.AuditLogs);

                await db.SaveChangesAsync();

                // 3. حذف ملفات الشهادات الفعلية من القرص
                try
                {
                    _certificateService?.DeleteAllCertificateFiles();
                }
                catch (Exception ex)
                {
                    LoggerService.LogWarning($"تعذر إكمال حذف ملفات الشهادات أثناء إعادة الضبط: {ex.Message}");
                }

                // 4. إعادة ضبط قيم إعدادات النظام للقيم الافتراضية الموحدة
                var existingSettings = db.AppSettings.ToList();
                foreach (var kvp in SystemSettingsDefaults.AllDefaults)
                {
                    var setting = existingSettings.FirstOrDefault(s => s.Key == kvp.Key);
                    if (setting != null)
                    {
                        setting.Value = kvp.Value;
                    }
                    else
                    {
                        db.AppSettings.Add(new AppSetting { Key = kvp.Key, Value = kvp.Value });
                    }
                }

                await db.SaveChangesAsync();

                // 5. تسجيل عملية التصفير في AuditLog بعد حذف السجلات القديمة (ليكون أول سجل)
                var executingUser = db.Users.FirstOrDefault(u => u.Username == executedByUsername);
                var resetLog = new AuditLog
                {
                    Id = Guid.NewGuid(),
                    UserId = executingUser?.Id,
                    ActionDate = DateTime.Now,
                    Action = "SystemReset",
                    TableName = "System",
                    RecordId = Guid.Empty,
                    Details = $"إعادة ضبط المنظومة للوضع الافتراضي (Factory Reset) شاملاً المصادر المشعة والنيترونية وفحوصات التسرب والشهادات والسجلات المحذوفة بواسطة {executedByUsername}. تم حفظ نسخة احتياطية في: {Path.GetFileName(backupResult.BackupPath)}"
                };
                db.AuditLogs.Add(resetLog);

                await db.SaveChangesAsync();

                await transaction.CommitAsync();

                // تحديث الكاش
                _settingsService?.ClearCache();

                return (true, "تمت إعادة ضبط المنظومة للوضع الافتراضي بنجاح", backupResult.BackupPath);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return (false, $"حدث خطأ أثناء تنفيذ عملية التصفير: {ex.Message}", backupResult.BackupPath);
            }
        }
        catch (Exception ex)
        {
            return (false, $"حدث خطأ أثناء الاتصال بقاعدة البيانات: {ex.Message}", backupResult.BackupPath);
        }
    }
}
