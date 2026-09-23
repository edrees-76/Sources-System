ROUND 195 — Small production items + CodeRabbit LOW rows + worktree cleanup

Save THIS TEXT VERBATIM as docs/rounds/195-small-items-batch.md (you may append a separate
"Discovery findings" section below it). Commit it in the round's PR.
BASE: main at 9769602 (round 194, PR #90 merged). Branch: chore/round-195-small-items-batch
(if a name collision forces a different name, report it as a deviation).
Risk: MEDIUM overall (many small independent changes). Full pipeline: implementer -> your own diff
review -> change-verifier -> ci-monitor.
ONE COMMIT PER GROUP (A, B, C ... J below), in order, each with a message starting "R195-<letter>:".
This keeps the round bisectable. Keep every change minimal.

STEP 0 — Worktree cleanup (report the table before removing anything)
For EVERY entry in `git worktree list` except D:\Sources-System and the worktree this session runs in:
  path | branch | HEAD | status clean? | unpushed commits | PR state (`gh pr list --state all --head <branch>`)
Merges are SQUASH merges: use PR state, not ancestry, as the merge signal.
Remove (`git worktree remove <path>`, never --force) only if: clean, nothing unpushed, PR MERGED.
Then `git branch -D <branch>` only if PR MERGED and local tip == remote tip.
SPECIAL CASE agent-ad1b968703bae6642 (local-only commit e6a6fd7, no PR): this is round 193's original
commit, squash-merged via PR #89. Verify: for every CODE file in `git show --stat e6a6fd7`
(exclude docs/), `git diff e6a6fd7 354128b -- <file>` must be EMPTY. If all empty -> remove the
worktree and `git branch -D` its branch. If any diff is non-empty -> keep it and report the diff.
Local branches cool-knuth-9e21eb and neutron-sources-current-emission-rate(-*): delete with
`git branch -d` (safe: refuses if not merged). Report if refused.
Folders under .claude\worktrees that are NOT registered in `git worktree list`
(e.g. neutron-sources-current-emission-rate-642203, round-193-neutron-current-activity-928b45):
list their contents recursively. If they contain ONLY bin/, obj/, .vs/ or nothing, delete them with
`Remove-Item -Recurse`. If they contain anything else, do not touch — report it.
Finish with `git worktree prune`. Continue to STEP 1 even if some items are kept.

STEP 1 — Discovery (read-only). STOP and report before coding if any assumption is false.
D1. LoggerService.cs:9 builds the log path manually instead of using DatabasePaths.
D2. SettingsHelper legacy-settings migration abandons the old file after a transient copy failure
    (round 105-b). Quote the code path.
D3. Where the "SourceCode already exists" message is produced during RESTORE (source and neutron
    source); quote current ar/en text.
D4. UsersViewModel.cs:404 (" ، ") and :436 ("، ") Arabic list separators.
D5. TestDataGeneratorService: confirm it creates no NeutronSource rows; confirm it is DEBUG-only
    or how it is reachable in the UI.
D6. ActivityCalculatorViewModel "unit duplication": describe exactly what is duplicated (unit list
    hardcoded vs db.ActivityUnits? conversion factors?) with file:line.
D7. UpdateAllCurrentActivities: every reference (interface, service, tests). Confirm zero production callers.
D8. AllModels.cs DisplaySourceCode "(محذوف)" literal (3 places ~361/~715/~1096). CRITICAL: list every
    consumer of DisplaySourceCode, and state whether ANY of them builds an _auditService.Log / AuditLog
    text. Audit logs must stay Arabic ALWAYS (final decision). Also: does any Model class already call
    TranslationHelper?
D9. Installer Hindi digits in the wizard title bar: identify the cause (Arabic.isl message string,
    Windows digit substitution, or other).
D10. MsgErrAdminOnlyAction (SettingsViewModel.cs:501): ar/en values vs MsgErrAdminOnly.
D11. The catches in LeakTestsViewModel, SettingsViewModel, SourcesViewModel, AlertsViewModel that show an
     error dialog but do not log (listed in round 194 backlog): file:line.
D12. CodeRabbit rows: AllModels.cs:1114-1116 ActivityValueFormatted (Arabic "غير مسجّل" + "42 " with
     empty unit); SourcesView.xaml:810 and NeutronSourceDetailsWindow.xaml:105 bind NameAr always —
     find the EXISTING project pattern for language-aware names (e.g. Radioisotope Name/ArabicName);
     SourcesViewModel.cs:131-143 + :444-459 double load on tab switch; ScientificNotationParser.cs:193
     only strips U+0020 and rebuilds regexes per call.

STEP 2 — Changes (one commit per group)
A. Small tech debt:
   A1 LoggerService: use DatabasePaths for the log directory (project rule). Same resulting path.
   A2 SettingsHelper: do not abandon the legacy file after a failed copy; keep it so the next launch
      retries. Log the failure (already logs? keep). No change when copy succeeds.
   A3 Restore duplicate-code message: make it state clearly that an ACTIVE source already uses this
      code and the deleted one cannot be restored until that code is changed. New keys ar+en.
   A4 UsersViewModel separators: move both to resource keys (ar keeps exact current text; en ", ").
   A5 TestDataGeneratorService: add generation of a small number (≤5) of neutron sources using existing
      seeded neutron types and locations. Only if D5 shows it is a dev/test tool; otherwise skip and report.
B. Calculator: remove the duplication found in D6 by reusing the single existing source of truth.
   If D6 shows the fix touches conversion math or more than ~40 lines, STOP and report instead.
C. Delete UpdateAllCurrentActivities (interface member + implementation) and the tests that exist only
   to test it. No other change.
D. "(محذوف)": if D8 shows DisplaySourceCode feeds ANY audit-log text -> STOP this group, report, and move
   it to the backlog (do not change it). Otherwise replace the literal with
   TranslationHelper.GetString("TextDeletedSuffix") ?? "(محذوف)" (exact Arabic fallback), keys ar+en.
E. Installer digits: only if D9 shows a message-string cause fixable via [Messages]/[CustomMessages]
   overrides in deploy\installer.iss. If the cause is Windows digit substitution or unclear -> skip,
   report, backlog. Do NOT run the installer build in this session unless ISCC is available; if not,
   leave visual verification to Edrees.
F. MsgErrAdminOnlyAction: if its meaning equals MsgErrAdminOnly, switch the usage to MsgErrAdminOnly and
   remove the unused key from both dictionaries; update test assertions without weakening them.
   If the meaning differs, leave it and report.
G. D11 catches: add LoggerService.LogError inside each; keep the existing dialog exactly as is.
H. CodeRabbit LOW rows:
   H1 ActivityValueFormatted: translated "not recorded" via TranslationHelper with exact Arabic
      fallback; no trailing space when unit symbol is empty.
   H2 Neutron type name under code: use the existing language-aware pattern from D12. If no such pattern
      exists in the project, skip, report, backlog.
   H3 Tab switch: load data once per tab switch; the "no results" dialog must appear at most once.
   H4 Parser: also strip U+00A0 and U+202F; make regexes static readonly (RegexOptions.Compiled).
      Parsing results for all existing inputs must be unchanged.
I. Tests for every behavior change: A2, A3, D (English shows translated suffix; Arabic unchanged),
   H1, H3 (single load/single dialog), H4 (NBSP inputs parse; existing parser tests unchanged).
   Tests for C are deletions only.
J. Documentation:
   - session-summary.md: round 195 entry (CI Release and local Debug counts stated separately).
     Include a correction note: the round-124 entry's Am-241 half-life "432.6" is superseded; the
     project value is 432.2 y (ICRP-107, consistent with the app's isotope library and the
     manufacturer value 157850 d). Do not edit the old line (append-only).
   - release-readiness.md: header (main at round 194 / 9769602, round 195 under review); close each
     completed item; in §4 record as final decisions: (1) audit log stays Arabic regardless of UI
     language, (2) SourceCertificate.AttachedBy -> Guid? rejected permanently (risk to regulatory
     attachment-provenance data outweighs consistency benefit), (3) CodeRabbit suggestion to remove
     IsDefault from SourceFormWindow rejected (intended design: Enter must not save from step 1);
     update the CodeRabbit follow-up line: triage done, real total 44 (not 51), 14 still valid,
     PRs #18–#90 never auto-reviewed; list rows routed to rounds 196/197; add any skipped group
     (D, E, H2, B, A5) to "قائمة ما بعد الإصدار" with its reason.

FORBIDDEN
- LoginWindow, LoginView, SplashWindow. PhraseFactoryResetConfirmation / RequiredResetPhrase.
- UserService.UnlockAccount. SimpleArabicStatus (reserved for the status-enum rounds).
- NeutronSourceService.Restore logic and AppDbContext legacy Am-241/Be handling (reserved for round 197).
- LocationFormWindow / UserFormWindow and the test-hardening rows (reserved for round 196).
- Any audit-log text. Any decay math or scientific constant in code.
- No EF migration. No `git add .` / `-A`; explicit pathspecs; `git show --stat` per commit with a
  one-line justification per file. Never delete worktree folders except as allowed in STEP 0.

GIT
- Commits A..J in order, push, open a DRAFT PR to main. Do not merge.

REPORT
- STEP 0 table (removed / kept + reason).
- D1–D12 findings with quotes/evidence; list every group skipped or stopped, with reason.
- Per commit: hash, files, justification.
- Test counts: local Debug and CI "Build and Test" (CI baseline 1294; state new/removed tests).
- CI warnings vs the 3 known CS8604.
- Worktree path holding the final commit + exact PowerShell commands (cd, git log -1 --oneline,
  dotnet build Sources.sln -c Debug, launch exe) for Edrees's visual check.
- A short visual checklist for Edrees covering every user-visible change in this round.

---

## Discovery findings (lead, base 9769602)

### STEP 0 — worktree cleanup

| Path | Branch | HEAD | Clean | Unpushed | PR | Action |
|---|---|---|---|---|---|---|
| agent-a9da21004d2c78acc | chore/round-194-final-consolidation | 1aa2b03 | yes | none (remote branch deleted after merge; local tip == PR #90 head 1aa2b03) | #90 MERGED | worktree removed; branch -D |
| agent-ad1b968703bae6642 | worktree-agent-ad1b968703bae6642 | e6a6fd7 | yes | local-only | none (special case) | all 5 code files of e6a6fd7 have EMPTY diff vs 354128b → worktree removed; branch -D |
| coderabbit-review-triage-f578d9 | claude/coderabbit-review-triage-f578d9 | 9769602 | yes | none | none | KEPT (no merged PR; may belong to another session) |
| round-194-final-consolidation-073ef3 | claude/round-194-final-consolidation-073ef3 | 354128b | yes | none | none | KEPT (no merged PR) |

- Local branches `claude/cool-knuth-9e21eb` (4b038ba) and `claude/neutron-sources-current-emission-rate-642203` (bb64808): deleted with `git branch -d` (not refused).
- Unregistered folders: `initial-activity-source-details-c6ba63` (empty) deleted. `neutron-sources-current-emission-rate-642203` and `round-193-neutron-current-activity-928b45` are empty but `Remove-Item` failed ("being used by another process") — kept; Edrees deletes manually after releasing the holding process (same state as round 194).
- `git worktree prune` executed.

### D1 — confirmed
`Services/LoggerService.cs:8-9` builds `Path.Combine(LocalApplicationData, "Sources", "Logs")`. `Data/DatabasePaths.cs` has `AppDataDirectory` (= LocalApplicationData\Sources) and the sibling pattern `BackupsDirectory`. Fix: add `DatabasePaths.LogsDirectory => Path.Combine(AppDataDirectory, "Logs")` and use it. Identical path.

### D2 — ASSUMPTION FALSE
`Helpers/SettingsHelper.cs:31-74` `MigrateLegacySettings`: the legacy file is never deleted or renamed. On copy failure (catch at :59) a `settings.ini.pending` flag is written (:62-68) so the next launch retries (:45 condition); the flag is removed only after a confirmed successful copy (:53-56). This is the CodeRabbit fix already recorded in release-readiness (row 5, SettingsHelper). The failure IS logged: returned as `MigrationWarning`, logged by `App.xaml.cs:87-88` via `LoggerService.LogWarning`. Only residual edge: if the copy leaves a partial target AND writing the pending flag also fails (`catch { }` at :69), the next launch sees target-exists + no-flag and skips the retry. No tests cover `MigrateLegacySettings`. The backlog line "abandons the old file" is stale.

### D3 — confirmed (wording only)
- Source: `SourceService.RestoreSource` (:430-434) key `MsgErrSourceRestoreConflict` — ar «لا يمكن استرجاع المصدر لوجود مصدر نشط آخر بنفس الكود ({0})», en "Cannot restore the source because another active source with the same code exists ({0})".
- Neutron: `NeutronSourceService.Restore` (:395-397) key `MsgErrNeutronSourceRestoreConflict` — ar «لا يمكن استرجاع المصدر النيتروني لوجود مصدر نشط آخر بنفس الكود ({0})», en "Cannot restore the neutron source because another active source with the same code exists ({0})".
- Keys used nowhere else; no test asserts them. Changing the neutron message touches only the key/fallback literal inside `NeutronSourceService.Restore` (no logic).

### D4 — confirmed
`UsersViewModel.cs:404` `string.Join(" ، ", deltaList)` (permission delta) and `:435` `string.Join("، ", translatedNames)`. Both build `AuditDiffItem` display text for the Users screen history panel (ObservableCollection); neither is passed to `_auditService`.

### D5 — confirmed
`Services/TestDataGeneratorService.cs` is wrapped in `#if DEBUG` (:1/:620); creates Locations, Sources (+ isotopes/history) and BorrowRequests — no NeutronSource. Reachable only from `SettingsViewModel.GenerateTestDataCommand` (inside `#if DEBUG` :563-630) bound in `SettingsView.xaml:1231`. `TestDataGeneratorTests.cs` is `#if DEBUG` (excluded from CI Release). Seeded neutron types: AppDbContext:179-189.

### D6 — B STOPPED
Three copies of unit knowledge: (1) `ActivityCalculatorViewModel.cs:106-109` hardcoded symbol list; (2) `DecayCalculationService.ConvertFromBq`/`ConvertToBq(string)` switch factors (:67-81, :94-108); (3) `db.ActivityUnits` seeded with `ConversionToBq` (AppDbContext:76-107). Factors are equal. The calculator VM has no DB access and no existing service exposes the unit list; the substantive duplication is the conversion factors (switch vs DB). Removing it touches conversion math → STOP per contract; backlog.

### D7 — confirmed
`ISourceService.cs:14`, `SourceService.cs:472-495`, tests `SourceServiceTests.cs` region "4. UpdateAllCurrentActivities Tests" (:790-856, two [Fact]s, both only exercise this method). One other mention: a comment at `SourceServiceTests.cs:960`. Zero production callers.

### D8 — D proceeds
Literal at `AllModels.cs:361`, `:715-716`, `:1096`. Consumers: XAML display (BorrowView, BorrowFormWindow, LocationDetailsWindow, SourceDetailsWindow, SourcesView), ViewModel pass-throughs (Borrow, Locations, Reports, SourceDetails, Sources), `LocationDetailsViewModel.cs:157` search filter, `ReportingService.cs:442/528/718/853` (Excel/PDF report cells). NONE builds `_auditService.Log`/AuditLog text. A Model class already calls TranslationHelper (`AllModels.cs:813`, Role.DisplayName). Tests asserting the Arabic suffix (BorrowServiceTests:916-921, LocationServiceTests:902, LocationsViewModelTests:153) stay valid under the Arabic fallback.

### D9 — E SKIPPED
Title template is `SetupWindowTitle=تثبيت - %1` from Inno's stock `compiler:Languages\Arabic.isl` (not bundled in the repo; `RightToLeft=yes`). No Arabic-Indic code points (U+0660–U+0669) exist in Arabic.isl or under `deploy/`; `AppVersion` is ASCII. The shapes come from Windows digit substitution rendering ASCII digits in the RTL Arabic wizard, not from a message string. ISCC.exe is installed but was not run.

### D10 — F proceeds
ar values are identical («غير مصرح: هذه العملية مخصصة لمدير النظام فقط»); en differs only in wording ("action … System Administrators" vs "operation … system administrators"). Single usage `SettingsViewModel.cs:502`. No test references `MsgErrAdminOnlyAction`.

### D11 — confirmed (lines shifted)
`LeakTestsViewModel.cs:436` (PDF export), `:459` (Excel export); `SettingsViewModel.cs:551` (factory reset failure), `:621` (DEBUG test-data generation); `SourcesViewModel.cs:1461` (save), `:1498` (PDF), `:1513` (Excel), `:1623` (image load); `AlertsViewModel.cs:202`. Nine catches.

### D12
- (a) `AllModels.cs:1113-1116` `ActivityValueFormatted` confirmed; existing key `TextNotRecorded` (ar «غير مسجّل», en "Not recorded") is reused — no new key.
- (b) `SourcesView.xaml:810` binds `NeutronSourceType.NameAr`; `NeutronSourceDetailsWindow.xaml:105` binds `TypeNameAr` (VM :46). Existing pattern: `Radioisotope.DisplayName` (`AllModels.cs:85-93`) via `CurrentUICulture.TwoLetterISOLanguageName == "ar"` (culture set in App.xaml.cs:51/:198). H2 proceeds with `NeutronSourceType.DisplayName` in the same pattern.
- (c) `SourcesViewModel.cs:131-143` `OnSelectedTabChanged` fires `_ = LoadNeutronDataAsync()`/`LoadDeletedDataAsync()`; `SwitchToNeutronSourcesAsync`/`SwitchToDeletedSourcesAsync` (:443-459) set `SelectedTab` then `await` the same load again → two loads; with non-empty SearchText and zero hits the "no results" `DialogHelper.ShowInfo` fires twice. Tests set `vm.SelectedTab = "Neutron"` directly and rely on the handler load.
- (d) `ScientificNotationParser.cs:193` strips only U+0020 (Trim at :190 removes edge NBSP, not interior); three `Regex.Match` calls with inline patterns at :50/:71/:83. No dedicated parser test file.

## Lead decisions (after Edrees's answers, 2026-09-23)

- **A2 SKIPPED (Edrees):** no code change. release-readiness: correct the stale "abandons the old file" line and record the residual edge (partial target + failed flag write → retry skipped) in the post-release list.
- **A3 allowed message-only (Edrees):** in `SourceService.RestoreSource` and `NeutronSourceService.Restore` change ONLY the message key and its Arabic fallback. No change to any condition, control flow, return value or check order. Round 197 builds on this message.
- **B STOPPED** (D6: conversion math). **E SKIPPED** (D9: Windows digit substitution). Both go to the post-release list.
- A4 resource values need `xml:space="preserve"` (existing pattern, Strings.ar.xaml:1803/1834) so " ، " keeps its spaces.
- H1 reuses the existing `TextNotRecorded` key.
- H2 adds `NeutronSourceType.DisplayName` following `Radioisotope.DisplayName`.
- Commits: A, C, D, F, G, H, I, J (B and E produce no commit). The C commit carries its own test deletions so every commit builds.
