# Round 193 — Neutron list: add "Current Activity" column + documentation catch-up (rounds 189–193)

## Identity

- Owner: Edrees
- Lead: Claude Code
- Base branch: `main`
- Base commit: `c473746eaed9010e8bf3f7935e6e37a65ab570cd`
- Working branch: `feature/round-193-neutron-list-current-activity`
- Risk: `medium`
- Parallel-safe: `yes` (isolated worktree, no schema/auth/lifecycle impact)

## Goal

Add a "النشاط الحالي" (Current Activity) column to the neutron sources DataGrid in
`SourcesView.xaml`, showing the decay-corrected current activity in the source's recorded unit,
formatted identically to `NeutronSourceDetailsViewModel.CurrentActivityDisplay`
(e.g. "99.0338 MBq"). Show "-" when not calculable.

## Evidence and diagnosis (discovery, verified)

a) `NeutronSourceListRow` (SourcesViewModel.cs:79-91) exists with
   `CurrentEmissionRateDisplay { get; set; } = "-"` (round 192), populated in
   `UpdatePagedNeutronSources()` (SourcesViewModel.cs:628-642) via
   `BuildCurrentEmissionRateDisplay(n)` (SourcesViewModel.cs:649-658).

b) `SourcesViewModel.FormatActivityValue(double, string)` (SourcesViewModel.cs:1719-1727) is
   character-identical in logic to `NeutronSourceDetailsViewModel.FormatActivityValue`
   (NeutronSourceDetailsViewModel.cs:144-152) — same N4/N0/N2/E3 branching. Signature differs only
   in instance vs static, which is irrelevant to output.

c) `CalculateCurrentSourceActivity` (NeutronDecayCalculationService.cs:157-158, delegating to
   `CalculateSourceActivityAtDate`) keys off **`CalibrationDate`**
   (NeutronDecayCalculationService.cs:206, 215, 252) — NOT `EmissionCalibrationDate`. Tests must set
   `CalibrationDate`.

d) No resource key exists with Arabic text exactly "النشاط الحالي" AND an English pairing usable as
   a DataGrid column header with a matching semantic role to the round-192 pattern
   (`HeaderCurrentEmissionRate`). Existing keys (`ColCurrentActivity`, `LabelCurrentActivity`,
   `DetailLabelCurrentActivity`, `FieldNameCurrentActivity`, `RptColCurrentActivity`,
   `ColCurrentActivityBilingual`) serve other contexts (grid columns elsewhere, detail labels,
   reports) and reusing any of them risks unrelated coupling. Per contract step (d) tie-break: add
   ONE new key `HeaderCurrentActivity` to `Strings.ar.xaml` / `Strings.en.xaml`, mirroring
   `HeaderCurrentEmissionRate`'s placement.

e) Round-192 column "معدل الانبعاث الحالي" (SourcesView.xaml:830-839): `DataGridTemplateColumn`,
   `Header="{DynamicResource HeaderCurrentEmissionRate}"`, `Width="Auto" MinWidth="140"`, cell
   template wraps a `Border Background="#1A0D9488" CornerRadius="4" Padding="6,3" Margin="4,2"
   HorizontalAlignment="Center"` containing
   `TextBlock FontWeight="Bold" Foreground="#0D9488" FlowDirection="LeftToRight"`. The uncertainty
   column `HeaderUncertaintyPercent` (SourcesView.xaml:842) immediately follows it, before
   `ColStatus` (SourcesView.xaml:845).

Additional verified facts:
- `NeutronSource.ActivityUnit` (AllModels.cs:1110-1111) is the `ActivityUnit?` navigation property;
  `ActivityUnit` (AllModels.cs:183-197) has `ConversionToBq` (double) and `UnitSymbol` (string).
