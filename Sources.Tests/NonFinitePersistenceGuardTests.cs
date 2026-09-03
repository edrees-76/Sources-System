using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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

public class NonFinitePersistenceGuardTests : IDisposable
{
    private readonly SqliteInMemoryFixture _fixture;
    private readonly IServiceProvider _sp;
    private readonly Mock<IAuditService> _mockAudit;
    private readonly Mock<IUserService> _mockUser;
    private readonly Mock<IDecayCalculationService> _mockDecay;
    private readonly Mock<ISystemSettingsService> _mockSettings;

    public NonFinitePersistenceGuardTests()
    {
        _fixture = new SqliteInMemoryFixture();

        var services = new ServiceCollection();
        services.AddSingleton<IDbContextFactory<AppDbContext>>(_fixture.ContextFactory);
        _sp = services.BuildServiceProvider();
        typeof(App).GetProperty("ServiceProvider", BindingFlags.Public | BindingFlags.Static)?.SetValue(null, _sp);

        _mockAudit = new Mock<IAuditService>();
        _mockUser = new Mock<IUserService>();
        _mockDecay = new Mock<IDecayCalculationService>();
        _mockSettings = new Mock<ISystemSettingsService>();
    }

    public void Dispose()
    {
        _fixture.Dispose();
    }

    #region 1. RadioisotopeService Tests

    [Fact]
    public void RadioisotopeService_Create_WithNaNFields_FailsAndDoesNotPersist()
    {
        var service = new RadioisotopeService(_fixture.ContextFactory, _mockAudit.Object, _mockUser.Object);

        // HalfLife = NaN
        var iso1 = new Radioisotope { Name = "Test1", Symbol = "T-1", HalfLife = double.NaN, Energy = 100 };
        var res1 = service.Create(iso1);
        Assert.False(res1.Success);

        // Energy = NaN
        var iso2 = new Radioisotope { Name = "Test2", Symbol = "T-2", HalfLife = 10, Energy = double.NaN };
        var res2 = service.Create(iso2);
        Assert.False(res2.Success);

        // Yield = NaN
        var iso3 = new Radioisotope { Name = "Test3", Symbol = "T-3", HalfLife = 10, Energy = 100, Yield = double.NaN };
        var res3 = service.Create(iso3);
        Assert.False(res3.Success);

        // ExemptionLimit = NaN
        var iso4 = new Radioisotope { Name = "Test4", Symbol = "T-4", HalfLife = 10, Energy = 100, ExemptionLimit = double.NaN };
        var res4 = service.Create(iso4);
        Assert.False(res4.Success);

        // GammaConstant = NaN
        var iso5 = new Radioisotope { Name = "Test5", Symbol = "T-5", HalfLife = 10, Energy = 100, GammaConstant = double.NaN };
        var res5 = service.Create(iso5);
        Assert.False(res5.Success);

        using var db = _fixture.CreateContext();
        Assert.Empty(db.Radioisotopes.ToList());
    }

    [Fact]
    public void RadioisotopeService_Create_WithInfinity_Fails()
    {
        var service = new RadioisotopeService(_fixture.ContextFactory, _mockAudit.Object, _mockUser.Object);

        var iso = new Radioisotope { Name = "TestInf", Symbol = "T-Inf", HalfLife = double.PositiveInfinity, Energy = 100 };
        var res = service.Create(iso);
        Assert.False(res.Success);

        using var db = _fixture.CreateContext();
        Assert.Empty(db.Radioisotopes.ToList());
    }

    [Fact]
    public void RadioisotopeService_Create_WithValidData_Succeeds()
    {
        var service = new RadioisotopeService(_fixture.ContextFactory, _mockAudit.Object, _mockUser.Object);

        var iso = new Radioisotope { Name = "Cobalt-60", Symbol = "Co-60", HalfLife = 5.27, Energy = 1173.2, GammaConstant = 0.305 };
        var res = service.Create(iso);
        Assert.True(res.Success);

        using var db = _fixture.CreateContext();
        Assert.Single(db.Radioisotopes.ToList());
    }

