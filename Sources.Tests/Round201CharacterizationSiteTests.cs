using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sources.Data;
using Sources.Helpers;
using Sources.Models;
using Sources.Services;
using Sources.Tests.Fakes;
using Sources.Tests.Fixtures;
using Sources.Tests.Helpers;
using Sources.ViewModels;
using Xunit;

namespace Sources.Tests;

/// <summary>
/// نسخة من SqliteInMemoryFixture لكن بقاعدة بيانات ذاكرة "Cache=Shared" (بدل مشاركة كائن اتصال
/// SqliteConnection واحد حرفياً). هذا الملف تحديداً يقود ViewModels حقيقية (SourcesViewModel/
/// DashboardViewModel/BorrowViewModel/LeakTestsViewModel) التي تستخدم Task.Run داخلياً لتفريغ
/// الاستعلام عن خيط الواجهة — فتح أكثر من DbContext في آنٍ واحد على نفس كائن SqliteConnection
/// المشترَك حرفياً في SqliteInMemoryFixture يفشل بخطأ SQLite "active statements" لأن جميع
/// الـDbContext تتشارك مقبض اتصال ADO.NET واحداً. هنا كل DbContext يفتح اتصاله الخاص (يُغلق تلقائياً
/// من EF) لكنها كلها تشير لنفس قاعدة الذاكرة المسمّاة عبر Cache=Shared، فتبقى البيانات مشتركة بأمان
/// عبر الخيوط. اتصال "مرساة" واحد يبقى مفتوحاً طوال عمر الفكستشر لمنع تفريغ القاعدة بين الاستعلامات.
/// </summary>
public sealed class ConcurrentSqliteFixture : IDisposable
{
    private readonly SqliteConnection _anchorConnection;
    private readonly string _connectionString;
    public IDbContextFactory<AppDbContext> ContextFactory { get; }

    public ConcurrentSqliteFixture()
    {
        _connectionString = $"Data Source=Round201Site_{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
        _anchorConnection = new SqliteConnection(_connectionString);
        _anchorConnection.Open();
        using (var cmd = _anchorConnection.CreateCommand())
        {
            cmd.CommandText = "PRAGMA foreign_keys = ON;";
            cmd.ExecuteNonQuery();
        }

        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connectionString).Options;
        ContextFactory = new SharedCacheDbContextFactory(options);

        using var migrateContext = ContextFactory.CreateDbContext();
        migrateContext.Database.Migrate();
    }

    public AppDbContext CreateContext() => ContextFactory.CreateDbContext();

    public void ResetDatabase()
    {
        using var cmd = _anchorConnection.CreateCommand();
        cmd.CommandText = @"
            PRAGMA foreign_keys = OFF;
            DELETE FROM BorrowRequests;
            DELETE FROM SourceLocationHistories;
            DELETE FROM SourceIsotopes;
            DELETE FROM GammaLines;
            DELETE FROM LeakTestRecords;
            DELETE FROM SourceCertificates;
            DELETE FROM Sources;
            DELETE FROM NeutronSources;
            DELETE FROM NeutronSourceTypes;
            DELETE FROM ActivityUnits;
            DELETE FROM Locations;
            DELETE FROM Radioisotopes;
            DELETE FROM AuditLogs;
            DELETE FROM AlertNotifications;
            DELETE FROM AppSettings;
            DELETE FROM Users;
            DELETE FROM Roles;
            PRAGMA foreign_keys = ON;
        ";
        cmd.ExecuteNonQuery();
    }

    public void Dispose()
    {
        _anchorConnection.Close();
        _anchorConnection.Dispose();
    }

    private sealed class SharedCacheDbContextFactory : IDbContextFactory<AppDbContext>
    {
        private readonly DbContextOptions<AppDbContext> _options;
        public SharedCacheDbContextFactory(DbContextOptions<AppDbContext> options) => _options = options;
        public AppDbContext CreateDbContext() => new AppDbContext(_options);
    }
}

