using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Moq;
using Sources.Data;
using Sources.Models;
using Sources.Services;
using Sources.Tests.Fixtures;
using Sources.Tests.Helpers;
using Xunit;

namespace Sources.Tests;

public class AlertServiceTests : IClassFixture<SqliteInMemoryFixture>, IDisposable
{
    private readonly SqliteInMemoryFixture _fixture;
    private readonly DecayCalculationService _decayService;
    private readonly ISystemSettingsService _settingsService;
    private readonly AlertService _alertService;

    // كائنات مرجعية مشتركة
    private Radioisotope _isoCs137 = null!; // T½ = 30.08 years
    private Radioisotope _isoCo60 = null!;  // T½ = 5.27 years
    private Radioisotope _isoI131 = null!;  // T½ = 8.02 days
    private ActivityUnit _unitBq = null!;
    private Location _testLocation = null!;

    public AlertServiceTests(SqliteInMemoryFixture fixture)
    {
        _fixture = fixture;
        _fixture.ResetDatabase();

        _decayService = new DecayCalculationService();
        _settingsService = new SystemSettingsService(_fixture.ContextFactory);

        _alertService = new AlertService(
            _fixture.ContextFactory,
            _decayService,
            _settingsService);

        SeedLookupData();
    }

    private void SeedLookupData()
    {
        using var context = _fixture.CreateContext();

        _isoCs137 = TestDataBuilder.CreateRadioisotope("Cs-137", "Cesium-137", 30.08, "years", 661.7);
        _isoCo60 = TestDataBuilder.CreateRadioisotope("Co-60", "Cobalt-60", 5.27, "years", 1332.5);
        _isoI131 = TestDataBuilder.CreateRadioisotope("I-131", "Iodine-131", 8.02, "days", 364.5);

        _unitBq = TestDataBuilder.CreateActivityUnit("Becquerel", "Bq", 1.0);
        _testLocation = TestDataBuilder.CreateLocation("مختبر المعايرة", "Lab", "المبنى B", "201");

        context.Radioisotopes.AddRange(_isoCs137, _isoCo60, _isoI131);
        context.ActivityUnits.Add(_unitBq);
        context.Locations.Add(_testLocation);

        context.SaveChanges();
    }

    public void Dispose()
    {
        // تنظيف بعد انتهاء الاختبار
    }

    #region 1. تصنيف المصادر حسب فترات نصف العمر (Half-Life Thresholds)

    [Theory]
    [InlineData(1.0, "None")]     // 1 فترة نصف عمر -> نشاط متبقي 50% (لا يوجد تنبيه)
    [InlineData(4.99, "None")]    // 4.99 فترة نصف عمر -> نشاط متبقي ~3.15% (لا يوجد تنبيه)
    [InlineData(5.0, "Warning")]   // 5.0 فترات نصف عمر -> نشاط متبقي 3.125% (تنبيه تحذيري)
    [InlineData(5.5, "Warning")]   // 5.5 فترات نصف عمر -> تنبيه تحذيري
    [InlineData(5.99, "Warning")]  // 5.99 فترات نصف عمر -> تنبيه تحذيري
    [InlineData(6.0, "Critical")]  // 6.0 فترات نصف عمر -> نشاط متبقي 1.5625% (تنبيه حرج)
    [InlineData(10.0, "Critical")] // 10 فترات نصف عمر -> نشاط متبقي < 0.1% (تنبيه حرج)
    public void GenerateAlerts_SingleIsotopeSource_ClassifiesSeverityAccurately(double halfLivesElapsed, string expectedSeverity)
    {
        // Arrange
        // Co-60 فترة نصف عمره 5.27 سنة (1924.8675 يوم)
        var halfLifeDays = 5.27 * 365.25;
        var elapsedDays = halfLivesElapsed * halfLifeDays;
        var calibrationDate = DateTime.Now.AddDays(-elapsedDays);

        var source = TestDataBuilder.CreateSource(
            _isoCo60,
            _unitBq,
            _testLocation,
            sourceCode: $"SRC-HL-{halfLivesElapsed}",
            calibrationDate: calibrationDate,
            status: "InUse");

        using (var context = _fixture.CreateContext())
        {
            context.Sources.Add(source);
            context.SaveChanges();
        }

        // Act
        var alerts = _alertService.GenerateAlerts();
        var sourceAlert = alerts.FirstOrDefault(a => a.SourceId == source.Id);

        // Assert
        if (expectedSeverity == "None")
        {
            Assert.Null(sourceAlert);
        }
        else
        {
            Assert.NotNull(sourceAlert);
            Assert.Equal(expectedSeverity, sourceAlert.Severity);
            Assert.Equal("LowActivity", sourceAlert.AlertType);
            Assert.Contains(source.SourceCode, sourceAlert.Message);
            Assert.Contains(_isoCo60.Symbol, sourceAlert.Message);
        }
    }

