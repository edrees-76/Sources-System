using System;
using Sources.Models;
using Sources.Services;
using Xunit;

namespace Sources.Tests;

/// <summary>
/// اختبارات وحدة شاملة لمحرك حساب الاضمحلال الإشعاعي DecayCalculationService
/// </summary>
public class DecayCalculationServiceTests
{
    private readonly DecayCalculationService _decayService = new();

    #region 1. حساب A(t) لثلاثة نظائر مختلفة في عمر النصف ومقارنتها بالصيغة النظرية الدقيقة

    [Theory]
    // نظير قصير جداً: F-18 (نصف عمر 109.7 دقيقة) بعد مرور ساعتين (120 دقيقة)
    [InlineData(1000000.0, 109.7, "minutes", 120.0 * 60.0)]
    // نظير قصير جداً: Tc-99m (نصف عمر 6.01 ساعة) بعد مرور 24 ساعة (يوم كامل)
    [InlineData(5000000.0, 6.01, "hours", 24.0 * 3600.0)]
    // نظير متوسط: Co-60 (نصف عمر 5.27 سنة) بعد مرور 5.27 سنة (عمر نصف واحد بالضبط)
    [InlineData(100000.0, 5.27, "years", 5.27 * 365.25 * 86400.0)]
    // نظير متوسط: Cs-137 (نصف عمر 30.08 سنة) بعد مرور 30.08 سنة (عمر نصف واحد بالضبط)
    [InlineData(37000000.0, 30.08, "years", 30.08 * 365.25 * 86400.0)]
    // نظير متوسط: Cs-137 بعد مرور 10 سنوات
    [InlineData(1000000.0, 30.08, "years", 10.0 * 365.25 * 86400.0)]
    // نظير طويل جداً: Am-241 (نصف عمر 432.2 سنة) بعد مرور 50 سنة
    [InlineData(2000000.0, 432.2, "years", 50.0 * 365.25 * 86400.0)]
    // نظير طويل جداً: Ra-226 (نصف عمر 1600 سنة) بعد مرور 800 سنة (نصف فترة نصف عمر)
    [InlineData(800000.0, 1600.0, "years", 800.0 * 365.25 * 86400.0)]
    // نظير طويل جداً: Pu-239 (نصف عمر 24110 سنة) بعد مرور 5000 سنة
    [InlineData(500000.0, 24110.0, "years", 5000.0 * 365.25 * 86400.0)]
    public void CalculateActivityAtDate_MatchesExactExponentialFormula(
        double initialActivityBq, double halfLife, string halfLifeUnit, double elapsedSeconds)
    {
        // Arrange
        var calibrationDate = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var calculationDate = calibrationDate.AddSeconds(elapsedSeconds);

        // الحساب اليدوي الدقيق باستخدام الصيغة الفيزيائية: A(t) = A0 * e^(-lambda * t)
        var halfLifeSeconds = _decayService.ConvertTimeToSeconds(halfLife, halfLifeUnit);
        var lambda = Math.Log(2.0) / halfLifeSeconds;
        var expectedActivityExact = initialActivityBq * Math.Exp(-lambda * elapsedSeconds);

        // Act
        var actualActivity = _decayService.CalculateActivityAtDate(
            initialActivityBq, halfLife, halfLifeUnit, calibrationDate, calculationDate);

        // Assert - التحقق بتطابق دقيق جداً (خطأ نسبي أقل من 1e-9)
        var relativeError = Math.Abs(actualActivity - expectedActivityExact) / expectedActivityExact;
        Assert.True(relativeError < 1e-9, $"Expected {expectedActivityExact}, but got {actualActivity}. RelErr: {relativeError}");
    }

    [Fact]
    public void CalculateActivityAtDate_Cs137_ExactHalfLife_ReturnsHalfInitialActivity()
    {
        // Arrange: سيزيوم Cs-137 بنشاط ابتدائي 1000 Bq وعمر نصف 30.08 سنة
        double initialActivityBq = 1000.0;
        double halfLife = 30.08;
        string halfLifeUnit = "years";
        var calibrationDate = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        
        // بعد انقضاء عمر نصف واحد بالتمام والكمال
        var halfLifeSeconds = halfLife * 365.25 * 86400.0;
        var calculationDate = calibrationDate.AddSeconds(halfLifeSeconds);

        // Act
        var result = _decayService.CalculateActivityAtDate(
            initialActivityBq, halfLife, halfLifeUnit, calibrationDate, calculationDate);

        // Assert: يجب أن تكون القيمة 500 Bq بالضبط
        Assert.Equal(500.0, result, precision: 6);
    }

