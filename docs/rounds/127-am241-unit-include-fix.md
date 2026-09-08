# Round 127 — Fix: eagerly load Am241ActivityUnit in NeutronSourceService read methods

## Identity
- Owner: Edrees
- Lead: Claude Code
- Base branch: `main`
- Base commit: `b7e424aea2a74475e1f3949efd0fe79c1c9ce942`
- Working branch: `round-127-am241-unit-include-fix`
- Risk: `high` (fixes a confirmed functional defect in a decay calculation on a regulated entity)
- Parallel-safe: `no`

## Goal
Round 126 added `NeutronSource.Am241ActivityUnit` (navigation property) and a
decay-calculation method (`CalculateCurrentAm241Activity`/
`CalculateAm241ActivityAtDate`) that requires this navigation to be loaded.
CodeRabbit's review on PR #14 correctly identified that none of
`NeutronSourceService`'s five read methods (`GetAll`, `GetDeleted`,
`GetById`, `GetByCode`, `GetByLocation`) eagerly load it. Confirmed
independently by direct code review: the property is non-virtual, so it
stays `null` after the `DbContext` is disposed, meaning
`CalculateCurrentAm241Activity`/`CalculateAm241ActivityAtDate` will always
return `MissingActivityUnit` for any source fetched through these methods —
**even when `Am241ActivityUnitId` is correctly stored**. This round fixes
that and adds a regression test proving the calculation actually succeeds
end-to-end through the service's own read API, not just against a
manually-constructed object.

## Evidence and diagnosis
- `Sources-System-Project/Services/NeutronSourceService.cs`, lines ~29-88:
  all five read methods include `.Include(n => n.NeutronSourceType)`,
  `.Include(n => n.Location)`, and `.Include(n => n.AddedByUser)`/
  `.Include(n => n.DeletedByUser)`, but none include
  `.Include(n => n.Am241ActivityUnit)` — confirmed by direct reading, not
  just CodeRabbit's claim.
- This is purely additive: adding an `.Include()` call does not change
  query results' filtering, ordering, or any existing field — only adds one
  more navigation property to the already-loaded object graph.

## Architectural decision
Add `.Include(n => n.Am241ActivityUnit)` to all five read methods in
`NeutronSourceService.cs` (`GetAll`, `GetDeleted`, `GetById`, `GetByCode`,
`GetByLocation`), in the same position/style as the existing `.Include()`
calls in each method (no reordering of existing includes). No other change
to these methods' logic, filtering, or return types.

## Allowed files
- `Sources-System-Project/Services/NeutronSourceService.cs`
- `Sources.Tests/NeutronSourceServiceTests.cs` and/or `Sources.Tests/NeutronDecayTests.cs`
  (whichever already tests `GetById`/decay calculation together — check
  both before deciding where the new regression test belongs; do not
  duplicate across both files)
- `docs/release-readiness.md`
- `docs/session-summary.md`

## Forbidden scope
- `LoginWindow`, `LoginView`, `SplashWindow`
- Any change to `Create`/`Update`/`Delete`/`Restore` in this file — already
  correct, not touched
- `NeutronDecayCalculationService.cs`/`INeutronDecayCalculationService.cs` —
  the calculation logic itself is correct (verified in round 126); this
  round fixes only the data-loading gap upstream of it
- Any XAML view or ViewModel — still no UI in this round
- Any other service's read methods (this defect is specific to the new
  `Am241ActivityUnit` navigation, not a general audit of other `.Include()`
  patterns)

## Acceptance criteria
1. All five read methods now include `Am241ActivityUnit`.
2. A regression test: create a `NeutronSource` with valid
   `Am241ActivityValue`/`Am241ActivityUnitId`/`CalibrationDate` via the
   service's `Create` method, then fetch it back via `GetById` (not a
   manually-constructed object), then call
   `CalculateCurrentAm241Activity`/`CalculateAm241ActivityAtDate` on the
   fetched object and assert the result's `Status == Calculated` with a
   non-null `CurrentActivityBq` — proving the fix closes the gap
   end-to-end, not just that the `.Include()` line exists.
3. All existing tests continue to pass unmodified.
4. Documentation updated, explicitly referencing this as a fix for the
   defect CodeRabbit found on PR #14 (round 126), citing the PR number.

## Required commands
````powershell
dotnet test -c Debug
dotnet test -c Release
````

## Migration protocol
Not applicable — no schema change.

## Expected test baseline
- Report exact count; at least 1 new regression test expected (baseline
  1124/1122).

## Visual verification by Edrees
Not required (no UI in this round).

## Completion report requirements
- Base/result SHA, Draft PR URL, per-file justification, exact test counts,
  build warnings, deviations or `none`, remaining risks, explicit
  confirmation that only the five `.Include()` additions and the regression
  test were made — no logic changes elsewhere.
````