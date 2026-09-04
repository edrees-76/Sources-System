# Round 119 — Populate OldValues/NewValues in SourceService.Create/Update audit entries

## Identity
- Owner: Edrees
- Lead: Claude Code
- Base branch: `main`
- Base commit: `25b35ffe2ac260234f5e46044f243f81706720e4`
- Working branch: `round-119-source-audit-diff`
- Risk: `high` (regulated flagship entity, audit-trail correctness)
- Parallel-safe: `no`

## Goal
`SourceService.CreateSource` and `UpdateSource` call the bare `_auditService.Log(...)`
overload (no diff data), unlike `DeleteSource`/`RestoreSource` in the same file which
already build JSON snapshots via `LogWithChanges`. This is part of a broader,
already-inventoried inconsistency across 7 services (tracked in
`docs/release-readiness.md` as "نقص OldValues/NewValues"); this round addresses
`SourceService` only — the first of a planned sequence (119: SourceService, 120:
LocationService, 121: RadioisotopeService, 122: NeutronSourceService +
NeutronSourceTypeService). Do not touch any other service in this round.

## Evidence and diagnosis
- File: `Sources-System-Project/Services/SourceService.cs`
- `CreateSource` (ends with `_auditService.Log("Create", "Sources", source.Id, ...)`)
  and `UpdateSource` (ends with `_auditService.Log("Update", "Sources", source.Id, ...)`)
  use the bare overload — confirmed by reading both method bodies in full.
- `DeleteSource`/`RestoreSource` in the same file already call
  `_auditService.LogWithChanges(...)` with `System.Text.Json.JsonSerializer.Serialize(...)`
  snapshots — this is the file's own established reference pattern; follow it exactly
  (same serialization call style, same `using System.Text.Json.JsonSerializer` — note
  the file uses the fully-qualified `System.Text.Json.JsonSerializer.Serialize` inline
  rather than a `using` import; match that existing style, don't add a new `using`).
- `Sources.Tests/SourceServiceTests.cs` uses `Sources.Tests.Fakes.FakeAuditService`
  (`_auditService.LoggedEntries`, each entry exposing `Action`/`TableName`/`RecordId`/
  `Details`/`OldValues`/`NewValues`), **not** `Mock<IAuditService>`. Follow this file's
  own existing convention — do not introduce Moq here.

## Architectural decision
1. **`CreateSource`**: immediately before the existing `_auditService.Log(...)` call
   (which becomes `LogWithChanges`), build a `newValuesObj` snapshotting the raw
   mutated/set fields: `source.SourceCode`, `source.RadioisotopeId`,
   `source.SerialNumber`, `source.Manufacturer`, `source.Model`,
   `source.InitialActivityValue`, `source.InitialActivityUnitId`,
   `source.CalibrationDate` (as `"yyyy-MM-dd"` string, matching the existing
   `Delete`/`Restore` date-formatting convention in this file), `source.CurrentActivityUnitId`,
   `source.LocationId`, `source.Status`, `source.IsSealed`, `source.Notes`.
   Call `_auditService.LogWithChanges("Create", "Sources", source.Id, $"إنشاء مصدر:
   {source.SourceCode}", oldValues: null, newValues: System.Text.Json.JsonSerializer.Serialize(newValuesObj))`.

2. **`UpdateSource`**: capture `oldValuesObj` from `existing` using the same field list
   above, **immediately after** `var existing = db.Sources.Include(...).FirstOrDefault(...)`
   and the `if (existing == null) return ...` null check — **before** any of the
   `existing.SourceCode = ...` mutation lines. After all mutations and
   `db.SaveChanges()`, build `newValuesObj` from `existing` (now mutated) using the
   identical field list, and call `_auditService.LogWithChanges("Update", "Sources",
   source.Id, $"تعديل مصدر: {source.SourceCode}", oldValuesJson, newValuesJson)`
   replacing the current bare `Log(...)` call.

No change to method signatures, return values, validation logic, the location-history
tracking block, the active-borrow guard, or `DeleteSource`/`RestoreSource` (already
correct). No change to `_decayService`/isotope-calculation logic.

## Allowed files
- `Sources-System-Project/Services/SourceService.cs`
- `Sources.Tests/SourceServiceTests.cs`
- `docs/release-readiness.md`
- `docs/session-summary.md`

## Forbidden scope
- `LoginWindow`, `LoginView`, `SplashWindow`
- `LocationService.cs`, `RadioisotopeService.cs`, `NeutronSourceService.cs`,
  `NeutronSourceTypeService.cs`, `UserService.cs`, `LeakTestService.cs`,
  `BorrowService.cs` — each gets its own later round
- Introducing `Mock<IAuditService>`/Moq into `SourceServiceTests.cs` — use the
  existing `FakeAuditService`/`_auditService.LoggedEntries` pattern only
- Any change to isotope-activity calculation, decay logic, or location-history
  recording behavior

## Acceptance criteria
1. `CreateSource` writes one audit entry with `NewValues` containing the created
   source's `SourceCode` and at least two other snapshotted fields; `OldValues` is
   `null`.
2. `UpdateSource` writes one audit entry with `OldValues` reflecting the pre-update
   field values and `NewValues` reflecting the post-update values — verify with a
   test that changes at least `SerialNumber` and `Status`, then asserts the old
   entry's `OldValues` contains the *original* `SerialNumber` and the *new* value
   is absent from `OldValues`, and vice versa for `NewValues`.
3. All existing `SourceServiceTests.cs` tests continue to pass unmodified (in
   particular the two that already assert on `_auditService.LoggedEntries` for
   Create/Update — `CreateSource_ValidSingleSource_SucceedsAndLogsAudit` and
   `UpdateSource_ValidSource_SucceedsAndUpdatesPropertiesAndLogsAudit` — these may
   need their existing assertions *extended* with `OldValues`/`NewValues` checks,
   but their existing assertions must not be weakened or removed).
4. Documentation updated in the same commit, noting this closes `SourceService`
   specifically while the remaining 3 services stay tracked as debt.

## Required commands
````powershell
dotnet test -c Debug
dotnet test -c Release
````

## Migration protocol
Not applicable — no schema change.

## Expected test baseline
- Debug/local expected count: 1082 (unchanged — existing tests extended, not
  added, unless a genuinely new test is needed for criterion 2, in which case
  report the exact new count)
- Release/CI expected count: 1080 (unchanged, +1 if a new test was added)
- Documented conditional-test difference: `TestDataGeneratorTests` under `#if DEBUG`

## Visual verification by Edrees
Not required.

## Completion report requirements
- Base/result SHA, Draft PR URL, per-file justification, exact test counts,
  build warnings, deviations or `none`, remaining risks, and explicit confirmation
  of the exact test count (extended vs. new) since the contract allows either.
````