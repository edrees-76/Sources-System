using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Microsoft.Data.Sqlite;
using Sources.Data;
using Sources.Helpers;
using Sources.Models;
using Sources.Services;
using Sources.Tests.Fakes;
using Sources.Tests.Fixtures;
using Xunit;

namespace Sources.Tests;

/// <summary>
/// الجولة 209 — استعادة النسخ الاحتياطية (Zip Slip، صلاحية المدير، عدم لمس القاعدة الحية عند الرفض).
/// </summary>
public class Round209BackupRestoreTests : IDisposable
{
    private const string KnownMigration = "20260901112320_InitialSchema";

    private readonly string _testRoot;
    private readonly string _dbPath;
    private readonly string _backupDir;
    private readonly string _certsDir;
    private readonly FakeLicenseService _license = new();

    public Round209BackupRestoreTests()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), "Sources_R209_Restore_" + Guid.NewGuid().ToString("N"));
        _backupDir = Path.Combine(_testRoot, "Backups");
        _dbPath = Path.Combine(_testRoot, "Live.db");
        _certsDir = Path.Combine(_testRoot, "AppData", "Certificates");
        Directory.CreateDirectory(_backupDir);
        Directory.CreateDirectory(_certsDir);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try
        {
            if (Directory.Exists(_testRoot))
                Directory.Delete(_testRoot, recursive: true);
        }
        catch { }
    }

    private static FakeUserService UserWithRole(string roleName) => new(new User
    {
        Id = Guid.NewGuid(),
        FullName = "مستخدم اختباري",
        Username = "actor",
        IsActive = true,
        IsEditor = true,
        Permissions = "All",
        Role = new Role { RoleName = roleName }
    });

    private BackupService CreateSut(IUserService? userService) =>
        new(_dbPath, _backupDir, _certsDir, _license, userService);

    private static void CreateDatabase(string path, string code, bool withMigrationHistory = true)
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(path)) File.Delete(path);
        using var conn = new SqliteConnection($"Data Source={path}");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"CREATE TABLE Sources (Id INTEGER PRIMARY KEY, Code TEXT); INSERT INTO Sources (Code) VALUES ('{code}');";
        if (withMigrationHistory)
        {
            cmd.CommandText += " CREATE TABLE \"__EFMigrationsHistory\" (\"MigrationId\" TEXT NOT NULL PRIMARY KEY, \"ProductVersion\" TEXT NOT NULL);" +
                               $" INSERT INTO \"__EFMigrationsHistory\" VALUES ('{KnownMigration}', '8.0.12');";
        }
        cmd.ExecuteNonQuery();
        conn.Close();
        SqliteConnection.ClearAllPools();
    }

    private static string ReadCode(string path)
    {
        SqliteConnection.ClearAllPools();
        using var conn = new SqliteConnection($"Data Source={path};Mode=ReadOnly");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Code FROM Sources LIMIT 1;";
        var result = cmd.ExecuteScalar()?.ToString() ?? string.Empty;
        conn.Close();
        SqliteConnection.ClearAllPools();
        return result;
    }

    private string CreateZip(string name, string dbCode, params (string EntryName, string Content)[] extraEntries)
    {
        var dbFile = Path.Combine(_testRoot, $"zipdb_{Guid.NewGuid():N}.db");
        CreateDatabase(dbFile, dbCode);
        var zipPath = Path.Combine(_testRoot, name);
        using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            zip.CreateEntryFromFile(dbFile, "Sources.db");
            foreach (var (entryName, content) in extraEntries)
            {
                var entry = zip.CreateEntry(entryName);
                using var writer = new StreamWriter(entry.Open());
                writer.Write(content);
            }
        }
        return zipPath;
    }

    [Fact]
    public void RestoreBackup_ZipSlipCertificateEntry_IsRejected_AndNothingIsWrittenOrReplaced()
    {
        CreateDatabase(_dbPath, "LIVE_DATA");
        File.WriteAllText(Path.Combine(_certsDir, "existing.pdf"), "existing");
        var zip = CreateZip("evil.zip", "EVIL_DATA", ("Certificates/../../../escaped.txt", "pwned"));
        var escapedTarget = Path.GetFullPath(Path.Combine(_certsDir, "..", "..", "..", "escaped.txt"));

        var result = CreateSut(UserWithRole(RoleNames.Admin)).RestoreBackup(zip);

        Assert.False(result.Success);
        Assert.False(File.Exists(escapedTarget), "مدخل ZIP بمسار خارج مجلد الشهادات يجب ألا يُكتب على القرص إطلاقاً");
        Assert.Equal("LIVE_DATA", ReadCode(_dbPath));
        Assert.True(File.Exists(Path.Combine(_certsDir, "existing.pdf")));
        Assert.Empty(Directory.GetFiles(_backupDir, "SOURCES_pre_restore_*.db"));
    }

    [Fact]
    public void RestoreBackup_NonAdminUser_IsRejected_AndLiveDatabaseUntouched()
    {
        CreateDatabase(_dbPath, "LIVE_DATA");
        var backup = Path.Combine(_backupDir, "SOURCES_backup_x.db");
        CreateDatabase(backup, "BACKUP_DATA");

        var result = CreateSut(UserWithRole(RoleNames.User)).RestoreBackup(backup);

        Assert.False(result.Success);
        Assert.Equal("LIVE_DATA", ReadCode(_dbPath));
    }

    [Fact]
    public void RestoreBackup_NoUserService_FailsClosed()
    {
        CreateDatabase(_dbPath, "LIVE_DATA");
        var backup = Path.Combine(_backupDir, "SOURCES_backup_x.db");
        CreateDatabase(backup, "BACKUP_DATA");

        var result = CreateSut(null).RestoreBackup(backup);

        Assert.False(result.Success);
        Assert.Equal("LIVE_DATA", ReadCode(_dbPath));
    }

    [Fact]
    public void RestoreBackup_IncompatibleZip_LeavesDatabaseAndCertificatesUntouched_WithoutSafetyCopy()
    {
        CreateDatabase(_dbPath, "LIVE_DATA");
        File.WriteAllText(Path.Combine(_certsDir, "existing.pdf"), "existing");

        var legacyDb = Path.Combine(_testRoot, "legacy.db");
        CreateDatabase(legacyDb, "LEGACY_DATA", withMigrationHistory: false);
        var zipPath = Path.Combine(_testRoot, "legacy.zip");
        using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            zip.CreateEntryFromFile(legacyDb, "Sources.db");
            var cert = zip.CreateEntry("Certificates/new.pdf");
            using var writer = new StreamWriter(cert.Open());
            writer.Write("new");
        }

        var result = CreateSut(UserWithRole(RoleNames.Admin)).RestoreBackup(zipPath);

        Assert.False(result.Success);
        Assert.Equal("LIVE_DATA", ReadCode(_dbPath));
        Assert.True(File.Exists(Path.Combine(_certsDir, "existing.pdf")));
        Assert.False(File.Exists(Path.Combine(_certsDir, "new.pdf")));
        // الرفض يحدث قبل أي استبدال، فلا حاجة لنسخة أمان وقائية.
        Assert.Empty(Directory.GetFiles(_backupDir, "SOURCES_pre_restore_*.db"));
    }

    [Fact]
    public void RestoreBackup_ValidZip_ReplacesDatabaseAndCertificates_IncludingNestedFolders()
    {
        CreateDatabase(_dbPath, "LIVE_DATA");
        File.WriteAllText(Path.Combine(_certsDir, "old.pdf"), "old");
        var zip = CreateZip("good.zip", "RESTORED_DATA",
            ("Certificates/new.pdf", "new"),
            ("Certificates/sub/nested.pdf", "nested"));

        var result = CreateSut(UserWithRole(RoleNames.Admin)).RestoreBackup(zip);

        Assert.True(result.Success, result.Message);
        Assert.Equal("RESTORED_DATA", ReadCode(_dbPath));
        Assert.False(File.Exists(Path.Combine(_certsDir, "old.pdf")));
        Assert.Equal("new", File.ReadAllText(Path.Combine(_certsDir, "new.pdf")));
        Assert.Equal("nested", File.ReadAllText(Path.Combine(_certsDir, "sub", "nested.pdf")));
        Assert.Single(Directory.GetFiles(_backupDir, "SOURCES_pre_restore_*.db"));
        Assert.Empty(Directory.GetDirectories(Path.GetDirectoryName(_certsDir)!, "Certificates_pre_restore_*"));
    }

    [Fact]
    public void RestoreBackup_ZipWithDbOnlyInsideSubfolder_IsRejectedAsMissingDatabase()
    {
        CreateDatabase(_dbPath, "LIVE_DATA");
        var innerDb = Path.Combine(_testRoot, "inner.db");
        CreateDatabase(innerDb, "INNER_DATA");
        var zipPath = Path.Combine(_testRoot, "nested.zip");
        using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            zip.CreateEntryFromFile(innerDb, "Certificates/sneaky.db");
        }

        var result = CreateSut(UserWithRole(RoleNames.Admin)).RestoreBackup(zipPath);

        Assert.False(result.Success);
        Assert.Equal("LIVE_DATA", ReadCode(_dbPath));
    }

    [Fact]
    public void BackupService_ResolvedThroughDependencyInjection_ReceivesUserServiceForAdminCheck()
    {
        // نفس نمط تسجيل App.ConfigureServices: يجب أن يختار الحاوي المُنشئ الذي يستقبل IUserService،
        // وإلا بقيت الاستعادة مرفوضة للجميع (أو مسموحة بلا فحص إن تغيّر الحارس لاحقاً).
        IBackupService Resolve(IUserService users)
        {
            var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
            Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions.AddSingleton<ILicenseService>(services, _license);
            Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions.AddSingleton(services, users);
            Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions.AddSingleton<IBackupService, BackupService>(services);
            using var provider = Microsoft.Extensions.DependencyInjection.ServiceCollectionContainerBuilderExtensions.BuildServiceProvider(services);
            return Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IBackupService>(provider);
        }

        var missing = Path.Combine(_testRoot, "does_not_exist.zip");
        var asUser = Resolve(UserWithRole(RoleNames.User)).RestoreBackup(missing);
        var asAdmin = Resolve(UserWithRole(RoleNames.Admin)).RestoreBackup(missing);

        Assert.False(asUser.Success);
        Assert.Contains("مدير النظام", asUser.Message);
        Assert.False(asAdmin.Success);
        Assert.Contains("غير موجود", asAdmin.Message);
    }

    [Theory]
    [InlineData("a.pdf", true)]
    [InlineData("sub/a.pdf", true)]
    [InlineData("../a.pdf", false)]
    [InlineData("../../a.pdf", false)]
    [InlineData("sub/../../a.pdf", false)]
    [InlineData("", false)]
    public void IsPathInsideDirectory_DetectsTraversal(string relative, bool expectedInside)
    {
        var root = Path.Combine(_testRoot, "root");
        var candidate = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));

        Assert.Equal(expectedInside, BackupService.IsPathInsideDirectory(candidate, root));
    }

    [Fact]
    public void IsPathInsideDirectory_SiblingWithSharedPrefix_IsOutside()
    {
        var root = Path.Combine(_testRoot, "Certificates");
        var sibling = Path.Combine(_testRoot, "Certificates_evil", "a.pdf");

        Assert.False(BackupService.IsPathInsideDirectory(sibling, root));
    }
}

