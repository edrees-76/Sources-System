using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Sources.Data;
using Sources.Models;

namespace Sources.Services;

/// <summary>
/// خدمة التنبيهات الذكية — تراقب المصادر وتولّد تنبيهات آلية
/// </summary>
public class AlertService : IAlertService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly IDecayCalculationService _decayService;
    private readonly ISystemSettingsService _settingsService;

    public AlertService(
        IDbContextFactory<AppDbContext> dbFactory, 
        IDecayCalculationService decayService,
        ISystemSettingsService settingsService)
    {
        _dbFactory = dbFactory;
        _decayService = decayService;
        _settingsService = settingsService;
    }

    /// <summary>توليد التنبيهات عند فتح التطبيق أو الطلب</summary>
    public List<AlertNotification> GenerateAlerts()
    {
        var alerts = new List<AlertNotification>();

        using var db = _dbFactory.CreateDbContext();
        var activeSources = db.Sources
            .Include(s => s.Radioisotope)
            .Include(s => s.InitialActivityUnit)
            .Include(s => s.CurrentActivityUnit)
            .Include(s => s.Location)
            .Include(s => s.SourceIsotopes)
                .ThenInclude(si => si.Radioisotope)
            .Where(s => s.Status == "InUse" || s.Status == "Storage")
            .ToList();

        foreach (var source in activeSources)
        {
            // ─── تنبيه انخفاض النشاط الإشعاعي (قانون التحلل: 5 إلى 6 أضعاف نصف العمر T½) ───
            double maxHalfLivesElapsed = -1;
            string worstIsotopeSymbol = string.Empty;

            if (source.HasDetailedIsotopes && source.SourceIsotopes != null && source.SourceIsotopes.Any(si => si.Radioisotope != null))
            {
                foreach (var si in source.SourceIsotopes.Where(si => si.Radioisotope != null))
                {
                    var isotope = si.Radioisotope!;
                    var calibDate = si.CalibrationDate ?? source.CalibrationDate;
                    if (calibDate == default) continue;

                    var halfLifeSec = ConvertToSeconds(isotope.HalfLife, isotope.HalfLifeUnit);
                    if (halfLifeSec <= 0) continue;

                    var elapsedSec = (DateTime.Now - calibDate).TotalSeconds;
                    if (elapsedSec < 0) elapsedSec = 0;

                    var halfLives = elapsedSec / halfLifeSec;
                    if (halfLives > maxHalfLivesElapsed)
                    {
                        maxHalfLivesElapsed = halfLives;
                        worstIsotopeSymbol = isotope.Symbol;
                    }
                }
            }
            else if (source.Radioisotope != null && source.CalibrationDate != default)
            {
                var isotope = source.Radioisotope;
                var halfLifeSec = ConvertToSeconds(isotope.HalfLife, isotope.HalfLifeUnit);
                if (halfLifeSec > 0)
                {
                    var elapsedSec = (DateTime.Now - source.CalibrationDate).TotalSeconds;
                    if (elapsedSec < 0) elapsedSec = 0;

                    maxHalfLivesElapsed = elapsedSec / halfLifeSec;
                    worstIsotopeSymbol = isotope.Symbol;
                }
            }

            if (maxHalfLivesElapsed >= 6.0)
            {
                var symbolPart = string.IsNullOrEmpty(worstIsotopeSymbol) ? "" : $" ({worstIsotopeSymbol})";
                alerts.Add(new AlertNotification
                {
                    AlertType = "LowActivity",
                    Severity = "Critical",
                    Message = $"المصدر {source.SourceCode}{symbolPart}: انخفاض حرج في النشاط الإشعاعي (انقضى {maxHalfLivesElapsed:F1} فترة نصف عمر)",
                    SourceId = source.Id
                });
            }
            else if (maxHalfLivesElapsed >= 5.0)
            {
                var symbolPart = string.IsNullOrEmpty(worstIsotopeSymbol) ? "" : $" ({worstIsotopeSymbol})";
                alerts.Add(new AlertNotification
                {
                    AlertType = "LowActivity",
                    Severity = "Warning",
                    Message = $"المصدر {source.SourceCode}{symbolPart}: اقتراب انخفاض النشاط الإشعاعي (انقضى {maxHalfLivesElapsed:F1} فترة نصف عمر من أصل 6)",
                    SourceId = source.Id
                });
            }
        }

        // تنظيف أي تنبيهات ملغاة من النوع القديم (CalibrationDue أو نص المعايرة)
        var obsoleteAlerts = db.AlertNotifications
            .Where(a => a.AlertType == "CalibrationDue" || a.Message.Contains("معايرة"))
            .ToList();
        if (obsoleteAlerts.Any())
        {
            db.AlertNotifications.RemoveRange(obsoleteAlerts);
        }

        // تنظيف تنبيهات المصادر التي لم تعد في حالة نشاط منخفض
        var currentAlertSourceIds = alerts.Select(a => a.SourceId).Where(id => id.HasValue).Select(id => id!.Value).ToHashSet();
        var resolvedAlerts = db.AlertNotifications
            .Where(a => a.SourceId.HasValue && !currentAlertSourceIds.Contains(a.SourceId.Value))
            .ToList();
        if (resolvedAlerts.Any())
        {
            db.AlertNotifications.RemoveRange(resolvedAlerts);
        }

        // حفظ التنبيهات الجديدة وتحديث القائم منها
        SaveNewAlerts(db, alerts);

        return GetActiveAlerts();
    }

    private static double ConvertToSeconds(double value, string? unit)
    {
        return unit?.ToLower() switch
        {
            "seconds" or "second" or "s" => value,
            "minutes" or "minute" or "min" or "m" => value * 60,
            "hours" or "hour" or "h" => value * 3600,
            "days" or "day" or "d" => value * 86400,
            "months" or "month" or "mo" => value * 30 * 86400,
            "years" or "year" or "yr" or "y" => value * 365.25 * 86400,
            _ => value * 365.25 * 86400
        };
    }

    private void SaveNewAlerts(AppDbContext db, List<AlertNotification> alerts)
    {
        var existingAlerts = db.AlertNotifications
            .Where(a => !a.IsDismissed)
            .ToList();

        foreach (var alert in alerts)
        {
            var existing = existingAlerts.FirstOrDefault(e => e.SourceId == alert.SourceId && e.AlertType == alert.AlertType);
            if (existing == null)
            {
                db.AlertNotifications.Add(alert);
            }
            else
            {
                // تحديث مستوى الخطورة والرسالة إن تغيرت
                existing.Severity = alert.Severity;
                existing.Message = alert.Message;
            }
        }

        db.SaveChanges();
    }

    /// <summary>جلب التنبيهات النشطة</summary>
    public List<AlertNotification> GetActiveAlerts()
    {
        using var db = _dbFactory.CreateDbContext();
        return db.AlertNotifications
            .Include(a => a.Source)
            .Where(a => !a.IsDismissed)
            .OrderByDescending(a => a.Severity == "Critical" ? 3 : a.Severity == "Warning" ? 2 : 1)
            .ThenByDescending(a => a.CreatedAt)
            .ToList();
    }

    /// <summary>عدد التنبيهات غير المقروءة</summary>
    public int GetUnreadCount()
    {
        using var db = _dbFactory.CreateDbContext();
        return db.AlertNotifications.Count(a => !a.IsRead && !a.IsDismissed);
    }

    /// <summary>تعليم تنبيه كمقروء</summary>
    public void MarkAsRead(Guid alertId)
    {
        using var db = _dbFactory.CreateDbContext();
        var alert = db.AlertNotifications.Find(alertId);
        if (alert != null)
        {
            alert.IsRead = true;
            db.SaveChanges();
        }
    }

    /// <summary>إخفاء تنبيه</summary>
    public void DismissAlert(Guid alertId)
    {
        using var db = _dbFactory.CreateDbContext();
        var alert = db.AlertNotifications.Find(alertId);
        if (alert != null)
        {
            alert.IsDismissed = true;
            db.SaveChanges();
        }
    }

    /// <summary>تعليم كل التنبيهات كمقروءة</summary>
    public void MarkAllAsRead()
    {
        using var db = _dbFactory.CreateDbContext();
        var unread = db.AlertNotifications.Where(a => !a.IsRead && !a.IsDismissed).ToList();
        foreach (var a in unread) a.IsRead = true;
        db.SaveChanges();
    }
}
