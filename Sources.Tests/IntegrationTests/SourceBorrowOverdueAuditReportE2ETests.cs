using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Sources.Data;
using Sources.Models;
using Sources.Services;
using Sources.Tests.Fakes;
using Sources.Tests.Fixtures;
using Sources.Tests.Helpers;
using Xunit;

namespace Sources.Tests.IntegrationTests;

/// <summary>
/// اختبار تكاملي شامل (End-to-End) يغطي دورة حياة المصدر والاستعارة والتأخر والتدقيق والتقارير:
/// Source -> Borrow -> Overdue -> Audit -> Report
/// </summary>
public class SourceBorrowOverdueAuditReportE2ETests : IClassFixture<SqliteInMemoryFixture>, IDisposable
{
    private readonly SqliteInMemoryFixture _fixture;
    private readonly FakeUserService _fakeUserService;
    private readonly AuditService _auditService;
    private readonly DecayCalculationService _decayService;
    private readonly SourceService _sourceService;
    private readonly BorrowService _borrowService;
    private readonly ReportingService _reportingService;

    private Radioisotope _testIsotope = null!;
    private ActivityUnit _testUnit = null!;
    private Location _testLocation = null!;
    private User _testUser = null!;

    public SourceBorrowOverdueAuditReportE2ETests(SqliteInMemoryFixture fixture)
    {
        _fixture = fixture;
        _fixture.ResetDatabase();

        // إعداد الدور والمستخدم
        var role = new Role
        {
            Id = Guid.NewGuid(),
            RoleName = "أمين العهدة التجريبي",
            Permissions = "Borrowing,Sources,Audit,Reports"
        };

        _testUser = new User
        {
            Id = Guid.NewGuid(),
            FullName = "أمين العهدة التجريبي",
            Username = "e2e_admin",
            PasswordHash = "hashed_pass",
            IsActive = true,
            RoleId = role.Id
        };

        _fakeUserService = new FakeUserService(_testUser);

        // استخدام خدمة التدقيق الحقيقية المتصلة بقاعدة البيانات
        _auditService = new AuditService(_fixture.ContextFactory, _fakeUserService);
        _decayService = new DecayCalculationService();
        _sourceService = new SourceService(_fixture.ContextFactory, _decayService, _auditService, _fakeUserService);
        _borrowService = new BorrowService(_fixture.ContextFactory, _auditService, _fakeUserService);
        _reportingService = new ReportingService();

        SeedMasterData(role);
    }

    public void Dispose()
    {
        _fixture.ResetDatabase();
    }

    private void SeedMasterData(Role role)
    {
        using var db = _fixture.CreateContext();

        _testIsotope = TestDataBuilder.CreateRadioisotope(symbol: "Cs-137", name: "Cesium-137", halfLife: 30.08, halfLifeUnit: "years");
        _testUnit = TestDataBuilder.CreateActivityUnit(name: "MegaBecquerel", symbol: "MBq", conversionToBq: 1_000_000.0);
        _testLocation = TestDataBuilder.CreateLocation(name: "مستودع النظائر المركزي", building: "مبنى الوقاية", room: "G-01");

        db.Roles.Add(role);
        db.Users.Add(_testUser);
        db.Radioisotopes.Add(_testIsotope);
        db.ActivityUnits.Add(_testUnit);
        db.Locations.Add(_testLocation);
        db.SaveChanges();
    }

