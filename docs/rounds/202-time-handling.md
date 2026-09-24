ROUND 202 — Time handling: one TimeProvider seam, UTC for instants, calendar dates untouched, decay unchanged

Save THIS TEXT VERBATIM as docs/rounds/202-time-handling.md (you may append a separate "Discovery findings"
section below it). Commit it in the round's PR.
BASE: main at 35dbc0e (round 201, PR #97 merged). Branch: refactor/round-202-time-handling
(if a name collision forces a different name, report it as a deviation).
Risk: HIGH (decay calculations, alerts, due dates, audit timestamps). Full pipeline: implementer -> your own diff
review -> change-verifier -> ci-monitor. One commit per group (A..F), prefix "R202-<letter>:". Fixups as separate
"R202-<letter>-fix" commits (never amend, not even before push), reported as deviations. CodeRabbit is not a merge
requirement.

DOMAIN WARNING (why this is high risk): the lab is in Libya (UTC+2, no DST). The data contains short-lived
radionuclides (F-18, T½ ≈ 110 min; Tc-99m, T½ ≈ 6 h). A 2-hour mix-up between local and UTC time inside a decay
calculation changes the computed activity by roughly a third for F-18. Decay results MUST NOT change in this round.

CORE RULE — behaviour-preserving, proven by characterization tests (same method as round 201):
Group B tests must call REAL production code (no re-implemented logic inside tests), run with a FIXED clock, pass on
the code after group A, and then pass UNCHANGED after C and D. STOP RULE: if any group-B expectation would need to
change, stop that group and report — only the architect can approve a behaviour change.

STEP 0 — Worktree housekeeping: same rules as round 201 STEP 0. Report the table and the base hash.

STEP 1 — Discovery (read-only)
D1. Every use of DateTime.Now, DateTime.UtcNow, DateTime.Today, DateTimeOffset.Now/UtcNow in production code
    (file:line), classified as: INSTANT (created/updated/deleted-at, audit, login, alert time, backup name, logs),
    CALENDAR (calibration, manufacture, emission calibration, expected/actual return, leak-test dates and due dates),
    or DECAY/ELAPSED (any "now minus reference date" used in a calculation or a threshold).
D2. For every DateTime property in the EF model: its category (INSTANT or CALENDAR), how it is written (Now vs
    UtcNow), how it is stored in SQLite, and the Kind it has when read back.
D3. Every place where two DateTimes of DIFFERENT categories or kinds are subtracted or compared (e.g. UtcNow minus a
    local calibration date). These are the real bugs, if any. List each with the time error it causes.
D4. Every place a DateTime is DISPLAYED or EXPORTED (UI, reports, PDF/Excel, audit log view) and the format used.
D5. Tests that depend on the real clock (flaky near midnight or month end).
STOP RULE: if D1 finds more than ~80 production sites, stop after discovery and propose a split into two rounds.

STEP 2 — Changes
A. TimeProvider seam (commit R202-A, pure refactor): register .NET 8 TimeProvider in DI (TimeProvider.System) and
   route every D1 site through it (GetLocalNow / GetUtcNow) WITHOUT changing which kind of time each site uses today.
   Static/helper code that cannot take DI gets the same TimeProvider through one small static accessor used ONLY
   where injection is impossible (list each such site). No behaviour change.
B. Characterization tests (commit R202-B): with a FIXED FakeTimeProvider (Microsoft.Extensions.TimeProvider.Testing,
   or an equivalent minimal fake), pin today's exact results for: decay/current activity (including F-18 and Tc-99m
   and a long-lived nuclide), neutron emission-rate decay, low-activity alert counts and half-lives-elapsed values,
   leak-test due/overdue lists, borrow due-soon/overdue counts, and dashboard counts. Include a fixed time near
   local midnight (e.g. 23:30 local = 21:30 UTC) and one at a month boundary. All must pass on the code after A.
C. Instants to UTC (commit R202-C): INSTANT fields are written with GetUtcNow() and stored as UTC; add an EF value
   converter so INSTANT properties read back with DateTimeKind.Utc. Every INSTANT display converts to local time
   through ONE shared converter/formatter. CALENDAR fields are NOT converted and NOT given a time-zone meaning.
   Group-B tests unchanged.
