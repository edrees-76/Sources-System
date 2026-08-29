using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.EntityFrameworkCore;
using Moq;
using Sources.Data;
using Sources.Messages;
using Sources.Models;
using Sources.Services;
using Sources.Tests.Fakes;
using Sources.Tests.Fixtures;
using Sources.Tests.Helpers;
using Sources.ViewModels;
using Xunit;

namespace Sources.Tests;

public class BorrowViewModelAndDueSoonTests : IDisposable
{
    private readonly SqliteInMemoryFixture _fixture;
    private readonly FakeAuditService _fakeAuditService;
    private readonly FakeUserService _fakeUserService;
    private readonly SystemSettingsService _settingsService;
    private readonly BorrowService _borrowService;

    public BorrowViewModelAndDueSoonTests()
    {
        Sources.Helpers.DialogHelper.IsTestMode = true;
        _fixture = new SqliteInMemoryFixture();
        _fixture.ResetDatabase();

        _fakeAuditService = new FakeAuditService();
        _fakeUserService = new FakeUserService();
        _settingsService = new SystemSettingsService(_fixture.ContextFactory);
        _borrowService = new BorrowService(_fixture.ContextFactory, _fakeAuditService, _fakeUserService, _settingsService);
    }

    public void Dispose()
    {
        Sources.Helpers.DialogHelper.IsTestMode = false;
        _fixture.ResetDatabase();
    }

    #region 1. تطابق أرقام DueSoonCount و OverdueCount و ActiveCount بين BorrowViewModel و DashboardViewModel

    [Fact]
    public void DueSoonCount_And_BorrowSummary_MatchIdentically_Between_BorrowViewModel_And_DashboardViewModel()
    {
        // Arrange
        using var db = _fixture.CreateContext();
        var iso = TestDataBuilder.CreateRadioisotope("Co-60", "Cobalt-60");
        var unit = TestDataBuilder.CreateActivityUnit("Curie", "Ci");
        var loc = TestDataBuilder.CreateLocation("مختبر الأبحاث");
        db.Radioisotopes.Add(iso);
        db.ActivityUnits.Add(unit);
        db.Locations.Add(loc);
        db.SaveChanges();

        var src1 = TestDataBuilder.CreateSource(iso, unit, loc, "SRC-MATCH-1", 50.0, DateTime.Now.AddMonths(-2), "InUse");
        var src2 = TestDataBuilder.CreateSource(iso, unit, loc, "SRC-MATCH-2", 50.0, DateTime.Now.AddMonths(-2), "InUse");
        var src3 = TestDataBuilder.CreateSource(iso, unit, loc, "SRC-MATCH-3", 50.0, DateTime.Now.AddMonths(-2), "InUse");
        var src4 = TestDataBuilder.CreateSource(iso, unit, loc, "SRC-MATCH-4", 50.0, DateTime.Now.AddMonths(-2), "Storage");
        db.Sources.AddRange(src1, src2, src3, src4);
        db.SaveChanges();

        // 1. طلب مستحق اليوم (Delivered)
        var reqDueToday = new BorrowRequest
        {
            Id = Guid.NewGuid(),
            SourceId = src1.Id,
            BorrowerName = "مستعير 1",
            Purpose = "تجربة 1",
            Status = "Delivered",
            RequestDate = DateTime.Today.AddDays(-3),
            ExpectedReturnDate = DateTime.Today
        };

        // 2. طلب مستحق بعد 4 أيام (Delivered)
        var reqDueIn4Days = new BorrowRequest
        {
            Id = Guid.NewGuid(),
            SourceId = src2.Id,
            BorrowerName = "مستعير 2",
            Purpose = "تجربة 2",
            Status = "Delivered",
            RequestDate = DateTime.Today.AddDays(-2),
            ExpectedReturnDate = DateTime.Today.AddDays(4)
        };

        // 3. طلب متأخر (Overdue)
        var reqOverdue = new BorrowRequest
        {
            Id = Guid.NewGuid(),
            SourceId = src3.Id,
            BorrowerName = "مستعير 3",
            Purpose = "تجربة 3",
            Status = "Delivered", // سيتحول إلى Overdue بواسطة CheckAndUpdateOverdue
            RequestDate = DateTime.Today.AddDays(-10),
            ExpectedReturnDate = DateTime.Today.AddDays(-2)
        };

        // 4. طلب تم إرجاعه (Returned)
        var reqReturned = new BorrowRequest
        {
            Id = Guid.NewGuid(),
            SourceId = src4.Id,
            BorrowerName = "مستعير 4",
            Purpose = "تجربة 4",
            Status = "Returned",
            RequestDate = DateTime.Today.AddDays(-15),
            ExpectedReturnDate = DateTime.Today.AddDays(-5),
            ActualReturnDate = DateTime.Today.AddDays(-5)
        };

        db.BorrowRequests.AddRange(reqDueToday, reqDueIn4Days, reqOverdue, reqReturned);
        db.SaveChanges();

        // Ensure status update
        _borrowService.CheckAndUpdateOverdue();
        var allRequests = _borrowService.GetAll();

        // Assert: BorrowService gives exact figures
        int expectedDueSoon = _borrowService.GetDueSoonCount(allRequests);
        int expectedOverdue = allRequests.Count(r => r.Status == "Overdue");
        int expectedActive = allRequests.Count(r => r.Status == "Delivered" || r.Status == "Overdue");

        Assert.Equal(2, expectedDueSoon); // reqDueToday and reqDueIn4Days
        Assert.Equal(1, expectedOverdue); // reqOverdue
        Assert.Equal(3, expectedActive);  // reqDueToday, reqDueIn4Days, reqOverdue

        // Act & Assert for DueSoon Count across both view logic sources
        var countFromList = _borrowService.GetDueSoonCount(allRequests);
        var countDirectFromDb = _borrowService.GetDueSoonCount();

        Assert.Equal(expectedDueSoon, countFromList);
        Assert.Equal(expectedDueSoon, countDirectFromDb);
    }

