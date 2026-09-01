using System;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Sources.Data;
using Sources.Models;
using Sources.Services;
using Sources.Tests.Fakes;
using Sources.Tests.Fixtures;
using Xunit;

namespace Sources.Tests;

public class AddedByUnificationTests : IClassFixture<SqliteInMemoryFixture>, IDisposable
{
    private readonly SqliteInMemoryFixture _fixture;
    private readonly FakeAuditService _fakeAuditService;
    private readonly FakeUserService _fakeUserService;
    private readonly SourceService _sourceService;
    private readonly LocationService _locationService;
    private readonly RadioisotopeService _radioisotopeService;
    private readonly BorrowService _borrowService;
    private readonly NeutronSourceService _neutronSourceService;
    private readonly NeutronSourceTypeService _neutronSourceTypeService;
    private readonly User _testUser;
    private readonly ActivityUnit _unit;
    private readonly Radioisotope _iso;
    private readonly Location _loc;
    private readonly NeutronSourceType _nst;

    public AddedByUnificationTests(SqliteInMemoryFixture fixture)
    {
        _fixture = fixture;
        _fixture.ResetDatabase();

        _testUser = new User
        {
            Id = Guid.NewGuid(),
            Username = "addedby_tester",
            FullName = "مختبر توحيد الحقول",
            Role = new Role { Id = Guid.NewGuid(), RoleName = "مدير النظام" }
        };

        _unit = new ActivityUnit { Id = Guid.NewGuid(), UnitName = "MegaBecquerel", UnitSymbol = "MBq", ConversionToBq = 1e6 };
        _iso = new Radioisotope { Id = Guid.NewGuid(), Symbol = "Co-60", Name = "Cobalt-60", ArabicName = "كوبالت-60", HalfLife = 5.27, HalfLifeUnit = "years" };
        _loc = new Location { Id = Guid.NewGuid(), LocationName = "موقع الاختبار الموحد" };
        _nst = new NeutronSourceType { Id = Guid.NewGuid(), Code = "AmBe-Test", NameEn = "Am-Be Test", HalfLife = 432.2 };

        using (var db = _fixture.CreateContext())
        {
            db.Users.Add(_testUser);
            db.ActivityUnits.Add(_unit);
            db.Radioisotopes.Add(_iso);
            db.Locations.Add(_loc);
            db.NeutronSourceTypes.Add(_nst);
            db.SaveChanges();
        }

        _fakeAuditService = new FakeAuditService();
        _fakeUserService = new FakeUserService { CurrentUser = _testUser };

        _sourceService = new SourceService(_fixture.ContextFactory, new DecayCalculationService(), _fakeAuditService, _fakeUserService);
        _locationService = new LocationService(_fixture.ContextFactory, _fakeAuditService, _fakeUserService);
        _radioisotopeService = new RadioisotopeService(_fixture.ContextFactory, _fakeAuditService, _fakeUserService);
        _borrowService = new BorrowService(_fixture.ContextFactory, _fakeAuditService, _fakeUserService);
        _neutronSourceService = new NeutronSourceService(_fixture.ContextFactory, _fakeAuditService, _fakeUserService);
        _neutronSourceTypeService = new NeutronSourceTypeService(_fixture.ContextFactory, _fakeAuditService, _fakeUserService);
    }

    public void Dispose()
    {
        _fixture.ResetDatabase();
    }

    #region 1. كتابة مسجّل الكيان للمستخدم الحالي في الكيانات الستة

