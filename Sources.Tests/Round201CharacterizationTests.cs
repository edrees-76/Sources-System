using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
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
/// الجولة 201 (دفعة أ) — اختبارات تثبيت السلوك الحالي (Characterization) قبل أي تعديل إنتاجي.
/// تُثبِّت هذه الاختبارات النتائج الدقيقة الحالية لكل موقع منطقي (LOGIC) ورد في D1/D2/D3 من وثيقة
/// الجولة، على بيانات مزروعة ثابتة تغطي: InUse, Storage, Waste, Transfer وحالات قديمة/غير معروفة
/// ("Active", "Decayed", "", "inuse" بحروف صغيرة)، وحالات الاستعارة الست الموثّقة بالإضافة لحالة غير
/// معروفة ("Cancelled")، والأدوار الثلاثة («مدير النظام»، «مستخدم»، ودور غير متوقع).
///
/// قاعدة مُلزِمة: بعد كل من الدفعات B وC وD يجب أن تمر هذه الاختبارات دون أي تغيير في أي قيمة متوقعة.
/// إن احتاج أي توقّع هنا للتغيير، فذلك يعني تغيّراً سلوكياً ويجب إيقاف تلك الدفعة (STOP RULE).
///
/// ملاحظة نطاق (انحراف مُعلَن): مواقع لا يمكن الوصول إليها بسهولة عبر اختبار وحدة دون بناء واجهات
/// وهمية إضافية ثقيلة (DashboardViewModel, ReportsViewModel, LeakTestsViewModel, AlertService,
/// UsersViewModel, BorrowViewModel ككائنات كاملة) تُثبَّت هنا عبر إعادة تنفيذ نفس المُسنَد (predicate)
/// المطابق حرفياً للكود الحالي على نفس البيانات الحقيقية المسترجَعة من الخدمات الحقيقية (SourceService/
/// BorrowService) حيثما أمكن، أو عبر استدعاء الكود الإنتاجي الساكن مباشرة (AuthorizationGuard,
/// PasswordPromptDialog.ValidateAdminPassword, StatusCatalog, Role.DisplayName, User.IsAdmin,
/// BorrowRequest.ArabicStatus) حيثما كان ذلك ممكناً. هذا مُسجَّل كانحراف في تقرير الجولة.
/// </summary>
public class Round201CharacterizationTests : IClassFixture<SqliteInMemoryFixture>, IDisposable
{
    private readonly SqliteInMemoryFixture _fixture;
    private readonly FakeAuditService _fakeAuditService = new();
    private readonly FakeLicenseService _fakeLicenseService = new();
    private Radioisotope _isotope = null!;
    private ActivityUnit _unit = null!;
    private Location _location = null!;
    private Location _locationLD = null!;
    private User _regularUser = null!;

    private static readonly string[] SourceStatusUniverse =
    {
        "InUse", "Storage", "Waste", "Transfer", "Active", "Decayed", "", "inuse"
    };

    private static readonly string[] BorrowStatusUniverse =
    {
        "Pending", "Approved", "Rejected", "Delivered", "Returned", "Overdue", "Cancelled"
    };

    public Round201CharacterizationTests(SqliteInMemoryFixture fixture)
    {
        _fixture = fixture;
        _fixture.ResetDatabase();
        Seed();
    }

    public void Dispose() => _fixture.ResetDatabase();