/// <summary>
/// الجولة 201 (R201-A-fix) — يكمل Round201CharacterizationTests.cs بتشغيل الكود الإنتاجي الحقيقي
/// (لا إعادة تنفيذ للمُسنَد داخل الاختبار) لكل موقع طالب به مراجع القائد: مرشِّحات SourcesViewModel
/// وبحثها على نص الحالة، مرشِّح/عدادات/جدول لوحة التحكم، تقارير GeneralReport وتنبيهات انخفاض
/// النشاط، القائمة الحقيقية للمصادر المتاحة للاستعارة وعدادات KPI عبر BorrowViewModel، منطق الإرجاع
/// الحقيقي (EditCommand)، AlertService.GenerateAlerts الحقيقية، وUsersViewModel (ملخصات الأدوار،
/// ظهور قسم الصلاحيات، الصلاحيات "All") وPasswordPromptDialog.ValidateAdminPassword.
///
/// كل ViewModel مُسجَّل مع Messenger يُبنى بنسخة IMessenger مستقلة طازجة (لا WeakReferenceMessenger.Default)
/// فلا يتسرَّب أي تسجيل إلى اختبارات أخرى؛ BorrowViewModel (IDisposable) يُتخلَّص منه صراحة.
/// </summary>
public class Round201CharacterizationSiteTests : IClassFixture<ConcurrentSqliteFixture>, IDisposable
{
    private readonly ConcurrentSqliteFixture _fixture;
    private readonly FakeAuditService _audit = new();
    private readonly FakeLicenseService _license = new();
    private Radioisotope _isotope = null!;
    private Radioisotope _shortHalfLifeIsotope = null!;
    private ActivityUnit _unit = null!;
    private Location _location = null!;
    private User _regularUser = null!;
    private Role _adminRole = null!;
    private Role _userRole = null!;
    private Role _unexpectedRole = null!;

    public Round201CharacterizationSiteTests(ConcurrentSqliteFixture fixture)
    {
        _fixture = fixture;
        _fixture.ResetDatabase();
        Seed();

        // SourcesViewModel.LoadDataAsync يستدعي App.CreateDbContext() الساكنة لجلب ActivityUnits —
        // بنفس نمط SourcesViewModelTests.cs القائم: نضبط App.ServiceProvider بحاوية DI صغيرة تكفي
        // لهذا الاستدعاء فقط، بلا أي تغيير إنتاجي.
        var services = new ServiceCollection();
        services.AddSingleton<IDbContextFactory<AppDbContext>>(_fixture.ContextFactory);
        var sp = services.BuildServiceProvider();
        typeof(Sources.App).GetProperty("ServiceProvider", BindingFlags.Public | BindingFlags.Static)?.SetValue(null, sp);
    }

    public void Dispose() => _fixture.ResetDatabase();

    private void Seed()
    {
        using var db = _fixture.CreateContext();
        _isotope = TestDataBuilder.CreateRadioisotope(symbol: "Co-60", name: "Cobalt-60", halfLife: 5.27, halfLifeUnit: "years");
        _shortHalfLifeIsotope = TestDataBuilder.CreateRadioisotope(symbol: "Tc-99m", name: "Technetium-99m", halfLife: 6, halfLifeUnit: "hours");
        _unit = TestDataBuilder.CreateActivityUnit();
        _location = TestDataBuilder.CreateLocation();

        _adminRole = new Role { Id = Guid.NewGuid(), RoleName = "مدير النظام", Permissions = "All" };
        _userRole = new Role { Id = Guid.NewGuid(), RoleName = "مستخدم", Permissions = "" };
        _unexpectedRole = new Role { Id = Guid.NewGuid(), RoleName = "مشرف قسم", Permissions = "" };

        _regularUser = new User
        {
            Id = Guid.NewGuid(),
            FullName = "مستخدم اختبار مواقع الجولة 201",
            Username = "r201site",
            PasswordHash = PasswordHelper.HashPassword("Passw0rd!"),
            IsActive = true,
            IsEditor = true,
            Permissions = "All",
            RoleId = _adminRole.Id
        };

        db.Roles.AddRange(_adminRole, _userRole, _unexpectedRole);
        db.Users.Add(_regularUser);
        db.Radioisotopes.AddRange(_isotope, _shortHalfLifeIsotope);
        db.ActivityUnits.Add(_unit);
        db.Locations.Add(_location);
        db.SaveChanges();
    }

