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
        Task GenerateCalibrationReportPdfAsync(IEnumerable<Source> sources, string filePath);
        Task GenerateCalibrationReportExcelAsync(IEnumerable<Source> sources, string filePath);
        Task GenerateGeneralReportPdfAsync(IEnumerable<Source> inventory, IEnumerable<BorrowRequest> borrowing, IEnumerable<Source> lowActivity, IEnumerable<Source> calibration, string filePath);
        Task GenerateGeneralReportExcelAsync(IEnumerable<Source> inventory, IEnumerable<BorrowRequest> borrowing, IEnumerable<Source> lowActivity, IEnumerable<Source> calibration, string filePath);
    }
}