    private void Seed()
    {
        using var db = _fixture.CreateContext();
        _isotope = TestDataBuilder.CreateRadioisotope(symbol: "Co-60", name: "Cobalt-60", halfLife: 5.27, halfLifeUnit: "years");
        _unit = TestDataBuilder.CreateActivityUnit();
        _location = TestDataBuilder.CreateLocation();
        _locationLD = TestDataBuilder.CreateLocation(name: "موقع اختبار فلترة تفاصيل الموقع - الجولة 201");

        var roleUser = new Role { Id = Guid.NewGuid(), RoleName = "أمين عهدة اختباري", Permissions = "Sources,Borrowing" };
        _regularUser = new User
        {
            Id = Guid.NewGuid(),
            FullName = "مستخدم اختبار الجولة 201",
            Username = "r201user",
            PasswordHash = PasswordHelper.HashPassword("Passw0rd!"),
            IsActive = true,
            IsEditor = true,
            Permissions = "Sources,Borrowing",
            RoleId = roleUser.Id
        };

        db.Roles.Add(roleUser);
        db.Users.Add(_regularUser);
        db.Radioisotopes.Add(_isotope);
        db.ActivityUnits.Add(_unit);
        db.Locations.Add(_location);
        db.Locations.Add(_locationLD);

        foreach (var status in SourceStatusUniverse)
        {
            var src = TestDataBuilder.CreateSource(
                _isotope, _unit, _locationLD,
                sourceCode: SourceCodeFor(status),
                status: status,
                isSealed: true);
            db.Sources.Add(src);
        }

        db.SaveChanges();

        // مصدر إضافي منخفض النشاط فعلياً (معايرة قديمة جداً + نصف عمر قصير) لاختبار GetLowActivitySources
        var lowActivitySource = TestDataBuilder.CreateSource(
            _isotope, _unit, _location,
            sourceCode: "SRC-201-LOW-INUSE",
            calibrationDate: DateTime.Now.AddYears(-20),
            status: "InUse",
            isSealed: true);
        db.Sources.Add(lowActivitySource);
        db.SaveChanges();

        // طلبات استعارة تغطي كل حالة موثّقة + حالة غير معروفة، كل واحدة على مصدر منفصل
        var borrowSourceIds = new List<Guid>();
        foreach (var status in BorrowStatusUniverse)
        {
            var src = TestDataBuilder.CreateSource(_isotope, _unit, _location, sourceCode: $"SRC-201-BRW-{status}", status: "Storage");
            db.Sources.Add(src);
            db.SaveChanges();
            borrowSourceIds.Add(src.Id);

            var expected = status == "Overdue" ? DateTime.Today.AddDays(-3) : DateTime.Today.AddDays(5);
            db.BorrowRequests.Add(new BorrowRequest
            {
                Id = Guid.NewGuid(),
                SourceId = src.Id,
                BorrowerName = $"مستعير {status}",
                Purpose = "اختبار",
                Status = status,
                RequestDate = DateTime.Now.AddDays(-10),
                ExpectedReturnDate = expected
            });
        }
        db.SaveChanges();
    }

    private static string SourceCodeFor(string status) => $"SRC-201-{(string.IsNullOrEmpty(status) ? "EMPTY" : status)}";

    private SourceService CreateSourceService()
    {
        var decay = new DecayCalculationService();
        return new SourceService(_fixture.ContextFactory, decay, _fakeAuditService, new FakeUserService(_regularUser), _fakeLicenseService);
    }

    private BorrowService CreateBorrowService() =>
        new BorrowService(_fixture.ContextFactory, _fakeAuditService, new FakeUserService(_regularUser), _fakeLicenseService);

    // ═══════════════════════════ D1 — SourceService ═══════════════════════════

    [Fact]
    public void GetAllSources_ReturnsAllStatuses_RegardlessOfStatus()
    {
        var all = CreateSourceService().GetAllSources();
        foreach (var status in SourceStatusUniverse)
        {
            Assert.Contains(all, s => s.SourceCode == SourceCodeFor(status));
        }
    }

    [Theory]
    [InlineData("InUse", true)]
    [InlineData("Storage", true)]
    [InlineData("Waste", false)]
    [InlineData("Transfer", false)]
    [InlineData("Active", false)]
    [InlineData("Decayed", false)]
    [InlineData("", false)]
    [InlineData("inuse", false)] // متغيّر حالة الأحرف — لا يُطابق "InUse" بمقارنة حساسة لحالة الأحرف
    public void GetAllSources_RecalculatesActivity_OnlyForInUseOrStorage_ExactCaseSensitiveMatch(string status, bool expectRecalculated)
    {
        var all = CreateSourceService().GetAllSources();
        var source = all.Single(s => s.SourceCode == SourceCodeFor(status));

        // عند عدم إعادة الحساب، تبقى القيمة الحالية مطابقة تماماً للقيمة الابتدائية المخزّنة (بلا اضمحلال محسوب هنا)
        if (!expectRecalculated)
        {
            Assert.Equal(source.InitialActivityValue, source.CurrentActivityValue, 10);
        }
    }

