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
            .Where(s => s.Status == "Active" || s.Status == "InUse" || s.Status == "Storage")
            .ToList();

        // جلب الإعدادات الديناميكية
        var calibThreshold = _settingsService.GetSetting("CalibrationThresholdDays", 730);
        var lowActivityThreshold = _settingsService.GetSetting("LowActivityThresholdPercent", 10.0);

        foreach (var source in activeSources)
        {
            if (source.Radioisotope == null || source.InitialActivityUnit == null) continue;

            // ─── تنبيه 1: معايرة انتهت أو قاربت على الانتهاء ───
            var calibrationAge = (DateTime.Now - source.CalibrationDate).TotalDays;
            if (calibrationAge > calibThreshold)
            {
                alerts.Add(new AlertNotification
                {
                    AlertType = "CalibrationDue",
                    Severity = "Critical",
                    Message = $"المصدر {source.SourceCode} يحتاج إعادة معايرة (انتهت منذ {(int)(calibrationAge - calibThreshold)} يوم)",
                    SourceId = source.Id
                });
            }
            else if (calibrationAge > (calibThreshold - 60)) // تنبيه قبل 60 يوم
            {
                alerts.Add(new AlertNotification
                {
                    AlertType = "CalibrationDue",
                    Severity = "Warning",
                    Message = $"المصدر {source.SourceCode} يقترب من موعد إعادة المعايرة (متبقي {(int)(calibThreshold - calibrationAge)} يوم)",
                    SourceId = source.Id
                });
            }

            // ─── تنبيه 2: اضمحلال عالي / نشاط منخفض ───
            var currentBq = _decayService.CalculateCurrentActivityForSource(source, source.Radioisotope, source.InitialActivityUnit);
            var initialBq = source.InitialActivityValue * source.InitialActivityUnit.ConversionToBq;
            var currentPercent = initialBq > 0 ? (currentBq / initialBq) * 100 : 0;

            if (currentPercent <= lowActivityThreshold)
            {
                alerts.Add(new AlertNotification
                {
                    AlertType = "LowActivity",
                    Severity = currentPercent <= (lowActivityThreshold / 2) ? "Critical" : "Warning",
                    Message = $"المصدر {source.SourceCode} ({source.Radioisotope.Symbol}) وصل لنشاط منخفض بنسبة {currentPercent:F1}%",
                    SourceId = source.Id
                });
            }
        }

        // حفظ التنبيهات الجديدة (بدون تكرار)
        SaveNewAlerts(db, alerts);

        return GetActiveAlerts();
    }

    private void SaveNewAlerts(AppDbContext db, List<AlertNotification> alerts)
    {
        var existingAlerts = db.AlertNotifications
            .Where(a => !a.IsDismissed)
            .ToList();

        foreach (var alert in alerts)
        {
            // لا نضيف تنبيه مكرر لنفس المصدر ونفس النوع
            if (!existingAlerts.Any(e => e.SourceId == alert.SourceId && e.AlertType == alert.AlertType))
            {
                db.AlertNotifications.Add(alert);
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
