# Claude Code Workflow — Sources System

## Purpose

Preserve the existing verification standard while reducing duplicated context, repeated prompts, and manual transfer between Claude and Antigravity.

## Authority boundaries

| Actor | May read | May edit | May commit | May push feature branch | May open Draft PR | May merge |
|---|---:|---:|---:|---:|---:|---:|
| Edrees | Yes | Yes | Yes | Yes | Yes | Yes |
| Lead | Yes | Only when explicitly acting as implementer | By exception | By exception | By exception | No |
| Explorer | Yes | No | No | No | No | No |
| Implementer | Yes | Contract scope | Yes | Yes | Yes | No |
| Verifier | Yes | No | No | No | No | No |
| CI monitor | Yes | No | No | No | No | No |
| CodeRabbit | Review | No | No | No | No | No |

Feature-branch push is permitted to create a Draft PR. It is not approval to merge or push to the protected branch.

## Quality gates

### Gate 1 — Contract

No implementation without a round contract containing a full base SHA, allowed files, acceptance criteria, tests, and risk rating.

### Gate 2 — Implementation

The implementer must stop on base mismatch, protected-file contact, unexplained test failure, or required scope expansion.

### Gate 3 — Independent verification

The verifier checks actual repository state and repeats critical tests. A report without diff inspection is invalid.

### Gate 4 — Lead judgment

The lead reads every PR diff and complete high-risk files. It individually classifies CodeRabbit findings and issues a written verdict.

### Gate 5 — Owner approval

Edrees performs required visual verification and explicitly authorizes merge.

### Gate 6 — CI evidence

Record commit SHA, run ID, job conclusions, exact test counts, and authoritative build warnings.

## Risk routing

| Risk | Examples | Execution | Review |
|---|---|---|---|
| Low | Documentation, isolated resource text | May run parallel | Diff + targeted checks |
| Medium | ViewModel behavior, isolated service change | One writer per PR | Diff + affected full files |
| High | Authorization, backup, startup, lifecycle | Sequential | Full files + independent tests |
| Critical | Migration, reset/data loss, scientific calculation, deployment/signing | Sequential only | Full files, specialist evidence, rollback/data checks |

## Parallel-work policy

Two implementation PRs may coexist only when contracts declare `Parallel-safe: yes`, their file sets do not overlap, neither depends on the other, and neither creates a competing migration. Merge them one at a time and rebase/reverify the second after the first merge.

Read-only discovery may run beside one implementation task. Under Claude Pro, keep total active subagents at two or fewer.

## Context policy

- `/clear` before every new round.
- `/compact` only when continuing the same long round.
- Load the current readiness board and round contract first.
- Load historical summaries and large scientific references only when the contract requires them.
- Keep raw command output outside the lead conversation when possible; return exact counts and paths, but preserve mandatory full migration output.

## Rollout for the current roadmap

1. Finish 115-ب with the current process.
2. Pilot this workflow on 116/117.
3. In parallel, allow only read-only AuditLog inventory.
4. Review two complete pilot rounds.
5. Execute ب4 sequentially because it combines scientific decisions and schema migration.
6. Start ب5 only after ب4 merges.
7. Run ب6 independently only after overlap analysis.
8. Finalize ب7 after neutron behavior stabilizes.
9. Execute ب8 sequentially with release-specific gates.

