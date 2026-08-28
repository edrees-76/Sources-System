using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Moq;
using Sources.Helpers;
using Sources.Models;
using Sources.Services;
using Sources.ViewModels;
using Xunit;

namespace Sources.Tests;

public class SourceDetailsViewModelTests
{
    private readonly ActivityUnit _unitCi;
    private readonly ActivityUnit _unitMci;
    private readonly ActivityUnit _unitBq;
    private readonly Location _testLocation;

    public SourceDetailsViewModelTests()
    {
        _unitCi = new ActivityUnit { Id = Guid.NewGuid(), UnitName = "Curie", UnitSymbol = "Ci", ConversionToBq = 3.7e10 };
        _unitMci = new ActivityUnit { Id = Guid.NewGuid(), UnitName = "Millicurie", UnitSymbol = "mCi", ConversionToBq = 3.7e7 };
        _unitBq = new ActivityUnit { Id = Guid.NewGuid(), UnitName = "Becquerel", UnitSymbol = "Bq", ConversionToBq = 1.0 };

        _testLocation = new Location
        {
            Id = Guid.NewGuid(),
            LocationName = "مستودع المصادر المشعة",
            LocationType = "Storage",
            Building = "المبنى 2",
            Room = "G-10"
        };
    }

    [Fact]
    public void Constructor_SingleIsotopeSource_InitializesPropertiesCorrectly()
    {
        // Arrange
        var source = new Source
        {
            Id = Guid.NewGuid(),
            SourceCode = "SRC-2024-001",
            SerialNumber = "SN-9988",
            Manufacturer = "GammaTech",
            IsSealed = true,
            Status = "InUse",
            Location = _testLocation,
            CalibrationDate = new DateTime(2024, 1, 15),
            CurrentActivityValue = 250.5,
            CurrentActivityUnit = _unitMci,
            Notes = "مصدر في حالة ممتازة",
            Radioisotope = new Radioisotope
            {
                Symbol = "Cs-137",
                RadiationType = "Gamma",
                GammaConstant = 0.081
            }
        };

        // Act
        var vm = new SourceDetailsViewModel(source);

        // Assert
        Assert.Equal("SRC-2024-001", vm.SourceCode);
        Assert.Equal("SRC-2024-001", vm.DisplaySourceCode);
        Assert.Equal("InUse", vm.Status);
        Assert.Equal("قيد الاستخدام", vm.ArabicStatus);
        Assert.Equal("#3FAE7A", vm.StatusColor);
        Assert.Equal("SN-9988", vm.SerialNumber);
        Assert.Equal("مستودع المصادر المشعة", vm.LocationName);
        Assert.Equal("2024-01-15", vm.CalibrationDateDisplay);
        Assert.Equal("GammaTech", vm.Manufacturer);
        Assert.Equal("مصدر مختوم", vm.SourceTypeDisplay);
        Assert.True(vm.HasNotes);
        Assert.Equal("مصدر في حالة ممتازة", vm.Notes);

        // Isotopes card
        Assert.Single(vm.Isotopes);
        Assert.Equal("Cs-137", vm.Isotopes[0].Symbol);
        Assert.Equal("250.5000", vm.Isotopes[0].ActivityDisplay);
        Assert.Equal("mCi", vm.Isotopes[0].UnitSymbol);

        // Dose rate card
        Assert.True(vm.HasContributingIsotopes);
        Assert.False(vm.HasDoseRateWarning);
        Assert.NotEmpty(vm.DisplayDoseRate);
        Assert.NotEmpty(vm.EquivalentDoseRatesDisplay);
        Assert.Single(vm.DoseRateContributions);
        Assert.Equal("Cs-137", vm.DoseRateContributions[0].Symbol);
        Assert.True(vm.DoseRateContributions[0].IsContributing);
        Assert.False(vm.DoseRateContributions[0].IsWarning);
    }

    [Fact]
    public void Constructor_MultiIsotopeSource_InitializesAllIsotopes()
    {
        // Arrange
        var iso1 = new Radioisotope { Symbol = "Co-60", RadiationType = "Gamma", GammaConstant = 0.35 };
        var iso2 = new Radioisotope { Symbol = "Sr-90", RadiationType = "Beta" };

        var source = new Source
        {
            Id = Guid.NewGuid(),
            SourceCode = "MIX-001",
            HasDetailedIsotopes = true,
            Status = "Storage",
            Location = _testLocation,
            CalibrationDate = new DateTime(2023, 6, 1),
            SourceIsotopes = new List<SourceIsotope>
            {
                new SourceIsotope
                {
                    Radioisotope = iso1,
                    CurrentActivityValue = 50.0,
                    ActivityUnit = _unitMci
                },
                new SourceIsotope
                {
                    Radioisotope = iso2,
                    CurrentActivityValue = 100.0,
                    ActivityUnit = _unitMci
                }
            }
        };

        // Act
        var vm = new SourceDetailsViewModel(source);

        // Assert
        Assert.Equal("Storage", vm.Status);
        Assert.Equal("مخزن", vm.ArabicStatus);
        Assert.Equal("#4F7FA3", vm.StatusColor);
        Assert.Equal(2, vm.Isotopes.Count);
        Assert.Equal("Co-60", vm.Isotopes[0].Symbol);
        Assert.Equal("50", vm.Isotopes[0].ActivityDisplay);
        Assert.Equal("mCi", vm.Isotopes[0].UnitSymbol);
        Assert.Equal("Sr-90", vm.Isotopes[1].Symbol);
        Assert.Equal("100", vm.Isotopes[1].ActivityDisplay);
        Assert.Equal("mCi", vm.Isotopes[1].UnitSymbol);

        // Dose rate contributions
        Assert.Equal(2, vm.DoseRateContributions.Count);
        Assert.True(vm.DoseRateContributions[0].IsContributing);
        Assert.False(vm.DoseRateContributions[1].IsContributing);
        Assert.True(vm.DoseRateContributions[1].IsWarning);
    }