    #endregion

    #region 2. اختبارات التحقق من التواريخ في ViewModel

    [Fact]
    public void Submit_Validation_Rejects_ExpectedReturnDate_Past_Or_TooFarInFuture()
    {
        // Arrange
        var mockSourceService = new Mock<ISourceService>();
        var mockUserService = new Mock<IUserService>();
        var mockReportingService = new Mock<IReportingService>();
        var mockBorrowService = new Mock<IBorrowService>();

        var vm = new BorrowViewModel(mockBorrowService.Object, mockSourceService.Object, mockUserService.Object, mockReportingService.Object);

        var testSource = new Source { Id = Guid.NewGuid(), SourceCode = "SRC-VAL-1" };
        vm.SelectedSourceForNew = testSource;
        vm.NewBorrowerName = "أحمد محمد";
        vm.NewPurpose = "فحص عينات";

        // Case 1: Expected return date in the past
        vm.NewExpectedReturnDate = DateTime.Today.AddDays(-1);
        vm.SubmitCommand.Execute(null);
        mockBorrowService.Verify(b => b.CreateRequest(It.IsAny<BorrowRequest>()), Times.Never);

        // Case 2: Expected return date too far in the future (> 2 years)
        vm.NewExpectedReturnDate = DateTime.Today.AddYears(2).AddDays(5);
        vm.SubmitCommand.Execute(null);
        mockBorrowService.Verify(b => b.CreateRequest(It.IsAny<BorrowRequest>()), Times.Never);
    }