    [Fact]
    public void Create_AllSixEntities_AssignsCurrentUserGuid_AndLoadsAddedByUser()
    {
        // 1. Source
        var src = new Source
        {
            SourceCode = "SRC-ADD-01",
            RadioisotopeId = _iso.Id,
            LocationId = _loc.Id,
            InitialActivityValue = 100,
            InitialActivityUnitId = _unit.Id,
            CurrentActivityValue = 100,
            CurrentActivityUnitId = _unit.Id,
            CalibrationDate = DateTime.Now,
            Status = "Storage"
        };
        var (sOk, _) = _sourceService.CreateSource(src);
        Assert.True(sOk);

        var loadedSource = _sourceService.GetSourceById(src.Id);
        Assert.NotNull(loadedSource);
        Assert.Equal(_testUser.Id, loadedSource.AddedBy);
        Assert.Equal(_testUser.FullName, loadedSource.AddedByName);
        Assert.NotNull(loadedSource.AddedByUser);
        Assert.Equal(_testUser.FullName, loadedSource.AddedByUser.FullName);

        // 2. Location
        var loc = new Location { LocationName = "موقع جديد للاختبار" };
        var (lOk, _) = _locationService.Create(loc);
        Assert.True(lOk);

        var loadedLoc = _locationService.GetById(loc.Id);
        Assert.NotNull(loadedLoc);
        Assert.Equal(_testUser.Id, loadedLoc.AddedBy);
        Assert.Equal(_testUser.FullName, loadedLoc.AddedByName);

        // 3. Radioisotope
        var iso = new Radioisotope { Symbol = "Cs-137-Test", Name = "Cesium-137 Test", HalfLife = 30.17 };
        var (rOk, _) = _radioisotopeService.Create(iso);
        Assert.True(rOk);

        var loadedIso = _radioisotopeService.GetById(iso.Id);
        Assert.NotNull(loadedIso);
        Assert.Equal(_testUser.Id, loadedIso.AddedBy);
        Assert.Equal(_testUser.FullName, loadedIso.AddedByName);

        // 4. BorrowRequest
        var borrowReq = new BorrowRequest
        {
            SourceId = src.Id,
            BorrowerName = "مستعير الاختبار",
            Purpose = "تجربة",
            ExpectedReturnDate = DateTime.Today.AddDays(7)
        };
        var (bOk, _) = _borrowService.CreateRequest(borrowReq);
        Assert.True(bOk);

        var loadedBorrows = _borrowService.GetBySource(src.Id);
        Assert.Single(loadedBorrows);
        Assert.Equal(_testUser.Id, loadedBorrows[0].AddedBy);
        Assert.Equal(_testUser.FullName, loadedBorrows[0].AddedByName);

        // 5. NeutronSourceType
        var nst = new NeutronSourceType { Code = "Cf-252-Test", NameEn = "Californium-252", HalfLife = 2.645 };
        var (tOk, _) = _neutronSourceTypeService.Create(nst);
        Assert.True(tOk);

        var loadedNst = _neutronSourceTypeService.GetById(nst.Id);
        Assert.NotNull(loadedNst);
        Assert.Equal(_testUser.Id, loadedNst.AddedBy);
        Assert.Equal(_testUser.FullName, loadedNst.AddedByName);

        // 6. NeutronSource
        var ns = new NeutronSource
        {
            SourceCode = "NS-ADD-01",
            NeutronSourceTypeId = _nst.Id,
            LocationId = _loc.Id,
            EmissionRate = 5.5e5
        };
        var (nOk, _) = _neutronSourceService.Create(ns);
        Assert.True(nOk);

        var loadedNs = _neutronSourceService.GetById(ns.Id);
        Assert.NotNull(loadedNs);
        Assert.Equal(_testUser.Id, loadedNs.AddedBy);
        Assert.Equal(_testUser.FullName, loadedNs.AddedByName);
    }

    #endregion

    #region 2. حارس وجود المستخدم (Non-existent user ID in DB)