    #endregion

    #region 2. المصادر متعددة النظائر (Multi-Isotope Worst-Case Logic)

    [Fact]
    public void GenerateAlerts_MultiIsotopeSource_UsesFastestDecayingWorstCaseIsotope()
    {
        // Arrange
        // مصدر يحتوي على نظيرين:
        // 1. Cs-137: T½ = 30.08 سنة (انقضى سنتان = 0.066 T½ -> لا تنبيه)
        // 2. Co-60:  T½ = 5.27 سنة (انقضى 31.62 سنة = 6.0 T½ -> Critical)
        var now = DateTime.Now;
        var source = TestDataBuilder.CreateSource(
            _isoCs137,
            _unitBq,
            _testLocation,
            sourceCode: "SRC-MULTI-WORST",
            calibrationDate: now.AddDays(-2 * 365.25),
            hasDetailedIsotopes: true);

        var isoCs = TestDataBuilder.CreateSourceIsotope(
            source,
            _isoCs137,
            _unitBq,
            initialActivity: 1000.0,
            calibrationDate: now.AddDays(-2 * 365.25)); // 2 years ago

        var isoCo = TestDataBuilder.CreateSourceIsotope(
            source,
            _isoCo60,
            _unitBq,
            initialActivity: 2000.0,
            calibrationDate: now.AddDays(-31.62 * 365.25)); // 6.0 half-lives ago

        using (var context = _fixture.CreateContext())
        {
            context.Sources.Add(source);
            context.SourceIsotopes.AddRange(isoCs, isoCo);
            context.SaveChanges();
        }

        // Act
        var alerts = _alertService.GenerateAlerts();
        var alert = alerts.FirstOrDefault(a => a.SourceId == source.Id);

        // Assert
        // يجب أن يعتمد التنبيه على أسوأ نظير (Co-60) ويكون تصنيفه حرج Critical
        Assert.NotNull(alert);
        Assert.Equal("Critical", alert.Severity);
        Assert.Contains("Co-60", alert.Message);
        Assert.DoesNotContain("Cs-137", alert.Message);
    }

    [Fact]
    public void GenerateAlerts_MultiIsotopeSource_SelectsWarningWhenWorstIsotopeIsBetween5And6HalfLives()
    {
        // Arrange
        // نظير 1: Cs-137 (انقضى 1 سنة = 0.03 T½ -> None)
        // نظير 2: Co-60  (انقضى 5.3 فترة نصف عمر = 27.93 سنة -> Warning)
        var now = DateTime.Now;
        var source = TestDataBuilder.CreateSource(
            _isoCs137,
            _unitBq,
            _testLocation,
            sourceCode: "SRC-MULTI-WARN",
            hasDetailedIsotopes: true);

        var isoCs = TestDataBuilder.CreateSourceIsotope(
            source, _isoCs137, _unitBq, 1000.0, now.AddDays(-365));

        var isoCo = TestDataBuilder.CreateSourceIsotope(
            source, _isoCo60, _unitBq, 1000.0, now.AddDays(-5.3 * 5.27 * 365.25));

        using (var context = _fixture.CreateContext())
        {
            context.Sources.Add(source);
            context.SourceIsotopes.AddRange(isoCs, isoCo);
            context.SaveChanges();
        }

        // Act
        var alerts = _alertService.GenerateAlerts();
        var alert = alerts.FirstOrDefault(a => a.SourceId == source.Id);

        // Assert
        Assert.NotNull(alert);
        Assert.Equal("Warning", alert.Severity);
        Assert.Contains("Co-60", alert.Message);
    }

    #endregion

    #region 3. استبعاد المصادر المحذوفة ناعماً (Soft-Deleted Sources)

