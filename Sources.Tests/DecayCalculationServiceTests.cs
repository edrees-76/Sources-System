using System;
using System.Collections.Generic;
using System.Linq;
using Sources.Models;
using Sources.Services;
using Xunit;

namespace Sources.Tests;

/// <summary>
/// اختبارات وحدة شاملة لمحرك حساب الاضمحلال الإشعاعي DecayCalculationService
/// Pure Unit Tests تغطي كافة الصيغ الفيزيائية والتحويلات والحالات الحدية
/// </summary>
public class DecayCalculationServiceTests
{
    private readonly DecayCalculationService _decayService = new();

    #region 1. حساب A(t) ومقارنتها بالصيغة النظرية وفترات نصف العمر الصحيحة

    [Theory]
    // 1 فترة نصف عمر -> 50% من النشاط
    [InlineData(1000.0, 1.0, 500.0)]
    // 2 فترات نصف عمر -> 25% من النشاط
    [InlineData(1000.0, 2.0, 250.0)]
    // 3 فترات نصف عمر -> 12.5% من النشاط
    [InlineData(1000.0, 3.0, 125.0)]
    // 4 فترات نصف عمر -> 6.25% من النشاط
    [InlineData(1000.0, 4.0, 62.5)]
    // 5 فترات نصف عمر -> 3.125% من النشاط
    [InlineData(1000.0, 5.0, 31.25)]
    public void CalculateActivityAtDate_IntegerHalfLives_MatchesExactFractions(
        double initialActivity, double halfLivesCount, double expectedActivity)
    {
        // Arrange: سيزيوم Cs-137 (30.08 سنة)
        double halfLife = 30.08;
        string halfLifeUnit = "years";
        var calibDate = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var halfLifeSec = _decayService.ConvertTimeToSeconds(halfLife, halfLifeUnit);
        var calcDate = calibDate.AddSeconds(halfLivesCount * halfLifeSec);

        // Act
        var actualActivity = _decayService.CalculateActivityAtDate(
            initialActivity, halfLife, halfLifeUnit, calibDate, calcDate);

        // Assert
        Assert.Equal(expectedActivity, actualActivity, precision: 4);
    }

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
        
        var halfLifeSeconds = halfLife * 365.25 * 86400.0;
        var calculationDate = calibrationDate.AddSeconds(halfLifeSeconds);

        // Act
        var result = _decayService.CalculateActivityAtDate(
            initialActivityBq, halfLife, halfLifeUnit, calibrationDate, calculationDate);

