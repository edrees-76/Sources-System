using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Sources.Data;
using Sources.Helpers;
using Sources.Models;
using Sources.Services;
using Sources.Tests.Fixtures;
using Sources.ViewModels;
using Xunit;

namespace Sources.Tests;

public class NeutronSourcesUITests : IDisposable
{
    private readonly SqliteInMemoryFixture _fixture;
    private readonly IServiceProvider _sp;
    private readonly Guid _neutronTypeId = Guid.NewGuid();
    private readonly Guid _locationId = Guid.NewGuid();

    public NeutronSourcesUITests()
    {
        _fixture = new SqliteInMemoryFixture();

        // Seed basic reference data
        using (var db = _fixture.CreateContext())
        {
            db.NeutronSourceTypes.Add(new NeutronSourceType
            {
                Id = _neutronTypeId,
                Code = "Am-241/Be",
                NameAr = "أمريسيوم-بريليوم",
                NameEn = "Americium-Beryllium",
                ReactionType = "(α,n)",
                HalfLife = 432.2,
                HalfLifeUnit = "years",
                AverageNeutronEnergyMeV = 4.2,
                TypicalNeutronYield = 2.2e6
            });
            db.Locations.Add(new Location
            {
                Id = _locationId,
                LocationName = "Neutron Lab 101",
                Building = "Main",
                Room = "101"
            });
            db.SaveChanges();
        }

        var services = new ServiceCollection();
        services.AddSingleton<IDbContextFactory<AppDbContext>>(_fixture.ContextFactory);
        _sp = services.BuildServiceProvider();
        typeof(App).GetProperty("ServiceProvider", BindingFlags.Public | BindingFlags.Static)?.SetValue(null, _sp);
    }

    public void Dispose()
    {
        _fixture.Dispose();
    }

    [Fact]
    public async Task SourcesViewModel_SwitchToNeutronTab_LoadsNeutronSources()
    {
        // Arrange
        var mockSourceService = new Mock<ISourceService>();
        var mockIsotopeService = new Mock<IRadioisotopeService>();
        var mockLocationService = new Mock<ILocationService>();
        var mockReportingService = new Mock<IReportingService>();
        var mockNeutronService = new Mock<INeutronSourceService>();
        var mockNeutronTypeService = new Mock<INeutronSourceTypeService>();

        mockNeutronService.Setup(s => s.GetAll()).Returns(new List<NeutronSource>
        {
            new NeutronSource
            {
                Id = Guid.NewGuid(),
                SourceCode = "NS-001",
                NeutronSourceTypeId = _neutronTypeId,
                NeutronSourceType = new NeutronSourceType { Code = "Am-241/Be" },
                EmissionRate = 2200000,
                Status = "Storage"
            }
        });

        mockNeutronTypeService.Setup(s => s.GetAll()).Returns(new List<NeutronSourceType>
        {
            new NeutronSourceType { Id = _neutronTypeId, Code = "Am-241/Be" }
        });

        var vm = new SourcesViewModel(
            mockSourceService.Object,
            mockIsotopeService.Object,
            mockLocationService.Object,
            mockReportingService.Object,
            neutronSourceService: mockNeutronService.Object,
            neutronSourceTypeService: mockNeutronTypeService.Object);

        // Act
        vm.SelectedTab = "Neutron";
        await vm.LoadNeutronDataAsync();

        // Assert
        Assert.True(vm.IsNeutronSourcesView);
        Assert.True(vm.HasNeutronSources);
        Assert.Equal(1, vm.NeutronSourcesCount);
        Assert.Single(vm.PagedNeutronSources);
        Assert.Equal("NS-001", vm.PagedNeutronSources[0].SourceCode);
    }