    #endregion

    #region 2. حالة t = 0 (يجب أن تُرجع A0 بالضبط)

    [Theory]
    [InlineData(1000.0, 109.7, "minutes")]
    [InlineData(50000.0, 6.01, "hours")]
    [InlineData(3.7e7, 30.08, "years")]
    [InlineData(1.0e9, 432.2, "years")]
    [InlineData(500.0, 1600.0, "years")]
    public void CalculateActivityAtDate_WhenTimeIsZero_ReturnsExactInitialActivity(
        double initialActivityBq, double halfLife, string halfLifeUnit)
    {
        // Arrange
        var date = new DateTime(2025, 6, 15, 12, 0, 0, DateTimeKind.Utc);

        // Act
        var result = _decayService.CalculateActivityAtDate(
            initialActivityBq, halfLife, halfLifeUnit, date, date);

        // Assert
        Assert.Equal(initialActivityBq, result, precision: 8);
    }

    [Fact]
    public void CalculateActivityAtDate_WhenCalculationDateBeforeCalibrationDate_ReturnsInitialActivity()
    {
        // Arrange
        var calibDate = new DateTime(2025, 6, 15, 12, 0, 0, DateTimeKind.Utc);
        var pastDate = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        // Act
        var result = _decayService.CalculateActivityAtDate(10000.0, 30.08, "years", calibDate, pastDate);

        // Assert: elapsedTime < 0 يتم ضبطه إلى 0 وبالتالي إرجاع النشاط الابتدائي
        Assert.Equal(10000.0, result, precision: 8);
    }

    #endregion

    #region 3. حالة t كبير جداً (أكثر من 20 عمر نصف)

