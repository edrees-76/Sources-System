using System;
using System.Collections.Generic;
using Sources.Models;
using Sources.Services;

namespace Sources.Tests.Fakes;

public class FakeAuditService : IAuditService
{
    public class AuditLogEntry
    {
        public string Action { get; set; } = string.Empty;
        public string? TableName { get; set; }
        public Guid? RecordId { get; set; }
        public string? Details { get; set; }
        public string? OldValues { get; set; }
        public string? NewValues { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }

    public List<AuditLogEntry> LoggedEntries { get; } = new();

    public void Log(string action, string? tableName, Guid? recordId, string? details)
    {
        LoggedEntries.Add(new AuditLogEntry
        {
            Action = action,
            TableName = tableName,
            RecordId = recordId,
            Details = details
        });
    }

    public void LogWithChanges(string action, string? tableName, Guid? recordId, string? details, string? oldValues, string? newValues)
    {
        LoggedEntries.Add(new AuditLogEntry
        {
            Action = action,
            TableName = tableName,
            RecordId = recordId,
            Details = details,
            OldValues = oldValues,
            NewValues = newValues
        });
    }

    public List<AuditLog> GetAuditLogs(int page = 1, int pageSize = 50, string? actionFilter = null, Guid? userFilter = null, DateTime? fromDate = null, DateTime? toDate = null)
    {
        return new List<AuditLog>();
    }

    public int GetTotalCount(string? actionFilter = null, Guid? userFilter = null, DateTime? fromDate = null, DateTime? toDate = null)
    {
        return LoggedEntries.Count;
    }
}
