using System.Collections.Generic;
using System.Threading.Tasks;
using Sources.Models;

namespace Sources.Services
{
    public interface IReportingService
    {
        Task GenerateInventoryReportPdfAsync(IEnumerable<Source> sources, string filePath, string reportTitle);
        Task GenerateInventoryReportExcelAsync(IEnumerable<Source> sources, string filePath, string reportTitle);
        Task GenerateBorrowHistoryPdfAsync(IEnumerable<BorrowRequest> requests, string filePath);
        Task GenerateBorrowHistoryExcelAsync(IEnumerable<BorrowRequest> requests, string filePath);
        Task GenerateLowActivityAlertReportPdfAsync(IEnumerable<Source> sources, string filePath);
        Task GenerateLowActivityAlertReportExcelAsync(IEnumerable<Source> sources, string filePath);
        Task GenerateGeneralReportPdfAsync(IEnumerable<Source> inventory, IEnumerable<BorrowRequest> borrowing, IEnumerable<Source> lowActivity, IEnumerable<Source> lowActivityAlerts, string filePath);
        Task GenerateGeneralReportExcelAsync(IEnumerable<Source> inventory, IEnumerable<BorrowRequest> borrowing, IEnumerable<Source> lowActivity, IEnumerable<Source> lowActivityAlerts, string filePath);
        Task GenerateLocationsReportPdfAsync(IEnumerable<Location> locations, string filePath);
        Task GenerateLocationsReportExcelAsync(IEnumerable<Location> locations, string filePath);
        Task GenerateUsersReportPdfAsync(IEnumerable<User> users, string filePath);
        Task GenerateUsersReportExcelAsync(IEnumerable<User> users, string filePath);
        Task GenerateAuditLogsPdfAsync(IEnumerable<AuditLog> logs, string filePath);
        Task GenerateAuditLogsExcelAsync(IEnumerable<AuditLog> logs, string filePath);
        Task GenerateLeakTestsReportPdfAsync(IEnumerable<LeakTestRecord> records, string filePath, string reportTitle);
        Task GenerateLeakTestsReportExcelAsync(IEnumerable<LeakTestRecord> records, string filePath, string reportTitle);
        Task GenerateFailedLeakTestsReportPdfAsync(IEnumerable<LeakTestRecord> records, string filePath, string? reportTitle = null);
        Task GenerateFailedLeakTestsReportExcelAsync(IEnumerable<LeakTestRecord> records, string filePath, string? reportTitle = null);

        /// <summary>إنشاء تقرير جرد المصادر النيترونية بصيغة PDF</summary>
        Task GenerateNeutronInventoryReportPdfAsync(IEnumerable<NeutronSource> sources, string filePath, string? reportTitle = null);

        /// <summary>إنشاء تقرير جرد المصادر النيترونية بصيغة Excel</summary>
        Task GenerateNeutronInventoryReportExcelAsync(IEnumerable<NeutronSource> sources, string filePath, string? reportTitle = null);
    }
}

