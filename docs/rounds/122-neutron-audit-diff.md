# Round 122 — Populate OldValues/NewValues in NeutronSourceService and NeutronSourceTypeService

## Identity
- Owner: Edrees
- Lead: Claude Code
- Base branch: `main`
- Base commit: `c2ded7e0a5abee84f690718aa803d3d4970fcbe7`
- Working branch: `round-122-neutron-audit-diff`
- Risk: `high` (audit-trail correctness, part of the tracked multi-service debt)
- Parallel-safe: `no`

## Goal
Final round of the OldValues/NewValues sequence (119: SourceService · 120:
LocationService · 121: RadioisotopeService · **122: NeutronSourceService +
NeutronSourceTypeService**). Both services have **no** existing
`LogWithChanges` usage — all four operations in each call bare
`_auditService.Log(...)`. Both are covered in one round because they are
small, tightly related (neutron domain), and each individually is smaller
than `RadioisotopeService`.

## Evidence and diagnosis
- `Sources-System-Project/Services/NeutronSourceService.cs` — all four
  operations (`Create`, `Update`, `Delete`, `Restore`) confirmed bare
  `_auditService.Log(...)`.
- `Sources-System-Project/Services/NeutronSourceTypeService.cs` — same,
  all four confirmed bare `Log(...)`.
- `NeutronSource` raw fields mutated in `Update` (12): `SourceCode`,
  `SerialNumber`, `NeutronSourceTypeId`, `LocationId`,
  `CalibratedEmissionRate`, `RelativeExpandedUncertaintyPercent`,
  `CalibrationDate`, `EmissionCalibrationDate`, `CalibrationReference`,
  `AnisotropyFactor`, `Status`, `Notes`. `AddedBy`/`CreatedAt` excluded
  (matches rounds 119–121 convention).
- `NeutronSourceType` raw fields mutated in `Update` (12): `Code`, `NameEn`,
  `NameAr`, `ReactionType`, `TargetMaterial`, `ParentNuclide`, `HalfLife`,
  `HalfLifeUnit`, `MeanNeutronEnergyMeV`, `AmbientDoseConversionCoefficient`,
  `StandardReference`, `Notes`.
- Both test files (`NeutronSourceServiceTests.cs`,
  `NeutronSourceTypeServiceTests.cs`) use `Sources.Tests.Fakes.
  FakeAuditService`, asserting via `Assert.Contains(_fakeAuditService.
  LoggedEntries, l => l.Action == ... && l.TableName == ...)` — note this
  differs slightly from `Assert.Single(...)` used in rounds 119–121; keep
  this file's own `Assert.Contains` idiom, don't switch to `Assert.Single`.
  No `OldValues`/`NewValues` assertions exist yet in either file. No Moq.
- **Both files already have a functional `Restore` test** (`Restore_
  DeletedSource_RestoresSuccessfully_AndClearsSoftDeleteFields` in
  `NeutronSourceServiceTests.cs`, `Restore_DeletedType_ReturnsSuccess_
  AndClearsSoftDeleteFields` in `NeutronSourceTypeServiceTests.cs`) — unlike
  rounds 120/121, **extend these existing tests** with `NewValues` assertions
  rather than adding new ones.
- `NeutronSourceService.Delete` guard (linked-sources check) does not apply —
  it has no such guard (unlike `RadioisotopeService`/`LocationService`); its
  only pre-mutation checks are the authorization guard and the not-found
  check. `NeutronSourceTypeService.Delete` **does** have a linked-sources
  guard (`item.NeutronSources.Any() || db.NeutronSources.Any(...)`) — old
  snapshot must be captured after that guard passes, before `item.IsDeleted
  = true`.

## Architectural decision
Same pattern as rounds 119–121, fully-qualified
`System.Text.Json.JsonSerializer.Serialize(...)`, no new `using`, dates
formatted as `"yyyy-MM-dd"` strings (matching `CalibrationDate`/
`EmissionCalibrationDate` in `NeutronSourceService`'s existing convention
elsewhere in the codebase):