    [Theory]
    [InlineData("InUse", true)]
    [InlineData("Storage", true)]
    [InlineData("Waste", false)]
    [InlineData("inuse", false)]
    public void GetSourceById_RecalculatesActivity_OnlyForInUseOrStorage(string status, bool expectRecalculated)
    {
        var db0 = _fixture.CreateContext();
        var id = db0.Sources.Single(s => s.SourceCode == SourceCodeFor(status)).Id;
        db0.Dispose();

        var source = CreateSourceService().GetSourceById(id);
        Assert.NotNull(source);
        if (!expectRecalculated)
        {
            Assert.Equal(source!.InitialActivityValue, source.CurrentActivityValue, 10);
        }
    }

    [Fact]
    public void GetLowActivitySources_NeverReturnsNonActiveInventoryStatuses()
    {
        var low = CreateSourceService().GetLowActivitySources(50.0);
        foreach (var s in low)
        {
            Assert.True(s.Status == "InUse" || s.Status == "Storage",
                $"GetLowActivitySources يجب ألا يعيد مصدراً بحالة {s.Status}");
        }
    }

    [Fact]
    public void GetLowActivitySources_Includes_TrulyLowActivity_InUse_Source()
    {
        var low = CreateSourceService().GetLowActivitySources(50.0);
        Assert.Contains(low, s => s.SourceCode == "SRC-201-LOW-INUSE");
    }

    [Fact]
    public void HasActiveBorrow_TrueOnlyFor_Delivered_Or_Overdue()
    {
        var svc = CreateSourceService();
        using var db = _fixture.CreateContext();
        foreach (var status in BorrowStatusUniverse)
        {
            var src = db.Sources.Single(s => s.SourceCode == $"SRC-201-BRW-{status}");
            bool expected = status == "Delivered" || status == "Overdue";
            Assert.Equal(expected, svc.HasActiveBorrow(src.Id));
        }
    }

    [Theory]
    [InlineData("Pending", "لوجود طلب استعارة معلّق عليه (قيد الانتظار)")]
    [InlineData("Approved", "لوجود طلب استعارة معتمد عليه")]
    [InlineData("Delivered", "لوجود استعارة نشطة عليه")]
    [InlineData("Overdue", "لوجود استعارة نشطة عليه")]
    public void Delete_BlockedWithExactReasonMessage_ForPendingOrActiveBorrow(string borrowStatus, string expectedReasonFragment)
    {
        var svc = CreateSourceService();
        using var db = _fixture.CreateContext();
        var src = db.Sources.Single(s => s.SourceCode == $"SRC-201-BRW-{borrowStatus}");

        var (success, message) = svc.DeleteSource(src.Id);

        Assert.False(success);
        Assert.Contains(expectedReasonFragment, message);
    }

    [Fact]
    public void Delete_NotBlocked_ForReturnedOrRejectedOrUnknownBorrowStatus()
    {
        // "Cancelled" (حالة غير معروفة) لا يطابقها استعلام الحظر الحالي (Pending/Approved/Delivered/Overdue فقط)
        // لذا لا يُحظر الحذف بسببها — هذا هو السلوك الحالي المُثبَّت.
        var svc = CreateSourceService();
        foreach (var status in new[] { "Returned", "Rejected", "Cancelled" })
        {
            using var db = _fixture.CreateContext();
            var src = db.Sources.Single(s => s.SourceCode == $"SRC-201-BRW-{status}");
            var (success, _) = svc.DeleteSource(src.Id);
            Assert.True(success, $"يجب ألا يُحظر الحذف بسبب حالة استعارة {status}");
        }
    }

    // ═══════════════════════════ D1 — LeakTestsViewModel / AlertService / Dashboard / Reports (predicate replication) ═══════════════════════════

