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
}