    // ─── مصانع الخدمات الحقيقية (لا Fakes إلا لما لا بديل حقيقي رخيص له: Audit/License/UserService) ───

    private SourceService CreateSourceService() =>
        new SourceService(_fixture.ContextFactory, new DecayCalculationService(), _audit, new FakeUserService(_regularUser), _license);

    private BorrowService CreateBorrowService() =>
        new BorrowService(_fixture.ContextFactory, _audit, new FakeUserService(_regularUser), _license);

    private RadioisotopeService CreateRadioisotopeService() =>
        new RadioisotopeService(_fixture.ContextFactory, _audit, new FakeUserService(_regularUser), _license);

    private LocationService CreateLocationService() =>
        new LocationService(_fixture.ContextFactory, _audit, new FakeUserService(_regularUser), _license);

    private SystemSettingsService CreateSettingsService() =>
        new SystemSettingsService(_fixture.ContextFactory, _license);

    private LeakTestService CreateLeakTestService() =>
        new LeakTestService(_fixture.ContextFactory, _audit, new FakeUserService(_regularUser), CreateSettingsService(), _license);

    private AlertService CreateAlertService() =>
        new AlertService(_fixture.ContextFactory, new DecayCalculationService(), CreateSettingsService(), _license);

    private Source CreateAndSaveSource(string sourceCode, string status, Radioisotope? isotope = null, DateTime? calibrationDate = null, bool isSealed = true, bool isDeleted = false)
    {
        using var db = _fixture.CreateContext();
        var src = TestDataBuilder.CreateSource(isotope ?? _isotope, _unit, _location, sourceCode: sourceCode, status: status, calibrationDate: calibrationDate, isSealed: isSealed);
        src.IsDeleted = isDeleted;
        db.Sources.Add(src);
        db.SaveChanges();
        return src;
    }

    // ═══════════════════════════ SourcesViewModel — فلتر الحالة + البحث على نص الحالة ═══════════════════════════

    private SourcesViewModel CreateSourcesViewModel()
    {
        return new SourcesViewModel(
            CreateSourceService(),
            CreateRadioisotopeService(),
            CreateLocationService(),
            new ReportingService(),
            decayService: new DecayCalculationService(),
            neutronSourceService: null,
            neutronSourceTypeService: null,
            neutronDecayService: new NeutronDecayCalculationService(),
            messenger: new WeakReferenceMessenger());
    }

    [Theory]
    [InlineData("InUse", 1)]
    [InlineData("Storage", 1)]
    [InlineData("Waste", 1)]
    [InlineData("Transfer", 1)]
    [InlineData("All", 4)]
    public async System.Threading.Tasks.Task SourcesViewModel_StatusFilter_SourcesTab_ReturnsExpectedCount(string filter, int expectedCount)
    {
        CreateAndSaveSource("SRC-SITE-InUse", "InUse");
        CreateAndSaveSource("SRC-SITE-Storage", "Storage");
        CreateAndSaveSource("SRC-SITE-Waste", "Waste");
        CreateAndSaveSource("SRC-SITE-Transfer", "Transfer");

        var vm = CreateSourcesViewModel();
        await vm.LoadDataCommand.ExecuteAsync(null);
        vm.StatusFilter = filter;
        await vm.SearchCommand.ExecuteAsync(null);

        Assert.Equal(expectedCount, vm.Sources.Count);
    }

    [Fact]
    public async System.Threading.Tasks.Task SourcesViewModel_Search_SourcesTab_MatchesRawStatusSubstring()
    {
        CreateAndSaveSource("SRC-SITE-Waste-1", "Waste");
        CreateAndSaveSource("SRC-SITE-InUse-1", "InUse");

        var vm = CreateSourcesViewModel();
        await vm.LoadDataCommand.ExecuteAsync(null);
        vm.SearchText = "waste";
        await vm.SearchCommand.ExecuteAsync(null);

        Assert.Single(vm.Sources);
        Assert.Equal("SRC-SITE-Waste-1", vm.Sources.Single().SourceCode);
    }