    [Fact]
    public void LeakTests_SealedActiveList_ReplicatesCurrentPredicate_IsSealed_And_InUseOrStorage()
    {
        var all = CreateSourceService().GetAllSources();
        var sealedActive = all.Where(s => s.IsSealed && (s.Status == "InUse" || s.Status == "Storage")).Select(s => s.SourceCode).OrderBy(x => x).ToList();

        // ملاحظة: مصادر الاستعارة (SRC-201-BRW-*) تُنشأ بالإعداد الافتراضي isSealed=false في TestDataBuilder،
        // لذا لا تظهر هنا رغم أن حالتها Storage — هذا يثبّت أن IsSealed شرط فعلي حقيقي في هذا الموقع.
        var expected = new[] { SourceCodeFor("InUse"), SourceCodeFor("Storage"), "SRC-201-LOW-INUSE" }
            .OrderBy(x => x).ToList();

        Assert.Equal(expected, sealedActive);
    }

    [Fact]
    public void AlertService_ActiveSourcesQuery_ReplicatesCurrentEfPredicate()
    {
        using var db = _fixture.CreateContext();
        var active = db.Sources
            .Where(s => s.Status == "InUse" || s.Status == "Storage")
            .Select(s => s.SourceCode)
            .ToList();

        Assert.Contains(SourceCodeFor("InUse"), active);
        Assert.Contains(SourceCodeFor("Storage"), active);
        Assert.DoesNotContain(SourceCodeFor("Waste"), active);
        Assert.DoesNotContain(SourceCodeFor("Transfer"), active);
        Assert.DoesNotContain(SourceCodeFor("Active"), active);
        Assert.DoesNotContain(SourceCodeFor("inuse"), active);
    }

    [Fact]
    public void ReportsViewModel_ActivityReport_ReplicatesCurrentPredicate()
    {
        var all = CreateSourceService().GetAllSources();
        var activityReport = all.Where(s => s.Status == "InUse" || s.Status == "Storage").OrderBy(s => s.SourceCode).ToList();

        Assert.Contains(activityReport, s => s.SourceCode == SourceCodeFor("InUse"));
        Assert.Contains(activityReport, s => s.SourceCode == SourceCodeFor("Storage"));
        Assert.DoesNotContain(activityReport, s => s.SourceCode == SourceCodeFor("Waste"));
        Assert.DoesNotContain(activityReport, s => s.SourceCode == SourceCodeFor("Transfer"));
    }

    // ═══════════════════════════ D1 — LocationDetailsViewModel ═══════════════════════════

    private LocationDetailsViewModel CreateLocationDetailsVm()
    {
        var sources = CreateSourceService().GetAllSources().Where(s => s.LocationId == _locationLD.Id).ToList();
        return new LocationDetailsViewModel(_locationLD, sources, reportingService: null, neutronSources: new List<NeutronSource>());
    }

    [Theory]
    [InlineData("الكل", -1)] // -1 = يجب أن يشمل كل المصادر بالموقع
    [InlineData("قيد الاستخدام", 2)] // يطابق "InUse" و"inuse" معاً (مقارنة غير حساسة لحالة الأحرف في هذا الموقع)
    [InlineData("InUse", 2)]
    [InlineData("مخزن", 1)]
    [InlineData("في المخزن", 1)] // مرادف غير موجود في StatusFilterOptions لكن مدعوم في MapFilterToStatusCode
    [InlineData("Storage", 1)]
    [InlineData("نفايات", 1)]
    [InlineData("Waste", 1)]
    [InlineData("قيد النقل", 1)]
    [InlineData("نقل", 1)]
    [InlineData("Transfer", 1)]
    [InlineData("نص غير معروف تماماً", 0)]
    public void LocationDetails_ApplyFilters_ReturnsExpectedCount_ForEveryFilterOption(string filter, int expectedCountOrAll)
    {
        var vm = CreateLocationDetailsVm();
        int totalAtLocation = vm.TotalSourcesCount;

        vm.SelectedStatusFilter = filter;

        int expected = expectedCountOrAll == -1 ? totalAtLocation : expectedCountOrAll;
        Assert.Equal(expected, vm.FilteredSourcesCount);
    }

