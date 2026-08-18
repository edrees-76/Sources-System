using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Moq;
using Sources.Data;
using Sources.Models;
using Sources.Services;
using Sources.Tests.Fixtures;
using Sources.Tests.Helpers;
using Xunit;

namespace Sources.Tests;

public class SystemResetServiceTests : IClassFixture<SqliteInMemoryFixture>, IDisposable
{
    private readonly SqliteInMemoryFixture _fixture;
    private readonly Mock<IBackupService> _mockBackupService;
    private readonly Mock<ISystemSettingsService> _mockSettingsService;
    private readonly SystemResetService _sut;

    public SystemResetServiceTests(SqliteInMemoryFixture fixture)
    {
        _fixture = fixture;
        _fixture.ResetDatabase();

        _mockBackupService = new Mock<IBackupService>();
        _mockBackupService.Setup(b => b.CreateBackup())
            .Returns((true, "Backup created", "C:\\Backups\\pre_reset_backup.db"));

        _mockSettingsService = new Mock<ISystemSettingsService>();

        _sut = new SystemResetService(_fixture.ContextFactory, _mockBackupService.Object, _mockSettingsService.Object);
    }

    public void Dispose()
    {
        _fixture.ResetDatabase();
    }

    [Fact]
    public async Task ResetSystemAsync_SuccessfulReset_ClearsTargetTablesAndPreservesCoreEntities()
    {
        // Arrange: Populate core data and transactional data
        var role = new Role { Id = Guid.NewGuid(), RoleName = "مدير النظام" };
        var user = new User { Id = Guid.NewGuid(), Username = "admin", RoleId = role.Id, PasswordHash = "hash" };
        var iso = TestDataBuilder.CreateRadioisotope("Cs-137", "Cesium-137", 30.08, "years", 661.7);
        var unit = TestDataBuilder.CreateActivityUnit("Bq", "Bq", 1.0);
        var loc = TestDataBuilder.CreateLocation(name: "موقع الاختبار");
        var source = TestDataBuilder.CreateSource(iso, unit, loc, sourceCode: "SRC-RESET-01");
        var history = new SourceLocationHistory { Id = Guid.NewGuid(), SourceId = source.Id, LocationId = loc.Id, MovedAt = DateTime.Now };
        var borrow = new BorrowRequest { Id = Guid.NewGuid(), SourceId = source.Id, BorrowerName = "مستعير 1", Status = "Delivered" };
        var alert = new AlertNotification { Id = Guid.NewGuid(), AlertType = "Warning", Severity = "Warning", Message = "نص التنبيه", CreatedAt = DateTime.Now };
        var oldAudit = new AuditLog { Id = Guid.NewGuid(), Action = "Create", TableName = "Sources", UserId = user.Id, ActionDate = DateTime.Now.AddDays(-1) };
        var gamma = new GammaLine { Id = Guid.NewGuid(), RadioisotopeId = iso.Id, Energy = 661.7, Intensity = 85.1 };

        using (var db = _fixture.CreateContext())
        {
            db.Roles.Add(role);
            db.Users.Add(user);
            db.Radioisotopes.Add(iso);
            db.GammaLines.Add(gamma);
            db.ActivityUnits.Add(unit);
            db.Locations.Add(loc);
            db.Sources.Add(source);
            db.SourceLocationHistories.Add(history);
            db.BorrowRequests.Add(borrow);
            db.AlertNotifications.Add(alert);
            db.AuditLogs.Add(oldAudit);

            // إعدادات بقيم مخصصة
            db.AppSettings.Add(new AppSetting { Key = "LowActivityThresholdPercent", Value = "55" });
            db.AppSettings.Add(new AppSetting { Key = "FacilityName", Value = "مختبر مخصص" });
            db.SaveChanges();
        }

        // Act
        var result = await _sut.ResetSystemAsync("admin");

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.BackupPath);

        using (var db = _fixture.CreateContext())
        {
            // 1. الجداول المستهدفة تم تصفيرها بالكامل
            Assert.Empty(db.Sources.ToList());
            Assert.Empty(db.SourceIsotopes.ToList());
            Assert.Empty(db.SourceLocationHistories.ToList());
            Assert.Empty(db.BorrowRequests.ToList());
            Assert.Empty(db.AlertNotifications.ToList());
            Assert.Empty(db.Locations.ToList());

            // 2. الجداول الجوهرية لم تُمس إطلاقاً
            Assert.Single(db.Roles.ToList());
            Assert.Single(db.Users.ToList());
            Assert.Single(db.Radioisotopes.ToList());
            Assert.Single(db.GammaLines.ToList());
            Assert.Single(db.ActivityUnits.ToList());

            // 3. سجل التدقيق يحتوي فقط على سجل التصفير الجديد
            var logs = db.AuditLogs.ToList();
            Assert.Single(logs);
            Assert.Equal("SystemReset", logs[0].Action);
            Assert.Equal(user.Id, logs[0].UserId);
            Assert.Contains("admin", logs[0].Details);
            Assert.Contains("pre_reset_backup.db", logs[0].Details);

            // 4. إعدادات النظام عادت للقيم الافتراضية الموحدة
            var lowActivity = db.AppSettings.Find("LowActivityThresholdPercent");
            var facility = db.AppSettings.Find("FacilityName");
            Assert.NotNull(lowActivity);
            Assert.Equal("10", lowActivity!.Value);
            Assert.NotNull(facility);
            Assert.Equal("", facility!.Value);
        }

        _mockSettingsService.Verify(s => s.ClearCache(), Times.Once);
    }

    [Fact]
    public async Task ResetSystemAsync_BackupFails_AbortsAndPreservesAllData()
    {
        // Arrange
        _mockBackupService.Setup(b => b.CreateBackup())
            .Returns((false, "Disk error: No space left", null));

        var loc = TestDataBuilder.CreateLocation(name: "موقع لا يجب أن يُحذف");
        var iso = TestDataBuilder.CreateRadioisotope("Co-60", "Cobalt-60", 5.27, "years", 1332.5);
        var unit = TestDataBuilder.CreateActivityUnit("Ci", "Ci", 3.7e10);
        var src = TestDataBuilder.CreateSource(iso, unit, loc, "SRC-PRESERVE-01");

        using (var db = _fixture.CreateContext())
        {
            db.Locations.Add(loc);
            db.Radioisotopes.Add(iso);
            db.ActivityUnits.Add(unit);
            db.Sources.Add(src);
            db.SaveChanges();
        }

        // Act
        var result = await _sut.ResetSystemAsync("Admin");

        // Assert
        Assert.False(result.Success);
        Assert.Contains("فشل إنشاء النسخة الاحتياطية", result.Message);

        using (var db = _fixture.CreateContext())
        {
            // لم يُحذف أي شيء
            Assert.Single(db.Sources.ToList());
            Assert.Single(db.Locations.ToList());
        }
    }
}
