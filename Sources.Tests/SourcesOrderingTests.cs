using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Moq;
using Sources.Data;
using Sources.Models;
using Sources.Services;
using Sources.Tests.Fakes;
using Sources.Tests.Fixtures;
using Sources.ViewModels;
using Xunit;

namespace Sources.Tests;

public class SourcesOrderingTests : IClassFixture<SqliteInMemoryFixture>, IDisposable
{
    private readonly SqliteInMemoryFixture _fixture;
    private readonly DecayCalculationService _decayService;
    private readonly FakeAuditService _auditService;
    private readonly FakeUserService _userService;
    private readonly ISystemSettingsService _settingsService;
    private readonly SourceService _sourceService;
    private readonly BorrowService _borrowService;
    private readonly LeakTestService _leakTestService;
    private readonly AlertService _alertService;

    private Radioisotope _testIsotope = null!;
    private ActivityUnit _testUnit = null!;
    private Location _testLocation = null!;
    private User _testUser = null!;

    public SourcesOrderingTests(SqliteInMemoryFixture fixture)
    {
        _fixture = fixture;
        _fixture.ResetDatabase();

        _decayService = new DecayCalculationService();
        _auditService = new FakeAuditService();
        _userService = new FakeUserService();
        _settingsService = new SystemSettingsService(_fixture.ContextFactory);

        _sourceService = new SourceService(
            _fixture.ContextFactory,
            _decayService,
            _auditService,
            _userService);

        _borrowService = new BorrowService(
            _fixture.ContextFactory,
            _auditService,
            _userService,
            _settingsService);

        _leakTestService = new LeakTestService(
            _fixture.ContextFactory,
            _auditService,
            _userService,
            _settingsService);

        _alertService = new AlertService(
            _fixture.ContextFactory,
            _decayService,
            _settingsService);

        SeedData();
    }

    private void SeedData()
    {
        using var db = _fixture.CreateContext();

        _testIsotope = new Radioisotope
        {
            Id = Guid.NewGuid(),
            Name = "Cesium-137",
            Symbol = "Cs-137",
            HalfLife = 30.08,
            HalfLifeUnit = "years"
        };
        db.Radioisotopes.Add(_testIsotope);

        _testUnit = new ActivityUnit
        {
            Id = Guid.NewGuid(),
            UnitName = "Megabecquerel",
            UnitSymbol = "MBq",
            ConversionToBq = 1_000_000
        };
        db.ActivityUnits.Add(_testUnit);

        _testLocation = new Location
        {
            Id = Guid.NewGuid(),
            LocationName = "المستودع الرئيسي",
            Building = "مبنى 1",
            Room = "101"
        };
        db.Locations.Add(_testLocation);

        var adminRole = new Role { Id = Guid.NewGuid(), RoleName = "مدير النظام" };
        db.Roles.Add(adminRole);

        _testUser = new User
        {
            Id = Guid.NewGuid(),
            Username = "testuser",
            FullName = "مستخدم تجريبي",
            RoleId = adminRole.Id,
            Role = adminRole,
            IsActive = true
        };
        db.Users.Add(_testUser);

        db.SaveChanges();
    }

    public void Dispose()
    {
        _fixture.ResetDatabase();
    }

    [Fact]
    public void SourceService_GetAllSources_IsOrderedBySourceCodeAscending()
    {
        // Arrange
        using (var db = _fixture.CreateContext())
        {
            db.Sources.AddRange(
                new Source { Id = Guid.NewGuid(), SourceCode = "SRC-Z99", RadioisotopeId = _testIsotope.Id, InitialActivityUnitId = _testUnit.Id, CurrentActivityUnitId = _testUnit.Id, LocationId = _testLocation.Id, Status = "Storage", CreatedAt = DateTime.Now.AddDays(-1) },
                new Source { Id = Guid.NewGuid(), SourceCode = "SRC-A01", RadioisotopeId = _testIsotope.Id, InitialActivityUnitId = _testUnit.Id, CurrentActivityUnitId = _testUnit.Id, LocationId = _testLocation.Id, Status = "Storage", CreatedAt = DateTime.Now.AddDays(-10) },
                new Source { Id = Guid.NewGuid(), SourceCode = "SRC-M50", RadioisotopeId = _testIsotope.Id, InitialActivityUnitId = _testUnit.Id, CurrentActivityUnitId = _testUnit.Id, LocationId = _testLocation.Id, Status = "Storage", CreatedAt = DateTime.Now }
            );
            db.SaveChanges();
        }

        // Act
        var sources = _sourceService.GetAllSources();

        // Assert
        Assert.Equal(3, sources.Count);
        Assert.Equal("SRC-A01", sources[0].SourceCode);
        Assert.Equal("SRC-M50", sources[1].SourceCode);
        Assert.Equal("SRC-Z99", sources[2].SourceCode);
    }

