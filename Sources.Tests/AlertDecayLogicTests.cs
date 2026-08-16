using System;
using Xunit;

namespace Sources.Tests;

/// <summary>
/// اختبارات وحدة لمنطق عتبات الاضمحلال ونسب فترات نصف العمر المعتمدة في نظام التنبيهات
/// (قانون التحلل: 5 إلى 6 أضعاف نصف العمر T½ للتحذير، و6 أضعاف فأكثر للحرج)
/// </summary>
public class AlertDecayLogicTests
{
    /// <summary>
    /// دالة مساعدة تحاكي تقييم مستوى الخطورة بناءً على عدد فترات نصف العمر المنقضية كما هي معتمدة في المنظومة
    /// </summary>
    public static string EvaluateDecayAlertSeverity(double elapsedHalfLives)
    {
        if (elapsedHalfLives >= 6.0)
            return "Critical";
        if (elapsedHalfLives >= 5.0)
            return "Warning";
        return "None";
    }

    [Theory]
    // أقل من 5 فترات نصف عمر: لا يوجد تنبيه
    [InlineData(0.0, "None")]
    [InlineData(1.0, "None")]
    [InlineData(4.99, "None")]
    // بين 5.0 و 5.99 فترة نصف عمر: تنبيه تحذيري (Warning)
    [InlineData(5.0, "Warning")]
    [InlineData(5.5, "Warning")]
    [InlineData(5.999, "Warning")]
    // 6 فترات نصف عمر فأكثر: تنبيه حرج (Critical)
    [InlineData(6.0, "Critical")]
    [InlineData(6.5, "Critical")]
    [InlineData(10.0, "Critical")]
    [InlineData(25.0, "Critical")]
    public void EvaluateDecayAlertSeverity_ReturnsExpectedLevel(double elapsedHalfLives, string expectedSeverity)
    {
        // Act
        var severity = EvaluateDecayAlertSeverity(elapsedHalfLives);

        // Assert
        Assert.Equal(expectedSeverity, severity);
    }

    [Theory]
    // سيزيوم Cs-137 (30.08 سنة): 5 فترات نصف عمر = 150.4 سنة -> Warning
    [InlineData(30.08, 150.4, "Warning")]
    // سيزيوم Cs-137: 6 فترات نصف عمر = 180.48 سنة -> Critical
    [InlineData(30.08, 180.48, "Critical")]
    // كوبالت Co-60 (5.27 سنة): 4 فترات نصف عمر = 21.08 سنة -> None
    [InlineData(5.27, 21.08, "None")]
    // كوبالت Co-60: 5.5 فترة نصف عمر = 28.985 سنة -> Warning
    [InlineData(5.27, 28.985, "Warning")]
    // تكنيشيوم Tc-99m (6.01 ساعة): بعد 36.06 ساعة (6 فترات نصف عمر) -> Critical
    [InlineData(6.01, 36.06, "Critical")]
    public void ElapsedTimeVersusHalfLife_MapsToAccurateSeverity(
        double halfLife, double elapsedTime, string expectedSeverity)
    {
        // Arrange
        var halfLivesElapsed = elapsedTime / halfLife;

        // Act
        var severity = EvaluateDecayAlertSeverity(halfLivesElapsed);

        // Assert
        Assert.Equal(expectedSeverity, severity);
    }

    [Fact]
    public void ActivityFraction_AtWarningAndCriticalThresholds_MatchesPhysics()
    {
        // عند 5 فترات نصف عمر (Warning threshold):
        // النشاط المتبقي = (0.5)^5 = 1/32 = 0.03125 (3.125% من النشاط الأصلي)
        var warningFraction = Math.Pow(0.5, 5.0);
        Assert.Equal(0.03125, warningFraction, precision: 6);

        // عند 6 فترات نصف عمر (Critical threshold):
        // النشاط المتبقي = (0.5)^6 = 1/64 = 0.015625 (1.5625% من النشاط الأصلي)
        var criticalFraction = Math.Pow(0.5, 6.0);
        Assert.Equal(0.015625, criticalFraction, precision: 6);
    }
}