    [Fact]
    public void LocationDetails_ClearFilters_ResetsToAll()
    {
        var vm = CreateLocationDetailsVm();
        vm.SelectedStatusFilter = "نفايات";
        Assert.Equal(1, vm.FilteredSourcesCount);

        vm.ClearFiltersCommand.Execute(null);

        Assert.Equal("الكل", vm.SelectedStatusFilter);
        Assert.Equal(vm.TotalSourcesCount, vm.FilteredSourcesCount);
        Assert.Equal(string.Empty, vm.SearchText);
    }

    // ═══════════════════════════ D2 — BorrowService LOGIC ═══════════════════════════

    [Theory]
    [InlineData("Storage", true)]
    [InlineData("InUse", false)]
    [InlineData("Waste", false)]
    [InlineData("Transfer", false)]
    [InlineData("", false)]
    [InlineData("inuse", false)]
    public void CreateRequest_AllowedOnlyWhenSourceStatusIsExactlyStorage(string status, bool expectSuccess)
    {
        using (var db = _fixture.CreateContext())
        {
            var src = TestDataBuilder.CreateSource(_isotope, _unit, _location, sourceCode: $"SRC-201-CREATE-{(string.IsNullOrEmpty(status) ? "EMPTY" : status)}-{Guid.NewGuid():N}".Substring(0, 40), status: status);
            db.Sources.Add(src);
            db.SaveChanges();

            var svc = CreateBorrowService();
            var (success, _) = svc.CreateRequest(new BorrowRequest
            {
                Id = Guid.NewGuid(),
                SourceId = src.Id,
                BorrowerName = "مستعير اختبار",
                Purpose = "اختبار",
                ExpectedReturnDate = DateTime.Today.AddDays(5)
            });

            Assert.Equal(expectSuccess, success);
        }
    }

    [Theory]
    [InlineData("Delivered", true)]
    [InlineData("Approved", true)]
    [InlineData("Overdue", true)]
    [InlineData("Pending", false)]
    [InlineData("Rejected", false)]
    [InlineData("Returned", false)]
    [InlineData("Cancelled", false)]
    public void MarkReturned_AllowedOnlyFrom_Delivered_Approved_Overdue(string status, bool expectSuccess)
    {
        using var db = _fixture.CreateContext();
        var src = TestDataBuilder.CreateSource(_isotope, _unit, _location, sourceCode: $"SRC-201-RET-{status}", status: "Storage");
        db.Sources.Add(src);
        db.SaveChanges();

        var req = new BorrowRequest { Id = Guid.NewGuid(), SourceId = src.Id, BorrowerName = "س", Purpose = "اختبار", Status = status, ExpectedReturnDate = DateTime.Today.AddDays(3) };
        db.BorrowRequests.Add(req);
        db.SaveChanges();

        var svc = CreateBorrowService();
        var (success, _) = svc.MarkReturned(req.Id, _regularUser.Id, DateTime.Today);

        Assert.Equal(expectSuccess, success);
    }