    [Theory]
    [InlineData(1.0e6, 109.7, "minutes", 20.0)] // 20 فترة نصف عمر لـ F-18 (حوالي 1.5 يوم)
    [InlineData(1.0e6, 109.7, "minutes", 30.0)] // 30 فترة نصف عمر لـ F-18
    [InlineData(5.0e8, 6.01, "hours", 40.0)]    // 40 فترة نصف عمر لـ Tc-99m (10 أيام)
    [InlineData(1.0e7, 8.02, "days", 25.0)]     // 25 فترة نصف عمر لـ I-131 (200 يوم)
    [InlineData(1.0e5, 5.27, "years", 25.0)]    // 25 فترة نصف عمر لـ Co-60 (131.75 سنة)
    [InlineData(3.7e10, 30.08, "years", 25.0)]  // 25 فترة نصف عمر لـ Cs-137 (752 سنة)
    [InlineData(3.7e10, 30.08, "years", 50.0)]  // 50 فترة نصف عمر لـ Cs-137 (1504 سنة)
    [InlineData(1.0e9, 432.2, "years", 20.0)]   // 20 فترة نصف عمر لـ Am-241 (من عام 1 حتى 8645)
    public void CalculateActivityAtDate_WhenTimeIsVeryLarge_ApproachesZeroWithoutNegativeOrNaN(
        double initialActivityBq, double halfLife, string halfLifeUnit, double halfLivesCount)
    {
        // Arrange: استخدام تاريخ بداية قديم (عام 1) لتجنب تجاوز DateTime.MaxValue عند إضافة آلاف السنين
        var halfLifeSeconds = _decayService.ConvertTimeToSeconds(halfLife, halfLifeUnit);
        var elapsedSeconds = halfLivesCount * halfLifeSeconds;
        var calibDate = new DateTime(1, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var calcDate = calibDate.AddSeconds(elapsedSeconds);

        // Act
        var result = _decayService.CalculateActivityAtDate(
            initialActivityBq, halfLife, halfLifeUnit, calibDate, calcDate);

        // Assert
        Assert.False(double.IsNaN(result), "Result should not be NaN");
        Assert.False(double.IsInfinity(result), "Result should not be Infinity");
        Assert.True(result >= 0.0, "Result must be non-negative");

        // بعد 20 عمر نصف، العامل هو (0.5)^20 = ~9.53e-7، أي أقل من 1 على مليون من النشاط الأصلي
        var expectedMaxAllowed = initialActivityBq * Math.Pow(0.5, halfLivesCount) * 1.0001;
        Assert.True(result <= expectedMaxAllowed, $"Activity {result} should be <= {expectedMaxAllowed}");
        Assert.True(result < initialActivityBq * 1e-5, "Activity should be practically negligible");
    }

    #endregion

    #region 4. اختبارات ConvertToBq و ConvertFromBq والتحويل الدائري (Round-Trip)

    [Theory]
    [InlineData("Bq", 1.0, 1.0)]
    [InlineData("kBq", 1.0, 1e3)]
    [InlineData("MBq", 1.0, 1e6)]
    [InlineData("GBq", 1.0, 1e9)]
    [InlineData("TBq", 1.0, 1e12)]
    [InlineData("Ci", 1.0, 3.7e10)]
    [InlineData("mCi", 1.0, 3.7e7)]
    [InlineData("µCi", 1.0, 3.7e4)]
    [InlineData("uCi", 1.0, 3.7e4)]
    public void ConvertToBq_WithUnitSymbol_ConvertsCorrectly(string unitSymbol, double value, double expectedBq)
    {
        // Act
        var actualBq = _decayService.ConvertToBq(value, unitSymbol);

        // Assert
        Assert.Equal(expectedBq, actualBq, precision: 4);
    }

    [Theory]
    [InlineData("Bq", 1000.0, 1000.0)]
    [InlineData("kBq", 1e6, 1e3)]
    [InlineData("MBq", 5e6, 5.0)]
    [InlineData("GBq", 2.5e9, 2.5)]
    [InlineData("TBq", 1e12, 1.0)]
    [InlineData("Ci", 3.7e10, 1.0)]
    [InlineData("mCi", 3.7e7, 1.0)]
    [InlineData("µCi", 3.7e4, 1.0)]
    [InlineData("uCi", 3.7e4, 1.0)]
    public void ConvertFromBq_WithUnitSymbol_ConvertsCorrectly(string unitSymbol, double activityBq, double expectedValue)
    {
        // Act
        var actualValue = _decayService.ConvertFromBq(activityBq, unitSymbol);

        // Assert
        Assert.Equal(expectedValue, actualValue, precision: 6);
    }

    [Theory]
    [InlineData("Bq", 12345.67)]
    [InlineData("kBq", 845.2)]
    [InlineData("MBq", 150.75)]
    [InlineData("GBq", 12.34)]
    [InlineData("TBq", 0.05)]
    [InlineData("Ci", 2.5)]
    [InlineData("mCi", 150.0)]
    [InlineData("µCi", 500.0)]
    [InlineData("uCi", 750.0)]
    public void RoundTrip_UnitSymbolConversion_RestoresOriginalValue(string unitSymbol, double originalValue)
    {
        // Act: تحويل من الوحدة إلى Bq ثم العودة من Bq إلى نفس الوحدة
        var activityInBq = _decayService.ConvertToBq(originalValue, unitSymbol);
        var roundTripValue = _decayService.ConvertFromBq(activityInBq, unitSymbol);

        // Assert
        Assert.Equal(originalValue, roundTripValue, precision: 6);
    }

    [Theory]
    [InlineData(1.0, 100.0)]       // Bq -> factor 1.0
    [InlineData(1e3, 50.0)]       // kBq -> factor 1e3
    [InlineData(1e6, 12.5)]       // MBq -> factor 1e6
    [InlineData(1e9, 3.2)]        // GBq -> factor 1e9
    [InlineData(1e12, 0.5)]       // TBq -> factor 1e12
    [InlineData(3.7e10, 1.5)]     // Ci -> factor 3.7e10
    [InlineData(3.7e7, 10.0)]     // mCi -> factor 3.7e7
    [InlineData(3.7e4, 250.0)]    // µCi -> factor 3.7e4
    public void RoundTrip_NumericFactorConversion_RestoresOriginalValue(double conversionFactor, double originalValue)
    {
        // Act
        var activityInBq = _decayService.ConvertToBq(originalValue, conversionFactor);
        var roundTripValue = _decayService.ConvertFromBq(activityInBq, conversionFactor);

        // Assert
        Assert.Equal(originalValue, roundTripValue, precision: 6);
    }

    #endregion

    #region 5. اختبار الدوال المساعدة الإضافية (DecayPercentage, TimeToActivity, DecayCurve, TimeConversion)

    [Theory]
    [InlineData(1000.0, 500.0, 50.0)]   // اضمحل بنسبة 50%
    [InlineData(1000.0, 250.0, 75.0)]   // اضمحل بنسبة 75%
    [InlineData(1000.0, 1000.0, 0.0)]   // لم يضمحل بعد (0%)
    [InlineData(1000.0, 0.0, 100.0)]    // اضمحل كلياً (100%)
    public void CalculateDecayPercentage_ReturnsAccuratePercentage(
        double initialBq, double currentBq, double expectedPercentage)
    {
        // Act
        var percentage = _decayService.CalculateDecayPercentage(initialBq, currentBq);

        // Assert
        Assert.Equal(expectedPercentage, percentage, precision: 6);
    }

    [Fact]
    public void CalculateTimeToActivity_ForExactHalfActivity_ReturnsOneHalfLifeInSeconds()
    {
        // Arrange
        double initialBq = 1000.0;
        double targetBq = 500.0;
        double halfLife = 30.08;
        string unit = "years";

        var expectedSeconds = halfLife * 365.25 * 86400.0;

        // Act
        var actualSeconds = _decayService.CalculateTimeToActivity(initialBq, targetBq, halfLife, unit);

        // Assert
        Assert.Equal(expectedSeconds, actualSeconds, precision: 4);
    }

    [Fact]
    public void CalculateTimeToActivity_ForQuarterActivity_ReturnsTwoHalfLivesInSeconds()
    {
        // Arrange
        double initialBq = 1000.0;
        double targetBq = 250.0;
        double halfLife = 5.27;
        string unit = "years";

        var expectedSeconds = 2.0 * halfLife * 365.25 * 86400.0;

        // Act
        var actualSeconds = _decayService.CalculateTimeToActivity(initialBq, targetBq, halfLife, unit);

        // Assert
        Assert.Equal(expectedSeconds, actualSeconds, precision: 4);
    }

    [Fact]
    public void GenerateDecayCurve_GeneratesMonotonicallyDecreasingPoints()
    {
        // Arrange
        var calibDate = new DateTime(2025, 1, 1);
        int points = 50;

        // Act
        var curve = _decayService.GenerateDecayCurve(100000.0, 30.08, "years", calibDate, points);

        // Assert
        Assert.Equal(points + 1, curve.Count);
        Assert.Equal(100000.0, curve[0].Activity, precision: 4);

        for (int i = 1; i < curve.Count; i++)
        {
            Assert.True(curve[i].Activity <= curve[i - 1].Activity, $"Point {i} should be <= point {i - 1}");
            Assert.True(curve[i].Time > curve[i - 1].Time, $"Time {i} should be > time {i - 1}");
        }
    }

    [Theory]
    [InlineData(1.0, "s", 1.0)]
    [InlineData(1.0, "seconds", 1.0)]
    [InlineData(1.0, "second", 1.0)]
    [InlineData(1.0, "m", 60.0)]
    [InlineData(1.0, "minutes", 60.0)]
    [InlineData(1.0, "minute", 60.0)]
    [InlineData(1.0, "h", 3600.0)]
    [InlineData(1.0, "hours", 3600.0)]
    [InlineData(1.0, "hour", 3600.0)]
    [InlineData(1.0, "d", 86400.0)]
    [InlineData(1.0, "days", 86400.0)]
    [InlineData(1.0, "day", 86400.0)]
    [InlineData(1.0, "mo", 30.0 * 86400.0)]
    [InlineData(1.0, "months", 30.0 * 86400.0)]
    [InlineData(1.0, "y", 365.25 * 86400.0)]
    [InlineData(1.0, "years", 365.25 * 86400.0)]
    [InlineData(1.0, "yr", 365.25 * 86400.0)]
    public void ConvertTimeToSeconds_SupportedUnits_ConvertCorrectly(double value, string unit, double expectedSeconds)
    {
        // Act
        var seconds = _decayService.ConvertTimeToSeconds(value, unit);

        // Assert
        Assert.Equal(expectedSeconds, seconds, precision: 4);
    }

    #endregion

    #region 6. اختبار CalculateCurrentActivityForSource مع الكائنات المصدرية

    [Fact]
    public void CalculateCurrentActivityForSource_SingleIsotope_CalculatesAccurately()
    {
        // Arrange
        var isotope = new Radioisotope
        {
            Symbol = "Cs-137",
            Name = "Cesium-137",
            HalfLife = 30.08,
            HalfLifeUnit = "years"
        };
        var unit = new ActivityUnit
        {
            UnitName = "Millicurie",
            UnitSymbol = "mCi",
            ConversionToBq = 3.7e7
        };
        var source = new Source
        {
            SourceCode = "SRC-TEST-01",
            InitialActivityValue = 10.0, // 10 mCi = 3.7e8 Bq
            InitialActivityUnit = unit,
            Radioisotope = isotope,
            CalibrationDate = DateTime.Now // اليوم
        };

        // Act
        var currentBq = _decayService.CalculateCurrentActivityForSource(source, isotope, unit);

        // Assert: المعايرة اليوم لذا يجب أن يكون النشاط مساوياً للنشاط الابتدائي بـ Bq
        var expectedBq = 10.0 * 3.7e7;
        Assert.Equal(expectedBq, currentBq, precision: 2);
    }

    #endregion
}