    [Fact]
    public async Task CompletePipeline_Source_Borrow_Overdue_Audit_Report_ShouldSucceedEndToEnd()
    {
        // ─── المرحلة 1: إنشاء مصدر جديد عبر SourceService ───
        var source = TestDataBuilder.CreateSource(
            _testIsotope,
            _testUnit,
            _testLocation,
            sourceCode: "SRC-E2E-100",
            initialActivity: 500.0,
            status: "Storage");

        var (createSourceSuccess, createSourceMsg) = _sourceService.CreateSource(source);
        Assert.True(createSourceSuccess, $"فشل إنشاء المصدر: {createSourceMsg}");

        var storedSource = _sourceService.GetSourceById(source.Id);
        Assert.NotNull(storedSource);
        Assert.Equal("Storage", storedSource.Status);
        Assert.Equal(_testUser.FullName, storedSource.AddedBy);

        // التحقق من تسجيل تدقيق إنشاء المصدر
        var initialAuditLogs = _auditService.GetAuditLogs(pageSize: 10, actionFilter: "Create");
        Assert.Contains(initialAuditLogs, l => l.TableName == "Sources" && l.RecordId == source.Id);

        // ─── المرحلة 2: استعارة المصدر بتاريخ إرجاع متوقع في الماضي ───
        var borrowRequest = new BorrowRequest
        {
            Id = Guid.NewGuid(),
            SourceId = source.Id,
            BorrowerName = "د. طارق الأحمدي",
            Purpose = "إجراء تجارب المعايرة الدورية",
            ExpectedReturnDate = DateTime.Today.AddDays(-2) // تاريخ متأخر لمحاكاة التأخر بدون Mocking للوقت
        };

        var (borrowSuccess, borrowMsg) = _borrowService.CreateRequest(borrowRequest);
        Assert.True(borrowSuccess, $"فشل تسجيل الاستعارة: {borrowMsg}");

        // التحقق من تحديث حالة المصدر وحالة الطلب
        var sourceAfterBorrow = _sourceService.GetSourceById(source.Id);
        Assert.NotNull(sourceAfterBorrow);
        Assert.Equal("InUse", sourceAfterBorrow.Status);

        var requestsForSource = _borrowService.GetBySource(source.Id);
        Assert.Single(requestsForSource);
        Assert.Equal("Delivered", requestsForSource[0].Status);
        Assert.Equal(_testUser.FullName, requestsForSource[0].AddedBy);

        // التحقق من تسجيل تدقيق الاستعارة
        var afterBorrowAuditLogs = _auditService.GetAuditLogs(pageSize: 20);
        Assert.Contains(afterBorrowAuditLogs, l => l.TableName == "BorrowRequests" && l.Action == "Create" && l.RecordId == borrowRequest.Id);

        // ─── المرحلة 3: محاولة حذف المصدر أثناء الاستعارة النشطة (Delivered) ───
        var (deleteDeliveredSuccess, deleteDeliveredMsg) = _sourceService.DeleteSource(source.Id);
        Assert.False(deleteDeliveredSuccess, "يجب منع حذف المصدر طالما أن لديه استعارة نشطة بحالة Delivered.");
        Assert.Contains("استعارة نشطة", deleteDeliveredMsg);

        // ─── المرحلة 4: تشغيل فحص وتحديث التأخر (Overdue) ───
        _borrowService.CheckAndUpdateOverdue();

        var overdueRequests = _borrowService.GetOverdue();
        Assert.Single(overdueRequests);
        Assert.Equal(borrowRequest.Id, overdueRequests[0].Id);
        Assert.Equal("Overdue", overdueRequests[0].Status);
        Assert.Equal("متأخر", overdueRequests[0].ArabicStatus);

        // ─── المرحلة 5: محاولة حذف المصدر أثناء حالة Overdue ───
        var (deleteOverdueSuccess, deleteOverdueMsg) = _sourceService.DeleteSource(source.Id);
        Assert.False(deleteOverdueSuccess, "يجب استمرار منع حذف المصدر أثناء حالة Overdue.");
        Assert.Contains("استعارة نشطة", deleteOverdueMsg);

        // ─── المرحلة 6: التحقق من سجل التدقيق الكامل في قاعدة البيانات ───
        var allAuditLogs = _auditService.GetAuditLogs(pageSize: 50);
        Assert.True(allAuditLogs.Count >= 3, "يجب وجود 3 سجلات تدقيق على الأقل في دورة الحياة هذه.");

        // تأكيد وجود الأحداث الثلاثة المحددة:
        // 1. إنشاء المصدر
        var sourceCreateLog = allAuditLogs.FirstOrDefault(l => l.TableName == "Sources" && l.Action == "Create" && l.RecordId == source.Id);
        Assert.NotNull(sourceCreateLog);
        Assert.Equal(_testUser.Id, sourceCreateLog.UserId);

        // 2. إنشاء الاستعارة
        var borrowCreateLog = allAuditLogs.FirstOrDefault(l => l.TableName == "BorrowRequests" && l.Action == "Create" && l.RecordId == borrowRequest.Id);
        Assert.NotNull(borrowCreateLog);
        Assert.Equal(_testUser.Id, borrowCreateLog.UserId);

        // 3. تحديث النظام للطلبات المتأخرة
        var overdueSystemLog = allAuditLogs.FirstOrDefault(l => l.TableName == "BorrowRequests" && l.Action == "System");
        Assert.NotNull(overdueSystemLog);

        // تأكيد الحفظ المادي في جدول AuditLogs الفعلي
        using (var verifyDb = _fixture.CreateContext())
        {
            var physicalLogsCount = verifyDb.AuditLogs.Count();
            Assert.True(physicalLogsCount >= 3);
        }

        // ─── المرحلة 7: توليد وفحص تقرير Excel لسجل الاستعارات ───
        var allRequestsForReport = _borrowService.GetAll();
        var tempExcelPath = Path.Combine(Path.GetTempPath(), $"E2E_BorrowHistoryReport_{Guid.NewGuid():N}.xlsx");

        try
        {
            // توليد التقرير
            await _reportingService.GenerateBorrowHistoryExcelAsync(allRequestsForReport, tempExcelPath);

            // التأكد من وجود الملف وحجمه
            Assert.True(File.Exists(tempExcelPath), "ملف تقرير الإكسل لم يُنشأ على القرص.");
            var fileInfo = new FileInfo(tempExcelPath);
            Assert.True(fileInfo.Length > 0, "ملف تقرير الإكسل فارغ الحجم.");

            // فتح وفحص محتوى ملف الإكسل عبر ClosedXML
            using var workbook = new XLWorkbook(tempExcelPath);
            var worksheet = workbook.Worksheets.FirstOrDefault(w => w.Name == "سجل الاستعارات");
            Assert.NotNull(worksheet);

            // فحص صف العناوين (الصف الأول)
            Assert.Equal("رقم المصدر", worksheet.Cell(1, 1).GetString());
            Assert.Equal("المستعير", worksheet.Cell(1, 2).GetString());
            Assert.Equal("الغرض", worksheet.Cell(1, 3).GetString());
            Assert.Equal("تاريخ الإرجاع", worksheet.Cell(1, 4).GetString());
            Assert.Equal("الحالة", worksheet.Cell(1, 5).GetString());
            Assert.Equal("المسؤول", worksheet.Cell(1, 6).GetString());
            Assert.Equal("تاريخ الطلب", worksheet.Cell(1, 7).GetString());

            // فحص صف البيانات (الصف الثاني - يعكس الاستعارة المتأخرة)
            Assert.Equal("SRC-E2E-100", worksheet.Cell(2, 1).GetString());
            Assert.Equal("د. طارق الأحمدي", worksheet.Cell(2, 2).GetString());
            Assert.Equal("إجراء تجارب المعايرة الدورية", worksheet.Cell(2, 3).GetString());
            Assert.Equal(borrowRequest.ExpectedReturnDate.ToString("yyyy/MM/dd"), worksheet.Cell(2, 4).GetString());
            Assert.Equal("متأخر", worksheet.Cell(2, 5).GetString()); // التحقق من ترجمة حالة Overdue إلى متأخر
            Assert.Equal(_testUser.FullName, worksheet.Cell(2, 6).GetString());
        }
        finally
        {
            // ─── المرحلة 8: تنظيف الملفات المؤقتة ───
            if (File.Exists(tempExcelPath))
            {
                try
                {
                    File.Delete(tempExcelPath);
                }
                catch
                {
                    // تجاهل أخطاء الحذف المؤقت إن وجدت
                }
            }
        }
    }