/// <summary>
/// الجولة 209 — حراسات إدارة المستخدمين وتغيير المستخدم لكلمة مروره بنفسه.
/// </summary>
public class Round209UserServiceTests : IClassFixture<SqliteInMemoryFixture>, IDisposable
{
    private readonly SqliteInMemoryFixture _fixture;
    private readonly UserService _sut;
    private readonly Role _adminRole;
    private readonly Role _userRole;

    public Round209UserServiceTests(SqliteInMemoryFixture fixture)
    {
        _fixture = fixture;
        _fixture.ResetDatabase();
        _sut = new UserService(_fixture.ContextFactory, licenseService: new FakeLicenseService());

        _adminRole = new Role { Id = Guid.NewGuid(), RoleName = RoleNames.Admin, Permissions = "All" };
        _userRole = new Role { Id = Guid.NewGuid(), RoleName = RoleNames.User, Permissions = "Sources" };
        using var db = _fixture.CreateContext();
        db.Roles.AddRange(_adminRole, _userRole);
        db.SaveChanges();
    }

    public void Dispose() => _sut.Logout();

    private User AddUser(string username, string password, Role role, bool isActive = true, bool mustChange = false)
    {
        using var db = _fixture.CreateContext();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = username,
            FullName = "مستخدم " + username,
            PasswordHash = PasswordHelper.HashPassword(password),
            RoleId = role.Id,
            IsActive = isActive,
            MustChangePassword = mustChange,
            CreatedAt = DateTime.Now
        };
        db.Users.Add(user);
        db.SaveChanges();
        return user;
    }

    private static User CopyForUpdate(User u, Guid? roleId = null, bool? isActive = null) => new()
    {
        Id = u.Id,
        Username = u.Username,
        FullName = u.FullName,
        Email = u.Email,
        RoleId = roleId ?? u.RoleId,
        IsActive = isActive ?? u.IsActive,
        IsEditor = u.IsEditor,
        Permissions = u.Permissions
    };

    private User LoginAs(string username, string password, Role role)
    {
        var user = AddUser(username, password, role);
        Assert.True(_sut.Login(username, password).Success);
        return user;
    }

    [Fact]
    public void CreateUser_UsernameDifferingOnlyInCase_IsRejected()
    {
        LoginAs("boss", "BossPass1", _adminRole);
        AddUser("ahmed", "Password1", _userRole);

        var result = _sut.CreateUser(new User { FullName = "x", Username = "AHMED", RoleId = _userRole.Id }, "Password1");

        Assert.False(result.Success);
        using var db = _fixture.CreateContext();
        Assert.Single(db.Users.Where(u => u.Username.ToLower() == "ahmed"));
    }

    [Fact]
    public void CreateUser_TrimsUsername_AndRejectsShortPassword()
    {
        LoginAs("boss", "BossPass1", _adminRole);

        var shortResult = _sut.CreateUser(new User { FullName = "x", Username = "newbie", RoleId = _userRole.Id }, "12345");
        Assert.False(shortResult.Success);

        var ok = _sut.CreateUser(new User { FullName = "x", Username = "  spaced  ", RoleId = _userRole.Id }, "123456");
        Assert.True(ok.Success, ok.Message);
        using var db = _fixture.CreateContext();
        Assert.True(db.Users.Any(u => u.Username == "spaced"));
    }

    [Fact]
    public void Login_PrefersExactCaseMatch_WhenLegacyCaseDuplicatesExist()
    {
        AddUser("Admin", "UpperPass1", _userRole);
        AddUser("admin", "LowerPass1", _adminRole);

        var result = _sut.Login("admin", "LowerPass1");

        Assert.True(result.Success, result.Message);
        Assert.Equal("admin", _sut.CurrentUser!.Username);
    }

    [Fact]
    public void UpdateUser_CannotDemoteOrFreezeBaseAdmin()
    {
        AddUser("admin", "AdminPass1", _adminRole);
        var admin2 = LoginAs("second_admin", "AdminPass2", _adminRole);
        using var db = _fixture.CreateContext();
        var baseAdmin = db.Users.Single(u => u.Username == "admin");

        var demote = _sut.UpdateUser(CopyForUpdate(baseAdmin, roleId: _userRole.Id));
        var freeze = _sut.UpdateUser(CopyForUpdate(baseAdmin, isActive: false));

        Assert.False(demote.Success);
        Assert.False(freeze.Success);
        using var check = _fixture.CreateContext();
        var reloaded = check.Users.Single(u => u.Username == "admin");
        Assert.Equal(_adminRole.Id, reloaded.RoleId);
        Assert.True(reloaded.IsActive);
        Assert.NotEqual(Guid.Empty, admin2.Id);
    }

    [Fact]
    public void UpdateUser_CannotRemoveLastActiveAdmin()
    {
        var onlyAdmin = LoginAs("solo_admin", "AdminPass1", _adminRole);

        var result = _sut.UpdateUser(CopyForUpdate(onlyAdmin, roleId: _userRole.Id));

        Assert.False(result.Success);
        using var db = _fixture.CreateContext();
        Assert.Equal(_adminRole.Id, db.Users.Find(onlyAdmin.Id)!.RoleId);
    }

    [Fact]
    public void UpdateUser_DemotingAdmin_AllowedWhenAnotherActiveAdminRemains()
    {
        LoginAs("boss", "BossPass1", _adminRole);
        var other = AddUser("other_admin", "OtherPass1", _adminRole);

        var result = _sut.UpdateUser(CopyForUpdate(other, roleId: _userRole.Id));

        Assert.True(result.Success, result.Message);
    }

    [Fact]
    public void ToggleUserFreeze_And_DeleteUser_OnOwnAccount_AreRejected()
    {
        var me = LoginAs("boss", "BossPass1", _adminRole);
        AddUser("other_admin", "OtherPass1", _adminRole);

        Assert.False(_sut.ToggleUserFreeze(me.Id).Success);
        Assert.False(_sut.DeleteUser(me.Id).Success);

        using var db = _fixture.CreateContext();
        var reloaded = db.Users.Find(me.Id)!;
        Assert.True(reloaded.IsActive);
        Assert.False(reloaded.IsDeleted);
    }

    [Fact]
    public void ChangeOwnPassword_NonAdmin_SucceedsWithCorrectCurrentPassword_AndSyncsSession()
    {
        var me = LoginAs("clerk", "OldPass1", _userRole);

        var result = _sut.ChangeOwnPassword("OldPass1", "NewPass22");

        Assert.True(result.Success, result.Message);
        Assert.True(PasswordHelper.VerifyPassword("NewPass22", _sut.CurrentUser!.PasswordHash));
        using var db = _fixture.CreateContext();
        var reloaded = db.Users.Find(me.Id)!;
        Assert.True(PasswordHelper.VerifyPassword("NewPass22", reloaded.PasswordHash));
        Assert.False(reloaded.MustChangePassword);
    }

    [Fact]
    public void ChangeOwnPassword_ClearsMustChangePassword()
    {
        AddUser("fresh", "admin1", _adminRole, mustChange: true);
        Assert.True(_sut.Login("fresh", "admin1").Success);

        var result = _sut.ChangeOwnPassword("admin1", "Better123");

        Assert.True(result.Success, result.Message);
        Assert.False(_sut.CurrentUser!.MustChangePassword);
    }

    [Theory]
    [InlineData("WrongPass", "NewPass22")]  // كلمة المرور الحالية خاطئة
    [InlineData("OldPass1", "123")]         // الجديدة أقصر من الحد الأدنى
    [InlineData("OldPass1", "OldPass1")]    // الجديدة مطابقة للحالية
    [InlineData("OldPass1", "      ")]      // فراغات فقط
    public void ChangeOwnPassword_InvalidInput_IsRejected_AndPasswordUnchanged(string current, string next)
    {
        var me = LoginAs("clerk", "OldPass1", _userRole);

        var result = _sut.ChangeOwnPassword(current, next);

        Assert.False(result.Success);
        using var db = _fixture.CreateContext();
        Assert.True(PasswordHelper.VerifyPassword("OldPass1", db.Users.Find(me.Id)!.PasswordHash));
    }

    [Fact]
    public void ChangeOwnPassword_NotLoggedIn_IsRejected()
    {
        Assert.False(_sut.ChangeOwnPassword("a", "NewPass22").Success);
    }

    [Fact]
    public void ResetPassword_OwnAccount_SyncsInMemorySessionHash()
    {
        var me = LoginAs("boss", "BossPass1", _adminRole);

        var result = _sut.ResetPassword(me.Id, "BossPass2");

        Assert.True(result.Success, result.Message);
        Assert.True(PasswordHelper.VerifyPassword("BossPass2", _sut.CurrentUser!.PasswordHash));
        Assert.False(PasswordHelper.VerifyPassword("BossPass1", _sut.CurrentUser!.PasswordHash));
    }

    [Fact]
    public void ResetPassword_EmptyPassword_IsRejected()
    {
        LoginAs("boss", "BossPass1", _adminRole);
        var target = AddUser("clerk", "OldPass1", _userRole);

        var result = _sut.ResetPassword(target.Id, "   ");

        Assert.False(result.Success);
        using var db = _fixture.CreateContext();
        Assert.True(PasswordHelper.VerifyPassword("OldPass1", db.Users.Find(target.Id)!.PasswordHash));
    }
}

