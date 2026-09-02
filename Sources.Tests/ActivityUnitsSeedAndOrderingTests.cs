using System;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Sources.Data;
using Sources.Models;
using Xunit;

namespace Sources.Tests;

public class ActivityUnitsSeedAndOrderingTests : IDisposable
{
    private readonly string _tempDbPath;
    private readonly DbContextOptions<AppDbContext> _options;

    public ActivityUnitsSeedAndOrderingTests()
    {
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"sources_test_units_{Guid.NewGuid():N}.db");
        _options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={_tempDbPath}")
            .Options;
    }

    public void Dispose()
    {
        try
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(_tempDbPath)) File.Delete(_tempDbPath);
            if (File.Exists(_tempDbPath + "-wal")) File.Delete(_tempDbPath + "-wal");
            if (File.Exists(_tempDbPath + "-shm")) File.Delete(_tempDbPath + "-shm");
        }
        catch { }
    }

    [Fact]
    public void Test1_SeedData_ContainsEightUnits_AndExistingUnitsRemainUnmodified()
    {
        // Arrange & Act
        using (var db = new AppDbContext(_options))
        {
            db.InitializeDatabase();
        }

        // Assert
        using (var db = new AppDbContext(_options))
        {
            var units = db.ActivityUnits.ToList();
            Assert.Equal(8, units.Count);

            var bq = units.FirstOrDefault(u => u.UnitSymbol == "Bq");
            Assert.NotNull(bq);
            Assert.Equal("Becquerel", bq!.UnitName);
            Assert.Equal(1.0, bq.ConversionToBq);

            var ci = units.FirstOrDefault(u => u.UnitSymbol == "Ci");
            Assert.NotNull(ci);
            Assert.Equal("Curie", ci!.UnitName);
            Assert.Equal(3.7e10, ci.ConversionToBq);

            var mci = units.FirstOrDefault(u => u.UnitSymbol == "mCi");
            Assert.NotNull(mci);
            Assert.Equal("Millicurie", mci!.UnitName);
            Assert.Equal(3.7e7, mci.ConversionToBq);

            var uci = units.FirstOrDefault(u => u.UnitSymbol == "µCi");
            Assert.NotNull(uci);
            Assert.Equal("Microcurie", uci!.UnitName);
            Assert.Equal(3.7e4, uci.ConversionToBq);
        }
    }

    [Fact]
    public void Test2_SeedData_NewUnitsHaveCorrectConversionFactors()
    {
        // Arrange & Act
        using (var db = new AppDbContext(_options))
        {
            db.InitializeDatabase();
        }

        // Assert
        using (var db = new AppDbContext(_options))
        {
            var kbq = db.ActivityUnits.FirstOrDefault(u => u.UnitSymbol == "kBq");
            Assert.NotNull(kbq);
            Assert.Equal("Kilobecquerel", kbq!.UnitName);
            Assert.Equal(1e3, kbq.ConversionToBq);

            var mbq = db.ActivityUnits.FirstOrDefault(u => u.UnitSymbol == "MBq");
            Assert.NotNull(mbq);
            Assert.Equal("Megabecquerel", mbq!.UnitName);
            Assert.Equal(1e6, mbq.ConversionToBq);

            var gbq = db.ActivityUnits.FirstOrDefault(u => u.UnitSymbol == "GBq");
            Assert.NotNull(gbq);
            Assert.Equal("Gigabecquerel", gbq!.UnitName);
            Assert.Equal(1e9, gbq.ConversionToBq);

            var tbq = db.ActivityUnits.FirstOrDefault(u => u.UnitSymbol == "TBq");
            Assert.NotNull(tbq);
            Assert.Equal("Terabecquerel", tbq!.UnitName);
            Assert.Equal(1e12, tbq.ConversionToBq);
        }
    }

    [Fact]
    public void Test3_SeedData_ExecutedTwice_DoesNotDuplicateOrDeleteUnits()
    {
        // Arrange & Act
        using (var db = new AppDbContext(_options))
        {
            db.InitializeDatabase();
        }

        using (var db = new AppDbContext(_options))
        {
            db.InitializeDatabase();
        }

        // Assert
        using (var db = new AppDbContext(_options))
        {
            var units = db.ActivityUnits.ToList();
            Assert.Equal(8, units.Count);
            Assert.Equal(8, units.Select(u => u.UnitSymbol).Distinct().Count());
            Assert.Equal(8, units.Select(u => u.UnitName).Distinct().Count());
        }
    }

    [Fact]
    public void Test4_ActivityUnits_OrderByConversionToBq_ReturnsExpectedSequence()
    {
        // Arrange & Act
        using (var db = new AppDbContext(_options))
        {
            db.InitializeDatabase();
        }

        // Assert
        using (var db = new AppDbContext(_options))
        {
            var orderedSymbols = db.ActivityUnits
                .OrderBy(u => u.ConversionToBq)
                .Select(u => u.UnitSymbol)
                .ToList();

            var expected = new[] { "Bq", "kBq", "µCi", "MBq", "mCi", "GBq", "Ci", "TBq" };
            Assert.Equal(expected, orderedSymbols);
        }
    }

    [Fact]
    public void Test5_SeedData_WithExistingSourceLinkedToExistingUnit_LeavesSourceAndUnitIntact()
    {
        // Arrange
        Guid sourceId = Guid.NewGuid();
        Guid unitCiId;

        using (var db = new AppDbContext(_options))
        {
            db.InitializeDatabase();

            var unitCi = db.ActivityUnits.First(u => u.UnitSymbol == "Ci");
            unitCiId = unitCi.Id;

            var isotope = db.Radioisotopes.First();
            var location = new Location { Id = Guid.NewGuid(), LocationName = "مختبر 1" };
            db.Locations.Add(location);

            var source = new Source
            {
                Id = sourceId,
                SourceCode = "TEST-SRC-001",
                RadioisotopeId = isotope.Id,
                InitialActivityValue = 100,
                InitialActivityUnitId = unitCiId,
                CurrentActivityValue = 100,
                CurrentActivityUnitId = unitCiId,
                CalibrationDate = DateTime.Today,
                Status = "Storage",
                LocationId = location.Id
            };
            db.Sources.Add(source);
            db.SaveChanges();
        }

        // Act: Run InitializeDatabase again (which runs SeedData)
        using (var db = new AppDbContext(_options))
        {
            db.InitializeDatabase();
        }

        // Assert
        using (var db = new AppDbContext(_options))
        {
            var source = db.Sources
                .Include(s => s.InitialActivityUnit)
                .Include(s => s.CurrentActivityUnit)
                .FirstOrDefault(s => s.Id == sourceId);

            Assert.NotNull(source);
            Assert.Equal("TEST-SRC-001", source!.SourceCode);
            Assert.Equal(unitCiId, source.InitialActivityUnitId);
            Assert.Equal(unitCiId, source.CurrentActivityUnitId);
            Assert.NotNull(source.InitialActivityUnit);
            Assert.Equal("Ci", source.InitialActivityUnit!.UnitSymbol);
            Assert.Equal("Curie", source.InitialActivityUnit.UnitName);
            Assert.Equal(3.7e10, source.InitialActivityUnit.ConversionToBq);
        }
    }
}