    [Fact]
    public async System.Threading.Tasks.Task SourcesViewModel_Search_DeletedTab_MatchesRawStatusSubstring()
    {
        CreateAndSaveSource("SRC-SITE-DEL-Waste", "Waste", isDeleted: true);
        CreateAndSaveSource("SRC-SITE-DEL-InUse", "InUse", isDeleted: true);

        var vm = CreateSourcesViewModel();
        await vm.SwitchToDeletedSourcesAsync();
        vm.SearchText = "waste";
        await vm.SearchCommand.ExecuteAsync(null);

        Assert.Single(vm.DeletedSources);
        Assert.Equal("SRC-SITE-DEL-Waste", vm.DeletedSources.Single().SourceCode);
    }

    // ═══════════════════════════ DashboardViewModel — فلتر الحالة + عدادات/جدول انخفاض النشاط + ملخص الاستعارة ═══════════════════════════

    private DashboardViewModel CreateDashboardViewModel(IAlertService? alertService = null) =>
        new DashboardViewModel(
            CreateSourceService(),
            CreateRadioisotopeService(),
            CreateLocationService(),
            new DecayCalculationService(),
            CreateBorrowService(),
            CreateSettingsService(),
            alertService: alertService,
            globalSearchService: null,
            neutronSourceService: null);

    [Theory]
    [InlineData("InUse", 1)]
    [InlineData("Storage", 1)]
    [InlineData("Waste", 1)]
    [InlineData("", 3)]
    public async System.Threading.Tasks.Task DashboardViewModel_StatusFilter_ReturnsExpectedCount(string filter, int expectedCount)
    {
        CreateAndSaveSource("SRC-DASH-InUse", "InUse");
        CreateAndSaveSource("SRC-DASH-Storage", "Storage");
        CreateAndSaveSource("SRC-DASH-Waste", "Waste");

        var vm = CreateDashboardViewModel();
        await vm.LoadDataAsync();
        vm.SelectedStatusFilter = filter;

        Assert.Equal(expectedCount, vm.TotalFilteredCount);
    }

    [Fact]
    public async System.Threading.Tasks.Task DashboardViewModel_LowActivityCardAndTable_OnlyCountActiveInventoryStatuses()
    {
        // معايرة قديمة جداً + نصف عمر قصير (ساعات) => تجاوز 6 أضعاف نصف العمر بيقين لمصدر InUse فقط
        var oldCalibration = DateTime.Now.AddDays(-30);
        CreateAndSaveSource("SRC-DASH-LOW-InUse", "InUse", isotope: _shortHalfLifeIsotope, calibrationDate: oldCalibration);
        CreateAndSaveSource("SRC-DASH-LOW-Waste", "Waste", isotope: _shortHalfLifeIsotope, calibrationDate: oldCalibration);

        var vm = CreateDashboardViewModel();
        await vm.LoadDataAsync();

        Assert.True(vm.LowActivityCriticalCount >= 1);
        Assert.Contains(vm.LowActivitySources, r => r.SourceCode == "SRC-DASH-LOW-InUse");
        Assert.DoesNotContain(vm.LowActivitySources, r => r.SourceCode == "SRC-DASH-LOW-Waste");
    }

    [Fact]
    public async System.Threading.Tasks.Task DashboardViewModel_BorrowSummaryCard_UsesRealBorrowServiceCounts()
    {
        var src = CreateAndSaveSource("SRC-DASH-BRW", "Storage");
        using (var db = _fixture.CreateContext())
        {
            db.BorrowRequests.Add(new BorrowRequest { Id = Guid.NewGuid(), SourceId = src.Id, BorrowerName = "س", Purpose = "اختبار", Status = "Delivered", ExpectedReturnDate = DateTime.Today.AddDays(3) });
            db.SaveChanges();
        }

        var vm = CreateDashboardViewModel();
        await vm.LoadDataAsync();

        Assert.Equal(1, vm.BorrowSummary.ActiveCount);
        Assert.Equal(0, vm.BorrowSummary.OverdueCount);
    }

    // ═══════════════════════════ ReportsViewModel — GeneralReport (نشاط + تنبيهات انخفاض النشاط) ═══════════════════════════

