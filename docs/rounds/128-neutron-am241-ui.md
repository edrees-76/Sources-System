# Round 128 — ب4 UI (final): wire Am241Activity value+unit and display calculated current activity

## Identity
- Owner: Edrees
- Lead: Claude Code
- Base branch: `main`
- Base commit: `8daf1393f41ff2e782a91633cbac804db5a4b83c`
- Working branch: `round-128-neutron-am241-ui`
- Risk: `high` (final UI round of ب4; scientific reference data entry/display on a regulated entity)
- Parallel-safe: `no`

## Goal
Wire `NeutronSource.Am241ActivityValue`/`Am241ActivityUnitId` into the unified
source form (value + **selectable unit dropdown**, per Edrees's explicit
requirement — mirror `Source`'s existing `EditInitialActivity`/
`EditInitialUnitId` unit-chooser pattern, not a fixed unit), and display the
calculated current Am-241 activity (via round 126/127's
`CalculateCurrentAm241Activity`) when viewing/editing an existing neutron
source. This is the final ب4 round — after this, ب4 is functionally complete.

## Evidence and diagnosis
- `Sources-System-Project/ViewModels/SourcesViewModel.cs`:
  - `ActivityUnits` (`ObservableCollection<ActivityUnit>`) is already loaded
    once in `LoadDataAsync()` and shared across both the regular-`Source`
    and neutron form sections — no new data loading needed for the unit
    dropdown.
  - `Source`'s reference pattern for "value + selectable unit": `EditInitialActivity`
    (double)/`EditInitialActivityText` (string, parsed via
    `NumericInputParser.TryParseFinite` in `OnEditInitialActivityTextChanged`)/
    `EditInitialUnitId` (`Guid?`, bound to an `ActivityUnits`-backed
    `ComboBox`). Mirror this shape, but **nullable** (`double?`) since
    `Am241ActivityValue` is optional, matching `EditCapsuleLengthMm`'s
    nullable-optional style from round 125 rather than `Source`'s
    required-value style.
  - No `INeutronDecayCalculationService` is currently injected into this
    ViewModel. Add it as an optional constructor parameter defaulting to
    `new NeutronDecayCalculationService()`, mirroring exactly how
    `IDecayCalculationService? decayService = null` already defaults to
    `new DecayCalculationService()` in this same constructor.
  - `EditNeutronSource(NeutronSource target)` currently populates all
    editable fields from `target` but not `Am241ActivityValue`/
    `Am241ActivityUnitId` (round 126 added the model fields; round 127
    fixed the read-path `.Include()`; this round is the first to read them
    into the form).
  - `NeutronSourceService.Create`/`Update` already reject (server-side) the
    case where exactly one of value/unit is set (round 126). The
    established client-side pattern (matching `EditCapsuleLengthText`'s
    pre-save guard) is a UX-only duplicate check before calling the
    service — add an equivalent "both-or-neither" guard for
    `Am241ActivityText`/`Am241ActivityUnitId`.

## Architectural decision
1. **`SourcesViewModel.cs`**:
   - Constructor: add `INeutronDecayCalculationService? neutronDecayService = null`
     parameter, store as `_neutronDecayService`, default to
     `new NeutronDecayCalculationService()`.
   - Add properties: `EditAm241ActivityValue` (`double?`),
     `EditAm241ActivityText` (`string`), `EditAm241ActivityUnitId` (`Guid?`),
     with a `partial void OnEditAm241ActivityTextChanged` parser mirroring
     `OnEditCapsuleLengthTextChanged` exactly (empty → null,
     `NumericInputParser.TryParseFinite` otherwise).
   - Add a read-only display property (e.g. `DisplayAm241CurrentActivity`,
     `string`) computed after `EditNeutronSource` loads an existing record:
     call `_neutronDecayService.CalculateCurrentAm241Activity(target)` and
     format each `NeutronDecayCalculationStatus` case into a short Arabic
     string (`Calculated` → the value with its Bq unit and appropriate
     scientific/engineering formatting mirroring `FormatActivityValue`'s
     existing style; `NotRecorded` → a neutral "لم يُسجَّل" message, not an
     error; other statuses → their existing meaning, e.g.
     `MissingCalibrationDate` → a message saying calibration date is
     needed). For a **new** record (`IsNew == true`), leave this empty —
     there is nothing to calculate yet.
   - `ClearForm()`: reset the three new edit fields and the display
     property to empty/null.
   - `EditNeutronSource(...)`: populate `EditAm241ActivityValue`/
     `EditAm241ActivityText`/`EditAm241ActivityUnitId` from `target`, then
     compute `DisplayAm241CurrentActivity` as described above.
   - `SaveAsync()`'s neutron branch: add the both-or-neither guard (mirror
     `EditCapsuleLengthText`'s guard structure) before constructing
     `neutronSource`, then set `Am241ActivityValue = EditAm241ActivityValue`,
     `Am241ActivityUnitId = EditAm241ActivityUnitId` on the constructed
     object.