    [Fact]
    public async Task SourcesViewModel_AddNewNeutron_AndSave_CallsCreate()
    {
        // Arrange
        var mockSourceService = new Mock<ISourceService>();
        var mockIsotopeService = new Mock<IRadioisotopeService>();
        var mockLocationService = new Mock<ILocationService>();
        var mockReportingService = new Mock<IReportingService>();
        var mockNeutronService = new Mock<INeutronSourceService>();
        var mockNeutronTypeService = new Mock<INeutronSourceTypeService>();

        mockNeutronService.Setup(s => s.Create(It.IsAny<NeutronSource>()))
            .Returns((true, "تمت إضافة المصدر النيتروني بنجاح"));

        var vm = new SourcesViewModel(
            mockSourceService.Object,
            mockIsotopeService.Object,
            mockLocationService.Object,
            mockReportingService.Object,
            neutronSourceService: mockNeutronService.Object,
            neutronSourceTypeService: mockNeutronTypeService.Object);

        // Act
        vm.AddNewNeutron();

        Assert.True(vm.IsEditing);
        Assert.True(vm.IsNew);
        Assert.True(vm.IsNeutronForm);

        vm.EditSourceCode = "NS-TEST-1";
        vm.EditNeutronTypeId = _neutronTypeId;
        vm.EditEmissionRate = 5000000;
        vm.EditRelativeUncertaintyPercent = 2.5;
        vm.EditLocationId = _locationId;
        vm.EditCalibrationDate = DateTime.Today;
        vm.EditStatus = "Storage";

        await vm.SaveCommand.ExecuteAsync(null);

        // Assert
        mockNeutronService.Verify(s => s.Create(It.Is<NeutronSource>(n =>
            n.SourceCode == "NS-TEST-1" &&
            n.NeutronSourceTypeId == _neutronTypeId &&
            n.EmissionRate == 5000000 &&
            n.RelativeExpandedUncertaintyPercent == 2.5)), Times.Once);
        Assert.False(vm.IsEditing);
    }

    [Fact]
    public async Task SourcesViewModel_EditNeutronSource_AndSave_CallsUpdate()
    {
        // Arrange
        var mockSourceService = new Mock<ISourceService>();
        var mockIsotopeService = new Mock<IRadioisotopeService>();
        var mockLocationService = new Mock<ILocationService>();
        var mockReportingService = new Mock<IReportingService>();
        var mockNeutronService = new Mock<INeutronSourceService>();
        var mockNeutronTypeService = new Mock<INeutronSourceTypeService>();

        var existingId = Guid.NewGuid();
        var existing = new NeutronSource
        {
            Id = existingId,
            SourceCode = "NS-EDIT-1",
            NeutronSourceTypeId = _neutronTypeId,
            EmissionRate = 3000000,
            RelativeExpandedUncertaintyPercent = 4.0,
            LocationId = _locationId,
            CalibrationDate = DateTime.Today,
            Status = "Storage"
        };

        mockNeutronService.Setup(s => s.Update(It.IsAny<NeutronSource>()))
            .Returns((true, "تم التعديل بنجاح"));

        var vm = new SourcesViewModel(
            mockSourceService.Object,
            mockIsotopeService.Object,
            mockLocationService.Object,
            mockReportingService.Object,
            neutronSourceService: mockNeutronService.Object,
            neutronSourceTypeService: mockNeutronTypeService.Object);

        // Act
        vm.EditNeutronSource(existing);

        Assert.True(vm.IsEditing);
        Assert.False(vm.IsNew);
        Assert.True(vm.IsNeutronForm);
        Assert.Equal("NS-EDIT-1", vm.EditSourceCode);
        Assert.Equal(3000000, vm.EditEmissionRate);

        vm.EditEmissionRate = 4500000;
        await vm.SaveCommand.ExecuteAsync(null);

        // Assert
        mockNeutronService.Verify(s => s.Update(It.Is<NeutronSource>(n =>
            n.Id == existingId &&
            n.EmissionRate == 4500000)), Times.Once);
        Assert.False(vm.IsEditing);
    }

    [Fact]
    public void NeutronSourceTypesViewModel_CrudOperations()
    {
        // Arrange
        var mockService = new Mock<INeutronSourceTypeService>();
        var typeList = new List<NeutronSourceType>
        {
            new NeutronSourceType { Id = _neutronTypeId, Code = "Am-241/Be", NameAr = "أمريسيوم", NameEn = "AmBe", HalfLife = 432.2 }
        };

        mockService.Setup(s => s.GetAll()).Returns(typeList);
        mockService.Setup(s => s.Create(It.IsAny<NeutronSourceType>()))
            .Returns((true, "تمت الإضافة"));
        mockService.Setup(s => s.Update(It.IsAny<NeutronSourceType>()))
            .Returns((true, "تم التعديل"));

        var vm = new NeutronSourceTypesViewModel(mockService.Object);

        // Assert initial load
        Assert.Single(vm.Types);
        Assert.Equal("Am-241/Be", vm.Types[0].Code);

        // Act - Add new
        vm.AddNew();
        Assert.True(vm.IsEditing);
        Assert.True(vm.IsNew);

        vm.EditCode = "Cf-252";
        vm.EditNameAr = "كاليفورنيوم-252";
        vm.EditNameEn = "Californium-252";
        vm.EditHalfLife = 2.645;
        vm.EditHalfLifeUnit = "years";
        vm.Save();

        mockService.Verify(s => s.Create(It.Is<NeutronSourceType>(t => t.Code == "Cf-252" && t.HalfLife == 2.645)), Times.Once);
        Assert.False(vm.IsEditing);
    }

