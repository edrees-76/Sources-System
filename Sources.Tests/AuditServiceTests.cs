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
using Xunit;

namespace Sources.Tests;

public class AuditServiceTests : IClassFixture<SqliteInMemoryFixture>, IDisposable
{
    private readonly SqliteInMemoryFixture _fixture;
    private readonly FakeUserService _fakeUserService;
    private readonly AuditService _sut;
    private User _defaultUser = null!;

    public AuditServiceTests(SqliteInMemoryFixture fixture)
    {
        _fixture = fixture;
        _fixture.ResetDatabase();

        _defaultUser = SeedUser("admin");
        _fakeUserService = new FakeUserService(_defaultUser);
        _sut = new AuditService(_fixture.ContextFactory, _fakeUserService);
    }

    public void Dispose()
    {
        _fixture.ResetDatabase();
    }

    private User SeedUser(string username = "testuser")
    {
        using var db = _fixture.CreateContext();
        var role = db.Roles.FirstOrDefault();
        if (role == null)
        {
            role = new Role { Id = Guid.NewGuid(), RoleName = "مدير", Description = "مدير" };
            db.Roles.Add(role);
            db.SaveChanges();
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = username + "_" + Guid.NewGuid().ToString("N")[..6],
            FullName = "مستخدم " + username,
            PasswordHash = "hash",
            RoleId = role.Id,
            IsActive = true
        };
        db.Users.Add(user);
        db.SaveChanges();
        return user;
    }

    #region 1. Log and LogWithChanges Tests

    [Fact]
    public void Log_WithValidData_CreatesAuditLogWithCorrectFields()
    {
        // Arrange
        var recordId = Guid.NewGuid();
        var beforeTime = DateTime.Now.AddSeconds(-2);

        // Act
        _sut.Log("Create", "Sources", recordId, "إنشاء مصدر جديد SRC-100");

        // Assert
        using var db = _fixture.CreateContext();
        var log = db.AuditLogs.FirstOrDefault();

        Assert.NotNull(log);
        Assert.NotEqual(Guid.Empty, log.Id);
        Assert.Equal(_fakeUserService.CurrentUser!.Id, log.UserId);
        Assert.Equal("Create", log.Action);
        Assert.Equal("Sources", log.TableName);
        Assert.Equal(recordId, log.RecordId);
        Assert.Equal("إنشاء مصدر جديد SRC-100", log.Details);
        Assert.Null(log.OldValues);
        Assert.Null(log.NewValues);
        Assert.True(log.ActionDate >= beforeTime && log.ActionDate <= DateTime.Now.AddSeconds(2));
    }

    [Fact]
    public void LogWithChanges_WithOldAndNewValues_SavesValuesCorrectly()
    {
        // Arrange
        var recordId = Guid.NewGuid();
        var oldJson = "{\"Status\":\"Available\"}";
        var newJson = "{\"Status\":\"InUse\"}";

        // Act
        _sut.LogWithChanges("Update", "Sources", recordId, "تغيير حالة المصدر", oldJson, newJson);

        // Assert
        using var db = _fixture.CreateContext();
        var log = db.AuditLogs.FirstOrDefault();

        Assert.NotNull(log);
        Assert.Equal("Update", log.Action);
        Assert.Equal("Sources", log.TableName);
        Assert.Equal(recordId, log.RecordId);
        Assert.Equal("تغيير حالة المصدر", log.Details);
        Assert.Equal(oldJson, log.OldValues);
        Assert.Equal(newJson, log.NewValues);
    }

    [Fact]
    public void Log_WhenNoUserLoggedIn_SetsUserIdToNull()
    {
        // Arrange
        _fakeUserService.CurrentUser = null;

        // Act
        _sut.Log("System", "Settings", null, "عملية تلقائية للنظام");

        // Assert
        using var db = _fixture.CreateContext();
        var log = db.AuditLogs.FirstOrDefault();

        Assert.NotNull(log);
        Assert.Null(log.UserId);
        Assert.Equal("System", log.Action);
        Assert.Equal("Settings", log.TableName);
        Assert.Null(log.RecordId);
        Assert.Equal("عملية تلقائية للنظام", log.Details);
    }

    [Fact]
    public void Log_WithGuidEmptyAndNullParameters_SavesSuccessfully()
    {
        // Act
        _sut.Log("System", null, Guid.Empty, null);

        // Assert
        using var db = _fixture.CreateContext();
        var log = db.AuditLogs.FirstOrDefault();

        Assert.NotNull(log);
        Assert.Equal("System", log.Action);
        Assert.Null(log.TableName);
        Assert.Equal(Guid.Empty, log.RecordId);
        Assert.Null(log.Details);
    }