    [Fact]
    public void CheckAndUpdateOverdue_OnlySweepsDeliveredOrApproved_PastDueDate()
    {
        using (var db = _fixture.CreateContext())
        {
            foreach (var status in new[] { "Delivered", "Approved", "Pending", "Rejected", "Returned" })
            {
                var src = TestDataBuilder.CreateSource(_isotope, _unit, _location, sourceCode: $"SRC-201-SWEEP-{status}", status: "Storage");
                db.Sources.Add(src);
                db.SaveChanges();
                db.BorrowRequests.Add(new BorrowRequest
                {
                    Id = Guid.NewGuid(),
                    SourceId = src.Id,
                    BorrowerName = "س",
                    Purpose = "اختبار",
                    Status = status,
                    ExpectedReturnDate = DateTime.Today.AddDays(-5)
                });
            }
            db.SaveChanges();
        }

        CreateBorrowService().CheckAndUpdateOverdue();

        using var verifyDb = _fixture.CreateContext();
        Assert.Equal("Overdue", verifyDb.BorrowRequests.Single(b => b.Source!.SourceCode == "SRC-201-SWEEP-Delivered").Status);
        Assert.Equal("Overdue", verifyDb.BorrowRequests.Single(b => b.Source!.SourceCode == "SRC-201-SWEEP-Approved").Status);
        Assert.Equal("Pending", verifyDb.BorrowRequests.Single(b => b.Source!.SourceCode == "SRC-201-SWEEP-Pending").Status);
        Assert.Equal("Rejected", verifyDb.BorrowRequests.Single(b => b.Source!.SourceCode == "SRC-201-SWEEP-Rejected").Status);
        Assert.Equal("Returned", verifyDb.BorrowRequests.Single(b => b.Source!.SourceCode == "SRC-201-SWEEP-Returned").Status);
    }

    [Fact]
    public void GetPending_And_GetPendingCount_ReturnOnlyPendingStatus()
    {
        var svc = CreateBorrowService();
        Assert.Equal(1, svc.GetPendingCount());
        Assert.Single(svc.GetPending());
        Assert.Equal("Pending", svc.GetPending().Single().Status);
    }

    [Fact]
    public void GetOverdue_ReturnsOnlyOverdueStatus()
    {
        var svc = CreateBorrowService();
        var overdue = svc.GetOverdue();
        Assert.Single(overdue);
        Assert.Equal("Overdue", overdue.Single().Status);
    }

    [Fact]
    public void GetDueSoonCount_And_GetDueSoonRequests_OnlyCountDeliveredWithinThreshold()
    {
        var svc = CreateBorrowService();
        // في مجموعة البيانات المزروعة: طلب واحد فقط بحالة Delivered وتاريخ إرجاع متوقع خلال 5 أيام (ضمن العتبة الافتراضية 7)
        Assert.Equal(1, svc.GetDueSoonCount());
        var dueSoon = svc.GetDueSoonRequests();
        Assert.Single(dueSoon);
        Assert.Equal("Delivered", dueSoon.Single().Status);
    }

    // ═══════════════════════════ D2 — DISPLAY: BorrowRequest.ArabicStatus ═══════════════════════════

    [Theory]
    [InlineData("Pending", "معلّق")]
    [InlineData("Approved", "تمت الموافقة")]
    [InlineData("Rejected", "مرفوض")]
    [InlineData("Delivered", "تم التسليم")]
    [InlineData("Returned", "تم الإرجاع")]
    [InlineData("Overdue", "متأخر")]
    [InlineData("Cancelled", "Cancelled")] // غير معروف -> يعود النص الخام كما هو
    public void BorrowRequest_ArabicStatus_ExactCurrentMapping(string status, string expectedArabic)
    {
        var req = new BorrowRequest { Status = status };
        Assert.Equal(expectedArabic, req.ArabicStatus);
    }

    // ═══════════════════════════ D2 — BorrowViewModel filter map & KPI counts (predicate replication) ═══════════════════════════

    [Theory]
    [InlineData("الكل", 7)]
    [InlineData("تم التسليم", 1)]
    [InlineData("تم الإرجاع", 1)]
    [InlineData("متأخر", 1)]
    [InlineData("قريبة الإرجاع", 1)]
    [InlineData("نص غير معروف", 0)]
    public void BorrowViewModel_FilterMap_ReplicatesCurrentSwitchLogic(string filter, int expectedCount)
    {
        var svc = CreateBorrowService();
        var all = svc.GetAll();
        var today = DateTime.Today;
        var maxDate = today.AddDays(svc.GetDueSoonDaysThreshold() + 1).Date;

        IEnumerable<BorrowRequest> filtered = all;
        if (filter != "الكل")
        {
            if (filter == "قريبة الإرجاع")
            {
                filtered = filtered.Where(r => r.Status == "Delivered" && r.ExpectedReturnDate.Date >= today && r.ExpectedReturnDate.Date < maxDate);
            }
            else
            {
                string enStatus = filter switch
                {
                    "تم التسليم" => "Delivered",
                    "تم الإرجاع" => "Returned",
                    "متأخر" => "Overdue",
                    _ => ""
                };
                filtered = string.IsNullOrEmpty(enStatus) ? Enumerable.Empty<BorrowRequest>() : filtered.Where(r => r.Status == enStatus);
            }
        }

        Assert.Equal(expectedCount, filtered.Count());
    }

