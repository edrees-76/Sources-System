# Round 125 — ب4 UI: wire Manufacturer/Model/Capsule fields into the neutron source form, add PhotonToNeutronDoseRatio to the type screen

## Identity
- Owner: Edrees
- Lead: Claude Code
- Base branch: `main`
- Base commit: `157840de0420e300ecedd1cefb15ab37409ea26c`
- Working branch: `round-125-neutron-b4-ui`
- Risk: `high` (first UI round of ب4; scientific reference data entry on a regulated entity)
- Parallel-safe: `no`

## Goal
Rounds 123–124 added schema fields and corrected reference data but touched
no UI. This round exposes the four new `NeutronSource` fields
(`Manufacturer`, `Model`, `CapsuleLengthMm`, `CapsuleDiameterMm`) in the
unified source form, and the new `NeutronSourceType.PhotonToNeutronDoseRatio`
field in the reference-types management screen.

## Evidence and diagnosis
- `Sources-System-Project/ViewModels/SourcesViewModel.cs`: `EditManufacturer`/
  `EditModel` already exist as shared `[ObservableProperty]` fields (used
  today only by the non-neutron `Source` path). `EditNeutronSource(...)`
  does **not** populate them from the target `NeutronSource`, and the neutron
  branch of `SaveAsync()` builds the `neutronSource` object **without**
  setting `Manufacturer`/`Model` at all, even though the properties are
  already in scope. This is a wiring gap, not a missing property.
- No `CapsuleLengthMm`/`CapsuleDiameterMm`-equivalent properties exist yet.
  The established pattern for an optional numeric field with free-text
  scientific-notation-tolerant input in this exact file is
  `EditRelativeUncertaintyPercent`/`EditRelativeUncertaintyText` (nullable
  double + string, with a `partial void On...TextChanged` parser using
  `NumericInputParser.TryParseFinite`, plus a pre-save guard rejecting
  non-finite/negative values with `ShowMessage`). Follow this exact pattern
  for both new fields.
- `Sources-System-Project/Services/NeutronSourceService.cs` (round 123)
  already rejects non-finite or `<=0` `CapsuleLengthMm`/`CapsuleDiameterMm`
  server-side — the ViewModel-level guard is for UX (fail fast, clear
  message) not a substitute for the service guard, matching how
  `EditRelativeUncertaintyPercent` already duplicates its service-level check.
- `Sources-System-Project/ViewModels/NeutronSourceTypesViewModel.cs`: has no
  field for `AmbientDoseConversionCoefficient` or `PhotonToNeutronDoseRatio`
  today. **`AmbientDoseConversionCoefficient` being non-editable via this UI
  is a pre-existing gap unrelated to rounds 123–125 — do not fix it in this
  round; only add `PhotonToNeutronDoseRatio`.** Follow the existing
  `EditAverageEnergyMev`/`EditAverageEnergyText` pattern exactly (nullable
  double + text, `ScientificNotationParser.TryParse`, pre-save guard).
- `Sources-System-Project/Views/SourcesView.xaml` and
  `Sources-System-Project/Views/NeutronSourceTypesWindow.xaml` need new
  input controls; locate the existing `Manufacturer`/`Model` controls in the
  non-neutron section of `SourcesView.xaml` and the existing
  `EditAverageEnergyText` control in `NeutronSourceTypesWindow.xaml` to
  match the established control style/layout exactly (do not invent a new
  visual pattern).
- `Sources.Tests/SourcesViewModelTests.cs` exists — extend it.
  `Sources.Tests/NeutronSourceTypesViewModelTests.cs` does **not** exist —
  this is the first test file for that ViewModel; create it following the
  style of an existing simple ViewModel test file (e.g.
  `LocationDetailsViewModelTests.cs`) for construction conventions.

## Architectural decision
1. **`SourcesViewModel.cs`**:
   - Add `EditCapsuleLengthMm` (`double?`) / `EditCapsuleLengthText` (`string`)
     and `EditCapsuleDiameterMm` (`double?`) / `EditCapsuleDiameterText`
     (`string`), each with a `partial void On...TextChanged` parser mirroring
     `OnEditRelativeUncertaintyTextChanged` exactly (empty → null, else
     `NumericInputParser.TryParseFinite`, non-parseable → null).
   - `ClearForm()`: reset both new pairs to empty/null alongside the existing
     neutron field resets.
   - `EditNeutronSource(...)`: populate `EditManufacturer`/`EditModel` from
     `target.Manufacturer`/`target.Model` (mirroring how `EditSource` already
     does this), and populate the two new capsule fields/texts from
     `target.CapsuleLengthMm`/`CapsuleDiameterMm`.
   - `SaveAsync()`'s neutron branch: add pre-save guards for the two new
     fields (non-finite or `<=0` when the text is non-empty → `ShowMessage`
     and return, mirroring the existing `EditAnisotropyFactorText`/
     `EditRelativeUncertaintyText` guard blocks exactly), then set
     `Manufacturer = EditManufacturer?.Trim()`, `Model = EditModel?.Trim()`,
     `CapsuleLengthMm = EditCapsuleLengthMm`, `CapsuleDiameterMm =
     EditCapsuleDiameterMm` on the constructed `neutronSource` object.
