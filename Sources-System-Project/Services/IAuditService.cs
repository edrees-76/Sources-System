using System;
using System.Collections.Generic;
using Sources.Models;

namespace Sources.Services;

public interface IAuditService
{
    void Log(string action, string? tableName, Guid? recordId, string? details);
    void LogWithChanges(string action, string? tableName, Guid? recordId, string? details, string? oldValues, string? newValues);
    List<AuditLog> GetAuditLogs(int page = 1, int pageSize = 50, string? actionFilter = null, Guid? userFilter = null, DateTime? fromDate = null, DateTime? toDate = null);
    int GetTotalCount(string? actionFilter = null, Guid? userFilter = null, DateTime? fromDate = null, DateTime? toDate = null);
}