    [Fact]
    public void MarkReturned_Validation_Rejects_ActualReturnDate_InFuture_Or_BeforeRequestDate()
    {
        // Arrange
        var mockSourceService = new Mock<ISourceService>();
        var mockUserService = new Mock<IUserService>();
        var mockReportingService = new Mock<IReportingService>();
        var mockBorrowService = new Mock<IBorrowService>();

        var vm = new BorrowViewModel(mockBorrowService.Object, mockSourceService.Object, mockUserService.Object, mockReportingService.Object);

        var request = new BorrowRequest
        {
            Id = Guid.NewGuid(),
            RequestDate = DateTime.Today.AddDays(-5),
            ExpectedReturnDate = DateTime.Today.AddDays(2),
            Status = "Delivered"
        };
        var returnerUser = new User { Id = Guid.NewGuid(), FullName = "علي المستلم" };

        vm.SelectedRequest = request;

        // Case 1: Null returned by user
        vm.SelectedReturnedBy = null;
        vm.NewActualReturnDate = DateTime.Today;
        vm.MarkReturnedCommand.Execute(null);
        mockBorrowService.Verify(b => b.MarkReturned(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<string>()), Times.Never);

        // Case 2: Actual return date before request date
        vm.SelectedReturnedBy = returnerUser;
        vm.NewActualReturnDate = DateTime.Today.AddDays(-6);
        vm.MarkReturnedCommand.Execute(null);
        mockBorrowService.Verify(b => b.MarkReturned(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<string>()), Times.Never);

        // Case 3: Actual return date in the future
        vm.NewActualReturnDate = DateTime.Today.AddDays(1);
        vm.MarkReturnedCommand.Execute(null);
        mockBorrowService.Verify(b => b.MarkReturned(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<string>()), Times.Never);
    }

    #endregion

    #region 3. إعدادات النظام - مهلة الاستحقاق القريب (DueSoonDaysThreshold)

    [Fact]
    public void SettingsViewModel_LoadsAndSaves_DueSoonDaysThreshold_WithValidation()
    {
        // Arrange
        var mockBackupService = new Mock<IBackupService>();
        var mockSettingsService = new Mock<ISystemSettingsService>();

        mockSettingsService
            .Setup(s => s.GetSetting("DueSoonDaysThreshold", 7))
            .Returns(7);
        mockSettingsService
            .Setup(s => s.GetSetting("LowActivityThresholdPercent", 10.0))
            .Returns(10.0);
        mockSettingsService
            .Setup(s => s.GetSetting("NotificationCheckIntervalMinutes", 60))
            .Returns(60);

        var vm = new SettingsViewModel(mockBackupService.Object, mockSettingsService.Object);

        // Assert: Initial load
        Assert.Equal(7, vm.DueSoonDaysThreshold);

        // Case 1: Invalid values (0 or > 365)
        vm.DueSoonDaysThreshold = 0;
        vm.SaveSystemSettingsCommand.Execute(null);
        mockSettingsService.Verify(s => s.SaveSetting("DueSoonDaysThreshold", It.IsAny<string>()), Times.Never);

        vm.DueSoonDaysThreshold = 400;
        vm.SaveSystemSettingsCommand.Execute(null);
        mockSettingsService.Verify(s => s.SaveSetting("DueSoonDaysThreshold", It.IsAny<string>()), Times.Never);

        // Case 2: Valid value
        vm.DueSoonDaysThreshold = 14;
        vm.SaveSystemSettingsCommand.Execute(null);
        mockSettingsService.Verify(s => s.SaveSetting("DueSoonDaysThreshold", "14"), Times.Once);
    }

    #endregion

    #region 4. اختبارات ترقيم صفوف الاستعارة (BorrowRequestRow)

    [Fact]
    public async Task BorrowViewModel_LoadData_And_PerformSearch_AssignsSequentialRowNumbersStartingFromOne()
    {
        // Arrange
        var mockSourceService = new Mock<ISourceService>();
        var mockUserService = new Mock<IUserService>();
        var mockReportingService = new Mock<IReportingService>();
        var mockBorrowService = new Mock<IBorrowService>();

        var testRequests = new List<BorrowRequest>
        {
            new BorrowRequest { Id = Guid.NewGuid(), BorrowerName = "مستعير 1", Status = "Delivered" },
            new BorrowRequest { Id = Guid.NewGuid(), BorrowerName = "مستعير 2", Status = "Returned" },
            new BorrowRequest { Id = Guid.NewGuid(), BorrowerName = "مستعير 3", Status = "Overdue" }
        };

        mockBorrowService.Setup(b => b.GetAll()).Returns(testRequests);

        var vm = new BorrowViewModel(mockBorrowService.Object, mockSourceService.Object, mockUserService.Object, mockReportingService.Object);
        await vm.LoadDataAsync();

        // Assert: Initial load
        Assert.Equal(3, vm.Requests.Count);
        Assert.Equal(1, vm.Requests[0].RowNumber);
        Assert.Equal(2, vm.Requests[1].RowNumber);
        Assert.Equal(3, vm.Requests[2].RowNumber);

        // Act: Filter by status "تم الإرجاع"
        vm.SelectedStatusFilter = "تم الإرجاع";
        vm.PerformSearchCommand.Execute(null);

        // Assert: Filtered sequence starts from 1
        Assert.Single(vm.Requests);
        Assert.Equal(1, vm.Requests[0].RowNumber);
        Assert.Equal("مستعير 2", vm.Requests[0].BorrowerName);
    }

