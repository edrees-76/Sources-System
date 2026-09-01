using System;
using System.Collections.Generic;
using System.IO;
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
    private readonly Mock<ISourceCertificateService> _mockCertificateService;
    private readonly SystemResetService _sut;
    private string? _tempCertFolder;

    public SystemResetServiceTests(SqliteInMemoryFixture fixture)
    {
        _fixture = fixture;
        _fixture.ResetDatabase();

        _mockBackupService = new Mock<IBackupService>();
        _mockBackupService.Setup(b => b.CreateBackup())
            .Returns((true, "Backup created", "C:\\Backups\\pre_reset_backup.db"));

        _mockSettingsService = new Mock<ISystemSettingsService>();
        _mockCertificateService = new Mock<ISourceCertificateService>();

        _sut = new SystemResetService(
            _fixture.ContextFactory,
            _mockBackupService.Object,
            _mockSettingsService.Object,
            _mockCertificateService.Object);
    }

    public void Dispose()
    {
        _fixture.ResetDatabase();
        if (!string.IsNullOrEmpty(_tempCertFolder) && Directory.Exists(_tempCertFolder))
        {
            try { Directory.Delete(_tempCertFolder, recursive: true); } catch { }
        }
    }

    [Fact]
    public async Task ResetSystemAsync_SuccessfulReset_ClearsTargetTablesAndPreservesCoreEntities()
    {
        // Arrange: Populate core reference data and transactional data including new tables and soft-deleted items
        var role = new Role { Id = Guid.NewGuid(), RoleName = "مدير النظام" };
        var user = new User { Id = Guid.NewGuid(), Username = "admin", RoleId = role.Id, PasswordHash = "hash" };
        var iso = TestDataBuilder.CreateRadioisotope("Cs-137", "Cesium-137", 30.08, "years", 661.7);
        var unit = TestDataBuilder.CreateActivityUnit("Bq", "Bq", 1.0);
        var loc = TestDataBuilder.CreateLocation(name: "موقع الاختبار");
        var locSoftDeleted = TestDataBuilder.CreateLocation(name: "موقع محذوف ناعماً");
        locSoftDeleted.IsDeleted = true;

        var source = TestDataBuilder.CreateSource(iso, unit, loc, sourceCode: "SRC-RESET-01");
        var sourceSoftDeleted = TestDataBuilder.CreateSource(iso, unit, loc, sourceCode: "SRC-RESET-DEL");
        sourceSoftDeleted.IsDeleted = true;

        var history = new SourceLocationHistory { Id = Guid.NewGuid(), SourceId = source.Id, LocationId = loc.Id, MovedAt = DateTime.Now };
        var borrow = new BorrowRequest { Id = Guid.NewGuid(), SourceId = source.Id, BorrowerName = "مستعير 1", Status = "Delivered" };
        var alert = new AlertNotification { Id = Guid.NewGuid(), AlertType = "Warning", Severity = "Warning", Message = "نص التنبيه", CreatedAt = DateTime.Now };
        var oldAudit = new AuditLog { Id = Guid.NewGuid(), Action = "Create", TableName = "Sources", UserId = user.Id, ActionDate = DateTime.Now.AddDays(-1) };
        var gamma = new GammaLine { Id = Guid.NewGuid(), RadioisotopeId = iso.Id, Energy = 661.7, Intensity = 85.1 };

        // New tables: NeutronSourceType (core reference), NeutronSource, LeakTestRecord, SourceCertificate
        var neutronType = new NeutronSourceType
        {
            Id = Guid.NewGuid(),
            Code = "Cf-252-TEST",
            NameEn = "Californium-252 Test",
            NameAr = "كاليفورنيوم-252 تجريبي",
            ReactionType = "Spontaneous Fission",
            HalfLife = 2.645,
            HalfLifeUnit = "years"
        };

        var neutronSource = new NeutronSource
        {
            Id = Guid.NewGuid(),
            SourceCode = "NS-RESET-01",
            NeutronSourceTypeId = neutronType.Id,
            LocationId = loc.Id,
            EmissionRate = 50000.0,
            Status = "Storage"
        };

        var neutronSourceSoftDeleted = new NeutronSource
        {
            Id = Guid.NewGuid(),
            SourceCode = "NS-RESET-DEL",
            NeutronSourceTypeId = neutronType.Id,
            LocationId = loc.Id,
            EmissionRate = 20000.0,
            Status = "Storage",
            IsDeleted = true
        };

        var leakTest = new LeakTestRecord
        {
            Id = Guid.NewGuid(),
            SourceId = source.Id,
            TestDate = DateTime.Today,
            NextDueDate = DateTime.Today.AddMonths(6),
            Result = "Pass",
            MeasuredActivityBq = 12.5,
            PerformedByUserId = user.Id
        };

        var certificate = new SourceCertificate
        {
            Id = Guid.NewGuid(),
            SourceId = source.Id,
            SourceType = "Standard",
            StoredFileName = $"{Guid.NewGuid():N}.pdf",
            OriginalFileName = "Calibration_Cert.pdf",
            AttachedAt = DateTime.Now,
            AttachedBy = "admin"
        };

        using (var db = _fixture.CreateContext())
        {
            // Core reference entities
            db.Roles.Add(role);
            db.Users.Add(user);
            db.Radioisotopes.Add(iso);
            db.GammaLines.Add(gamma);
            db.ActivityUnits.Add(unit);
            db.NeutronSourceTypes.Add(neutronType);

            // Target tables & transactional data
            db.Locations.AddRange(loc, locSoftDeleted);
            db.Sources.AddRange(source, sourceSoftDeleted);
            db.NeutronSources.AddRange(neutronSource, neutronSourceSoftDeleted);
            db.SourceLocationHistories.Add(history);
            db.BorrowRequests.Add(borrow);
            db.AlertNotifications.Add(alert);
            db.LeakTestRecords.Add(leakTest);
            db.SourceCertificates.Add(certificate);
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
            // 1. الجداول المستهدفة تم تصفيرها بالكامل بما في ذلك السجلات المحذوفة ناعماً
            Assert.Empty(db.Sources.IgnoreQueryFilters().ToList());
            Assert.Empty(db.NeutronSources.IgnoreQueryFilters().ToList());
            Assert.Empty(db.Locations.IgnoreQueryFilters().ToList());
            Assert.Empty(db.SourceIsotopes.ToList());
            Assert.Empty(db.SourceLocationHistories.ToList());
            Assert.Empty(db.BorrowRequests.ToList());
            Assert.Empty(db.AlertNotifications.ToList());
            Assert.Empty(db.LeakTestRecords.ToList());
            Assert.Empty(db.SourceCertificates.ToList());

            // 2. الجداول الجوهرية والمرجعية لم تُمس إطلاقاً
            Assert.Single(db.Roles.ToList());
            Assert.Single(db.Users.ToList());
            Assert.Single(db.Radioisotopes.ToList());
            Assert.Single(db.GammaLines.ToList());
            Assert.Single(db.ActivityUnits.ToList());
            Assert.Single(db.NeutronSourceTypes.IgnoreQueryFilters().ToList());

            // 3. سجل التدقيق يحتوي فقط على سجل التصفير الجديد بالتفاصيل المحدثة
            var logs = db.AuditLogs.ToList();
            Assert.Single(logs);
            Assert.Equal("SystemReset", logs[0].Action);
            Assert.Equal(user.Id, logs[0].UserId);
            Assert.Contains("admin", logs[0].Details);
            Assert.Contains("pre_reset_backup.db", logs[0].Details);
            Assert.Contains("المصادر المشعة والنيترونية", logs[0].Details);

            // 4. إعدادات النظام عادت للقيم الافتراضية الموحدة
            var lowActivity = db.AppSettings.Find("LowActivityThresholdPercent");
            var facility = db.AppSettings.Find("FacilityName");
            Assert.NotNull(lowActivity);
            Assert.Equal("10", lowActivity!.Value);
            Assert.NotNull(facility);
            Assert.Equal("", facility!.Value);
        }

        _mockSettingsService.Verify(s => s.ClearCache(), Times.Once);
        _mockCertificateService.Verify(c => c.DeleteAllCertificateFiles(), Times.Once);
    }

    [Fact]
    public async Task ResetSystemAsync_SoftDeletedRecords_AreCompletelyRemoved()
    {
        // Arrange
        var iso = TestDataBuilder.CreateRadioisotope("Am-241", "Americium-241", 432.2, "years", 59.54);
        var unit = TestDataBuilder.CreateActivityUnit("mCi", "mCi", 3.7e7);
        var locActive = TestDataBuilder.CreateLocation(name: "موقع نشط");
        var locDeleted = TestDataBuilder.CreateLocation(name: "موقع محذوف");
        locDeleted.IsDeleted = true;

        var srcActive = TestDataBuilder.CreateSource(iso, unit, locActive, "SRC-ACT-01");
        var srcDeleted = TestDataBuilder.CreateSource(iso, unit, locActive, "SRC-DEL-01");
        srcDeleted.IsDeleted = true;

        var neutronType = new NeutronSourceType
        {
            Id = Guid.NewGuid(),
            Code = "Am-241/Be-TEST",
            NameEn = "Am-Be Test",
            NameAr = "أمريسيوم-بيريليوم",
            ReactionType = "(α,n)",
            HalfLife = 432.2,
            HalfLifeUnit = "years"
        };

        var neutronActive = new NeutronSource
        {
            Id = Guid.NewGuid(),
            SourceCode = "NS-ACT-01",
            NeutronSourceTypeId = neutronType.Id,
            EmissionRate = 1000.0,
            Status = "Storage"
        };

        var neutronDeleted = new NeutronSource
        {
            Id = Guid.NewGuid(),
            SourceCode = "NS-DEL-01",
            NeutronSourceTypeId = neutronType.Id,
            EmissionRate = 2000.0,
            Status = "Storage",
            IsDeleted = true
        };

        using (var db = _fixture.CreateContext())
        {
            db.Radioisotopes.Add(iso);
            db.ActivityUnits.Add(unit);
            db.NeutronSourceTypes.Add(neutronType);
            db.Locations.AddRange(locActive, locDeleted);
            db.Sources.AddRange(srcActive, srcDeleted);
            db.NeutronSources.AddRange(neutronActive, neutronDeleted);
            db.SaveChanges();
        }

        // Act
        var result = await _sut.ResetSystemAsync("admin");

        // Assert
        Assert.True(result.Success);

        using (var db = _fixture.CreateContext())
        {
            Assert.Empty(db.Sources.IgnoreQueryFilters().ToList());
            Assert.Empty(db.Locations.IgnoreQueryFilters().ToList());
            Assert.Empty(db.NeutronSources.IgnoreQueryFilters().ToList());
            // NeutronSourceType is preserved
            Assert.Single(db.NeutronSourceTypes.IgnoreQueryFilters().ToList());
        }
    }

    [Fact]
    public async Task ResetSystemAsync_CertificateFilesOnDisk_DeletesFilesAndPreservesFolder()
    {
        // Arrange
        var testId = Guid.NewGuid().ToString("N");
        _tempCertFolder = Path.Combine(Path.GetTempPath(), $"ResetCertTest_{testId}");
        Directory.CreateDirectory(_tempCertFolder);

        var file1 = Path.Combine(_tempCertFolder, "cert1.pdf");
        var file2 = Path.Combine(_tempCertFolder, "cert2.pdf");
        File.WriteAllText(file1, "Certificate Data 1");
        File.WriteAllText(file2, "Certificate Data 2");

        var auditMock = new Mock<IAuditService>();
        var realCertService = new SourceCertificateService(_fixture.ContextFactory, auditMock.Object, _tempCertFolder);

        var sutWithRealCertService = new SystemResetService(
            _fixture.ContextFactory,
            _mockBackupService.Object,
            _mockSettingsService.Object,
            realCertService);

        var certRecord = new SourceCertificate
        {
            Id = Guid.NewGuid(),
            SourceId = Guid.NewGuid(),
            SourceType = "Standard",
            StoredFileName = "cert1.pdf",
            OriginalFileName = "Original_Cert.pdf",
            AttachedAt = DateTime.Now,
            AttachedBy = "admin"
        };

        using (var db = _fixture.CreateContext())
        {
            db.SourceCertificates.Add(certRecord);
            db.SaveChanges();
        }

        Assert.True(File.Exists(file1));
        Assert.True(File.Exists(file2));

        // Act
        var result = await sutWithRealCertService.ResetSystemAsync("admin");

        // Assert
        Assert.True(result.Success);
        Assert.True(Directory.Exists(_tempCertFolder));
        Assert.Empty(Directory.GetFiles(_tempCertFolder));

        using (var db = _fixture.CreateContext())
        {
            Assert.Empty(db.SourceCertificates.ToList());
        }
    }

    [Fact]
    public async Task ResetSystemAsync_CertificateFileDeletionError_DoesNotFailReset()
    {
        // Arrange: Service that throws on certificate file deletion
        var failingCertService = new Mock<ISourceCertificateService>();
        failingCertService.Setup(c => c.DeleteAllCertificateFiles())
            .Throws(new IOException("Simulated disk error"));

        var sutWithFailingCertService = new SystemResetService(
            _fixture.ContextFactory,
            _mockBackupService.Object,
            _mockSettingsService.Object,
            failingCertService.Object);

        var loc = TestDataBuilder.CreateLocation(name: "موقع الاختبار");
        using (var db = _fixture.CreateContext())
        {
            db.Locations.Add(loc);
            db.SaveChanges();
        }

        // Act
        var result = await sutWithFailingCertService.ResetSystemAsync("admin");

        // Assert: Reset should still succeed despite certificate file deletion failure
        Assert.True(result.Success);

        using (var db = _fixture.CreateContext())
        {
            Assert.Empty(db.Locations.IgnoreQueryFilters().ToList());
        }
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

        var neutronType = new NeutronSourceType
        {
            Id = Guid.NewGuid(),
            Code = "Cf-252-PRESERVE",
            NameEn = "Cf-252 Preserve",
            NameAr = "كاليفورنيوم-252",
            ReactionType = "Spontaneous Fission",
            HalfLife = 2.645,
            HalfLifeUnit = "years"
        };

        var neutronSrc = new NeutronSource
        {
            Id = Guid.NewGuid(),
            SourceCode = "NS-PRESERVE-01",
            NeutronSourceTypeId = neutronType.Id,
            EmissionRate = 5000.0,
            Status = "Storage"
        };

        var leakTest = new LeakTestRecord
        {
            Id = Guid.NewGuid(),
            SourceId = src.Id,
            TestDate = DateTime.Today,
            NextDueDate = DateTime.Today.AddMonths(6),
            Result = "Pass"
        };

        var cert = new SourceCertificate
        {
            Id = Guid.NewGuid(),
            SourceId = src.Id,
            SourceType = "Standard",
            StoredFileName = "cert.pdf",
            OriginalFileName = "cert.pdf"
        };

        using (var db = _fixture.CreateContext())
        {
            db.Locations.Add(loc);
            db.Radioisotopes.Add(iso);
            db.ActivityUnits.Add(unit);
            db.Sources.Add(src);
            db.NeutronSourceTypes.Add(neutronType);
            db.NeutronSources.Add(neutronSrc);
            db.LeakTestRecords.Add(leakTest);
            db.SourceCertificates.Add(cert);
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
            Assert.Single(db.Sources.IgnoreQueryFilters().ToList());
            Assert.Single(db.Locations.IgnoreQueryFilters().ToList());
            Assert.Single(db.NeutronSources.IgnoreQueryFilters().ToList());
            Assert.Single(db.NeutronSourceTypes.IgnoreQueryFilters().ToList());
            Assert.Single(db.LeakTestRecords.ToList());
            Assert.Single(db.SourceCertificates.ToList());
        }

        _mockCertificateService.Verify(c => c.DeleteAllCertificateFiles(), Times.Never);
    }
}
