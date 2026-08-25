using System;
using System.Collections.Generic;
using System.Linq;
using Moq;
using Sources.Models;
using Sources.Services;
using Sources.ViewModels;
using Xunit;

namespace Sources.Tests;

public class DoseRateCalculationTests
{
    private readonly DecayCalculationService _decayService = new();

    [Fact]
    public void CalculateDoseRate_SingleGammaIsotope_CalculatesCorrectly()
    {
        // Cs-137: 100 MBq, GammaConstant = 0.0772 µSv·m²/(MBq·h)
        var isotope = new Radioisotope
        {
            Id = Guid.NewGuid(),
            Name = "Cesium-137",
            Symbol = "Cs-137",
            RadiationType = "Beta/Gamma",
            GammaConstant = 0.0772
        };

        Assert.True(isotope.IsGammaEmitter);

        var result = _decayService.CalculateDoseRateAtOneMeter(new[] { (isotope, 100.0) });

        Assert.Equal(7.72, result.TotalDoseRateMicroSvPerHour, precision: 4);
        Assert.Equal(7.72 * 0.115, result.TotalDoseRatemRPerHour, precision: 4);
        Assert.Equal(7.72 * 0.1, result.TotalDoseRatemremPerHour, precision: 4);

        Assert.Single(result.Contributions);
        var contrib = result.Contributions[0];
        Assert.Equal(DoseRateContributionStatus.Contributing, contrib.Status);
        Assert.Equal(7.72, contrib.ContributionMicroSvPerHour, precision: 4);
        Assert.True(result.HasContributingIsotopes);
        Assert.False(result.HasMissingData);
        Assert.False(result.IsAllNonGamma);
    }

    [Fact]
    public void CalculateDoseRate_PureAlphaOrBetaIsotope_ReportsNonGammaEmitter()
    {
        var betaIsotope = new Radioisotope
        {
            Id = Guid.NewGuid(),
            Name = "Carbon-14",
            Symbol = "C-14",
            RadiationType = "Beta",
            GammaConstant = null
        };

        Assert.False(betaIsotope.IsGammaEmitter);

        var result = _decayService.CalculateDoseRateAtOneMeter(new[] { (betaIsotope, 500.0) });

        Assert.Equal(0, result.TotalDoseRateMicroSvPerHour);
        Assert.Single(result.Contributions);
        var contrib = result.Contributions[0];
        Assert.Equal(DoseRateContributionStatus.NonGammaEmitter, contrib.Status);
        Assert.Equal("غير مساهم عند هذه المسافة", contrib.StatusText);
        Assert.False(result.HasContributingIsotopes);
        Assert.True(result.IsAllNonGamma);
        Assert.Equal("N/A (α/β)", result.FormattedSummary);
    }

    [Fact]
    public void CalculateDoseRate_GammaIsotopeWithMissingGammaConstant_ReportsMissingData()
    {
        var co60 = new Radioisotope
        {
            Id = Guid.NewGuid(),
            Name = "Cobalt-60",
            Symbol = "Co-60",
            RadiationType = "Gamma",
            GammaConstant = null
        };

        Assert.True(co60.IsGammaEmitter);

        var result = _decayService.CalculateDoseRateAtOneMeter(new[] { (co60, 200.0) });

        Assert.Equal(0, result.TotalDoseRateMicroSvPerHour);
        Assert.Single(result.Contributions);
        var contrib = result.Contributions[0];
        Assert.Equal(DoseRateContributionStatus.MissingGammaConstant, contrib.Status);
        Assert.Equal("بيانات ثابت غاما غير مسجلة", contrib.StatusText);
        Assert.False(result.HasContributingIsotopes);
        Assert.True(result.HasMissingData);
        Assert.Equal("N/A (بيانات غير مسجلة)", result.FormattedSummary);
    }