- `NeutronSourceDetailsViewModel.CurrentActivityDisplay` success branch (lines 103-118): if
  `result.IsCalculated && result.CurrentActivityBq.HasValue`, and `unit != null &&
  unit.ConversionToBq != 0` → `FormatActivityValue(bq / unit.ConversionToBq, unit.UnitSymbol)`;
  else → `FormatActivityValue(bq, "Bq")`. Otherwise a per-status message string (list version
  collapses all of these to "-", matching round 192's `BuildCurrentEmissionRateDisplay` pattern).

## Architectural decision

Mirror the round-192 pattern exactly: a computed row-level display string built once per page load,
reusing the existing decay service and formatting helper without modifying either. This keeps the
list a thin read-only projection and avoids duplicating decay/business logic in XAML or introducing
a value converter, consistent with how `CurrentEmissionRateDisplay` was implemented. A new resource
key is added rather than reusing an existing "النشاط الحالي" key, since none of the existing keys
share this column's semantic role (round-192-style DataGrid header) and reuse would create implicit
coupling between unrelated UI surfaces.

## Allowed files

- `Sources-System-Project/ViewModels/SourcesViewModel.cs`
- `Sources-System-Project/Views/SourcesView.xaml`
- `Sources-System-Project/Resources/Strings.ar.xaml`
- `Sources-System-Project/Resources/Strings.en.xaml`
- `Sources.Tests/SourcesViewModelTests.cs`
- `docs/session-summary.md`
- `docs/release-readiness.md`
- `docs/rounds/193-neutron-list-current-activity.md` (this file)

Any additional file requires a reported deviation and lead approval before editing.

## Forbidden scope

- `LoginWindow`, `LoginView`, `SplashWindow` (any file)
- `UpdateDisplaySourceCurrentActivity`, `FormatActivityValue`, `NeutronSourceDetailsViewModel`,
  `NeutronDecayCalculationService`, or any other method/service
- Any EF migration
- Any file not listed in Allowed files
- `git add .` / `git add -A` — stage explicit pathspecs only
- Direct push to `main`
- PR merge

## Acceptance criteria

1. `NeutronSourceListRow.CurrentActivityDisplay` shows the decay-corrected current activity in the
   source's recorded unit, formatted identically to
   `NeutronSourceDetailsViewModel.CurrentActivityDisplay`'s success branch; "-" when not calculable.
2. New DataGrid column bound to `CurrentActivityDisplay`, header via `DynamicResource`, placed
   immediately after the uncertainty column, styled identically to the round-192 column.
3. Three new tests pass (half-life decay case, null-activity case, list/detail consistency case).
4. No new build warnings beyond the 3 known `CS8604` (LoginWindow.xaml.cs:104,:199,
   ViewInstantiationTests.cs:218), possibly reported as 5 due to wpftmp duplication.
5. `docs/session-summary.md` and `docs/release-readiness.md` updated in the same commit.

## Required commands

```powershell
dotnet test
```

Run from the worktree root; capture full pass/fail/skip counts.

## Migration protocol

Not applicable — no schema change.

## Expected test baseline

- Debug/local expected count: 1291 + 3 + 2 (known `#if DEBUG` TestDataGeneratorTests) = 1296
- Release/CI expected count: 1291 + 3 = 1294
- Documented conditional-test difference: Debug-only `TestDataGeneratorTests` under `#if DEBUG`
  account for the +2 Debug/CI gap (pre-existing, not introduced by this round).

## Visual verification by Edrees

List the exact screens/states to capture from `bin\Debug\net8.0-windows`:
1. Neutron sources list (السجلات → المصادر النيترونية) showing the new "النشاط الحالي" column
   populated for a source with a recorded activity value and calibration date.
2. Same grid for a source with no recorded activity value, confirming "-" is shown.
3. Column position confirmed immediately after "عدم اليقين %" and before the status column.

## Test template (mirror exactly, adapted for activity)

Existing round-192 tests live in `Sources.Tests/SourcesViewModelTests.cs` under
`#region Round 192: PagedNeutronSources Current Emission Rate Column` (lines ~989-1062). Add a new
`#region Round 193: PagedNeutronSources Current Activity Column` immediately after it, following the
same `_mockNeutronSourceService.Setup(s => s.GetAll())` + `CreateViewModel()` +
`vm.SelectedTab = "Neutron"` + `await vm.LoadNeutronDataAsync()` shape.

- T1 (half-life case): set `ActivityValue = 100`, `ActivityUnit = new ActivityUnit { UnitSymbol =
  "MBq", ConversionToBq = 1e6 }` (or however `ActivityUnit` is normally constructed/referenced in
  this file — check for an existing `ActivityUnit` test fixture/helper first and reuse it), and
  `CalibrationDate = DateTime.Now.AddDays(-halfLifeDays)` for the source's actual half-life (reuse
  the Am-241 constants from the round-192 test: `432.2 * NeutronDecayCalculationService.DaysPerYear`
  if using Am-241 again, or another isotope already fixtured in this file). Expected:
  `row.CurrentActivityDisplay` equals `$"{(50.0):N4} MBq"` computed via the exact `N4` format (do
  not hardcode the literal — build it with `50.0.ToString("N4")` + `" MBq"` or equivalent, per the
  original contract's culture-safety requirement).
- T2: `ActivityValue = null` → `Assert.Equal("-", row.CurrentActivityDisplay)`.
- T3 (consistency): construct `new NeutronSourceDetailsViewModel(source, decayService: <same
  INeutronDecayCalculationService instance/mock used by the ViewModel under test>)` and assert
  `row.CurrentActivityDisplay == detailsVm.CurrentActivityDisplay` for the same `NeutronSource`
  instance from T1's setup (or a fresh equivalent one). Constructor:
  `NeutronSourceDetailsViewModel(NeutronSource source, IUserService? userService = null,
  ISourceCertificateService? certificateService = null, INeutronDecayCalculationService?
  decayService = null)` (NeutronSourceDetailsViewModel.cs:29-34). Dispose the details VM after
  asserting if it registers with the messenger (check its constructor/base class first).

## Completion report requirements

- Base/result SHA.
- Draft PR URL.
- Per-file justification.
- Test passed/failed/skipped counts (local Debug and CI).
- Build warnings.
- Deviations or `none`.
- Remaining risks.