    [Fact]
    public void Create_WhenCurrentUserIdDoesNotExistInDb_SetsAddedByToNull_WithoutForeignKeyError()
    {
        // Arrange: User is authenticated in service with a random Guid that does not exist in DB Users table
        var ghostUserId = Guid.NewGuid();
        _fakeUserService.CurrentUser = new User { Id = ghostUserId, FullName = "مستخدم شبحي غير موجود في القاعدة" };

        // 1. Source
        var src = new Source
        {
            SourceCode = "SRC-GHOST-01",
            RadioisotopeId = _iso.Id,
            LocationId = _loc.Id,
            InitialActivityValue = 100,
            InitialActivityUnitId = _unit.Id,
            CurrentActivityValue = 100,
            CurrentActivityUnitId = _unit.Id,
            CalibrationDate = DateTime.Now,
            Status = "Storage"
        };
        var (sOk, _) = _sourceService.CreateSource(src);
        Assert.True(sOk);

        var loadedSource = _sourceService.GetSourceById(src.Id);
        Assert.NotNull(loadedSource);
        Assert.Null(loadedSource.AddedBy);
        Assert.Equal("غير معروف", loadedSource.AddedByName);

        // 2. Location
        var loc = new Location { LocationName = "موقع شبحي" };
        var (lOk, _) = _locationService.Create(loc);
        Assert.True(lOk);
        var loadedLoc = _locationService.GetById(loc.Id);
        Assert.NotNull(loadedLoc);
        Assert.Null(loadedLoc.AddedBy);
        Assert.Equal("غير معروف", loadedLoc.AddedByName);

        // 3. Radioisotope
        var iso = new Radioisotope { Symbol = "Ghost-Isotope", Name = "Ghost", HalfLife = 10 };
        var (rOk, _) = _radioisotopeService.Create(iso);
        Assert.True(rOk);
        var loadedIso = _radioisotopeService.GetById(iso.Id);
        Assert.NotNull(loadedIso);
        Assert.Null(loadedIso.AddedBy);
        Assert.Equal("غير معروف", loadedIso.AddedByName);

        // 4. BorrowRequest
        var borrowReq = new BorrowRequest
        {
            SourceId = src.Id,
            BorrowerName = "مستعير شبحي",
            Purpose = "تجربة",
            ExpectedReturnDate = DateTime.Today.AddDays(7)
        };
        var (bOk, _) = _borrowService.CreateRequest(borrowReq);
        Assert.True(bOk);
        var loadedBorrows = _borrowService.GetBySource(src.Id);
        Assert.Single(loadedBorrows);
        Assert.Null(loadedBorrows[0].AddedBy);
        Assert.Equal("غير معروف", loadedBorrows[0].AddedByName);

        // 5. NeutronSourceType
        var nst = new NeutronSourceType { Code = "Ghost-NST", NameEn = "Ghost Type", HalfLife = 100 };
        var (tOk, _) = _neutronSourceTypeService.Create(nst);
        Assert.True(tOk);
        var loadedNst = _neutronSourceTypeService.GetById(nst.Id);
        Assert.NotNull(loadedNst);
        Assert.Null(loadedNst.AddedBy);
        Assert.Equal("غير معروف", loadedNst.AddedByName);

        // 6. NeutronSource
        var ns = new NeutronSource
        {
            SourceCode = "NS-GHOST-01",
            NeutronSourceTypeId = _nst.Id,
            LocationId = _loc.Id,
            EmissionRate = 1e5
        };
        var (nOk, _) = _neutronSourceService.Create(ns);
        Assert.True(nOk);
        var loadedNs = _neutronSourceService.GetById(ns.Id);
        Assert.NotNull(loadedNs);
        Assert.Null(loadedNs.AddedBy);
        Assert.Equal("غير معروف", loadedNs.AddedByName);
    }

    #endregion

    #region 3. عدم وجود مستخدم مسجّل (CurrentUser == null)

    [Fact]
    public void Create_WhenCurrentUserIsNull_SetsAddedByToNull()
    {
        _fakeUserService.CurrentUser = null;

        var loc = new Location { LocationName = "موقع مجهول الهوية" };
        var (lOk, _) = _locationService.Create(loc);
        Assert.True(lOk);

        var loadedLoc = _locationService.GetById(loc.Id);
        Assert.NotNull(loadedLoc);
        Assert.Null(loadedLoc.AddedBy);
        Assert.Equal("غير معروف", loadedLoc.AddedByName);
    }

    #endregion

    #region 4. خاصية AddedByName الحسابية

    [Fact]
    public void AddedByName_ComputedProperty_ReturnsFullNameOrUnknown()
    {
        var user = new User { Id = Guid.NewGuid(), FullName = "المهندس المشرف" };
        var entityWithUser = new Source { AddedBy = user.Id, AddedByUser = user };
        var entityWithoutUser = new Source { AddedBy = null, AddedByUser = null };

        Assert.Equal("المهندس المشرف", entityWithUser.AddedByName);
        Assert.Equal("غير معروف", entityWithoutUser.AddedByName);
    }