    private ReportsViewModel CreateReportsViewModel() =>
        new ReportsViewModel(CreateSourceService(), CreateBorrowService(), new ReportingService(), CreateSettingsService(), dbFactory: _fixture.ContextFactory, neutronSourceService: null);

    [Fact]
    public void ReportsViewModel_GeneralReport_ActivityRows_OnlyActiveInventoryStatuses()
    {
        CreateAndSaveSource("SRC-REP-InUse", "InUse");
        CreateAndSaveSource("SRC-REP-Storage", "Storage");
        CreateAndSaveSource("SRC-REP-Waste", "Waste");
        CreateAndSaveSource("SRC-REP-Transfer", "Transfer");

        var vm = CreateReportsViewModel();
        vm.SelectedReport = "GeneralReport";

        var codes = vm.ActivityData.Select(r => r.Source.SourceCode).ToList();
        Assert.Contains("SRC-REP-InUse", codes);
        Assert.Contains("SRC-REP-Storage", codes);
        Assert.DoesNotContain("SRC-REP-Waste", codes);
        Assert.DoesNotContain("SRC-REP-Transfer", codes);
    }

    [Fact]
    public void ReportsViewModel_GeneralReport_LowActivityAlertRows_OnlyActiveInventoryStatuses()
    {
        var oldCalibration = DateTime.Now.AddDays(-30);
        CreateAndSaveSource("SRC-REP-ALERT-InUse", "InUse", isotope: _shortHalfLifeIsotope, calibrationDate: oldCalibration);
        CreateAndSaveSource("SRC-REP-ALERT-Waste", "Waste", isotope: _shortHalfLifeIsotope, calibrationDate: oldCalibration);

        var vm = CreateReportsViewModel();
        vm.SelectedReport = "GeneralReport";

        var codes = vm.LowActivityAlertData.Select(r => r.Source.SourceCode).ToList();
        Assert.Contains("SRC-REP-ALERT-InUse", codes);
        Assert.DoesNotContain("SRC-REP-ALERT-Waste", codes);
    }

    // ═══════════════════════════ LeakTestsViewModel — القائمة المختومة/النشطة ═══════════════════════════

    private LeakTestsViewModel CreateLeakTestsViewModel() =>
        new LeakTestsViewModel(CreateLeakTestService(), CreateSourceService(), new ReportingService(), new FakeUserService(_regularUser), CreateSettingsService(), messenger: new WeakReferenceMessenger());

    [Fact]
    public async System.Threading.Tasks.Task LeakTestsViewModel_SealedSources_OnlySealedAndActiveInventoryStatuses()
    {
        CreateAndSaveSource("SRC-LEAK-InUse-Sealed", "InUse", isSealed: true);
        CreateAndSaveSource("SRC-LEAK-Waste-Sealed", "Waste", isSealed: true);
        CreateAndSaveSource("SRC-LEAK-InUse-Unsealed", "InUse", isSealed: false);

        var vm = CreateLeakTestsViewModel();
        await vm.InitializeAsync();

        var codes = vm.SealedSources.Select(s => s.SourceCode).ToList();
        Assert.Contains("SRC-LEAK-InUse-Sealed", codes);
        Assert.DoesNotContain("SRC-LEAK-Waste-Sealed", codes);
        Assert.DoesNotContain("SRC-LEAK-InUse-Unsealed", codes);
    }

    // ═══════════════════════════ AlertService.GenerateAlerts — الاستعلام المُترجَم إلى EF ═══════════════════════════

    [Fact]
    public void AlertService_GenerateAlerts_OnlyConsidersActiveInventoryStatuses()
    {
        var oldCalibration = DateTime.Now.AddDays(-30);
        CreateAndSaveSource("SRC-ALERT-InUse", "InUse", isotope: _shortHalfLifeIsotope, calibrationDate: oldCalibration);
        CreateAndSaveSource("SRC-ALERT-Waste", "Waste", isotope: _shortHalfLifeIsotope, calibrationDate: oldCalibration);

        var alerts = CreateAlertService().GenerateAlerts();

        using var db = _fixture.CreateContext();
        var inUseId = db.Sources.Single(s => s.SourceCode == "SRC-ALERT-InUse").Id;
        var wasteId = db.Sources.Single(s => s.SourceCode == "SRC-ALERT-Waste").Id;

        Assert.Contains(alerts, a => a.SourceId == inUseId);
        Assert.DoesNotContain(alerts, a => a.SourceId == wasteId);
    }

