# Round 129 — Surface radioactive activity (value + calculated current) in Neutron Source Details window

## Identity
- Owner: Edrees
- Lead: Claude Code
- Base branch: `main`
- Base commit: `300f2f14d742662580fa21506e349e08464139b4`
- Working branch: `round-129-neutron-activity-details`
- Risk: `medium` (read-only display addition; no schema/service logic change)
- Parallel-safe: `yes`

## Goal
In `NeutronSourceDetailsViewModel`/`NeutronSourceDetailsWindow.xaml`, display the neutron
source's radioactive activity (`ActivityValue`+`ActivityUnit`, generalized in the pre-merge
PR #16 correction from Am-241-specific naming) as an entered value AND its calculated
current value — mirroring exactly the existing two-row pattern already used in this same
window for `CalibratedEmissionRateFormatted`/`CurrentEmissionRateDisplay`. Round 128 wired
this pair into the edit form (`SourcesViewModel`/`SourcesView.xaml`) only, and explicitly
excluded this details window from its scope ("`NeutronSourceDetailsViewModel.cs`/`Window.xaml`
— unrelated read-only details/certificates window"). Verified in production by Edrees: a
neutron source (`edr-1`) with a saved activity value shows correctly in the edit form on
reopen, but its details window (opened via the eye/view icon in the sources list) shows no
activity information at all, while the analogous emission-rate field shows both its
calibrated and calculated-current rows there.

## Evidence and diagnosis
- `Sources-System-Project/ViewModels/NeutronSourceDetailsViewModel.cs`: exposes `DecayResult`
  (`_decayService.CalculateCurrentEmissionRate(NeutronSource)`), `CurrentEmissionRateDisplay`,
  `CalibratedEmissionRateFormatted` (reads `NeutronSource.CalibratedEmissionRateFormatted`).
  No property references `ActivityValue`/`ActivityUnit`/`CalculateCurrentSourceActivity` at
  all. Constructor already accepts `INeutronDecayCalculationService? decayService = null`
  defaulting to `new NeutronDecayCalculationService()` — no constructor change needed.
- `Sources-System-Project/Views/NeutronSourceDetailsWindow.xaml`: the "الخصائص الإشعاعية
  والانبعاث" card (`DetailCardNeutronEmissionProps`) has a `Grid` with 4 `RowDefinition
  Height="Auto"` rows: row 0 = calibrated rate + calibration date, row 1
  (`Grid.ColumnSpan="2"`) = current emission rate, row 2 = calibration reference + anisotropy
  factor, row 3 = uncertainty + average neutron energy. No row for activity.
- `Sources-System-Project/Models/AllModels.cs`, `NeutronSource` class: has `ActivityValue`
  (`double?`), `ActivityUnitId` (`Guid?`), `ActivityUnit` (non-virtual navigation, requires
  explicit `.Include()` per round 127's fix — already included by `NeutronSourceService`'s
  five read methods since round 127). Has `CalibratedEmissionRateFormatted` (`[NotMapped]`)
  as the precedent for a model-level formatted-value property; no equivalent exists for
  `ActivityValue`.
- `Sources-System-Project/Services/INeutronDecayCalculationService.cs`/
  `NeutronDecayCalculationService.cs`: `CalculateCurrentSourceActivity(NeutronSource?)`
  already exists (renamed from `CalculateCurrentAm241Activity` in the pre-merge correction)
  and returns `NeutronDecayResult.CurrentActivityBq` with statuses `Calculated`,
  `NotRecorded`, `MissingActivityUnit`, `InvalidActivityValue`, plus the shared statuses
  (`MissingSource`, `MissingCalibrationDate`, `CalculationDatePrecedesCalibrationDate`,
  `MissingSourceType`, `InvalidHalfLife`, `UnsupportedHalfLifeUnit`). No change needed here —
  read-only consumer.
- `Sources-System-Project/ViewModels/SourcesViewModel.cs`,
  `UpdateDisplaySourceCurrentActivity(NeutronSource? target)` (~lines 841-875): the existing,
  already-shipped (round 128) pattern for turning a `NeutronDecayResult` from
  `CalculateCurrentSourceActivity` into a display string, including the exact Arabic wording
  per status. `NotRecorded`/`MissingActivityUnit`/`InvalidActivityValue` are hardcoded Arabic
  literals there ("لم يُسجَّل", "غير محسوب — وحدة النشاط غير محمّلة", "غير محسوب — قيمة النشاط
  غير صالحة") — no `TranslationHelper` key exists for these three; an already-accepted,
  documented deviation from round 128, out of scope to fix here. The other statuses go
  through `TranslationHelper.GetString(...)` with keys confirmed present in both
  `Strings.ar.xaml`/`Strings.en.xaml` (`DecayStatusMissingCalibrationDate`,
  `DecayStatusMissingSourceType`, `DecayStatusMissingSource`, `DecayStatusInvalidHalfLife`,
  `DecayStatusUnsupportedHalfLifeUnit`, `DecayStatusDatePrecedesCalibration`).
- `Sources-System-Project/Views/SourcesView.xaml`: the edit-form labels for these fields are
  literal (non-`DynamicResource`) Arabic strings "النشاط الإشعاعي" (value), "وحدة النشاط"
  (unit), "النشاط الحالي المحسوب" (computed current value) — this is the exact wording to
  reuse for the new details-window row labels, for terminology consistency between the two
  screens (not the older Am-241-specific wording, which no longer exists post-correction).
- `Resources/Strings.ar.xaml`/`Strings.en.xaml`: unlike `SourcesView.xaml`'s edit-form
  labels, every existing label in `NeutronSourceDetailsWindow.xaml` already goes through a
  `DynamicResource` translation key (confirmed: `DetailLabelCalibratedEmissionRate`,
  `DetailLabelCurrentEmissionRate`, etc., present in both dictionaries, ~line 1271-1280 ar /
  ~1274-1283 en). `TextNotRecorded` ("غير مسجّل" / "Not recorded") already exists in both
  dictionaries and is already used elsewhere in this same ViewModel
  (`CalibrationReferenceDisplay`) for an analogous "value not provided" case.
- `Sources.Tests/TranslationKeysTests.cs`: `AllKeys_MustMatchBetweenArabicAndEnglishDictionaries`
  fails the build if any `x:Key` exists in one dictionary but not the other — confirmed by
  reading the file. Any new `DynamicResource` key must be added to **both** dictionaries in
  the same commit.
- `Sources.Tests/NeutronSourcesUITests.cs` (~line 856-862): existing test constructs a bare
  `new NeutronSourceDetailsViewModel(source)` (no mocks) and asserts against the real
  `NeutronDecayCalculationService` default via `CalibratedEmissionRateFormatted` — establishes
  the precedent for testing this ViewModel's computed properties without a mock decay
  service, by controlling the input `NeutronSource`'s dates/values directly.
- Inspection needed at implementation time: whether `IsCurrentEmissionRateCalculated` (already
  exposed by the ViewModel) is actually bound anywhere in the current XAML. Not observed in
  the read XAML content reviewed for this prompt — confirm by search before deciding whether
  its activity-equivalent needs a matching XAML binding or is inspection-only.

## Architectural decision
1. **Keep this window's existing full-translation quality bar** (every label already uses
   `DynamicResource`) rather than importing `SourcesView.xaml`'s literal-Arabic-label
   shortcut into a file that doesn't yet carry that debt. Add exactly two new resource keys
   — `DetailLabelActivity` and `DetailLabelCurrentActivity` — to **both** `Strings.ar.xaml`
   and `Strings.en.xaml`, positioned next to the existing `DetailLabelCalibratedEmissionRate`/
   `DetailLabelCurrentEmissionRate` keys. Arabic text: reuse the exact wording from
   `SourcesView.xaml` ("النشاط الإشعاعي:" / "النشاط الحالي المحسوب:", trailing colon matching
   this file's existing label convention). English text: match the existing style
   ("Radioactive Activity:" / "Current Calculated Activity:").
2. **Do not touch `NeutronDecayCalculationService`/`INeutronDecayCalculationService`/
   `NeutronSourceService`** — `CalculateCurrentSourceActivity` and the round-127 `.Include()`
   fix already provide everything needed; this round is a pure read-only consumer, exactly
   like the existing `CalculateCurrentEmissionRate` usage in this same ViewModel.
3. **Add to `NeutronSource` (`AllModels.cs`)**, mirroring the existing
   `CalibratedEmissionRateFormatted` `[NotMapped]` precedent exactly: a new
   `[NotMapped] public string ActivityValueFormatted` property returning
   `"{ActivityValue} {ActivityUnit.UnitSymbol}"` when both `ActivityValue` and `ActivityUnit`
   are set, or the hardcoded Arabic fallback `"غير مسجّل"` otherwise (do not call
   `TranslationHelper` from the model layer — no existing model property does; keep this
   consistent with `DisplayEmissionRate` and other model-level `[NotMapped]` properties).
4. **Add to `NeutronSourceDetailsViewModel.cs`**, directly beneath the existing
   `CalibratedEmissionRateFormatted`/`DecayResult`/`CurrentEmissionRateDisplay`/
   `IsCurrentEmissionRateCalculated` block, in the same order/shape:
   - `public string ActivityValueFormatted => NeutronSource.ActivityValueFormatted;` (thin
     pass-through, mirrors how `CalibratedEmissionRateFormatted` already delegates to the
     model).
   - `public NeutronDecayResult ActivityDecayResult => _decayService.CalculateCurrentSourceActivity(NeutronSource);`
   - `public string CurrentActivityDisplay` — same `switch` shape as `CurrentEmissionRateDisplay`,
     but on `ActivityDecayResult.Status`/`ActivityDecayResult.CurrentActivityBq`, `Calculated`
     case formatted as `"{value} Bq"` using the same numeric-magnitude formatting style as
     `SourcesViewModel.FormatActivityValue` (a small local private static formatting helper
     duplicated here is acceptable and matches this codebase's existing accepted pattern of
     small per-ViewModel formatting helpers rather than a new shared abstraction — do not
     introduce a new shared `Helpers` class in this round). For `NotRecorded`/
     `MissingActivityUnit`/`InvalidActivityValue`, use the exact same hardcoded Arabic strings
     already shipped in `SourcesViewModel.UpdateDisplaySourceCurrentActivity` for terminology
     consistency across the two screens — do not invent new wording. For the shared statuses,
     route through `TranslationHelper.GetString(...)` with the same existing keys
     `CurrentEmissionRateDisplay` already uses, exactly as that property does.
   - `public bool IsCurrentActivityCalculated => ActivityDecayResult.IsCalculated;` (mirrors
     `IsCurrentEmissionRateCalculated`; wire it into the XAML identically to how that property
     is used there if inspection shows it's consumed — otherwise keep it for parity/future
     use and note this in the report).
5. **Add to `NeutronSourceDetailsWindow.xaml`**, inside the existing "الخصائص الإشعاعية
   والانبعاث" card's `Grid`: add two new `RowDefinition Height="Auto"` rows beneath the
   existing four, following the exact same `StackPanel`/`TextBlock` label-then-value
   structure as the emission-rate rows — row 4: `DetailLabelActivity` +
   `{Binding ActivityValueFormatted}`; row 5 (`Grid.ColumnSpan="2"`, matching how
   `CurrentEmissionRateDisplay`'s row spans both columns): `DetailLabelCurrentActivity` +
   `{Binding CurrentActivityDisplay}`, styled identically (`FontSize="16" FontWeight="Bold"
   Foreground="#0D9488"`) to the current-emission-rate row, since both are "the
   calculated-today headline number" for their respective quantity.

## Allowed files
- `Sources-System-Project/Models/AllModels.cs`
- `Sources-System-Project/ViewModels/NeutronSourceDetailsViewModel.cs`
- `Sources-System-Project/Views/NeutronSourceDetailsWindow.xaml`
- `Sources-System-Project/Resources/Strings.ar.xaml`
- `Sources-System-Project/Resources/Strings.en.xaml`
- `Sources.Tests/NeutronSourcesUITests.cs`
- `docs/release-readiness.md`
- `docs/session-summary.md`

Any additional file requires a reported deviation and lead approval before editing.

## Forbidden scope
- `LoginWindow`, `LoginView`, `SplashWindow`
- `NeutronDecayCalculationService.cs`, `INeutronDecayCalculationService.cs`,
  `NeutronSourceService.cs` — read-only consumers in this round, no changes
- `SourcesViewModel.cs`, `SourcesView.xaml` — the edit form is already correct (round 128);
  do not touch it, and do not "fix" its hardcoded-Arabic-label deviation here — that is ب5's
  job
- Any migration file — no schema change
- Fixing the documented `UpdateSourceTrigger=LostFocus`/Enter-key issue — unrelated, tracked
  separately
- Unrelated cleanup or refactoring
- Extracting a new shared activity-formatting helper class — duplicate the small formatting
  logic locally in this ViewModel, matching the codebase's existing accepted pattern (see
  Architectural decision §4)
- Direct push to the protected/default branch
- PR merge

## Acceptance criteria
1. Opening the details window (eye icon) for a neutron source that has a saved
   `ActivityValue`/`ActivityUnitId` shows both the entered value+unit and a correctly
   computed current-activity value reflecting decay from `CalibrationDate` (test required,
   with a hand-verifiable expected number like round 126's/128's tests).
2. Opening the details window for a neutron source with no saved activity shows the neutral
   "لم يُسجَّل" state for both rows, not an error and not a blank space.
3. `ActivityValueFormatted` and `CurrentActivityDisplay` both correctly handle a source whose
   `ActivityUnit` navigation property was not `.Include()`-d (i.e. is `null` despite
   `ActivityUnitId` having a value) — reflect the existing `MissingActivityUnit`/fallback
   status rather than throwing a `NullReferenceException` (regression test required; this is
   the exact class of bug fixed in round 127 for the calculation service — the display layer
   must not reintroduce an equivalent crash/silent-wrong-value path).
4. `TranslationKeysTests` continues to pass (both new keys present in both dictionaries).
5. All existing tests continue to pass unmodified.
6. No new build warnings.
7. `docs/release-readiness.md` updated: close this gap explicitly, note the "current
   activity" display now has full coverage across both the edit form (round 128) and the
   details window (this round). Also correct the record to reflect that PR #16 has been
   merged to `main` (merge commit `300f2f14d742662580fa21506e349e08464139b4`, post-merge CI
   run `34235478693`, green, 1131/1131 Release) — the current file still frames this as
   pending.

## Required commands
```powershell
dotnet test -c Debug
dotnet test -c Release
```

## Migration protocol
Not applicable — no schema change, `[NotMapped]` property only.

## Expected test baseline
- Base: 1133 Debug / 1131 Release (per `release-readiness.md`; confirm this still matches
  actual `main` HEAD after the PR #16 merge before starting).
- Report exact resulting count; at least 3 new tests expected (calculated case, not-recorded
  case, missing-unit-navigation-property regression case).

## Visual verification by Edrees
**Required.** After merge, build and run from `bin\Debug\net8.0-windows`: open the neutron
sources list, click the eye/view icon on `edr-1` (or any source with a saved activity
value), confirm the details window now shows both the entered activity value+unit and a
plausible calculated current value; then open a neutron source with no saved activity and
confirm it shows the neutral message, not an error or blank row. Provide a real screenshot
of both states.

## Completion report requirements
- Base/result SHA, Draft PR URL, per-file justification, exact test counts, build warnings,
  deviations or `none`, remaining risks.