    [Fact]
    public void RadioisotopeService_Update_WithNaN_FailsAndPreservesOriginalValue()
    {
        var service = new RadioisotopeService(_fixture.ContextFactory, _mockAudit.Object, _mockUser.Object);
        var id = Guid.NewGuid();

        using (var db = _fixture.CreateContext())
        {
            db.Radioisotopes.Add(new Radioisotope
            {
                Id = id,
                Name = "Cesium-137",
                Symbol = "Cs-137",
                HalfLife = 30.08,
                Energy = 661.7,
                GammaConstant = 0.077
            });
            db.SaveChanges();
        }

        var updateItem = new Radioisotope
        {
            Id = id,
            Name = "Cesium-137",
            Symbol = "Cs-137",
            HalfLife = double.NaN,
            Energy = 661.7,
            GammaConstant = 0.077
        };

        var res = service.Update(updateItem);
        Assert.False(res.Success);

        using (var db = _fixture.CreateContext())
        {
            var fromDb = db.Radioisotopes.Find(id);
            Assert.NotNull(fromDb);
            Assert.Equal(30.08, fromDb.HalfLife);
        }
    }

    #endregion

    #region 2. NeutronSourceService Tests

    [Fact]
    public void NeutronSourceService_Create_WithNaNOrInfinity_FailsAndDoesNotPersist()
    {
        var typeId = Guid.NewGuid();
        using (var db = _fixture.CreateContext())
        {
            db.NeutronSourceTypes.Add(new NeutronSourceType { Id = typeId, Code = "AmBe-Test", NameEn = "AmBe", HalfLife = 432 });
            db.SaveChanges();
        }

        var service = new NeutronSourceService(_fixture.ContextFactory, _mockAudit.Object, _mockUser.Object);

        // EmissionRate = NaN
        var s1 = new NeutronSource { SourceCode = "NS-1", NeutronSourceTypeId = typeId, CalibratedEmissionRate = double.NaN };
        Assert.False(service.Create(s1).Success);

        // EmissionRate = Infinity
        var s2 = new NeutronSource { SourceCode = "NS-2", NeutronSourceTypeId = typeId, CalibratedEmissionRate = double.PositiveInfinity };
        Assert.False(service.Create(s2).Success);

        // Anisotropy = NaN
        var s3 = new NeutronSource { SourceCode = "NS-3", NeutronSourceTypeId = typeId, CalibratedEmissionRate = 2.2e6, AnisotropyFactor = double.NaN };
        Assert.False(service.Create(s3).Success);

        // Uncertainty = Infinity
        var s4 = new NeutronSource { SourceCode = "NS-4", NeutronSourceTypeId = typeId, CalibratedEmissionRate = 2.2e6, RelativeExpandedUncertaintyPercent = double.PositiveInfinity };
        Assert.False(service.Create(s4).Success);

        using (var db = _fixture.CreateContext())
        {
            Assert.Empty(db.NeutronSources.ToList());
        }
    }

    [Fact]
    public void NeutronSourceService_Update_WithNaN_PreservesOriginalData()
    {
        var typeId = Guid.NewGuid();
        var sourceId = Guid.NewGuid();
        using (var db = _fixture.CreateContext())
        {
            db.NeutronSourceTypes.Add(new NeutronSourceType { Id = typeId, Code = "AmBe-Test", NameEn = "AmBe", HalfLife = 432 });
            db.NeutronSources.Add(new NeutronSource
            {
                Id = sourceId,
                SourceCode = "NS-VALID",
                NeutronSourceTypeId = typeId,
                CalibratedEmissionRate = 2.2e6,
                AnisotropyFactor = 1.05
            });
            db.SaveChanges();
        }

        var service = new NeutronSourceService(_fixture.ContextFactory, _mockAudit.Object, _mockUser.Object);

        var updateItem = new NeutronSource
        {
            Id = sourceId,
            SourceCode = "NS-VALID",
            NeutronSourceTypeId = typeId,
            CalibratedEmissionRate = double.NaN,
            AnisotropyFactor = 1.05
        };

        var res = service.Update(updateItem);
        Assert.False(res.Success);

        using (var db = _fixture.CreateContext())
        {
            var fromDb = db.NeutronSources.Find(sourceId);
            Assert.NotNull(fromDb);
            Assert.Equal(2.2e6, fromDb.CalibratedEmissionRate);
        }
    }