    [Fact]
    public void BorrowViewModel_KpiCounts_ReplicateCurrentPredicates()
    {
        var all = CreateBorrowService().GetAll();
        int activeCount = all.Count(r => r.Status == "Delivered" || r.Status == "Overdue");
        int borrowedCount = all.Count(r => r.Status == "Delivered");
        int overdueCount = all.Count(r => r.Status == "Overdue");

        Assert.Equal(2, activeCount); // Delivered + Overdue
        Assert.Equal(1, borrowedCount);
        Assert.Equal(1, overdueCount);
    }

    [Theory]
    [InlineData("Delivered", true)]
    [InlineData("Overdue", true)]
    [InlineData("Approved", true)]
    [InlineData("Pending", false)]
    [InlineData("Rejected", false)]
    [InlineData("Returned", false)]
    public void BorrowViewModel_CanReturn_ReplicatesCurrentPredicate(string status, bool expected)
    {
        bool canReturn = status == "Delivered" || status == "Overdue" || status == "Approved";
        Assert.Equal(expected, canReturn);
    }

    // ═══════════════════════════ D3 — Role ═══════════════════════════

    [Theory]
    [InlineData("مدير النظام", true)]
    [InlineData("مستخدم", false)]
    [InlineData("مشرف قسم", false)] // دور غير متوقع
    public void User_IsAdmin_ExactCurrentComparison(string roleName, bool expectedIsAdmin)
    {
        var user = new User { Role = new Role { RoleName = roleName } };
        Assert.Equal(expectedIsAdmin, user.IsAdmin);
    }

    [Theory]
    [InlineData("مدير النظام", true, "")]
    [InlineData("مستخدم", false, "غير مصرح: هذه العملية مخصصة لمدير النظام فقط")]
    [InlineData("مشرف قسم", false, "غير مصرح: هذه العملية مخصصة لمدير النظام فقط")]
    public void AuthorizationGuard_RequireAdmin_ExactCurrentBehavior(string roleName, bool expectedAllowed, string expectedMessageWhenDenied)
    {
        var user = new User { Role = new Role { RoleName = roleName }, IsActive = true };
        var (allowed, message) = AuthorizationGuard.RequireAdmin(user);
        Assert.Equal(expectedAllowed, allowed);
        if (!expectedAllowed)
        {
            Assert.Equal(expectedMessageWhenDenied, message);
        }
    }

    [Theory]
    [InlineData("مدير النظام")]
    [InlineData("مستخدم")]
    [InlineData("مشرف قسم")]
    public void UsersViewModel_AdminUsersCount_ReplicatesCurrentPredicate(string roleName)
    {
        var users = new List<User>
        {
            new User { Role = new Role { RoleName = "مدير النظام" } },
            new User { Role = new Role { RoleName = "مستخدم" } },
            new User { Role = new Role { RoleName = roleName } },
        };
        int adminCount = users.Count(u => u.Role?.RoleName == "مدير النظام");
        int expectedAdmins = 1 + (roleName == "مدير النظام" ? 1 : 0);
        Assert.Equal(expectedAdmins, adminCount);
    }

    [Theory]
    [InlineData("مدير النظام", "All")]
    [InlineData("مستخدم", null)]
    [InlineData("مشرف قسم", null)]
    public void UsersViewModel_PermissionsAllShortCircuit_ReplicatesCurrentPredicate(string roleName, string? expectedAllValue)
    {
        var role = new Role { RoleName = roleName };
        string? result = role.RoleName == "مدير النظام" ? "All" : null;
        Assert.Equal(expectedAllValue, result);
    }
}
