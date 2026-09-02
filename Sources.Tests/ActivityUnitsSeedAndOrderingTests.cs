using System;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
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
    public void Test4_ActivityUnits_OrderByDisplayOrder_ReturnsExpectedSequence()
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
                .OrderBy(u => u.DisplayOrder)
                .Select(u => u.UnitSymbol)
                .ToList();

            var expected = new[] { "Bq", "kBq", "MBq", "GBq", "TBq", "µCi", "mCi", "Ci" };
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

    [Fact]
    public void Test6_SeedData_EachUnitHasCorrectNonZeroDisplayOrder()
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
            Assert.DoesNotContain(units, u => u.DisplayOrder == 0);

            Assert.Equal(1, units.First(u => u.UnitName == "Becquerel" && u.UnitSymbol == "Bq").DisplayOrder);
            Assert.Equal(2, units.First(u => u.UnitName == "Kilobecquerel" && u.UnitSymbol == "kBq").DisplayOrder);
            Assert.Equal(3, units.First(u => u.UnitName == "Megabecquerel" && u.UnitSymbol == "MBq").DisplayOrder);
            Assert.Equal(4, units.First(u => u.UnitName == "Gigabecquerel" && u.UnitSymbol == "GBq").DisplayOrder);
            Assert.Equal(5, units.First(u => u.UnitName == "Terabecquerel" && u.UnitSymbol == "TBq").DisplayOrder);
            Assert.Equal(6, units.First(u => u.UnitName == "Microcurie" && u.UnitSymbol == "µCi").DisplayOrder);
            Assert.Equal(7, units.First(u => u.UnitName == "Millicurie" && u.UnitSymbol == "mCi").DisplayOrder);
            Assert.Equal(8, units.First(u => u.UnitName == "Curie" && u.UnitSymbol == "Ci").DisplayOrder);
        }
    }

    [Fact]
    public void Test7_UpgradeScenario_ExistingDatabaseUpgraded_AllUnitsReceiveCorrectNonZeroDisplayOrder()
    {
        var upgradeDbPath = Path.Combine(Path.GetTempPath(), $"sources_upgrade_test_{Guid.NewGuid():N}.db");
        var upgradeOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={upgradeDbPath}")
            .Options;

        try
        {
            // 1. إنشاء القاعدة وترحيلها حتى ما قبل ترحيل AddActivityUnitDisplayOrder
            using (var db = new AppDbContext(upgradeOptions))
            {
                var migrator = db.Database.GetService<IMigrator>();
                migrator.Migrate("20260901184302_AddNeutronCalibrationAndDecayFields");

                // إدراج وحدات النشاط بالنمط القديم بدون عمود DisplayOrder
                var conn = db.Database.GetDbConnection();
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    INSERT INTO ActivityUnits (Id, UnitName, UnitSymbol, ConversionToBq, Description)
                    VALUES 
                    ('" + Guid.NewGuid().ToString().ToUpper() + @"', 'Becquerel', 'Bq', 1.0, 'الوحدة الدولية'),
                    ('" + Guid.NewGuid().ToString().ToUpper() + @"', 'Curie', 'Ci', 3.7e10, '1 Ci'),
                    ('" + Guid.NewGuid().ToString().ToUpper() + @"', 'Millicurie', 'mCi', 3.7e7, '1 mCi'),
                    ('" + Guid.NewGuid().ToString().ToUpper() + @"', 'Microcurie', 'µCi', 3.7e4, '1 µCi'),
                    ('" + Guid.NewGuid().ToString().ToUpper() + @"', 'Kilobecquerel', 'kBq', 1e3, '1 kBq'),
                    ('" + Guid.NewGuid().ToString().ToUpper() + @"', 'Megabecquerel', 'MBq', 1e6, '1 MBq'),
                    ('" + Guid.NewGuid().ToString().ToUpper() + @"', 'Gigabecquerel', 'GBq', 1e9, '1 GBq'),
                    ('" + Guid.NewGuid().ToString().ToUpper() + @"', 'Terabecquerel', 'TBq', 1e12, '1 TBq');
                ";
                cmd.ExecuteNonQuery();
                conn.Close();
            }

            // 2. تطبيق الترحيل الأخير وتشغيل InitializeDatabase (الذي يستدعي Migrate و SeedData)
            using (var db = new AppDbContext(upgradeOptions))
            {
                db.InitializeDatabase();
            }

            // 3. التحقق من أن جميع الوحدات الثماني حصلت على DisplayOrder صحيح وغير صفري
            using (var db = new AppDbContext(upgradeOptions))
            {
                var units = db.ActivityUnits.OrderBy(u => u.DisplayOrder).ToList();
                Assert.Equal(8, units.Count);
                Assert.DoesNotContain(units, u => u.DisplayOrder == 0);

                var symbolsInOrder = units.Select(u => u.UnitSymbol).ToList();
                var expectedOrder = new[] { "Bq", "kBq", "MBq", "GBq", "TBq", "µCi", "mCi", "Ci" };
                Assert.Equal(expectedOrder, symbolsInOrder);

                Assert.Equal(1, units.First(u => u.UnitSymbol == "Bq").DisplayOrder);
                Assert.Equal(2, units.First(u => u.UnitSymbol == "kBq").DisplayOrder);
                Assert.Equal(3, units.First(u => u.UnitSymbol == "MBq").DisplayOrder);
                Assert.Equal(4, units.First(u => u.UnitSymbol == "GBq").DisplayOrder);
                Assert.Equal(5, units.First(u => u.UnitSymbol == "TBq").DisplayOrder);
                Assert.Equal(6, units.First(u => u.UnitSymbol == "µCi").DisplayOrder);
                Assert.Equal(7, units.First(u => u.UnitSymbol == "mCi").DisplayOrder);
                Assert.Equal(8, units.First(u => u.UnitSymbol == "Ci").DisplayOrder);
            }
        }
        finally
        {
            try
            {
                SqliteConnection.ClearAllPools();
                if (File.Exists(upgradeDbPath)) File.Delete(upgradeDbPath);
                if (File.Exists(upgradeDbPath + "-wal")) File.Delete(upgradeDbPath + "-wal");
                if (File.Exists(upgradeDbPath + "-shm")) File.Delete(upgradeDbPath + "-shm");
            }
            catch { }
        }
    }
}