        // Assert: يجب أن تكون القيمة 500 Bq بالضبط
        Assert.Equal(500.0, result, precision: 6);
    }

    #endregion

    #region 2. حالات t = 0 وتواريخ المعايرة المستقبلية

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
        // Arrange: تاريخ الحساب قبل تاريخ المعايرة
        var calibDate = new DateTime(2025, 6, 15, 12, 0, 0, DateTimeKind.Utc);
        var pastDate = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        // Act
        var result = _decayService.CalculateActivityAtDate(10000.0, 30.08, "years", calibDate, pastDate);

        // Assert: elapsedTime < 0 يتم ضبطه إلى 0 وبالتالي إرجاع النشاط الابتدائي
        Assert.Equal(10000.0, result, precision: 8);
    }

    [Fact]
    public void CalculateCurrentActivity_UsesCurrentDateForCalculation()
    {
        // Arrange: مصدر عوير قبل 30.08 سنة من الآن
        double halfLife = 30.08;
        var calibDate = DateTime.Now.AddDays(-30.08 * 365.25);

        // Act
        var result = _decayService.CalculateCurrentActivity(1000.0, halfLife, "years", calibDate);

        // Assert: بعد نصف عمر واحد من الآن يجب أن يكون النشاط حوالي 500 Bq
        Assert.True(Math.Abs(result - 500.0) < 1.0, $"Expected ~500, but got {result}");
    }

    #endregion

    #region 3. الحالات الحدية (نشاط صفري، نصف عمر غير صالح، أزمنة هائلة)

    [Theory]
    [InlineData(0.0, 30.08, "years")]     // نشاط ابتدائي صفري
    [InlineData(-100.0, 30.08, "years")]   // نشاط ابتدائي سالب
    [InlineData(1000.0, 0.0, "years")]    // نصف عمر صفري
    [InlineData(1000.0, -5.0, "years")]   // نصف عمر سالب
    public void CalculateActivityAtDate_WithInvalidInput_ReturnsZero(
        double initialActivity, double halfLife, string halfLifeUnit)
    {
        // Arrange
        var calibDate = new DateTime(2020, 1, 1);
        var calcDate = new DateTime(2025, 1, 1);

        // Act
        var result = _decayService.CalculateActivityAtDate(
            initialActivity, halfLife, halfLifeUnit, calibDate, calcDate);

        // Assert
        Assert.Equal(0.0, result);
    }

    [Theory]
    [InlineData(1.0e6, 109.7, "minutes", 20.0)] // 20 فترة نصف عمر لـ F-18
    [InlineData(1.0e6, 109.7, "minutes", 30.0)] // 30 فترة نصف عمر لـ F-18
    [InlineData(5.0e8, 6.01, "hours", 40.0)]    // 40 فترة نصف عمر لـ Tc-99m
    [InlineData(1.0e7, 8.02, "days", 25.0)]     // 25 فترة نصف عمر لـ I-131
    [InlineData(1.0e5, 5.27, "years", 25.0)]    // 25 فترة نصف عمر لـ Co-60
    [InlineData(3.7e10, 30.08, "years", 25.0)]  // 25 فترة نصف عمر لـ Cs-137
    [InlineData(3.7e10, 30.08, "years", 50.0)]  // 50 فترة نصف عمر لـ Cs-137
    [InlineData(1.0e9, 432.2, "years", 20.0)]   // 20 فترة نصف عمر لـ Am-241
    public void CalculateActivityAtDate_WhenTimeIsVeryLarge_ApproachesZeroWithoutNegativeOrNaN(
        double initialActivityBq, double halfLife, string halfLifeUnit, double halfLivesCount)
    {
        // Arrange: استخدام تاريخ قديم لتفادي تجاوز الحد الأقصى لـ DateTime
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

        var expectedMaxAllowed = initialActivityBq * Math.Pow(0.5, halfLivesCount) * 1.0001;
        Assert.True(result <= expectedMaxAllowed, $"Activity {result} should be <= {expectedMaxAllowed}");
        Assert.True(result < initialActivityBq * 1e-5, "Activity should be practically negligible");
    }

    #endregion

    #region 4. حساب الزمن اللازم للوصول لنشاط مستهدف (Time to Target Activity)

    [Fact]
    public void CalculateTimeToActivity_Cs137_From100To25_ReturnsTwoHalfLives()
    {
        // Arrange: Cs-137 بنصف عمر 30.17 سنة (أو 30.08)، الوصول من 100 إلى 25 يتطلب فترتي نصف عمر (t = 2 * T½)
        double initialActivity = 100.0;
        double targetActivity = 25.0;
        double halfLife = 30.17;
        string unit = "years";

        var expectedSeconds = 2.0 * halfLife * 365.25 * 86400.0;

        // Act
        var actualSeconds = _decayService.CalculateTimeToActivity(initialActivity, targetActivity, halfLife, unit);

        // Assert
        Assert.Equal(expectedSeconds, actualSeconds, precision: 2);
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

    [Theory]
    [InlineData(100.0, 100.0, 30.08)]  // النشاط المستهدف مساوٍ للابتدائي -> 0 ثانية
    [InlineData(100.0, 150.0, 30.08)]  // النشاط المستهدف أكبر من الابتدائي -> 0 ثانية (لا يمكن بالاضمحلال)
    [InlineData(100.0, 0.0, 30.08)]    // نشاط مستهدف صفري -> 0
    [InlineData(100.0, -10.0, 30.08)]  // نشاط مستهدف سالب -> 0
    [InlineData(0.0, 10.0, 30.08)]     // نشاط ابتدائي صفري -> 0
    [InlineData(100.0, 50.0, 0.0)]     // نصف عمر صفري -> 0
    [InlineData(100.0, 50.0, -5.0)]    // نصف عمر سالب -> 0
    public void CalculateTimeToActivity_InvalidInputs_ReturnsZero(
        double initial, double target, double halfLife)
    {
        // Act
        var result = _decayService.CalculateTimeToActivity(initial, target, halfLife, "years");

        // Assert
        Assert.Equal(0.0, result);
    }

    #endregion

    #region 5. دوال تحويل النشاط (ConvertToBq / ConvertFromBq) والتحويل الدائري

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
    [InlineData(1.0, 100.0)]       // Bq
    [InlineData(1e3, 50.0)]       // kBq
    [InlineData(1e6, 12.5)]       // MBq
    [InlineData(1e9, 3.2)]        // GBq
    [InlineData(1e12, 0.5)]       // TBq
    [InlineData(3.7e10, 1.5)]     // Ci
    [InlineData(3.7e7, 10.0)]     // mCi
    [InlineData(3.7e4, 250.0)]    // µCi
    public void RoundTrip_NumericFactorConversion_RestoresOriginalValue(double conversionFactor, double originalValue)
    {
        // Act
        var activityInBq = _decayService.ConvertToBq(originalValue, conversionFactor);
        var roundTripValue = _decayService.ConvertFromBq(activityInBq, conversionFactor);

        // Assert
        Assert.Equal(originalValue, roundTripValue, precision: 6);
    }

    [Fact]
    public void ConvertFromBq_InvalidFactor_ReturnsOriginalActivity()
    {
        // Act & Assert
        Assert.Equal(100.0, _decayService.ConvertFromBq(100.0, 0.0));
        Assert.Equal(100.0, _decayService.ConvertFromBq(100.0, -1.0));
    }

    [Fact]
    public void ConvertUnits_UnknownSymbol_ReturnsOriginalValueAsFallback()
    {
        // Act & Assert
        Assert.Equal(50.0, _decayService.ConvertToBq(50.0, "UNKNOWN_UNIT"));
        Assert.Equal(50.0, _decayService.ConvertFromBq(50.0, "UNKNOWN_UNIT"));
    }

    #endregion

    #region 6. دوال منحنيات الاضمحلال وحساب نسبة الاضمحلال

    [Theory]
    [InlineData(1000.0, 500.0, 50.0)]   // اضمحل بنسبة 50%
    [InlineData(1000.0, 250.0, 75.0)]   // اضمحل بنسبة 75%
    [InlineData(1000.0, 1000.0, 0.0)]   // لم يضمحل بعد (0%)
    [InlineData(1000.0, 0.0, 100.0)]    // اضمحل كلياً (100%)
    [InlineData(0.0, 500.0, 0.0)]       // نشاط ابتدائي صفري -> 0%
    public void CalculateDecayPercentage_ReturnsAccuratePercentage(
        double initialBq, double currentBq, double expectedPercentage)
    {
        // Act
        var percentage = _decayService.CalculateDecayPercentage(initialBq, currentBq);

        // Assert
        Assert.Equal(expectedPercentage, percentage, precision: 6);
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

    [Fact]
    public void GenerateUnifiedDecayCurve_GeneratesPointsWithinSpecifiedRange()
    {
        // Arrange
        var calibDate = new DateTime(2020, 1, 1);
        var startDate = new DateTime(2025, 1, 1);
        var endDate = new DateTime(2030, 1, 1);
        int points = 20;

        // Act
        var curve = _decayService.GenerateUnifiedDecayCurve(
            1000.0, 30.08, "years", calibDate, startDate, endDate, points);

        // Assert
        Assert.Equal(points + 1, curve.Count);
        Assert.Equal(startDate, curve.First().Time);
        Assert.Equal(endDate, curve.Last().Time);
        Assert.True(curve.First().Activity >= curve.Last().Activity);
    }

    [Fact]
    public void GetSourceCompositeDecayCurve_MultiIsotopeSource_GeneratesCompositeSum()
    {
        // Arrange
        var isoCs = new Radioisotope { Symbol = "Cs-137", HalfLife = 30.08, HalfLifeUnit = "years" };
        var isoCo = new Radioisotope { Symbol = "Co-60", HalfLife = 5.27, HalfLifeUnit = "years" };
        var unitBq = new ActivityUnit { UnitName = "Bq", UnitSymbol = "Bq", ConversionToBq = 1.0 };

        var source = new Source
        {
            SourceCode = "SRC-COMPOSITE-TEST",
            HasDetailedIsotopes = true,
            CalibrationDate = DateTime.Today
        };

        var si1 = new SourceIsotope
        {
            Radioisotope = isoCs,
            ActivityUnit = unitBq,
            InitialActivityValue = 1000.0,
            CalibrationDate = DateTime.Today
        };

        var si2 = new SourceIsotope
        {
            Radioisotope = isoCo,
            ActivityUnit = unitBq,
            InitialActivityValue = 2000.0,
            CalibrationDate = DateTime.Today
        };

        source.SourceIsotopes = new List<SourceIsotope> { si1, si2 };

        // Act
        var compositeCurve = _decayService.GetSourceCompositeDecayCurve(source, points: 10);

        // Assert
        Assert.NotEmpty(compositeCurve);
        Assert.Equal(11, compositeCurve.Count);
        // النقطة الأولى (عند t=0) يجب أن تساوي مجموع النشاط الابتدائي = 3000 Bq
        Assert.Equal(3000.0, compositeCurve.First().ActivityBq, precision: 2);
        // النقطة الأخيرة أقل من الأولى
        Assert.True(compositeCurve.Last().ActivityBq < compositeCurve.First().ActivityBq);
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

    #region 7. اختبار CalculateCurrentActivityForSource

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
            CalibrationDate = DateTime.Now
        };

        // Act
        var currentBq = _decayService.CalculateCurrentActivityForSource(source, isotope, unit);

        // Assert: المعايرة اليوم لذا يجب أن يكون النشاط مساوياً للنشاط الابتدائي بـ Bq
        var expectedBq = 10.0 * 3.7e7;
        Assert.Equal(expectedBq, currentBq, precision: 2);
    }

    #endregion
}
