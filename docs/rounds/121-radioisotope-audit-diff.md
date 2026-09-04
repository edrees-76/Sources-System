# Round 121 — Populate OldValues/NewValues across all four RadioisotopeService operations

## Identity
- Owner: Edrees
- Lead: Claude Code
- Base branch: `main`
- Base commit: `98b85b42273f142c3a2419debe12bd385fde1600`
- Working branch: `round-121-radioisotope-audit-diff`
- Risk: `high` (audit-trail correctness, part of the tracked multi-service debt)
- Parallel-safe: `no`

## Goal
Same pattern as round 120 (`LocationService`): `RadioisotopeService` has **no**
existing `LogWithChanges` usage — all four operations (`Create`, `Update`,
`Delete`, `Restore`) call the bare `_auditService.Log(...)` overload. Third of
the planned sequence (119: SourceService · 120: LocationService · 121:
RadioisotopeService · 122: NeutronSourceService + NeutronSourceTypeService).

## Evidence and diagnosis
- File: `Sources-System-Project/Services/RadioisotopeService.cs` — all four
  methods confirmed ending with bare `_auditService.Log(...)`, no diff data.
- Raw mutated fields across `Create`/`Update`: `Name`, `ArabicName`, `Symbol`,
  `RadiationType`, `HalfLife`, `HalfLifeUnit`, `Energy`, `Yield`, `Category`,
  `ExemptionLimit`, `GammaConstant`, `Notes`, `EnglishNotes` (13 fields).
  `AddedBy` excluded (matches round 119/120 convention).
- `Sources.Tests/RadioisotopeServiceTests.cs` uses `Sources.Tests.Fakes.
  FakeAuditService` (`_fakeAuditService.LoggedEntries`), asserting today only
  on `Action`/`TableName`/`RecordId`/`Details`. No `OldValues`/`NewValues`
  assertions exist. No Moq.
- `Delete` has a business guard (`item.Sources.Any() || db.SourceIsotopes.Any(...)`)
  returning early before any mutation — the old-values snapshot for `Delete`
  must be taken **after** this guard passes, exactly where `item.IsDeleted =
  true` is about to execute (same relative position as `SourceService`/
  `LocationService`'s `Delete`).
- `Update`'s `ArabicName` auto-fill logic (`string.IsNullOrEmpty(item.ArabicName)
  ? IsotopeHelper.GetArabicNameFromSymbol(...) : item.ArabicName`) runs as
  part of the mutation itself — the **old** snapshot must be taken from
  `existing` *before* this line, and the **new** snapshot from `existing`
  *after* it (i.e., after `db.SaveChanges()`), so the new snapshot correctly
  reflects the auto-filled value when the user left `ArabicName` empty.

## Architectural decision
Same as round 120, adapted to the 13-field list, using fully-qualified
`System.Text.Json.JsonSerializer.Serialize(...)` (no new `using`):

1. **`Create`**: after `db.SaveChanges()`, `newValuesObj` from `item`,
   `oldValues: null`.
2. **`Update`**: `oldValuesObj` from `existing`, captured **immediately after**
   `var existing = db.Radioisotopes.Find(item.Id);` and its null check —
   **before** any `existing.Name = ...` mutation line (including before the
   `ArabicName` auto-fill assignment). `newValuesObj` from `existing` after
   `db.SaveChanges()`.
3. **`Delete`**: `oldValuesObj` from `item`, captured **after** the
   linked-sources guard passes and **before** `item.IsDeleted = true;`.
   `newValues: null`.
4. **`Restore`**: `oldValues: null`, `newValuesObj` from `item` captured
   **after** `db.SaveChanges()`.

No change to method signatures, return values, validation logic (finite-value
guards, half-life/energy checks), the linked-sources delete guard, or the
duplicate-symbol checks.

## Allowed files
- `Sources-System-Project/Services/RadioisotopeService.cs`
- `Sources.Tests/RadioisotopeServiceTests.cs`
- `docs/release-readiness.md`
- `docs/session-summary.md`

## Forbidden scope
- `LoginWindow`, `LoginView`, `SplashWindow`
- `SourceService.cs`, `LocationService.cs` (both done), `NeutronSourceService.cs`,
  `NeutronSourceTypeService.cs`, `UserService.cs`, `LeakTestService.cs`,
  `BorrowService.cs`
- Introducing Moq into `RadioisotopeServiceTests.cs` — `FakeAuditService` only
- Any change to `IsotopeHelper.GetArabicNameFromSymbol`, the finite-value
  validation guards, or the linked-sources delete guard's logic

## Acceptance criteria
1. `Create` writes one entry: `OldValues == null`, `NewValues` contains the
   created isotope's `Symbol`/`Name` and at least one other field.
2. `Update` writes one entry with a genuine before/after differential — a test
   changing at least `Name` and `GammaConstant`, asserting original values
   appear only in `OldValues` and new values only in `NewValues` (same
   differential style as rounds 119/120).
3. **Specifically test the `ArabicName` auto-fill interaction**: a test where
   `ArabicName` is left empty on update, asserting `NewValues` contains the
   auto-filled Arabic name (not empty/null), proving the new-value snapshot
   was taken after the auto-fill logic ran.
4. `Delete` writes one entry: `OldValues` contains the pre-delete `Symbol`/
   `Name`, `NewValues == null`.
5. `Restore` writes one entry: `OldValues == null`, `NewValues` contains the
   post-restore `Symbol`/`Name`. If no `Restore` audit test exists yet, add one.
6. All existing tests continue to pass unmodified in their pre-existing
   assertions; extend rather than weaken.
7. Documentation updated in the same commit, closing the `RadioisotopeService`
   slice while `NeutronSourceService`/`NeutronSourceTypeService` stay open for
   round 122.

## Required commands
````powershell
dotnet test -c Debug
dotnet test -c Release
````

## Migration protocol
Not applicable — no schema change.

## Expected test baseline
- Debug/local expected count: 1084 or higher (report exact count and explain
  delta — at minimum the new `ArabicName` auto-fill test and possibly a new
  `Restore` test)
- Release/CI expected count: 1082 or higher, matching the Debug delta
- Documented conditional-test difference: `TestDataGeneratorTests` under
  `#if DEBUG`

## Visual verification by Edrees
Not required.

## Completion report requirements
- Base/result SHA, Draft PR URL, per-file justification, exact test counts
  (explicitly note new tests and why), build warnings, deviations or `none`,
  remaining risks.
````