D. Fix D3 mismatches (commit R202-D): each DECAY/ELAPSED calculation compares values of the SAME basis. Decide per
   site and justify in the round doc. If fixing a D3 mismatch would change a group-B expectation, STOP and report it
   as a proposed behaviour change with the exact before/after numbers — do not apply it.
E. New tests (commit R202-E): INSTANT round-trip keeps Kind=Utc; display shows local time (UTC+2) for a known instant;
   CALENDAR dates never shift a day at 23:30 local; D5 flaky tests made clock-independent.
F. Documentation (commit R202-F): session-summary.md round 202 entry (CI Release and local Debug separately);
   release-readiness.md: header (main at 35dbc0e, round 202 under review); close the UTC item; record D1–D5 counts,
   the INSTANT/CALENDAR table and every D3 finding. Note that rows created before this round (test data only) keep
   local-time instants and may display 2 hours off — expected, no migration.

FORBIDDEN
- Any data migration or UPDATE of existing rows. Any schema change. No EF migration.
- Converting CALENDAR dates to/from UTC. Changing half-life values, units or decay formulas.
- Any behaviour change not approved by the architect (see CORE RULE and group D).
- Audit-log message TEXT (stays Arabic; only the timestamp storage changes).
- LoginWindow, LoginView, SplashWindow. PhraseFactoryResetConfirmation / RequiredResetPhrase, SystemResetService.
  Calculator math (round 205).
- No `git add .` / `-A`; explicit pathspecs; `git show --stat` per commit with a one-line justification per file.

GIT
- Commits in order A..F, push, open a DRAFT PR to main. Do not merge.

REPORT
- STEP 0 table and base hash.
- D1–D5 findings (counts per file; full lists in the round doc), and every D3 mismatch with its time error.
- Per commit: hash, files, justification.
- Explicit statement: "all group-B tests passed after A and unchanged after C and D" — or which group stopped and why.
- Test counts: local Debug and CI "Build and Test" (CI baseline 1567, Debug baseline 1569; state new tests).
  Confirm the TestDataIsolation sentinel tests passed.
- CI warnings vs the 3 known CS8604.
- Worktree path holding the final commit + exact PowerShell commands in lowercase (with quotes).
- A numbered, step-by-step visual checklist in ARABIC (one action per step, with the expected result): dashboard
  numbers and bell compared with a screenshot taken on main before this round; current activity of one F-18 and one
  Tc-99m source compared with main; a source's details (calibration date unchanged); Deletions log times; Users audit
  log times; Reports dates; Arabic and English UI (restart after changing language, by design); navigate-away-and-back
  loop after each screen. SAFE actions only: no deletions, no new records, no status or password changes.

---

## Addendum (architect, 2026-09-24)

Addendum to round 202 — findings from Edrees's "before" screenshots (main 35dbc0e, 2026-09-24 20:53 local):
1. Decay is computed at DAY resolution: SRC-0106 (Tc-99m, 98.2 mCi, calibration 2026-09-14) shows 9.23E-011 mCi,
   which equals exactly 240 h of decay (39.95 half-lives) = calibration-date midnight to TODAY's midnight, not to the
   current time. Confirm this in D1/D3 (which "now" each decay site uses: Today vs Now), and PIN it in group B —
   it is the current behaviour and must not change. Consequence for this round: a UTC/local mix-up would shift the
   DAY near midnight (00:00–02:00 local), i.e. a whole day of decay, so the 23:30-local and 00:30-local fixed-clock
   tests in group B are mandatory for every decay/alert/due-date site.
2. SRC-0029 F-18 (82.1 mCi, calibration 2026-02-01) shows 1.33E-038 mCi — near the float32 minimum normal value
   (~1.18E-38), whereas ~235 days of F-18 decay should underflow to ~0 in double. Investigate (read-only) whether any
   decay path uses float instead of double, or clamps to a minimum. Report it; do NOT change it in this round unless
   the architect approves.
Record both findings in the round doc.
If group B is already committed, add the missing 23:30/00:30 decay tests as a separate "R202-B-fix" commit and verify
they pass on the post-A code before continuing with C and D.

---

## Discovery findings (lead, 2026-09-24, base 35dbc0e) — STOP RULE TRIGGERED

### STEP 0

