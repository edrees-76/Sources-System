using System;
using System.Linq;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.EntityFrameworkCore;
using Moq;
using Sources.Data;
using Sources.Messages;
using Sources.Models;
using Sources.Services;
using Sources.Tests.Fixtures;
using Sources.Tests.Helpers;
using Xunit;


namespace Sources.Tests;

public class LeakTestServiceTests : IClassFixture<SqliteInMemoryFixture>, IDisposable
{
    private readonly SqliteInMemoryFixture _fixture;
    private readonly Mock<IAuditService> _mockAuditService;
    private readonly Mock<IUserService> _mockUserService;
    private readonly ISystemSettingsService _settingsService;
    private readonly LeakTestService _leakTestService;

    private Radioisotope _testIsotope = null!;
    private ActivityUnit _testUnit = null!;
    private Location _testLocation = null!;
    private Source _testSource = null!;
    private User _testUser = null!;

    public LeakTestServiceTests(SqliteInMemoryFixture fixture)
    {
        _fixture = fixture;
        _fixture.ResetDatabase();

        _mockAuditService = new Mock<IAuditService>();
        _mockUserService = new Mock<IUserService>();
        _settingsService = new SystemSettingsService(_fixture.ContextFactory);

        _leakTestService = new LeakTestService(
            _fixture.ContextFactory,
            _mockAuditService.Object,
            _mockUserService.Object,
            _settingsService);

        SeedData();
    }

    private void SeedData()
    {
        using var context = _fixture.CreateContext();

        _testIsotope = TestDataBuilder.CreateRadioisotope("Cs-137", "Cesium-137", 30.08, "years", 661.7);
        _testUnit = TestDataBuilder.CreateActivityUnit("Becquerel", "Bq", 1.0);
        _testLocation = TestDataBuilder.CreateLocation("مختبر 1", "Lab", "A", "101");

        var testRole = new Role
        {
            Id = Guid.NewGuid(),
            RoleName = "مسؤول وقاية",
            Description = "دور فاحص ومسؤول وقاية"
        };

        _testUser = new User
        {
            Id = Guid.NewGuid(),
            Username = "inspector1",
            FullName = "م. خالد الفاحص",
            PasswordHash = "hash",
            RoleId = testRole.Id,
            IsActive = true
        };

        _testSource = TestDataBuilder.CreateSource(
            _testIsotope,
            _testUnit,
            _testLocation,
            sourceCode: "SRC-LEAK-01",
            isSealed: true);


        context.Roles.Add(testRole);
        context.Radioisotopes.Add(_testIsotope);
        context.ActivityUnits.Add(_testUnit);
        context.Locations.Add(_testLocation);
        context.Users.Add(_testUser);
        context.Sources.Add(_testSource);

        context.SaveChanges();

        _settingsService.SaveSetting(SystemSettingsDefaults.LeakTestIntervalMonthsKey, "6");
        _settingsService.SaveSetting(SystemSettingsDefaults.LeakTestWarningDaysThresholdKey, "30");

        _mockUserService.Setup(u => u.CurrentUser).Returns(_testUser);
    }

    public void Dispose()
    {
    }

    #region AddRecord Tests

    [Fact]
    public void AddRecord_ValidRecord_PersistsToDbAndReturnsSuccess()
    {
        // Arrange
        var record = new LeakTestRecord
        {
            SourceId = _testSource.Id,
            TestDate = DateTime.Today,
            NextDueDate = DateTime.Today.AddMonths(6),
            Result = "Pass",
            MeasuredActivityBq = 0.05,
            InspectorName = "د. سعيد",
            CertificateNumber = "CERT-001",
            Notes = "سليم تماماً"
        };

        // Act
        var result = _leakTestService.AddRecord(record);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Record);

        using var context = _fixture.CreateContext();
        var dbRecord = context.LeakTestRecords.FirstOrDefault(r => r.Id == record.Id);
        Assert.NotNull(dbRecord);
        Assert.Equal("Pass", dbRecord.Result);
        Assert.Equal(0.05, dbRecord.MeasuredActivityBq);
        Assert.Equal("CERT-001", dbRecord.CertificateNumber);
        Assert.Equal(_testUser.Id, dbRecord.PerformedByUserId);

        _mockAuditService.Verify(a => a.Log("Create", "LeakTestRecords", record.Id, It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public void AddRecord_AutoCalculatesNextDueDate_WhenNotExplicitlyProvided()
    {
        // Arrange
        var testDate = new DateTime(2026, 1, 15);
        var record = new LeakTestRecord
        {
            SourceId = _testSource.Id,
            TestDate = testDate,
            NextDueDate = default,
            Result = "Pass"
        };

        // Act
        var result = _leakTestService.AddRecord(record);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Record);
        // Default interval is 6 months
        Assert.Equal(new DateTime(2026, 7, 15), result.Record.NextDueDate);
    }

    [Fact]
    public void AddRecord_RespectsCustomNextDueDate_WhenManuallySpecified()
    {
        // Arrange
        var testDate = new DateTime(2026, 1, 15);
        var customDueDate = new DateTime(2026, 4, 15); // 3 months manual override
        var record = new LeakTestRecord
        {
            SourceId = _testSource.Id,
            TestDate = testDate,
            NextDueDate = customDueDate,
            Result = "Pass"
        };

        // Act
        var result = _leakTestService.AddRecord(record);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Record);
        Assert.Equal(customDueDate, result.Record.NextDueDate);
    }