    [Fact]
    public void GenerateAlerts_SoftDeletedSource_DoesNotAppearInAlerts()
    {
        // Arrange
        // مصدر منقضٍ عليه 10 فترات نصف عمر (حرج جداً)، ولكنه محذوف ناعماً (IsDeleted = true)
        var source = TestDataBuilder.CreateSource(
            _isoCo60,
            _unitBq,
            _testLocation,
            sourceCode: "SRC-DELETED-ALERT",
            calibrationDate: DateTime.Now.AddDays(-10 * 5.27 * 365.25),
            status: "InUse");

        source.IsDeleted = true;

        using (var context = _fixture.CreateContext())
        {
            context.Sources.Add(source);
            context.SaveChanges();
        }

        // Act
        var alerts = _alertService.GenerateAlerts();
        var alert = alerts.FirstOrDefault(a => a.SourceId == source.Id);

        // Assert
        Assert.Null(alert);
    }

    #endregion

    #region 4. حالة عدم وجود أي تنبيهات حرجة (Zero Low-Activity Sources)

    [Fact]
    public void GenerateAlerts_WhenNoSourcesAreLowActivity_ReturnsEmptyListWithoutException()
    {
        // Arrange
        // كل المصادر تمت معايرتها اليوم (0 فترات نصف عمر)
        var source1 = TestDataBuilder.CreateSource(_isoCs137, _unitBq, _testLocation, "SRC-FRESH-1", calibrationDate: DateTime.Now);
        var source2 = TestDataBuilder.CreateSource(_isoCo60, _unitBq, _testLocation, "SRC-FRESH-2", calibrationDate: DateTime.Now);

        using (var context = _fixture.CreateContext())
        {
            context.Sources.AddRange(source1, source2);
            context.SaveChanges();
        }

        // Act
        var alerts = _alertService.GenerateAlerts();

        // Assert
        Assert.NotNull(alerts);
        Assert.Empty(alerts);
    }

    #endregion

    #region 5. تصفية حالات المصادر (Status Filtering: InUse vs Storage vs Waste vs Transfer)

    [Theory]
    [InlineData("InUse", true)]      // قيد الاستخدام -> مشمول بالتنبيه
    [InlineData("Storage", true)]    // في المخزن -> مشمول بالتنبيه
    [InlineData("Waste", false)]     // نفايات -> غير مشمول بالتنبيه
    [InlineData("Transfer", false)]  // قيد النقل -> غير مشمول بالتنبيه
    public void GenerateAlerts_FiltersSourcesByStatus(string status, bool shouldAlert)
    {
        // Arrange
        // مصدر منقضٍ عليه 8 فترات نصف عمر (Critical)
        var source = TestDataBuilder.CreateSource(
            _isoCo60,
            _unitBq,
            _testLocation,
            sourceCode: $"SRC-STAT-{status}",
            calibrationDate: DateTime.Now.AddDays(-8 * 5.27 * 365.25),
            status: status);

        using (var context = _fixture.CreateContext())
        {
            context.Sources.Add(source);
            context.SaveChanges();
        }

        // Act
        var alerts = _alertService.GenerateAlerts();
        var alert = alerts.FirstOrDefault(a => a.SourceId == source.Id);

        // Assert
        if (shouldAlert)
        {
            Assert.NotNull(alert);
            Assert.Equal("Critical", alert.Severity);
        }
        else
        {
            Assert.Null(alert);
        }
    }

    #endregion

    #region 6. إدارة ودورة حياة التنبيهات (Read, Dismiss, UnreadCount, Ordering)

    [Fact]
    public void AlertService_ManagesReadAndDismissStateCorrectly()
    {
        // Arrange
        var source = TestDataBuilder.CreateSource(
            _isoCo60,
            _unitBq,
            _testLocation,
            sourceCode: "SRC-LIFECYCLE-1",
            calibrationDate: DateTime.Now.AddDays(-7 * 5.27 * 365.25));

        using (var context = _fixture.CreateContext())
        {
            context.Sources.Add(source);
            context.SaveChanges();
        }

        // Act & Assert 1: توليد التنبيه
        var alerts = _alertService.GenerateAlerts();
        Assert.Single(alerts);
        var alertId = alerts[0].Id;

        // التحقق من عدد التنبيهات غير المقروءة
        var unreadCount = _alertService.GetUnreadCount();
        Assert.Equal(1, unreadCount);

        // Act & Assert 2: تعليم التنبيه كمقروء
        _alertService.MarkAsRead(alertId);
        Assert.Equal(0, _alertService.GetUnreadCount());
        Assert.Single(_alertService.GetActiveAlerts()); // ما زال نشطاً ولم يُخفَ

        // Act & Assert 3: إخفاء التنبيه (Dismiss)
        _alertService.DismissAlert(alertId);
        Assert.Empty(_alertService.GetActiveAlerts());
        Assert.Equal(0, _alertService.GetUnreadCount());
    }

