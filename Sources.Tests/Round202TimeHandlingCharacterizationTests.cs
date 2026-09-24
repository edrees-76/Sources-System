using System;
using System.Linq;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Sources.Data;
using Sources.Helpers;
using Sources.Models;
using Sources.Services;
using Sources.Tests.Fakes;
using Sources.Tests.Fixtures;
using Sources.Tests.Helpers;
using Xunit;

namespace Sources.Tests;

/// <summary>
/// الجولة 202 — اختبارات تثبيت السلوك (Characterization Tests) لمواقع القراءة الزمنية التي
/// وُجّهت عبر TimeProvider في R202-A. تستدعي كود الإنتاج الفعلي عبر FakeTimeProvider بمنطقة
/// زمنية ثابتة (Libya-Test = UTC+2 بلا توقيت صيفي)، وتقارَن بقيم حرفية محسوبة بشكل مستقل
/// (الصيغة موضّحة في التعليق أعلى كل حالة)، وليس بإعادة تنفيذ منطق الإنتاج وقت التشغيل.
/// وفق "CORE RULE": إن اختلفت أي قيمة متوقعة محسوبة بشكل مستقل عن ناتج الإنتاج، يجب التوقف
/// والإبلاغ بالأرقام الدقيقة بدلاً من تعديل التوقع بصمت.
/// </summary>
public class Round202TimeHandlingCharacterizationTests : IClassFixture<SqliteInMemoryFixture>, IDisposable
{
    private readonly SqliteInMemoryFixture _fixture;

    // منطقة زمنية ثابتة صناعية (لا تعتمد على منطقة جهاز التشغيل؛ CI يعمل بتوقيت UTC)
    private static readonly TimeZoneInfo LibyaTestZone =
        TimeZoneInfo.CreateCustomTimeZone("Libya-Test", TimeSpan.FromHours(2), "Libya-Test", "Libya-Test");

    public Round202TimeHandlingCharacterizationTests(SqliteInMemoryFixture fixture)
    {
        _fixture = fixture;
        _fixture.ResetDatabase();
    }

    public void Dispose() => _fixture.ResetDatabase();

    private static FakeTimeProvider CreateFakeClock(DateTime localDateTime)
    {
        var fake = new FakeTimeProvider();
        fake.SetLocalTimeZone(LibyaTestZone);
        // SetUtcNow يتطلب DateTimeOffset حقيقياً بصفر إزاحة (UTC)؛ تمرير إزاحة غير صفرية
        // يُضاعِف التحويل لاحقاً عند GetLocalNow (تأكدنا تجريبياً). لذلك نطرح ساعتي المنطقة
        // يدوياً هنا للحصول على اللحظة العالمية الصحيحة المقابلة لـ localDateTime بتوقيت UTC+2.
        var utcEquivalent = new DateTimeOffset(localDateTime.AddHours(-2), TimeSpan.Zero);
        fake.SetUtcNow(utcEquivalent);
        return fake;
    }

    // الأوقات الثابتة المطلوبة تعاقدياً: 23:30 محلي، 00:30 اليوم التالي محلي، وحد نهاية شهر
    public static readonly DateTime FixedNear2330Local = new(2026, 9, 24, 23, 30, 0);
    public static readonly DateTime FixedNear0030LocalNextDay = new(2026, 9, 25, 0, 30, 0);
    public static readonly DateTime FixedMonthBoundaryBefore = new(2026, 9, 30, 23, 30, 0);
    public static readonly DateTime FixedMonthBoundaryAfter = new(2026, 10, 1, 0, 30, 0);

    // ══════════════════════════ 1. الاضمحلال بدقة الدقيقة (SRC-0106 Tc-99m و SRC-0029 F-18) ══════════════════════════