    [Fact]
    public void Log_WhenDatabaseFails_DoesNotThrowAndSwallowsExceptionSafely()
    {
        // Arrange: DbContextFactory throwing exception
        var mockFactory = new Mock<IDbContextFactory<AppDbContext>>();
        mockFactory.Setup(f => f.CreateDbContext()).Throws(new InvalidOperationException("Simulated DB Connection Failure"));

        var faultTolerantService = new AuditService(mockFactory.Object, _fakeUserService);

        // Act & Assert: Must not throw to the caller
        var exception = Record.Exception(() =>
        {
            faultTolerantService.Log("Delete", "Sources", Guid.NewGuid(), "حذف مصدر تجريبي");
        });

        Assert.Null(exception);
    }

    #endregion

    #region 2. GetAuditLogs and GetTotalCount Tests

    [Fact]
    public void GetAuditLogs_DefaultPagination_ReturnsFirstPageWithCorrectPageSize()
    {
        // Arrange: Seed 60 logs
        using (var db = _fixture.CreateContext())
        {
            for (int i = 1; i <= 60; i++)
            {
                db.AuditLogs.Add(new AuditLog
                {
                    Action = "Action_" + i,
                    TableName = "Sources",
                    ActionDate = DateTime.Now.AddMinutes(-i),
                    Details = $"Log details {i}"
                });
            }
            db.SaveChanges();
        }

        // Act
        var page1 = _sut.GetAuditLogs(page: 1, pageSize: 50);
        var page2 = _sut.GetAuditLogs(page: 2, pageSize: 50);
        var totalCount = _sut.GetTotalCount();

        // Assert
        Assert.Equal(50, page1.Count);
        Assert.Equal(10, page2.Count);
        Assert.Equal(60, totalCount);
    }

    [Fact]
    public void GetAuditLogs_OrdersByActionDateDescending()
    {
        // Arrange: Seed logs with different dates
        var now = DateTime.Now;
        using (var db = _fixture.CreateContext())
        {
            db.AuditLogs.AddRange(
                new AuditLog { Action = "Oldest", ActionDate = now.AddDays(-5), Details = "1" },
                new AuditLog { Action = "Newest", ActionDate = now.AddMinutes(-5), Details = "3" },
                new AuditLog { Action = "Middle", ActionDate = now.AddDays(-2), Details = "2" }
            );
            db.SaveChanges();
        }

        // Act
        var logs = _sut.GetAuditLogs();

        // Assert
        Assert.Equal(3, logs.Count);
        Assert.Equal("Newest", logs[0].Action);
        Assert.Equal("Middle", logs[1].Action);
        Assert.Equal("Oldest", logs[2].Action);
    }

    [Fact]
    public void GetAuditLogs_FilterByAction_ReturnsMatchingOnly()
    {
        // Arrange
        using (var db = _fixture.CreateContext())
        {
            db.AuditLogs.AddRange(
                new AuditLog { Action = "Create", TableName = "Sources", ActionDate = DateTime.Now },
                new AuditLog { Action = "Update", TableName = "Sources", ActionDate = DateTime.Now },
                new AuditLog { Action = "Update", TableName = "Locations", ActionDate = DateTime.Now },
                new AuditLog { Action = "Delete", TableName = "Sources", ActionDate = DateTime.Now }
            );
            db.SaveChanges();
        }

        // Act
        var updateLogs = _sut.GetAuditLogs(actionFilter: "Update");
        var updateCount = _sut.GetTotalCount(actionFilter: "Update");

        // Assert
        Assert.Equal(2, updateLogs.Count);
        Assert.All(updateLogs, l => Assert.Equal("Update", l.Action));
        Assert.Equal(2, updateCount);
    }

    [Fact]
    public void GetAuditLogs_FilterByUser_ReturnsMatchingOnly()
    {
        // Arrange
        var user1 = SeedUser("user1");
        var user2 = SeedUser("user2");

        using (var db = _fixture.CreateContext())
        {
            db.AuditLogs.AddRange(
                new AuditLog { Action = "Action1", UserId = user1.Id, ActionDate = DateTime.Now },
                new AuditLog { Action = "Action2", UserId = user2.Id, ActionDate = DateTime.Now },
                new AuditLog { Action = "Action3", UserId = user1.Id, ActionDate = DateTime.Now },
                new AuditLog { Action = "Action4", UserId = null, ActionDate = DateTime.Now }
            );
            db.SaveChanges();
        }

        // Act
        var user1Logs = _sut.GetAuditLogs(userFilter: user1.Id);
        var user1Count = _sut.GetTotalCount(userFilter: user1.Id);

        // Assert
        Assert.Equal(2, user1Logs.Count);
        Assert.All(user1Logs, l => Assert.Equal(user1.Id, l.UserId));
        Assert.Equal(2, user1Count);
    }