    #endregion

    #region 3. SourceService Tests

    [Fact]
    public void SourceService_CreateSource_WithNaNActivity_FailsAndDoesNotPersist()
    {
        var isoId = Guid.NewGuid();
        var unitId = Guid.NewGuid();
        using (var db = _fixture.CreateContext())
        {
            db.Radioisotopes.Add(new Radioisotope { Id = isoId, Name = "Co-60", Symbol = "Co-60", HalfLife = 5.27 });
            db.ActivityUnits.Add(new ActivityUnit { Id = unitId, UnitName = "MBq", UnitSymbol = "MBq", ConversionToBq = 1e6 });
            db.SaveChanges();
        }

        var service = new SourceService(_fixture.ContextFactory, _mockDecay.Object, _mockAudit.Object, _mockUser.Object);

        // Single source with InitialActivity = NaN
        var src1 = new Source
        {
            SourceCode = "SRC-NAN",
            RadioisotopeId = isoId,
            InitialActivityUnitId = unitId,
            CurrentActivityUnitId = unitId,
            InitialActivityValue = double.NaN,
            CalibrationDate = DateTime.Today
        };
        var res1 = service.CreateSource(src1);
        Assert.False(res1.Success);

        // Multi-isotope mixture with one isotope InitialActivity = Infinity
        var src2 = new Source
        {
            SourceCode = "SRC-MIX",
            RadioisotopeId = isoId,
            InitialActivityUnitId = unitId,
            CurrentActivityUnitId = unitId,
            InitialActivityValue = 100,
            CalibrationDate = DateTime.Today
        };
        var isotopes = new List<SourceIsotope>
        {
            new SourceIsotope { RadioisotopeId = isoId, InitialActivityValue = double.PositiveInfinity, ActivityUnitId = unitId }
        };
        var res2 = service.CreateSource(src2, isotopes);
        Assert.False(res2.Success);

        using (var db = _fixture.CreateContext())
        {
            Assert.Empty(db.Sources.ToList());
        }
    }

    [Fact]
    public void SourceService_UpdateSource_WithNaN_PreservesOriginalData()
    {
        var isoId = Guid.NewGuid();
        var unitId = Guid.NewGuid();
        var srcId = Guid.NewGuid();
        using (var db = _fixture.CreateContext())
        {
            db.Radioisotopes.Add(new Radioisotope { Id = isoId, Name = "Co-60", Symbol = "Co-60", HalfLife = 5.27 });
            db.ActivityUnits.Add(new ActivityUnit { Id = unitId, UnitName = "MBq", UnitSymbol = "MBq", ConversionToBq = 1e6 });
            db.Sources.Add(new Source
            {
                Id = srcId,
                SourceCode = "SRC-ORIG",
                RadioisotopeId = isoId,
                InitialActivityUnitId = unitId,
                CurrentActivityUnitId = unitId,
                InitialActivityValue = 500,
                CalibrationDate = DateTime.Today
            });
            db.SaveChanges();
        }

        var service = new SourceService(_fixture.ContextFactory, _mockDecay.Object, _mockAudit.Object, _mockUser.Object);

        var updateItem = new Source
        {
            Id = srcId,
            SourceCode = "SRC-ORIG",
            RadioisotopeId = isoId,
            InitialActivityUnitId = unitId,
            CurrentActivityUnitId = unitId,
            InitialActivityValue = double.NaN,
            CalibrationDate = DateTime.Today
        };

        var res = service.UpdateSource(updateItem);
        Assert.False(res.Success);

        using (var db = _fixture.CreateContext())
        {
            var fromDb = db.Sources.Find(srcId);
            Assert.NotNull(fromDb);
            Assert.Equal(500, fromDb.InitialActivityValue);
        }
    }

    #endregion

    #region 4. LeakTestService Tests

