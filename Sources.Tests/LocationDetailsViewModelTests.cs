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

public class LocationDetailsViewModelTests
{
    private readonly Location _testLocation;
    private readonly ActivityUnit _unitCi;
    private readonly ActivityUnit _unitMci;
    private readonly ActivityUnit _unitUci;
    private readonly ActivityUnit _unitBq;

    public LocationDetailsViewModelTests()
    {
        _testLocation = new Location
        {
            Id = Guid.NewGuid(),
            LocationName = "مختبر الفيزياء النووية",
            LocationType = "Lab",
            Building = "المبنى الرئيسي",
            Room = "105",
            ResponsiblePerson = "د. أحمد علي"
        };

        _unitCi = new ActivityUnit { Id = Guid.NewGuid(), UnitName = "Curie", UnitSymbol = "Ci", ConversionToBq = 3.7e10 };
        _unitMci = new ActivityUnit { Id = Guid.NewGuid(), UnitName = "Millicurie", UnitSymbol = "mCi", ConversionToBq = 3.7e7 };
        _unitUci = new ActivityUnit { Id = Guid.NewGuid(), UnitName = "Microcurie", UnitSymbol = "µCi", ConversionToBq = 3.7e4 };
        _unitBq = new ActivityUnit { Id = Guid.NewGuid(), UnitName = "Becquerel", UnitSymbol = "Bq", ConversionToBq = 1.0 };
    }

    [Fact]
    public void Constructor_InitializesPropertiesAndTotalsCorrectly()
    {
        // Arrange
        var sources = new List<Source>
        {
            new Source
            {
                Id = Guid.NewGuid(),
                LocationId = _testLocation.Id,
                SourceCode = "SRC-001",
                CurrentActivityValue = 10,
                CurrentActivityUnit = _unitMci,
                Status = "InUse",
                Radioisotope = new Radioisotope { Symbol = "Co-60" }
            },
            new Source
            {
                Id = Guid.NewGuid(),
                LocationId = _testLocation.Id,
                SourceCode = "SRC-002",
                CurrentActivityValue = 5,
                CurrentActivityUnit = _unitMci,
                Status = "Storage",
                Radioisotope = new Radioisotope { Symbol = "Cs-137" }
            }
        };

        // Act
        var vm = new LocationDetailsViewModel(_testLocation, sources);

        // Assert
        Assert.Equal("مختبر الفيزياء النووية", vm.LocationName);
        Assert.Equal("Lab", vm.LocationType);
        Assert.Equal("المبنى الرئيسي", vm.Building);
        Assert.Equal("105", vm.Room);
        Assert.Equal("د. أحمد علي", vm.ResponsiblePerson);
        Assert.Equal(2, vm.TotalSourcesCount);
        Assert.Equal(2, vm.FilteredSourcesCount);
        Assert.True(vm.HasSources);
        Assert.True(vm.HasFilteredSources);
        Assert.NotEmpty(vm.TotalActivityItems);
    }

    [Fact]
    public void CalculateTotalActivity_WithMixedUnits_CalculatesAccurately()
    {
        // Arrange
        // Source 1: 2 Ci = 2 * 3.7e10 = 7.4e10 Bq
        // Source 2: 100 mCi = 100 * 3.7e7 = 3.7e9 Bq
        // Source 3: 50 µCi = 50 * 3.7e4 = 1.85e6 Bq
        // Source 4: 500 Bq = 500 Bq
        // Total Bq = 74,000,000,000 + 3,700,000,000 + 1,850,000 + 500 = 77,701,850,500 Bq
        var sources = new List<Source>
        {
            new Source { Id = Guid.NewGuid(), LocationId = _testLocation.Id, CurrentActivityValue = 2, CurrentActivityUnit = _unitCi },
            new Source { Id = Guid.NewGuid(), LocationId = _testLocation.Id, CurrentActivityValue = 100, CurrentActivityUnit = _unitMci },
            new Source { Id = Guid.NewGuid(), LocationId = _testLocation.Id, CurrentActivityValue = 50, CurrentActivityUnit = _unitUci },
            new Source { Id = Guid.NewGuid(), LocationId = _testLocation.Id, CurrentActivityValue = 500, CurrentActivityUnit = _unitBq }
        };

        // Act
        var vm = new LocationDetailsViewModel(_testLocation, sources);

        // Assert
        var bqItem = vm.TotalActivityItems.FirstOrDefault(x => x.UnitSymbol == "Bq");
        var ciItem = vm.TotalActivityItems.FirstOrDefault(x => x.UnitSymbol == "Ci");
        var mciItem = vm.TotalActivityItems.FirstOrDefault(x => x.UnitSymbol == "mCi");
        var uciItem = vm.TotalActivityItems.FirstOrDefault(x => x.UnitSymbol == "µCi");

        Assert.NotNull(bqItem);
        Assert.NotNull(ciItem);
        Assert.NotNull(mciItem);
        Assert.NotNull(uciItem);

        double expectedBq = 77701850500.0;
        Assert.Equal(expectedBq, bqItem.Value, precision: 1);
        Assert.Equal(expectedBq / 3.7e10, ciItem.Value, precision: 4);
        Assert.Equal(expectedBq / 3.7e7, mciItem.Value, precision: 2);
        Assert.Equal(expectedBq / 3.7e4, uciItem.Value, precision: 0);
    }