    [Fact]
    public async Task DeletionsViewModel_IncludesAndRestores_NeutronSources()
    {
        // Arrange - Seed soft-deleted neutron source
        var deletedId = Guid.NewGuid();
        using (var db = _fixture.CreateContext())
        {
            db.NeutronSources.Add(new NeutronSource
            {
                Id = deletedId,
                SourceCode = "NS-DELETED-1",
                NeutronSourceTypeId = _neutronTypeId,
                EmissionRate = 1000000,
                IsDeleted = true,
                DeletedAt = DateTime.Now.AddDays(-1)
            });
            db.SaveChanges();
        }

        var mockSourceService = new Mock<ISourceService>();
        var mockIsotopeService = new Mock<IRadioisotopeService>();
        var mockLocationService = new Mock<ILocationService>();
        var mockUserService = new Mock<IUserService>();
        var mockNeutronService = new Mock<INeutronSourceService>();

        mockNeutronService.Setup(s => s.Restore(deletedId))
            .Returns((true, "تم الاسترجاع بنجاح"));

        var vm = new DeletionsViewModel(
            _fixture.ContextFactory,
            sourceService: mockSourceService.Object,
            locationService: mockLocationService.Object,
            userService: mockUserService.Object,
            radioisotopeService: mockIsotopeService.Object,
            neutronSourceService: mockNeutronService.Object);

        // Act - Load items
        await vm.LoadDeletedItemsAsync();

        // Assert
        Assert.Equal(1, vm.NeutronSourcesCount);
        var item = vm.AllItems.FirstOrDefault(i => i.Id == deletedId);
        Assert.NotNull(item);
        Assert.Equal("NeutronSource", item.EntityType);
        Assert.Equal("NS-DELETED-1", item.Identifier);

        // Act - Restore item
        await vm.RestoreItem(item);

        // Assert
        mockNeutronService.Verify(s => s.Restore(deletedId), Times.Once);
    }

    [Fact]
    public async Task ReportsViewModel_NeutronInventory_LoadsDataAndExports()
    {
        // Arrange - Seed neutron source
        using (var db = _fixture.CreateContext())
        {
            db.NeutronSources.Add(new NeutronSource
            {
                Id = Guid.NewGuid(),
                SourceCode = "NS-REP-01",
                NeutronSourceTypeId = _neutronTypeId,
                EmissionRate = 2400000,
                RelativeExpandedUncertaintyPercent = 3.0,
                Status = "Storage",
                IsDeleted = false
            });
            db.SaveChanges();
        }

        var mockSourceService = new Mock<ISourceService>();
        var mockBorrowService = new Mock<IBorrowService>();
        var mockReportingService = new Mock<IReportingService>();
        var mockSettingsService = new Mock<ISystemSettingsService>();
        var mockNeutronService = new Mock<INeutronSourceService>();

        mockNeutronService.Setup(s => s.GetAll()).Returns(new List<NeutronSource>
        {
            new NeutronSource
            {
                Id = Guid.NewGuid(),
                SourceCode = "NS-REP-01",
                NeutronSourceTypeId = _neutronTypeId,
                NeutronSourceType = new NeutronSourceType { Code = "Am-241/Be", NameAr = "أمريسيوم" },
                EmissionRate = 2400000,
                RelativeExpandedUncertaintyPercent = 3.0,
                Status = "Storage"
            }
        });

        var vm = new ReportsViewModel(
            mockSourceService.Object,
            mockBorrowService.Object,
            mockReportingService.Object,
            mockSettingsService.Object,
            _fixture.ContextFactory,
            mockNeutronService.Object);

        // Act - Select neutron inventory report
        vm.SelectedReport = "NeutronInventoryReport";
        await Task.Delay(100);

        // Assert
        Assert.Single(vm.NeutronInventoryData);
        Assert.Equal("NS-REP-01", vm.NeutronInventoryData[0].SourceCode);
        Assert.Equal("Am-241/Be", vm.NeutronInventoryData[0].TypeCode);
    }

