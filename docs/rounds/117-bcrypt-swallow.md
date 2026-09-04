# Round 117 — Log BCrypt verification failures instead of swallowing them

## Identity
- Owner: Edrees
- Lead: Claude Code
- Base branch: `main`
- Base commit: `0fd62aabe15e08a435b61d67f49268a932f0df66`
- Working branch: `round-117-bcrypt-logging`
- Risk: `high` (touches authentication path — PasswordHelper.VerifyPassword)
- Parallel-safe: `no`

## Goal
`PasswordHelper.VerifyPassword` currently has a bare `catch` that swallows any
BCrypt exception (malformed hash, legacy SHA256 hash, unsupported format) and
returns `false` — indistinguishable from a genuinely wrong password, with zero
diagnostic trail. Make the failure visible via logging without changing the
method's return contract or any caller's behavior.

## Evidence and diagnosis
- File: `Sources-System-Project/Helpers/PasswordHelper.cs`
- `VerifyPassword` wraps `BCrypt.Net.BCrypt.Verify(password, hash)` in
  `try { return ...; } catch { return false; }` — the catch has no logging.
- `LoggerService.LogError(string, Exception)` already exists and is the
  established pattern used elsewhere in this codebase for this exact purpose.
- Do not change the return value, the method signature, or add any new
  parameter. Do not change caller behavior. This is a pure observability fix.

## Architectural decision
Add `LoggerService.LogError(...)` inside the existing `catch (Exception ex)`
block (name the previously-unnamed exception variable), logging that BCrypt
verification threw, without altering control flow or the `false` return.
No new abstraction, no new interface — matches the established
"never silent-swallow without logging" rule in `CLAUDE.md`.

## Allowed files
- `Sources-System-Project/Helpers/PasswordHelper.cs`
- `Sources.Tests/PasswordHelperTests.cs` (create if it doesn't exist)
- `docs/release-readiness.md`
- `docs/session-summary.md`

## Forbidden scope
- `LoginWindow`, `LoginView`, `SplashWindow`
- Any change to `VerifyPassword`'s return value or signature
- Any change to `HashPassword`
- Unrelated cleanup

## Acceptance criteria
1. A malformed/invalid hash passed to `VerifyPassword` still returns `false`.
2. The exception is now logged via `LoggerService.LogError` with a message
   identifying the source as BCrypt verification failure.
3. A new or existing test proves the pre-fix defect (silent swallow) would
   have gone undetected, and now the log path is exercised.
4. No change to `Login`/`UserService` call sites.
5. Documentation updated in the same commit.

## Required commands
```powershell
dotnet test -c Debug
dotnet test -c Release
```

## Migration protocol
Not applicable.

## Expected test baseline
- Debug/local expected count: 1079 (1078 + 1 new test)
- Release/CI expected count: 1077
- Documented conditional-test difference: `TestDataGeneratorTests` under `#if DEBUG`

## Visual verification by Edrees
Not required.

## Completion report requirements
- Base/result SHA, Draft PR URL, per-file justification, exact test counts,
  build warnings, deviations or `none`, remaining risks.