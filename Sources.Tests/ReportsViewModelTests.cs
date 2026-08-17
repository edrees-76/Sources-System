using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using Sources.Models;
using Sources.Services;
using Sources.ViewModels;
using Xunit;

namespace Sources.Tests;

public class ReportsViewModelTests
{
    private readonly Mock<ISourceService> _mockSourceService;
    private readonly Mock<IBorrowService> _mockBorrowService;
    private readonly Mock<IReportingService> _mockReportingService;
    private readonly Mock<ISystemSettingsService> _mockSettingsService;

    public ReportsViewModelTests()
    {
        _mockSourceService = new Mock<ISourceService>();
        _mockBorrowService = new Mock<IBorrowService>();
        _mockReportingService = new Mock<IReportingService>();
        _mockSettingsService = new Mock<ISystemSettingsService>();

        _mockSettingsService
            .Setup(s => s.GetSetting("LowActivityThresholdPercent", 10.0))
            .Returns(10.0);
    }

    #region 1. ConvertHalfLifeToSeconds Tests

    [Theory]
    [InlineData(10, "seconds", 10)]
    [InlineData(10, "SECOND", 10)]
    [InlineData(10, "s", 10)]
    [InlineData(2, "minutes", 120)]
    [InlineData(2, "minute", 120)]
    [InlineData(2, "min", 120)]
    [InlineData(2, "m", 120)]
    [InlineData(3, "hours", 3 * 3600)]
    [InlineData(3, "hour", 3 * 3600)]
    [InlineData(3, "h", 3 * 3600)]
    [InlineData(5, "days", 5 * 86400)]
    [InlineData(5, "day", 5 * 86400)]
    [InlineData(5, "d", 5 * 86400)]
    [InlineData(6, "months", 6 * 30 * 86400)]
    [InlineData(6, "month", 6 * 30 * 86400)]
    [InlineData(6, "mo", 6 * 30 * 86400)]
    [InlineData(1, "years", 1 * 365.25 * 86400)]
    [InlineData(1, "year", 1 * 365.25 * 86400)]
    [InlineData(1, "yr", 1 * 365.25 * 86400)]
    [InlineData(1, "y", 1 * 365.25 * 86400)]
    [InlineData(1, "UNKNOWN", 1 * 365.25 * 86400)]
    [InlineData(1, null, 1 * 365.25 * 86400)]
    public void ConvertHalfLifeToSeconds_SupportsAllUnitsAndAbbreviations(double value, string? unit, double expected)
    {
        var result = ReportsViewModel.ConvertHalfLifeToSeconds(value, unit);
        Assert.Equal(expected, result, precision: 3);
    }

    #endregion

    #region 2. CalculateMaxHalfLivesElapsed Tests

    [Fact]
    public void CalculateMaxHalfLivesElapsed_SingleIsotope_ReturnsCorrectHalfLivesAndSymbol()
    {
        var isotope = new Radioisotope
        {
            Symbol = "Cs-137",
            HalfLife = 30.0,
            HalfLifeUnit = "years"
        };

        var source = new Source
        {
            SourceCode = "SRC-001",
            Radioisotope = isotope,
            CalibrationDate = DateTime.Now.AddDays(-60.0 * 365.25) // 2 half lives
        };

        var (maxHalfLives, worstIsotope) = ReportsViewModel.CalculateMaxHalfLivesElapsed(source);

        Assert.Equal("Cs-137", worstIsotope);
        Assert.True(maxHalfLives >= 1.99 && maxHalfLives <= 2.01);
    }

    [Fact]
    public void CalculateMaxHalfLivesElapsed_MultiIsotope_IdentifiesWorstIsotope()
    {
        var isoSlow = new Radioisotope { Symbol = "Cs-137", HalfLife = 30.0, HalfLifeUnit = "years" };
        var isoFast = new Radioisotope { Symbol = "Co-60", HalfLife = 5.27, HalfLifeUnit = "years" };

        var source = new Source
        {
            SourceCode = "SRC-MULTI",
            HasDetailedIsotopes = true,
            CalibrationDate = DateTime.Now.AddDays(-10.54 * 365.25), // 2 half-lives for Co-60, ~0.35 for Cs-137
            SourceIsotopes = new List<SourceIsotope>
            {
                new SourceIsotope { Radioisotope = isoSlow, CalibrationDate = DateTime.Now.AddDays(-10.54 * 365.25) },
                new SourceIsotope { Radioisotope = isoFast, CalibrationDate = DateTime.Now.AddDays(-10.54 * 365.25) }
            }
        };

        var (maxHalfLives, worstIsotope) = ReportsViewModel.CalculateMaxHalfLivesElapsed(source);

        Assert.Equal("Co-60", worstIsotope);
        Assert.True(maxHalfLives >= 1.99 && maxHalfLives <= 2.01);
    }