    #endregion

    #region 5. سلوك الحذف الأجنبي (DeleteBehavior.SetNull)

    [Fact]
    public void ForeignKey_WhenUserDeletedHard_SetsAddedByToNullInAllEntities()
    {
        // Arrange
        Guid tempUserId = Guid.NewGuid();
        var tempUser = new User
        {
            Id = tempUserId,
            Username = "temp_deletable",
            FullName = "مستخدم مؤقت للحذف الصلب",
            Role = new Role { Id = Guid.NewGuid(), RoleName = "مستخدم" }
        };

        var src = new Source
        {
            Id = Guid.NewGuid(),
            SourceCode = "SRC-FK-DEL-01",
            RadioisotopeId = _iso.Id,
            LocationId = _loc.Id,
            InitialActivityValue = 50,
            InitialActivityUnitId = _unit.Id,
            CurrentActivityValue = 50,
            CurrentActivityUnitId = _unit.Id,
            CalibrationDate = DateTime.Now,
            Status = "Storage",
            AddedBy = tempUserId
        };

        var loc = new Location
        {
            Id = Guid.NewGuid(),
            LocationName = "موقع للحذف الصلب",
            AddedBy = tempUserId
        };

        var iso = new Radioisotope
        {
            Id = Guid.NewGuid(),
            Symbol = "Iso-FK-Del",
            Name = "Del Isotope",
            HalfLife = 10,
            AddedBy = tempUserId
        };

        var borrowReq = new BorrowRequest
        {
            Id = Guid.NewGuid(),
            SourceId = src.Id,
            BorrowerName = "مستعير",
            Purpose = "غرض",
            ExpectedReturnDate = DateTime.Today.AddDays(3),
            Status = "Delivered",
            AddedBy = tempUserId
        };

        var nst = new NeutronSourceType
        {
            Id = Guid.NewGuid(),
            Code = "NST-FK-Del",
            NameEn = "NST Del",
            HalfLife = 50,
            AddedBy = tempUserId
        };

        var ns = new NeutronSource
        {
            Id = Guid.NewGuid(),
            SourceCode = "NS-FK-DEL-01",
            NeutronSourceTypeId = _nst.Id,
            LocationId = _loc.Id,
            EmissionRate = 2e5,
            AddedBy = tempUserId
        };

        using (var db = _fixture.CreateContext())
        {
            db.Users.Add(tempUser);
            db.Sources.Add(src);
            db.Locations.Add(loc);
            db.Radioisotopes.Add(iso);
            db.BorrowRequests.Add(borrowReq);
            db.NeutronSourceTypes.Add(nst);
            db.NeutronSources.Add(ns);
            db.SaveChanges();
        }

        // Act: حذف المستخدم صلبياً من قاعدة البيانات
        using (var db = _fixture.CreateContext())
        {
            var u = db.Users.Find(tempUserId);
            Assert.NotNull(u);
            db.Users.Remove(u);
            db.SaveChanges();
        }

        // Assert: التحقق من أن المفتاح الخارجي AddedBy في جميع الجداول أصبح null بفضل SetNull
        using (var db = _fixture.CreateContext())
        {
            var dbSrc = db.Sources.Find(src.Id);
            var dbLoc = db.Locations.Find(loc.Id);
            var dbIso = db.Radioisotopes.Find(iso.Id);
            var dbReq = db.BorrowRequests.Find(borrowReq.Id);
            var dbNst = db.NeutronSourceTypes.Find(nst.Id);
            var dbNs = db.NeutronSources.Find(ns.Id);

            Assert.Null(dbSrc!.AddedBy);
            Assert.Null(dbLoc!.AddedBy);
            Assert.Null(dbIso!.AddedBy);
            Assert.Null(dbReq!.AddedBy);
            Assert.Null(dbNst!.AddedBy);
            Assert.Null(dbNs!.AddedBy);
        }
    }

    #endregion

    #region 6. قيد المفتاح الخارجي المباشر على مستوى قاعدة البيانات (Direct DbContext Insert with non-existent User)

