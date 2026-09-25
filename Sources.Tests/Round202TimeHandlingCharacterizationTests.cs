using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Time.Testing;
using Moq;
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

    // بيانات SRC-0106 (Tc-99m): معايرة 2026-09-14 20:46:36.7276544 محلي، 98.2 mCi، T½=6.01h.
    // القيم التالية محسوبة يدوياً (خارج الاختبار، بدقة tick=100ns، لا إعادة تنفيذ لصيغة الإنتاج
    // وقت التشغيل): elapsedSeconds = (لحظة الساعة - المعايرة).Ticks / 1e7 ، halfLifeSeconds=21636 s،
    // A = 98.2 * 0.5^(elapsedSeconds/21636).
    // 20:53:00 (لقطة الشاشة المرجعية في العقد): elapsed=864383.2723456   => 9.238773496698382E-011
    // 23:30:00 نفس اليوم:                      elapsed=873803.2723456   => 6.832037383736898E-011
    // 00:30:00 اليوم التالي:                    elapsed=877403.2723456   => 6.087823439559247E-011
    // 30/09 23:30:00 (حد الشهر قبل):            elapsed=1392203.2723456  => 4.186503924785254E-018
    // 01/10 00:30:00 (حد الشهر بعد):             elapsed=1395803.2723456  => 3.730467983647703E-018
    public static TheoryData<DateTime, double> Tc99mFixedPoints => new()
    {
        { new DateTime(2026, 9, 24, 20, 53, 0), 9.238773496698382e-11 },
        { FixedNear2330Local, 6.832037383736898e-11 },
        { FixedNear0030LocalNextDay, 6.087823439559247e-11 },
        { FixedMonthBoundaryBefore, 4.186503924785254e-18 },
        { FixedMonthBoundaryAfter, 3.730467983647703e-18 },
    };

    [Theory]
    [MemberData(nameof(Tc99mFixedPoints))]
    public void Decay_Tc99mLikeSource_AtFixedClocks_MatchesIndependentFormula(DateTime fixedLocalNow, double expected)
    {
        var calibDate = new DateTime(2026, 9, 14, 20, 46, 36).AddTicks(7276544);
        var fake = CreateFakeClock(fixedLocalNow);
        var sut = new DecayCalculationService(fake);

        var actual = sut.CalculateCurrentActivity(98.2, 6.01, "hours", calibDate);

        AssertRelativelyClose(expected, actual);
    }

    // بيانات صف F-18 من SRC-0029: معايرة 2026-09-14 19:12:18.7596567 محلي، 82.1 mCi، T½=109.7min.
    // نفس المنهج: halfLifeSeconds=6582 s، A = 82.1 * 0.5^(elapsedSeconds/6582).
    // 20:53:00: elapsed=870041.2403433   => 1.3265170327085314E-038
    // 23:30:00: elapsed=879461.2403433   => 4.919103330784008E-039
    // 00:30:00 (اليوم التالي): elapsed=883061.2403433 => 3.366965978908266E-039
    // 30/09 23:30:00: elapsed=1397861.2403433 => 9.609187144223089E-063
    // 01/10 00:30:00: elapsed=1401461.2403433 => 6.577175559027191E-063
    public static TheoryData<DateTime, double> F18FixedPoints => new()
    {
        { new DateTime(2026, 9, 24, 20, 53, 0), 1.3265170327085314e-38 },
        { FixedNear2330Local, 4.919103330784008e-39 },
        { FixedNear0030LocalNextDay, 3.366965978908266e-39 },
        { FixedMonthBoundaryBefore, 9.609187144223089e-63 },
        { FixedMonthBoundaryAfter, 6.577175559027191e-63 },
    };

    [Theory]
    [MemberData(nameof(F18FixedPoints))]
    public void Decay_F18LikeSource_AtFixedClocks_MatchesIndependentFormula(DateTime fixedLocalNow, double expected)
    {
        var calibDate = new DateTime(2026, 9, 14, 19, 12, 18).AddTicks(7596567);
        var fake = CreateFakeClock(fixedLocalNow);
        var sut = new DecayCalculationService(fake);

        var actual = sut.CalculateCurrentActivity(82.1, 109.7, "minutes", calibDate);

        AssertRelativelyClose(expected, actual);
    }

    // Cs-137: معايرة مطلقة ثابتة 2021-09-24 12:00:00 محلي (~5 سنوات قبل 2026)، 3.7e7 Bq، T½=30.08y.
    // halfLifeSeconds = 30.08*365.2422*86400 = 949,084,874.112 s (توحيد ثابت السنة الاستوائية للجولة 205).
    // القيم التالية محسوبة بالمعامل الموحد بدقة tick:
    // 23:30:00 (24/09): elapsed=157807800  => 32972824.08991160 Bq
    // 00:30:00 (25/09): elapsed=157811400  => 32972737.41148643 Bq
    // 23:30:00 (30/09): elapsed=158326200  => 32960344.74243011 Bq
    // 00:30:00 (01/10): elapsed=158329800  => 32960258.09681045 Bq
    public static readonly DateTime Cs137CalibrationDate = new(2021, 9, 24, 12, 0, 0);

    public static TheoryData<DateTime, double> Cs137FixedPoints => new()
    {
        { FixedNear2330Local, 32972824.08991160 },
        { FixedNear0030LocalNextDay, 32972737.41148643 },
        { FixedMonthBoundaryBefore, 32960344.74243011 },
        { FixedMonthBoundaryAfter, 32960258.09681045 },
    };

    [Theory]
    [MemberData(nameof(Cs137FixedPoints))]
    public void Decay_LongLivedNuclide_Cs137_AtFixedClocks_MatchesIndependentFormula(DateTime fixedLocalNow, double expected)
    {
        const double initialBq = 3.7e7;
        var fake = CreateFakeClock(fixedLocalNow);
        var sut = new DecayCalculationService(fake);

        var actual = sut.CalculateCurrentActivity(initialBq, 30.08, "years", Cs137CalibrationDate);

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

    // Am-241/Be: معايرة مطلقة ثابتة 2023-09-24 12:00:00 محلي (~3 سنوات قبل 2026)، معدل معاير
    // 1.0e7 n/s، T½=432.2y. halfLifeSeconds = 432.2*365.2422*86400 = 13,644,913,747.776 s تقريباً.
    // القيم التالية محسوبة يدوياً بدقة tick (B(t)=B0*exp(-ln2*elapsed/T½))، وليست إعادة تنفيذ
    // صيغة الإنتاج وقت التشغيل:
    // 23:30:00 (24/09): elapsed=94735800  => 9951969.724758875 n/s
    // 00:30:00 (25/09): elapsed=94739400  => 9951967.903978713 n/s
    // 23:30:00 (30/09): elapsed=95254200  => 9951707.53584523 n/s
    // 00:30:00 (01/10): elapsed=95257800  => 9951705.715113036 n/s
    public static readonly DateTime NeutronCalibrationDate = new(2023, 9, 24, 12, 0, 0);

    public static TheoryData<DateTime, double> NeutronFixedPoints => new()
    {
        { FixedNear2330Local, 9951969.724758875 },
        { FixedNear0030LocalNextDay, 9951967.903978713 },
        { FixedMonthBoundaryBefore, 9951707.53584523 },
        { FixedMonthBoundaryAfter, 9951705.715113036 },
    };

    [Theory]
    [MemberData(nameof(NeutronFixedPoints))]
    public void NeutronDecay_CalibratedThreeYearsAgo_AtFixedClocks_MatchesIndependentFormula(DateTime fixedLocalNow, double expected)
    {
        const double calibratedRate = 1.0e7;
        const double halfLifeYears = 432.2;
        var fake = CreateFakeClock(fixedLocalNow);
        var sut = new NeutronDecayCalculationService(fake);

        var source = new NeutronSource
        {
            CalibratedEmissionRate = calibratedRate,
            EmissionCalibrationDate = NeutronCalibrationDate,
            NeutronSourceType = new NeutronSourceType
            {
                Code = "AmBe-TEST",
                HalfLife = halfLifeYears,
                HalfLifeUnit = "years"
            }
        };

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

    // مصادر Co-60 معايَرة عند 4.9/5.2/6.3 فترة نصف عمر بالضبط (5.27 سنة لكل فترة) — بيانات اختبار
    // فقط (نستخدم الصيغة لبناء تاريخ المعايرة كمدخل، لا لحساب قيمة نشاط متوقعة وقت التشغيل)؛
    // التأكيد الفعلي فئوي بحت (عدد التنبيهات الحرجة/التحذيرية الناتجة عن GenerateAlerts() الحقيقية):
    // 4.9 T½  -> لا تنبيه (تحت عتبة 5.0)
    // 5.2 T½  -> تحذيري (Warning)  [عتبة: >=5.0 و<6.0]
    // 6.3 T½  -> حرج (Critical)    [عتبة: >=6.0]
    public static TheoryData<DateTime> NearMidnightClocks => new()
    {
        FixedNear2330Local,
        FixedNear0030LocalNextDay,
    };

    [Theory]
    [MemberData(nameof(NearMidnightClocks))]
    public void AlertService_GenerateAlerts_LowActivityCounts_AtFixedClocks_MatchesExactExpectedCounts(DateTime fixedLocalNow)
    {
        var fake = CreateFakeClock(fixedLocalNow);
        var decayService = new DecayCalculationService(fake);
        var fakeLicense = new FakeLicenseService();
        var settingsService = new SystemSettingsService(_fixture.ContextFactory, fakeLicense);
        var sut = new AlertService(_fixture.ContextFactory, decayService, settingsService, fakeLicense, fake);

        var isotope = TestDataBuilder.CreateRadioisotope("Co-60", "Cobalt-60", 5.27, "years", 1332.5);
        var unit = TestDataBuilder.CreateActivityUnit("Becquerel", "Bq", 1.0);
        var location = TestDataBuilder.CreateLocation("مختبر 202-تنبيهات");
        var halfLifeSeconds = 5.27 * 365.25 * 86400.0;

        var srcNone = TestDataBuilder.CreateSource(isotope, unit, location, sourceCode: $"SRC-AL-NONE-{fixedLocalNow:yyyyMMddHHmm}",
            calibrationDate: fixedLocalNow.AddSeconds(-4.9 * halfLifeSeconds), status: "InUse");
        var srcWarning = TestDataBuilder.CreateSource(isotope, unit, location, sourceCode: $"SRC-AL-WARN-{fixedLocalNow:yyyyMMddHHmm}",
            calibrationDate: fixedLocalNow.AddSeconds(-5.2 * halfLifeSeconds), status: "InUse");
        var srcCritical = TestDataBuilder.CreateSource(isotope, unit, location, sourceCode: $"SRC-AL-CRIT-{fixedLocalNow:yyyyMMddHHmm}",
            calibrationDate: fixedLocalNow.AddSeconds(-6.3 * halfLifeSeconds), status: "InUse");

        using (var db = _fixture.CreateContext())
        {
            db.Radioisotopes.Add(isotope);
            db.ActivityUnits.Add(unit);
            db.Locations.Add(location);
            db.Sources.AddRange(srcNone, srcWarning, srcCritical);
            db.SaveChanges();
        }

        var alerts = sut.GenerateAlerts();
        var lowActivityAlerts = alerts.Where(a => a.AlertType == "LowActivity").ToList();

        Assert.DoesNotContain(lowActivityAlerts, a => a.SourceId == srcNone.Id);
        Assert.Equal(1, lowActivityAlerts.Count(a => a.SourceId == srcWarning.Id && a.Severity == "Warning"));
        Assert.Equal(1, lowActivityAlerts.Count(a => a.SourceId == srcCritical.Id && a.Severity == "Critical"));
        Assert.Equal(2, lowActivityAlerts.Count); // بالضبط تنبيهان: تحذيري واحد وحرج واحد، لا شيء لـ4.9
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

    // ══════════════════════════ 5. استحقاق/تأخر طلبات الاستعارة (حدّية دقيقة) ══════════════════════════

    [Theory]
    [MemberData(nameof(FixedClocks))]
    public void BorrowService_OverdueAndDueSoon_AtFixedClocks_ExactBoundaries(DateTime fixedLocalNow)
    {
        // أربعة طلبات "Delivered" عند كل ساعة ثابتة، بالاستناد إلى العتبة الفعلية الافتراضية
        // GetDueSoonDaysThreshold()=7 (لا خدمة إعدادات مُمرَّرة):
        //   يوم أمس (Today-1)        -> يجب أن تصبح Overdue بعد CheckAndUpdateOverdue
        //   اليوم بالضبط (Today)     -> يجب ألا تصبح Overdue (تبقى Delivered)
        //   Today+7 (حد العتبة)      -> Delivered ومُحتسَبة ضمن قريبة الإرجاع
        //   Today+8 (خارج العتبة)    -> Delivered وغير محتسَبة
        // الساعة 00:30 محلياً (=22:30 اليوم السابق UTC) هي الحالة التي يقلب فيها أي خطأ UTC/محلي
        // "اليوم" إلى اليوم السابق فيُخطئ تصنيف الطلبين الحدّيين (أمس/اليوم).
        var fake = CreateFakeClock(fixedLocalNow);
        var fakeAudit = new FakeAuditService();
        var fakeUser = new FakeUserService();
        var fakeLicense = new FakeLicenseService();
        var sut = new BorrowService(_fixture.ContextFactory, fakeAudit, fakeUser, fakeLicense, timeProvider: fake);

        const int thresholdDays = 7; // BorrowService.GetDueSoonDaysThreshold() الافتراضي بلا ISystemSettingsService
        var today = fake.LocalToday();

        var isotope = TestDataBuilder.CreateRadioisotope("Cs-137", "Cesium-137");
        var unit = TestDataBuilder.CreateActivityUnit("Becquerel", "Bq");
        var location = TestDataBuilder.CreateLocation("مستودع 202");
        var srcYesterday = TestDataBuilder.CreateSource(isotope, unit, location, sourceCode: $"SRC-BW-Y-{fixedLocalNow:yyyyMMddHHmm}", status: "InUse");
        var srcToday = TestDataBuilder.CreateSource(isotope, unit, location, sourceCode: $"SRC-BW-T-{fixedLocalNow:yyyyMMddHHmm}", status: "InUse");
        var srcAtThreshold = TestDataBuilder.CreateSource(isotope, unit, location, sourceCode: $"SRC-BW-TH-{fixedLocalNow:yyyyMMddHHmm}", status: "InUse");
        var srcPastThreshold = TestDataBuilder.CreateSource(isotope, unit, location, sourceCode: $"SRC-BW-PT-{fixedLocalNow:yyyyMMddHHmm}", status: "InUse");
        var testRole = new Role { Id = Guid.NewGuid(), RoleName = $"دور-{fixedLocalNow:yyyyMMddHHmm}", Permissions = "Borrowing,Sources" };
        var testUser = new User { Id = Guid.NewGuid(), Username = $"u{fixedLocalNow:yyyyMMddHHmm}", FullName = "مستخدم", PasswordHash = "h", RoleId = testRole.Id };

        Guid reqYesterdayId, reqTodayId, reqAtThresholdId, reqPastThresholdId;
        using (var db = _fixture.CreateContext())
        {
            db.Radioisotopes.Add(isotope);
            db.ActivityUnits.Add(unit);
            db.Locations.Add(location);
            db.Roles.Add(testRole);
            db.Sources.AddRange(srcYesterday, srcToday, srcAtThreshold, srcPastThreshold);
            db.Users.Add(testUser);

            BorrowRequest MakeReq(Source src, DateTime expectedReturn) => new()
            {
                Id = Guid.NewGuid(),
                SourceId = src.Id,
                BorrowerName = "مستعير",
                BorrowerUserId = testUser.Id,
                Purpose = "اختبار حدّي",
                Status = "Delivered",
                RequestDate = today.AddDays(-1),
                ExpectedReturnDate = expectedReturn
            };

            var reqYesterday = MakeReq(srcYesterday, today.AddDays(-1));
            var reqToday = MakeReq(srcToday, today);
            var reqAtThreshold = MakeReq(srcAtThreshold, today.AddDays(thresholdDays));
            var reqPastThreshold = MakeReq(srcPastThreshold, today.AddDays(thresholdDays + 1));

            reqYesterdayId = reqYesterday.Id;
            reqTodayId = reqToday.Id;
            reqAtThresholdId = reqAtThreshold.Id;
            reqPastThresholdId = reqPastThreshold.Id;

            db.BorrowRequests.AddRange(reqYesterday, reqToday, reqAtThreshold, reqPastThreshold);
            db.SaveChanges();
        }

        sut.CheckAndUpdateOverdue();
        var dueSoonCount = sut.GetDueSoonCount();

        using var verifyDb = _fixture.CreateContext();
        var afterYesterday = verifyDb.BorrowRequests.Find(reqYesterdayId);
        var afterToday = verifyDb.BorrowRequests.Find(reqTodayId);
        var afterAtThreshold = verifyDb.BorrowRequests.Find(reqAtThresholdId);
        var afterPastThreshold = verifyDb.BorrowRequests.Find(reqPastThresholdId);

        Assert.Equal("Overdue", afterYesterday?.Status);
        Assert.Equal("Delivered", afterToday?.Status);
        Assert.Equal("Delivered", afterAtThreshold?.Status);
        Assert.Equal("Delivered", afterPastThreshold?.Status);

        // Delivered ضمن [today, today+threshold] تُحتسَب قريبة الإرجاع: اليوم بالضبط وحد العتبة (2)؛
        // أمس (Overdue، لم تعد Delivered) وما بعد العتبة (Delivered لكن خارج النافذة) غير محتسَبتين.
        Assert.Equal(2, dueSoonCount);
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

    // ══════════════════════════ 7. عدّادات UsersViewModel (القفل + نشاط اليوم) ══════════════════════════

    [Fact]
    public void UsersViewModel_LockedUsersCount_And_ActivitiesTodayCount_AtNearMidnightClock_MatchesExactCounts()
    {
        var fixedLocalNow = FixedNear0030LocalNextDay; // 00:30 محلي — الحالة التي يقلب فيها خطأ UTC "اليوم"
        var fake = CreateFakeClock(fixedLocalNow);
        var today = fake.LocalToday();

        var stillLockedUser = new User
        {
            Id = Guid.NewGuid(), Username = "u-locked", FullName = "مقفل",
            LockoutEnd = fixedLocalNow.AddMinutes(1), IsActive = true
        };
        var justExpiredUser = new User
        {
            Id = Guid.NewGuid(), Username = "u-expired", FullName = "منتهي القفل",
            LockoutEnd = fixedLocalNow.AddMinutes(-1), IsActive = true
        };

        var todayLog = new AuditLog { Id = Guid.NewGuid(), Action = "Login", ActionDate = today.AddHours(0).AddMinutes(10) }; // اليوم 00:10
        var yesterdayLog = new AuditLog { Id = Guid.NewGuid(), Action = "Login", ActionDate = today.AddDays(-1).AddHours(23).AddMinutes(50) }; // أمس 23:50

        var mockUserService = new Mock<IUserService>();
        mockUserService.Setup(s => s.GetAllUsers()).Returns(new List<User> { stillLockedUser, justExpiredUser });
        mockUserService.Setup(s => s.GetAllRoles()).Returns(new List<Role>());
        mockUserService.Setup(s => s.GetAuditLogs(It.IsAny<Guid?>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
            .Returns(new List<AuditLog> { todayLog, yesterdayLog });
        var mockReportingService = new Mock<IReportingService>();

        // messenger منفصل (وليس WeakReferenceMessenger.Default) لعزل الاختبار؛ UsersViewModel لا
        // يُنفِّذ IDisposable فلا يوجد ما يُستدعى بعد الاختبار (يطابق نمط UsersViewModelTests.cs الحالي).
        var vm = new UsersViewModel(mockUserService.Object, mockReportingService.Object,
            messenger: new WeakReferenceMessenger(), timeProvider: fake);

        Assert.Equal(1, vm.LockedUsersCount); // stillLockedUser فقط (LockoutEnd > الآن)
        Assert.Equal(1, vm.ActivitiesTodayCount); // todayLog فقط (ActionDate.Date == اليوم المحلي)
    }

    // ══════════════════════════ 8. لوحة القيادة — ملاحظة جدوى (لا اختبار) ══════════════════════════
    // DashboardViewModel.UpdateLowActivityAlertCard (Low/CriticalActivityCount) يستدعي
    // AlertService.CalculateMaxHalfLivesElapsed(source) الثابتة **بلا** تمرير TimeProvider، فتستخدم
    // دوماً TimeProvider.System الحقيقي بصرف النظر عن _timeProvider المحقون في نفس الـViewModel —
    // موقع الاستدعاء هذا لم يكن ضمن الـ65 موقعاً في عقد الجولة (لأنه ليس قراءة DateTime.Now/Today
    // مباشرة، بل استدعاء تابع ثابت بمعامل اختياري افتراضي). ولأن الإنتاج مُجمَّد منذ R202-A (هذا
    // الإصلاح لا يمسّ أي كود إنتاجي)، لا يمكن تثبيت عدّادي لوحة القيادة (LowActivityCriticalCount/
    // LowActivityWarningCount) بساعة مزيَّفة دون تعديل إنتاجي خارج النطاق. المُساعد المُسنَّم الذي
    // يُغطّي نفس الحساب فعلياً وبحتمية كاملة هو AlertService.CalculateMaxHalfLivesElapsed(source,
    // timeProvider) — مختبَر أعلاه في القسمين 3 و3-ب (GenerateAlerts) — وAlertService.GenerateAlerts()
    // نفسها (القسم 3-ب) التي تغذي شاشة التنبيهات الفعلية بنفس المنطق.

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