    [Fact]
    public void LeakTestService_AddRecord_WithNaNOrInfinity_FailsAndDoesNotPersist()
    {
        var isoId = Guid.NewGuid();
        var unitId = Guid.NewGuid();
        var srcId = Guid.NewGuid();
        using (var db = _fixture.CreateContext())
        {
            db.Radioisotopes.Add(new Radioisotope { Id = isoId, Name = "Co-60", Symbol = "Co-60", HalfLife = 5.27 });
            db.ActivityUnits.Add(new ActivityUnit { Id = unitId, UnitName = "MBq", UnitSymbol = "MBq", ConversionToBq = 1e6 });
            db.Sources.Add(new Source
            {
                Id = srcId,
                SourceCode = "SRC-LEAK",
                RadioisotopeId = isoId,
                InitialActivityUnitId = unitId,
                CurrentActivityUnitId = unitId
            });
            db.SaveChanges();
        }

        var service = new LeakTestService(_fixture.ContextFactory, _mockAudit.Object, _mockUser.Object, _mockSettings.Object);

        var rec1 = new LeakTestRecord { SourceId = srcId, MeasuredActivityBq = double.NaN };
        var res1 = service.AddRecord(rec1);
        Assert.False(res1.Success);

        var rec2 = new LeakTestRecord { SourceId = srcId, MeasuredActivityBq = double.PositiveInfinity };
        var res2 = service.AddRecord(rec2);
        Assert.False(res2.Success);

        using (var db = _fixture.CreateContext())
        {
            Assert.Empty(db.LeakTestRecords.ToList());
        }
    }

    [Fact]
    public void LeakTestService_UpdateRecord_WithNaN_PreservesOriginalData()
    {
        var isoId = Guid.NewGuid();
        var unitId = Guid.NewGuid();
        var srcId = Guid.NewGuid();
        var recId = Guid.NewGuid();
        using (var db = _fixture.CreateContext())
        {
            db.Radioisotopes.Add(new Radioisotope { Id = isoId, Name = "Co-60", Symbol = "Co-60", HalfLife = 5.27 });
            db.ActivityUnits.Add(new ActivityUnit { Id = unitId, UnitName = "MBq", UnitSymbol = "MBq", ConversionToBq = 1e6 });
            db.Sources.Add(new Source
            {
                Id = srcId,
                SourceCode = "SRC-LEAK",
                RadioisotopeId = isoId,
                InitialActivityUnitId = unitId,
                CurrentActivityUnitId = unitId
            });
            db.LeakTestRecords.Add(new LeakTestRecord { Id = recId, SourceId = srcId, MeasuredActivityBq = 15.5 });
            db.SaveChanges();
        }

        var service = new LeakTestService(_fixture.ContextFactory, _mockAudit.Object, _mockUser.Object, _mockSettings.Object);

        var updateRec = new LeakTestRecord { Id = recId, SourceId = srcId, MeasuredActivityBq = double.NaN };
        var res = service.UpdateRecord(updateRec);
        Assert.False(res.Success);

        using (var db = _fixture.CreateContext())
        {
            var fromDb = db.LeakTestRecords.Find(recId);
            Assert.NotNull(fromDb);
            Assert.Equal(15.5, fromDb.MeasuredActivityBq);
        }
    }

    #endregion

    #region 5. NeutronSourceTypeService Tests

    [Fact]
    public void NeutronSourceTypeService_Create_WithNaNOrInfinity_FailsAndDoesNotPersist()
    {
        var service = new NeutronSourceTypeService(_fixture.ContextFactory, _mockAudit.Object, _mockUser.Object);

        // HalfLife = NaN
        var t1 = new NeutronSourceType { Code = "TYPE-1", NameEn = "Type 1", HalfLife = double.NaN };
        Assert.False(service.Create(t1).Success);

        // MeanEnergy = Infinity
        var t2 = new NeutronSourceType { Code = "TYPE-2", NameEn = "Type 2", HalfLife = 100, MeanNeutronEnergyMeV = double.PositiveInfinity };
        Assert.False(service.Create(t2).Success);

        // ConversionCoeff = NaN
        var t3 = new NeutronSourceType { Code = "TYPE-3", NameEn = "Type 3", HalfLife = 100, AmbientDoseConversionCoefficient = double.NaN };
        Assert.False(service.Create(t3).Success);

        using (var db = _fixture.CreateContext())
        {
            Assert.Empty(db.NeutronSourceTypes.ToList());
        }
    }