/// <summary>
/// الجولة 209 — صحة الحسابات: نسبة النشاط المتبقي بوحدتين مختلفتين، ورفض وحدات نصف العمر المجهولة.
/// </summary>
public class Round209CalculationTests
{
    private static ActivityUnit Unit(string symbol, double factor) => new() { UnitSymbol = symbol, UnitName = symbol, ConversionToBq = factor };

    [Fact]
    public void RemainingActivityFraction_ConvertsBothValuesToBq_BeforeDividing()
    {
        // 10 mCi = 3.7e8 Bq ابتدائياً، 37 MBq = 3.7e7 Bq حالياً → 10% متبقٍّ (القسمة الخام كانت تعطي 370%).
        var source = new Source
        {
            InitialActivityValue = 10,
            InitialActivityUnit = Unit("mCi", 3.7e7),
            CurrentActivityValue = 37,
            CurrentActivityUnit = Unit("MBq", 1e6)
        };

        var fraction = SourceService.GetRemainingActivityFraction(source);

        Assert.NotNull(fraction);
        Assert.Equal(0.1, fraction!.Value, precision: 10);
    }

    [Fact]
    public void RemainingActivityFraction_SameUnits_IsPlainRatio()
    {
        var source = new Source
        {
            InitialActivityValue = 200,
            InitialActivityUnit = Unit("MBq", 1e6),
            CurrentActivityValue = 50,
            CurrentActivityUnit = Unit("MBq", 1e6)
        };

        Assert.Equal(0.25, SourceService.GetRemainingActivityFraction(source)!.Value, precision: 10);
    }