    #endregion

    #region 3. LowActivityAlertReport Classification & Loading

    [Fact]
    public void LoadReport_LowActivityAlertReport_ClassifiesWarningAndCriticalCorrectly()
    {
        var iso = new Radioisotope { Symbol = "Co-60", HalfLife = 1.0, HalfLifeUnit = "years" };

        var safeSource = new Source
        {
            SourceCode = "SRC-SAFE",
            Radioisotope = iso,
            CalibrationDate = DateTime.Now.AddDays(-3.0 * 365.25), // 3 T½ (< 5)
            Status = "InUse"
        };

        var warningSource = new Source
        {
            SourceCode = "SRC-WARN",
            Radioisotope = iso,
            CalibrationDate = DateTime.Now.AddDays(-5.5 * 365.25), // 5.5 T½ (Warning: 5 <= t < 6)
            Status = "InUse"
        };

        var criticalSource = new Source
        {
            SourceCode = "SRC-CRIT",
            Radioisotope = iso,
            CalibrationDate = DateTime.Now.AddDays(-7.0 * 365.25), // 7 T½ (Critical: >= 6)
            Status = "InUse"
        };

        _mockSourceService.Setup(s => s.GetAllSources())
            .Returns(new List<Source> { safeSource, warningSource, criticalSource });

        var vm = new ReportsViewModel(_mockSourceService.Object, _mockBorrowService.Object, _mockReportingService.Object, _mockSettingsService.Object);
        vm.SelectReportCommand.Execute("LowActivityAlertReport");

        Assert.Equal(2, vm.LowActivityAlertData.Count);

        // First item should be Critical (sorted descending by severity, then by half lives)
        var first = vm.LowActivityAlertData[0];
        Assert.Equal("SRC-CRIT", first.SourceCode);
        Assert.Equal("Critical", first.AlertSeverity);
        Assert.Equal("حرج", first.AlertSeverityDisplay);
        Assert.Equal("Co-60", first.AlertWorstIsotope);

        // Second item should be Warning
        var second = vm.LowActivityAlertData[1];
        Assert.Equal("SRC-WARN", second.SourceCode);
        Assert.Equal("Warning", second.AlertSeverity);
        Assert.Equal("تحذير", second.AlertSeverityDisplay);
        Assert.Equal("Co-60", second.AlertWorstIsotope);
    }

    [Fact]
    public void LoadReport_GeneralReport_PopulatesLowActivityAlertDataWithClassification()
    {
        var iso = new Radioisotope { Symbol = "Co-60", HalfLife = 1.0, HalfLifeUnit = "years" };
        var warningSource = new Source
        {
            SourceCode = "SRC-WARN",
            Radioisotope = iso,
            CalibrationDate = DateTime.Now.AddDays(-5.2 * 365.25),
            Status = "InUse"
        };

        _mockSourceService.Setup(s => s.GetAllSources())
            .Returns(new List<Source> { warningSource });
        _mockBorrowService.Setup(b => b.GetAll())
            .Returns(new List<BorrowRequest>());
        _mockSourceService.Setup(s => s.GetLowActivitySources(It.IsAny<double>()))
            .Returns(new List<Source>());

        var vm = new ReportsViewModel(_mockSourceService.Object, _mockBorrowService.Object, _mockReportingService.Object, _mockSettingsService.Object);
        vm.SelectReportCommand.Execute("GeneralReport");

        Assert.Single(vm.LowActivityAlertData);
        Assert.Equal("SRC-WARN", vm.LowActivityAlertData[0].SourceCode);
        Assert.Equal("Warning", vm.LowActivityAlertData[0].AlertSeverity);
    }

    #endregion
}