    [Fact]
    public void AddRecord_InvalidSource_ReturnsFalse()
    {
        // Arrange
        var record = new LeakTestRecord
        {
            SourceId = Guid.NewGuid(), // Non-existent source
            TestDate = DateTime.Today,
            Result = "Pass"
        };

        // Act
        var result = _leakTestService.AddRecord(record);

        // Assert
        Assert.False(result.Success);
    }

    #endregion

    #region UpdateRecord Tests

    [Fact]
    public void UpdateRecord_ValidChanges_UpdatesDbAndLogsAudit()
    {
        // Arrange
        var record = new LeakTestRecord
        {
            SourceId = _testSource.Id,
            TestDate = DateTime.Today.AddDays(-10),
            NextDueDate = DateTime.Today.AddMonths(6),
            Result = "Pass",
            Notes = "أولي"
        };
        _leakTestService.AddRecord(record);

        // Act
        record.Result = "Fail";
        record.MeasuredActivityBq = 250.0;
        record.Notes = "تم اكتشاف تسرب إشعاعي";
        var result = _leakTestService.UpdateRecord(record);

        // Assert
        Assert.True(result.Success);

        using var context = _fixture.CreateContext();
        var updated = context.LeakTestRecords.Find(record.Id);
        Assert.NotNull(updated);
        Assert.Equal("Fail", updated.Result);
        Assert.Equal(250.0, updated.MeasuredActivityBq);
        Assert.Equal("تم اكتشاف تسرب إشعاعي", updated.Notes);

        _mockAuditService.Verify(a => a.LogWithChanges("Update", "LeakTestRecords", record.Id, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    #endregion

    #region DeleteRecord Tests

    [Fact]
    public void DeleteRecord_RemovesFromDbAndLogsAudit()
    {
        // Arrange
        var record = new LeakTestRecord
        {
            SourceId = _testSource.Id,
            TestDate = DateTime.Today,
            NextDueDate = DateTime.Today.AddMonths(6),
            Result = "Pass"
        };
        _leakTestService.AddRecord(record);

        // Act
        var result = _leakTestService.DeleteRecord(record.Id);

        // Assert
        Assert.True(result.Success);

        using var context = _fixture.CreateContext();
        var deleted = context.LeakTestRecords.Find(record.Id);
        Assert.Null(deleted);

        _mockAuditService.Verify(a => a.LogWithChanges("Delete", "LeakTestRecords", record.Id, It.IsAny<string>(), It.IsAny<string>(), null), Times.Once);
    }

    #endregion

    #region Query & Filter Tests

    [Fact]
    public void GetAllRecords_FilterByResult_ReturnsOnlyMatchingResults()
    {
        // Arrange
        var recPass = new LeakTestRecord { SourceId = _testSource.Id, TestDate = DateTime.Today, NextDueDate = DateTime.Today.AddMonths(6), Result = "Pass" };
        var recFail = new LeakTestRecord { SourceId = _testSource.Id, TestDate = DateTime.Today, NextDueDate = DateTime.Today.AddMonths(6), Result = "Fail" };
        _leakTestService.AddRecord(recPass);
        _leakTestService.AddRecord(recFail);

        // Act
        var passResults = _leakTestService.GetAllRecords(resultFilter: "Pass");
        var failResults = _leakTestService.GetAllRecords(resultFilter: "Fail");

        // Assert
        Assert.Single(passResults);
        Assert.Equal("Pass", passResults[0].Result);

        Assert.Single(failResults);
        Assert.Equal("Fail", failResults[0].Result);
    }

    [Fact]
    public void GetAllRecords_FilterByDueStatus_Overdue_ReturnsOnlyOverdue()
    {
        // Arrange
        var recOverdue = new LeakTestRecord { SourceId = _testSource.Id, TestDate = DateTime.Today.AddMonths(-7), NextDueDate = DateTime.Today.AddDays(-10), Result = "Pass" };
        var recValid = new LeakTestRecord { SourceId = _testSource.Id, TestDate = DateTime.Today, NextDueDate = DateTime.Today.AddMonths(6), Result = "Pass" };
        _leakTestService.AddRecord(recOverdue);
        _leakTestService.AddRecord(recValid);

        // Act
        var overdue = _leakTestService.GetAllRecords(dueStatusFilter: "Overdue");

        // Assert
        Assert.Single(overdue);
        Assert.Equal(recOverdue.Id, overdue[0].Id);
    }

    [Fact]
    public void GetAllRecords_FilterByDueStatus_DueSoon_ReturnsOnlyDueSoon()
    {
        // Arrange
        // Warning threshold is 30 days
        var recDueSoon = new LeakTestRecord { SourceId = _testSource.Id, TestDate = DateTime.Today.AddMonths(-5), NextDueDate = DateTime.Today.AddDays(15), Result = "Pass" };
        var recFarFuture = new LeakTestRecord { SourceId = _testSource.Id, TestDate = DateTime.Today, NextDueDate = DateTime.Today.AddMonths(6), Result = "Pass" };
        _leakTestService.AddRecord(recDueSoon);
        _leakTestService.AddRecord(recFarFuture);

        // Act
        var dueSoon = _leakTestService.GetAllRecords(dueStatusFilter: "DueSoon");

        // Assert
        Assert.Single(dueSoon);
        Assert.Equal(recDueSoon.Id, dueSoon[0].Id);
    }

    [Fact]
    public void GetAllRecords_SearchText_MatchesSourceCodeOrInspectorOrCert()
    {
        // Arrange
        var rec1 = new LeakTestRecord { SourceId = _testSource.Id, TestDate = DateTime.Today, NextDueDate = DateTime.Today.AddMonths(6), Result = "Pass", InspectorName = "Dr. Sameh", CertificateNumber = "CERT-999" };
        var rec2 = new LeakTestRecord { SourceId = _testSource.Id, TestDate = DateTime.Today, NextDueDate = DateTime.Today.AddMonths(6), Result = "Pass", InspectorName = "Dr. Hani", CertificateNumber = "CERT-111" };
        _leakTestService.AddRecord(rec1);
        _leakTestService.AddRecord(rec2);

        // Act
        var searchByCert = _leakTestService.GetAllRecords(search: "999");
        var searchByInspector = _leakTestService.GetAllRecords(search: "Hani");

        // Assert
        Assert.Single(searchByCert);
        Assert.Equal("CERT-999", searchByCert[0].CertificateNumber);

        Assert.Single(searchByInspector);
        Assert.Equal("Dr. Hani", searchByInspector[0].InspectorName);
    }

    [Fact]
    public void GetLatestRecordBySourceId_ReturnsMostRecentTest()
    {
        // Arrange
        var older = new LeakTestRecord { SourceId = _testSource.Id, TestDate = DateTime.Today.AddMonths(-6), NextDueDate = DateTime.Today, Result = "Pass" };
        var newer = new LeakTestRecord { SourceId = _testSource.Id, TestDate = DateTime.Today, NextDueDate = DateTime.Today.AddMonths(6), Result = "Pass" };
        _leakTestService.AddRecord(older);
        _leakTestService.AddRecord(newer);

        // Act
        var latest = _leakTestService.GetLatestRecordBySourceId(_testSource.Id);

        // Assert
        Assert.NotNull(latest);
        Assert.Equal(newer.Id, latest.Id);
    }

    [Fact]
    public void CalculateNextDueDate_RespectsSystemSettingsInterval()
    {
        try
        {
            // Arrange - Save custom interval in settings
            _settingsService.SaveSetting(SystemSettingsDefaults.LeakTestIntervalMonthsKey, "12");

            // Act
            var baseDate = new DateTime(2026, 3, 1);
            var dueDate = _leakTestService.CalculateNextDueDate(baseDate);

            // Assert
            Assert.Equal(new DateTime(2027, 3, 1), dueDate);
        }
        finally
        {
            // Restore default setting
            _settingsService.SaveSetting(SystemSettingsDefaults.LeakTestIntervalMonthsKey, "6");
        }
    }

    [Fact]
    public void AddAndUpdateAndDeleteRecord_DoNotBroadcastSourcesUpdatedMessage_FromServiceLayer()
    {
        // Arrange
        bool messageReceived = false;
        WeakReferenceMessenger.Default.Register<SourcesUpdatedMessage>(this, (r, m) =>
        {
            messageReceived = true;
        });

        try
        {
            // Act 1: AddRecord
            var record = new LeakTestRecord
            {
                SourceId = _testSource.Id,
                TestDate = DateTime.Today,
                Result = "Pass"
            };
            var addResult = _leakTestService.AddRecord(record);
            Assert.True(addResult.Success);
            Assert.False(messageReceived, "AddRecord should not broadcast SourcesUpdatedMessage from service layer");

            // Act 2: UpdateRecord
            record.Notes = "ملاحظة معدلة";
            var updateResult = _leakTestService.UpdateRecord(record);
            Assert.True(updateResult.Success);
            Assert.False(messageReceived, "UpdateRecord should not broadcast SourcesUpdatedMessage from service layer");

            // Act 3: DeleteRecord
            var deleteResult = _leakTestService.DeleteRecord(record.Id);
            Assert.True(deleteResult.Success);
            Assert.False(messageReceived, "DeleteRecord should not broadcast SourcesUpdatedMessage from service layer");
        }
        finally
        {
            WeakReferenceMessenger.Default.Unregister<SourcesUpdatedMessage>(this);
        }
    }



    #endregion
}