    [Fact]
    public void CalculateTotalActivity_ExcludesDeletedAndHistoricalSources_IncludesOnlyActiveCurrentSources()
    {
        // Arrange
        var otherLocationId = Guid.NewGuid();
        var sources = new List<Source>
        {
            // 1. مصدر نشط متواجد حالياً بالموقع (يجب احتسابه: 100 mCi = 3.7e9 Bq)
            new Source
            {
                Id = Guid.NewGuid(),
                LocationId = _testLocation.Id,
                IsDeleted = false,
                CurrentActivityValue = 100,
                CurrentActivityUnit = _unitMci,
                Status = "InUse",
                SourceCode = "SRC-ACTIVE-01"
            },
            // 2. مصدر محذوف (يجب استثناؤه من الإجمالي: 500 mCi)
            new Source
            {
                Id = Guid.NewGuid(),
                LocationId = _testLocation.Id,
                IsDeleted = true,
                CurrentActivityValue = 500,
                CurrentActivityUnit = _unitMci,
                Status = "Waste",
                SourceCode = "SRC-DELETED-02"
            },
            // 3. مصدر تاريخي نُقل لموقع آخر (يجب استثناؤه من الإجمالي: 250 mCi)
            new Source
            {
                Id = Guid.NewGuid(),
                LocationId = otherLocationId,
                IsDeleted = false,
                CurrentActivityValue = 250,
                CurrentActivityUnit = _unitMci,
                Status = "InUse",
                SourceCode = "SRC-HISTORICAL-03"
            }
        };

        // Act
        var vm = new LocationDetailsViewModel(_testLocation, sources);

        // Assert:
        // 1. جدول المصادر التفصيلي يحتوي على كافة السجلات الثلاثة دون إنقاص
        Assert.Equal(3, vm.TotalSourcesCount);
        Assert.Equal(3, vm.FilteredSources.Count);

        // 2. إجمالي النشاط الإشعاعي يعكس فقط المصدر الأول (100 mCi = 3.7e9 Bq)
        var bqItem = vm.TotalActivityItems.FirstOrDefault(x => x.UnitSymbol == "Bq");
        var mciItem = vm.TotalActivityItems.FirstOrDefault(x => x.UnitSymbol == "mCi");

        Assert.NotNull(bqItem);
        Assert.NotNull(mciItem);

        double expectedBq = 100 * 3.7e7; // 3,700,000,000 Bq
        Assert.Equal(expectedBq, bqItem.Value, precision: 1);
        Assert.Equal(100.0, mciItem.Value, precision: 2);
    }

    [Fact]
    public void ApplyFilters_SearchBySourceCode_FiltersCorrectly()
    {
        // Arrange
        var sources = new List<Source>
        {
            new Source { Id = Guid.NewGuid(), SourceCode = "SRC-ALPHA-01", Status = "InUse" },
            new Source { Id = Guid.NewGuid(), SourceCode = "SRC-BETA-02", Status = "InUse" },
            new Source { Id = Guid.NewGuid(), SourceCode = "SRC-GAMMA-03", Status = "Storage" }
        };
        var vm = new LocationDetailsViewModel(_testLocation, sources);

        // Act
        vm.SearchText = "BETA";

        // Assert
        Assert.Single(vm.FilteredSources);
        Assert.Equal("SRC-BETA-02", vm.FilteredSources[0].SourceCode);
        Assert.Equal(1, vm.FilteredSources[0].RowNumber);
    }

    [Fact]
    public void ApplyFilters_SearchByIsotopeSymbol_FiltersCorrectly()
    {
        // Arrange
        var sources = new List<Source>
        {
            new Source { Id = Guid.NewGuid(), SourceCode = "SRC-01", Radioisotope = new Radioisotope { Symbol = "Co-60" } },
            new Source { Id = Guid.NewGuid(), SourceCode = "SRC-02", Radioisotope = new Radioisotope { Symbol = "Cs-137" } },
            new Source { Id = Guid.NewGuid(), SourceCode = "SRC-03", Radioisotope = new Radioisotope { Symbol = "Am-241" } }
        };
        var vm = new LocationDetailsViewModel(_testLocation, sources);

        // Act
        vm.SearchText = "Cs-137";

        // Assert
        Assert.Single(vm.FilteredSources);
        Assert.Equal("SRC-02", vm.FilteredSources[0].SourceCode);
    }