    [Fact]
    public void Decay_Tc99mLikeSource_MinuteResolutionAgainstLocalNow_MatchesIndependentFormula()
    {
        // SRC-0106: 98.2 mCi، T½ = 6.01 ساعة، معايرة 2026-09-14 20:46:36.7276544 محلي.
        // اللحظة: 2026-09-24 20:53:00 محلي (نفس لقطة الشاشة المرجعية في عقد الجولة).
        // A = A0 * 0.5^(elapsedSeconds / halfLifeSeconds) — محسوبة يدوياً بدقة tick (100ns):
        // elapsedSeconds = 864383.2723456 s ، halfLifeSeconds = 6.01*3600 = 21636 s
        // => 98.2 * 0.5^(864383.2723456/21636) ≈ 9.238773496698382E-011 mCi
        const double expected = 9.238773496698382e-11;

        var calibDate = new DateTime(2026, 9, 14, 20, 46, 36).AddTicks(7276544);
        var fake = CreateFakeClock(new DateTime(2026, 9, 24, 20, 53, 0));
        var sut = new DecayCalculationService(fake);

        var actual = sut.CalculateCurrentActivity(98.2, 6.01, "hours", calibDate);

        AssertRelativelyClose(expected, actual);
    }

    [Fact]
    public void Decay_F18LikeSource_MinuteResolutionAgainstLocalNow_MatchesIndependentFormula()
    {
        // SRC-0029 (صف F-18 المنفرد): 82.1 mCi، T½ = 109.7 دقيقة، معايرة 2026-09-14 19:12:18.7596567 محلي.
        // نفس اللحظة: 2026-09-24 20:53:00 محلي.
        // elapsedSeconds = 870041.2403433 s ، halfLifeSeconds = 109.7*60 = 6582 s
        // => 82.1 * 0.5^(870041.2403433/6582) ≈ 1.3265170327085314E-038 mCi
        const double expected = 1.3265170327085314e-38;

        var calibDate = new DateTime(2026, 9, 14, 19, 12, 18).AddTicks(7596567);
        var fake = CreateFakeClock(new DateTime(2026, 9, 24, 20, 53, 0));
        var sut = new DecayCalculationService(fake);

        var actual = sut.CalculateCurrentActivity(82.1, 109.7, "minutes", calibDate);

        AssertRelativelyClose(expected, actual);
    }

    [Theory]
    [MemberData(nameof(FixedClocks))]
    public void Decay_LongLivedNuclide_Cs137_AtFixedClocks_MatchesExponentialFormula(DateTime fixedLocalNow)
    {
        // Cs-137: T½ = 30.08 سنة (365.25*86400*30.08 ثانية)، معايرة قبل 5 سنوات بالضبط من كل ساعة مضبوطة
        const double initialBq = 3.7e7;
        var calibDate = fixedLocalNow.AddYears(-5);
        var fake = CreateFakeClock(fixedLocalNow);
        var sut = new DecayCalculationService(fake);

        var halfLifeSeconds = 30.08 * 365.25 * 86400.0;
        var elapsedSeconds = (fixedLocalNow - calibDate).TotalSeconds;
        var expected = initialBq * Math.Pow(0.5, elapsedSeconds / halfLifeSeconds);

        var actual = sut.CalculateCurrentActivity(initialBq, 30.08, "years", calibDate);

        AssertRelativelyClose(expected, actual);
    }

    public static TheoryData<DateTime> FixedClocks => new()
    {
        FixedNear2330Local,
        FixedNear0030LocalNextDay,
        FixedMonthBoundaryBefore,
        FixedMonthBoundaryAfter
    };

    // ══════════════════════════ 2. اضمحلال الانبعاث النيتروني ══════════════════════════

    [Theory]
    [MemberData(nameof(FixedClocks))]
    public void NeutronDecay_CalibratedThreeYearsAgo_AtFixedClocks_MatchesExponentialFormula(DateTime fixedLocalNow)
    {
        // Am-241/Be، T½ = 432.2 سنة، معدل انبعاث معاير = 1.0e7 n/s، معايرة قبل 3 سنوات بالضبط
        const double calibratedRate = 1.0e7;
        const double halfLifeYears = 432.2;
        var calibDate = fixedLocalNow.AddYears(-3);
        var fake = CreateFakeClock(fixedLocalNow);
        var sut = new NeutronDecayCalculationService(fake);

        var source = new NeutronSource
        {
            CalibratedEmissionRate = calibratedRate,
            EmissionCalibrationDate = calibDate,
            NeutronSourceType = new NeutronSourceType
            {
                Code = "AmBe-TEST",
                HalfLife = halfLifeYears,
                HalfLifeUnit = "years"
            }
        };

        var halfLifeSeconds = halfLifeYears * NeutronDecayCalculationService.SecondsPerYear;
        var elapsedSeconds = (fixedLocalNow - calibDate).TotalSeconds;
        var expected = calibratedRate * Math.Exp(-Math.Log(2.0) * (elapsedSeconds / halfLifeSeconds));

        var result = sut.CalculateCurrentEmissionRate(source);

        Assert.True(result.IsCalculated);
        Assert.NotNull(result.CurrentEmissionRate);
        AssertRelativelyClose(expected, result.CurrentEmissionRate!.Value);
    }