    [Fact]
    public void GetAuditLogs_FilterByFromDate_ReturnsMatchingOnly()
    {
        // Arrange
        var baseDate = new DateTime(2026, 8, 10);
        using (var db = _fixture.CreateContext())
        {
            db.AuditLogs.AddRange(
                new AuditLog { Action = "Before", ActionDate = new DateTime(2026, 8, 5, 12, 0, 0) },
                new AuditLog { Action = "ExactFrom", ActionDate = new DateTime(2026, 8, 10, 0, 0, 0) },
                new AuditLog { Action = "After", ActionDate = new DateTime(2026, 8, 15, 12, 0, 0) }
            );
            db.SaveChanges();
        }

        // Act
        var logs = _sut.GetAuditLogs(fromDate: baseDate);
        var count = _sut.GetTotalCount(fromDate: baseDate);

        // Assert
        Assert.Equal(2, logs.Count);
        Assert.DoesNotContain(logs, l => l.Action == "Before");
        Assert.Equal(2, count);
    }

    [Fact]
    public void GetAuditLogs_FilterByToDate_IncludesAfternoonLogsOnSameDay()
    {
        // Arrange: Explicit test for the toDate full-day fix
        var targetDay = new DateTime(2026, 8, 17); // 2026-08-17 00:00:00

        using (var db = _fixture.CreateContext())
        {
            db.AuditLogs.AddRange(
                new AuditLog { Action = "MorningLog", ActionDate = new DateTime(2026, 8, 17, 8, 30, 0) },
                new AuditLog { Action = "AfternoonLog", ActionDate = new DateTime(2026, 8, 17, 15, 45, 0) },
                new AuditLog { Action = "NightLog", ActionDate = new DateTime(2026, 8, 17, 23, 30, 0) },
                new AuditLog { Action = "NextDayLog", ActionDate = new DateTime(2026, 8, 18, 0, 1, 0) }
            );
            db.SaveChanges();
        }

        // Act: Filter with toDate = targetDay (2026-08-17)
        var logs = _sut.GetAuditLogs(toDate: targetDay);
        var count = _sut.GetTotalCount(toDate: targetDay);

        // Assert: Morning, Afternoon, and Night of 2026-08-17 must ALL be included; NextDayLog must NOT
        Assert.Equal(3, logs.Count);
        Assert.Contains(logs, l => l.Action == "MorningLog");
        Assert.Contains(logs, l => l.Action == "AfternoonLog");
        Assert.Contains(logs, l => l.Action == "NightLog");
        Assert.DoesNotContain(logs, l => l.Action == "NextDayLog");
        Assert.Equal(3, count);
    }

    [Fact]
    public void GetAuditLogs_CombinedFilters_FiltersCorrectly()
    {
        // Arrange
        var user1 = SeedUser("user1");
        var user2 = SeedUser("user2");
        var startDate = new DateTime(2026, 8, 10);
        var endDate = new DateTime(2026, 8, 12);

        using (var db = _fixture.CreateContext())
        {
            db.AuditLogs.AddRange(
                // Matches all criteria:
                new AuditLog { Action = "Update", UserId = user1.Id, TableName = "Sources", ActionDate = new DateTime(2026, 8, 11, 14, 0, 0) },
                // Different action:
                new AuditLog { Action = "Create", UserId = user1.Id, TableName = "Sources", ActionDate = new DateTime(2026, 8, 11, 14, 0, 0) },
                // Different user:
                new AuditLog { Action = "Update", UserId = user2.Id, TableName = "Sources", ActionDate = new DateTime(2026, 8, 11, 14, 0, 0) },
                // Out of date range:
                new AuditLog { Action = "Update", UserId = user1.Id, TableName = "Sources", ActionDate = new DateTime(2026, 8, 15, 14, 0, 0) }
            );
            db.SaveChanges();
        }

        // Act
        var logs = _sut.GetAuditLogs(actionFilter: "Update", userFilter: user1.Id, fromDate: startDate, toDate: endDate);
        var count = _sut.GetTotalCount(actionFilter: "Update", userFilter: user1.Id, fromDate: startDate, toDate: endDate);

        // Assert
        Assert.Single(logs);
        Assert.Equal(user1.Id, logs[0].UserId);
        Assert.Equal("Update", logs[0].Action);
        Assert.Equal(1, count);
    }

