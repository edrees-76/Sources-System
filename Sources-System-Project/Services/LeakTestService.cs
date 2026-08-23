using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sources.Data;
using Sources.Models;

namespace Sources.Services;

public class LeakTestService : ILeakTestService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly IAuditService _auditService;
    private readonly IUserService _userService;
    private readonly ISystemSettingsService _settingsService;

    public LeakTestService(
        IDbContextFactory<AppDbContext> dbFactory,
        IAuditService auditService,
        IUserService userService,
        ISystemSettingsService settingsService)
    {
        _dbFactory = dbFactory;
        _auditService = auditService;
        _userService = userService;
        _settingsService = settingsService;
    }

    public DateTime CalculateNextDueDate(DateTime testDate, int? customIntervalMonths = null)
    {
        int interval = customIntervalMonths ?? _settingsService.GetSetting<int>(
            SystemSettingsDefaults.LeakTestIntervalMonthsKey,
            int.Parse(SystemSettingsDefaults.DefaultLeakTestIntervalMonths));

        if (interval <= 0) interval = 6;
        return testDate.AddMonths(interval);
    }

    public List<LeakTestRecord> GetAllRecords(string? resultFilter = null, string? dueStatusFilter = null, string? search = null)
    {
        using var db = _dbFactory.CreateDbContext();
        var query = db.LeakTestRecords
            .AsNoTracking()
            .Include(r => r.Source)
                .ThenInclude(s => s!.Radioisotope)
            .Include(r => r.Source)
                .ThenInclude(s => s!.Location)
            .Include(r => r.Source)
                .ThenInclude(s => s!.CurrentActivityUnit)
            .Include(r => r.Source)
                .ThenInclude(s => s!.SourceIsotopes)
                    .ThenInclude(si => si.Radioisotope)
            .Include(r => r.PerformedByUser)
            .AsQueryable();

        // تصفية النتيجة
        if (!string.IsNullOrWhiteSpace(resultFilter) && resultFilter != "All")
        {
            query = query.Where(r => r.Result == resultFilter);
        }

        // تصفية الاستحقاق
        if (!string.IsNullOrWhiteSpace(dueStatusFilter) && dueStatusFilter != "All")
        {
            var today = DateTime.Today;
            int warningDays = _settingsService.GetSetting<int>(
                SystemSettingsDefaults.LeakTestWarningDaysThresholdKey,
                int.Parse(SystemSettingsDefaults.DefaultLeakTestWarningDaysThreshold));
            if (warningDays <= 0) warningDays = 30;

            var thresholdDate = today.AddDays(warningDays);

            if (dueStatusFilter == "Overdue")
            {
                query = query.Where(r => r.NextDueDate.Date < today);
            }
            else if (dueStatusFilter == "DueSoon")
            {
                query = query.Where(r => r.NextDueDate.Date >= today && r.NextDueDate.Date <= thresholdDate);
            }
            else if (dueStatusFilter == "Valid")
            {
                query = query.Where(r => r.NextDueDate.Date > thresholdDate);
            }
        }

        // البحث النصي
        if (!string.IsNullOrWhiteSpace(search))
        {
            var sLower = search.Trim().ToLower();
            query = query.Where(r =>
                (r.Source != null && r.Source.SourceCode.ToLower().Contains(sLower)) ||
                (r.CertificateNumber != null && r.CertificateNumber.ToLower().Contains(sLower)) ||
                (r.InspectorName != null && r.InspectorName.ToLower().Contains(sLower)) ||
                (r.Notes != null && r.Notes.ToLower().Contains(sLower)) ||
                (r.Result.ToLower().Contains(sLower))
            );
        }

        return query
            .OrderByDescending(r => r.TestDate)
            .ThenByDescending(r => r.CreatedAt)
            .ThenBy(r => r.Source != null ? r.Source.SourceCode : string.Empty)
            .ToList();
    }

    public List<LeakTestRecord> GetRecordsBySourceId(Guid sourceId)
    {
        using var db = _dbFactory.CreateDbContext();
        return db.LeakTestRecords
            .AsNoTracking()
            .Include(r => r.Source)
                .ThenInclude(s => s!.Radioisotope)
            .Include(r => r.PerformedByUser)
            .Where(r => r.SourceId == sourceId)
            .OrderByDescending(r => r.TestDate)
            .ThenByDescending(r => r.CreatedAt)
            .ToList();
    }

    public LeakTestRecord? GetLatestRecordBySourceId(Guid sourceId)
    {
        using var db = _dbFactory.CreateDbContext();
        return db.LeakTestRecords
            .AsNoTracking()
            .Include(r => r.Source)
            .Include(r => r.PerformedByUser)
            .Where(r => r.SourceId == sourceId)
            .OrderByDescending(r => r.TestDate)
            .ThenByDescending(r => r.CreatedAt)
            .FirstOrDefault();
    }

    public LeakTestRecord? GetById(Guid id)
    {
        using var db = _dbFactory.CreateDbContext();
        return db.LeakTestRecords
            .AsNoTracking()
            .Include(r => r.Source)
                .ThenInclude(s => s!.Radioisotope)
            .Include(r => r.PerformedByUser)
            .FirstOrDefault(r => r.Id == id);
    }

    public (bool Success, string Message, LeakTestRecord? Record) AddRecord(LeakTestRecord record)
    {
        if (record == null) return (false, "سجل الفحص غير صالح", null);
        if (record.SourceId == Guid.Empty) return (false, "يجب تحديد المصدر المشع", null);

        using var db = _dbFactory.CreateDbContext();
        var source = db.Sources.Find(record.SourceId);
        if (source == null) return (false, "المصدر المحدد غير موجود", null);

        if (record.NextDueDate == default)
        {
            record.NextDueDate = CalculateNextDueDate(record.TestDate);
        }

        if (!record.PerformedByUserId.HasValue && _userService.CurrentUser != null)
        {
            record.PerformedByUserId = _userService.CurrentUser.Id;
        }

        record.CreatedAt = DateTime.Now;

        db.LeakTestRecords.Add(record);
        db.SaveChanges();

        _auditService.Log("Create", "LeakTestRecords", record.Id, 
            $"تسجيل فحص تسرب للمصدر: {source.SourceCode} (النتيجة: {record.ArabicResult}، الاستحقاق القادم: {record.NextDueDate:yyyy/MM/dd})");

        return (true, "تم تسجيل اختبار التسرب بنجاح", record);
    }


    public (bool Success, string Message) UpdateRecord(LeakTestRecord record)
    {
        if (record == null) return (false, "سجل الفحص غير صالح");

        using var db = _dbFactory.CreateDbContext();
        var existing = db.LeakTestRecords.Include(r => r.Source).FirstOrDefault(r => r.Id == record.Id);
        if (existing == null) return (false, "سجل الفحص غير موجود");

        var oldValuesObj = new
        {
            existing.SourceId,
            SourceCode = existing.Source?.SourceCode ?? "—",
            TestDate = existing.TestDate.ToString("yyyy-MM-dd"),
            NextDueDate = existing.NextDueDate.ToString("yyyy-MM-dd"),
            existing.Result,
            existing.MeasuredActivityBq,
            existing.InspectorName,
            existing.CertificateNumber,
            existing.Notes
        };
        string oldValuesJson = JsonSerializer.Serialize(oldValuesObj);

        existing.SourceId = record.SourceId;
        existing.TestDate = record.TestDate;
        existing.NextDueDate = record.NextDueDate;
        existing.Result = record.Result;
        existing.MeasuredActivityBq = record.MeasuredActivityBq;
        existing.InspectorName = record.InspectorName;
        existing.CertificateNumber = record.CertificateNumber;
        existing.Notes = record.Notes;

        var newValuesObj = new
        {
            existing.SourceId,
            SourceCode = existing.Source?.SourceCode ?? "—",
            TestDate = existing.TestDate.ToString("yyyy-MM-dd"),
            NextDueDate = existing.NextDueDate.ToString("yyyy-MM-dd"),
            existing.Result,
            existing.MeasuredActivityBq,
            existing.InspectorName,
            existing.CertificateNumber,
            existing.Notes
        };
        string newValuesJson = JsonSerializer.Serialize(newValuesObj);

        db.SaveChanges();

        _auditService.LogWithChanges("Update", "LeakTestRecords", record.Id,
            $"تعديل سجل فحص تسرب للمصدر: {existing.Source?.SourceCode ?? "—"}",
            oldValuesJson, newValuesJson);

        return (true, "تم تحديث سجل اختبار التسرب بنجاح");
    }


    public (bool Success, string Message) DeleteRecord(Guid id)
    {
        using var db = _dbFactory.CreateDbContext();
        var record = db.LeakTestRecords.Include(r => r.Source).FirstOrDefault(r => r.Id == id);
        if (record == null) return (false, "سجل الفحص غير موجود");

        var oldValuesObj = new
        {
            record.SourceId,
            SourceCode = record.Source?.SourceCode ?? "—",
            TestDate = record.TestDate.ToString("yyyy-MM-dd"),
            NextDueDate = record.NextDueDate.ToString("yyyy-MM-dd"),
            record.Result,
            record.MeasuredActivityBq,
            record.InspectorName,
            record.CertificateNumber,
            record.Notes
        };
        string oldValuesJson = JsonSerializer.Serialize(oldValuesObj);

        db.LeakTestRecords.Remove(record);
        db.SaveChanges();

        _auditService.LogWithChanges("Delete", "LeakTestRecords", id,
            $"حذف سجل فحص تسرب للمصدر: {record.Source?.SourceCode ?? "—"}",
            oldValuesJson, null);

        return (true, "تم حذف سجل اختبار التسرب بنجاح");
    }
}

