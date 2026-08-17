using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Sources.Data;
using Sources.Models;

namespace Sources.Services;

public class AuditService : IAuditService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly IUserService _userService;

    public AuditService(IDbContextFactory<AppDbContext> dbFactory, IUserService userService)
    {
        _dbFactory = dbFactory;
        _userService = userService;
    }

    public void Log(string action, string? tableName, Guid? recordId, string? details)
    {
        LogWithChanges(action, tableName, recordId, details, null, null);
    }

    public void LogWithChanges(string action, string? tableName, Guid? recordId, string? details, string? oldValues, string? newValues)
    {
        try
        {
            using var db = _dbFactory.CreateDbContext();
            db.AuditLogs.Add(new AuditLog
            {
                UserId = _userService.CurrentUser?.Id,
                Action = action,
                TableName = tableName,
                RecordId = recordId,
                ActionDate = DateTime.Now,
                Details = details,
                OldValues = oldValues,
                NewValues = newValues
            });
            db.SaveChanges();
        }
        catch (Exception ex)
        {
            LoggerService.LogError("خطأ أثناء تسجيل التدقيق", ex);
        }
    }

    public List<AuditLog> GetAuditLogs(int page = 1, int pageSize = 50, string? actionFilter = null, Guid? userFilter = null, DateTime? fromDate = null, DateTime? toDate = null)
    {
        using var db = _dbFactory.CreateDbContext();
        var query = db.AuditLogs
            .Include(a => a.User)
            .AsQueryable();

        if (!string.IsNullOrEmpty(actionFilter))
            query = query.Where(a => a.Action == actionFilter);

        if (userFilter.HasValue)
            query = query.Where(a => a.UserId == userFilter.Value);

        if (fromDate.HasValue)
            query = query.Where(a => a.ActionDate >= fromDate.Value);

        if (toDate.HasValue)
        {
            var endOfDay = toDate.Value.Date.AddDays(1);
            query = query.Where(a => a.ActionDate < endOfDay);
        }

        return query
            .OrderByDescending(a => a.ActionDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();
    }

    public int GetTotalCount(string? actionFilter = null, Guid? userFilter = null, DateTime? fromDate = null, DateTime? toDate = null)
    {
        using var db = _dbFactory.CreateDbContext();
        var query = db.AuditLogs.AsQueryable();

        if (!string.IsNullOrEmpty(actionFilter))
            query = query.Where(a => a.Action == actionFilter);

        if (userFilter.HasValue)
            query = query.Where(a => a.UserId == userFilter.Value);

        if (fromDate.HasValue)
            query = query.Where(a => a.ActionDate >= fromDate.Value);

        if (toDate.HasValue)
        {
            var endOfDay = toDate.Value.Date.AddDays(1);
            query = query.Where(a => a.ActionDate < endOfDay);
        }

        return query.Count();
    }
}