    #endregion

    #region 3. Soft Delete & Query Filter Behavior Tests

    [Fact]
    public void GetAuditLogs_WhenUserIsSoftDeleted_LogRemainsVisibleWithNullUserNavigation()
    {
        // Arrange
        var user = SeedUser("softdeleteduser");

        using (var db = _fixture.CreateContext())
        {
            db.AuditLogs.Add(new AuditLog
            {
                Action = "Delete",
                TableName = "Sources",
                UserId = user.Id,
                ActionDate = DateTime.Now,
                Details = "حذف مصدر"
            });
            db.SaveChanges();
        }

        // Soft delete the user
        using (var db = _fixture.CreateContext())
        {
            var dbUser = db.Users.Find(user.Id);
            Assert.NotNull(dbUser);
            dbUser.IsDeleted = true;
            db.SaveChanges();
        }

        // Act
        var logs = _sut.GetAuditLogs();

        // Assert: Log still exists, UserId intact, but User navigation is null due to EF Global Query Filter
        Assert.Single(logs);
        var log = logs[0];
        Assert.Equal(user.Id, log.UserId);
        Assert.Null(log.User); // Filtered out by User global query filter
    }

    #endregion

    #region 4. Unification with UserService.GetAuditLogs Tests

    [Fact]
    public void UserService_GetAuditLogs_DelegatesToAuditServiceAndReturnsMatchingResults()
    {
        // Arrange
        var user1 = SeedUser("u1");
        var user2 = SeedUser("u2");
        var date1 = new DateTime(2026, 8, 15, 10, 0, 0);
        var date2 = new DateTime(2026, 8, 16, 16, 0, 0);

        using (var db = _fixture.CreateContext())
        {
            db.AuditLogs.AddRange(
                new AuditLog { Action = "ActionA", UserId = user1.Id, ActionDate = date1, Details = "A" },
                new AuditLog { Action = "ActionB", UserId = user1.Id, ActionDate = date2, Details = "B" },
                new AuditLog { Action = "ActionC", UserId = user2.Id, ActionDate = date1, Details = "C" }
            );
            db.SaveChanges();
        }

        var userService = new UserService(_fixture.ContextFactory, _sut);

        // Act: Query through UserService
        var userLogs = userService.GetAuditLogs(userId: user1.Id, from: new DateTime(2026, 8, 15), to: new DateTime(2026, 8, 16));
        var auditLogs = _sut.GetAuditLogs(page: 1, pageSize: 200, userFilter: user1.Id, fromDate: new DateTime(2026, 8, 15), toDate: new DateTime(2026, 8, 16));

        // Assert: Same count, same items, same order
        Assert.Equal(2, userLogs.Count);
        Assert.Equal(auditLogs.Count, userLogs.Count);
        Assert.Equal(auditLogs[0].Id, userLogs[0].Id);
        Assert.Equal(auditLogs[1].Id, userLogs[1].Id);
    }

    [Fact]
    public void UserService_GetAuditLogs_WithInjectedAuditService_CallsAuditServiceWithCorrectParameters()
    {
        // Arrange
        var mockAuditService = new Mock<IAuditService>();
        var expectedUserId = Guid.NewGuid();
        var expectedFrom = new DateTime(2026, 8, 1);
        var expectedTo = new DateTime(2026, 8, 17);
        var expectedLogs = new List<AuditLog>
        {
            new AuditLog { Id = Guid.NewGuid(), Action = "Test" }
        };

        mockAuditService
            .Setup(s => s.GetAuditLogs(1, 200, null, expectedUserId, expectedFrom, expectedTo))
            .Returns(expectedLogs);

        var userService = new UserService(_fixture.ContextFactory, mockAuditService.Object);

        // Act
        var result = userService.GetAuditLogs(userId: expectedUserId, from: expectedFrom, to: expectedTo);

        // Assert
        Assert.Same(expectedLogs, result);
        mockAuditService.Verify(s => s.GetAuditLogs(1, 200, null, expectedUserId, expectedFrom, expectedTo), Times.Once);
    }

    #endregion
}