    // ═══════════════════════════ BorrowViewModel — قائمة المصادر المتاحة، KPI، إمكانية الإرجاع (Edit) ═══════════════════════════

    private BorrowViewModel CreateBorrowViewModel() =>
        new BorrowViewModel(CreateBorrowService(), CreateSourceService(), new FakeUserService(_regularUser), new ReportingService(), dbFactory: _fixture.ContextFactory, messenger: new WeakReferenceMessenger());

    [Fact]
    public void BorrowViewModel_LoadAvailableSources_OnlyNonDeletedStorageStatus()
    {
        CreateAndSaveSource("SRC-BVM-Storage", "Storage");
        CreateAndSaveSource("SRC-BVM-InUse", "InUse");
        CreateAndSaveSource("SRC-BVM-Storage-Deleted", "Storage", isDeleted: true);

        using var vm = CreateBorrowViewModel();
        vm.LoadAvailableSources();

        var codes = vm.AvailableSources.Select(s => s.SourceCode).ToList();
        Assert.Contains("SRC-BVM-Storage", codes);
        Assert.DoesNotContain("SRC-BVM-InUse", codes);
        Assert.DoesNotContain("SRC-BVM-Storage-Deleted", codes);
    }

    [Fact]
    public async System.Threading.Tasks.Task BorrowViewModel_KpiCounts_ViaRealLoadDataAsync()
    {
        var s1 = CreateAndSaveSource("SRC-BVM-KPI-1", "Storage");
        var s2 = CreateAndSaveSource("SRC-BVM-KPI-2", "Storage");
        using (var db = _fixture.CreateContext())
        {
            db.BorrowRequests.Add(new BorrowRequest { Id = Guid.NewGuid(), SourceId = s1.Id, BorrowerName = "س1", Purpose = "اختبار", Status = "Delivered", ExpectedReturnDate = DateTime.Today.AddDays(3) });
            db.BorrowRequests.Add(new BorrowRequest { Id = Guid.NewGuid(), SourceId = s2.Id, BorrowerName = "س2", Purpose = "اختبار", Status = "Pending", ExpectedReturnDate = DateTime.Today.AddDays(3) });
            db.SaveChanges();
        }

        using var vm = CreateBorrowViewModel();
        await vm.LoadDataCommand.ExecuteAsync(null);

        Assert.Equal(1, vm.ActiveCount);
        Assert.Equal(1, vm.BorrowedCount);
        Assert.Equal(0, vm.OverdueCount);
    }

    [Theory]
    [InlineData("Delivered", true)]
    [InlineData("Approved", true)]
    [InlineData("Overdue", true)]
    [InlineData("Pending", false)]
    [InlineData("Rejected", false)]
    [InlineData("Returned", false)]
    public void BorrowViewModel_Edit_LoadsAvailableBorrowers_OnlyWhenReturnable(string status, bool expectReturnable)
    {
        var src = CreateAndSaveSource($"SRC-BVM-EDIT-{status}", "Storage");
        BorrowRequest req;
        using (var db = _fixture.CreateContext())
        {
            req = new BorrowRequest { Id = Guid.NewGuid(), SourceId = src.Id, BorrowerName = "س", Purpose = "اختبار", Status = status, ExpectedReturnDate = DateTime.Today.AddDays(3) };
            db.BorrowRequests.Add(req);
            db.SaveChanges();
        }

        using var vm = CreateBorrowViewModel();
        vm.EditCommand.Execute(req);

        Assert.Equal(expectReturnable, vm.AvailableBorrowers.Count > 0);
    }