    [Fact]
    public void Constructor_NonGammaSource_SetsWarningStateProperly()
    {
        // Arrange
        var source = new Source
        {
            Id = Guid.NewGuid(),
            SourceCode = "ALPHA-01",
            Status = "Waste",
            CalibrationDate = DateTime.Now,
            CurrentActivityValue = 10,
            CurrentActivityUnit = _unitUci ?? _unitMci,
            Radioisotope = new Radioisotope
            {
                Symbol = "Po-210",
                RadiationType = "Alpha"
            }
        };

        // Act
        var vm = new SourceDetailsViewModel(source);

        // Assert
        Assert.Equal("Waste", vm.Status);
        Assert.Equal("نفايات", vm.ArabicStatus);
        Assert.Equal("#E0A93E", vm.StatusColor);
        Assert.False(vm.HasContributingIsotopes);
        Assert.True(vm.HasDoseRateWarning);
        Assert.Contains("ألفا/بيتا", vm.DoseRateWarningText);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("N/A", false)]
    [InlineData("—", false)]
    [InlineData("ملاحظة هامة جداً", true)]
    public void NotesVisibility_HandlesEmptyAndPlaceholdersCorrectly(string? notes, bool expectedHasNotes)
    {
        // Arrange
        var source = new Source
        {
            Id = Guid.NewGuid(),
            SourceCode = "TEST-NOTES",
            Status = "InUse",
            CalibrationDate = DateTime.Now,
            Notes = notes,
            Radioisotope = new Radioisotope { Symbol = "Am-241" }
        };

        // Act
        var vm = new SourceDetailsViewModel(source);

        // Assert
        Assert.Equal(expectedHasNotes, vm.HasNotes);
    }

    [Fact]
    public void SourceNavigationHelper_OpenSourceDetails_RoutesCustomActionCorrectly()
    {
        // Arrange
        var source = new Source
        {
            Id = Guid.NewGuid(),
            SourceCode = "SRC-TEST-01",
            Status = "InUse",
            CalibrationDate = DateTime.Now,
            Radioisotope = new Radioisotope { Symbol = "Cs-137" }
        };

        Source? interceptedSource = null;
        SourceNavigationHelper.CustomOpenAction = s => interceptedSource = s;

        try
        {
            // Act: Using direct source
            SourceNavigationHelper.OpenSourceDetails(source);
            Assert.Same(source, interceptedSource);

            // Act: Using DeletedSourceRow
            interceptedSource = null;
            var dsr = new DeletedSourceRow { Source = source };
            SourceNavigationHelper.OpenSourceDetails(dsr);
            Assert.Same(source, interceptedSource);

            // Act: Using DashboardSourceRow
            interceptedSource = null;
            var dbr = new DashboardSourceRow { Source = source };
            SourceNavigationHelper.OpenSourceDetails(dbr);
            Assert.Same(source, interceptedSource);

            // Act: Using AlertRow
            interceptedSource = null;
            var ar = new AlertRow { Alert = new AlertNotification { Source = source } };
            SourceNavigationHelper.OpenSourceDetails(ar);
            Assert.Same(source, interceptedSource);

            // Act: Using LocationSourceRow
            interceptedSource = null;
            var lsr = new LocationSourceRow { Source = source };
            SourceNavigationHelper.OpenSourceDetails(lsr);
            Assert.Same(source, interceptedSource);

            // Act: Using BorrowRequestRow
            interceptedSource = null;
            var brr = new BorrowRequestRow { Request = new BorrowRequest { Source = source } };
            SourceNavigationHelper.OpenSourceDetails(brr);
            Assert.Same(source, interceptedSource);

            // Act: Using LeakTestRecord
            interceptedSource = null;
            var ltr = new LeakTestRecord { Source = source };
            SourceNavigationHelper.OpenSourceDetails(ltr);
            Assert.Same(source, interceptedSource);

            // Act: Using ReportInventoryRow
            interceptedSource = null;
            var rir = new ReportInventoryRow { Source = source };
            SourceNavigationHelper.OpenSourceDetails(rir);
            Assert.Same(source, interceptedSource);

            // Act: Using ReportBorrowingRow
            interceptedSource = null;
            var rbr = new ReportBorrowingRow { Request = new BorrowRequest { Source = source } };
            SourceNavigationHelper.OpenSourceDetails(rbr);
            Assert.Same(source, interceptedSource);

            // Act: Using ReportActivityRow
            interceptedSource = null;
            var rar = new ReportActivityRow { Source = source };
            SourceNavigationHelper.OpenSourceDetails(rar);
            Assert.Same(source, interceptedSource);

            // Act: Using ReportLowActivityRow
            interceptedSource = null;
            var rlar = new ReportLowActivityRow { Source = source };
            SourceNavigationHelper.OpenSourceDetails(rlar);
            Assert.Same(source, interceptedSource);

            // Act: Using ReportLowActivityAlertRow
            interceptedSource = null;
            var rlaar = new ReportLowActivityAlertRow { Source = source };
            SourceNavigationHelper.OpenSourceDetails(rlaar);
            Assert.Same(source, interceptedSource);
        }
        finally
        {
            SourceNavigationHelper.CustomOpenAction = null;
        }
    }

    private ActivityUnit _unitUci => new ActivityUnit { Id = Guid.NewGuid(), UnitName = "Microcurie", UnitSymbol = "µCi", ConversionToBq = 3.7e4 };
}
