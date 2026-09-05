using System;
using Sources.Models;
using Sources.Services;
using Sources.ViewModels;
using Xunit;

namespace Sources.Tests;

public class NeutronDecayTests
{
    private readonly NeutronDecayCalculationService _decayService = new();

    [Fact]
    public void Cf252_CalibratedExactlyOneHalfLifeAgo_ReturnsHalfOfCalibratedRate()
    {
        // Arrange: Cf-252, HalfLife = 2.645 years
        double initialRate = 1.0e7; // 10,000,000 n/s
        double halfLife = 2.645;
        string halfLifeUnit = "years";

        DateTime calDate = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        // 1 half-life = 2.645 * 365.2422 days
        double halfLifeDays = halfLife * NeutronDecayCalculationService.DaysPerYear;
        DateTime calcDate = calDate.AddDays(halfLifeDays);

        // Act
        var result = _decayService.CalculateEmissionRate(initialRate, halfLife, halfLifeUnit, calDate, calcDate);

        // Assert
        Assert.True(result.IsCalculated);
        Assert.Equal(NeutronDecayCalculationStatus.Calculated, result.Status);
        Assert.NotNull(result.CurrentEmissionRate);

        double expectedRate = initialRate * 0.5;
        Assert.Equal(expectedRate, result.CurrentEmissionRate.Value, precision: 3);
    }

    [Fact]
    public void Cf252_CalibratedFiveYearsAgo_ReturnsApproximately27PercentOfCalibratedRate()
    {
        // Arrange: Cf-252, HalfLife = 2.645 years, elapsed = 5 years
        double initialRate = 1.0e7; // 10,000,000 n/s
        double halfLife = 2.645;
        string halfLifeUnit = "years";

        DateTime calDate = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        double elapsedDays = 5.0 * NeutronDecayCalculationService.DaysPerYear;
        DateTime calcDate = calDate.AddDays(elapsedDays);

        // Act
        var result = _decayService.CalculateEmissionRate(initialRate, halfLife, halfLifeUnit, calDate, calcDate);

        // Assert
        Assert.True(result.IsCalculated);
        Assert.NotNull(result.CurrentEmissionRate);

        double ratio = result.CurrentEmissionRate.Value / initialRate;
        // 0.5^(5 / 2.645) ≈ 0.269786 (نحو 27%)
        Assert.InRange(ratio, 0.265, 0.275);
        Assert.True(ratio < 0.30, "A 5-year-old Cf-252 source must be under 30% of original emission rate");
    }

    [Fact]
    public void Am241Be_CalibratedFiveYearsAgo_ReturnsGreaterThan99PercentOfCalibratedRate()
    {
        // Arrange: Am-241/Be, HalfLife = 432.2 years, elapsed = 5 years
        double initialRate = 2.2e6;
        double halfLife = 432.2;
        string halfLifeUnit = "years";

        DateTime calDate = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        double elapsedDays = 5.0 * NeutronDecayCalculationService.DaysPerYear;
        DateTime calcDate = calDate.AddDays(elapsedDays);

        // Act
        var result = _decayService.CalculateEmissionRate(initialRate, halfLife, halfLifeUnit, calDate, calcDate);

        // Assert
        Assert.True(result.IsCalculated);
        Assert.NotNull(result.CurrentEmissionRate);

        double ratio = result.CurrentEmissionRate.Value / initialRate;
        // 0.5^(5 / 432.2) ≈ 0.99201 (> 0.99)
        Assert.True(ratio > 0.99, "Am-241/Be over 5 years must retain > 99% of calibrated emission rate");
    }

    [Fact]
    public void Sb124Be_Calibrated60Point2DaysAgo_ReturnsHalfOfCalibratedRate()
    {
        // Arrange: Sb-124/Be, HalfLife = 60.2 days (covers 'days' unit)
        double initialRate = 5.0e5;
        double halfLife = 60.2;
        string halfLifeUnit = "days";

        DateTime calDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        DateTime calcDate = calDate.AddDays(60.2);

        // Act
        var result = _decayService.CalculateEmissionRate(initialRate, halfLife, halfLifeUnit, calDate, calcDate);

        // Assert
        Assert.True(result.IsCalculated);
        Assert.NotNull(result.CurrentEmissionRate);

        double expectedRate = initialRate * 0.5;
        Assert.Equal(expectedRate, result.CurrentEmissionRate.Value, precision: 3);
    }

