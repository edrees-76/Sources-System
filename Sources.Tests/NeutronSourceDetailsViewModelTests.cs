using System;
using Moq;
using Sources.Models;
using Sources.Services;
using Sources.ViewModels;
using Xunit;

namespace Sources.Tests;

public class NeutronSourceDetailsViewModelTests
{
    private readonly ActivityUnit _unitBq;
    private readonly ActivityUnit _unitMBq;

    public NeutronSourceDetailsViewModelTests()
    {
        _unitBq = new ActivityUnit { Id = Guid.NewGuid(), UnitName = "Becquerel", UnitSymbol = "Bq", ConversionToBq = 1.0 };
        _unitMBq = new ActivityUnit { Id = Guid.NewGuid(), UnitName = "Megabecquerel", UnitSymbol = "MBq", ConversionToBq = 1e6 };
    }

    [Fact]
    public void CurrentActivityDisplay_ConvertsToSourceActivityUnit_InsteadOfFixedBq()
    {
        // Arrange
        var source = new NeutronSource
        {
            Id = Guid.NewGuid(),
            SourceCode = "NS-001",
            Status = "InUse",
            ActivityUnit = _unitMBq
        };

        var decayServiceMock = new Mock<INeutronDecayCalculationService>();
        decayServiceMock
            .Setup(s => s.CalculateCurrentSourceActivity(source))
            .Returns(new NeutronDecayResult
            {
                Status = NeutronDecayCalculationStatus.Calculated,
                CurrentActivityBq = 5_000_000.0 // 5,000,000 Bq == 5 MBq
            });

        var vm = new NeutronSourceDetailsViewModel(source, decayService: decayServiceMock.Object);

        // Act
        string display = vm.CurrentActivityDisplay;

        // Assert: value must be expressed in MBq (source.ActivityUnit), not raw Bq
        Assert.Equal("5.0000 MBq", display);
    }

    [Fact]
    public void CurrentActivityDisplay_FallsBackToBq_WhenActivityUnitMissing()
    {
        // Arrange
        var source = new NeutronSource
        {
            Id = Guid.NewGuid(),
            SourceCode = "NS-002",
            Status = "InUse",
            ActivityUnit = null
        };

        var decayServiceMock = new Mock<INeutronDecayCalculationService>();
        decayServiceMock
            .Setup(s => s.CalculateCurrentSourceActivity(source))
            .Returns(new NeutronDecayResult
            {
                Status = NeutronDecayCalculationStatus.Calculated,
                CurrentActivityBq = 123.456
            });

        var vm = new NeutronSourceDetailsViewModel(source, decayService: decayServiceMock.Object);

        // Act
        string display = vm.CurrentActivityDisplay;

        // Assert: safe fallback to Bq
        Assert.Equal("123.4560 Bq", display);
    }

    [Fact]
    public void CurrentActivityDisplay_FallsBackToBq_WhenConversionToBqIsZero()
    {
        // Arrange
        var zeroConversionUnit = new ActivityUnit { Id = Guid.NewGuid(), UnitName = "Invalid", UnitSymbol = "XX", ConversionToBq = 0 };
        var source = new NeutronSource
        {
            Id = Guid.NewGuid(),
            SourceCode = "NS-003",
            Status = "InUse",
            ActivityUnit = zeroConversionUnit
        };

        var decayServiceMock = new Mock<INeutronDecayCalculationService>();
        decayServiceMock
            .Setup(s => s.CalculateCurrentSourceActivity(source))
            .Returns(new NeutronDecayResult
            {
                Status = NeutronDecayCalculationStatus.Calculated,
                CurrentActivityBq = 987.0
            });

        var vm = new NeutronSourceDetailsViewModel(source, decayService: decayServiceMock.Object);

        // Act
        string display = vm.CurrentActivityDisplay;

        // Assert: division by zero must never happen — safe fallback to Bq
        Assert.Equal("987.0000 Bq", display);
    }

    [Theory]
    [InlineData(1.9, "1.9\u200e%")]
    [InlineData(2.5, "2.5\u200e%")]
    public void UncertaintyFormatted_ContainsLrmBeforePercent_PreventingBidiFlip(double percent, string expected)
    {
        var source = new NeutronSource
        {
            Id = Guid.NewGuid(),
            SourceCode = "NS-UNC",
            RelativeExpandedUncertaintyPercent = percent
        };

        var vm = new NeutronSourceDetailsViewModel(source);

        Assert.Equal(expected, vm.UncertaintyFormatted);
        // Specifically assert that '%' is preceded by LRM (U+200E) and NOT inverted to "%1.9"
        Assert.EndsWith("\u200e%", vm.UncertaintyFormatted);
        Assert.False(vm.UncertaintyFormatted.StartsWith("%"));
    }

    [Fact]
    public void ReportNeutronInventoryRow_RelativeUncertainty_ContainsLrmBeforePercent()
    {
        var source = new NeutronSource
        {
            Id = Guid.NewGuid(),
            SourceCode = "NS-UNC",
            RelativeExpandedUncertaintyPercent = 1.9
        };

        var row = new ReportNeutronInventoryRow { Source = source };
        Assert.Equal("1.9\u200e%", row.RelativeUncertainty);
        Assert.EndsWith("\u200e%", row.RelativeUncertainty);
        Assert.False(row.RelativeUncertainty.StartsWith("%"));
    }
}