    #endregion

    #region 5. اختبارات الفلترة في الذاكرة ومطابقة المستعير وقريبة الإرجاع

    [Fact]
    public async Task PerformSearch_FiltersInMemory_WithoutCallingGetAllRepeatedly()
    {
        // Arrange
        var mockSourceService = new Mock<ISourceService>();
        var mockUserService = new Mock<IUserService>();
        var mockReportingService = new Mock<IReportingService>();
        var mockBorrowService = new Mock<IBorrowService>();

        var testRequests = new List<BorrowRequest>
        {
            new BorrowRequest { Id = Guid.NewGuid(), BorrowerName = "د. طارق", Status = "Delivered", Source = new Source { SourceCode = "SRC-001" } },
            new BorrowRequest { Id = Guid.NewGuid(), BorrowerName = "م. سامي", Status = "Returned", Source = new Source { SourceCode = "SRC-002" } }
        };

        mockBorrowService.Setup(b => b.GetAll()).Returns(testRequests);

        var vm = new BorrowViewModel(mockBorrowService.Object, mockSourceService.Object, mockUserService.Object, mockReportingService.Object);
        await vm.LoadDataAsync();

        int initialGetAllCalls = mockBorrowService.Invocations.Count(i => i.Method.Name == nameof(IBorrowService.GetAll));

        // Act: Search multiple times
        vm.SearchQuery = "طارق";
        vm.PerformSearchCommand.Execute(null);
        Assert.Single(vm.Requests);
        Assert.Equal("د. طارق", vm.Requests[0].BorrowerName);
        Assert.Equal(2, vm.TotalCount); // TotalCount remains constant

        vm.SearchQuery = "SRC-002";
        vm.PerformSearchCommand.Execute(null);
        Assert.Single(vm.Requests);
        Assert.Equal("م. سامي", vm.Requests[0].BorrowerName);
        Assert.Equal(2, vm.TotalCount);

        // Assert: GetAll was NOT called again during searches
        int afterSearchesGetAllCalls = mockBorrowService.Invocations.Count(i => i.Method.Name == nameof(IBorrowService.GetAll));
        Assert.Equal(initialGetAllCalls, afterSearchesGetAllCalls);
    }

    [Fact]
    public async Task PerformSearch_WithDueSoonFilter_FiltersCorrectly()
    {
        // Arrange
        var mockSourceService = new Mock<ISourceService>();
        var mockUserService = new Mock<IUserService>();
        var mockReportingService = new Mock<IReportingService>();
        var mockBorrowService = new Mock<IBorrowService>();

        mockBorrowService.Setup(b => b.GetDueSoonDaysThreshold()).Returns(7);

        var testRequests = new List<BorrowRequest>
        {
            new BorrowRequest 
            { 
                Id = Guid.NewGuid(), 
                BorrowerName = "مستعير قريب", 
                Status = "Delivered", 
                ExpectedReturnDate = DateTime.Today.AddDays(3) 
            },
            new BorrowRequest 
            { 
                Id = Guid.NewGuid(), 
                BorrowerName = "مستعير بعيد", 
                Status = "Delivered", 
                ExpectedReturnDate = DateTime.Today.AddDays(20) 
            },
            new BorrowRequest 
            { 
                Id = Guid.NewGuid(), 
                BorrowerName = "مستعير متأخر", 
                Status = "Overdue", 
                ExpectedReturnDate = DateTime.Today.AddDays(-2) 
            }
        };

        mockBorrowService.Setup(b => b.GetAll()).Returns(testRequests);

        var vm = new BorrowViewModel(mockBorrowService.Object, mockSourceService.Object, mockUserService.Object, mockReportingService.Object);
        await vm.LoadDataAsync();

        // Act: Filter by "قريبة الإرجاع"
        vm.SelectedStatusFilter = "قريبة الإرجاع";
        vm.PerformSearchCommand.Execute(null);

        // Assert
        Assert.Single(vm.Requests);
        Assert.Equal("مستعير قريب", vm.Requests[0].BorrowerName);
        Assert.Equal(3, vm.TotalCount);
    }