    [Fact]
    public void RemainingActivityFraction_MissingUnitOrZeroInitial_ReturnsNull()
    {
        Assert.Null(SourceService.GetRemainingActivityFraction(new Source
        {
            InitialActivityValue = 10, InitialActivityUnit = null,
            CurrentActivityValue = 5, CurrentActivityUnit = Unit("MBq", 1e6)
        }));
        Assert.Null(SourceService.GetRemainingActivityFraction(new Source
        {
            InitialActivityValue = 0, InitialActivityUnit = Unit("MBq", 1e6),
            CurrentActivityValue = 5, CurrentActivityUnit = Unit("MBq", 1e6)
        }));
    }

    [Theory]
    [InlineData("fortnights")]
    [InlineData("سنة")]
    [InlineData("")]
    [InlineData(null)]
    public void ConvertTimeToSeconds_UnknownUnit_ThrowsInsteadOfAssumingYears(string? unit)
    {
        var service = new DecayCalculationService();

        Assert.Throws<ArgumentException>(() => service.ConvertTimeToSeconds(1.0, unit!));
        Assert.False(DecayCalculationService.IsSupportedHalfLifeUnit(unit));
    }

    [Fact]
    public void ConvertTimeToSeconds_TrimsAndIgnoresCase()
    {
        var service = new DecayCalculationService();

        Assert.Equal(DecayCalculationService.SecondsPerYear, service.ConvertTimeToSeconds(1.0, " Years "), precision: 4);
        Assert.True(DecayCalculationService.IsSupportedHalfLifeUnit(" DAYS "));
    }
}