    [Fact]
    public void NeutronSourceTypeService_Update_WithNaN_PreservesOriginalData()
    {
        var typeId = Guid.NewGuid();
        using (var db = _fixture.CreateContext())
        {
            db.NeutronSourceTypes.Add(new NeutronSourceType { Id = typeId, Code = "Cf-252-Test", NameEn = "Californium-252", HalfLife = 2.645 });
            db.SaveChanges();
        }

        var service = new NeutronSourceTypeService(_fixture.ContextFactory, _mockAudit.Object, _mockUser.Object);

        var updateItem = new NeutronSourceType
        {
            Id = typeId,
            Code = "Cf-252-Test",
            NameEn = "Californium-252",
            HalfLife = double.NaN
        };

        var res = service.Update(updateItem);
        Assert.False(res.Success);

        using (var db = _fixture.CreateContext())
        {
            var fromDb = db.NeutronSourceTypes.Find(typeId);
            Assert.NotNull(fromDb);
            Assert.Equal(2.645, fromDb.HalfLife);
        }
    }

    #endregion

    #region 6. UI ViewModel Handlers Tests

    [Fact]
    public void RadioisotopesViewModel_WhenPropertiesAreNaNOrInfinity_ResetsToZeroOrNull()
    {
        var mockService = new Mock<IRadioisotopeService>();
        mockService.Setup(s => s.GetAll()).Returns(new List<Radioisotope>());

        var vm = new RadioisotopesViewModel(mockService.Object);
        CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default.UnregisterAll(vm);

        vm.EditHalfLife = 10.0;
        vm.EditHalfLifeText = "NaN";
        Assert.Equal(0, vm.EditHalfLife);

        vm.EditEnergy = 500.0;
        vm.EditEnergyText = "Infinity";
        Assert.Equal(0, vm.EditEnergy);

        vm.EditYield = 0.85;
        vm.EditYieldText = "-Infinity%";
        Assert.Equal(0, vm.EditYield);

        vm.EditGammaConstant = 0.3;
        vm.EditGammaConstantText = "NaN";
        Assert.Null(vm.EditGammaConstant);
    }

    [Fact]
    public void NeutronSourceTypesViewModel_WhenHalfLifeIsNaN_ResetsToZero()
    {
        var mockService = new Mock<INeutronSourceTypeService>();
        mockService.Setup(s => s.GetAll()).Returns(new List<NeutronSourceType>());

        var vm = new NeutronSourceTypesViewModel(mockService.Object);
        CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default.UnregisterAll(vm);

        vm.EditHalfLife = 20.0;
        vm.EditHalfLifeText = "NaN";
        Assert.Equal(0, vm.EditHalfLife);

        vm.EditHalfLife = 20.0;
        vm.EditHalfLifeText = "Infinity";
        Assert.Equal(0, vm.EditHalfLife);
    }

    [Fact]
    public void LeakTestsViewModel_WhenMeasuredActivityIsInfinity_RejectsInput()
    {
        var mockLeak = new Mock<ILeakTestService>();
        var mockSource = new Mock<ISourceService>();
        var mockReporting = new Mock<IReportingService>();
        var mockUser = new Mock<IUserService>();
        var mockSettings = new Mock<ISystemSettingsService>();

        mockLeak.Setup(s => s.GetAllRecords(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(new List<LeakTestRecord>());
        mockSource.Setup(s => s.GetAllSources()).Returns(new List<Source>());

        var vm = new LeakTestsViewModel(mockLeak.Object, mockSource.Object, mockReporting.Object, mockUser.Object, mockSettings.Object);
        CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default.UnregisterAll(vm);

        vm.FormMeasuredActivityText = "Infinity";
        // Attempting to parse via TryParseFinite returns false
        bool parsed = NumericInputParser.TryParseFinite(vm.FormMeasuredActivityText, out _);
        Assert.False(parsed);
    }

    #endregion
}