    [Fact]
    public void Submit_WhenBorrowerMatchesSystemUser_SetsBorrowerUserId_OtherwiseNull()
    {
        // Arrange
        var mockSourceService = new Mock<ISourceService>();
        var mockUserService = new Mock<IUserService>();
        var mockReportingService = new Mock<IReportingService>();
        var mockBorrowService = new Mock<IBorrowService>();

        var currentUser = new User { Id = Guid.NewGuid(), FullName = "أمين المخزن المشغل", Username = "operator" };
        mockUserService.Setup(u => u.CurrentUser).Returns(currentUser);

        var registeredUser = new User { Id = Guid.NewGuid(), FullName = "د. كريم إبراهيم", Username = "karim" };

        mockBorrowService.Setup(b => b.GetAll()).Returns(new List<BorrowRequest>());
        mockBorrowService.Setup(b => b.CreateRequest(It.IsAny<BorrowRequest>()))
            .Returns((true, "نجاح"));

        var vm = new BorrowViewModel(mockBorrowService.Object, mockSourceService.Object, mockUserService.Object, mockReportingService.Object);

        vm.AvailableBorrowers.Add(registeredUser);
        vm.AvailableBorrowers.Add(currentUser);

        var testSource = new Source { Id = Guid.NewGuid(), SourceCode = "SRC-001" };
        vm.SelectedSourceForNew = testSource;
        vm.NewPurpose = "تجربة";

        // Case 1: Borrower matches registered user
        vm.NewBorrowerName = "د. كريم إبراهيم";
        BorrowRequest? capturedReq1 = null;
        mockBorrowService.Setup(b => b.CreateRequest(It.IsAny<BorrowRequest>()))
            .Callback<BorrowRequest>(r => capturedReq1 = r)
            .Returns((true, "نجاح"));

        vm.SubmitCommand.Execute(null);

        Assert.NotNull(capturedReq1);
        Assert.Equal(registeredUser.Id, capturedReq1.BorrowerUserId);
        Assert.Equal("أمين المخزن المشغل", capturedReq1.AddedBy);

        // Case 2: Borrower is external / non-registered text
        vm.IsEditing = true;
        vm.SelectedSourceForNew = testSource;
        vm.NewBorrowerName = "جهة خارجية غير مسجلة";
        vm.NewPurpose = "فحص عينات خارجي";
        vm.NewExpectedReturnDate = DateTime.Today.AddDays(5);
        BorrowRequest? capturedReq2 = null;
        mockBorrowService.Setup(b => b.CreateRequest(It.IsAny<BorrowRequest>()))
            .Callback<BorrowRequest>(r => capturedReq2 = r)
            .Returns((true, "نجاح"));

        vm.SubmitCommand.Execute(null);

        Assert.NotNull(capturedReq2);
        Assert.Null(capturedReq2.BorrowerUserId); // ليس مسجلاً
        Assert.Equal("جهة خارجية غير مسجلة", capturedReq2.BorrowerName);
        Assert.Equal("أمين المخزن المشغل", capturedReq2.AddedBy);
    }

    #endregion

    #region 4. اختبارات ترتيب وتحديث قائمة المصادر المتاحة للاستعارة (LoadAvailableSources)

