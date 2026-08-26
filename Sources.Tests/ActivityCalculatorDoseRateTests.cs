using System;
using System.Collections.Generic;
using Moq;
using Sources.Models;
using Sources.Services;
using Sources.ViewModels;
using Xunit;

namespace Sources.Tests;

public class ActivityCalculatorDoseRateTests
{
    private readonly Mock<IRadioisotopeService> _isotopeServiceMock;
    private readonly DecayCalculationService _decayService;

    public ActivityCalculatorDoseRateTests()
    {
        _isotopeServiceMock = new Mock<IRadioisotopeService>();
        _decayService = new DecayCalculationService();

        var isotopes = new List<Radioisotope>
        {
            new Radioisotope
            {
                Id = Guid.NewGuid(),
                Name = "Cesium-137",
                Symbol = "Cs-137",
                HalfLife = 30.08,
                HalfLifeUnit = "years",
                RadiationType = "Beta/Gamma",
                GammaConstant = 0.0772 // µSv·m²/(MBq·h) at 1m
            },
            new Radioisotope
            {
                Id = Guid.NewGuid(),
                Name = "Cobalt-60",
                Symbol = "Co-60",
                HalfLife = 5.27,
                HalfLifeUnit = "years",
                RadiationType = "Gamma",
                GammaConstant = 0.3050 // µSv·m²/(MBq·h) at 1m
            },
            new Radioisotope
            {
                Id = Guid.NewGuid(),
                Name = "Strontium-90",
                Symbol = "Sr-90",
                HalfLife = 28.8,
                HalfLifeUnit = "years",
                RadiationType = "Beta",
                GammaConstant = null // Non-gamma emitter or missing gamma
            }
        };

        _isotopeServiceMock.Setup(s => s.GetAll()).Returns(isotopes);
    }

    [Fact]
    public void DoseRate_AtOneMeter_CalculatesCorrectlyForInventoryIsotope()
    {
        var vm = new ActivityCalculatorViewModel(_isotopeServiceMock.Object, _decayService);
        var cs137 = vm.Isotopes[0]; // Cs-137

        vm.IsFromDatabase = true;
        vm.SelectedIsotope = cs137;
        vm.InitialActivityText = "100";
        vm.InitialActivityUnit = "MBq";
        vm.CalibrationDate = DateTime.Today;
        vm.CalculationDate = DateTime.Today; // 0 time elapsed -> Activity = 100 MBq
        vm.DistanceText = "1.0";

        vm.CalculateCommand.Execute(null);

        Assert.False(vm.HasError);
        Assert.True(vm.HasResult);
        Assert.True(vm.HasDoseRateResult);

        // Dose at 1m = 100 MBq * 0.0772 = 7.72 µSv/h = 0.00772 mSv/h = 0.772 mrem/h
        Assert.Equal(7.72, vm.DoseRateMicroSvPerHour, precision: 4);
        Assert.Contains("0.0077", vm.DoseRateAtDistanceMSvText);
        Assert.Contains("mSv/h", vm.DoseRateAtDistanceMSvText);
        Assert.Contains("0.772", vm.DoseRateAtDistanceMremText);
        Assert.Contains("mrem/h", vm.DoseRateAtDistanceMremText);
        Assert.Equal("@ 1 m", vm.DoseRateDistanceText);
    }

    [Fact]
    public void DoseRate_InverseSquareLaw_AtTwoMeters_IsOneFourth()
    {
        var vm = new ActivityCalculatorViewModel(_isotopeServiceMock.Object, _decayService);
        var cs137 = vm.Isotopes[0]; // Cs-137

        vm.IsFromDatabase = true;
        vm.SelectedIsotope = cs137;
        vm.InitialActivityText = "100";
        vm.InitialActivityUnit = "MBq";
        vm.CalibrationDate = DateTime.Today;
        vm.CalculationDate = DateTime.Today;
        vm.DistanceText = "2.0";

        vm.CalculateCommand.Execute(null);

        Assert.True(vm.HasDoseRateResult);
        // Dose at 2m = 7.72 / (2^2) = 7.72 / 4 = 1.93 µSv/h = 0.00193 mSv/h
        Assert.Equal(1.93, vm.DoseRateMicroSvPerHour, precision: 4);
        Assert.Contains("0.0019", vm.DoseRateAtDistanceMSvText);
        Assert.Contains("0.193", vm.DoseRateAtDistanceMremText);
        Assert.Equal("@ 2 m", vm.DoseRateDistanceText);
    }

    [Fact]
    public void DoseRate_InverseSquareLaw_AtHalfMeter_IsFourTimes()
    {
        var vm = new ActivityCalculatorViewModel(_isotopeServiceMock.Object, _decayService);
        var cs137 = vm.Isotopes[0]; // Cs-137

        vm.IsFromDatabase = true;
        vm.SelectedIsotope = cs137;
        vm.InitialActivityText = "100";
        vm.InitialActivityUnit = "MBq";
        vm.CalibrationDate = DateTime.Today;
        vm.CalculationDate = DateTime.Today;
        vm.DistanceText = "0.5";

        vm.CalculateCommand.Execute(null);

        Assert.True(vm.HasDoseRateResult);
        // Dose at 0.5m = 7.72 / (0.5^2) = 7.72 / 0.25 = 30.88 µSv/h = 0.03088 mSv/h
        Assert.Equal(30.88, vm.DoseRateMicroSvPerHour, precision: 4);
        Assert.Contains("0.0309", vm.DoseRateAtDistanceMSvText);
        Assert.Contains("3.088", vm.DoseRateAtDistanceMremText);
        Assert.Equal("@ 0.5 m", vm.DoseRateDistanceText);
    }

