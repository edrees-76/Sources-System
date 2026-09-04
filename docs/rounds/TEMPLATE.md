# Round XXX — Title

## Identity

- Owner: Edrees
- Lead: Claude Code
- Base branch: `main`
- Base commit: `REQUIRED_FULL_SHA`
- Working branch: `round-xxx-short-name`
- Risk: `low | medium | high | critical`
- Parallel-safe: `yes | no`

## Goal

State one observable outcome.

## Evidence and diagnosis

List verified code facts. Separate inference from observation.

## Architectural decision

State the selected approach and why it is preferred over the material alternatives.

## Allowed files

- `path/to/file`

Any additional file requires a reported deviation and lead approval before editing.

## Forbidden scope

- `LoginWindow`
- `LoginView`
- `SplashWindow`
- Unrelated cleanup or refactoring
- Direct push to the protected/default branch
- PR merge

## Acceptance criteria

1. Required behavior.
2. Regression-test requirement.
3. Compatibility or localization requirement.
4. No new warnings unless explicitly accepted.
5. Documentation updated in the same commit when required.

## Required commands

```powershell
dotnet test
```

Add targeted commands and the authoritative solution/configuration explicitly.

## Migration protocol

Write `not applicable` or specify:

- Exact `dotnet ef migrations add` command.
- Required full output capture.
- Generated SQL/script inspection.
- Upgrade and downgrade/data-preservation checks.

## Expected test baseline

- Debug/local expected count:
- Release/CI expected count:
- Documented conditional-test difference:

## Visual verification by Edrees

Write `not required` or list exact screens and states to capture from `bin\Debug\net8.0-windows`.

## Completion report requirements

- Base/result SHA.
- Draft PR URL.
- Per-file justification.
- Test passed/failed/skipped counts.
- Build warnings.
- Migration evidence where applicable.
- Deviations or `none`.
- Remaining risks.