    [Fact]
    public void MarkAllAsRead_MarksAllActiveAlertsAsRead()
    {
        // Arrange
        var src1 = TestDataBuilder.CreateSource(_isoCo60, _unitBq, _testLocation, "SRC-READ-1", calibrationDate: DateTime.Now.AddDays(-7 * 5.27 * 365.25));
        var src2 = TestDataBuilder.CreateSource(_isoCo60, _unitBq, _testLocation, "SRC-READ-2", calibrationDate: DateTime.Now.AddDays(-5.5 * 5.27 * 365.25));

        using (var context = _fixture.CreateContext())
        {
            context.Sources.AddRange(src1, src2);
            context.SaveChanges();
        }

        _alertService.GenerateAlerts();
        Assert.Equal(2, _alertService.GetUnreadCount());

        // Act
        _alertService.MarkAllAsRead();

        // Assert
        Assert.Equal(0, _alertService.GetUnreadCount());
        Assert.Equal(2, _alertService.GetActiveAlerts().Count);
    }

    [Fact]
    public void GetActiveAlerts_OrdersCriticalAlertsBeforeWarningAlerts()
    {
        // Arrange
        // مصدر حرج (7 فترات نصف عمر)
        var srcCritical = TestDataBuilder.CreateSource(_isoCo60, _unitBq, _testLocation, "SRC-CRIT", calibrationDate: DateTime.Now.AddDays(-7 * 5.27 * 365.25));
        // مصدر تحذيري (5.2 فترات نصف عمر)
        var srcWarning = TestDataBuilder.CreateSource(_isoCo60, _unitBq, _testLocation, "SRC-WARN", calibrationDate: DateTime.Now.AddDays(-5.2 * 5.27 * 365.25));

        using (var context = _fixture.CreateContext())
        {
            context.Sources.AddRange(srcWarning, srcCritical);
            context.SaveChanges();
        }

        _alertService.GenerateAlerts();

        // Act
        var activeAlerts = _alertService.GetActiveAlerts();

        // Assert
        Assert.Equal(2, activeAlerts.Count);
        Assert.Equal("Critical", activeAlerts[0].Severity);
        Assert.Equal("Warning", activeAlerts[1].Severity);
    }

    [Fact]
    public void GenerateAlerts_CleansResolvedAlertsWhenSourceIsRecalibrated()
    {
        // Arrange
        // 1. مصدر قديم يولد تنبيهاً حرجاً
        var source = TestDataBuilder.CreateSource(
            _isoCo60,
            _unitBq,
            _testLocation,
            sourceCode: "SRC-RESOLVE-TEST",
            calibrationDate: DateTime.Now.AddDays(-8 * 5.27 * 365.25));

        using (var context = _fixture.CreateContext())
        {
            context.Sources.Add(source);
            context.SaveChanges();
        }

        var initialAlerts = _alertService.GenerateAlerts();
        Assert.Single(initialAlerts);

        // 2. تحديث تاريخ المعايرة للمصدر ليكون حديثاً جداً (تم حل المشكلة)
        using (var context = _fixture.CreateContext())
        {
            var dbSource = context.Sources.Find(source.Id);
            Assert.NotNull(dbSource);
            dbSource.CalibrationDate = DateTime.Now;
            context.SaveChanges();
        }

        // Act - إعادة توليد التنبيهات
        var updatedAlerts = _alertService.GenerateAlerts();

        // Assert - يجب أن يُحذف التنبيه المحلول تلقائياً من قاعدة البيانات
        Assert.Empty(updatedAlerts);

        using (var context = _fixture.CreateContext())
        {
            var remainingDbAlerts = context.AlertNotifications.Where(a => a.SourceId == source.Id).ToList();
            Assert.Empty(remainingDbAlerts);
        }
    }

    #endregion
}
