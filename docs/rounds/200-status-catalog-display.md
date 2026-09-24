ROUND 200 — Status catalog (batch 1 of 2): one source of truth for status display, no stored-value change

Save THIS TEXT VERBATIM as docs/rounds/200-status-catalog-display.md (you may append a separate
"Discovery findings" section below it). Commit it in the round's PR.
BASE: main at aa1e98d (round 199, PR #95 merged). Branch: refactor/round-200-status-catalog-display
(if a name collision forces a different name, report it as a deviation).
Risk: MEDIUM (wide but display-only). Full pipeline: implementer -> your own diff review -> change-verifier ->
ci-monitor. One commit per group (A, B, C, D), prefix "R200-<letter>:". Fixups as separate "R200-<letter>-fix"
commits (never amend, not even before push), reported as deviations.

STEP 0 — Worktree housekeeping: same rules as round 199 STEP 0. Report the table.

STEP 1 — Discovery (read-only). STOP and report before coding if any STOP rule fires.
D1. How status is STORED: for Source, NeutronSource and any other entity with a status (borrow requests, leak
    tests, users?), the column type and the exact stored values (quote the model property and every distinct
    literal written to it). Is there already a status "code" concept (round 195 found LocationDetailsViewModel
    mapping «في المخزن»/«مخزن» to a Storage code)? Quote it.
    STOP RULE: if stored values are not a fixed, known set (e.g. free text typed by users), stop and report.
D2. Full inventory of status literals in production code: every Arabic/English status string, SimpleArabicStatus
    (all copies), status-to-color mappings, status converters, and XAML triggers on status text. For each:
    file:line, and classify as DISPLAY (text/color shown to the user) or LOGIC (comparison, filter, query,
    report selection, alert rule, save).
D3. Which DISPLAY sites show Arabic in the English UI (e.g. the «مخزن» badge in the neutron list and details).
D4. Every place a status string is written into an audit-log message (audit log must stay Arabic — final decision).
D5. Existing tests that assert status text or colors.

STEP 2 — Changes (DISPLAY only in this round)
A. Status catalog: one central type (e.g. an enum of status codes + a single StatusCatalog) that maps each STORED
   value to: code, Arabic display text, English display text (via resource keys ar + en with exact Arabic
   fallback), and ONE color. Parsing unknown stored values must not throw: return an explicit Unknown code that
   displays the raw stored text. Pure, unit-tested. No change to stored values.
B. Route every DISPLAY site from D2 through the catalog: badges, list cells, detail windows, dashboard, converters,
   XAML triggers, report/export DISPLAY text only if it is purely presentational. Remove duplicate SimpleArabicStatus
   copies in favour of the catalog. Unify the status colors to the catalog (list the before/after color per status).
C. Tests: catalog round-trip for every stored value (ar + en text, color); unknown value -> Unknown + raw text;
   English UI shows English status text in the neutron list, neutron details, sources list and dashboard; Arabic UI
   text unchanged character-for-character from today's; existing status tests pass unchanged unless they assert a
   color that was intentionally unified (list each such change).
D. Documentation: session-summary.md round 200 entry (CI Release and local Debug counts separately);
   release-readiness.md: header (main at round 199 / aa1e98d, round 200 under review); record the D1/D2 inventory
   counts, the color unification table, and the list of LOGIC sites reserved for round 201.

FORBIDDEN
- Any LOGIC site from D2 (comparisons, filters, queries, alert rules, saves, report selection) — reserved for 201.
- Any change to stored values, schema, or seed data. No EF migration.
- Audit-log message text (stays Arabic). The hardcoded role name «مدير النظام» (round 201).
- LoginWindow, LoginView, SplashWindow. PhraseFactoryResetConfirmation / RequiredResetPhrase, SystemResetService.
  Date/time handling (round 202). Calculator math (round 205).
- No `git add .` / `-A`; explicit pathspecs; `git show --stat` per commit with a one-line justification per file.

GIT
- Commits in order, push, open a DRAFT PR to main. Do not merge. CodeRabbit is not a merge requirement.

REPORT
- STEP 0 table.
- D1–D5 findings (D2 as counts per file + DISPLAY/LOGIC split; full list in the round doc).
- Per commit: hash, files, justification. The color before/after table.
- Test counts: local Debug and CI "Build and Test" (CI baseline 1362, Debug baseline 1364; state new tests).
  Confirm the TestDataIsolation sentinel tests passed.
- CI warnings vs the 3 known CS8604.
- Worktree path holding the final commit + exact PowerShell commands in lowercase for Edrees's visual check.
- A step-by-step visual checklist (numbered, one action per step, with the expected result) covering every screen
  whose status display changed, in BOTH Arabic and English UI (remember: after changing language, restart the app
  — that is by design). Include the navigate-away-and-back loop after each screen. SAFE actions only: no deletions,
  no new records, no status changes on real records.

---

## Discovery findings (STEP 1, lead, 2026-09-24, on aa1e98d)

STEP 0: PR #95 merged -> worktree `agent-a7b99adc2b526de25` (fix/round-199, 75bf947 ⊂ PR head d37a85e) removed
without --force; `git-log-output-check-82b87f` (detached at PR #95 head d37a85e) unregistered, but its now-empty
folder is held open by another process ("Device or resource busy") — kept, delete later. `round-198-auth-test-isolation-338bd3`
kept: its only untracked file is an older draft of the committed 198 contract (committed copy is a superset);
removing would need --force.

D1 (stored values) — STOP rule NOT fired.
- `Source.Status` (Models/AllModels.cs:247) `string`, MaxLength 30, default "InUse".
- `NeutronSource.Status` (AllModels.cs:1069) `string`, Required, MaxLength 30, default "Storage".
- Both written only from fixed sets: SourceFormWindow.xaml:481-484 ComboBox (non-editable, `SelectedValuePath="Tag"`,
  Tags InUse/Storage/Waste/Transfer) -> SourcesViewModel.EditStatus -> :1238 (neutron) / :1408 (source);
  BorrowService.cs:162 "InUse", :211 "Storage"; NeutronSourceService.cs:166/:297 blank -> "Storage" (else trimmed input);
  TestDataGeneratorService.cs:181-235/390/430/464/532 (Storage/Waste/Transfer/InUse). No import path writes Status.
  Set = {InUse, Storage, Waste, Transfer}. Legacy XAML-only values never written: Active, Decayed, Disposed, Lost.
- `BorrowRequest.Status` (AllModels.cs:685) Required string: Pending/Approved/Rejected/Delivered/Returned/Overdue.
  Separate lifecycle domain — see decision below. `User` has no Status column (`StatusDisplayName` from IsActive,
  AllModels.cs:775); LeakTest has only computed `StatusDisplay` (AllModels.cs:931, due-date based).
- Existing code concept: LocationDetailsViewModel.cs:126-138 `MapFilterToStatusCode`
  ("قيد الاستخدام"/"InUse"->InUse, "مخزن"/"في المخزن"/"Storage"->Storage, "نفايات"/"Waste"->Waste,
  "قيد النقل"/"نقل"/"Transfer"->Transfer, else raw).

D2 (production inventory, Source/NeutronSource status family; D=DISPLAY, L=LOGIC)
- Models/AllModels.cs: 3 maps — Source.ArabicStatus :323 (D; also feeds audit :383 and filter logic), Source.SimpleArabicStatus :333
  (D, duplicate), NeutronSource.ArabicStatus :1089 (D), NeutronSource.StatusColor :1099 (D). 4 D / 0 L.
- Converters/AllConverters.cs: StatusToColorConverter :53 (D, unused in XAML), StatusToArabicConverter :71 (D). 2 D.
- ViewModels/SourceDetailsViewModel.cs: ArabicStatus :56, StatusColor :57-64 (D). 2 D.
- ViewModels/SourcesViewModel.cs: row ArabicStatus :69, neutron row ArabicStatus/StatusColor :87-88 (D);
  search on Status :501/:558/:712, filter :510/:566, EditStatus :170/:833/:920/:1043/:1238/:1327/:1408 (L). 3 D / 11 L.
- ViewModels/LocationsViewModel.cs: rows ArabicStatus :48, :67, StatusColor :68 (D). 3 D.
- ViewModels/ReportsViewModel.cs: rows ArabicStatus :79/:94/:116 (D), Status :30/:95/:115 (D input to converter);
  active filters :179/:208/:295 (L). 6 D / 3 L. (:42 is BorrowRequest.)
- ViewModels/NeutronSourceDetailsViewModel.cs: StatusArabic :175, StatusColor :176 (D). 2 D.
- ViewModels/LocationDetailsViewModel.cs: StatusFilterOptions Arabic list :72-75, MapFilterToStatusCode :126-138,
  filter :149-150/:180-181 (L). 0 D / 4 L.
- ViewModels/DashboardViewModel.cs: AvailableStatuses raw codes :454 (D text of a L-bound combo; SelectedItem stays code),
  filter :1283, KPI :1729/:1794 (L). 1 D / 3 L.
- ViewModels/LeakTestsViewModel.cs:95, Services/AlertService.cs:46, SourceService.cs:51/:74/:594 (L). 5 L.
- Services/SourceService.cs: audit msg :383 (audit — untouched), restore user message :480-481 (D), audit JSON :359/:487 (audit). 1 D.
- Services/ReportingService.cs: status cells :294, :396, :578, :657, :697, :748, :774, :1330, :1413, :1469, :1545 (D, export).
  11 D. (:446/:532/:857 are BorrowRequest; :150/:238 are LocationType.)
- Views: SourcesView.xaml list badge :555-626 (16 DataTriggers + SimpleArabicStatus, D), neutron badge :859-860 (D),
  deletions ColLastStatus :1037 (D), filter combos :257-264/:773-780 (L-bound, text already localized);
  SourceDetailsWindow.xaml:49-50, NeutronSourceDetailsWindow.xaml:47-48, LocationDetailsWindow.xaml:253/:303,
  ReportsView.xaml:284/:326/:492/:533/:696/:754/:777 (D); SourceFormWindow.xaml:481-484 (L-bound input, localized).
- Resources: StatusInUse/StatusStorage/StatusWaste/StatusTransfer (ar :404-407, en :407-410). Note ar StatusStorage =
  «في المخزن», while every model map says «مخزن».

D3 (Arabic shown in English UI): every site bound to ArabicStatus/SimpleArabicStatus/StatusArabic — sources list badge,
neutron list badge, source details, neutron details, location details (both grids), reports grids :326/:533/:696/:754/:777,
sources deletions tab, restore success message, all 11 export cells. Also dashboard filter shows raw English codes in the
Arabic UI, and LocationDetails filter options are Arabic-only (LOGIC — 201).

D4 (audit): SourceService.cs:383 `$"حذف مصدر: … (الحالة السابقة: {source.ArabicStatus})"`; audit JSON ArabicStatus
:359 and :487. NeutronSourceService audit JSON carries raw `Status` only (:194/:261/:320/:360/:440). All stay Arabic/raw.

D5 (tests): SourceDetailsViewModelTests.cs:70-71/164-165/213-214 (ArabicStatus + #3FAE7A/#4F7FA3/#E0A93E);
NeutronSourcesUITests.cs:620-626 (filter «مخزن», ArabicStatus); LocationDetailsViewModelTests.cs:259-305 (filter + ArabicStatus);
E2E :162/:295 (BorrowRequest ArabicStatus).

## Lead decision (before implementation)

1. Scope of the catalog = the Source/NeutronSource status family {InUse, Storage, Waste, Transfer} + Unknown.
   BorrowRequest statuses are a separate lifecycle and are deferred (reported as a deviation/interpretation).
2. `ArabicStatus` stays on the models and row wrappers with byte-identical output (now produced by the catalog):
   it feeds audit text/JSON (D4) and LOGIC filters (LocationDetails :150/:181) and is asserted by tests.
   New display properties `StatusDisplay` (localized) and `StatusColor` are added; XAML/exports/messages switch to them.
   `SimpleArabicStatus` is deleted.
3. Arabic display texts = today's badge texts: «قيد الاستخدام», «مخزن», «نفايات», «قيد النقل» via NEW keys
   `StatusDisplayInUse/Storage/Waste/Transfer` (en: In Use / In Storage / Waste / In Transfer). The existing
   `StatusXxx` keys (filter/form combos) are untouched.
4. Colors = design tokens (majority today): InUse #3FAE7A, Storage #4F7FA3, Waste #E0A93E, Transfer #E0A93E, Unknown #9E9E9E.
5. Only intended Arabic text change: ReportsView :284/:492 (StatusToArabic converter) Storage «في المخزن» -> «مخزن»,
   aligning them with the other five report grids. Declared deviation from C.

## Implementation notes (round-implementer, 2026-09-24)

Environment note: the sandbox's git isolation bound this session to worktree `agent-a3b4ea77e6c7872ad`
(branch `main` @ `aa1e98d`), not the `round-200-status-catalog-5d8531` worktree named in the task. Both
were at the identical base commit, so the branch `refactor/round-200-status-catalog-display` was created
and all work done from the sandbox-permitted worktree. No content impact; reported as a deviation.

Commits:
- **R200-A** (`bc040a5`): `Sources-System-Project/Helpers/StatusCatalog.cs` (new), `Resources/Strings.ar.xaml`,
  `Resources/Strings.en.xaml`. New `SourceStatusCode` enum + `StatusCatalog` static class (Parse/GetArabicText/
  GetDisplayText/GetColorHex), 4 new resource keys `StatusDisplayInUse/Storage/Waste/Transfer` (ar+en).
- **R200-B** (`8940d56`): 16 files — `Models/AllModels.cs` (Source/NeutronSource gain `StatusDisplay`/
  `StatusColor`, `ArabicStatus` reimplemented via catalog, `SimpleArabicStatus` deleted),
  `Converters/AllConverters.cs` (StatusToColorConverter/StatusToArabicConverter delegate to catalog,
  new `HexToTintBrushConverter`), `Resources/Converters.xaml` (registers `HexToTintBrush`),
  `Services/ReportingService.cs` (11 export cells -> StatusDisplay), `Services/SourceService.cs`
  (restore message -> StatusDisplay, audit text/JSON untouched), `ViewModels/LocationsViewModel.cs`,
  `ViewModels/NeutronSourceDetailsViewModel.cs`, `ViewModels/ReportsViewModel.cs`,
  `ViewModels/SourceDetailsViewModel.cs` (StatusColor -> catalog, unknown color #1F5A66 -> #9E9E9E),
  `ViewModels/SourcesViewModel.cs` (row wrappers gain StatusDisplay), `Views/DashboardView.xaml`
  (status filter ItemTemplate), `Views/LocationDetailsWindow.xaml`, `Views/NeutronSourceDetailsWindow.xaml`,
  `Views/ReportsView.xaml` (3 of 5 status columns switched; 2 BorrowRequest columns left untouched),
  `Views/SourceDetailsWindow.xaml`, `Views/SourcesView.xaml` (16 DataTriggers replaced by 2 bindings +
  HexToTintBrush).
- **R200-C** (`6624cc6`): `Sources.Tests/StatusCatalogTests.cs`, `Sources.Tests/Round200StatusDisplayTests.cs`
  (new, 50 tests total). Debug 1414/0/0, Release 1412/0/0, TestDataIsolationSentinelTests 6/6.
- **R200-D**: `docs/session-summary.md`, `docs/release-readiness.md`, `docs/rounds/200-status-catalog-display.md`
  (this file, committed with its Implementation notes appended).

No fixups (`R200-<letter>-fix`) were needed in this round.

Deviations: see the declared deviations list in session-summary.md §الجولة 200 and release-readiness.md
§الجولة 200 (StatusToArabic Storage text change, "Active" legacy value now Unknown instead of InUse,
SourceDetailsViewModel unknown color #1F5A66 -> #9E9E9E, SourceService restore message/audit variable
split, and the sandbox worktree-path substitution noted above).