    // ══════════════════════════ 3. تنبيهات انخفاض النشاط (عدد أنصاف الأعمار المنقضية) ══════════════════════════

    [Theory]
    [MemberData(nameof(FixedClocks))]
    public void AlertService_MaxHalfLivesElapsed_AtFixedClocks_MatchesIndependentFormula(DateTime fixedLocalNow)
    {
        // مصدر Co-60 معاير قبل 6 أنصاف أعمار بالضبط (5.27 سنة لكل منها) => halfLivesElapsed = 6.0 بالضبط
        var isotope = new Radioisotope { Symbol = "Co-60", HalfLife = 5.27, HalfLifeUnit = "years" };
        var halfLifeSeconds = 5.27 * 365.25 * 86400.0;
        var calibDate = fixedLocalNow.AddSeconds(-6.0 * halfLifeSeconds);

        var source = new Source
        {
            SourceCode = "SRC-ALERT-TEST",
            Radioisotope = isotope,
            CalibrationDate = calibDate,
            HasDetailedIsotopes = false
        };

        var fake = CreateFakeClock(fixedLocalNow);

        var (halfLivesElapsed, worstSymbol) = AlertService.CalculateMaxHalfLivesElapsed(source, fake);

        Assert.Equal("Co-60", worstSymbol);
        // نسمح بخطأ نسبي صغير جداً بسبب فروق التقريب الزمني الجزئية عند AddSeconds/AddYears
        Assert.True(Math.Abs(halfLivesElapsed - 6.0) < 1e-6,
            $"Expected ~6.0 half-lives elapsed, got {halfLivesElapsed}");
    }

    // ══════════════════════════ 4. استحقاق/تأخر اختبارات التسرب ══════════════════════════

    [Theory]
    [MemberData(nameof(FixedClocks))]
    public void LeakTestService_DueAndOverdueFiltering_AtFixedClocks_MatchesTodayBasedThreshold(DateTime fixedLocalNow)
    {
        var fake = CreateFakeClock(fixedLocalNow);
        var mockAudit = new Mock<IAuditService>();
        var mockUser = new Mock<IUserService>();
        var fakeLicense = new FakeLicenseService();
        var settingsService = new SystemSettingsService(_fixture.ContextFactory, fakeLicense);
        var sut = new LeakTestService(_fixture.ContextFactory, mockAudit.Object, mockUser.Object, settingsService, fakeLicense, fake);

        var isotope = TestDataBuilder.CreateRadioisotope("Cs-137", "Cesium-137", 30.08, "years", 661.7);
        var unit = TestDataBuilder.CreateActivityUnit("Becquerel", "Bq", 1.0);
        var location = TestDataBuilder.CreateLocation("مختبر 202");
        var overdueSource = TestDataBuilder.CreateSource(isotope, unit, location, sourceCode: $"SRC-LT-OVD-{fixedLocalNow:yyyyMMddHHmm}", isSealed: true);
        var dueSoonSource = TestDataBuilder.CreateSource(isotope, unit, location, sourceCode: $"SRC-LT-DUE-{fixedLocalNow:yyyyMMddHHmm}", isSealed: true);

        using (var db = _fixture.CreateContext())
        {
            db.Radioisotopes.Add(isotope);
            db.ActivityUnits.Add(unit);
            db.Locations.Add(location);
            db.Sources.AddRange(overdueSource, dueSoonSource);
            db.LeakTestRecords.AddRange(
                new LeakTestRecord
                {
                    SourceId = overdueSource.Id,
                    TestDate = fixedLocalNow.AddMonths(-7),
                    NextDueDate = fixedLocalNow.Date.AddDays(-1), // متأخر يوماً واحداً بالضبط عن اليوم الحالي (المحلي)
                    Result = "Pass"
                },
                new LeakTestRecord
                {
                    SourceId = dueSoonSource.Id,
                    TestDate = fixedLocalNow.AddMonths(-6).AddDays(5),
                    NextDueDate = fixedLocalNow.Date.AddDays(5), // ضمن نافذة "قريبة الاستحقاق" الافتراضية (30 يوماً)
                    Result = "Pass"
                });
            db.SaveChanges();
        }

        var overdueList = sut.GetAllRecords(dueStatusFilter: "Overdue");
        var dueSoonList = sut.GetAllRecords(dueStatusFilter: "DueSoon");

        Assert.Contains(overdueList, r => r.SourceId == overdueSource.Id);
        Assert.DoesNotContain(overdueList, r => r.SourceId == dueSoonSource.Id);
        Assert.Contains(dueSoonList, r => r.SourceId == dueSoonSource.Id);
        Assert.DoesNotContain(dueSoonList, r => r.SourceId == overdueSource.Id);
    }

