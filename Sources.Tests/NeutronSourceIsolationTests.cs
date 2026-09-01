using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Sources.Data;
using Sources.Models;
using Sources.Services;
using Sources.Tests.Fakes;
using Sources.Tests.Fixtures;
using Xunit;

namespace Sources.Tests;

public class NeutronSourceIsolationTests : IClassFixture<SqliteInMemoryFixture>, IDisposable
{
    private readonly SqliteInMemoryFixture _fixture;
    private readonly DecayCalculationService _decayService;
    private readonly SourceService _sourceService;
    private readonly LocationService _locationService;
    private readonly FakeAuditService _fakeAuditService;
    private readonly FakeUserService _fakeUserService;

    public NeutronSourceIsolationTests(SqliteInMemoryFixture fixture)
    {
        _fixture = fixture;
        _fixture.ResetDatabase();

        _fakeAuditService = new FakeAuditService();
        _fakeUserService = new FakeUserService();
        _decayService = new DecayCalculationService();
        _sourceService = new SourceService(_fixture.ContextFactory, _decayService, _fakeAuditService, _fakeUserService);
        _locationService = new LocationService(_fixture.ContextFactory, _fakeAuditService, _fakeUserService);
    }

    public void Dispose()
    {
        _fixture.ResetDatabase();
    }

    [Fact]
    public void DoseRateCalculation_RemainsIdenticalAndIsolated_WhenNeutronSourcesExistInDatabase()
    {
        // 1. Arrange Gamma isotopes and standard Gamma sources
        // 1. Arrange Gamma isotopes, activity unit, and standard Gamma sources
        var mbqUnit = new ActivityUnit { UnitName = "Megabecquerel", UnitSymbol = "MBq", ConversionToBq = 1e6 };
        var cs137 = new Radioisotope
        {
            Symbol = "Cs-137",
            Name = "Cesium-137",
            RadiationType = "Beta/Gamma",
            GammaConstant = 0.0772,
            HalfLife = 30.08,
            HalfLifeUnit = "years"
        };
        var co60 = new Radioisotope
        {
            Symbol = "Co-60",
            Name = "Cobalt-60",
            RadiationType = "Gamma",
            GammaConstant = 0.305,
            HalfLife = 5.27,
            HalfLifeUnit = "years"
        };

        // 2. Add standard sources to DB
        using (var db = _fixture.CreateContext())
        {
            db.ActivityUnits.Add(mbqUnit);
            db.Radioisotopes.AddRange(cs137, co60);
            db.Sources.Add(new Source
            {
                SourceCode = "SRC-GAMMA-01",
                Radioisotope = cs137,
                InitialActivityUnit = mbqUnit,
                CurrentActivityUnit = mbqUnit,
                InitialActivityValue = 100,
                CurrentActivityValue = 100, // MBq
                Status = "InUse"
            });

            // 3. Inject multiple Neutron Sources and Neutron Source Types into the same database
            var cf252Type = new NeutronSourceType
            {
                Code = "Cf-252",
                NameEn = "Californium-252",
                ReactionType = "Spontaneous Fission",
                HalfLife = 2.645
            };
            var amBeType = new NeutronSourceType
            {
                Code = "Am-241/Be",
                NameEn = "Americium-Beryllium",
                ReactionType = "(α,n)",
                HalfLife = 432.2
            };

            db.NeutronSourceTypes.AddRange(cf252Type, amBeType);
            db.NeutronSources.AddRange(
                new NeutronSource { SourceCode = "NS-001", NeutronSourceType = cf252Type, CalibratedEmissionRate = 2.4e6, Status = "Storage" },
                new NeutronSource { SourceCode = "NS-002", NeutronSourceType = amBeType, CalibratedEmissionRate = 1.1e6, Status = "InUse" }
            );

            db.SaveChanges();
        }

        // 4. Calculate Dose Rate via DecayCalculationService for standard Gamma inputs
        var gammaList = new List<(Radioisotope Isotope, double ActivityMBq)>
        {
            (cs137, 100.0), // 100 * 0.0772 = 7.72 µSv/h
            (co60, 50.0)    // 50 * 0.305 = 15.25 µSv/h
        };

        var doseResult = _decayService.CalculateDoseRateAtOneMeter(gammaList);

        // Assert: Calculations match mathematical expectation with 100% precision
        Assert.Equal(22.97, Math.Round(doseResult.TotalDoseRateMicroSvPerHour, 2));
        Assert.Equal(2, doseResult.Contributions.Count);
        Assert.Equal(DoseRateContributionStatus.Contributing, doseResult.Contributions[0].Status);
        Assert.Equal(DoseRateContributionStatus.Contributing, doseResult.Contributions[1].Status);

        // 5. Verify that standard SourceService and LocationService ignore NeutronSources completely
        var standardSources = _sourceService.GetAllSources();
        Assert.Single(standardSources);
        Assert.Equal("SRC-GAMMA-01", standardSources[0].SourceCode);

        // Verify that total sources count in SourceService does not count NeutronSources
        Assert.Equal(1, _sourceService.GetTotalSourcesCount());
    }

    [Fact]
    public void LocationSourcesQueries_NeverReturnNeutronSourcesInStandardSourcesList()
    {
        // Arrange
        var locId = Guid.NewGuid();
        using (var db = _fixture.CreateContext())
        {
            var unit = new ActivityUnit { UnitName = "Megabecquerel", UnitSymbol = "MBq", ConversionToBq = 1e6 };
            var loc = new Location { Id = locId, LocationName = "Multi-Source Bunker" };
            var iso = new Radioisotope { Symbol = "Ir-192", Name = "Iridium-192", HalfLife = 73.83 };
            var nType = new NeutronSourceType { Code = "Pu-239/Be", NameEn = "Pu-Be", HalfLife = 24110 };

            db.ActivityUnits.Add(unit);
            db.Locations.Add(loc);
            db.Radioisotopes.Add(iso);
            db.NeutronSourceTypes.Add(nType);

            db.Sources.Add(new Source
            {
                SourceCode = "GAMMA-IR-1",
                LocationId = locId,
                Radioisotope = iso,
                InitialActivityUnit = unit,
                CurrentActivityUnit = unit,
                InitialActivityValue = 50,
                CurrentActivityValue = 50
            });
            db.NeutronSources.Add(new NeutronSource { SourceCode = "NEUTRON-PU-1", LocationId = locId, NeutronSourceType = nType, CalibratedEmissionRate = 3e5 });

            db.SaveChanges();
        }

        // Act
        var linkedStandardSources = _locationService.GetSourcesLinkedToLocation(locId);

        // Assert
        Assert.Single(linkedStandardSources);
        Assert.Equal("GAMMA-IR-1", linkedStandardSources[0].SourceCode);
        Assert.DoesNotContain(linkedStandardSources, s => s.SourceCode == "NEUTRON-PU-1");
    }
}