    [Fact]
    public async Task ReportingService_NeutronInventory_GeneratesPdfAndExcel()
    {
        // Arrange
        var service = new ReportingService();

        var list = new List<NeutronSource>
        {
            new NeutronSource
            {
                Id = Guid.NewGuid(),
                SourceCode = "NS-EXP-01",
                NeutronSourceTypeId = _neutronTypeId,
                NeutronSourceType = new NeutronSourceType { Code = "Am-241/Be", NameAr = "أمريسيوم" },
                EmissionRate = 2500000,
                RelativeExpandedUncertaintyPercent = 2.8,
                Status = "Storage",
                CalibrationDate = DateTime.Today
            }
        };

        var tempPdf = Path.Combine(Path.GetTempPath(), $"neutron_inv_test_{Guid.NewGuid():N}.pdf");
        var tempExcel = Path.Combine(Path.GetTempPath(), $"neutron_inv_test_{Guid.NewGuid():N}.xlsx");

        try
        {
            // Act
            await service.GenerateNeutronInventoryReportPdfAsync(list, tempPdf);
            await service.GenerateNeutronInventoryReportExcelAsync(list, tempExcel);

            // Assert
            Assert.True(File.Exists(tempPdf));
            Assert.True(new FileInfo(tempPdf).Length > 0);

            Assert.True(File.Exists(tempExcel));
            Assert.True(new FileInfo(tempExcel).Length > 0);
        }
        finally
        {
            if (File.Exists(tempPdf)) File.Delete(tempPdf);
            if (File.Exists(tempExcel)) File.Delete(tempExcel);
        }
    }

    [Fact]
    public void NeutronSourceDetailsViewModel_AddedBy_ResolvesUserNameFromUserService()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var mockUserService = new Mock<IUserService>();
        mockUserService.Setup(u => u.GetUserById(userId)).Returns(new User
        {
            Id = userId,
            Username = "dr_radiation",
            FullName = "د. أحمد الإشعاعي"
        });

        var neutron = new NeutronSource
        {
            Id = Guid.NewGuid(),
            SourceCode = "NS-USR-01",
            EmissionRate = 123456,
            AddedBy = userId,
            CreatedAt = DateTime.Now
        };

        // Act
        var vm = new NeutronSourceDetailsViewModel(neutron, mockUserService.Object);

