using System;
using System.Collections.Generic;
using Sources.Models;

namespace Sources.Services;

public interface ILeakTestService
{
    List<LeakTestRecord> GetAllRecords(string? resultFilter = null, string? dueStatusFilter = null, string? search = null);
    List<LeakTestRecord> GetRecordsBySourceId(Guid sourceId);
    LeakTestRecord? GetLatestRecordBySourceId(Guid sourceId);
    LeakTestRecord? GetById(Guid id);
    (bool Success, string Message, LeakTestRecord? Record) AddRecord(LeakTestRecord record);
    (bool Success, string Message) UpdateRecord(LeakTestRecord record);
    (bool Success, string Message) DeleteRecord(Guid id);
    DateTime CalculateNextDueDate(DateTime testDate, int? customIntervalMonths = null);
}