    [Fact]
    public void SourceService_GetDeletedSources_IsOrderedByDeletedAtDesc_ThenSourceCodeAsc()
    {
        // Arrange
        var date1 = DateTime.Now.AddDays(-5);
        var date2 = DateTime.Now.AddDays(-1);

        using (var db = _fixture.CreateContext())
        {
            db.Sources.AddRange(
                new Source { Id = Guid.NewGuid(), SourceCode = "SRC-B", RadioisotopeId = _testIsotope.Id, InitialActivityUnitId = _testUnit.Id, CurrentActivityUnitId = _testUnit.Id, LocationId = _testLocation.Id, Status = "Storage", IsDeleted = true, DeletedAt = date1 },
                new Source { Id = Guid.NewGuid(), SourceCode = "SRC-Z", RadioisotopeId = _testIsotope.Id, InitialActivityUnitId = _testUnit.Id, CurrentActivityUnitId = _testUnit.Id, LocationId = _testLocation.Id, Status = "Storage", IsDeleted = true, DeletedAt = date2 },
                new Source { Id = Guid.NewGuid(), SourceCode = "SRC-A", RadioisotopeId = _testIsotope.Id, InitialActivityUnitId = _testUnit.Id, CurrentActivityUnitId = _testUnit.Id, LocationId = _testLocation.Id, Status = "Storage", IsDeleted = true, DeletedAt = date2 }
            );
            db.SaveChanges();
        }

        // Act
        var deleted = _sourceService.GetDeletedSources();

        // Assert
        Assert.Equal(3, deleted.Count);
        // date2 is more recent than date1, so date2 items come first, sorted by SourceCode ASC: SRC-A then SRC-Z, then date1: SRC-B
        Assert.Equal("SRC-A", deleted[0].SourceCode);
        Assert.Equal("SRC-Z", deleted[1].SourceCode);
        Assert.Equal("SRC-B", deleted[2].SourceCode);
    }

    [Fact]
    public void BorrowService_GetAll_IsOrderedByRequestDateDesc_ThenSourceCodeAsc()
    {
        // Arrange
        var date1 = DateTime.Now.AddDays(-5);
        var date2 = DateTime.Now.AddDays(-1);

        using var db = _fixture.CreateContext();
        var src1 = new Source { Id = Guid.NewGuid(), SourceCode = "SRC-Z", RadioisotopeId = _testIsotope.Id, InitialActivityUnitId = _testUnit.Id, CurrentActivityUnitId = _testUnit.Id, LocationId = _testLocation.Id, Status = "Storage" };
        var src2 = new Source { Id = Guid.NewGuid(), SourceCode = "SRC-A", RadioisotopeId = _testIsotope.Id, InitialActivityUnitId = _testUnit.Id, CurrentActivityUnitId = _testUnit.Id, LocationId = _testLocation.Id, Status = "Storage" };
        var src3 = new Source { Id = Guid.NewGuid(), SourceCode = "SRC-M", RadioisotopeId = _testIsotope.Id, InitialActivityUnitId = _testUnit.Id, CurrentActivityUnitId = _testUnit.Id, LocationId = _testLocation.Id, Status = "Storage" };
        db.Sources.AddRange(src1, src2, src3);

        db.BorrowRequests.AddRange(
            new BorrowRequest { Id = Guid.NewGuid(), SourceId = src1.Id, BorrowerUserId = _testUser.Id, RequestDate = date2, Status = "Delivered" },
            new BorrowRequest { Id = Guid.NewGuid(), SourceId = src2.Id, BorrowerUserId = _testUser.Id, RequestDate = date2, Status = "Delivered" },
            new BorrowRequest { Id = Guid.NewGuid(), SourceId = src3.Id, BorrowerUserId = _testUser.Id, RequestDate = date1, Status = "Delivered" }
        );
        db.SaveChanges();

        // Act
        var borrows = _borrowService.GetAll();

        // Assert
        Assert.Equal(3, borrows.Count);
        Assert.Equal("SRC-A", borrows[0].Source?.SourceCode);
        Assert.Equal("SRC-Z", borrows[1].Source?.SourceCode);
        Assert.Equal("SRC-M", borrows[2].Source?.SourceCode);
    }

    [Fact]
    public void LeakTestService_GetAllRecords_IsOrderedByTestDateDesc_ThenCreatedAtDesc_ThenSourceCodeAsc()
    {
        // Arrange
        var date1 = DateTime.Now.AddDays(-5);
        var date2 = DateTime.Now.AddDays(-1);

        using var db = _fixture.CreateContext();
        var srcZ = new Source { Id = Guid.NewGuid(), SourceCode = "SRC-Z", IsSealed = true, RadioisotopeId = _testIsotope.Id, InitialActivityUnitId = _testUnit.Id, CurrentActivityUnitId = _testUnit.Id, LocationId = _testLocation.Id, Status = "Storage" };
        var srcA = new Source { Id = Guid.NewGuid(), SourceCode = "SRC-A", IsSealed = true, RadioisotopeId = _testIsotope.Id, InitialActivityUnitId = _testUnit.Id, CurrentActivityUnitId = _testUnit.Id, LocationId = _testLocation.Id, Status = "Storage" };
        db.Sources.AddRange(srcZ, srcA);

        var created = DateTime.Now;
        db.LeakTestRecords.AddRange(
            new LeakTestRecord { Id = Guid.NewGuid(), SourceId = srcZ.Id, TestDate = date2, CreatedAt = created, Result = "Pass", PerformedByUserId = _testUser.Id, InspectorName = "Ins" },
            new LeakTestRecord { Id = Guid.NewGuid(), SourceId = srcA.Id, TestDate = date2, CreatedAt = created, Result = "Pass", PerformedByUserId = _testUser.Id, InspectorName = "Ins" },
            new LeakTestRecord { Id = Guid.NewGuid(), SourceId = srcZ.Id, TestDate = date1, CreatedAt = created, Result = "Pass", PerformedByUserId = _testUser.Id, InspectorName = "Ins" }
        );
        db.SaveChanges();

        // Act
        var records = _leakTestService.GetAllRecords();

        // Assert
        Assert.Equal(3, records.Count);
        Assert.Equal("SRC-A", records[0].Source?.SourceCode);
        Assert.Equal("SRC-Z", records[1].Source?.SourceCode);
        Assert.Equal("SRC-Z", records[2].Source?.SourceCode);
    }