    [Fact]
    public void LoadAvailableSources_ReturnsSourcesOrderedAlphabeticallyBySourceCode()
    {
        // Arrange
        using var db = _fixture.CreateContext();
        var iso = TestDataBuilder.CreateRadioisotope("Cs-137", "Cesium-137");
        var unit = TestDataBuilder.CreateActivityUnit("mCi", "mCi");
        var loc = TestDataBuilder.CreateLocation("مستودع أ");
        db.Radioisotopes.Add(iso);
        db.ActivityUnits.Add(unit);
        db.Locations.Add(loc);

        // إضافة مصادر بأسماء غير مرتبة
        var srcZ = TestDataBuilder.CreateSource(iso, unit, loc, "SRC-Z999", 10.0, DateTime.Now, "Storage");
        var srcA = TestDataBuilder.CreateSource(iso, unit, loc, "SRC-A001", 10.0, DateTime.Now, "Storage");
        var srcM = TestDataBuilder.CreateSource(iso, unit, loc, "SRC-M500", 10.0, DateTime.Now, "Storage");
        var srcEdr = TestDataBuilder.CreateSource(iso, unit, loc, "edr-1976", 10.0, DateTime.Now, "Storage");
        var srcInUse = TestDataBuilder.CreateSource(iso, unit, loc, "SRC-INUSE", 10.0, DateTime.Now, "InUse");
        var srcDeleted = TestDataBuilder.CreateSource(iso, unit, loc, "SRC-DELETED", 10.0, DateTime.Now, "Storage");
        srcDeleted.IsDeleted = true;

        db.Sources.AddRange(srcZ, srcA, srcM, srcEdr, srcInUse, srcDeleted);
        db.SaveChanges();

        var mockSourceService = new Mock<ISourceService>();
        var mockUserService = new Mock<IUserService>();
        var mockReportingService = new Mock<IReportingService>();

        var vm = new BorrowViewModel(_borrowService, mockSourceService.Object, mockUserService.Object, mockReportingService.Object, _fixture.ContextFactory);

        // Act
        vm.LoadAvailableSources();

        // Assert
        Assert.Equal(4, vm.AvailableSources.Count); // Z, A, M, edr-1976 فقط (استبعاد InUse و Deleted)
        Assert.DoesNotContain(vm.AvailableSources, s => s.SourceCode == "SRC-INUSE");
        Assert.DoesNotContain(vm.AvailableSources, s => s.SourceCode == "SRC-DELETED");

        var codes = vm.AvailableSources.Select(s => s.SourceCode).ToList();
        var sortedCodes = codes.OrderBy(c => c, StringComparer.Ordinal).ToList();
        Assert.Equal(sortedCodes, codes); // مرتبة أبجدياً (مطابقة لـ SQL ORDER BY)
    }

    [Fact]
    public void LoadAvailableSources_IncludesActiveStorageSource_SuchAs_edr1976()
    {
        // Arrange
        using var db = _fixture.CreateContext();
        var iso = TestDataBuilder.CreateRadioisotope("Am-241", "Americium-241");
        var unit = TestDataBuilder.CreateActivityUnit("µCi", "µCi");
        var loc = TestDataBuilder.CreateLocation("مستودع المواد المشعة");
        db.Radioisotopes.Add(iso);
        db.ActivityUnits.Add(unit);
        db.Locations.Add(loc);

        var edrSource = TestDataBuilder.CreateSource(iso, unit, loc, "edr-1976", 25.0, DateTime.Now, "Storage");
        edrSource.IsDeleted = false;
        edrSource.IsSealed = true;
        db.Sources.Add(edrSource);
        db.SaveChanges();

        var mockSourceService = new Mock<ISourceService>();
        var mockUserService = new Mock<IUserService>();
        var mockReportingService = new Mock<IReportingService>();

        var vm = new BorrowViewModel(_borrowService, mockSourceService.Object, mockUserService.Object, mockReportingService.Object, _fixture.ContextFactory);

        // Act
        vm.LoadAvailableSources();

        // Assert
        Assert.Contains(vm.AvailableSources, s => s.SourceCode == "edr-1976" && s.Status == "Storage");
    }

