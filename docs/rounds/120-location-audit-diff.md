# Round 120 — Populate OldValues/NewValues across all four LocationService operations

## Identity
- Owner: Edrees
- Lead: Claude Code
- Base branch: `main`
- Base commit: `5560f552def75ee6ed141ddaed4d038e7a1bd484`
- Working branch: `round-120-location-audit-diff`
- Risk: `high` (audit-trail correctness, part of the tracked multi-service debt)
- Parallel-safe: `no`

## Goal
Unlike `SourceService` (round 119), `LocationService` has **no** existing
`LogWithChanges` usage anywhere — all four operations (`Create`, `Update`,
`Delete`, `Restore`) call the bare `_auditService.Log(...)` overload. This
round builds the diff pattern from scratch for all four, following the
`SourceService`/`UserService` reference style. Second of the planned sequence
(119 done: SourceService · 120: LocationService · 121: RadioisotopeService ·
122: NeutronSourceService + NeutronSourceTypeService).

## Evidence and diagnosis
- File: `Sources-System-Project/Services/LocationService.cs` — all four
  methods confirmed to end with bare `_auditService.Log(...)`, no diff data,
  by reading every method body in full.
- `Location` entity's raw user-editable fields mutated across `Create`/
  `Update`: `LocationName`, `LocationType`, `Building`, `Room`,
  `ResponsiblePerson`. `AddedBy` is set once at creation and not part of the
  diff (matches `SourceService`'s exclusion of its own `AddedBy` field from
  round 119's snapshot).
- `Sources.Tests/LocationServiceTests.cs` uses
  `Sources.Tests.Fakes.FakeAuditService` (`_fakeAuditService.LoggedEntries`,
  field name is `_fakeAuditService` here, not `_auditService` — note the
  different field name from `SourceServiceTests.cs`), asserting on
  `Action`/`TableName`/`RecordId`/`Details` today. No `OldValues`/`NewValues`
  assertions exist yet. Do not introduce Moq.
- `Location` has no computed/display-only fields analogous to `Source`'s
  `ArabicStatus`/`DisplayIsotopes` — the same 5-field raw list is appropriate
  for all four operations' snapshots (no need for two different field sets
  the way `SourceService`'s Delete/Restore used display fields while
  Create/Update used raw fields).

## Architectural decision
Build snapshot objects from the 5-field list (`LocationName`, `LocationType`,
`Building`, `Room`, `ResponsiblePerson`) and call `_auditService.LogWithChanges`
in all four methods, replacing the bare `Log(...)` calls, matching
`SourceService`'s serialization style (fully-qualified
`System.Text.Json.JsonSerializer.Serialize(...)`, no new `using`):

1. **`Create`**: after `db.SaveChanges()`, `newValuesObj` from `item` (post-save
   state), `oldValues: null`.
2. **`Update`**: `oldValuesObj` from `existing` captured **immediately after**
   `var existing = db.Locations.Find(item.Id);` and its null check — **before**
   any `existing.LocationName = ...` mutation line. `newValuesObj` from
   `existing` after `db.SaveChanges()`.
3. **`Delete`**: `oldValuesObj` from `item` captured **before** the
   `item.IsDeleted = true;` mutation — `newValues: null` (matches
   `SourceService.DeleteSource`'s convention).
4. **`Restore`**: `oldValues: null`, `newValuesObj` from `item` captured
   **after** `db.SaveChanges()` (matches `SourceService.RestoreSource`'s
   convention).

No change to method signatures, return values, the `Sources`/`NeutronSources`
link-check guard in `Delete`, the duplicate-name checks, or any other logic.

## Allowed files
- `Sources-System-Project/Services/LocationService.cs`
- `Sources.Tests/LocationServiceTests.cs`
- `docs/release-readiness.md`
- `docs/session-summary.md`

## Forbidden scope
- `LoginWindow`, `LoginView`, `SplashWindow`
- `SourceService.cs` (already done, round 119), `RadioisotopeService.cs`,
  `NeutronSourceService.cs`, `NeutronSourceTypeService.cs`, `UserService.cs`,
  `LeakTestService.cs`, `BorrowService.cs`
- Introducing Moq into `LocationServiceTests.cs` — use `FakeAuditService` only
- Any change to the `GetSourcesLinkedToLocation`/`GetAll`/`GetById` query
  logic or the source-count aggregation

## Acceptance criteria
1. `Create` writes one entry: `OldValues == null`, `NewValues` contains the
   created location's `LocationName` and at least one other snapshotted field.
2. `Update` writes one entry with a genuine before/after differential — a test
   must change at least `LocationName` and `Room`, then assert the *original*
   values appear in `OldValues` and are absent from `NewValues`, and vice versa
   for the new values (same differential-assertion style as round 119's
   `UpdateSource` test).
3. `Delete` writes one entry: `OldValues` contains the pre-delete
   `LocationName`, `NewValues == null`.
4. `Restore` writes one entry: `OldValues == null`, `NewValues` contains the
   post-restore `LocationName`. If `Sources.Tests/LocationServiceTests.cs` has
   no existing `Restore` test, add one that also exercises this.
5. All existing tests in the file continue to pass unmodified in their
   pre-existing assertions; extend rather than weaken the four tests that
   already assert on `_fakeAuditService.LoggedEntries` for Create/Update/Delete.
6. Documentation updated in the same commit, closing the `LocationService`
   slice specifically while `RadioisotopeService`/`NeutronSourceService`/
   `NeutronSourceTypeService` stay open for rounds 121–122.

## Required commands
````powershell
dotnet test -c Debug
dotnet test -c Release
````

## Migration protocol
Not applicable — no schema change.

## Expected test baseline
- Debug/local expected count: 1082 or higher if a new `Restore` test is added
  (report the exact count and explain any delta)
- Release/CI expected count: 1080 or higher, matching the Debug delta
- Documented conditional-test difference: `TestDataGeneratorTests` under
  `#if DEBUG`

## Visual verification by Edrees
Not required.

## Completion report requirements
- Base/result SHA, Draft PR URL, per-file justification, exact test counts
  (explicitly note whether a new test was added and why), build warnings,
  deviations or `none`, remaining risks.
````