/// <summary>الجولة 209 — رفض حفظ نظير بوحدة نصف عمر غير مدعومة في طبقة الخدمة.</summary>
public class Round209RadioisotopeUnitValidationTests : IClassFixture<SqliteInMemoryFixture>, IDisposable
{
    private readonly SqliteInMemoryFixture _fixture;
    private readonly RadioisotopeService _sut;

    public Round209RadioisotopeUnitValidationTests(SqliteInMemoryFixture fixture)
    {
        _fixture = fixture;
        _fixture.ResetDatabase();
        var role = new Role { Id = Guid.NewGuid(), RoleName = RoleNames.Admin, Permissions = "All" };
        var user = new User { Id = Guid.NewGuid(), FullName = "x", Username = "x", RoleId = role.Id, Role = role, Permissions = "All", IsActive = true, IsEditor = true };
        using (var db = _fixture.CreateContext())
        {
            db.Roles.Add(role);
            db.Users.Add(user);
            db.SaveChanges();
        }
        _sut = new RadioisotopeService(_fixture.ContextFactory, new FakeAuditService(), new FakeUserService(user), new FakeLicenseService());
    }

    public void Dispose() => _fixture.ResetDatabase();

    [Fact]
    public void Create_UnsupportedHalfLifeUnit_IsRejected()
    {
        var result = _sut.Create(new Radioisotope { Symbol = "Xx-1", Name = "Test", HalfLife = 2, HalfLifeUnit = "fortnights" });

        Assert.False(result.Success);
        using var db = _fixture.CreateContext();
        Assert.False(db.Radioisotopes.Any(r => r.Symbol == "Xx-1"));
    }