    [Fact]
    public void CalculateDoseRate_MixedMultiIsotopeSource_CalculatesSumAndItemizesCorrectly()
    {
        // 1. Cs-137: Contributing (100 MBq * 0.0772 = 7.72 µSv/h)
        var cs137 = new Radioisotope
        {
            Id = Guid.NewGuid(),
            Name = "Cesium-137",
            Symbol = "Cs-137",
            RadiationType = "Gamma",
            GammaConstant = 0.0772
        };

        // 2. Co-60: Contributing (50 MBq * 0.308 = 15.4 µSv/h)
        var co60 = new Radioisotope
        {
            Id = Guid.NewGuid(),
            Name = "Cobalt-60",
            Symbol = "Co-60",
            RadiationType = "Beta/Gamma",
            GammaConstant = 0.308
        };

        // 3. H-3: Pure Beta (Non-Gamma)
        var h3 = new Radioisotope
        {
            Id = Guid.NewGuid(),
            Name = "Tritium",
            Symbol = "H-3",
            RadiationType = "Beta",
            GammaConstant = null
        };

        // 4. Ir-192: Gamma with Missing Constant
        var ir192 = new Radioisotope
        {
            Id = Guid.NewGuid(),
            Name = "Iridium-192",
            Symbol = "Ir-192",
            RadiationType = "Gamma",
            GammaConstant = null
        };

        var items = new List<(Radioisotope Isotope, double ActivityMBq)>
        {
            (cs137, 100.0),
            (co60, 50.0),
            (h3, 1000.0),
            (ir192, 25.0)
        };

        var result = _decayService.CalculateDoseRateAtOneMeter(items);

        // Total = 7.72 + 15.4 = 23.12 µSv/h
        Assert.Equal(23.12, result.TotalDoseRateMicroSvPerHour, precision: 4);
        Assert.Equal(23.12 * 0.115, result.TotalDoseRatemRPerHour, precision: 4);
        Assert.Equal(23.12 * 0.1, result.TotalDoseRatemremPerHour, precision: 4);

        Assert.Equal(4, result.Contributions.Count);
        Assert.Equal(DoseRateContributionStatus.Contributing, result.Contributions[0].Status);
        Assert.Equal(DoseRateContributionStatus.Contributing, result.Contributions[1].Status);
        Assert.Equal(DoseRateContributionStatus.NonGammaEmitter, result.Contributions[2].Status);
        Assert.Equal(DoseRateContributionStatus.MissingGammaConstant, result.Contributions[3].Status);

        Assert.True(result.HasContributingIsotopes);
        Assert.True(result.HasMissingData);
        Assert.Contains("(*)", result.FormattedSummary);
    }

    [Fact]
    public void CalculateDoseRateForSource_SingleSource_CalculatesAccurately()
    {
        var isotope = new Radioisotope
        {
            Id = Guid.NewGuid(),
            Name = "Cobalt-60",
            Symbol = "Co-60",
            RadiationType = "Gamma",
            GammaConstant = 0.308
        };
        var unit = new ActivityUnit
        {
            Id = Guid.NewGuid(),
            UnitName = "MegaBecquerel",
            UnitSymbol = "MBq",
            ConversionToBq = 1e6
        };

        var source = new Source
        {
            Id = Guid.NewGuid(),
            SourceCode = "SRC-CO60",
            RadioisotopeId = isotope.Id,
            Radioisotope = isotope,
            CurrentActivityUnit = unit,
            CurrentActivityValue = 200.0 // 200 MBq
        };

        var result = _decayService.CalculateDoseRateAtOneMeterForSource(source);

        // 200 MBq * 0.308 = 61.6 µSv/h
        Assert.Equal(61.6, result.TotalDoseRateMicroSvPerHour, precision: 4);
    }

    [Fact]
    public void UnitConversions_ConvertAccurately()
    {
        double microSv = 10.0;
        double mR = _decayService.ConvertDoseRateMicroSvTomR(microSv);
        double mrem = _decayService.ConvertDoseRateMicroSvTomrem(microSv);

        Assert.Equal(1.15, mR, precision: 4);
        Assert.Equal(1.0, mrem, precision: 4);
    }

    [Fact]
    public void RadioisotopesViewModel_GammaConstantValidation_ValidatesCorrectly()
    {
        var mockService = new Mock<IRadioisotopeService>();
        mockService.Setup(s => s.GetAll()).Returns(new List<Radioisotope>());
        Radioisotope? created = null;
        mockService.Setup(s => s.Create(It.IsAny<Radioisotope>()))
            .Callback<Radioisotope>(r => created = r)
            .Returns((true, "تم الإضافة بنجاح"));

        var vm = new RadioisotopesViewModel(mockService.Object);
        vm.AddNewCommand.Execute(null);

        // Invalid: non-numeric
        vm.EditName = "Test-1";
        vm.EditSymbol = "T-1";
        vm.EditGammaConstantText = "abc";
        vm.SaveCommand.Execute(null);
        Assert.True(vm.HasMessage);
        Assert.Null(created);

        // Invalid: negative or zero
        vm.EditGammaConstantText = "-0.5";
        vm.SaveCommand.Execute(null);
        Assert.True(vm.HasMessage);
        Assert.Null(created);

        // Valid: positive double
        vm.EditGammaConstantText = "0.0772";
        vm.SaveCommand.Execute(null);
        Assert.NotNull(created);
        Assert.Equal(0.0772, created!.GammaConstant);

        // Valid: empty (null)
        created = null;
        vm.EditGammaConstantText = "";
        vm.SaveCommand.Execute(null);
        Assert.NotNull(created);
        Assert.Null(created!.GammaConstant);
    }
}
