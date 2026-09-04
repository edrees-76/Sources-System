# Round 118 — Log ResetPassword, UnlockAccount, ToggleUserFreeze in the audit trail

## Identity
- Owner: Edrees
- Lead: Claude Code
- Base branch: `main`
- Base commit: `c8298e01f0833ffd5bcb030f6687ff93b60c3a0b`
- Working branch: `round-118-user-audit-gaps`
- Risk: `high` (authorization/audit trail for a regulated system)
- Parallel-safe: `no`

## Goal
`UserService.ResetPassword`, `UnlockAccount`, and `ToggleUserFreeze` currently
write **zero** audit log entries — not even a bare action-level entry. In a
radiation-source regulatory system, "who reset whose password, unlocked which
account, or froze/unfroze which account, and when" being entirely absent from
the audit trail is a compliance gap, distinct from and more serious than the
separately-tracked "OldValues/NewValues are inconsistently populated" issue
(deferred to a later round). This round adds audit entries for these three
operations only.

## Evidence and diagnosis
- File: `Sources-System-Project/Services/UserService.cs`
- `CreateUser`/`UpdateUser` already call `_auditService?.LogWithChanges(...)`
  with serialized before/after snapshots — the correct reference pattern.
- `DeleteUser`/`RestoreUser` call `var auditService = _auditService ?? new
  AuditService(_dbFactory, this); auditService.Log(...)` — a bare action
  entry, no diff, but using the fallback-construction pattern that guarantees
  logging happens even if `_auditService` was injected as null.
- `ResetPassword`, `UnlockAccount`, `ToggleUserFreeze` call **no audit method
  at all**. Confirmed by reading the full method bodies.
- `UserServiceTests.cs` already has a working pattern for audit verification:
  `UpdateUser_WhenAuditServiceProvided_LogsChangesWithPermissions` injects
  `Mock<IAuditService>` and asserts via `.Verify(a => a.LogWithChanges(...))`
  with `It.Is<string>(...)` substring checks on the serialized JSON.

## Architectural decision
Add audit logging to all three methods using the `_auditService ?? new
AuditService(_dbFactory, this)` fallback pattern (matching `DeleteUser`/
`RestoreUser`), not the `_auditService?.` pattern (matching `CreateUser`/
`UpdateUser`) — a fix for "audit entries are missing" must not itself risk
silently skipping the entry when `_auditService` is null.

1. **`ResetPassword`**: after the existing mutations, call
   `auditService.Log("ResetPassword", "Users", userId, $"إعادة تعيين كلمة مرور
   المستخدم: {user.FullName} (@{user.Username})")`.
   **Hard constraint: never serialize or log `PasswordHash` (old or new), in
   any form, under any circumstance, in this method.** No `LogWithChanges`
   call here — bare `Log()` only.

2. **`UnlockAccount`**: capture `oldValuesObj = new { user.FailedLoginAttempts,
   user.LockoutEnd }` **before** the two mutation lines execute. After
   `db.SaveChanges()`, call `auditService.LogWithChanges("UnlockAccount",
   "Users", userId, $"فك قفل حساب: {user.FullName} (@{user.Username})",
   JsonSerializer.Serialize(oldValuesObj), JsonSerializer.Serialize(new {
   FailedLoginAttempts = 0, LockoutEnd = (DateTime?)null }))`.

3. **`ToggleUserFreeze`**: capture `oldValuesObj = new { user.IsActive }`
   before the toggle line. After `db.SaveChanges()`, call
   `auditService.LogWithChanges("ToggleUserFreeze", "Users", userId, $"...",
   JsonSerializer.Serialize(oldValuesObj), JsonSerializer.Serialize(new {
   user.IsActive }))` (post-mutation `user.IsActive` is the new value).

No change to method signatures, return values, or the authorization guards
(`RequireAdmin`) already present. No change to `CreateUser`/`UpdateUser`/
`DeleteUser`/`RestoreUser` — their existing patterns are correct and out of
scope.

## Allowed files
- `Sources-System-Project/Services/UserService.cs`
- `Sources.Tests/UserServiceTests.cs`
- `docs/release-readiness.md`
- `docs/session-summary.md`

## Forbidden scope
- `LoginWindow`, `LoginView`, `SplashWindow`
- `SourceService.cs`, `LocationService.cs`, or any other service (the broader
  OldValues/NewValues unification across all services is a separate, later
  round — do not touch them here)
- Logging any password hash, plaintext, or salt in any form
- Unrelated cleanup or refactoring of `CreateUser`/`UpdateUser`/`DeleteUser`/
  `RestoreUser`

## Acceptance criteria
1. `ResetPassword` writes exactly one audit entry with action
   `"ResetPassword"`, `TableName = "Users"`, correct `RecordId`, and **no**
   `OldValues`/`NewValues` containing any password material — verify by
   asserting the log's `Details`/`OldValues`/`NewValues` never contain the
   old or new plaintext/hash used in the test.
2. `UnlockAccount` writes one entry with action `"UnlockAccount"` and
   `OldValues`/`NewValues` reflecting the actual pre/post
   `FailedLoginAttempts`/`LockoutEnd`.
3. `ToggleUserFreeze` writes one entry with action `"ToggleUserFreeze"` and
   `OldValues`/`NewValues` reflecting the actual pre/post `IsActive`.
4. All three continue to behave exactly as before functionally (existing
   `UnlockAccount_*`, `ResetPassword_*`, `ToggleUserFreeze_*` tests in
   `UserServiceTests.cs` must still pass unmodified).
5. New dedicated tests added (mirroring `UpdateUser_WhenAuditServiceProvided_
   LogsChangesWithPermissions`'s `Mock<IAuditService>` + `.Verify(...)`
   pattern) for each of the three methods.
6. Documentation updated in the same commit.

## Required commands
```powershell
dotnet test -c Debug
dotnet test -c Release
```

## Migration protocol
Not applicable.

## Expected test baseline
- Debug/local expected count: 1082 (1079 + 3 new tests)
- Release/CI expected count: 1080
- Documented conditional-test difference: `TestDataGeneratorTests` under `#if DEBUG`

## Visual verification by Edrees
Not required.

## Completion report requirements
- Base/result SHA, Draft PR URL, per-file justification, exact test counts,
  build warnings, deviations or `none`, remaining risks.