    [Fact]
    public void Create_SupportedUnitWithSpaces_IsTrimmedAndSaved()
    {
        var result = _sut.Create(new Radioisotope { Symbol = "Xx-2", Name = "Test", HalfLife = 2, HalfLifeUnit = " days " });

        Assert.True(result.Success, result.Message);
        using var db = _fixture.CreateContext();
        Assert.Equal("days", db.Radioisotopes.Single(r => r.Symbol == "Xx-2").HalfLifeUnit);
    }
}

/// <summary>الجولة 209 — حوار تغيير المستخدم لكلمة مروره بنفسه.</summary>
public class Round209ChangeOwnPasswordDialogTests
{
    private static void RunInSta(Action action) => Sources.Tests.Fixtures.WpfStaFixture.RunInSta(action);

    private static T Field<T>(Sources.Views.ChangeOwnPasswordDialog dialog, string name) =>
        (T)typeof(Sources.Views.ChangeOwnPasswordDialog)
            .GetField(name, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public)!
            .GetValue(dialog)!;

    private static void Fill(Sources.Views.ChangeOwnPasswordDialog dialog, string current, string next, string confirm)
    {
        Field<System.Windows.Controls.PasswordBox>(dialog, "TxtCurrentPassword").Password = current;
        Field<System.Windows.Controls.PasswordBox>(dialog, "TxtNewPassword").Password = next;
        Field<System.Windows.Controls.PasswordBox>(dialog, "TxtConfirmPassword").Password = confirm;
    }