    [Fact]
    public void EmissionCalibrationDate_Null_ReturnsUncalculated_AndNeverReturnsCalibratedEmissionRate()
    {
        // Arrange: source with no calibration date
        var type = new NeutronSourceType { HalfLife = 2.645, HalfLifeUnit = "years" };
        var source = new NeutronSource
        {
            CalibratedEmissionRate = 1.5e7,
            EmissionCalibrationDate = null,
            NeutronSourceType = type
        };

        // Act
        var result = _decayService.CalculateCurrentEmissionRate(source);

        // Assert
        Assert.False(result.IsCalculated);
        Assert.Equal(NeutronDecayCalculationStatus.MissingCalibrationDate, result.Status);
        Assert.Null(result.CurrentEmissionRate);

        // التأكيد الصريح: النتيجة لا تساوي القيمة المعايرة
        Assert.NotEqual(source.CalibratedEmissionRate, result.CurrentEmissionRate ?? 0.0);
    }

    [Fact]
    public void HalfLife_ZeroOrNegative_ReturnsUncalculated()
    {
        // Arrange
        DateTime calDate = new DateTime(2024, 1, 1);
        DateTime calcDate = new DateTime(2026, 1, 1);

        // Act - Zero HalfLife
        var resultZero = _decayService.CalculateEmissionRate(1.0e6, 0, "years", calDate, calcDate);
        // Act - Negative HalfLife
        var resultNeg = _decayService.CalculateEmissionRate(1.0e6, -5.0, "years", calDate, calcDate);

        // Assert
        Assert.False(resultZero.IsCalculated);
        Assert.Equal(NeutronDecayCalculationStatus.InvalidHalfLife, resultZero.Status);
        Assert.Null(resultZero.CurrentEmissionRate);

        Assert.False(resultNeg.IsCalculated);
        Assert.Equal(NeutronDecayCalculationStatus.InvalidHalfLife, resultNeg.Status);
        Assert.Null(resultNeg.CurrentEmissionRate);
    }

    [Fact]
    public void NeutronSourceType_Null_ReturnsUncalculated()
    {
        // Arrange: source with null NeutronSourceType
        var source = new NeutronSource
        {
            CalibratedEmissionRate = 1.0e6,
            EmissionCalibrationDate = DateTime.Today.AddYears(-2),
            NeutronSourceType = null
        };

        // Act
        var result = _decayService.CalculateCurrentEmissionRate(source);

        // Assert
        Assert.False(result.IsCalculated);
        Assert.Equal(NeutronDecayCalculationStatus.MissingSourceType, result.Status);
        Assert.Null(result.CurrentEmissionRate);
    }

    [Fact]
    public void HalfLifeUnit_Unsupported_ReturnsUncalculated()
    {
        // Arrange: Unsupported units like 'hours', 'months', 'invalid'
        DateTime calDate = new DateTime(2024, 1, 1);
        DateTime calcDate = new DateTime(2026, 1, 1);

        // Act
        var resultHours = _decayService.CalculateEmissionRate(1.0e6, 100, "hours", calDate, calcDate);
        var resultUnknown = _decayService.CalculateEmissionRate(1.0e6, 100, "centuries", calDate, calcDate);

        // Assert
        Assert.False(resultHours.IsCalculated);
        Assert.Equal(NeutronDecayCalculationStatus.UnsupportedHalfLifeUnit, resultHours.Status);
        Assert.Null(resultHours.CurrentEmissionRate);

        Assert.False(resultUnknown.IsCalculated);
        Assert.Equal(NeutronDecayCalculationStatus.UnsupportedHalfLifeUnit, resultUnknown.Status);
        Assert.Null(resultUnknown.CurrentEmissionRate);
    }