| Worktree / folder | State | Action |
|---|---|---|
| `agent-a0271139f6f654552` (refactor/round-201-status-logic-borrow-role @ 044554b, ancestor of PR #97 head 723eef5, MERGED, clean) | merged | removed (`git worktree remove`, no --force) |
| `round-201-status-logic-df87eb` (detached @ 723eef5 = PR #97 head, MERGED, clean) | merged | unregistered by `git worktree remove`; folder deletion failed "Permission denied" (held open) — delete later |
| `round-200-status-catalog-5d8531` (@ aa1e98d, only untracked = older draft of the 200 contract) | superseded | kept — removal needs --force |
| `round-198-auth-test-isolation-338bd3` (@ 36921cf, only untracked = older draft of the 198 contract) | superseded | kept — removal needs --force |
| `git-log-output-check-82b87f` (unregistered, empty folder) | empty | kept — still cannot be removed |

Base: 35dbc0ed4f7de870bf488dbeaa97f8bcb0c84983 = origin/main. Branch `refactor/round-202-time-handling` created (no collision).

### D1 — 144 production clock reads in 31 files (STOP RULE: > ~80)

No `DateTime.UtcNow` / `DateTimeOffset` anywhere in production: every read is `DateTime.Now` (local) or `DateTime.Today`.
Per file: TestDataGeneratorService 16, AllModels 13, BorrowViewModel 11, UsersViewModel 8, SourcesViewModel 8,
ReportingService 8, DashboardViewModel 7, LeakTestsViewModel 6, SourceService 6, NeutronSourceService 6, BorrowService 6,
UserService 5, DecayCalculationService 5, ReportsViewModel 4, ActivityCalculatorViewModel 4, AutoBackupService 4,
MainViewModel 3, BackupService 3, AlertService 3, LocationsViewModel 2, LocationDetailsViewModel 2, NeutronSourceTypeService 2,
NeutronDecayCalculationService 2, LoggerService 2, LeakTestService 2, SystemResetService 1, SourceCertificateService 1,
RadioisotopeService 1, LocationService 1, AuditService 1, AppDbContext 1. (AutoBackupService:168 is a comment — excluded.)

- INSTANT, persisted timestamps (26): AllModels 271, 631, 668, 749, 837, 867, 917, 998, 1084, 1142 (property initializers);
  UserService 64 (LockoutEnd), 80 (LastLoginDate), 289; BorrowService 152-154 (Request/Approval/DeliveryDate); AuditService 37;
  **SystemResetService 85 (FORBIDDEN file)**; AppDbContext 247; SourceCertificateService 83; NeutronSourceService 171, 366;
  RadioisotopeService 222; LocationService 174; NeutronSourceTypeService 94, 244; LeakTestService 177; SourceService 152, 249, 371.
- INSTANT, comparisons against the clock (8): lockout UserService 50, 52; AllModels 776; UsersViewModel 188, 239, 247;
  ReportingService 946; UsersViewModel 271 (Today vs ActionDate.Date).
- INSTANT, non-persisted (37): export file names UsersVM 528/550/572/594, SourcesVM 1498/1517, ReportsVM 325/385,
  LocationsVM 314/339, BorrowVM 512/538, LocationDetailsVM 296/323, LeakTestsVM 429/453 (16); report header stamps
  ReportingService 190/338/479/610/807/1156/1306 (7); BackupService 63/149/422; AutoBackupService 121/124/140/175;
  LoggerService 21/22; MainViewModel 501/516/526 (inactivity timer).
- CALENDAR (37): SourcesVM 169, 832, 913, 1152, 1157, 1304; NeutronSourceService 140, 142, 233, 235; SourceService 101, 195;
  BorrowVM 113, 120, 274, 318, 321, 422, 428, 484, 576; LeakTestsVM 51, 52, 216, 265; ActivityCalculatorVM 44, 45, 431, 432;
  AllModels 896, 929; BorrowService 232, 272, 292; LeakTestService 70; AlertService 54.
- DECAY/ELAPSED (20): DecayCalculationService 19, 140, 214, 215, 291; NeutronDecayCalculationService 22, 158; ReportsVM 461, 476;
  AlertService 220, 237; SourceService 576; DashboardVM 1395, 1476, 1558, 1559, 1561, 1587, 1589.
- TEST DATA (16): TestDataGeneratorService 171, 193, 196, 220, 222, 270, 292, 309, 393, 431, 434, 465, 468, 495, 507, 546.

### D2 — EF model: 28 DateTime properties, all SQLite TEXT, no value converter → all read back Kind=Unspecified

- INSTANT (20), all written with `DateTime.Now` (local): Radioisotope.DeletedAt; Source.DeletedAt, CreatedAt; Location.DeletedAt;
  SourceLocationHistory.MovedAt; BorrowRequest.RequestDate, ApprovalDate, DeliveryDate; User.DeletedAt, CreatedAt, LockoutEnd,
  LastLoginDate; AuditLog.ActionDate; AlertNotification.CreatedAt; LeakTestRecord.CreatedAt; NeutronSourceType.DeletedAt, CreatedAt;
  NeutronSource.DeletedAt, CreatedAt; SourceCertificate.AttachedAt.
- CALENDAR (8), written from DatePickers / VM defaults: SourceIsotope.CalibrationDate; Source.CalibrationDate;
  BorrowRequest.ExpectedReturnDate, ActualReturnDate; LeakTestRecord.TestDate, NextDueDate; NeutronSource.CalibrationDate,
  EmissionCalibrationDate.

**Finding D2-1: calibration "dates" are NOT date-only.** `SourcesViewModel` defaults `EditCalibrationDate = DateTime.Now`
(169, 832) and the test-data generator writes `Now.AddSeconds(...)`, so stored values carry a local time of day
(read-only DB check: SRC-0106 `2026-09-14 20:46:36.7276544`; SRC-0029 F-18 row `2026-09-14 19:12:18.7596567`).
Decay uses that time of day. They are local wall-clock values and must never be converted to/from UTC.

### D3 — mismatched-basis sites on the base: NONE

Every clock read is local and every stored value is local/Unspecified, so no subtraction or comparison mixes kinds today.
Decay (`Now − CalibrationDate`), alerts, due dates (`Today` vs `.Date`), lockout (`LockoutEnd` vs `Now`) and audit filters
are all local-vs-local. **Group C would CREATE mismatches unless these sites move together** (latent, not current):
- Lockout: UserService 50/52/64, AllModels 776, UsersVM 188/239/247, ReportingService 946 — if LockoutEnd is written as
  UTC+15 min but compared with local Now (UTC+2), the lock is already expired when written → authentication regression.
- Audit "today" counts: UsersVM 271-273 (`ActionDate.Date == Today`) — wrong between 00:00 and 02:00 local.
- Audit date filters: AuditService 64/69/91/96 — SQL-side TEXT comparison of a local DatePicker date vs UTC text → 2 h shift.
- Alerts date filter: AlertsViewModel 266/271 — same 2 h shift.
- Mixed legacy rows: ORDER BY ActionDate on TEXT interleaves pre-round local rows with new UTC rows (test data only).
- SystemResetService 85 is FORBIDDEN: its audit row would stay local while all others become UTC.

### D4 — display/export

No shared date converter exists. XAML StringFormat: UsersView 362/589/650 (LastLoginDate, ActionDate), SourcesView 805
(CalibrationDate), 978 (DeletedAt), LocationDetailsWindow 255, LeakTestsView 206/209, BorrowView 249/252/255,
BorrowFormWindow 308/384/388/477, AlertsView 308 (CreatedAt), ReportsView 324/325/531/532/590/695/753/778 (`d`).
C# formatting: ReportingService (30+ sites: yyyy/MM/dd, yyyy/MM/dd HH:mm[:ss]), ViewModels (~15), audit JSON `yyyy-MM-dd`
in services (~10), BackupService file names. INSTANT display sites needing the shared formatter in C: ~10 XAML bindings plus the
ReportingService / Deletions / Users export columns for CreatedAt, DeletedAt, ActionDate, LastLoginDate, AttachedAt, MovedAt.
(D4 inventory from code-explorer; line numbers to be re-verified before C.)

### D5 — tests on the real clock

510 `DateTime.Now/Today/UtcNow` reads in 48 test files. Highest: BorrowServiceTests 55, BorrowViewModelAndDueSoonTests 47,
SourceServiceTests 39, AlertServiceTests 32, LeakTestServiceTests 18, UserServiceTests 15, ReportsViewModelTests 14,
ActivityCalculatorDoseRateTests 12, SystemResetServiceTests 10, AutoBackupServiceTests 6, DecayCalculationServiceTests 5.
Midnight/month-end sensitive: Borrow* (Today ± n days), LeakTest* (Today.AddMonths(6)), AlertServiceTests (Now − calib),
UserServiceTests (lockout). No TimeProvider / FakeTimeProvider / IClock exists in production or tests.

### Addendum investigation (read-only; nothing changed)

1. **Decay is NOT at day resolution.** Every decay path uses `DateTime.Now` (DecayCalculationService 19;
   NeutronDecayCalculationService 22/158; ReportsVM 461/476; AlertService 220/237), and current activity is recomputed on
   every load (SourceService 53/78). SRC-0106 stored CalibrationDate = 2026-09-14 20:46:36.73 → at 20:53:00 elapsed =
   240.106 h = 39.951 T½ → 98.2 × 0.5^39.951 = **9.239E-011** mCi (screenshot 9.23E-011). Midnight→midnight (240 h)
   would give 9.353E-011, which does not match. The "exactly 240 h" is because calibration was saved at ~20:46
   (VM default `DateTime.Now`) and the screenshot was taken at ~20:53. Group B must therefore pin INSTANT-resolution decay
   (hour/minute sensitive), not day resolution. The 23:30 / 00:30 local tests remain mandatory.
2. **No float path, no clamp.** No `float`/`Single`/epsilon/min-clamp in the production decay code. SRC-0029 is
   multi-isotope (Co-57 + F-18); the F-18 row has its own CalibrationDate 2026-09-14 19:12:18.76 → elapsed 14 500.7 min =
   132.185 T½ → 82.1 × 0.5^132.185 = **1.327E-038** mCi in double (screenshot 1.33E-038). The 2026-02-01 date shown is the
   source-level / Co-57 calibration (the minimum), not the F-18 row's. The resemblance to float32 min-normal is a
   coincidence. No change proposed.

### Split proposal (architect decision required)

- **Round 202 (calculation seam + pinning, no storage change):** A for the CALENDAR + DECAY/ELAPSED + INSTANT-comparison
  sites (≈65: decay, alerts, due dates, borrow, leak tests, dashboard, lockout comparisons); B characterization tests
  (23:30 / 00:30 local, month boundary, F-18 / Tc-99m / long-lived, neutron, alerts, leak/borrow, dashboard); make the
  D5 midnight-sensitive tests clock-independent. No C, no D.
- **Round 203 (instants to UTC):** A for the remaining INSTANT writes / non-persisted sites (≈60) + C (UTC write,
  EF converter, one shared formatter) + the latent D3 sites moved in the same commit (lockout, audit today counts,
  audit/alert date filters) + E + F. Open decision for 203: SystemResetService 85 is FORBIDDEN — allow a one-line
  timestamp change, or accept one local-time audit row type.
- TestDataGeneratorService (16): static accessor or out of scope (test data only).

---

## Architect decisions

0. Both addendum premises are withdrawn — your data-backed findings stand: decay is minute-resolution against local
   Now using stored calibration timestamps (with time of day); no float path, no clamp. Record the correction.

1. UTC storage for INSTANT fields is REJECTED permanently (final decision, record it in release-readiness §4 with this
   reason): single lab computer, Libya UTC+2 with no DST, zero local/UTC mismatches exist today (no UtcNow in
   production), and converting would CREATE four mismatches (lockout expiry, audit "today" counts, date filters,
   reset audit row) across ~144 sites. There is NO round 203 for UTC. Groups C and D are cancelled.

2. Round 202 continues as ONE round with this scope (your "round 202" part of the split):
   A  TimeProvider seam (pure refactor, GetLocalNow — keep local time everywhere) for the CALENDAR, DECAY/ELAPSED and
      lockout-comparison sites (~65). INSTANT write sites stay as they are.
   B  Characterization tests with a fixed FakeTimeProvider calling REAL production code: minute-resolution decay
      against local Now using stored calibration timestamps (include SRC-0106-like Tc-99m and an F-18 case, a
      long-lived nuclide, neutron emission decay), low-activity alerts, leak-test due/overdue, borrow due-soon/overdue,
      lockout expiry, dashboard counts — at 23:30 and 00:30 local and at a month boundary. They must pass after A.
   E  Make the clock-dependent tests found in D5 deterministic (FakeTimeProvider), prioritising those sensitive to
      midnight/month end. CI runs in UTC while the lab runs in UTC+2 — note which tests this affects.
   F  Docs (includes committing the round doc with discovery findings and these decisions).
   Commit prefixes stay R202-A, R202-B, R202-E, R202-F. Group-B tests must pass unchanged after A (same CORE RULE).

3. TestDataGeneratorService: out of scope (dev tool).
4. SystemResetService.cs: untouched (still forbidden; no longer relevant without UTC).
5. Do not commit the round doc separately now — it goes into R202-F.

Supplement (architect read Services/DecayCalculationService.cs on main 35dbc0e):

- Confirmed from the code: CalculateCurrentActivity calls CalculateActivityAtDate(..., DateTime.Now); elapsed time is
  in seconds; the whole path is double (Math.Pow(0.5, elapsed / halfLifeSeconds)); no float, no clamp.
- Group A, DecayCalculationService specifically: route CalculateCurrentActivity (DateTime.Now), GenerateDecayCurve
  (DateTime.Now) and the two DateTime.Today fallbacks in GetSourceCompositeDecayCurve through the TimeProvider.
  CalculateActivityAtDate already takes the date as a parameter — keep it unchanged and reuse it. Same approach for
  NeutronDecayCalculationService.
- 6. Record in «قائمة ما بعد الإصدار» for round 205 (do NOT change now): DecayCalculationService.ConvertToSeconds
  silently treats any unknown half-life unit as years (`_ => value * 365.25 * 86400`), and maps "m" to minutes
  (ambiguous with months; months = 30 days).

### Lead implementation decisions (2026-09-24)

- Seam helper: one extension class `TimeProviderExtensions` in production with `LocalNow(this TimeProvider)` =
  `DateTime.SpecifyKind(tp.GetLocalNow().DateTime, DateTimeKind.Local)` and `LocalToday(this TimeProvider)` =
  `tp.LocalNow().Date`. With `TimeProvider.System` this equals `DateTime.Now` / `DateTime.Today` exactly (value and
  Kind=Local), so A is behaviour-neutral. `.LocalDateTime` is NOT used (it would re-convert to the machine zone and
  defeat a fake UTC+2 zone on a UTC CI runner).
- Injection: services and ViewModels take a trailing optional constructor parameter `TimeProvider? timeProvider = null`
  (`?? TimeProvider.System`); `TimeProvider.System` registered as singleton in DI. Existing constructor call sites and
  tests keep compiling unchanged.
- Static accessor `AppClock` used ONLY for entity members that cannot take DI: AllModels 776 (`User.IsLocked`),
  896 (`LeakTestRecord.TestDate` initializer), 929 (`LeakTestRecord.StatusDisplay`). Backed by an `AsyncLocal`
  override so tests can scope a fake clock without leaking across parallel test classes.
- Lockout scope = comparison sites only (UserService 50/52, AllModels 776, UsersVM 188/239/247, ReportingService 946,
  plus UsersVM 271 Today). UserService 64 (LockoutEnd write) stays `DateTime.Now` per decision 2A (INSTANT write);
  lockout-expiry tests set LockoutEnd explicitly.

---

## Implementation notes (lead/implementer, 2026-09-24, post-A/B/E)

### Group A — sites actually changed (65/65, base 35dbc0e line numbers as listed in the contract)

All 65 contracted sites were verified present at the stated line and converted:
`SourcesViewModel` (169,832,913,1152,1157,1304), `NeutronSourceService` (140,142,233,235),
`SourceService` (101,195,576), `BorrowViewModel` (113,120,274,318,321,422,428,484,576),
`LeakTestsViewModel` (51,52,216,265), `ActivityCalculatorViewModel` (44,45,431,432), `AllModels`
(896,929 via `AppClock.Current`), `BorrowService` (232,272,292), `LeakTestService` (70),
`AlertService` (54,220,237), `DecayCalculationService` (19,140,214,215,291),
`NeutronDecayCalculationService` (22,158), `ReportsViewModel` (461,476), `DashboardViewModel`
(1395,1476,1558,1559,1561,1587,1589), `UserService` (50,52), `AllModels` (776 via `AppClock.Current`),
`UsersViewModel` (188,239,247,271), `ReportingService` (946). None were skipped; none required
declining a site. All other `DateTime.Now`/`DateTime.Today` reads (INSTANT writes, file-name
timestamps, report header stamps, backup, logger, `MainViewModel` inactivity clock, `SystemResetService`,
`TestDataGeneratorService`) were left untouched exactly as the contract requires.

**Unplanned but required compile-time extension:** `AlertService.CalculateMaxHalfLivesElapsed` and
`ReportsViewModel.CalculateMaxHalfLivesElapsed` (both `public static`) and
`ReportsViewModel.GetLowActivityAlertSources` (`private static`) could not read an instance
`_timeProvider` field from a static method. Each got one added trailing optional parameter
(`TimeProvider? timeProvider = null`, default `TimeProvider.System` — value/Kind-neutral, same
behaviour-preserving pattern as the constructor parameters). Every in-class caller
(`AlertService.GenerateAlerts`, `ReportsViewModel.LoadReport` ×2, `ReportsViewModel.GetLowActivityAlertSources`
internal call) now passes `_timeProvider` explicitly. External callers in `AlertsViewModel.cs` and
`DashboardViewModel.cs` (×2) were not touched — they keep the default `TimeProvider.System`, which is
identical to their pre-round behaviour. This is a deviation from the letter of the contract's site list
(a compile necessity, not a scope choice); no behaviour or value changed.

### AppClock sites (static, no DI possible)

`Models/AllModels.cs`: `User.IsLocked` (776), `LeakTestRecord.TestDate` initializer (896),
`LeakTestRecord.StatusDisplay` (929). Backed by `AsyncLocal<TimeProvider?>`; `AppClock.Override(tp)`
returns an `IDisposable` restoring the previous value, used by Group B/E tests
(`Round202TimeHandlingCharacterizationTests.LeakTestRecord_StatusDisplay_...`).

### UsersViewModel 239/247 — EF vs in-memory

`ApplyUsersFilter()` starts with `var query = Users.AsEnumerable();` where `Users` is an in-memory
`ObservableCollection<User>` already loaded by `LoadData()`. The subsequent `.Where(...)` clauses
(including 239/247, now `_timeProvider.LocalNow()`) execute as plain LINQ-to-Objects against that
collection — there is no `IQueryable`, no SQL translation, and therefore no `ToQueryString()`/SQL
semantics to compare before/after. Confirmed by reading the field declaration and the method body;
no STOP was required.

### Package version (Group B)

`Microsoft.Extensions.TimeProvider.Testing` **10.10.0** (net8.0 target lib present in the package;
no explicit lower pin was requested — this was the latest version resolved by NuGet at the time of
the round and it builds/tests clean against `net8.0-windows`).

### Group B coverage (30 new tests, `Round202TimeHandlingCharacterizationTests.cs`)

- Minute-resolution decay: SRC-0106-like Tc-99m and SRC-0029-like F-18 cases pinned to the exact
  contract literals (9.238773496698382E-011 mCi and 1.3265170327085314E-038 mCi respectively,
  computed independently from tick-exact elapsed seconds, relative tolerance < 1e-9).
- Long-lived nuclide (Cs-137) decay, neutron emission decay (`NeutronDecayCalculationService`), and
  `AlertService.CalculateMaxHalfLivesElapsed` — each parametrised over the four contracted fixed
  clocks (23:30 local, 00:30 local next day, 2026-09-30 23:30 local, 2026-10-01 00:30 local).
- Leak-test due/overdue filtering (`LeakTestService.GetAllRecords`) and `LeakTestRecord.StatusDisplay`
  via `AppClock.Override` — same four fixed clocks.
- Borrow overdue/due-soon (`BorrowService.CheckAndUpdateOverdue`/`GetDueSoonCount`) — same four
  fixed clocks.
- Lockout expiry (`UserService.Login`) with explicit `LockoutEnd` — same four fixed clocks, both the
  "still locked" and "just expired" boundary.
- **Root-cause note (STOP-and-report, resolved before committing B):** the first draft of
  `CreateFakeClock` built the UTC instant as `new DateTimeOffset(localDateTime, TimeSpan.FromHours(2))`
  and passed it to `FakeTimeProvider.SetUtcNow`. Measured experimentally (isolated probe project):
  `FakeTimeProvider.SetUtcNow` does not normalise a non-zero-offset `DateTimeOffset` to a true UTC
  instant before applying `SetLocalTimeZone`'s offset again on `GetLocalNow()`, so the local
  offset was applied twice (a local 20:53 input was read back as 22:53, a 4-hour, not 2-hour,
  shift). Fixed by passing an explicit UTC (`Offset = TimeSpan.Zero`) `DateTimeOffset`
  (`localDateTime.AddHours(-2)`) to `SetUtcNow`. Confirmed independently in the probe (deleted,
  never committed) before touching the real test file — this was a test-harness bug, not a
  production defect, so no expectation was altered to work around it.
- Dashboard counts: not exercised through a full `DashboardViewModel` construction (it pulls a large
  DI graph via `App.ServiceProvider` for several optional services). Coverage taken instead through
  the same static half-lives helper `DashboardViewModel`/`AlertService` both call internally
  (`AlertService.CalculateMaxHalfLivesElapsed`), which is the actual seamed computation feeding the
  dashboard's low-activity counts. Full-VM dashboard-counts coverage is a documented deviation
  (see Deviations below).

### Group E — D5 determinism table

| File | DateTime.Now/Today sites converted | Remaining real-clock reads | UTC-vs-UTC+2 CI sensitivity |
|---|---|---|---|
| `BorrowServiceTests.cs` | 58 (all) | 0 | Was sensitive (ExpectedReturnDate/Today races at midnight); now clock-independent. |
| `BorrowViewModelAndDueSoonTests.cs` | 47 (all) | 0 | Was sensitive (same due/overdue pattern via VM+service); now clock-independent. |
| `LeakTestServiceTests.cs` | 18 (all) | 0 | Was sensitive (NextDueDate/Today race); now clock-independent. |
| `LeakTestsViewModelTests.cs` | 3 (all) | 0 | Was sensitive (`FormTestDate`/`FormNextDueDate` defaults vs mocked `CalculateNextDueDate`); now clock-independent. |
| `AlertServiceTests.cs` | 32 (all) | 0 | Was sensitive (leak-test due filters + half-lives-elapsed boundary cases); now clock-independent. One exact-boundary floating-point finding below. |
| `UserServiceTests.cs` | 0 (not converted) | 15 | **Not converted — assessed, found not midnight/month-end sensitive.** All 15 reads build full-precision `DateTime` offsets (`AddMinutes`, seconds-level tolerance windows) compared against `_timeProvider.LocalNow()` inside `UserService.Login`; none truncate to `.Date`/`Today`, and every safety margin is minutes wide (10–15 min), so a live-clock race of a few milliseconds between the test's `DateTime.Now` and the service's `TimeProvider.System.LocalNow()` cannot flip the assertion, at midnight or otherwise. Lockout-expiry-at-midnight is already covered independently in Group B (`UserService_Login_LockoutExpiry_AtFixedClocks_...`). |
| `DecayCalculationServiceTests.cs` | 0 (not converted) | 5 (3 in one test + 2 unrelated) | **Not converted — assessed, found not midnight/month-end sensitive.** The only `DateTime.Today` reads are in `GetSourceCompositeDecayCurve_MultiIsotopeSource_GeneratesCompositeSum`, used solely as an explicit non-null `CalibrationDate` (so the seamed `Today`-fallback branch is never exercised by this test) and never compared against a second independent clock read. |

**Finding recorded, not fixed in Group E (test-data hardening only):**
`AlertServiceTests.GenerateAlerts_SingleIsotopeSource_ClassifiesSeverityAccurately` at exactly
`halfLivesElapsed = 5.0` and `6.0` failed once the fake clock removed the small positive real-clock
drift that used to push the computed ratio comfortably past the `>=` threshold. A 1-second epsilon
was added to the two exact-boundary `[InlineData]` cases' calibration date (test data only — no
assertion, severity value, or comparison operator changed) to make the intended "at/above threshold"
behaviour deterministic instead of accidentally depending on floating-point rounding direction.

### Deviations

1. `AlertService`/`ReportsViewModel` `CalculateMaxHalfLivesElapsed` (+`GetLowActivityAlertSources`)
   needed an added optional `TimeProvider?` parameter beyond the 65 listed sites, because they are
   `static` methods that cannot read an instance field — see "Unplanned but required compile-time
   extension" above. No behaviour change; external callers keep the default system clock.
2. Group B does not construct a full `DashboardViewModel` for the "dashboard counts" characterization
   item; it covers the same underlying seamed computation (`AlertService.CalculateMaxHalfLivesElapsed`)
   that feeds the dashboard's low-activity counters instead. See "Group B coverage" above.
3. `AlertServiceTests` exact-half-life-boundary test data received a 1-second epsilon nudge (test data,
   not assertions) — see "Finding recorded" above.
4. No `R202-<letter>-fix` commits were needed; all four commits (A, B, E, F) landed clean on the first
   attempt after the fixes described above were applied before each commit.