    private static void Confirm(Sources.Views.ChangeOwnPasswordDialog dialog) =>
        typeof(Sources.Views.ChangeOwnPasswordDialog)
            .GetMethod("ConfirmButton_Click", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .Invoke(dialog, new object?[] { dialog, new System.Windows.RoutedEventArgs() });

    [Theory]
    [InlineData("", "")]
    [InlineData("   ", "   ")]
    [InlineData("NewPass22", "Different1")]
    public void ValidateInput_EmptyOrMismatched_ReturnsError(string next, string confirm)
    {
        Assert.NotNull(Sources.Views.ChangeOwnPasswordDialog.ValidateInput(next, confirm));
    }

    [Fact]
    public void ValidateInput_Matching_ReturnsNull()
    {
        Assert.Null(Sources.Views.ChangeOwnPasswordDialog.ValidateInput("NewPass22", "NewPass22"));
    }

    [Fact]
    public void Confirm_WithMismatchedConfirmation_DoesNotCallService_AndShowsError()
    {
        RunInSta(() =>
        {
            var service = new Moq.Mock<IUserService>();
            var dialog = new Sources.Views.ChangeOwnPasswordDialog(service.Object);

            Fill(dialog, "OldPass1", "NewPass22", "Other333");
            Confirm(dialog);

            service.Verify(s => s.ChangeOwnPassword(Moq.It.IsAny<string>(), Moq.It.IsAny<string>()), Moq.Times.Never);
            Assert.Equal(System.Windows.Visibility.Visible, Field<System.Windows.Controls.TextBlock>(dialog, "ErrorText").Visibility);
            Assert.False(dialog.Succeeded);
        });
    }

    [Fact]
    public void Confirm_WhenServiceRejects_ShowsServiceMessage_AndStaysOpen()
    {
        RunInSta(() =>
        {
            var service = new Moq.Mock<IUserService>();
            service.Setup(s => s.ChangeOwnPassword("WrongPass", "NewPass22")).Returns((false, "كلمة المرور الحالية غير صحيحة"));
            var dialog = new Sources.Views.ChangeOwnPasswordDialog(service.Object);

            Fill(dialog, "WrongPass", "NewPass22", "NewPass22");
            Confirm(dialog);

            service.Verify(s => s.ChangeOwnPassword("WrongPass", "NewPass22"), Moq.Times.Once);
            var error = Field<System.Windows.Controls.TextBlock>(dialog, "ErrorText");
            Assert.Equal("كلمة المرور الحالية غير صحيحة", error.Text);
            Assert.Equal(System.Windows.Visibility.Visible, error.Visibility);
            Assert.False(dialog.Succeeded);
        });
    }
}