    [Fact]
    public void AlertService_GetAllAlerts_IsOrderedBySeverityDesc_ThenCreatedAtDesc_ThenSourceCodeAsc()
    {
        // Arrange
        var date = DateTime.Now.AddDays(-1);

        using var db = _fixture.CreateContext();
        var srcZ = new Source { Id = Guid.NewGuid(), SourceCode = "SRC-Z", RadioisotopeId = _testIsotope.Id, InitialActivityUnitId = _testUnit.Id, CurrentActivityUnitId = _testUnit.Id, LocationId = _testLocation.Id, Status = "Storage" };
        var srcA = new Source { Id = Guid.NewGuid(), SourceCode = "SRC-A", RadioisotopeId = _testIsotope.Id, InitialActivityUnitId = _testUnit.Id, CurrentActivityUnitId = _testUnit.Id, LocationId = _testLocation.Id, Status = "Storage" };
        db.Sources.AddRange(srcZ, srcA);

        db.AlertNotifications.AddRange(
            new AlertNotification { Id = Guid.NewGuid(), SourceId = srcZ.Id, Severity = "Critical", AlertType = "LowActivity", Message = "Msg", CreatedAt = date },
            new AlertNotification { Id = Guid.NewGuid(), SourceId = srcA.Id, Severity = "Critical", AlertType = "LowActivity", Message = "Msg", CreatedAt = date },
            new AlertNotification { Id = Guid.NewGuid(), SourceId = srcA.Id, Severity = "Warning", AlertType = "LowActivity", Message = "Msg", CreatedAt = date }
        );
        db.SaveChanges();

        // Act
        var alerts = _alertService.GetAllAlerts(includeDismissed: true);

        // Assert
        Assert.Equal(3, alerts.Count);
        // Critical alerts first, then ordered by SourceCode: SRC-A then SRC-Z, then Warning: SRC-A
        Assert.Equal("Critical", alerts[0].Severity);
        Assert.Equal("SRC-A", alerts[0].Source?.SourceCode);
        Assert.Equal("Critical", alerts[1].Severity);
        Assert.Equal("SRC-Z", alerts[1].Source?.SourceCode);
        Assert.Equal("Warning", alerts[2].Severity);
        Assert.Equal("SRC-A", alerts[2].Source?.SourceCode);
    }

    [Fact]
    public void ReportsViewModel_Orders_InventoryAndActivityReports_BySourceCodeAsc()
    {
        // Arrange
        var mockSourceService = new Mock<ISourceService>();
        var mockBorrowService = new Mock<IBorrowService>();
        var mockReportingService = new Mock<IReportingService>();
        var mockSettingsService = new Mock<ISystemSettingsService>();

        var s1 = new Source { Id = Guid.NewGuid(), SourceCode = "SRC-99", Status = "InUse" };
        var s2 = new Source { Id = Guid.NewGuid(), SourceCode = "SRC-01", Status = "InUse" };
        var s3 = new Source { Id = Guid.NewGuid(), SourceCode = "SRC-50", Status = "Storage" };

        mockSourceService.Setup(s => s.GetAllSources()).Returns(new List<Source> { s1, s2, s3 });

        var vm = new ReportsViewModel(mockSourceService.Object, mockBorrowService.Object, mockReportingService.Object, mockSettingsService.Object);

        // Act - InventoryReport
        vm.SelectedReport = "InventoryReport";
        Assert.Equal(3, vm.InventoryData.Count);
        Assert.Equal("SRC-01", vm.InventoryData[0].Source.SourceCode);
        Assert.Equal("SRC-50", vm.InventoryData[1].Source.SourceCode);
        Assert.Equal("SRC-99", vm.InventoryData[2].Source.SourceCode);

        // Act - ActivityReport
        vm.SelectedReport = "ActivityReport";
        Assert.Equal(3, vm.ActivityData.Count);
        Assert.Equal("SRC-01", vm.ActivityData[0].Source.SourceCode);
        Assert.Equal("SRC-50", vm.ActivityData[1].Source.SourceCode);
        Assert.Equal("SRC-99", vm.ActivityData[2].Source.SourceCode);
    }
}