    [Fact]
    public void CompleteLifecycle_SourceBorrowReturnDelete_ShouldAllowDeletionAfterReturn()
    {
        // ─── 1. إنشاء مصدر جديد ───
        var source = TestDataBuilder.CreateSource(
            _testIsotope,
            _testUnit,
            _testLocation,
            sourceCode: "SRC-E2E-200",
            status: "Storage");

        var (createOk, _) = _sourceService.CreateSource(source);
        Assert.True(createOk);

        // ─── 2. استعارة المصدر ───
        var borrowRequest = new BorrowRequest
        {
            Id = Guid.NewGuid(),
            SourceId = source.Id,
            BorrowerName = "م. سارة المهندس",
            Purpose = "فحص ميداني",
            ExpectedReturnDate = DateTime.Today.AddDays(7)
        };

        var (borrowOk, _) = _borrowService.CreateRequest(borrowRequest);
        Assert.True(borrowOk);
        Assert.Equal("InUse", _sourceService.GetSourceById(source.Id)!.Status);

        // ─── 3. التأكد من منع الحذف أثناء الاستعارة ───
        var (deleteBlocked, _) = _sourceService.DeleteSource(source.Id);
        Assert.False(deleteBlocked);

        // ─── 4. إرجاع المصدر عبر MarkReturned ───
        var returnDate = DateTime.Now;
        var (returnOk, returnMsg) = _borrowService.MarkReturned(borrowRequest.Id, _testUser.Id, returnDate, "تم الإرجاع بنجاح والتأكد من سلامة المصدر");
        Assert.True(returnOk, $"فشل إرجاع المصدر: {returnMsg}");

        // التحقق من عودة حالة المصدر إلى Storage وحالة الطلب إلى Returned
        var sourceAfterReturn = _sourceService.GetSourceById(source.Id);
        Assert.NotNull(sourceAfterReturn);
        Assert.Equal("Storage", sourceAfterReturn.Status);

        var requestAfterReturn = _borrowService.GetBySource(source.Id).FirstOrDefault(b => b.Id == borrowRequest.Id);
        Assert.NotNull(requestAfterReturn);
        Assert.Equal("Returned", requestAfterReturn.Status);
        Assert.Equal("تم الإرجاع", requestAfterReturn.ArabicStatus);

        // ─── 5. التحقق من نجاح الحذف الآن بعد انتهاء الاستعارة ───
        var (deleteAllowed, deleteMsg) = _sourceService.DeleteSource(source.Id);
        Assert.True(deleteAllowed, $"يجب السماح بحذف المصدر بعد إرجاعه: {deleteMsg}");

        // التأكد من أن المصدر لم يعد يظهر في الاستعلامات العادية
        Assert.Null(_sourceService.GetSourceById(source.Id));

        using (var db = _fixture.CreateContext())
        {
            var dbSource = db.Sources.IgnoreQueryFilters().FirstOrDefault(s => s.Id == source.Id);
            Assert.NotNull(dbSource);
            Assert.True(dbSource.IsDeleted, "يجب أن تكون علامة IsDeleted للمصدر true.");
        }

        // ─── 6. التحقق من سلسلة التدقيق للعملية الكاملة ───
        var auditLogs = _auditService.GetAuditLogs(pageSize: 20);
        Assert.Contains(auditLogs, l => l.TableName == "BorrowRequests" && l.Action == "Return" && l.RecordId == borrowRequest.Id);
        Assert.Contains(auditLogs, l => l.TableName == "Sources" && l.Action == "Delete" && l.RecordId == source.Id);
    }
}