**`NeutronSourceService`** (12-field list above):
1. `Create`: `newValuesObj` after `db.SaveChanges()`, `oldValues: null`.
2. `Update`: `oldValuesObj` from `existing`, captured immediately after
   `var existing = db.NeutronSources.Find(item.Id);` and its null check,
   before any mutation line. `newValuesObj` after `db.SaveChanges()`.
3. `Delete`: `oldValuesObj` from `item`, captured before `item.IsDeleted =
   true;`. `newValues: null`.
4. `Restore`: `oldValues: null`, `newValuesObj` after `db.SaveChanges()`.

**`NeutronSourceTypeService`** (12-field list above): identical structure,
with `Delete`'s old-values snapshot captured after the linked-sources guard
passes and before `item.IsDeleted = true;`.

No change to method signatures, return values, the finite-value/date
validation guards, the linked-sources delete guard in
`NeutronSourceTypeService`, or the duplicate-code/duplicate-symbol checks in
either service.

## Allowed files
- `Sources-System-Project/Services/NeutronSourceService.cs`
- `Sources-System-Project/Services/NeutronSourceTypeService.cs`
- `Sources.Tests/NeutronSourceServiceTests.cs`
- `Sources.Tests/NeutronSourceTypeServiceTests.cs`
- `docs/release-readiness.md`
- `docs/session-summary.md`

## Forbidden scope
- `LoginWindow`, `LoginView`, `SplashWindow`
- `SourceService.cs`, `LocationService.cs`, `RadioisotopeService.cs` (all
  done), `UserService.cs`, `LeakTestService.cs`, `BorrowService.cs`
- Introducing Moq — `FakeAuditService` only in both test files
- Any change to the finite-value validation guards, the future-date guards,
  or either service's delete/duplicate-check logic
- `NeutronSourceIsolationTests.cs`, `NeutronSourcesUITests.cs` (unrelated
  test files with matching name prefix — do not touch)

## Acceptance criteria
1. Each service's `Create` writes one entry: `OldValues == null`, `NewValues`
   contains the created record's identifying field (`SourceCode` for
   `NeutronSource`, `Code` for `NeutronSourceType`) and at least one other
   field.
2. Each service's `Update` writes one entry with a genuine before/after
   differential on at least two fields (same style as rounds 119–121).
3. Each service's `Delete` writes one entry: `OldValues` contains the
   pre-delete identifying field, `NewValues == null`.
4. Each service's existing `Restore` test is extended (not duplicated) to
   assert `OldValues == null` and `NewValues` contains the post-restore
   identifying field.
5. All existing tests in both files continue to pass unmodified in their
   pre-existing assertions.
6. Documentation updated in the same commit — this closes the **entire**
   multi-service OldValues/NewValues debt item (`SourceService` in 119,
   `LocationService` in 120, `RadioisotopeService` in 121, both neutron
   services in 122), so the tracked debt line in `release-readiness.md`
   should be removed/closed entirely, not just reduced.

## Required commands
````powershell
dotnet test -c Debug
dotnet test -c Release
````

## Migration protocol
Not applicable — no schema change.

## Expected test baseline
- Debug/local expected count: 1088 or higher (report exact count; existing
  Restore tests extended, not duplicated, so no new test count increase is
  required unless a criterion genuinely needs a new one — explain any delta)
- Release/CI expected count: 1086 or higher, matching the Debug delta
- Documented conditional-test difference: `TestDataGeneratorTests` under
  `#if DEBUG`

## Visual verification by Edrees
Not required.

## Completion report requirements
- Base/result SHA, Draft PR URL, per-file justification, exact test counts
  (explicitly note any new vs. extended tests), build warnings, deviations or
  `none`, remaining risks.
- Explicit confirmation that this is the final round of the OldValues/
  NewValues sequence and the debt item is now fully closed.
````