    [Fact]
    public void ForeignKeyConstraint_DirectDbContextInsert_WithNonExistentUser_ThrowsDbUpdateException_ForSourceAndNeutronSource()
    {
        // 1. اختبار المصدر العادي Source
        var nonExistentUserId = Guid.NewGuid();
        var invalidSource = new Source
        {
            Id = Guid.NewGuid(),
            SourceCode = "SRC-INVALID-FK-01",
            RadioisotopeId = _iso.Id,
            LocationId = _loc.Id,
            InitialActivityValue = 10,
            InitialActivityUnitId = _unit.Id,
            CurrentActivityValue = 10,
            CurrentActivityUnitId = _unit.Id,
            CalibrationDate = DateTime.Now,
            Status = "Storage",
            AddedBy = nonExistentUserId // مستخدم غير موجود
        };

        using (var db = _fixture.CreateContext())
        {
            db.Sources.Add(invalidSource);
            var ex = Assert.Throws<DbUpdateException>(() => db.SaveChanges());
            Assert.Contains("FOREIGN KEY", ex.InnerException?.Message ?? ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        // 2. اختبار المصدر النيتروني NeutronSource
        var invalidNeutronSource = new NeutronSource
        {
            Id = Guid.NewGuid(),
            SourceCode = "NS-INVALID-FK-01",
            NeutronSourceTypeId = _nst.Id,
            LocationId = _loc.Id,
            EmissionRate = 1e4,
            AddedBy = nonExistentUserId // مستخدم غير موجود
        };

        using (var db = _fixture.CreateContext())
        {
            db.NeutronSources.Add(invalidNeutronSource);
            var ex = Assert.Throws<DbUpdateException>(() => db.SaveChanges());
            Assert.Contains("FOREIGN KEY", ex.InnerException?.Message ?? ex.Message, StringComparison.OrdinalIgnoreCase);
        }
    }

    #endregion

    #region 7. اختبار ترحيل البيانات السابقة واليتيمة (Migration with Pre-existing Data)

    [Fact]
    public void Migration_FromInitialSchemaWithPreExistingOrphanAndLegacyData_MigratesCleanly()
    {
        var tempDbPath = Path.Combine(Path.GetTempPath(), "Sources_MigrateTest_" + Guid.NewGuid().ToString("N") + ".db");
        try
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite($"Data Source={tempDbPath}")
                .Options;

            // 1. تطبيق المخطط الأولي فقط InitialSchema
            using (var ctx = new AppDbContext(options))
            {
                var migrator = Microsoft.EntityFrameworkCore.Infrastructure.AccessorExtensions.GetService<Microsoft.EntityFrameworkCore.Migrations.IMigrator>(ctx.Database);
                migrator.Migrate("20260901112320_InitialSchema");
            }

            var validUserGuid = Guid.NewGuid();
            var orphanUserGuid = Guid.NewGuid();
            var validRoleGuid = Guid.NewGuid();

            // 2. زرع بيانات سابقة تحوي أيتام ونصوص قديمة بـ SQL خام
            using (var conn = new SqliteConnection($"Data Source={tempDbPath}"))
            {
                conn.Open();
                using var cmd = conn.CreateCommand();

                cmd.CommandText = $@"
INSERT INTO Roles (Id, RoleName, Description, Permissions) VALUES ('{validRoleGuid}', 'Admin', 'Admin Role', 'All');

INSERT INTO Users (Id, FullName, Username, PasswordHash, RoleId, IsActive, IsDeleted, CreatedAt, FailedLoginAttempts, IsEditor)
VALUES ('{validUserGuid}', 'أحمد المهندس', 'ahmed_eng', 'hash', '{validRoleGuid}', 1, 0, '2026-01-01', 0, 0);

INSERT INTO NeutronSourceTypes (Id, Code, NameEn, NameAr, ReactionType, HalfLife, HalfLifeUnit, AddedBy, IsDeleted, CreatedAt)
VALUES ('{Guid.NewGuid()}', 'NST-ORPHAN', 'Orphan Type', 'نوع يتيم', '(alpha,n)', 10.0, 'years', '{orphanUserGuid}', 0, '2026-01-01');

INSERT INTO Locations (Id, LocationName, IsDeleted)
VALUES ('{Guid.NewGuid()}', 'موقع نيتروني', 0);

INSERT INTO NeutronSources (Id, SourceCode, NeutronSourceTypeId, LocationId, EmissionRate, Status, AddedBy, IsDeleted, CreatedAt)
VALUES ('{Guid.NewGuid()}', 'NS-ORPHAN-01', (SELECT Id FROM NeutronSourceTypes LIMIT 1), (SELECT Id FROM Locations LIMIT 1), 1000, 'Storage', '{orphanUserGuid}', 0, '2026-01-01');

INSERT INTO Radioisotopes (Id, Name, Symbol, RadiationType, HalfLife, HalfLifeUnit, Energy, Category, IsDeleted)
VALUES ('{Guid.NewGuid()}', 'Cesium', 'Cs-137-Mig', 'Gamma', 30.0, 'years', 661.7, 1, 0);

INSERT INTO ActivityUnits (Id, UnitName, UnitSymbol, ConversionToBq)
VALUES ('{Guid.NewGuid()}', 'Bq-Mig', 'Bq', 1.0);

INSERT INTO Sources (Id, SourceCode, RadioisotopeId, InitialActivityValue, InitialActivityUnitId, CurrentActivityValue, CurrentActivityUnitId, CalibrationDate, Status, HasDetailedIsotopes, IsSealed, AddedBy, IsDeleted, CreatedAt)
VALUES ('{Guid.NewGuid()}', 'SRC-NONMATCH', (SELECT Id FROM Radioisotopes LIMIT 1), 100, (SELECT Id FROM ActivityUnits LIMIT 1), 100, (SELECT Id FROM ActivityUnits LIMIT 1), '2026-01-01', 'Storage', 0, 1, 'مستخدم غير معروف نصي', 0, '2026-01-01');

INSERT INTO Sources (Id, SourceCode, RadioisotopeId, InitialActivityValue, InitialActivityUnitId, CurrentActivityValue, CurrentActivityUnitId, CalibrationDate, Status, HasDetailedIsotopes, IsSealed, AddedBy, IsDeleted, CreatedAt)
VALUES ('{Guid.NewGuid()}', 'SRC-MATCH', (SELECT Id FROM Radioisotopes LIMIT 1), 200, (SELECT Id FROM ActivityUnits LIMIT 1), 200, (SELECT Id FROM ActivityUnits LIMIT 1), '2026-01-01', 'Storage', 0, 1, 'أحمد المهندس', 0, '2026-01-01');
";
                cmd.ExecuteNonQuery();
            }

            // 3. تطبيق باقي الترحيلات (UnifyAddedByToGuid)
            using (var ctx = new AppDbContext(options))
            {
                ctx.Database.Migrate();
            }

            // 4. التحقق من تنظيف الأيتام وترحيل البيانات بنجاح
            using (var conn = new SqliteConnection($"Data Source={tempDbPath}"))
            {
                conn.Open();
                using var cmd = conn.CreateCommand();

                cmd.CommandText = "SELECT AddedBy FROM NeutronSources WHERE SourceCode = 'NS-ORPHAN-01';";
                var nsAddedBy = cmd.ExecuteScalar();
                Assert.True(nsAddedBy == null || nsAddedBy == DBNull.Value || string.IsNullOrEmpty(nsAddedBy.ToString()));

                cmd.CommandText = "SELECT AddedBy FROM NeutronSourceTypes WHERE Code = 'NST-ORPHAN';";
                var nstAddedBy = cmd.ExecuteScalar();
                Assert.True(nstAddedBy == null || nstAddedBy == DBNull.Value || string.IsNullOrEmpty(nstAddedBy.ToString()));

                cmd.CommandText = "SELECT AddedBy FROM Sources WHERE SourceCode = 'SRC-NONMATCH';";
                var srcNonMatch = cmd.ExecuteScalar();
                Assert.True(srcNonMatch == null || srcNonMatch == DBNull.Value || string.IsNullOrEmpty(srcNonMatch.ToString()));

                cmd.CommandText = "SELECT AddedBy FROM Sources WHERE SourceCode = 'SRC-MATCH';";
                var srcMatch = cmd.ExecuteScalar()?.ToString();
                Assert.Equal(validUserGuid.ToString(), srcMatch, ignoreCase: true);
            }
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(tempDbPath))
            {
                try { File.Delete(tempDbPath); } catch { }
            }
        }
    }