    [Fact]
    public void CalculationDate_BeforeCalibrationDate_ReturnsUncalculated()
    {
        // Arrange: Calculation date is BEFORE calibration date (no reverse decay)
        DateTime calDate = new DateTime(2025, 6, 1);
        DateTime calcDate = new DateTime(2024, 1, 1);

        // Act
        var result = _decayService.CalculateEmissionRate(1.0e6, 2.645, "years", calDate, calcDate);

        // Assert
        Assert.False(result.IsCalculated);
        Assert.Equal(NeutronDecayCalculationStatus.CalculationDatePrecedesCalibrationDate, result.Status);
        Assert.Null(result.CurrentEmissionRate);
    }

    [Fact]
    public void CalibratedEmissionRate_ZeroOrNegative_ReturnsUncalculated_WithNullRate()
    {
        // Arrange: Zero or negative calibrated rate
        DateTime calDate = new DateTime(2024, 1, 1);
        DateTime calcDate = new DateTime(2026, 1, 1);

        // Act - Zero rate
        var resultZero = _decayService.CalculateEmissionRate(0.0, 2.645, "years", calDate, calcDate);
        // Act - Negative rate
        var resultNeg = _decayService.CalculateEmissionRate(-1.0e6, 2.645, "years", calDate, calcDate);

        // Assert
        Assert.False(resultZero.IsCalculated);
        Assert.Equal(NeutronDecayCalculationStatus.InvalidCalibratedRate, resultZero.Status);
        Assert.Null(resultZero.CurrentEmissionRate);

        Assert.False(resultNeg.IsCalculated);
        Assert.Equal(NeutronDecayCalculationStatus.InvalidCalibratedRate, resultNeg.Status);
        Assert.Null(resultNeg.CurrentEmissionRate);
    }

    [Fact]
    public void NeutronSourceDetailsViewModel_DisplaysDualRates_AndCertificateProperties()
    {
        // Arrange
        var type = new NeutronSourceType
        {
            Code = "Cf-252",
            NameAr = "كاليفورنيوم-252",
            HalfLife = 2.645,
            HalfLifeUnit = "years"
        };
        var source = new NeutronSource
        {
            SourceCode = "NS-CF-001",
            CalibratedEmissionRate = 1.0e7,
            EmissionCalibrationDate = DateTime.Today.AddDays(-2.645 * 365.2422),
            CalibrationReference = "CERT-2024-001",
            AnisotropyFactor = 1.045,
            NeutronSourceType = type
        };

        // Act
        var vm = new NeutronSourceDetailsViewModel(source, decayService: _decayService);

        // Assert
        Assert.Equal(source.CalibratedEmissionRateFormatted, vm.CalibratedEmissionRateFormatted);
        Assert.Contains("CERT-2024-001", vm.CalibrationReferenceDisplay);
        Assert.Equal("1.045", vm.AnisotropyFactorDisplay);
        Assert.True(vm.IsCurrentEmissionRateCalculated);
        Assert.Contains("n/s", vm.CurrentEmissionRateDisplay);
    }

    [Fact]
    public void NeutronSourceDetailsViewModel_WhenCalibrationDateNull_DisplaysExplicitUncalculatedReason()
    {
        Sources.Tests.Fixtures.WpfStaFixture.RunInSta(() =>
        {
            // Arrange
            var type = new NeutronSourceType { Code = "Cf-252", HalfLife = 2.645, HalfLifeUnit = "years" };
            var source = new NeutronSource
            {
                SourceCode = "NS-CF-002",
                CalibratedEmissionRate = 1.0e7,
                EmissionCalibrationDate = null,
                CalibrationReference = null,
                AnisotropyFactor = null,
                NeutronSourceType = type
            };

            // Act
            var vm = new NeutronSourceDetailsViewModel(source, decayService: _decayService);

            // Assert
            Assert.Equal("تاريخ المعايرة غير مسجّل", vm.EmissionCalibrationDateFormatted);
            Assert.Equal("غير مسجّل", vm.CalibrationReferenceDisplay);
            Assert.Equal("غير مقاس", vm.AnisotropyFactorDisplay);
            Assert.False(vm.IsCurrentEmissionRateCalculated);
            Assert.Contains("غير محسوب", vm.CurrentEmissionRateDisplay);
            Assert.Contains("تاريخ المعايرة غير مسجّل", vm.CurrentEmissionRateDisplay);
        });
    }