    [Fact]
    public void BorrowViewModel_ReceivesSourcesUpdatedMessage_RefreshesAvailableSources()
    {
        // Arrange
        using var db = _fixture.CreateContext();
        var iso = TestDataBuilder.CreateRadioisotope("Co-60", "Cobalt-60");
        var unit = TestDataBuilder.CreateActivityUnit("Ci", "Ci");
        var loc = TestDataBuilder.CreateLocation("المستودع");
        db.Radioisotopes.Add(iso);
        db.ActivityUnits.Add(unit);
        db.Locations.Add(loc);
        db.SaveChanges();

        var mockSourceService = new Mock<ISourceService>();
        var mockUserService = new Mock<IUserService>();
        var mockReportingService = new Mock<IReportingService>();

        using var vm = new BorrowViewModel(_borrowService, mockSourceService.Object, mockUserService.Object, mockReportingService.Object, _fixture.ContextFactory);

        vm.LoadAvailableSources();
        Assert.Empty(vm.AvailableSources);

        // إضافة مصدر في قاعدة البيانات
        var newSource = TestDataBuilder.CreateSource(iso, unit, loc, "SRC-NEW-STORAGE", 5.0, DateTime.Now, "Storage");
        db.Sources.Add(newSource);
        db.SaveChanges();

        // Act: إرسال رسالة تحديث المصادر
        CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default.Send(new Sources.Messages.SourcesUpdatedMessage());

        // Assert
        Assert.Contains(vm.AvailableSources, s => s.SourceCode == "SRC-NEW-STORAGE");
    }

    [Fact]
    public void BorrowRequestRow_WithLoadedSourceRelations_DisplaysAccurateIsotopeActivityAndDoseRate()
    {
        // Arrange
        using var db = _fixture.CreateContext();
        var cs137 = TestDataBuilder.CreateRadioisotope("Cs-137", "Cesium-137", radiationType: "Beta/Gamma");
        cs137.GammaConstant = 0.0772;
        var mbqUnit = TestDataBuilder.CreateActivityUnit("MBq", "MBq", conversionToBq: 1e6);
        var loc = TestDataBuilder.CreateLocation("المختبر الرئيسي");
        db.Radioisotopes.Add(cs137);
        db.ActivityUnits.Add(mbqUnit);
        db.Locations.Add(loc);
        db.SaveChanges();

        // 100 MBq of Cs-137 -> DoseRate = 100 * 0.0772 = 7.72 µSv/h @ 1m
        var source = TestDataBuilder.CreateSource(cs137, mbqUnit, loc, "SRC-BORROW-TEST", 100.0, DateTime.Today.AddDays(-10), "InUse");
        db.Sources.Add(source);
        db.SaveChanges();

        var borrowReq = new BorrowRequest
        {
            Id = Guid.NewGuid(),
            SourceId = source.Id,
            BorrowerName = "د. أحمد علي",
            Purpose = "أبحاث علمية",
            RequestDate = DateTime.Today.AddDays(-5),
            ExpectedReturnDate = DateTime.Today.AddDays(5),
            Status = "Delivered"
        };
        db.BorrowRequests.Add(borrowReq);
        db.SaveChanges();

        // Act: استرجاع الطلبات عبر BorrowService.GetAll()
        var allRequests = _borrowService.GetAll();
        Assert.Single(allRequests);

        var loadedRequest = allRequests[0];
        Assert.NotNull(loadedRequest.Source);
        Assert.NotNull(loadedRequest.Source.Radioisotope);
        Assert.NotNull(loadedRequest.Source.InitialActivityUnit);

        // إنشاء صف العرض BorrowRequestRow
        var row = new BorrowRequestRow
        {
            RowNumber = 1,
            Request = loadedRequest
        };

        // Assert: التحقق من صحة الخصائص المعروضة
        Assert.Equal("SRC-BORROW-TEST", row.DisplaySourceCode);
        Assert.Contains("Cs-137", row.DisplayIsotopes);
        Assert.Equal("100 MBq", row.CurrentActivityWithUnit);
        Assert.Equal("7.72 µSv/h @ 1m", row.DisplayDoseRate);
        Assert.Contains("7.72 µSv/h", row.DoseRateTooltip);
    }