2. **`SourcesView.xaml`**: add four input controls (Manufacturer, Model,
   CapsuleLengthMm, CapsuleDiameterMm) inside the neutron-form section only,
   bound to the properties above, matching the exact style (labels, spacing,
   `MaterialDesign` control types) of the adjacent existing neutron-form
   fields (e.g. the uncertainty/anisotropy inputs).
3. **`NeutronSourceTypesViewModel.cs`**: add `EditPhotonToNeutronRatio`
   (`double?`) / `EditPhotonToNeutronRatioText` (`string`), parser mirroring
   `OnEditAverageEnergyTextChanged` (using `ScientificNotationParser.TryParse`
   — a ratio can be written in scientific notation like other measured
   quantities in this codebase), pre-save guard rejecting non-finite values
   only (no `<=0` rejection, matching round 123's server-side decision),
   wired into `AddNew`'s implicit clear (`ClearForm()`), `Edit(...)`
   (populate from `target.PhotonToNeutronDoseRatio`), and both branches of
   `Save()` (set on `newType`/`existing`).
4. **`NeutronSourceTypesWindow.xaml`**: add one input control for the new
   field, matching the existing `EditAverageEnergyText` control's style.
5. No change to any service, model, or migration file. No change to
   `AmbientDoseConversionCoefficient`'s absence from this UI (pre-existing,
   out of scope).

## Allowed files
- `Sources-System-Project/ViewModels/SourcesViewModel.cs`
- `Sources-System-Project/ViewModels/NeutronSourceTypesViewModel.cs`
- `Sources-System-Project/Views/SourcesView.xaml`
- `Sources-System-Project/Views/NeutronSourceTypesWindow.xaml`
- `Sources.Tests/SourcesViewModelTests.cs`
- `Sources.Tests/NeutronSourceTypesViewModelTests.cs` (new file)
- `docs/release-readiness.md`
- `docs/session-summary.md`

## Forbidden scope
- `LoginWindow`, `LoginView`, `SplashWindow`
- Any service, model, or migration file
- `AmbientDoseConversionCoefficient` (do not add a UI field for it — separate,
  unrelated, pre-existing gap)
- `NeutronSourceDetailsViewModel.cs`/`NeutronSourceDetailsWindow.xaml`
  (read-only details/certificates window — unrelated to data entry)
- `NeutronSourceServiceTests.cs`/`NeutronSourceTypeServiceTests.cs` (service
  layer already fully covered by rounds 122–123, not touched here)

## Acceptance criteria
1. Creating a new neutron source through the form with Manufacturer/Model/
   capsule dimensions filled in persists all four fields correctly (test
   required).
2. Editing an existing neutron source pre-fills all four fields from the
   loaded entity (test required).
3. Entering a non-finite or `<=0` capsule dimension is rejected client-side
   with a message, before the service is called (test required, mirroring
   existing anisotropy/uncertainty guard tests in this file if any exist —
   check first).
4. Creating/editing a neutron source type with a photon/neutron ratio value
   persists it; a non-finite value is rejected client-side (tests required
   in the new `NeutronSourceTypesViewModelTests.cs`).
5. All existing tests in both files continue to pass unmodified in their
   pre-existing assertions.
6. Documentation updated in the same commit — this is the first UI-facing
   round of ب4; note explicitly what remains (English translation of any
   new labels is ب5's job, not this round's — do not add English resource
   strings beyond whatever this codebase's minimal-comment convention
   already requires for XAML `x:Name`/binding, since ب5 hasn't started yet).

## Required commands
````powershell
dotnet test -c Debug
dotnet test -c Release
````

## Migration protocol
Not applicable — no schema change.

## Expected test baseline
- Report exact count; new tests required (at least 4, likely more across
  both files) will increase both Debug and Release counts from 1092/1094.

## Visual verification by Edrees
**Required.** This round changes `SourcesView.xaml` and
`NeutronSourceTypesWindow.xaml` — after merge, build and run the app from
`bin\Debug\net8.0-windows`, open a neutron source's add/edit form and the
neutron source types management screen, and confirm the new fields render
correctly, are usable, and match the existing visual style before
considering this round fully closed. Provide a real screenshot.

## Completion report requirements
- Base/result SHA, Draft PR URL, per-file justification, exact test counts,
  build warnings, deviations or `none`, remaining risks, and confirmation
  that no XAML style was invented rather than matched to existing controls.
````