    [Fact]
    public void Am241Activity_NotRecorded_WhenValueAndUnitAreNull()
    {
        // Arrange
        var source = new NeutronSource
        {
            Am241ActivityValue = null,
            Am241ActivityUnitId = null,
            Am241ActivityUnit = null,
            CalibrationDate = DateTime.Today.AddYears(-1)
        };

        // Act
        var result = _decayService.CalculateCurrentAm241Activity(source);

        // Assert
        Assert.False(result.IsCalculated);
        Assert.Equal(NeutronDecayCalculationStatus.NotRecorded, result.Status);
        Assert.Null(result.CurrentActivityBq);
    }

    [Fact]
    public void Am241Activity_MissingActivityUnit_WhenUnitNavigationNotLoaded()
    {
        // Arrange: Am241ActivityUnitId has a value but the navigation property was not
        // eager-loaded by the caller (mirrors MissingSourceType pattern)
        var source = new NeutronSource
        {
            Am241ActivityValue = 1.0e10,
            Am241ActivityUnitId = Guid.NewGuid(),
            Am241ActivityUnit = null,
            CalibrationDate = DateTime.Today.AddYears(-1)
        };

        // Act
        var result = _decayService.CalculateCurrentAm241Activity(source);

        // Assert
        Assert.False(result.IsCalculated);
        Assert.Equal(NeutronDecayCalculationStatus.MissingActivityUnit, result.Status);
        Assert.Null(result.CurrentActivityBq);
    }

    [Fact]
    public void Am241Activity_InvalidValue_WhenConvertedBqIsZeroOrNegative()
    {
        // Arrange: ConversionToBq = 0 makes the converted activity 0 (invalid: <= 0)
        var unit = new ActivityUnit { UnitName = "Zero", UnitSymbol = "Z", ConversionToBq = 0.0 };
        var source = new NeutronSource
        {
            Am241ActivityValue = 5.0,
            Am241ActivityUnitId = Guid.NewGuid(),
            Am241ActivityUnit = unit,
            CalibrationDate = DateTime.Today.AddYears(-1)
        };

        // Act
        var result = _decayService.CalculateCurrentAm241Activity(source);

        // Assert
        Assert.False(result.IsCalculated);
        Assert.Equal(NeutronDecayCalculationStatus.InvalidActivityValue, result.Status);
        Assert.Null(result.CurrentActivityBq);
    }

    [Fact]
    public void Am241Activity_CalibratedExactlyOneHalfLifeAgo_ReturnsHalfOfCalibratedActivity()
    {
        // Arrange: Am-241 half-life = 432.2 years, unit ConversionToBq = 1.0 (already in Bq)
        double initialActivityBq = 3.7e10; // 1 Ci equivalent value, but pre-expressed in Bq via unit
        var unit = new ActivityUnit { UnitName = "Becquerel", UnitSymbol = "Bq", ConversionToBq = 1.0 };

        DateTime calDate = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        double halfLifeDays = 432.2 * NeutronDecayCalculationService.DaysPerYear;
        DateTime calcDate = calDate.AddDays(halfLifeDays);

        var source = new NeutronSource
        {
            Am241ActivityValue = initialActivityBq,
            Am241ActivityUnitId = Guid.NewGuid(),
            Am241ActivityUnit = unit,
            CalibrationDate = calDate
        };

        // Act
        var result = _decayService.CalculateAm241ActivityAtDate(source, calcDate);

        // Assert
        Assert.True(result.IsCalculated);
        Assert.Equal(NeutronDecayCalculationStatus.Calculated, result.Status);
        Assert.NotNull(result.CurrentActivityBq);

        double expectedActivityBq = initialActivityBq * 0.5;
        Assert.Equal(expectedActivityBq, result.CurrentActivityBq.Value, precision: 3);
    }