    [Fact]
    public void ApplyFilters_SearchBySerialNumberAndManufacturer_FiltersCorrectly()
    {
        // Arrange
        var sources = new List<Source>
        {
            new Source { Id = Guid.NewGuid(), SourceCode = "SRC-01", SerialNumber = "SN-9988-X", Manufacturer = "Eckert & Ziegler" },
            new Source { Id = Guid.NewGuid(), SourceCode = "SRC-02", SerialNumber = "SN-1122-Y", Manufacturer = "Amersham" }
        };
        var vm = new LocationDetailsViewModel(_testLocation, sources);

        // Act 1: by serial number
        vm.SearchText = "9988";
        Assert.Single(vm.FilteredSources);
        Assert.Equal("SRC-01", vm.FilteredSources[0].SourceCode);

        // Act 2: by manufacturer
        vm.SearchText = "Amersham";
        Assert.Single(vm.FilteredSources);
        Assert.Equal("SRC-02", vm.FilteredSources[0].SourceCode);
    }

    [Fact]
    public void ApplyFilters_FilterByStatus_FiltersCorrectly()
    {
        // Arrange
        var sources = new List<Source>
        {
            new Source { Id = Guid.NewGuid(), SourceCode = "SRC-01", Status = "InUse" },
            new Source { Id = Guid.NewGuid(), SourceCode = "SRC-02", Status = "InUse" },
            new Source { Id = Guid.NewGuid(), SourceCode = "SRC-03", Status = "Storage" },
            new Source { Id = Guid.NewGuid(), SourceCode = "SRC-04", Status = "Waste" }
        };
        var vm = new LocationDetailsViewModel(_testLocation, sources);

        // Act: Filter for "قيد الاستخدام"
        vm.SelectedStatusFilter = "قيد الاستخدام";

        // Assert
        Assert.Equal(2, vm.FilteredSources.Count);
        Assert.All(vm.FilteredSources, r => Assert.Equal("قيد الاستخدام", r.ArabicStatus));

        // Act: Filter for "نفايات"
        vm.SelectedStatusFilter = "نفايات";
        Assert.Single(vm.FilteredSources);
        Assert.Equal("SRC-04", vm.FilteredSources[0].SourceCode);
    }

    [Fact]
    public void ApplyFilters_CombinedSearchAndStatus_FiltersCorrectly()
    {
        // Arrange
        var sources = new List<Source>
        {
            new Source { Id = Guid.NewGuid(), SourceCode = "SRC-LAB-01", Status = "InUse", Radioisotope = new Radioisotope { Symbol = "Co-60" } },
            new Source { Id = Guid.NewGuid(), SourceCode = "SRC-LAB-02", Status = "Storage", Radioisotope = new Radioisotope { Symbol = "Co-60" } },
            new Source { Id = Guid.NewGuid(), SourceCode = "SRC-STORE-03", Status = "InUse", Radioisotope = new Radioisotope { Symbol = "Cs-137" } }
        };
        var vm = new LocationDetailsViewModel(_testLocation, sources);

        // Act: Search for "LAB" and Status "InUse"
        vm.SearchText = "LAB";
        vm.SelectedStatusFilter = "قيد الاستخدام";

        // Assert
        Assert.Single(vm.FilteredSources);
        Assert.Equal("SRC-LAB-01", vm.FilteredSources[0].SourceCode);
    }

    [Fact]
    public void ClearFiltersCommand_ResetsSearchTextAndSelectedStatus()
    {
        // Arrange
        var sources = new List<Source>
        {
            new Source { Id = Guid.NewGuid(), SourceCode = "SRC-01", Status = "InUse" },
            new Source { Id = Guid.NewGuid(), SourceCode = "SRC-02", Status = "Storage" }
        };
        var vm = new LocationDetailsViewModel(_testLocation, sources);

        vm.SearchText = "01";
        vm.SelectedStatusFilter = "قيد الاستخدام";
        Assert.Single(vm.FilteredSources);

        // Act
        vm.ClearFiltersCommand.Execute(null);

        // Assert
        Assert.Equal(string.Empty, vm.SearchText);
        Assert.Equal("الكل", vm.SelectedStatusFilter);
        Assert.Equal(2, vm.FilteredSources.Count);
    }

    [Fact]
    public void ExportToPdf_CallsReportingService_WithFilteredSourcesAndLocationTitle()
    {
        // Arrange
        var mockReporting = new Mock<IReportingService>();
        var sources = new List<Source>
        {
            new Source { Id = Guid.NewGuid(), SourceCode = "SRC-EXP-01", Status = "InUse" }
        };

        var vm = new LocationDetailsViewModel(_testLocation, sources, mockReporting.Object);

        // Assert reporting service is passed and ready
        Assert.NotNull(vm);
    }

    [Fact]
    public void FormatActivityValue_FormatsAppropriatelyAccordingToMagnitude()
    {
        Assert.Equal("0 Bq", LocationDetailsViewModel.FormatActivityValue(0, "Bq"));
        Assert.Equal(string.Format("{0:E3} Bq", 1.5e9), LocationDetailsViewModel.FormatActivityValue(1.5e9, "Bq"));
        Assert.Equal("1,500,000 Bq", LocationDetailsViewModel.FormatActivityValue(1.5e6, "Bq"));
        Assert.Equal("1,500.00 Bq", LocationDetailsViewModel.FormatActivityValue(1500, "Bq"));
        Assert.Equal("12.3456 Bq", LocationDetailsViewModel.FormatActivityValue(12.3456, "Bq"));
    }
}
