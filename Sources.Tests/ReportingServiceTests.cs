using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ClosedXML.Excel;
using Sources.Models;
using Sources.Services;
using Xunit;

namespace Sources.Tests;

public class ReportingServiceTests : IDisposable
{
    private readonly string _testDir;
    private readonly ReportingService _sut;

    public ReportingServiceTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "Sources_ReportingServiceTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);
        _sut = new ReportingService();
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testDir))
            {
                Directory.Delete(_testDir, recursive: true);
            }
        }
        catch
        {
            // Ignore cleanup errors in temp directory
        }
    }

    private string GetTempFilePath(string extension)
    {
        return Path.Combine(_testDir, $"{Guid.NewGuid():N}.{extension.TrimStart('.')}");
    }

    #region Test Data Helpers

    private List<Source> CreateSampleSources()
    {
        var unit = new ActivityUnit { UnitName = "MegaBecquerel", UnitSymbol = "MBq", ConversionToBq = 1e6 };
        var isotope = new Radioisotope { Name = "Cesium-137", Symbol = "Cs-137", HalfLife = 11000, HalfLifeUnit = "days" };
        var location = new Location { LocationName = "مختبر الفيزياء النووية", Building = "المبنى الرئيسي", Room = "101" };
        var adder1 = new User { Id = Guid.NewGuid(), FullName = "أحمد محمد", Username = "ahmed" };
        var adder2 = new User { Id = Guid.NewGuid(), FullName = "سارة علي", Username = "sara" };

        return new List<Source>
        {
            new()
            {
                SourceCode = "SRC-001",
                Radioisotope = isotope,
                InitialActivityValue = 100,
                InitialActivityUnit = unit,
                CurrentActivityValue = 95.5,
                CurrentActivityUnit = unit,
                CalibrationDate = DateTime.Now.AddMonths(-6),
                Location = location,
                Status = "InUse",
                AddedBy = adder1.Id,
                AddedByUser = adder1
            },
            new()
            {
                SourceCode = "SRC-002",
                Radioisotope = isotope,
                InitialActivityValue = 50,
                InitialActivityUnit = unit,
                CurrentActivityValue = 48.2,
                CurrentActivityUnit = unit,
                CalibrationDate = DateTime.Now.AddYears(-1),
                Location = location,
                Status = "Storage",
                AddedBy = adder2.Id,
                AddedByUser = adder2
            }
        };
    }

    private List<BorrowRequest> CreateSampleBorrowRequests(Source? source = null)
    {
        source ??= CreateSampleSources().First();
        var user = new User { FullName = "د. خالد السعيد", Username = "khaled" };
        var borrowAdder1 = new User { Id = Guid.NewGuid(), FullName = "أمين المستودع", Username = "keeper" };
        var borrowAdder2 = new User { Id = Guid.NewGuid(), FullName = "مدير المختبر", Username = "manager" };

        return new List<BorrowRequest>
        {
            new()
            {
                Source = source,
                BorrowerName = "د. خالد السعيد",
                BorrowerUser = user,
                Purpose = "أبحاث جامعية",
                RequestDate = DateTime.Now.AddDays(-5),
                ExpectedReturnDate = DateTime.Now.AddDays(10),
                Status = "Approved",
                AddedBy = borrowAdder1.Id,
                AddedByUser = borrowAdder1
            },
            new()
            {
                Source = source,
                BorrowerName = "م. منى العبدالله",
                Purpose = "معايرة أجهزة قياس",
                RequestDate = DateTime.Now.AddDays(-2),
                ExpectedReturnDate = DateTime.Now.AddDays(5),
                Status = "Pending",
                AddedBy = borrowAdder2.Id,
                AddedByUser = borrowAdder2
            }
        };
    }

    private List<User> CreateSampleUsers()
    {
        var adminRole = new Role { RoleName = "مدير النظام" };
        var userRole = new Role { RoleName = "مستخدم عادي" };

        return new List<User>
        {
            new()
            {
                FullName = "مدير المنظومة",
                Username = "admin",
                Role = adminRole,
                Email = "admin@system.local",
                IsActive = true,
                LastLoginDate = DateTime.Now.AddHours(-2),
                LockoutEnd = null
            },
            new()
            {
                FullName = "مستخدم مقفل",
                Username = "locked_user",
                Role = userRole,
                Email = "locked@system.local",
                IsActive = false,
                LastLoginDate = DateTime.Now.AddDays(-10),
                LockoutEnd = DateTime.Now.AddDays(1) // Locked
            }
        };
    }

    private List<AuditLog> CreateSampleAuditLogs()
    {
        var user = new User { Id = Guid.NewGuid(), FullName = "أحمد محمد" };

        return new List<AuditLog>
        {
            new()
            {
                UserId = user.Id,
                User = user,
                Action = "إضافة مصدر جديد",
                TableName = "Sources",
                Details = "تمت إضافة المصدر SRC-001 بنجاح",
                ActionDate = DateTime.Now.AddMinutes(-30)
            },
            new()
            {
                UserId = null,
                User = null, // System automatic action
                Action = "نسخ احتياطي تلقائي",
                TableName = "System",
                Details = "تم إنشاء نسخة احتياطية دورية",
                ActionDate = DateTime.Now.AddHours(-1)
            }
        };
    }

    #endregion

    #region 1. Basic Generation Tests with Real Data (12 Functions: 6 PDF + 6 Excel)

    [Fact]
    public async Task GenerateInventoryReportPdfAsync_WithData_CreatesValidPdfFile()
    {
        var filePath = GetTempFilePath("pdf");
        var sources = CreateSampleSources();

        await _sut.GenerateInventoryReportPdfAsync(sources, filePath, "تقرير جرد المصادر المشعة");

        Assert.True(File.Exists(filePath));
        Assert.True(new FileInfo(filePath).Length > 0);
    }

    [Fact]
    public async Task GenerateInventoryReportExcelAsync_WithData_CreatesValidExcelFile()
    {
        var filePath = GetTempFilePath("xlsx");
        var sources = CreateSampleSources();

        await _sut.GenerateInventoryReportExcelAsync(sources, filePath, "جرد المصادر");

        Assert.True(File.Exists(filePath));
        Assert.True(new FileInfo(filePath).Length > 0);

        using var wb = new XLWorkbook(filePath);
        Assert.Single(wb.Worksheets);
        var ws = wb.Worksheet("جرد المصادر");
        Assert.True(ws.RightToLeft);
        Assert.Equal("#", ws.Cell(1, 1).GetString());
        Assert.Equal("رقم المصدر", ws.Cell(1, 2).GetString());
        Assert.Equal("النظير", ws.Cell(1, 3).GetString());
        Assert.Equal(1, ws.Cell(2, 1).GetValue<int>());
        Assert.Equal("SRC-001", ws.Cell(2, 2).GetString());
        Assert.Equal(2, ws.Cell(3, 1).GetValue<int>());
        Assert.Equal("SRC-002", ws.Cell(3, 2).GetString());
    }

    [Fact]
    public async Task GenerateBorrowHistoryPdfAsync_WithData_CreatesValidPdfFile()
    {
        var filePath = GetTempFilePath("pdf");
        var requests = CreateSampleBorrowRequests();

        await _sut.GenerateBorrowHistoryPdfAsync(requests, filePath);

        Assert.True(File.Exists(filePath));
        Assert.True(new FileInfo(filePath).Length > 0);
    }

    [Fact]
    public async Task GenerateBorrowHistoryExcelAsync_WithData_CreatesValidExcelFile()
    {
        var filePath = GetTempFilePath("xlsx");
        var requests = CreateSampleBorrowRequests();

        await _sut.GenerateBorrowHistoryExcelAsync(requests, filePath);

        Assert.True(File.Exists(filePath));
        Assert.True(new FileInfo(filePath).Length > 0);

        using var wb = new XLWorkbook(filePath);
        Assert.Single(wb.Worksheets);
        var ws = wb.Worksheet("سجل الاستعارات");
        Assert.True(ws.RightToLeft);
        Assert.Equal("#", ws.Cell(1, 1).GetString());
        Assert.Equal("رقم المصدر", ws.Cell(1, 2).GetString());
        Assert.Equal("المستعير", ws.Cell(1, 3).GetString());
        Assert.Equal(1, ws.Cell(2, 1).GetValue<int>());
        Assert.Equal("SRC-001", ws.Cell(2, 2).GetString());
        Assert.Equal("د. خالد السعيد", ws.Cell(2, 3).GetString());
    }

    [Fact]
    public async Task GenerateLowActivityAlertReportPdfAsync_WithData_CreatesValidPdfFile()
    {
        var filePath = GetTempFilePath("pdf");
        var sources = CreateSampleSources();

        await _sut.GenerateLowActivityAlertReportPdfAsync(sources, filePath);

        Assert.True(File.Exists(filePath));
        Assert.True(new FileInfo(filePath).Length > 0);
    }

    [Fact]
    public async Task GenerateLowActivityAlertReportExcelAsync_WithData_CreatesValidExcelFile()
    {
        var filePath = GetTempFilePath("xlsx");
        var sources = CreateSampleSources();

        await _sut.GenerateLowActivityAlertReportExcelAsync(sources, filePath);

        Assert.True(File.Exists(filePath));
        Assert.True(new FileInfo(filePath).Length > 0);

        using var wb = new XLWorkbook(filePath);
        Assert.Single(wb.Worksheets);
        var ws = wb.Worksheet("تنبيهات انخفاض النشاط");
        Assert.True(ws.RightToLeft);
        Assert.Equal("#", ws.Cell(1, 1).GetString());
        Assert.Equal("رقم المصدر", ws.Cell(1, 2).GetString());
        Assert.Equal(1, ws.Cell(2, 1).GetValue<int>());
        Assert.Equal("SRC-001", ws.Cell(2, 2).GetString());
    }

    [Fact]
    public async Task GenerateLocationsReportPdfAsync_WithData_CreatesValidPdfFile()
    {
        var filePath = GetTempFilePath("pdf");
        var locations = new List<Location>
        {
            new Location { LocationName = "موقع 1", LocationType = "مختبر", Building = "مبنى أ", Room = "101", ResponsiblePerson = "أحمد" }
        };

        await _sut.GenerateLocationsReportPdfAsync(locations, filePath);

        Assert.True(File.Exists(filePath));
        Assert.True(new FileInfo(filePath).Length > 0);
    }

    [Fact]
    public async Task GenerateLocationsReportExcelAsync_WithData_CreatesValidExcelFile()
    {
        var filePath = GetTempFilePath("xlsx");
        var locations = new List<Location>
        {
            new Location { LocationName = "مختبر الأبحاث", LocationType = "مختبر", Building = "مبنى أ", Room = "101", ResponsiblePerson = "أحمد" },
            new Location { LocationName = "المستودع الرئيسي", LocationType = "مستودع", Building = "مبنى ب", Room = "002", ResponsiblePerson = "سارة" }
        };

        await _sut.GenerateLocationsReportExcelAsync(locations, filePath);

        Assert.True(File.Exists(filePath));
        Assert.True(new FileInfo(filePath).Length > 0);

        using var wb = new XLWorkbook(filePath);
        Assert.Single(wb.Worksheets);
        var ws = wb.Worksheet("المواقع والمخازن");
        Assert.True(ws.RightToLeft);
        Assert.Equal("#", ws.Cell(1, 1).GetString());
        Assert.Equal("اسم الموقع", ws.Cell(1, 2).GetString());
        Assert.Equal(1, ws.Cell(2, 1).GetValue<int>());
        Assert.Equal("مختبر الأبحاث", ws.Cell(2, 2).GetString());
        Assert.Equal(2, ws.Cell(3, 1).GetValue<int>());
        Assert.Equal("المستودع الرئيسي", ws.Cell(3, 2).GetString());
    }

    [Fact]
    public async Task GenerateGeneralReportPdfAsync_WithData_CreatesValidPdfFile()
    {
        var filePath = GetTempFilePath("pdf");
        var sources = CreateSampleSources();
        var requests = CreateSampleBorrowRequests();

        await _sut.GenerateGeneralReportPdfAsync(sources, requests, sources, sources, filePath);

        Assert.True(File.Exists(filePath));
        Assert.True(new FileInfo(filePath).Length > 0);
    }

    [Fact]
    public async Task GenerateGeneralReportExcelAsync_WithData_CreatesFourWorksheetsWithValidData()
    {
        var filePath = GetTempFilePath("xlsx");
        var sources = CreateSampleSources();
        var requests = CreateSampleBorrowRequests();

        await _sut.GenerateGeneralReportExcelAsync(sources, requests, sources, sources, filePath);

        Assert.True(File.Exists(filePath));
        Assert.True(new FileInfo(filePath).Length > 0);

        using var wb = new XLWorkbook(filePath);
        Assert.Equal(4, wb.Worksheets.Count);

        var ws1 = wb.Worksheet("جرد المصادر");
        Assert.NotNull(ws1);
        Assert.True(ws1.RightToLeft);
        Assert.Equal("#", ws1.Cell(1, 1).GetString());
        Assert.Equal(1, ws1.Cell(2, 1).GetValue<int>());
        Assert.Equal("SRC-001", ws1.Cell(2, 2).GetString());

        var ws2 = wb.Worksheet("سجل الاستعارات");
        Assert.NotNull(ws2);
        Assert.True(ws2.RightToLeft);
        Assert.Equal("#", ws2.Cell(1, 1).GetString());
        Assert.Equal(1, ws2.Cell(2, 1).GetValue<int>());
        Assert.Equal("SRC-001", ws2.Cell(2, 2).GetString());

        var ws3 = wb.Worksheet("المصادر منخفضة النشاط");
        Assert.NotNull(ws3);
        Assert.True(ws3.RightToLeft);
        Assert.Equal("#", ws3.Cell(1, 1).GetString());
        Assert.Equal(1, ws3.Cell(2, 1).GetValue<int>());
        Assert.Equal("SRC-001", ws3.Cell(2, 2).GetString());

        var ws4 = wb.Worksheet("تنبيهات انخفاض النشاط");
        Assert.NotNull(ws4);
        Assert.True(ws4.RightToLeft);
        Assert.Equal("#", ws4.Cell(1, 1).GetString());
        Assert.Equal(1, ws4.Cell(2, 1).GetValue<int>());
        Assert.Equal("SRC-001", ws4.Cell(2, 2).GetString());
    }

    [Fact]
    public async Task GenerateUsersReportPdfAsync_WithData_CreatesValidPdfFile()
    {
        var filePath = GetTempFilePath("pdf");
        var users = CreateSampleUsers();

        await _sut.GenerateUsersReportPdfAsync(users, filePath);

        Assert.True(File.Exists(filePath));
        Assert.True(new FileInfo(filePath).Length > 0);
    }

    [Fact]
    public async Task GenerateUsersReportExcelAsync_WithData_CreatesValidExcelFile()
    {
        var filePath = GetTempFilePath("xlsx");
        var users = CreateSampleUsers();

        await _sut.GenerateUsersReportExcelAsync(users, filePath);

        Assert.True(File.Exists(filePath));
        Assert.True(new FileInfo(filePath).Length > 0);

        using var wb = new XLWorkbook(filePath);
        Assert.Single(wb.Worksheets);
        var ws = wb.Worksheet("المستخدمين والكوادر");
        Assert.True(ws.RightToLeft);
        Assert.Equal("الاسم الكامل", ws.Cell(1, 2).GetString());
        Assert.Equal("مدير المنظومة", ws.Cell(2, 2).GetString());
        Assert.Equal("طبيعي", ws.Cell(2, 7).GetString());
        Assert.Equal("مستخدم مقفل", ws.Cell(3, 2).GetString());
        Assert.Equal("مقفل مؤقتاً", ws.Cell(3, 7).GetString());
    }

    [Fact]
    public async Task GenerateAuditLogsPdfAsync_WithData_CreatesValidPdfFile()
    {
        var filePath = GetTempFilePath("pdf");
        var logs = CreateSampleAuditLogs();

        await _sut.GenerateAuditLogsPdfAsync(logs, filePath);

        Assert.True(File.Exists(filePath));
        Assert.True(new FileInfo(filePath).Length > 0);
    }

    [Fact]
    public async Task GenerateAuditLogsExcelAsync_WithData_CreatesValidExcelFile()
    {
        var filePath = GetTempFilePath("xlsx");
        var logs = CreateSampleAuditLogs();

        await _sut.GenerateAuditLogsExcelAsync(logs, filePath);

        Assert.True(File.Exists(filePath));
        Assert.True(new FileInfo(filePath).Length > 0);

        using var wb = new XLWorkbook(filePath);
        Assert.Single(wb.Worksheets);
        var ws = wb.Worksheet("سجل التدقيق والنشاطات");
        Assert.True(ws.RightToLeft);
        Assert.Equal("المستخدم", ws.Cell(1, 2).GetString());
        Assert.Equal("أحمد محمد", ws.Cell(2, 2).GetString());
        Assert.Equal("إضافة مصدر جديد", ws.Cell(2, 3).GetString());
        Assert.Equal("عملية تلقائية", ws.Cell(3, 2).GetString());
        Assert.Equal("نسخ احتياطي تلقائي", ws.Cell(3, 3).GetString());
    }

    #endregion

    #region 2. Null Input Safety Tests (Fixed Excel Methods & PDF Methods)

    [Fact]
    public async Task GenerateInventoryReportExcelAsync_WithNullSources_CreatesHeaderOnlyWorkbookWithoutException()
    {
        var filePath = GetTempFilePath("xlsx");

        await _sut.GenerateInventoryReportExcelAsync(null!, filePath, "جرد فارغ");

        Assert.True(File.Exists(filePath));
        using var wb = new XLWorkbook(filePath);
        var ws = wb.Worksheet("جرد فارغ");
        Assert.Equal("#", ws.Cell(1, 1).GetString());
        Assert.Equal("رقم المصدر", ws.Cell(1, 2).GetString());
        Assert.True(ws.Cell(2, 1).IsEmpty());
    }

    [Fact]
    public async Task GenerateBorrowHistoryExcelAsync_WithNullRequests_CreatesHeaderOnlyWorkbookWithoutException()
    {
        var filePath = GetTempFilePath("xlsx");

        await _sut.GenerateBorrowHistoryExcelAsync(null!, filePath);

        Assert.True(File.Exists(filePath));
        using var wb = new XLWorkbook(filePath);
        var ws = wb.Worksheet("سجل الاستعارات");
        Assert.Equal("#", ws.Cell(1, 1).GetString());
        Assert.Equal("رقم المصدر", ws.Cell(1, 2).GetString());
        Assert.True(ws.Cell(2, 1).IsEmpty());
    }

    [Fact]
    public async Task GenerateLowActivityAlertReportExcelAsync_WithNullSources_CreatesHeaderOnlyWorkbookWithoutException()
    {
        var filePath = GetTempFilePath("xlsx");

        await _sut.GenerateLowActivityAlertReportExcelAsync(null!, filePath);

        Assert.True(File.Exists(filePath));
        using var wb = new XLWorkbook(filePath);
        var ws = wb.Worksheet("تنبيهات انخفاض النشاط");
        Assert.Equal("#", ws.Cell(1, 1).GetString());
        Assert.Equal("رقم المصدر", ws.Cell(1, 2).GetString());
        Assert.True(ws.Cell(2, 1).IsEmpty());
    }

    [Fact]
    public async Task GenerateLocationsReportExcelAsync_WithNullLocations_CreatesHeaderOnlyWorkbookWithoutException()
    {
        var filePath = GetTempFilePath("xlsx");

        await _sut.GenerateLocationsReportExcelAsync(null!, filePath);

        Assert.True(File.Exists(filePath));
        using var wb = new XLWorkbook(filePath);
        var ws = wb.Worksheet("المواقع والمخازن");
        Assert.Equal("#", ws.Cell(1, 1).GetString());
        Assert.Equal("اسم الموقع", ws.Cell(1, 2).GetString());
        Assert.True(ws.Cell(2, 1).IsEmpty());
    }

    [Fact]
    public async Task GenerateGeneralReportExcelAsync_WithNullDatasets_CreatesFourHeaderOnlyWorksheetsWithoutException()
    {
        var filePath = GetTempFilePath("xlsx");

        await _sut.GenerateGeneralReportExcelAsync(null!, null!, null!, null!, filePath);

        Assert.True(File.Exists(filePath));
        using var wb = new XLWorkbook(filePath);
        Assert.Equal(4, wb.Worksheets.Count);

        foreach (var ws in wb.Worksheets)
        {
            Assert.Equal("#", ws.Cell(1, 1).GetString());
            Assert.True(ws.Cell(2, 1).IsEmpty());
        }
    }

    [Fact]
    public async Task GenerateUsersReportExcelAsync_WithNullUsers_CreatesHeaderOnlyWorkbookWithoutException()
    {
        var filePath = GetTempFilePath("xlsx");

        await _sut.GenerateUsersReportExcelAsync(null!, filePath);

        Assert.True(File.Exists(filePath));
        using var wb = new XLWorkbook(filePath);
        var ws = wb.Worksheet("المستخدمين والكوادر");
        Assert.Equal("#", ws.Cell(1, 1).GetString());
        Assert.True(ws.Cell(2, 1).IsEmpty());
    }

    [Fact]
    public async Task GenerateAuditLogsExcelAsync_WithNullLogs_CreatesHeaderOnlyWorkbookWithoutException()
    {
        var filePath = GetTempFilePath("xlsx");

        await _sut.GenerateAuditLogsExcelAsync(null!, filePath);

        Assert.True(File.Exists(filePath));
        using var wb = new XLWorkbook(filePath);
        var ws = wb.Worksheet("سجل التدقيق والنشاطات");
        Assert.Equal("#", ws.Cell(1, 1).GetString());
        Assert.True(ws.Cell(2, 1).IsEmpty());
    }

    [Fact]
    public async Task PdfMethods_WithNullInputs_CreateValidPdfsWithoutException()
    {
        var pathInv = GetTempFilePath("pdf");
        var pathBor = GetTempFilePath("pdf");
        var pathCal = GetTempFilePath("pdf");
        var pathGen = GetTempFilePath("pdf");
        var pathUsr = GetTempFilePath("pdf");
        var pathAud = GetTempFilePath("pdf");

        await _sut.GenerateInventoryReportPdfAsync(null!, pathInv, "تقرير");
        await _sut.GenerateBorrowHistoryPdfAsync(null!, pathBor);
        await _sut.GenerateLowActivityAlertReportPdfAsync(null!, pathCal);
        await _sut.GenerateGeneralReportPdfAsync(null!, null!, null!, null!, pathGen);
        await _sut.GenerateUsersReportPdfAsync(null!, pathUsr);
        await _sut.GenerateAuditLogsPdfAsync(null!, pathAud);

        Assert.True(File.Exists(pathInv) && new FileInfo(pathInv).Length > 0);
        Assert.True(File.Exists(pathBor) && new FileInfo(pathBor).Length > 0);
        Assert.True(File.Exists(pathCal) && new FileInfo(pathCal).Length > 0);
        Assert.True(File.Exists(pathGen) && new FileInfo(pathGen).Length > 0);
        Assert.True(File.Exists(pathUsr) && new FileInfo(pathUsr).Length > 0);
        Assert.True(File.Exists(pathAud) && new FileInfo(pathAud).Length > 0);
    }

    #endregion

    #region 3. Empty Collections Tests (Enumerable.Empty)

    [Fact]
    public async Task PdfMethods_WithEmptyCollections_GenerateValidPdfFiles()
    {
        var pathInv = GetTempFilePath("pdf");
        var pathBor = GetTempFilePath("pdf");
        var pathCal = GetTempFilePath("pdf");
        var pathGen = GetTempFilePath("pdf");
        var pathUsr = GetTempFilePath("pdf");
        var pathAud = GetTempFilePath("pdf");

        await _sut.GenerateInventoryReportPdfAsync(Enumerable.Empty<Source>(), pathInv, "فارغ");
        await _sut.GenerateBorrowHistoryPdfAsync(Enumerable.Empty<BorrowRequest>(), pathBor);
        await _sut.GenerateLowActivityAlertReportPdfAsync(Enumerable.Empty<Source>(), pathCal);
        await _sut.GenerateGeneralReportPdfAsync(Enumerable.Empty<Source>(), Enumerable.Empty<BorrowRequest>(), Enumerable.Empty<Source>(), Enumerable.Empty<Source>(), pathGen);
        await _sut.GenerateUsersReportPdfAsync(Enumerable.Empty<User>(), pathUsr);
        await _sut.GenerateAuditLogsPdfAsync(Enumerable.Empty<AuditLog>(), pathAud);

        Assert.True(File.Exists(pathInv) && new FileInfo(pathInv).Length > 0);
        Assert.True(File.Exists(pathBor) && new FileInfo(pathBor).Length > 0);
        Assert.True(File.Exists(pathCal) && new FileInfo(pathCal).Length > 0);
        Assert.True(File.Exists(pathGen) && new FileInfo(pathGen).Length > 0);
        Assert.True(File.Exists(pathUsr) && new FileInfo(pathUsr).Length > 0);
        Assert.True(File.Exists(pathAud) && new FileInfo(pathAud).Length > 0);
    }

    #endregion

    #region 4. Null Navigation Properties and Fallback Handling

    [Fact]
    public async Task ExportMethods_WithNullNavigationProperties_HandlesFallbacksGracefully()
    {
        // Source with null Location, Radioisotope, Unit, AddedBy
        var bareSource = new Source
        {
            SourceCode = "SRC-BARE",
            Location = null,
            Radioisotope = null,
            InitialActivityUnit = null,
            CurrentActivityUnit = null,
            AddedBy = null,
            Status = "InUse"
        };

        // BorrowRequest with null Source, BorrowerUser, BorrowerName, Purpose, AddedBy
        var bareRequest = new BorrowRequest
        {
            Source = null,
            BorrowerUser = null,
            BorrowerName = null!,
            Purpose = null!,
            AddedBy = null,
            Status = "Pending"
        };

        // User with null Role, Email, LastLoginDate, LockoutEnd
        var bareUser = new User
        {
            FullName = "مستخدم بسيط",
            Username = "simple",
            Role = null,
            Email = null,
            LastLoginDate = null,
            LockoutEnd = null
        };

        // AuditLog with null User, TableName, Details
        var bareLog = new AuditLog
        {
            User = null,
            TableName = null,
            Details = null,
            Action = "Login"
        };

        var excelPath = GetTempFilePath("xlsx");
        var pdfPath = GetTempFilePath("pdf");

        // Excel tests with bare objects
        await _sut.GenerateInventoryReportExcelAsync(new[] { bareSource }, excelPath, "جرد");
        using (var wb = new XLWorkbook(excelPath))
        {
            var ws = wb.Worksheet("جرد");
            Assert.Equal(1, ws.Cell(2, 1).GetValue<int>());
            Assert.Equal("غير محدد", ws.Cell(2, 5).GetString()); // Location fallback
            Assert.Equal("غير معروف", ws.Cell(2, 7).GetString()); // AddedBy fallback
        }

        var excelBorrowPath = GetTempFilePath("xlsx");
        await _sut.GenerateBorrowHistoryExcelAsync(new[] { bareRequest }, excelBorrowPath);
        using (var wb = new XLWorkbook(excelBorrowPath))
        {
            var ws = wb.Worksheet("سجل الاستعارات");
            Assert.Equal(1, ws.Cell(2, 1).GetValue<int>());
            Assert.Equal("-", ws.Cell(2, 2).GetString()); // Source fallback
            Assert.Equal("-", ws.Cell(2, 3).GetString()); // Borrower fallback
            Assert.Equal("-", ws.Cell(2, 4).GetString()); // Purpose fallback
            Assert.Equal("غير معروف", ws.Cell(2, 7).GetString()); // AddedBy fallback
        }

        var excelUserPath = GetTempFilePath("xlsx");
        await _sut.GenerateUsersReportExcelAsync(new[] { bareUser }, excelUserPath);
        using (var wb = new XLWorkbook(excelUserPath))
        {
            var ws = wb.Worksheet("المستخدمين والكوادر");
            Assert.Equal("-", ws.Cell(2, 4).GetString()); // Role fallback
            Assert.Equal("-", ws.Cell(2, 5).GetString()); // Email fallback
            Assert.Equal("طبيعي", ws.Cell(2, 7).GetString()); // Lockout status
            Assert.Equal("لم يسجل بعد", ws.Cell(2, 8).GetString()); // LastLogin fallback
        }

        var excelLogPath = GetTempFilePath("xlsx");
        await _sut.GenerateAuditLogsExcelAsync(new[] { bareLog }, excelLogPath);
        using (var wb = new XLWorkbook(excelLogPath))
        {
            var ws = wb.Worksheet("سجل التدقيق والنشاطات");
            Assert.Equal("عملية تلقائية", ws.Cell(2, 2).GetString()); // User fallback (UserId == null -> automated)
            Assert.Equal("-", ws.Cell(2, 4).GetString()); // Table fallback
            Assert.Equal("-", ws.Cell(2, 5).GetString()); // Details fallback
        }

        // PDF tests with bare objects
        await _sut.GenerateInventoryReportPdfAsync(new[] { bareSource }, pdfPath, "جرد");
        Assert.True(File.Exists(pdfPath) && new FileInfo(pdfPath).Length > 0);

        var pdfBorrowPath = GetTempFilePath("pdf");
        await _sut.GenerateBorrowHistoryPdfAsync(new[] { bareRequest }, pdfBorrowPath);
        Assert.True(File.Exists(pdfBorrowPath) && new FileInfo(pdfBorrowPath).Length > 0);

        var pdfUserPath = GetTempFilePath("pdf");
        await _sut.GenerateUsersReportPdfAsync(new[] { bareUser }, pdfUserPath);
        Assert.True(File.Exists(pdfUserPath) && new FileInfo(pdfUserPath).Length > 0);

        var pdfLogPath = GetTempFilePath("pdf");
        await _sut.GenerateAuditLogsPdfAsync(new[] { bareLog }, pdfLogPath);
        Assert.True(File.Exists(pdfLogPath) && new FileInfo(pdfLogPath).Length > 0);
    }

    [Theory]
    [InlineData("تقرير مصادر الموقع: مختبر الأبحاث النووية والتحاليل المتقدمة", 31)]
    [InlineData("Sheet:with*invalid/chars?and[brackets]", 31)]
    [InlineData("", 5)]
    [InlineData("   ", 5)]
    [InlineData(null, 5)]
    [InlineData("'SingleQuotedSheet'", 17)]
    public void SanitizeSheetName_AlwaysReturnsValidExcelSheetName(string? input, int maxExpectedLength)
    {
        // Act
        var result = ReportingService.SanitizeSheetName(input);

        // Assert
        Assert.False(string.IsNullOrWhiteSpace(result));
        Assert.True(result.Length <= 31);
        Assert.True(result.Length <= maxExpectedLength);
        Assert.DoesNotContain('\\', result);
        Assert.DoesNotContain('/', result);
        Assert.DoesNotContain('?', result);
        Assert.DoesNotContain('*', result);
        Assert.DoesNotContain('[', result);
        Assert.DoesNotContain(']', result);
        Assert.DoesNotContain(':', result);
    }

    [Fact]
    public async Task GenerateInventoryReportExcelAsync_WithLongSheetName_SucceedsWithoutClosedXmlException()
    {
        // Arrange
        var sources = CreateSampleSources();
        var filePath = GetTempFilePath("xlsx");
        var veryLongTitle = "تقرير مصادر الموقع: مختبر الأبحاث النووية والتحاليل الطبية المتقدمة التابع لقسم الفيزياء";

        // Act
        await _sut.GenerateInventoryReportExcelAsync(sources, filePath, veryLongTitle);

        // Assert
        Assert.True(File.Exists(filePath));
        using var wb = new XLWorkbook(filePath);
        Assert.Single(wb.Worksheets);
        var ws = wb.Worksheets.First();
        Assert.True(ws.Name.Length <= 31);
    }

    [Fact]
    public async Task GenerateFailedLeakTestsReportExcelAsync_GeneratesValidExcelFile()
    {
        // Arrange
        var source = new Source
        {
            SourceCode = "SRC-FAIL-01",
            Status = "InUse",
            Radioisotope = new Radioisotope { Symbol = "Cs-137" },
            Location = new Location { LocationName = "مستودع 1" }
        };
        var records = new List<LeakTestRecord>
        {
            new()
            {
                Source = source,
                TestDate = DateTime.Today.AddDays(-3),
                Result = "Fail",
                Notes = "تسرب إشعاعي ملحوظ"
            }
        };
        var filePath = GetTempFilePath("xlsx");

        // Act
        await _sut.GenerateFailedLeakTestsReportExcelAsync(records, filePath);

        // Assert
        Assert.True(File.Exists(filePath));
        var fileInfo = new FileInfo(filePath);
        Assert.True(fileInfo.Length > 0);

        using var wb = new XLWorkbook(filePath);
        Assert.Single(wb.Worksheets);
        var ws = wb.Worksheets.First();
        Assert.Equal("SRC-FAIL-01", ws.Cell(5, 2).GetString());
        Assert.Equal("تسرب إشعاعي ملحوظ", ws.Cell(5, 7).GetString());
    }

    [Fact]
    public async Task GenerateFailedLeakTestsReportPdfAsync_GeneratesValidPdfFile()
    {
        // Arrange
        var source = new Source
        {
            SourceCode = "SRC-FAIL-02",
            Status = "Storage",
            Radioisotope = new Radioisotope { Symbol = "Co-60" },
            Location = new Location { LocationName = "مخزن 2" }
        };
        var records = new List<LeakTestRecord>
        {
            new()
            {
                Source = source,
                TestDate = DateTime.Today.AddDays(-1),
                Result = "Fail",
                Notes = "فحص دوري فاشل"
            }
        };
        var filePath = GetTempFilePath("pdf");

        // Act
        await _sut.GenerateFailedLeakTestsReportPdfAsync(records, filePath);

        // Assert
        Assert.True(File.Exists(filePath));
        var fileInfo = new FileInfo(filePath);
        Assert.True(fileInfo.Length > 0);
    }

    #endregion

    #region Round 98 Tests — Neutron Inventory Calibration Date & Audit Log Attribution

    [Fact]
    public async Task GenerateNeutronInventoryReportExcelAsync_UsesEmissionCalibrationDate_NotCalibrationDate()
    {
        // Arrange
        var emissionDate = new DateTime(2025, 6, 15);
        var generalCalibrationDate = new DateTime(2023, 1, 10);
        var sources = new List<NeutronSource>
        {
            new()
            {
                SourceCode = "NS-CAL-01",
                NeutronSourceType = new NeutronSourceType { Code = "Am-241/Be" },
                CalibratedEmissionRate = 5.4e6,
                RelativeExpandedUncertaintyPercent = 3.2,
                EmissionCalibrationDate = emissionDate,
                CalibrationDate = generalCalibrationDate,
                Status = "InUse"
            }
        };
        var filePath = GetTempFilePath("xlsx");

        // Act
        await _sut.GenerateNeutronInventoryReportExcelAsync(sources, filePath);

        // Assert
        Assert.True(File.Exists(filePath));
        using var wb = new XLWorkbook(filePath);
        var ws = wb.Worksheets.First();

        // Header check
        var header = ws.Cell(1, 8).GetString();
        Assert.Equal("تاريخ معايرة الانبعاث", header);

        // Value check: MUST match EmissionCalibrationDate, NOT CalibrationDate
        var value = ws.Cell(2, 8).GetString();
        Assert.Equal(emissionDate.ToString("yyyy-MM-dd"), value);
        Assert.NotEqual(generalCalibrationDate.ToString("yyyy-MM-dd"), value);
    }

    [Fact]
    public async Task GenerateNeutronInventoryReportExcelAsync_WhenEmissionCalibrationDateNull_DisplaysNotRecorded()
    {
        // Arrange
        var sources = new List<NeutronSource>
        {
            new()
            {
                SourceCode = "NS-CAL-NULL",
                NeutronSourceType = new NeutronSourceType { Code = "Cf-252" },
                CalibratedEmissionRate = 1.2e7,
                EmissionCalibrationDate = null,
                CalibrationDate = new DateTime(2024, 5, 20),
                Status = "Storage"
            }
        };
        var filePath = GetTempFilePath("xlsx");

        // Act
        await _sut.GenerateNeutronInventoryReportExcelAsync(sources, filePath);

        // Assert
        Assert.True(File.Exists(filePath));
        using var wb = new XLWorkbook(filePath);
        var ws = wb.Worksheets.First();
        var value = ws.Cell(2, 8).GetString();
        Assert.Equal("غير مسجّل", value);
    }

    [Fact]
    public async Task GenerateAuditLogsExcelAsync_FormatsActorAttribution_ForThreeCases()
    {
        // Arrange: 3 cases:
        // 1. Automated process without user (UserId == null)
        // 2. Deleted user (UserId != null, User == null)
        // 3. Normal user (UserId != null, User != null)
        var normalUser = new User { Id = Guid.NewGuid(), FullName = "د. طارق محمود" };
        var logs = new List<AuditLog>
        {
            new()
            {
                UserId = null,
                User = null,
                Action = "SystemAutoCheck",
                TableName = "Sources",
                Details = "فحص دوري آلي",
                ActionDate = DateTime.Now.AddMinutes(-30)
            },
            new()
            {
                UserId = Guid.NewGuid(),
                User = null,
                Action = "Delete",
                TableName = "Locations",
                Details = "حذف موقع",
                ActionDate = DateTime.Now.AddMinutes(-15)
            },
            new()
            {
                UserId = normalUser.Id,
                User = normalUser,
                Action = "Create",
                TableName = "NeutronSources",
                Details = "إضافة مصدر نيتروني جديد",
                ActionDate = DateTime.Now
            }
        };
        var filePath = GetTempFilePath("xlsx");

        // Act
        await _sut.GenerateAuditLogsExcelAsync(logs, filePath);

        // Assert
        Assert.True(File.Exists(filePath));
        using var wb = new XLWorkbook(filePath);
        var ws = wb.Worksheets.First();

        // Row 2: Automated
        Assert.Equal("عملية تلقائية", ws.Cell(2, 2).GetString());

        // Row 3: Deleted user
        Assert.Equal("مستخدم محذوف", ws.Cell(3, 2).GetString());

        // Row 4: Normal user
        Assert.Equal("د. طارق محمود", ws.Cell(4, 2).GetString());
    }

    [Fact]
    public async Task GenerateAuditLogsPdfAndExcelAsync_BothSucceedForIdenticalDataset()
    {
        // Arrange
        var user = new User { Id = Guid.NewGuid(), FullName = "مهندس سليم" };
        var logs = new List<AuditLog>
        {
            new() { UserId = null, User = null, Action = "AutoBackup", ActionDate = DateTime.Now.AddHours(-1) },
            new() { UserId = Guid.NewGuid(), User = null, Action = "Purge", ActionDate = DateTime.Now.AddMinutes(-30) },
            new() { UserId = user.Id, User = user, Action = "Update", ActionDate = DateTime.Now }
        };
        var excelPath = GetTempFilePath("xlsx");
        var pdfPath = GetTempFilePath("pdf");

        // Act
        await _sut.GenerateAuditLogsExcelAsync(logs, excelPath);
        await _sut.GenerateAuditLogsPdfAsync(logs, pdfPath);

        // Assert
        Assert.True(File.Exists(excelPath));
        Assert.True(new FileInfo(excelPath).Length > 0);

        Assert.True(File.Exists(pdfPath));
        Assert.True(new FileInfo(pdfPath).Length > 0);

        // Validate Excel attribution
        using var wb = new XLWorkbook(excelPath);
        var ws = wb.Worksheets.First();
        Assert.Equal("عملية تلقائية", ws.Cell(2, 2).GetString());
        Assert.Equal("مستخدم محذوف", ws.Cell(3, 2).GetString());
        Assert.Equal("مهندس سليم", ws.Cell(4, 2).GetString());
    }

    #endregion
}