    [Fact]
    public void Am241Activity_PartialDecay_100YearsElapsed_MatchesHandComputedValue()
    {
        // Arrange: 100 years elapsed out of a 432.2-year half-life
        double initialActivityBq = 1.0e9;
        var unit = new ActivityUnit { UnitName = "Becquerel", UnitSymbol = "Bq", ConversionToBq = 1.0 };

        DateTime calDate = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        double elapsedDays = 100.0 * NeutronDecayCalculationService.DaysPerYear;
        DateTime calcDate = calDate.AddDays(elapsedDays);

        var source = new NeutronSource
        {
            Am241ActivityValue = initialActivityBq,
            Am241ActivityUnitId = Guid.NewGuid(),
            Am241ActivityUnit = unit,
            CalibrationDate = calDate
        };

        // Act
        var result = _decayService.CalculateAm241ActivityAtDate(source, calcDate);

        // Assert
        Assert.True(result.IsCalculated);
        Assert.NotNull(result.CurrentActivityBq);

        // B(t) = B0 * exp(-ln(2) * 100 / 432.2)
        double expectedActivityBq = initialActivityBq * Math.Exp(-Math.Log(2.0) * (100.0 / 432.2));
        Assert.Equal(expectedActivityBq, result.CurrentActivityBq.Value, precision: 3);
    }

    [Fact]
    public void Am241Activity_ConvertsNonBqUnit_BeforeApplyingDecay()
    {
        // Arrange: unit conversion factor != 1 must be applied before decay
        double rawValue = 1.0; // 1 Ci
        double conversionToBq = 3.7e10; // Ci -> Bq
        var unit = new ActivityUnit { UnitName = "Curie", UnitSymbol = "Ci", ConversionToBq = conversionToBq };

        DateTime calDate = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        double halfLifeDays = 432.2 * NeutronDecayCalculationService.DaysPerYear;
        DateTime calcDate = calDate.AddDays(halfLifeDays);

        var source = new NeutronSource
        {
            Am241ActivityValue = rawValue,
            Am241ActivityUnitId = Guid.NewGuid(),
            Am241ActivityUnit = unit,
            CalibrationDate = calDate
        };

        // Act
        var result = _decayService.CalculateAm241ActivityAtDate(source, calcDate);

        // Assert
        Assert.True(result.IsCalculated);
        Assert.NotNull(result.CurrentActivityBq);
        double expectedActivityBq = (rawValue * conversionToBq) * 0.5;
        Assert.Equal(expectedActivityBq, result.CurrentActivityBq.Value, precision: 0);
    }

    [Fact]
    public void Am241Activity_MissingCalibrationDate_ReturnsUncalculated()
    {
        // Arrange
        var unit = new ActivityUnit { UnitName = "Becquerel", UnitSymbol = "Bq", ConversionToBq = 1.0 };
        var source = new NeutronSource
        {
            Am241ActivityValue = 1.0e9,
            Am241ActivityUnitId = Guid.NewGuid(),
            Am241ActivityUnit = unit,
            CalibrationDate = null
        };

        // Act
        var result = _decayService.CalculateCurrentAm241Activity(source);

        // Assert
        Assert.False(result.IsCalculated);
        Assert.Equal(NeutronDecayCalculationStatus.MissingCalibrationDate, result.Status);
        Assert.Null(result.CurrentActivityBq);
    }

    [Fact]
    public void Am241Activity_CalculationDateBeforeCalibrationDate_ReturnsUncalculated()
    {
        // Arrange
        var unit = new ActivityUnit { UnitName = "Becquerel", UnitSymbol = "Bq", ConversionToBq = 1.0 };
        var source = new NeutronSource
        {
            Am241ActivityValue = 1.0e9,
            Am241ActivityUnitId = Guid.NewGuid(),
            Am241ActivityUnit = unit,
            CalibrationDate = new DateTime(2025, 6, 1)
        };

        // Act
        var result = _decayService.CalculateAm241ActivityAtDate(source, new DateTime(2024, 1, 1));

        // Assert
        Assert.False(result.IsCalculated);
        Assert.Equal(NeutronDecayCalculationStatus.CalculationDatePrecedesCalibrationDate, result.Status);
        Assert.Null(result.CurrentActivityBq);
    }

    [Fact]
    public void Am241Activity_MissingSource_ReturnsUncalculated()
    {
        // Act
        var result = _decayService.CalculateCurrentAm241Activity(null);

        // Assert
        Assert.False(result.IsCalculated);
        Assert.Equal(NeutronDecayCalculationStatus.MissingSource, result.Status);
        Assert.Null(result.CurrentActivityBq);
    }
}
