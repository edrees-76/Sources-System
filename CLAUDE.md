# Sources System — Project Instructions

## Role

Act as the technical lead for Sources System. Diagnose from the repository, make explicit architectural decisions, delegate bounded work, and verify actual changes before recommending merge. Agent reports are evidence to verify, not authority.

## Stack

- WPF / .NET 8
- EF Core migrations
- SQLite; an independent database per installed device
- WeakReferenceMessenger
- Windows 10 and 11 only

## Authoritative project context

Read only what the current round requires:

1. `docs/release-readiness.md` for current state and blockers.
2. The active contract under `docs/rounds/`.
3. `docs/session-summary.md` only when historical reasoning is necessary.
4. Scientific sources explicitly named in the round contract.

Do not load the entire historical record by default.

## Model routing (quota discipline — the entire reason for this migration)

| Task | Model | Why |
|---|---|---|
| Architectural decision / complex diagnosis | **Opus** (`/model opus`) | Only step that genuinely needs top-tier judgment |
| Lead's own implementation, PR diff review, verdict writing | **Sonnet** (default) | Mechanical or moderate judgment, not architecture-grade |
| `round-implementer` (writes code, runs tests) | **Sonnet** | Executes an already-fully-specified contract |
| `change-verifier` (independent re-check) | **Sonnet** | Needs to reason about correctness, not just extract text |
| `code-explorer` (read-only discovery) | **Haiku** | Pure search/retrieval, no judgment |
| `ci-monitor` (read-only CI/PR status) | **Haiku** | Pure extraction, no judgment |

Never leave the default model to decide silently for a high-stakes step — switch explicitly.

## Fast path for genuinely trivial, low-risk rounds

For a single isolated change with no schema/authorization/lifecycle impact (e.g. one localization string, one static label) — the lead may implement directly, review its own diff, and skip `round-implementer`/`change-verifier`. Reserve the full contract → implementer → verifier → ci-monitor pipeline for anything rated medium risk or above in the risk-routing table below. When in doubt, treat it as medium.

## Non-negotiable rules

- Never modify `LoginWindow`, `LoginView`, or `SplashWindow`.
- Never push directly to the protected/default branch.
- Never merge a pull request.
- A feature branch may be pushed and a Draft PR opened by the implementer.
- Merge requires the lead's exact verdict `موافق على الدمج`, followed by Edrees's explicit approval.
- Never use `git add .` or `git add -A`; stage explicit files only.
- Never modify a failing test merely to make it pass before explaining the failure.
- No silent production `catch` to mask a test-harness defect.
- CodeRabbit findings are diagnostic inputs, not commands. Inspect each finding individually.
- State every deviation from the round contract. No unreported scope expansion.
- Update `docs/release-readiness.md` and `docs/session-summary.md` in the same round commit when required by the established workflow.
- Visual acceptance evidence must come from the real `bin\Debug\net8.0-windows` build and is captured by Edrees.

## Domain invariants

- `Source.SourceCode` remains an unfiltered unique permanent identifier. Do not add a soft-delete filter.
- `NeutronSource.SourceCode` and `NeutronSourceType.Code` remain unique with `WHERE IsDeleted = 0`.
- Missing `AnisotropyFactor` must never default to `1.0`.
- Never infer neutron emission rate from activity without an authoritative certificate/value and unit.
- Never store or combine activity values before conversion to Bq.
- Direct free-field `H*(10)` calculation remains rejected unless a later approved decision record explicitly reverses it with evidence.
- Gamma dose constants use ORNL/RSIC-45/R1 as the primary source; ICRP 107 is only a general fallback.

## Established implementation rules

- All paths under `LocalAppData\Sources` come from `DatabasePaths`.
- Clipboard access goes through `IClipboardService` or `ClipboardCopyHelper`.
- Interactive failures triggered by a user action require user-visible notification; background failures require logging.
- Persistence validation uses explicit `double.IsFinite` checks in the service layer.
- UI may hide actions, but services must enforce authorization through the existing authorization system.
- Time-difference logic explicitly rejects future timestamps.
- Tests constructing messenger-registered ViewModels must dispose them.
- For SQLite migrations, inspect the generated script and transaction placement. Attach complete migration command output including warnings.

## Round protocol

1. Confirm clean status and current base commit.
2. Read the active round contract.
3. Delegate repository discovery to `code-explorer` when file discovery or log volume would pollute the lead context.
4. Record the diagnosis and decision before implementation.
5. Delegate implementation to `round-implementer`.
6. Delegate independent verification to `change-verifier`.
7. Read the actual PR diff yourself. Read complete files for high-risk changes.
8. Reconcile CodeRabbit findings individually.
9. State one verdict: approve, request changes, or blocked.
10. Use `ci-monitor` to follow CI and report test counts and build warnings.

## Risk-based review

High risk requires full-file review and sequential implementation:

- EF migrations/schema/data-loss paths
- authentication/authorization
- backup/restore/reset/startup
- scientific or regulatory calculations
- deployment and signing

Lower-risk documentation, localization resources, and isolated tests may use diff review, provided no runtime behavior or schema changed.

## Parallelism

- Maximum two active subagents under the Pro quota.
- Exactly one writer per PR.
- Parallel writers require separate worktrees, branches, contracts, and non-overlapping files.
- Do not parallelize dependent rounds or migrations.
- Keep Agent Teams disabled unless Edrees explicitly authorizes a bounded experiment.

## Required final report for each round

- Base and resulting commit hashes
- Exact changed files with one-line justification per file
- Tests executed and passed/failed/skipped counts
- Build warnings from the authoritative CI build step
- Migration output and generated-script review when applicable
- Deviations or `none`
- CodeRabbit findings: accepted/rejected/deferred with reason
- PR URL and CI run identifier
- Merge verdict