        // Assert
        Assert.Equal("د. أحمد الإشعاعي", vm.AddedBy);
        Assert.DoesNotContain(userId.ToString(), vm.AddedBy);
    }

    [Fact]
    public void NeutronSourceTypesViewModel_OnEditHalfLifeTextChanged_InvalidInputResetsToZero()
    {
        // Arrange
        var mockService = new Mock<INeutronSourceTypeService>();
        mockService.Setup(s => s.GetAll()).Returns(new List<NeutronSourceType>());
        var vm = new NeutronSourceTypesViewModel(mockService.Object);

        // Act 1: valid number
        vm.EditHalfLifeText = "432.2";
        Assert.Equal(432.2, vm.EditHalfLife);

        // Act 2: invalid string input
        vm.EditHalfLifeText = "invalid-text";
        Assert.Equal(0, vm.EditHalfLife);
    }

    [Fact]
    public void LocationDetailsViewModel_DisplaysAndFilters_NeutronSources()
    {
        // Arrange
        var loc = new Location { Id = _locationId, LocationName = "Neutron Vault" };
        var neutronList = new List<NeutronSource>
        {
            new NeutronSource
            {
                Id = Guid.NewGuid(),
                SourceCode = "NS-LOC-01",
                LocationId = _locationId,
                EmissionRate = 2000000,
                Status = "Storage",
                NeutronSourceType = new NeutronSourceType { Code = "Cf-252", NameAr = "كاليفورنيوم" }
            },
            new NeutronSource
            {
                Id = Guid.NewGuid(),
                SourceCode = "NS-LOC-02",
                LocationId = _locationId,
                EmissionRate = 3500000,
                Status = "InUse",
                NeutronSourceType = new NeutronSourceType { Code = "Am-241/Be", NameAr = "أمريسيوم" }
            }
        };

        var mockNeutronService = new Mock<INeutronSourceService>();
        mockNeutronService.Setup(s => s.GetByLocation(_locationId)).Returns(neutronList);

        // Act
        var vm = new LocationDetailsViewModel(
            location: loc,
            sources: new List<Source>(),
            reportingService: null,
            neutronSources: neutronList,
            neutronSourceService: mockNeutronService.Object);

        // Assert initial load
        Assert.True(vm.HasNeutronSources);
        Assert.Equal(2, vm.TotalNeutronSourcesCount);
        Assert.Equal(2, vm.FilteredNeutronSourcesCount);

        // Act - Filter by Status
        vm.SelectedStatusFilter = "في المخزن";
        // Assert
        Assert.Single(vm.FilteredNeutronSources);
        Assert.Equal("NS-LOC-01", vm.FilteredNeutronSources[0].SourceCode);

        // Act - Search by Code
        vm.SelectedStatusFilter = "الكل";
        vm.SearchText = "Am-241";
        // Assert
        Assert.Single(vm.FilteredNeutronSources);
        Assert.Equal("NS-LOC-02", vm.FilteredNeutronSources[0].SourceCode);
    }

    [Fact]
    public void Localization_EnglishAndArabic_ContainAllNeutronKeys()
    {
        // Arrange - Load resource dictionary files directly from disk
        var baseDir = AppContext.BaseDirectory;
        // Search for Sources-System-Project/Resources/Strings.ar.xaml and Strings.en.xaml
        var arPath = Path.GetFullPath(Path.Combine(baseDir, @"..\..\..\..\Sources-System-Project\Resources\Strings.ar.xaml"));
        var enPath = Path.GetFullPath(Path.Combine(baseDir, @"..\..\..\..\Sources-System-Project\Resources\Strings.en.xaml"));

        Assert.True(File.Exists(arPath), $"Arabic strings file not found at {arPath}");
        Assert.True(File.Exists(enPath), $"English strings file not found at {enPath}");

        var arContent = File.ReadAllText(arPath);
        var enContent = File.ReadAllText(enPath);

        var requiredKeys = new[]
        {
            "TabNeutronSources",
            "BtnAddNeutronSource",
            "BtnManageNeutronTypes",
            "HintSearchNeutronSource",
            "HeaderRefType",
            "HeaderEmissionRate",
            "HeaderUncertaintyPercent",
            "MsgNoNeutronSourcesRecorded",
            "MsgNoNeutronSourcesHint",
            "MsgNoSearchNeutronSource",
            "MsgConfirmDeleteNeutronSource",
            "MsgNeutronSourceNotFound",
            "TitleNeutronSourceDetails",
            "LabelSourceCategory",
            "RadioCategoryStandardSource",
            "RadioCategoryNeutronSource",
            "CardNeutronEmissionProps",
            "FieldNeutronRefTypeReq",
            "FieldEmissionRateReq",
            "FieldUncertaintyPercent",
            "DetailCardNeutronIdentity",
            "DetailLabelSourceCode",
            "DetailLabelSerialNumber",
            "DetailLabelNeutronRefType",
            "DetailLabelReactionType",
            "DetailCardNeutronEmissionProps",
            "DetailLabelEmissionRate",
            "DetailLabelUncertainty",
            "DetailCardLocationNotes",
            "TitleNeutronSourceTypes",
            "BtnAddNeutronType",
            "BtnNeutronInventoryReport",
            "HeaderLocationNeutronSources"
        };

        foreach (var key in requiredKeys)
        {
            Assert.Contains($"x:Key=\"{key}\"", arContent);
            Assert.Contains($"x:Key=\"{key}\"", enContent);
        }
    }

    [Fact]
    public void LocationDetailsViewModel_FilterByMokhzan_ReturnsStorageNeutronSources()
    {
        // Arrange
        var loc = new Location { Id = _locationId, LocationName = "Vault A" };
        var neutronList = new List<NeutronSource>
        {
            new NeutronSource
            {
                Id = Guid.NewGuid(),
                SourceCode = "NS-STOR-01",
                LocationId = _locationId,
                EmissionRate = 2000000,
                Status = "Storage",
                NeutronSourceType = new NeutronSourceType { Code = "Cf-252", NameAr = "كاليفورنيوم" }
            },
            new NeutronSource
            {
                Id = Guid.NewGuid(),
                SourceCode = "NS-USE-01",
                LocationId = _locationId,
                EmissionRate = 3500000,
                Status = "InUse",
                NeutronSourceType = new NeutronSourceType { Code = "Am-241/Be", NameAr = "أمريسيوم" }
            }
        };

        var mockNeutronService = new Mock<INeutronSourceService>();
        mockNeutronService.Setup(s => s.GetByLocation(_locationId)).Returns(neutronList);

        var vm = new LocationDetailsViewModel(
            location: loc,
            sources: new List<Source>(),
            reportingService: null,
            neutronSources: neutronList,
            neutronSourceService: mockNeutronService.Object);

        // Act 1 - Filter with "مخزن" (the exact item from StatusFilterOptions)
        vm.SelectedStatusFilter = "مخزن";

        // Assert 1
        Assert.Single(vm.FilteredNeutronSources);
        Assert.Equal("NS-STOR-01", vm.FilteredNeutronSources[0].SourceCode);
        Assert.Equal("مخزن", vm.FilteredNeutronSources[0].ArabicStatus);

        // Act 2 - Filter with "في المخزن"
        vm.SelectedStatusFilter = "في المخزن";
        Assert.Single(vm.FilteredNeutronSources);
        Assert.Equal("NS-STOR-01", vm.FilteredNeutronSources[0].SourceCode);

        // Act 3 - Filter with "Storage"
        vm.SelectedStatusFilter = "Storage";
        Assert.Single(vm.FilteredNeutronSources);
        Assert.Equal("NS-STOR-01", vm.FilteredNeutronSources[0].SourceCode);
    }

    [Fact]
    public void NeutronSourceTypesViewModel_OnEditNeutronYieldTextChanged_InvalidInputResetsToNull()
    {
        // Arrange
        var mockService = new Mock<INeutronSourceTypeService>();
        mockService.Setup(s => s.GetAll()).Returns(new List<NeutronSourceType>());
        var vm = new NeutronSourceTypesViewModel(mockService.Object);

        // Act 1: valid number (scientific notation)
        vm.EditNeutronYieldText = "2.2e6";
        Assert.Equal(2200000, vm.EditNeutronYield);

        // Act 2: invalid string input
        vm.EditNeutronYieldText = "invalid-yield-value";
        Assert.Null(vm.EditNeutronYield);

        // Act 3: edit existing type and then set invalid input
        var existing = new NeutronSourceType
        {
            Id = Guid.NewGuid(),
            Code = "Am-241/Be",
            HalfLife = 432.2,
            TypicalNeutronYield = 2200000
        };
        vm.Edit(existing);
        Assert.Equal(2200000, vm.EditNeutronYield);

        vm.EditNeutronYieldText = "not-a-number";
        Assert.Null(vm.EditNeutronYield);
    }

    [Theory]
    [InlineData("1.1E7", 11000000.0)]
    [InlineData("1.1e+7", 11000000.0)]
    [InlineData("1.1E-3", 0.0011)]
    [InlineData("2.2e6", 2200000.0)]
    public void ScientificNotationParser_ParsesStandardScientificNotation(string input, double expected)
    {
        bool success = ScientificNotationParser.TryParse(input, out double result);
        Assert.True(success);
        Assert.Equal(expected, result, 6);
    }

    [Theory]
    [InlineData("1.1x10^7", 11000000.0)]
    [InlineData("1.1X10^7", 11000000.0)]
    [InlineData("1.1*10^7", 11000000.0)]
    [InlineData("1.1*10^-3", 0.0011)]
    [InlineData("2.2 x 10^6", 2200000.0)]
    public void ScientificNotationParser_ParsesMultiplicationFormat(string input, double expected)
    {
        bool success = ScientificNotationParser.TryParse(input, out double result);
        Assert.True(success);
        Assert.Equal(expected, result, 6);
    }

    [Theory]
    [InlineData("1.1×10^7", 11000000.0)]
    [InlineData("1.1×10⁷", 11000000.0)]
    [InlineData("1.1 × 10⁷", 11000000.0)]
    [InlineData("10^7", 10000000.0)]
    [InlineData("10⁷", 10000000.0)]
    [InlineData("10⁻³", 0.001)]
    public void ScientificNotationParser_ParsesMultiplicationSymbolAndSuperscripts(string input, double expected)
    {
        bool success = ScientificNotationParser.TryParse(input, out double result);
        Assert.True(success);
        Assert.Equal(expected, result, 6);
    }

    [Theory]
    [InlineData("11000000", 11000000.0)]
    [InlineData("11,000,000", 11000000.0)]
    [InlineData("1,234.56", 1234.56)]
    public void ScientificNotationParser_ParsesStandardDecimalAndThousandsCommas(string input, double expected)
    {
        bool success = ScientificNotationParser.TryParse(input, out double result);
        Assert.True(success);
        Assert.Equal(expected, result, 2);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("invalid-text")]
    [InlineData("1.1xx10")]
    [InlineData("10^^7")]
    public void ScientificNotationParser_RejectsInvalidStrings(string input)
    {
        bool success = ScientificNotationParser.TryParse(input, out double result);
        Assert.False(success);
    }

    [Fact]
    public void ScientificNotationParser_TryParsePositive_RejectsZeroAndNegativeAndReturnsErrorMessage()
    {
        // Act & Assert 1: Zero
        bool successZero = ScientificNotationParser.TryParsePositive("0", out double resZero, out string? errZero);
        Assert.False(successZero);
        Assert.NotNull(errZero);

        // Act & Assert 2: Negative
        bool successNeg = ScientificNotationParser.TryParsePositive("-1.1E7", out double resNeg, out string? errNeg);
        Assert.False(successNeg);
        Assert.NotNull(errNeg);

        // Act & Assert 3: Invalid text
        bool successInv = ScientificNotationParser.TryParsePositive("abc", out double resInv, out string? errInv);
        Assert.False(successInv);
        Assert.False(string.IsNullOrWhiteSpace(errInv));

        // Act & Assert 4: Valid
        bool successVal = ScientificNotationParser.TryParsePositive("1.1E7", out double resVal, out string? errVal);
        Assert.True(successVal);
        Assert.Null(errVal);
        Assert.Equal(11000000.0, resVal);
    }

    [Fact]
    public void SourcesViewModel_EmissionRate_AcceptsScientificAndMultiplicationNotations()
    {
        // Arrange
        var mockSourceService = new Mock<ISourceService>();
        mockSourceService.Setup(s => s.GetAllSources()).Returns(new List<Source>());
        mockSourceService.Setup(s => s.GetDeletedSources()).Returns(new List<Source>());
        var mockIsotopeService = new Mock<IRadioisotopeService>();
        mockIsotopeService.Setup(s => s.GetAll()).Returns(new List<Radioisotope>());
        var mockLocationService = new Mock<ILocationService>();
        mockLocationService.Setup(s => s.GetAll()).Returns(new List<Location>());
        var mockNeutronTypeService = new Mock<INeutronSourceTypeService>();
        mockNeutronTypeService.Setup(s => s.GetAll()).Returns(new List<NeutronSourceType>());
        var mockNeutronService = new Mock<INeutronSourceService>();
        mockNeutronService.Setup(s => s.GetAll()).Returns(new List<NeutronSource>());

        var vm = new SourcesViewModel(
            sourceService: mockSourceService.Object,
            isotopeService: mockIsotopeService.Object,
            locationService: mockLocationService.Object,
            reportingService: null!,
            decayService: null,
            neutronSourceService: mockNeutronService.Object,
            neutronSourceTypeService: mockNeutronTypeService.Object);

        // Act 1: Standard scientific notation
        vm.EditEmissionRateText = "1.1E7";
        Assert.Equal(11000000.0, vm.EditEmissionRate);

        // Act 2: Multiplication notation
        vm.EditEmissionRateText = "1.1x10^7";
        Assert.Equal(11000000.0, vm.EditEmissionRate);

        // Act 3: Symbol multiplication with superscript
        vm.EditEmissionRateText = "1.1 × 10⁷";
        Assert.Equal(11000000.0, vm.EditEmissionRate);

        // Act 4: Invalid input sets to 0
        vm.EditEmissionRateText = "invalid-rate";
        Assert.Equal(0, vm.EditEmissionRate);
    }

    [Fact]
    public async Task SourcesViewModel_InvalidEmissionRate_ShowsWarningAndPreventsSave()
    {
        // Arrange
        var mockSourceService = new Mock<ISourceService>();
        var mockIsotopeService = new Mock<IRadioisotopeService>();
        var mockLocationService = new Mock<ILocationService>();
        var mockReportingService = new Mock<IReportingService>();
        var mockNeutronService = new Mock<INeutronSourceService>();
        var mockNeutronTypeService = new Mock<INeutronSourceTypeService>();

        var vm = new SourcesViewModel(
            mockSourceService.Object,
            mockIsotopeService.Object,
            mockLocationService.Object,
            mockReportingService.Object,
            neutronSourceService: mockNeutronService.Object,
            neutronSourceTypeService: mockNeutronTypeService.Object);

        // Act
        vm.AddNewNeutron();
        vm.EditSourceCode = "NS-ERR-TEST";
        vm.EditNeutronTypeId = _neutronTypeId;
        vm.EditEmissionRateText = "invalid_text_abc";
        vm.EditCalibrationDate = DateTime.Today;
        vm.EditLocationId = _locationId;
        vm.EditStatus = "Storage";

        await vm.SaveCommand.ExecuteAsync(null);

        // Assert: Save is prevented, form remains open, message is set
        mockNeutronService.Verify(s => s.Create(It.IsAny<NeutronSource>()), Times.Never);
        Assert.True(vm.IsEditing);
        Assert.True(vm.HasMessage);
        Assert.False(string.IsNullOrWhiteSpace(vm.Message));
    }

    [Theory]
    [InlineData(11000000, "1.1×10⁷")]
    [InlineData(2200000, "2.2×10⁶")]
    [InlineData(10000000, "1×10⁷")]
    [InlineData(3500000, "3.5×10⁶")]
    [InlineData(0.0011, "1.1×10⁻³")]
    [InlineData(500, "500")]
    [InlineData(0, "0")]
    public void ScientificNotationParser_FormatScientific_FormatsNumbersCorrectly(double value, string expected)
    {
        // Act
        string formatted = ScientificNotationParser.FormatScientific(value);

        // Assert
        Assert.Equal(expected, formatted);
    }

    [Fact]
    public void NeutronSource_DisplayEmissionRate_PreservesDoubleAndFormatsConciseString()
    {
        // Arrange
        var source = new NeutronSource
        {
            Id = Guid.NewGuid(),
            SourceCode = "NS-DISP-1",
            EmissionRate = 11000000.0
        };

        // Assert: Numeric value is untouched
        Assert.Equal(11000000.0, source.EmissionRate);

        // Assert: Formatted display uses superscripts
        Assert.Equal("1.1×10⁷ n/s", source.DisplayEmissionRate);
        Assert.Equal("1.1×10⁷ n/s", source.EmissionRateFormatted);

        // Details VM
        var detailsVm = new NeutronSourceDetailsViewModel(source);
        Assert.Equal("1.1×10⁷ n/s", detailsVm.EmissionRateFormatted);
    }

    [Theory]
    [InlineData("1.1×1000000", 1100000.0, 1.1)]
    [InlineData("5x1000", 5000.0, 5.0)]
    [InlineData("1.1x1000000", 1100000.0, 1.1)]
    [InlineData("2*500000", 1000000.0, 2.0)]
    public void ScientificNotationParser_ExplicitMultiplicationWithoutExponent_ParsesCorrectlyAndNotSilentTruncation(
        string input, double expectedValue, double faultySilentValue)
    {
        // Act
        bool success = ScientificNotationParser.TryParse(input, out double result);

        // Assert
        Assert.True(success, $"Parsing failed for: {input}");
        Assert.Equal(expectedValue, result);
        Assert.NotEqual(faultySilentValue, result);
    }

    [Theory]
    [InlineData("1.1x10^7", 11000000.0)]
    [InlineData("1.1X10^7", 11000000.0)]
    [InlineData("1.1×10^7", 11000000.0)]
    [InlineData("1.1×10⁷", 11000000.0)]
    [InlineData("10^7", 10000000.0)]
    [InlineData("10⁷", 10000000.0)]
    public void ScientificNotationParser_MandatoryExponentSymbol_ParsesCorrectly(string input, double expected)
    {
        // Act
        bool success = ScientificNotationParser.TryParse(input, out double result);

        // Assert
        Assert.True(success, $"Parsing failed for: {input}");
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("11,000x2", 22000.0)]
    [InlineData("11,000", 11000.0)]
    [InlineData("1,1x10^7", 11000000.0)]
    [InlineData("1,5", 1.5)]
    [InlineData("1,234.56", 1234.56)]
    [InlineData("11,000,000", 11000000.0)]
    public void ScientificNotationParser_ThousandsCommaAndDecimals_ParsesCorrectly(string input, double expected)
    {
        // Act
        bool success = ScientificNotationParser.TryParse(input, out double result);

        // Assert
        Assert.True(success, $"Parsing failed for input: {input}");
        Assert.Equal(expected, result, 4);
    }

    [Fact]
    public void ScientificNotationParser_ThousandsCommaWithMultiplication_AvoidsSilentTruncationBug()
    {
        // Act
        bool success = ScientificNotationParser.TryParse("11,000x2", out double result);

        // Assert
        Assert.True(success);
        Assert.Equal(22000.0, result);
        Assert.NotEqual(22.0, result); // Must not treat 11,000 as 11.000 leading to 11 * 2 = 22
    }
}