    [Fact]
    public void DoseRate_InventoryIsotope_WithoutGammaConstant_HidesDoseRateAndDisplaysNotAvailableMessage()
    {
        var vm = new ActivityCalculatorViewModel(_isotopeServiceMock.Object, _decayService);
        var sr90 = vm.Isotopes[2]; // Sr-90 without GammaConstant

        vm.IsFromDatabase = true;
        vm.SelectedIsotope = sr90;

        Assert.False(vm.IsGammaConstantAvailable);
        Assert.Contains("غير متوفر", vm.DatabaseGammaConstantText);

        vm.InitialActivityText = "100";
        vm.InitialActivityUnit = "MBq";
        vm.CalibrationDate = DateTime.Today.AddYears(-1);
        vm.CalculationDate = DateTime.Today;
        vm.DistanceText = "1.0";

        vm.CalculateCommand.Execute(null);

        Assert.False(vm.HasError);
        Assert.True(vm.HasResult); // Standard decay activity calculated successfully
        Assert.False(vm.HasDoseRateResult); // Dose rate KPI is hidden
        Assert.Equal(0, vm.DoseRateMicroSvPerHour);
    }

    [Fact]
    public void DoseRate_ManualInput_CalculatesCorrectlyInBothModes()
    {
        var vm = new ActivityCalculatorViewModel(_isotopeServiceMock.Object, _decayService);

        // 1. Manual mode - Activity Calculation Mode
        vm.IsManualInput = true;
        vm.InitialActivityText = "200";
        vm.InitialActivityUnit = "MBq";
        vm.HalfLifeValueText = "5.27";
        vm.HalfLifeUnit = "years";
        vm.CalibrationDate = DateTime.Today;
        vm.CalculationDate = DateTime.Today;
        vm.ManualGammaConstantText = "0.305";
        vm.DistanceText = "1.0";

        vm.CalculateCommand.Execute(null);

        Assert.True(vm.HasResult);
        Assert.True(vm.HasDoseRateResult);
        // Dose at 1m = 200 MBq * 0.305 = 61.0 µSv/h = 0.061 mSv/h = 6.1 mrem/h
        Assert.Equal(61.0, vm.DoseRateMicroSvPerHour, precision: 4);
        Assert.Contains("0.061", vm.DoseRateAtDistanceMSvText);
        Assert.Contains("6.1", vm.DoseRateAtDistanceMremText);

        // 2. Manual mode - Time to Target Activity Mode
        vm.SelectedModeIndex = 1; // Time to target mode
        vm.TargetActivityText = "100";
        vm.TargetActivityUnit = "MBq";
        vm.DistanceText = "2.0";

        vm.CalculateCommand.Execute(null);

        Assert.True(vm.HasResult);
        Assert.True(vm.HasDoseRateResult);
        // At target activity 100 MBq and distance 2m:
        // Dose at 1m = 100 * 0.305 = 30.5 µSv/h
        // Dose at 2m = 30.5 / 4 = 7.625 µSv/h = 0.007625 mSv/h
        Assert.Equal(7.625, vm.DoseRateMicroSvPerHour, precision: 4);
        Assert.Contains("0.0076", vm.DoseRateAtDistanceMSvText);
    }

    [Fact]
    public void DoseRate_InvalidOrEmptyDistance_CalculatesActivityNormallyWithoutBreaking()
    {
        var vm = new ActivityCalculatorViewModel(_isotopeServiceMock.Object, _decayService);
        var cs137 = vm.Isotopes[0];

        vm.IsFromDatabase = true;
        vm.SelectedIsotope = cs137;
        vm.InitialActivityText = "100";
        vm.InitialActivityUnit = "MBq";
        vm.CalibrationDate = DateTime.Today.AddYears(-1);
        vm.CalculationDate = DateTime.Today;

        // Empty distance
        vm.DistanceText = string.Empty;
        vm.CalculateCommand.Execute(null);

        Assert.False(vm.HasError);
        Assert.True(vm.HasResult);
        Assert.False(vm.HasDoseRateResult);

        // Negative distance
        vm.DistanceText = "-5";
        vm.CalculateCommand.Execute(null);

        Assert.False(vm.HasError);
        Assert.True(vm.HasResult);
        Assert.False(vm.HasDoseRateResult);
    }

    [Fact]
    public void DoseRate_Reset_RestoresDefaultDistanceAndClearsDoseRateFields()
    {
        var vm = new ActivityCalculatorViewModel(_isotopeServiceMock.Object, _decayService);
        var cs137 = vm.Isotopes[0];

        vm.IsFromDatabase = true;
        vm.SelectedIsotope = cs137;
        vm.InitialActivityText = "100";
        vm.DistanceText = "3.5";

        vm.CalculateCommand.Execute(null);
        Assert.True(vm.HasDoseRateResult);

        vm.ResetCommand.Execute(null);

        Assert.Equal("1", vm.DistanceText);
        Assert.False(vm.HasDoseRateResult);
        Assert.Equal(0, vm.DoseRateMicroSvPerHour);
        Assert.Empty(vm.DoseRateAtDistanceMSvText);
        Assert.Empty(vm.DoseRateAtDistanceMremText);
        Assert.Empty(vm.DatabaseGammaConstantText);
        Assert.Null(vm.SelectedIsotope);
    }
}
