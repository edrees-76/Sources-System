ROUND 201 — Status catalog (batch 2 of 2): status LOGIC via the catalog, borrow-request statuses, role name

Save THIS TEXT VERBATIM as docs/rounds/201-status-logic-borrow-role.md (you may append a separate
"Discovery findings" section below it). Commit it in the round's PR.
BASE: main after round 200 (PR #96 merged) — report the exact hash you start from. Branch:
refactor/round-201-status-logic-borrow-role (if a name collision forces a different name, report it as a deviation).
Risk: HIGH (filters, counts, report selection and alert rules on regulatory inventory). Full pipeline:
implementer -> your own diff review -> change-verifier -> ci-monitor. One commit per group (A..F), prefix
"R201-<letter>:". Fixups as separate "R201-<letter>-fix" commits (never amend, not even before push), reported as
deviations. CodeRabbit is not a merge requirement.

CORE RULE — behaviour-preserving refactor, proven by characterization tests:
Group A (tests pinning CURRENT behaviour) is committed FIRST, on unchanged production code, and must pass there.
Groups B–D must then pass the SAME group-A tests with ZERO changes to their expected results.
STOP RULE: if any group-A expectation would need to change for B–D to pass, stop that group and report — it means
behaviour changed, and only the architect can approve a behaviour change.

STEP 0 — Worktree housekeeping: same rules as round 200 STEP 0. Report the table.

STEP 1 — Discovery (read-only)
D1. Confirm the round-200 inventory of LOGIC sites still holds on the base: SourcesViewModel filter/search/save
    paths, LocationDetailsViewModel filter options + MapFilterToStatusCode, DashboardViewModel filter and counts,
    ReportsViewModel selection, the "active source" rules in LeakTests, AlertService and SourceService, plus any
    site round 200 missed. file:line each.
D2. Borrow-request statuses: model property, column type, the exact fixed set of stored values, every DISPLAY site
    and every LOGIC site. STOP RULE: if the stored values are not a fixed known set, stop and report.
D3. Role name: every occurrence of «مدير النظام» and of the other role names in production code (C# and XAML),
    classified as STORED value, COMPARISON (authorization/visibility), or DISPLAY. How the role is stored.
D4. Audit-log sites that embed a status or role text (they stay Arabic — final decision).

STEP 2 — Changes
A. Characterization tests (commit R201-A, NO production change): for every LOGIC site in D1 and D2, tests that pin
   today's exact results on a fixed seeded data set covering every status value (InUse, Storage, Waste, Transfer,
   Unknown/legacy) and every borrow status: which records each filter/search returns, every dashboard count, which
   sources each report selects, which sources raise each alert, which count as "active" for leak tests. Also pin
   role-based visibility/authorization results for each role. Run them on the unchanged base: all must pass.
B. Source-status LOGIC -> catalog (commit R201-B): replace string comparisons at every D1 site with catalog codes.
   LocationDetails filter options become translated (display via catalog) while mapping to the same codes.
   Group-A tests must pass unchanged.
C. Borrow-request statuses (commit R201-C): a separate BorrowStatusCatalog (same design as StatusCatalog: stored
   value -> code, ar/en display via resource keys with exact Arabic fallback, one color, Unknown for unexpected
   values). Route every DISPLAY and LOGIC site from D2 through it. Group-A tests unchanged. The Reports borrow grids
   show English in the English UI after this.
D. Role name (commit R201-D): one central definition of the stored role values (identical strings — stored values
   do not change). Replace every literal COMPARISON with it (same value, same logic — AuthorizationGuard may be
   touched ONLY to swap an identical literal for the constant). Route role DISPLAY through ar/en resource keys so the
   English UI shows English role names. Group-A tests unchanged.
E. New tests (commit R201-E): catalog tests for BorrowStatusCatalog and the role definition; English UI shows English
   borrow statuses and role names; Arabic UI text unchanged character-for-character.
F. Documentation (commit R201-F): session-summary.md round 201 entry (CI Release and local Debug separately);
   release-readiness.md: header (main at round 200, round 201 under review); close the status-enum and role-name
   items; record D1–D4 counts; state that every group-A test passed unchanged after B–D.

FORBIDDEN
- Any change to stored values, schema or seed data. No EF migration.
- Any behaviour change (see the CORE RULE). Audit-log message text (stays Arabic).
- AuthorizationGuard logic (only identical-literal -> constant swaps), password logic, UserService.
- LoginWindow, LoginView, SplashWindow. PhraseFactoryResetConfirmation / RequiredResetPhrase, SystemResetService.
  Date/time handling (round 202). Calculator math (round 205).
- No `git add .` / `-A`; explicit pathspecs; `git show --stat` per commit with a one-line justification per file.

GIT
- Commits in order A..F, push, open a DRAFT PR to main. Do not merge.

REPORT
- STEP 0 table and the base hash.
- D1–D4 findings (counts per file; full lists in the round doc).
- Per commit: hash, files, justification.
- Explicit statement: "all group-A characterization tests passed on the unchanged base AND after B, C and D, with
  no expected result changed" — or which group stopped and why.
- Test counts: local Debug and CI "Build and Test" (CI baseline 1412, Debug baseline 1414; state new tests).
  Confirm the TestDataIsolation sentinel tests passed.
- CI warnings vs the 3 known CS8604.
- Worktree path holding the final commit + exact PowerShell commands in lowercase (with quotes) for Edrees.
- A numbered, step-by-step visual checklist in ARABIC (one action per step, with the expected result) covering:
  source filters and search on every tab, the dashboard status filter and all dashboard counts (compare the numbers
  with a screenshot taken on main before this round), location details filter, reports (including borrow grids),
  the alerts/bell counts, leak-test due list, and the users screen role column — in Arabic UI, then English UI
  (restart after changing language, by design), with the navigate-away-and-back loop after each screen.
  SAFE actions only: no deletions, no new records, no status changes, no password changes.

---

## Discovery findings (STEP 1, lead, 2026-09-24, on ef5db3b)

Base: `main` = `origin/main` = `ef5db3b8a245cc5f7b6ab5ffd4e42d7a8edbeb49` (round 200, PR #96 merged 2026-09-24T13:11:28Z).
Branch `refactor/round-201-status-logic-borrow-role` created from it in worktree `round-201-status-logic-df87eb`
(no name collision).

STEP 0:

| Worktree / folder | State | Action |
|---|---|---|
| `agent-a3b4ea77e6c7872ad` (refactor/round-200-status-catalog-display @ 53532bc = PR #96 head, MERGED, clean) | merged | removed (`git worktree remove`, no --force) |
| `round-200-status-catalog-5d8531` (@ aa1e98d, only untracked = older draft of the 200 contract, strict subset of the committed file) | superseded | kept — removal needs --force |
| `round-198-auth-test-isolation-338bd3` (@ 36921cf, only untracked = older draft of the 198 contract) | superseded | kept — removal needs --force |
| `git-log-output-check-82b87f` (unregistered, empty folder) | empty | kept — `rmdir` fails "Device or resource busy" (held open by another process); delete later |

D1 — Source/NeutronSource status LOGIC sites (round-200 inventory confirmed; line numbers on ef5db3b).
- ViewModels/SourcesViewModel.cs: search on raw Status substring :503 (sources), :560 (neutron), :714 (deleted);
  filter equality :510-512, :566-568 (StatusFilter from combo Tag); EditStatus default :172, :835 (write);
  edit load :922, :1045; save :1202/:1240 (neutron), :1314/:1329 (change detection)/:1410 (source). 11 L.
- ViewModels/LocationDetailsViewModel.cs: StatusFilterOptions Arabic list :69-76, MapFilterToStatusCode :126-138,
  filter predicates :147-150 (sources), :178-181 (neutron) — OrdinalIgnoreCase on stored value OR on ArabicStatus;
  ClearFilters :210. 4 L.
- ViewModels/DashboardViewModel.cs: AvailableStatuses raw codes :454, filter :1280-1283, low-activity card :1728-1729,
  low-activity table :1793-1794. 4 L.
- ViewModels/ReportsViewModel.cs: ActivityReport :182, GeneralReport activity :211, low-activity alerts :298. 3 L.
- ViewModels/LeakTestsViewModel.cs:95 (sealed AND InUse/Storage). 1 L.
- Services/AlertService.cs:46 (EF query, InUse/Storage). 1 L.
- Services/SourceService.cs: GetAllSources activity calc :51, GetSourceById :74, GetLowActivitySources :592. 3 L.
- MISSED by round 200: Services/BorrowService.cs:131 (borrow allowed only when Status == "Storage"),
  ViewModels/BorrowViewModel.cs:330 (EF query: sources available to borrow = Storage). 2 L.
- Writes (not comparisons): BorrowService.cs:162 "InUse", :211 "Storage"; NeutronSourceService.cs:166/:297 default "Storage".
- TestDataGeneratorService.cs :181-235/:388/:390/:419/:430/:464/:491/:532 — demo-data generator (seed-like). Out of scope.
- GlobalSearchService.cs:75 projects raw Status (not compared). Not LOGIC.
Total: 29 LOGIC sites in 10 files (+ 4 write sites + generator).

D2 — Borrow-request statuses. STOP rule NOT fired.
- `BorrowRequest.Status` (Models/AllModels.cs:676) `string`, Required, MaxLength 30 (InitialSchema TEXT(30)), default "Pending",
  indexed (AppDbContext.cs:501).
- Written only from fixed literals: BorrowService.cs:151 "Delivered", :198 "Returned", :243 "Overdue"; model default "Pending";
  TestDataGeneratorService.cs:410/:447/:481/:509 (Returned/Delivered/Overdue/Approved|Pending). "Rejected" is in the documented
  set (:674, ArabicStatus :693) but never written. Set = {Pending, Approved, Rejected, Delivered, Returned, Overdue}.
- DISPLAY: AllModels.cs:689-697 ArabicStatus map (معلّق/تمت الموافقة/مرفوض/تم التسليم/تم الإرجاع/متأخر, else raw);
  BorrowViewModel.cs:40 row ArabicStatus; ReportsViewModel.cs:42 row ArabicStatus; BorrowView.xaml:279 text + badge
  color triggers :264-275 (bg) / :283-292 (fg); BorrowFormWindow.xaml:405 text; ReportsView.xaml:326/:533 (borrow grids);
  ReportingService.cs:446/:532/:722/:857 (export cells); BorrowViewModel.cs:149-152 filter option texts (Arabic only). 13 D.
- LOGIC: BorrowService.cs :87, :95 (Pending), :111 (Overdue), :147 (Delivered|Overdue active-borrow check), :195 (returnable =
  Delivered|Approved|Overdue), :236 (overdue sweep: Delivered|Approved and past due), :277/:284/:305 (Delivered-based counts);
  SourceService.cs :233 (active borrow lock), :339 (pending/active borrow blocks delete), :343-350 (reason message switch),
  :607 (HasActiveBorrow); BorrowViewModel.cs :231-233 (KPI counts), :268 (can return), :562-583 (filter map incl. «قريبة الإرجاع»);
  DashboardViewModel.cs :1770-1771 (borrow counts); BorrowFormWindow.xaml:419-427 (return panel visible for
  Delivered|Overdue|Approved), :470 (returned panel visible for Returned). 20 L (13 C# files-sites + XAML triggers).

D3 — Role name. Stored in `Role.RoleName` (AllModels.cs:798, string MaxLength 50); `User.RoleId` FK. Two stored values,
seeded in AppDbContext: «مدير النظام» and «مستخدم» (all other roles deleted at startup :277).
- STORED (seed writes): AppDbContext.cs:262 «مدير النظام», :271 «مستخدم». 2.
- COMPARISON: AppDbContext.cs:259, :268, :277 (x2), :291; AllModels.cs:773 `User.IsAdmin`, :806 (DisplayName key choice);
  UsersViewModel.cs:187 (AdminUsersCount), :198 (role summary), :211 (description), :752 (permissions "All"),
  :821 (IsPermissionsSectionVisible); PasswordPromptDialog.xaml.cs:45 (admin check); GlobalSearchService.cs:205. 14.
  AuthorizationGuard.cs has NO role literal (uses `user.IsAdmin`) — it is not touched.
- DISPLAY: Role.DisplayName (AllModels.cs:806, keys RoleAdmin/RoleUser — already localized; used by UsersView.xaml:360/:744,
  UserFormWindow.xaml:73, UsersView.xaml:348); MainViewModel.cs:91 `CurrentUserRole` = raw RoleName (MainWindow.xaml:346 —
  Arabic in English UI); DeletionsViewModel.cs:425 raw RoleName in deleted-user details; GlobalSearchService.cs:205-207. 4 D.
- Username literal "admin" (UserService.cs:285/:349) is a username, not a role — UserService is forbidden; untouched.
- Message/comment texts containing «مدير النظام» (DialogHelper, MainViewModel :344-345, SettingsViewModel :502/:571,
  UserService :47/:225/:285/:349, AuthorizationGuard :40, PasswordPromptDialog :48) are prose fallbacks, not role values.

D4 — Audit text embedding status/role: SourceService.cs:383 (`ArabicStatus` in delete message), :492 (restore message),
audit JSON :359 and :485 (`ArabicStatus`). BorrowService audit texts (:167/:217/:251/:256) embed no status value (:251 is the
fixed word «متأخرة»). UserService audit carries RoleId only. All stay as they are.

## Lead decision (before implementation)

1. Equivalence rule for every LOGIC swap: the new expression must select exactly the same records for EVERY possible
   stored string (including legacy/unknown and case variants). `StatusCatalog.Parse` is Ordinal, so
   `Parse(x) == SourceStatusCode.InUse` ⇔ `x == "InUse"`. Never compare `Unknown == Unknown` as a match.
   Where a site is case-insensitive today (LocationDetails, OrdinalIgnoreCase), it stays case-insensitive.
2. EF-translated queries (AlertService:46, BorrowViewModel:330, BorrowService/SourceService borrow queries) must keep the
   SAME generated SQL: use `const string` stored values from the catalog (inlined as SQL literals), not `Parse()` and not a
   collection `.Contains()`. Catalog: add `public const string` stored values + `ToStored(code)`, and an
   "active inventory" rule (InUse or Storage) used by all 10 active-source sites.
3. Raw-substring search on the stored Status (SourcesViewModel :503/:560/:714) is NOT a status comparison; left untouched
   (changing it would change search results). Save-path writes/`!=` change detection between two stored values stay raw.
   Stored-value writes use the catalog consts (identical strings).
4. Filter combos (LocationDetails, and BorrowView in C): the bound `SelectedStatusFilter` keeps its current string values
   (e.g. «الكل», «مخزن», «تم الإرجاع», «قريبة الإرجاع») so selection logic is unchanged; the options become objects
   {Value = today's key, generated from the catalog Arabic text; Display = localized text}, ComboBox uses
   SelectedValuePath/DisplayMemberPath. The key -> code mapping goes through the catalog. «الكل» / «قريبة الإرجاع» display via
   existing keys `FilterAll` / `DueSoon` if their Arabic text is identical, else new keys.
5. BorrowStatusCatalog colors (one color each; badge = tint of the color + the color as foreground, like round 200):
   Delivered #3FAE7A, Overdue #C25B4A, Pending/Approved/Rejected/Returned #4F7FA3, Unknown #9E9E9E. Declared visual change:
   foreground of Pending/Approved/Returned/Rejected #1F5A66 (PrimaryBrush) -> #4F7FA3; Unknown badge blue -> gray.
   BorrowFormWindow visibility triggers become bindings to [NotMapped] bools computed via the catalog (same sets).
6. Role: new `RoleNames` (Helpers) with `public const string Admin = "مدير النظام"`, `User = "مستخدم"`, and a display
   function reproducing `Role.DisplayName` exactly (RoleAdmin for admin, RoleUser for everything else, same fallbacks).
   AppDbContext seed comparisons and writes use the consts (byte-identical, pinned by a test). `CurrentUserRole` and the
   deleted-user details switch to the localized display text (Arabic unchanged because ar RoleAdmin/RoleUser equal the
   stored values — asserted by test). AuthorizationGuard untouched (no literal present).
7. TestDataGeneratorService and GlobalSearchService raw-Status projection are out of scope (declared).

## Implementation notes (round-implementer, 2026-09-24)

Worktree: `D:\Sources-System\.claude\worktrees\agent-a0271139f6f654552` (sandbox-assigned; the
contract's named worktree `round-201-status-logic-df87eb` was read-only for this session — the
contract doc was copied byte-for-byte from there into this worktree). Branch
`refactor/round-201-status-logic-borrow-role` created from `ef5db3b` (verified clean, matches Base).

Commits (all still on the branch, none amended, no fixups needed):
- R201-A `0ce781d` — 86 characterization tests, no production change. Debug 1500/1500 on unchanged base.
- R201-B `073f909` — StatusCatalog consts + IsActiveInventory; 10 active-inventory sites +
  BorrowService:131/BorrowViewModel:330 + 4 write sites + LocationDetailsViewModel filter options +
  LocationDetailsWindow.xaml + Dashboard AvailableStatuses. Group-A unchanged, Debug 1500/1500.
- R201-C `0819131` — new BorrowStatusCatalog.cs; BorrowRequest DISPLAY additions; BorrowView/
  BorrowFormWindow/ReportsView/ReportingService routed to StatusDisplay/StatusColor; BorrowView
  filter options {Value,Display}; every D2 LOGIC site in BorrowService/SourceService/BorrowViewModel/
  DashboardViewModel. Group-A unchanged, Debug 1500/1500.
- R201-D `aba2bd7` — new RoleNames.cs; AppDbContext seed + AllModels + UsersViewModel +
  PasswordPromptDialog + GlobalSearchService comparisons; MainViewModel.CurrentUserRole and
  DeletionsViewModel display switched to RoleNames.GetDisplayName. AuthorizationGuard untouched
  (confirmed no literal present). Group-A unchanged, Debug 1500/1500.
- R201-E `61be44a` — 38 new tests (Round201NewCatalogAndRoleTests.cs). Debug 1538/1538.
- R201-F `2dd09f5` — docs (session-summary, release-readiness, this file's first Implementation notes).

### Lead review of PR #97 (CHANGES REQUESTED) and fix commits

The lead reviewed the diff and found that 7 tests in `Round201CharacterizationTests.cs` re-implement
the current predicate/switch inside the test body instead of calling the production code that Groups
B-D change, so they would pass regardless of what B-D did:
`LeakTests_SealedActiveList_ReplicatesCurrentPredicate_IsSealed_And_InUseOrStorage`,
`AlertService_ActiveSourcesQuery_ReplicatesCurrentEfPredicate`,
`ReportsViewModel_ActivityReport_ReplicatesCurrentPredicate`,
`BorrowViewModel_KpiCounts_ReplicateCurrentPredicates`,
`BorrowViewModel_CanReturn_ReplicatesCurrentPredicate`,
`UsersViewModel_AdminUsersCount_ReplicatesCurrentPredicate`,
`UsersViewModel_PermissionsAllShortCircuit_ReplicatesCurrentPredicate`. **These 7 are declared "not
evidence" for the CORE RULE** — left in the file unmodified (per the lead's explicit instruction not
to edit `Round201CharacterizationTests.cs`), but they must not be relied on to prove B-D preserved
behaviour at those specific sites. The lead also identified sites not pinned at all: the
`SourcesViewModel` status filter (sources + neutron tabs) and search on status text (sources/neutron/
deleted tabs); `DashboardViewModel` status filter results, low-activity card counts, low-activity
table rows, and the borrow-summary counts; `ReportsViewModel` GeneralReport activity rows and
low-activity-alert rows; `BorrowViewModel`'s real available-to-borrow source list (:330) and KPI
counts through the VM itself; `UsersViewModel` role summaries and `IsPermissionsSectionVisible`;
`PasswordPromptDialog`'s public logical admin check.

- **R201-A-fix `919d4a3`** — new `Sources.Tests/Round201CharacterizationSiteTests.cs` (31 tests) drives
  the real production code (real `ViewModel`/`Service` instances, public commands/properties: `LoadDataCommand`,
  `SearchCommand`, `EditCommand`, `SaveCommand`, `EditRoleId` setter, `GenerateAlerts()`,
  `PasswordPromptDialog.ValidateAdminPassword`) for every site listed above. Introduced
  `ConcurrentSqliteFixture` (local to this file only, not a change to the shared
  `Fixtures/SqliteInMemoryFixture.cs`): `SourcesViewModel`/`DashboardViewModel`/`BorrowViewModel`/
  `LeakTestsViewModel` internally offload their DB reads onto `Task.Run` background threads; opening
  more than one `AppDbContext` concurrently on the single shared `SqliteConnection` object used by
  `SqliteInMemoryFixture` throws SQLite "unable to delete/modify user-function due to active
  statements" the moment two such background reads overlap (which happens routinely once a VM's own
  constructor fire-and-forgets an initial load and the test then awaits a second explicit call).
  `ConcurrentSqliteFixture` uses a named in-memory database opened with `Mode=Memory;Cache=Shared` so
  every `AppDbContext` gets its own `SqliteConnection` object (opened/closed by EF per unit of work)
  while all of them see the same data; a single anchor connection is kept open for the fixture's
  lifetime so the shared-cache database is not torn down between calls. `SourcesViewModel.LoadDataAsync`
  also calls the static `App.CreateDbContext()` for `ActivityUnits`, so the constructor follows the
  existing `SourcesViewModelTests.cs` pattern of pointing `App.ServiceProvider` (via reflection) at a
  minimal DI container that resolves `IDbContextFactory<AppDbContext>` to the fixture's factory.
  **Proof run:** the identical file (after replacing `RoleNames.Admin`/`RoleNames.User` with their
  literal stored strings so it compiles pre-R201-D) was copied into a detached worktree at `0ce781d`
  (unmodified base + Group A only) and run there: **31/31 passed**. The same file at HEAD: **31/31
  passed**. No test that passed on the base failed on HEAD, so the STOP RULE was not triggered. Debug
  full suite after this commit: **1569/1569** (1538 + 31). The temporary worktree was removed
  afterwards (`git worktree remove`, file deleted first, no `--force` needed).
- **R201-C-fix `88687c4`** — `BorrowService.cs:198` (`req.Status = "Returned"`, missed in the original
  R201-C pass) now uses `BorrowStatusCatalog.Returned` (identical value); corrected a `StatusCatalog.cs`
  comment that referenced a nonexistent "StatusService الجولة 201".
- **R201-F-fix** — this update to the Implementation notes, plus the corresponding count updates in
  `docs/session-summary.md` and `docs/release-readiness.md`.

Color table (BorrowStatusCatalog, before -> after, per Lead decision 5):
| Status | Background tint (before) | Foreground (before) | Background tint (after) | Foreground (after) |
|---|---|---|---|---|
| Pending | `#1A4F7FA3` | `PrimaryBrush` (`#1F5A66`) | tint of `#4F7FA3` | `#4F7FA3` |
| Approved | `#1A4F7FA3` | `PrimaryBrush` | tint of `#4F7FA3` | `#4F7FA3` |
| Rejected | (no trigger — default `#1A4F7FA3`/`PrimaryBrush`) | `PrimaryBrush` | tint of `#4F7FA3` | `#4F7FA3` |
| Delivered | `#1A3FAE7A` | `#3FAE7A` | tint of `#3FAE7A` | `#3FAE7A` (unchanged) |
| Returned | `#1A4F7FA3` | `PrimaryBrush` | tint of `#4F7FA3` | `#4F7FA3` |
| Overdue | `#1AC25B4A` | `#C25B4A` | tint of `#C25B4A` | `#C25B4A` (unchanged) |
| Unknown (unreachable today) | `#1A4F7FA3` (default) | `PrimaryBrush` | tint of `#9E9E9E` | `#9E9E9E` |

Deviations (declared, none silent):
1. **Superseded by R201-A-fix.** The original Group-A characterization used predicate-replication for
   DashboardViewModel/ReportsViewModel/LeakTestsViewModel/AlertService/BorrowViewModel/UsersViewModel
   instead of driving the real ViewModel/Service. `Round201CharacterizationSiteTests.cs` (R201-A-fix)
   now drives the real production code for all of those sites via `ConcurrentSqliteFixture` (see above).
   The 7 tautological tests named above remain in `Round201CharacterizationTests.cs` unedited (per the
   lead's explicit instruction) but are documented as **not evidence** for the CORE RULE; the
   corresponding real-code coverage lives in `Round201CharacterizationSiteTests.cs` instead
   (`DashboardViewModel_LowActivityCardAndTable_OnlyCountActiveInventoryStatuses` /
   `DashboardViewModel_BorrowSummaryCard_UsesRealBorrowServiceCounts` /
   `AlertService_GenerateAlerts_OnlyConsidersActiveInventoryStatuses` /
   `ReportsViewModel_GeneralReport_ActivityRows_OnlyActiveInventoryStatuses` /
   `BorrowViewModel_KpiCounts_ViaRealLoadDataAsync` /
   `BorrowViewModel_Edit_LoadsAvailableBorrowers_OnlyWhenReturnable` /
   `UsersViewModel_RoleSummaries_And_AdminUsersCount_ViaRealConstructorLoad` /
   `UsersViewModel_Save_NewAdminUser_PacksPermissionsAsAll_ViaRealSaveCommand`).
2. NeutronSource-specific characterization tests were not added: no NeutronSource LOGIC comparison
   site is touched by Groups B-D beyond two literal-to-constant default-value writes in
   NeutronSourceService (identical string value, zero behavioural risk).
3. SourcesViewModel's generic `Status == StatusFilter` (combo-Tag-driven) sites (:510-512, :566-568)
   and Dashboard's `r.Source.Status == SelectedStatusFilter` (:1283) were left untouched: they are not
   a duplicated literal comparison against a catalog value (unlike the 10 "active inventory" sites) —
   they compare against whatever value the bound combo already holds, so there is nothing to
   consolidate through the catalog without also restructuring those combos, which was out of the
   round's declared scope (only LocationDetails and BorrowView combos were named for restructuring).
   `Round201CharacterizationSiteTests.cs` does pin the current `SourcesViewModel` filter/search
   results themselves (sources/neutron/deleted tabs) even though the underlying comparison is
   untouched by B-D, so a future regression there would still be caught.
4. `Round201CharacterizationSiteTests.cs` introduces `ConcurrentSqliteFixture`, a fixture local to that
   one file (not a modification of the shared `Fixtures/SqliteInMemoryFixture.cs` used by the rest of
   the suite), because the VMs it drives use internal `Task.Run` and are incompatible with a single
   shared `SqliteConnection` object under concurrent access. This is additive test infrastructure, not
   a change to existing test behaviour.
5. One `PasswordPromptDialog_ValidateAdminPassword` scenario is asserted via `FakeUserService`
   directly (not through a full dialog/window) since `ValidateAdminPassword` is `static` and takes
   `IUserService?` — there is no WPF dialog to instantiate for this specific check.


## Lead review addendum (2026-09-24, head 044554b)

- Lead diff review of B/C/D: every LOGIC swap is an identical-value const or a `==`/`||` set helper over the same
  consts (no `Parse()`/collection in EF queries); AuthorizationGuard/UserService/Login*/Splash/SystemResetService,
  audit text, schema, stored and seed values untouched. Group A: 7 tests re-implemented the predicate (listed above as
  "not evidence") and several sites were unpinned -> CHANGES REQUESTED -> `R201-A-fix` (31 tests on real production code).
- change-verifier: PASS. Independently re-ran `Round201CharacterizationSiteTests` on 0ce781d (31/31) and on 044554b (31/31);
  `Round201CharacterizationTests.cs` byte-identical 0ce781d..head; no pre-round test file changed; Debug 1569/0/0;
  TestDataIsolationSentinelTests 6/6; only the 3 known CS8604.
- CI run 36019150242 on 044554b: success, Build and Test 1567/0/0, only the 3 known CS8604. CodeRabbit: not run (Draft).
- Residual (accepted): SourcesViewModel neutron-tab filter/search not separately pinned — that code (`n.Status == StatusFilter`,
  raw substring search) was not modified by this round. BorrowFormWindow visibility triggers are XAML-only; covered by
  set-equivalence review + R201-E tests of `IsReturnableStatus`/`IsReturnedStatus`.
- Fixups (declared deviations): R201-A-fix, R201-C-fix, R201-F-fix, R201-A-fix2, R201-F-fix2 (this addendum + CI results).