    [Theory]
    [MemberData(nameof(FixedClocks))]
    public void LeakTestRecord_StatusDisplay_AtFixedClocks_ViaAppClockOverride_MatchesOverdueThreshold(DateTime fixedLocalNow)
    {
        var fake = CreateFakeClock(fixedLocalNow);
        using var scope = AppClock.Override(fake);

        var overdueRecord = new LeakTestRecord { NextDueDate = fixedLocalNow.Date.AddDays(-1) };
        var validRecord = new LeakTestRecord { NextDueDate = fixedLocalNow.Date.AddDays(1) };

        Assert.Equal("متأخر", overdueRecord.StatusDisplay);
        Assert.Equal("ساري", validRecord.StatusDisplay);
    }

    // ══════════════════════════ 5. استحقاق/تأخر طلبات الاستعارة ══════════════════════════

    [Theory]
    [MemberData(nameof(FixedClocks))]
    public void BorrowService_OverdueAndDueSoon_AtFixedClocks_MatchesTodayBasedThreshold(DateTime fixedLocalNow)
    {
        var fake = CreateFakeClock(fixedLocalNow);
        var fakeAudit = new FakeAuditService();
        var fakeUser = new FakeUserService();
        var fakeLicense = new FakeLicenseService();
        var sut = new BorrowService(_fixture.ContextFactory, fakeAudit, fakeUser, fakeLicense, timeProvider: fake);

        var isotope = TestDataBuilder.CreateRadioisotope("Cs-137", "Cesium-137");
        var unit = TestDataBuilder.CreateActivityUnit("Becquerel", "Bq");
        var location = TestDataBuilder.CreateLocation("مستودع 202");
        var overdueSource = TestDataBuilder.CreateSource(isotope, unit, location, sourceCode: $"SRC-BW-OVD-{fixedLocalNow:yyyyMMddHHmm}", status: "InUse");
        var dueSoonSource = TestDataBuilder.CreateSource(isotope, unit, location, sourceCode: $"SRC-BW-DUE-{fixedLocalNow:yyyyMMddHHmm}", status: "InUse");
        var testRole = new Role { Id = Guid.NewGuid(), RoleName = $"دور-{fixedLocalNow:yyyyMMddHHmm}", Permissions = "Borrowing,Sources" };
        var testUser = new User { Id = Guid.NewGuid(), Username = $"u{fixedLocalNow:yyyyMMddHHmm}", FullName = "مستخدم", PasswordHash = "h", RoleId = testRole.Id };

        Guid overdueReqId, dueSoonReqId;
        using (var db = _fixture.CreateContext())
        {
            db.Radioisotopes.Add(isotope);
            db.ActivityUnits.Add(unit);
            db.Locations.Add(location);
            db.Roles.Add(testRole);
            db.Sources.AddRange(overdueSource, dueSoonSource);
            db.Users.Add(testUser);

            var overdueReq = new BorrowRequest
            {
                Id = Guid.NewGuid(),
                SourceId = overdueSource.Id,
                BorrowerName = "مستعير 1",
                BorrowerUserId = testUser.Id,
                Purpose = "اختبار",
                Status = "Delivered",
                RequestDate = fixedLocalNow.AddDays(-10),
                ExpectedReturnDate = fixedLocalNow.AddDays(-3)
            };
            var dueSoonReq = new BorrowRequest
            {
                Id = Guid.NewGuid(),
                SourceId = dueSoonSource.Id,
                BorrowerName = "مستعير 2",
                BorrowerUserId = testUser.Id,
                Purpose = "اختبار",
                Status = "Delivered",
                RequestDate = fixedLocalNow.AddDays(-2),
                ExpectedReturnDate = fixedLocalNow.AddDays(3)
            };
            overdueReqId = overdueReq.Id;
            dueSoonReqId = dueSoonReq.Id;
            db.BorrowRequests.AddRange(overdueReq, dueSoonReq);
            db.SaveChanges();
        }

        sut.CheckAndUpdateOverdue();
        var dueSoonCount = sut.GetDueSoonCount();

        using var verifyDb = _fixture.CreateContext();
        var overdueAfter = verifyDb.BorrowRequests.Find(overdueReqId);
        Assert.Equal("Overdue", overdueAfter?.Status);
        Assert.True(dueSoonCount >= 1, "Due-soon request must be counted");
    }