    // ═══════════════════════════ UsersViewModel — ملخصات الأدوار، ظهور قسم الصلاحيات، "All" ═══════════════════════════
    // يستخدم UserService الحقيقي (لا Fake) لأن UsersViewModel.LoadData/Save يعتمدان فعلياً على
    // IUserService.GetAllUsers/CreateUser المدعومتين بقاعدة البيانات، وFakeUserService.GetAllUsers
    // تعيد CurrentUser فقط (لا تصلح لاختبار عدّ/ملخص كل المستخدمين).

    private UserService CreateRealUserService()
    {
        var svc = new UserService(_fixture.ContextFactory, _audit, _license);
        var (success, _) = svc.Login(_regularUser.Username, "Passw0rd!");
        Assert.True(success, "تسجيل الدخول بمستخدم الاختبار يجب أن ينجح");
        return svc;
    }

    private UsersViewModel CreateUsersViewModel(IUserService userService) =>
        new UsersViewModel(userService, new ReportingService(), messenger: new WeakReferenceMessenger());

    [Fact]
    public void UsersViewModel_RoleSummaries_And_AdminUsersCount_ViaRealConstructorLoad()
    {
        using (var db = _fixture.CreateContext())
        {
            db.Users.Add(new User { Id = Guid.NewGuid(), FullName = "مدير ثانٍ", Username = "admin2", PasswordHash = PasswordHelper.HashPassword("x"), IsActive = true, RoleId = _adminRole.Id });
            db.Users.Add(new User { Id = Guid.NewGuid(), FullName = "مستخدم عادي", Username = "user1", PasswordHash = PasswordHelper.HashPassword("x"), IsActive = true, RoleId = _userRole.Id });
            db.SaveChanges();
        }

        var vm = CreateUsersViewModel(CreateRealUserService());

        // regularUser (admin، مسجَّل دخوله) + admin2 = 2 مديرين
        Assert.Equal(2, vm.AdminUsersCount);
        Assert.Contains(vm.RoleSummaries, r => r.Role.RoleName == "مدير النظام" && r.UsersCount == 2);
        Assert.Contains(vm.RoleSummaries, r => r.Role.RoleName == "مستخدم" && r.UsersCount == 1);
    }

    [Fact]
    public void UsersViewModel_IsPermissionsSectionVisible_HiddenForAdminRole_VisibleOtherwise()
    {
        var vm = CreateUsersViewModel(CreateRealUserService());
        vm.Roles.Clear();
        vm.Roles.Add(_adminRole);
        vm.Roles.Add(_userRole);

        vm.EditRoleId = _adminRole.Id;
        Assert.False(vm.IsPermissionsSectionVisible);

        vm.EditRoleId = _userRole.Id;
        Assert.True(vm.IsPermissionsSectionVisible);
    }

    [Fact]
    public void UsersViewModel_Save_NewAdminUser_PacksPermissionsAsAll_ViaRealSaveCommand()
    {
        var vm = CreateUsersViewModel(CreateRealUserService());
        vm.Roles.Clear();
        vm.Roles.Add(_adminRole);

        vm.IsNew = true;
        vm.EditFullName = "مستخدم جديد للاختبار";
        vm.EditUsername = "newadmin201";
        vm.EditRoleId = _adminRole.Id;
        vm.EditPassword = "Passw0rd!";

        vm.SaveCommand.Execute(null);

        using var db = _fixture.CreateContext();
        var created = db.Users.Single(u => u.Username == "newadmin201");
        Assert.Equal("All", created.Permissions);
    }

    // ═══════════════════════════ PasswordPromptDialog.ValidateAdminPassword — التحقق المنطقي العلني ═══════════════════════════

    [Theory]
    [InlineData("مدير النظام", true)]
    [InlineData("مستخدم", false)]
    [InlineData("مشرف قسم", false)]
    public void PasswordPromptDialog_ValidateAdminPassword_ExactCurrentBehavior(string roleName, bool expectedSuccess)
    {
        var user = new User { Role = new Role { RoleName = roleName }, PasswordHash = PasswordHelper.HashPassword("Passw0rd!") };
        var userService = new FakeUserService(user);

        var (success, _) = Sources.Views.PasswordPromptDialog.ValidateAdminPassword(userService, "Passw0rd!");

        Assert.Equal(expectedSuccess, success);
    }
}
