#if DEBUG
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Sources.Data;
using Sources.Models;
using Sources.Services;
using Xunit;

namespace Sources.Tests;

public class TestDataGeneratorTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly IDecayCalculationService _decayService;

    public TestDataGeneratorTests()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        _factory = new TestGenDbContextFactory(_options);
        _decayService = new DecayCalculationService();

        using var db = new AppDbContext(_options);
        db.Database.EnsureCreated();
        SeedEssentialData(db);
    }

    private void SeedEssentialData(AppDbContext db)
    {
        // 1. Units
        db.ActivityUnits.AddRange(
            new ActivityUnit { UnitName = "Becquerel", UnitSymbol = "Bq", ConversionToBq = 1 },
            new ActivityUnit { UnitName = "Curie", UnitSymbol = "Ci", ConversionToBq = 3.7e10 },
            new ActivityUnit { UnitName = "Millicurie", UnitSymbol = "mCi", ConversionToBq = 3.7e7 },
            new ActivityUnit { UnitName = "Microcurie", UnitSymbol = "µCi", ConversionToBq = 3.7e4 }
        );

        // 2. Isotopes (The 19 library isotopes)
        var isotopes = new[]
        {
            new Radioisotope { Symbol = "Am-241", Name = "Americium-241", HalfLife = 432.2, HalfLifeUnit = "years", RadiationType = "Alpha" },
            new Radioisotope { Symbol = "Ba-133", Name = "Barium-133", HalfLife = 10.51, HalfLifeUnit = "years", RadiationType = "Gamma" },
            new Radioisotope { Symbol = "Cf-252", Name = "Californium-252", HalfLife = 2.645, HalfLifeUnit = "years", RadiationType = "Neutron" },
            new Radioisotope { Symbol = "Co-57", Name = "Cobalt-57", HalfLife = 271.7, HalfLifeUnit = "days", RadiationType = "Gamma" },
            new Radioisotope { Symbol = "Co-60", Name = "Cobalt-60", HalfLife = 5.27, HalfLifeUnit = "years", RadiationType = "Gamma" },
            new Radioisotope { Symbol = "Cs-137", Name = "Cesium-137", HalfLife = 30.08, HalfLifeUnit = "years", RadiationType = "Beta/Gamma" },
            new Radioisotope { Symbol = "Eu-152", Name = "Europium-152", HalfLife = 13.53, HalfLifeUnit = "years", RadiationType = "Gamma" },
            new Radioisotope { Symbol = "F-18", Name = "Fluorine-18", HalfLife = 109.7, HalfLifeUnit = "minutes", RadiationType = "Beta+" },
            new Radioisotope { Symbol = "I-131", Name = "Iodine-131", HalfLife = 8.02, HalfLifeUnit = "days", RadiationType = "Beta/Gamma" },
            new Radioisotope { Symbol = "Ir-192", Name = "Iridium-192", HalfLife = 73.83, HalfLifeUnit = "days", RadiationType = "Gamma" },
            new Radioisotope { Symbol = "K-40", Name = "Potassium-40", HalfLife = 1.25e9, HalfLifeUnit = "years", RadiationType = "Beta/Gamma" },
            new Radioisotope { Symbol = "Lu-177", Name = "Lutetium-177", HalfLife = 6.64, HalfLifeUnit = "days", RadiationType = "Beta/Gamma" },
            new Radioisotope { Symbol = "Na-22", Name = "Sodium-22", HalfLife = 2.6, HalfLifeUnit = "years", RadiationType = "Beta+/Gamma" },
            new Radioisotope { Symbol = "Pu-239", Name = "Plutonium-239", HalfLife = 24110.0, HalfLifeUnit = "years", RadiationType = "Alpha" },
            new Radioisotope { Symbol = "Ra-226", Name = "Radium-226", HalfLife = 1600.0, HalfLifeUnit = "years", RadiationType = "Alpha/Gamma" },
            new Radioisotope { Symbol = "Se-75", Name = "Selenium-75", HalfLife = 119.7, HalfLifeUnit = "days", RadiationType = "Gamma" },
            new Radioisotope { Symbol = "Tc-99m", Name = "Technetium-99m", HalfLife = 6.01, HalfLifeUnit = "hours", RadiationType = "Gamma" },
            new Radioisotope { Symbol = "Tl-208", Name = "Thallium-208", HalfLife = 3.05, HalfLifeUnit = "minutes", RadiationType = "Gamma" },
            new Radioisotope { Symbol = "Y-88", Name = "Yttrium-88", HalfLife = 106.6, HalfLifeUnit = "days", RadiationType = "Gamma" }
        };
        db.Radioisotopes.AddRange(isotopes);

        // 3. Locations
        db.Locations.AddRange(
            new Location { LocationName = "المخزن الرئيسي", LocationType = "Storage" },
            new Location { LocationName = "معمل القياسات", LocationType = "Lab" },
            new Location { LocationName = "قسم الطب النووي", LocationType = "Hospital" }
        );

        db.SaveChanges();
    }

    public void Dispose()
    {
        _connection.Close();
        _connection.Dispose();
    }

    private class TestGenDbContextFactory : IDbContextFactory<AppDbContext>
    {
        private readonly DbContextOptions<AppDbContext> _opts;
        public TestGenDbContextFactory(DbContextOptions<AppDbContext> opts) => _opts = opts;
        public AppDbContext CreateDbContext() => new(_opts);
    }

    [Fact]
    public async Task GenerateRealisticDataAsync_CreatesExpectedCountsAndValidData()
    {
        // Act
        var result = await TestDataGeneratorService.GenerateRealisticDataAsync(
            _factory,
            _decayService);

        // Assert - Result Object
        Assert.True(result.Success, result.Message);
        Assert.Equal(20, result.TotalLocations);
        Assert.Equal(300, result.TotalSources);
        Assert.Equal(60, result.MultiIsotopeSources);
        Assert.Equal(25, result.WarningAlertSources);
        Assert.Equal(25, result.CriticalAlertSources);
        Assert.Equal(100, result.TotalBorrowRequests);
        Assert.Equal(70, result.ReturnedBorrows);
        Assert.Equal(15, result.DeliveredBorrows);
        Assert.Equal(10, result.OverdueBorrows);
        Assert.Equal(5, result.PendingOrApprovedBorrows);

        // Assert - Database State
        using var db = new AppDbContext(_options);

        // 1. Locations
        var locations = await db.Locations.ToListAsync();
        Assert.Equal(20, locations.Count);
        Assert.All(locations, l => Assert.False(string.IsNullOrWhiteSpace(l.LocationName)));

        // 2. Sources
        var sources = await db.Sources
            .Include(s => s.SourceIsotopes)
            .Include(s => s.Radioisotope)
            .ToListAsync();
        Assert.Equal(300, sources.Count);

        // Unique codes
        var uniqueCodes = sources.Select(s => s.SourceCode).Distinct().Count();
        Assert.Equal(300, uniqueCodes);

        // Multi isotope count
        var multiCount = sources.Count(s => s.HasDetailedIsotopes);
        Assert.Equal(60, multiCount);
        foreach (var multi in sources.Where(s => s.HasDetailedIsotopes))
        {
            Assert.True(multi.SourceIsotopes.Count >= 2, $"Source {multi.SourceCode} should have at least 2 isotopes");
        }

        // Current Activity calculated
        Assert.All(sources, s => Assert.True(s.CurrentActivityValue >= 0));

        // 3. Borrow Requests
        var borrows = await db.BorrowRequests.ToListAsync();
        Assert.Equal(100, borrows.Count);

        // Strict Uniqueness for Active/Overdue borrows per source
        var activeBorrows = borrows.Where(b => b.Status == "Delivered" || b.Status == "Overdue").ToList();
        Assert.Equal(25, activeBorrows.Count);
        var uniqueActiveSources = activeBorrows.Select(b => b.SourceId).Distinct().Count();
        Assert.Equal(25, uniqueActiveSources);

        // Returned status has actual return date
        var returned = borrows.Where(b => b.Status == "Returned").ToList();
        Assert.Equal(70, returned.Count);
        Assert.All(returned, r => Assert.NotNull(r.ActualReturnDate));

        // Overdue status has expected return date in the past
        var overdue = borrows.Where(b => b.Status == "Overdue").ToList();
        Assert.Equal(10, overdue.Count);
        Assert.All(overdue, o => Assert.True(o.ExpectedReturnDate.Date < DateTime.Today));

        // Delivered status has expected return date in future
        var delivered = borrows.Where(b => b.Status == "Delivered").ToList();
        Assert.Equal(15, delivered.Count);
        Assert.All(delivered, d => Assert.True(d.ExpectedReturnDate.Date >= DateTime.Today));
    }
}
#endif