    // ══════════════════════════ 6. انتهاء قفل الحساب (Lockout Expiry) ══════════════════════════

    [Theory]
    [MemberData(nameof(FixedClocks))]
    public void UserService_Login_LockoutExpiry_AtFixedClocks_RespectsExplicitLockoutEnd(DateTime fixedLocalNow)
    {
        var fakeLicense = new FakeLicenseService();

        // (أ) القفل لا يزال سارياً: LockoutEnd بعد اللحظة الحالية بدقيقة واحدة => الدخول مرفوض
        var fakeStillLocked = CreateFakeClock(fixedLocalNow);
        var svcStillLocked = new UserService(_fixture.ContextFactory, licenseService: fakeLicense, timeProvider: fakeStillLocked);
        var lockedUser = CreateLockedTestUser($"locked1_{fixedLocalNow:yyyyMMddHHmm}", fixedLocalNow.AddMinutes(1));
        var (successLocked, _) = svcStillLocked.Login(lockedUser.Username, "Password123!");
        Assert.False(successLocked, "Login must be rejected while LockoutEnd is still in the future relative to the fake clock");

        // (ب) القفل انتهى للتو: LockoutEnd قبل اللحظة الحالية بدقيقة واحدة => الدخول مسموح (بكلمة مرور صحيحة)
        var fakeExpired = CreateFakeClock(fixedLocalNow);
        var svcExpired = new UserService(_fixture.ContextFactory, licenseService: fakeLicense, timeProvider: fakeExpired);
        var expiredUser = CreateLockedTestUser($"locked2_{fixedLocalNow:yyyyMMddHHmm}", fixedLocalNow.AddMinutes(-1));
        var (successExpired, _) = svcExpired.Login(expiredUser.Username, "Password123!");
        Assert.True(successExpired, "Login must succeed once LockoutEnd is in the past relative to the fake clock");
    }

    private User CreateLockedTestUser(string username, DateTime lockoutEnd)
    {
        using var context = _fixture.CreateContext();
        var role = new Role { Id = Guid.NewGuid(), RoleName = $"دور-{username}", Permissions = "Sources" };
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = username,
            FullName = "مستخدم مقفل",
            Email = $"{username}@test.local",
            PasswordHash = PasswordHelper.HashPassword("Password123!"),
            RoleId = role.Id,
            IsActive = true,
            FailedLoginAttempts = 5,
            LockoutEnd = lockoutEnd
        };
        context.Roles.Add(role);
        context.Users.Add(user);
        context.SaveChanges();
        return user;
    }

    // ══════════════════════════ Helper ══════════════════════════

    private static void AssertRelativelyClose(double expected, double actual, double relativeTolerance = 1e-9)
    {
        if (expected == 0.0)
        {
            Assert.Equal(0.0, actual, precision: 15);
            return;
        }
        var relativeError = Math.Abs(actual - expected) / Math.Abs(expected);
        Assert.True(relativeError < relativeTolerance,
            $"Expected {expected:E15}, but got {actual:E15}. Relative error: {relativeError:E4}");
    }
}