    [Fact]
    public void Migration_DownFromUnifyAddedByToGuid_RevertsToInitialSchemaAndConvertsAddedByToString()
    {
        var tempDbPath = Path.Combine(Path.GetTempPath(), "Sources_DownMigrateTest_" + Guid.NewGuid().ToString("N") + ".db");
        try
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite($"Data Source={tempDbPath}")
                .Options;

            // 1. تطبيق جميع الترحيلات كاملة Database.Migrate()
            using (var ctx = new AppDbContext(options))
            {
                ctx.Database.Migrate();
            }

            var userId = Guid.NewGuid();
            var roleId = Guid.NewGuid();
            var userFullName = "د. محمد الباحث";
            var sourceId = Guid.NewGuid();
            var isoId = Guid.NewGuid();
            var unitId = Guid.NewGuid();

            // 2. زرع مستخدم ومصدر يحمل AddedBy مساوياً لمعرّف ذلك المستخدم
            using (var conn = new SqliteConnection($"Data Source={tempDbPath}"))
            {
                conn.Open();
                using var cmd = conn.CreateCommand();

                cmd.CommandText = $@"
INSERT INTO Roles (Id, RoleName, Description, Permissions) VALUES ('{roleId}', 'Admin', 'Admin Role', 'All');

INSERT INTO Users (Id, FullName, Username, PasswordHash, RoleId, IsActive, IsDeleted, CreatedAt, FailedLoginAttempts, IsEditor)
VALUES ('{userId}', '{userFullName}', 'mohamed_doc', 'hash', '{roleId}', 1, 0, '2026-01-01', 0, 0);

INSERT INTO Radioisotopes (Id, Name, Symbol, RadiationType, HalfLife, HalfLifeUnit, Energy, Category, IsDeleted)
VALUES ('{isoId}', 'Cobalt', 'Co-60-Down', 'Gamma', 5.27, 'years', 1173.2, 1, 0);

INSERT INTO ActivityUnits (Id, UnitName, UnitSymbol, ConversionToBq)
VALUES ('{unitId}', 'MBq-Down', 'MBq', 1e6);

INSERT INTO Sources (Id, SourceCode, RadioisotopeId, InitialActivityValue, InitialActivityUnitId, CurrentActivityValue, CurrentActivityUnitId, CalibrationDate, Status, HasDetailedIsotopes, IsSealed, AddedBy, IsDeleted, CreatedAt)
VALUES ('{sourceId}', 'SRC-DOWN-01', '{isoId}', 100, '{unitId}', 100, '{unitId}', '2026-01-01', 'Storage', 0, 1, '{userId}', 0, '2026-01-01');
";
                cmd.ExecuteNonQuery();
            }

            // 3. التراجع إلى InitialSchema عبر IMigrator.Migrate("20260901112320_InitialSchema")
            using (var ctx = new AppDbContext(options))
            {
                var migrator = Microsoft.EntityFrameworkCore.Infrastructure.AccessorExtensions.GetService<Microsoft.EntityFrameworkCore.Migrations.IMigrator>(ctx.Database);
                migrator.Migrate("20260901112320_InitialSchema");
            }

            // 4. التحقق بـ SqliteConnection و SQL خام أن العمود يحمل FullName نصاً وأن التراجع لم يرمِ استثناءً
            using (var conn = new SqliteConnection($"Data Source={tempDbPath}"))
            {
                conn.Open();
                using var cmd = conn.CreateCommand();

                cmd.CommandText = "SELECT AddedBy FROM Sources WHERE SourceCode = 'SRC-DOWN-01';";
                var result = cmd.ExecuteScalar()?.ToString();
                Assert.Equal(userFullName, result);
            }
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(tempDbPath))
            {
                try { File.Delete(tempDbPath); } catch { }
            }
        }
    }

    #endregion
}