2. **`SourcesView.xaml`**: inside the neutron-form section, add:
   - A value `TextBox` + unit `ComboBox` pair (bound to `ActivityUnits`,
     `DisplayMemberPath`/`SelectedValuePath` matching how `EditInitialUnitId`'s
     existing `ComboBox` for the regular-`Source` form is wired — mirror
     that control's exact style/structure, don't invent a new one).
   - A read-only text block bound to `DisplayAm241CurrentActivity`, visible
     only when editing an existing record (not `IsNew`), styled like an
     adjacent existing read-only/computed display if one exists in this
     form (check `Source`'s current-activity display area for the closest
     precedent).
3. No change to `NeutronSourceService.cs`, `NeutronDecayCalculationService.cs`,
   any migration, or `NeutronSourceTypesViewModel.cs`/`Window.xaml` (that
   screen's photon-ratio field is already done, round 125).

## Allowed files
- `Sources-System-Project/ViewModels/SourcesViewModel.cs`
- `Sources-System-Project/Views/SourcesView.xaml`
- `Sources.Tests/SourcesViewModelTests.cs`
- `docs/release-readiness.md`
- `docs/session-summary.md`

## Forbidden scope
- `LoginWindow`, `LoginView`, `SplashWindow`
- `NeutronSourceService.cs`, `NeutronDecayCalculationService.cs`,
  `INeutronDecayCalculationService.cs` — read-only consumers in this round,
  no changes
- `NeutronSourceTypesViewModel.cs`/`NeutronSourceTypesWindow.xaml` — already
  complete (round 125)
- Any migration file — no schema change
- `NeutronSourceDetailsViewModel.cs`/`Window.xaml` (unrelated read-only
  details/certificates window)

## Acceptance criteria
1. Creating a new neutron source with Am-241 activity value + a chosen unit
   persists both fields correctly (test required).
2. Editing an existing neutron source with stored Am-241 activity pre-fills
   the value, unit, and shows a correctly computed
   `DisplayAm241CurrentActivity` reflecting actual decay from
   `CalibrationDate` (test required, with a hand-verifiable expected value
   like round 126's test).
3. Editing a neutron source with **no** stored Am-241 activity shows a
   neutral "not recorded" display, not an error (test required).
4. Entering a value with no unit selected (or vice versa) is rejected
   client-side before the service is called (test required).
5. A new/`IsNew` record's form does not attempt to compute/display a
   current-activity value (test required, or verified by inspection if a
   test is impractical — justify whichever in the report).
6. All existing tests continue to pass unmodified.
7. Documentation updated — this closes ب4 UI-wise entirely; explicitly
   state that ب4 is now functionally complete (123–128) and the next
   blocker is ب5.

## Required commands
````powershell
dotnet test -c Debug
dotnet test -c Release
````

## Migration protocol
Not applicable — no schema change.

## Expected test baseline
- Report exact count; at least 5 new tests expected (baseline 1125/1123).

## Visual verification by Edrees
**Required.** This round changes `SourcesView.xaml`. After merge, build and
run from `bin\Debug\net8.0-windows`, open the neutron source add/edit form,
enter an Am-241 activity value with a chosen unit, save, reopen it, and
confirm: the value/unit persist correctly, the calculated current-activity
display appears and looks sensible (not "not recorded" for a record that
has data), and a record with no Am-241 data shows the neutral message
instead of an error. Provide a real screenshot.

## Completion report requirements
- Base/result SHA, Draft PR URL, per-file justification, exact test counts,
  build warnings, deviations or `none`, remaining risks, and explicit
  confirmation that ب4 is now functionally complete pending your visual
  verification.
````