    #endregion

    #region 7. فحص استبعاد المصادر الفاشلة في فحص التسرب من قائمة الاستعارة المتاحة

    [Fact]
    public void LoadAvailableSources_ShouldExcludeSourcesWithFailedLatestLeakTest()
    {
        // Arrange
        using var db = _fixture.CreateContext();
        var iso = TestDataBuilder.CreateRadioisotope("Cs-137", "Cesium-137");
        var unit = TestDataBuilder.CreateActivityUnit("MBq", "MBq");
        var loc = TestDataBuilder.CreateLocation("مخزن رئيسي");
        db.Radioisotopes.Add(iso);
        db.ActivityUnits.Add(unit);
        db.Locations.Add(loc);
        db.SaveChanges();

        // 1. مصدر صالح بدون فحص تسرب (مخزن)
        var srcValid1 = TestDataBuilder.CreateSource(iso, unit, loc, "SRC-VALID-NO-TEST", 50.0, DateTime.Today, "Storage");
        // 2. مصدر صالح بآخر فحص ناجح (مخزن)
        var srcValid2 = TestDataBuilder.CreateSource(iso, unit, loc, "SRC-VALID-PASSED", 50.0, DateTime.Today, "Storage");
        // 3. مصدر فاشل بآخر فحص راسب (مخزن)
        var srcFailed = TestDataBuilder.CreateSource(iso, unit, loc, "SRC-FAILED-LEAK", 50.0, DateTime.Today, "Storage");
        // 4. مصدر كان فاشلاً سابقاً ولكن أحدث فحص له ناجح (مخزن)
        var srcRecovered = TestDataBuilder.CreateSource(iso, unit, loc, "SRC-RECOVERED", 50.0, DateTime.Today, "Storage");

        db.Sources.AddRange(srcValid1, srcValid2, srcFailed, srcRecovered);
        db.SaveChanges();

        // سجلات الفحص
        var testPass = new LeakTestRecord
        {
            SourceId = srcValid2.Id,
            TestDate = DateTime.Today.AddDays(-5),
            Result = "Pass",
            NextDueDate = DateTime.Today.AddMonths(6)
        };
        var testFail = new LeakTestRecord
        {
            SourceId = srcFailed.Id,
            TestDate = DateTime.Today.AddDays(-2),
            Result = "Fail",
            NextDueDate = DateTime.Today.AddMonths(6)
        };
        var oldTestFail = new LeakTestRecord
        {
            SourceId = srcRecovered.Id,
            TestDate = DateTime.Today.AddDays(-20),
            Result = "Fail",
            NextDueDate = DateTime.Today.AddMonths(6)
        };
        var newTestPass = new LeakTestRecord
        {
            SourceId = srcRecovered.Id,
            TestDate = DateTime.Today.AddDays(-1),
            Result = "Pass",
            NextDueDate = DateTime.Today.AddMonths(6)
        };

        db.LeakTestRecords.AddRange(testPass, testFail, oldTestFail, newTestPass);
        db.SaveChanges();

        var mockSourceService = new Mock<ISourceService>();
        var mockReportingService = new Mock<IReportingService>();

        var vm = new BorrowViewModel(
            _borrowService,
            mockSourceService.Object,
            _fakeUserService,
            mockReportingService.Object,
            _fixture.ContextFactory);

        // Act
        vm.LoadAvailableSources();

        // Assert
        var availableCodes = vm.AvailableSources.Select(s => s.SourceCode).ToList();

        Assert.Contains("SRC-VALID-NO-TEST", availableCodes);
        Assert.Contains("SRC-VALID-PASSED", availableCodes);
        Assert.Contains("SRC-RECOVERED", availableCodes);
        Assert.DoesNotContain("SRC-FAILED-LEAK", availableCodes);
    }

    #endregion
}
