ROUND 194 — FINAL consolidation round for Sources System (low-risk items only) + post-release backlog

Save THIS TEXT VERBATIM as docs/rounds/194-final-consolidation.md (do not rewrite it into TEMPLATE.md;
you may append a separate "Discovery findings" section below it). Commit it in the round's PR.
BASE: main at 354128b (round 193, PR #89 merged). Branch: chore/round-194-final-consolidation
(if a worktree/branch-name collision forces a different name, report it as a deviation).
Risk: MEDIUM overall. Full pipeline: implementer -> your own diff review -> change-verifier -> ci-monitor.
This is the LAST planned round before the project is paused. Keep every change minimal.

STEP 0 — Housekeeping: clean up ALL stale worktrees (report the table before removing anything)
For EVERY entry in `git worktree list` except the main repo D:\Sources-System and the worktree this
session is running in, collect:
  path | branch | HEAD | `git -C <path> status --short` (clean?) |
  unpushed commits (`git -C <path> log @{u}..HEAD --oneline`, or "no upstream") |
  PR state for that branch (`gh pr list --state all --head <branch> --json number,state`)
Branches are merged by SQUASH, so `git branch -d` / ancestry checks will NOT detect merged branches —
use the PR state as the merge signal.

Remove a worktree ONLY if ALL are true: status clean, zero unpushed commits, PR state = MERGED
(or branch fully contained in main). Remove with `git worktree remove <path>` (never --force,
never delete folders manually). Then delete its local branch with `git branch -D <branch>` ONLY if
the PR is MERGED and the local tip equals the remote branch tip (nothing local-only).
Finish with `git worktree prune`.

Any worktree that fails a condition (dirty, unpushed, PR open/closed-unmerged, no PR, or in use):
DO NOT touch it — list it in the report with the reason, and continue with STEP 1.

STEP 1 — Discovery (read-only). STOP and report before coding if ANY assumption is false.
D1. SeedData (AppDbContext or wherever it lives) has a catch around the admin hash upgrade that
    swallows the exception without logging. Quote it.
D2. Keys MsgErrAdminOnly and MsgErrOperationAdminOnly: list every usage (file:line) and both ar/en values.
D3. Silent-swallow inventory from release-readiness §3 ("جرد 106"): App.xaml.cs (theme),
    LeakTestsViewModel, DashboardViewModel, SettingsViewModel, SourceDetailsViewModel,
    SourcesViewModel, AlertsViewModel, ActivityCalculatorViewModel. List every catch block that
    neither logs nor rethrows (file:line). EXCLUDE LoginWindow.xaml.cs (forbidden file).
D4. H*(10) Arabic wording: list every user-visible string and code comment using
    "الجرعة المحيطية" or "المكافئ المحيطي" for H*(10) (including AllModels.cs ~965).
D5. Round 149 (Enter/LostFocus, PR #44): is PR #44 merged? Is its commit an ancestor of main?
    (`gh pr view 44 --json state,mergedAt,mergeCommit`, `git merge-base --is-ancestor`).
D6. `Build and Test #82`: `gh run list` / `gh run view` — which commit, why red, and whether later
    runs on main are green. Read-only.

STEP 2 — Code changes (only these)
C1. SeedData catch: add LoggerService.LogError(<clear English context>, ex) inside the existing catch.
    No change to return values, flow, or signatures.
C2. Admin-only messages: make both code paths use ONE key. Keep the key whose Arabic text is the
    existing MsgErrAdminOnly text ("غير مصرح: هذه العملية مخصصة لمدير النظام فقط"). Delete the other
    key from BOTH dictionaries only if it has zero remaining usages. Update any test that asserts
    the removed text, without weakening the assertion.
C3. Silent swallows (D3 list only): add LoggerService.LogError or LogWarning inside each catch.
    Logging ONLY — no new dialogs, no rethrow, no change in user-visible behavior or control flow.
    If a catch is intentionally silent for a documented reason (comment says so), leave it and list it.
C4. H*(10): unify to ONE Arabic term across user-visible strings and comments found in D4.
    Use "المكافئ المحيطي للجرعة" unless D4 shows a dominant existing term — report which and why.
    Text-only change; no identifiers renamed.

STEP 3 — Documentation (same commit as code)
- docs/session-summary.md: append round 194 entry (verified numbers only; CI Release and local Debug
  counts stated separately).
- docs/release-readiness.md:
  a) Header: main state after round 193 (354128b), round 194 under review.
  b) §3: strike through and mark closed: SeedData swallow, admin duplicate messages,
     silent-swallow logging (list files), H*(10) wording (§5 "دَين تحسين مسجَّل").
  c) Round 149 item: if D5 shows merged, write "مدموج — بانتظار تحقق بصري نهائي من إدريس".
     If NOT merged, keep it open and move it to the backlog below. Do not merge PR #44 yourself.
  d) Build #82: record the D6 finding. If later main runs are green and #82 is on a superseded
     commit, mark it closed as obsolete.
  e) NEW section at the end of §3 titled "## قائمة ما بعد الإصدار (مُعلَّقة عمداً — المشروع متوقف
     مؤقتاً)" listing, one line each with its reason and any prior decision:
     status-string enum + hardcoded role name; IMessenger sweep; DialogHelper.IsTestMode isolation;
     PhraseFactoryResetConfirmation (high-risk, isolated round); AllModels.cs "(محذوف)" literal;
     audit-log language decision; UsersViewModel Arabic list separators; UTC unification;
     calculator unit duplication; SimpleArabicStatus duplication; SettingsHelper legacy-file
     abandonment; LoggerService.cs:9 manual path; SourceCertificate.AttachedBy -> Guid?;
     UpdateAllCurrentActivities removal (needs Edrees approval); TestDataGeneratorService without
     neutron sources; SourceCode duplicate-on-restore message clarity; installer Hindi digits;
     51 CodeRabbit comments (triage session, not a coding round); round 149 if not merged;
     any worktree left untouched in STEP 0 (with its reason).
  f) Remove or correct the stale note that the activation serial is a placeholder (fixed in round 169).

FORBIDDEN
- LoginWindow, LoginView, SplashWindow. PhraseFactoryResetConfirmation / RequiredResetPhrase.
- UserService.UnlockAccount (already fixed in round 190 — do not touch).
- Any refactor, rename, enum introduction, or behavior change beyond C1–C4.
- No EF migration. No `git add .` / `-A`; explicit pathspecs with per-file justification in
  `git show --stat`.
- Never delete worktree folders manually; never `git worktree remove --force`.

GIT
- Commit, push, open a DRAFT PR to main. Do not merge.

REPORT
- STEP 0 table: every worktree, its checks, and removed / kept (with reason).
- D1–D6 findings with quotes/evidence.
- Changed files + justification; list of catches left silent (with reason).
- Test counts: local Debug and CI "Build and Test" (CI expected = 1294 + new tests, if any).
- CI warnings vs the 3 known CS8604.
- Worktree path holding the final commit and exact PowerShell commands for Edrees to build and
  launch from it.
- If D5 = merged: exact manual steps for Edrees's one-time Enter-key visual check
  (RadioisotopeFormWindow: edit an isotope, change half-life, press Enter, reopen, confirm saved).

---

## Discovery findings (lead, before implementation — base 354128b)

### STEP 0 — worktrees

| Worktree | Branch | HEAD | Clean | Unpushed | PR | Result |
|---|---|---|---|---|---|---|
| agent-a6c6dfbe3e4b5aacb | fix/round-190-trial-mode-account-alerts-exemption | 47ef0c6 | yes | 0 | #86 MERGED | removed; branch deleted (local = remote tip) |
| agent-ad1b968703bae6642 | worktree-agent-ad1b968703bae6642 | e6a6fd7 | yes | no upstream; commit not in main | none | **kept** — local-only commit with no PR |
| cool-knuth-9e21eb | claude/cool-knuth-9e21eb | 4b038ba | yes | no upstream; HEAD contained in main | none | removed; branch kept (no PR → deletion not allowed) |
| docs-round-188-close-b8-f4a8c9 | docs/round-188-close-b8-deployment | 1140170 | folder already missing (prunable) | local = remote tip | #84 MERGED | pruned; branch deleted |
| neutron-sources-current-emission-rate-642203 | claude/neutron-sources-current-emission-rate-642203 | bb64808 | yes | no upstream; HEAD contained in main | none | deregistered by `git worktree remove`; empty folder left (Permission denied — directory handle held by another process); branch kept (no PR) |
| round-193-neutron-current-activity-928b45 | claude/round-193-neutron-current-activity-928b45 | 0be5ee1 | yes | no upstream set; local = origin tip | #89 MERGED | deregistered; empty folder left (Permission denied); branch deleted |

The two empty folders were NOT deleted manually (contract). Edrees may delete them after closing
whatever process holds them.

### D1 — `Sources-System-Project/Data/AppDbContext.cs:327`
`catch { /* تجاوز أي خطأ في التحقق */ }` — wraps the admin legacy-SHA256 → BCrypt upgrade and the
`MustChangePassword` flag; swallows with no logging. Confirmed.

### D2 — admin-only keys
- `MsgErrAdminOnly` — ar `Strings.ar.xaml:1267` «غير مصرح: هذه العملية مخصصة لمدير النظام فقط»;
  en `Strings.en.xaml:1269` "Unauthorized: This operation is restricted to system administrators only".
  Used by `Views/PasswordPromptDialog.xaml.cs:53`; test `DeletionsAndAdminPromptTests.cs:134`.
- `MsgErrOperationAdminOnly` — ar `Strings.ar.xaml:1272` «هذه العملية مقصورة على مدير النظام.»;
  en `Strings.en.xaml:1274` "This operation is restricted to system administrators.".
  Used only by `Services/AuthorizationGuard.cs:39` (fallback literal :40); asserted by 9 tests in
  `AuthorizationEnforcementTests.cs` (lines 142, 174, 200, 227, 252, 282, 308, 341, 367) via
  `Assert.Contains("مقصورة على مدير النظام", …)`.
- Extra finding (not in contract scope, left untouched): a third key `MsgErrAdminOnlyAction`
  (ar :232 identical Arabic text to `MsgErrAdminOnly`; en :235 slightly different wording) used by
  `SettingsViewModel.cs:501` (factory reset path). Added to the post-release backlog.
- Decision: `AuthorizationGuard.RequireAdmin` switches to `MsgErrAdminOnly` (fallback literal = its
  Arabic text); `MsgErrOperationAdminOnly` deleted from both dictionaries (zero usages remain);
  the 9 assertions change to `Assert.Contains("مخصصة لمدير النظام فقط", …)` — same strength.

### D3 — catches that neither log nor rethrow (in the listed files)
Changed (logging added, behavior unchanged):
- `App.xaml.cs:248, 269, 282` (ApplyTheme) and `:318` (ApplyAccentColor, theme accent)
- `LeakTestsViewModel.cs:318, 349, 378` (SourcesUpdatedMessage broadcast)
- `DashboardViewModel.cs:1598` (decay-curve end-date overflow fallback → LogWarning),
  `:1656, :1698` (only `Console.Error`, which a WinExe discards → LoggerService added, Console line kept),
  `:1778` (borrow summary card)
- `SettingsViewModel.cs:233` (last-backup info), `SourceDetailsViewModel.cs:122` (image path),
  `SourcesViewModel.cs:1738` (total activity), `AlertsViewModel.cs:158` (locations),
  `ActivityCalculatorViewModel.cs:153` (isotopes), `:574` (chart)
Left as is:
- `App.xaml.cs:23` — intentionally silent, documented by its comment (global crash-file handler).
- `App.xaml.cs:369` — `HandleGlobalException`: not a theme catch (outside D3's "App.xaml.cs (theme)"
  scope) and the original exception is already logged at :360.
- Catches that already show a user-visible error dialog but do not log (e.g. `LeakTestsViewModel`
  :427/:450, `SettingsViewModel` :550/:620, `SourcesViewModel` :1461/:1498/:1513/:1623,
  `AlertsViewModel` :201) are not silent swallows; left unchanged to keep the round minimal.

### D4 — H*(10) Arabic wording
- «الجرعة المحيطية»: `Strings.ar.xaml:1599` (`MsgErrInvalidAmbientDoseConversionFinite`) and the two
  identical fallback literals in `NeutronSourceTypeService.cs:76, :131` — one message, three copies.
- «المكافئ المحيطي»: `Models/AllModels.cs:968` (XML doc comment).
- No dominant term (a single message duplicated as fallback). Decision: contract default
  «المكافئ المحيطي للجرعة» (the correct rendering of *ambient dose equivalent*). No test asserts
  the old text. `docs/schema-drift-report.md` (historical doc) is not changed.

### D5 — Round 149 / PR #44
MERGED 2026-09-10T06:23:29Z, merge commit `d69703f`, ancestor of main → «مدموج — بانتظار تحقق بصري نهائي من إدريس».

### D6 — `Build and Test #82`
Run 33579070894, push on main at `1cf6617` (2026-09-02, "fix(tests): unregister view models from the
messenger…"); failed in "Run Unit Tests" with 3 failures, all in `SourcesViewModelTests`
(`SaveAsync_WithFutureCalibrationDate_FailsAndShowsErrorMessage`,
`SaveAsync_WhenDisablingMultiIsotope_ForSourceWithMultipleSavedIsotopes_FailsAndShowsErrorMessage`,
`SaveAsync_MultiIsotope_WithFutureCalibrationDate_Fails`). Superseded: every later main run checked
is green, latest `Build and Test #323` on `354128b` = success → closed as obsolete.

### Branch
Session worktree branch `claude/round-194-final-consolidation-073ef3` renamed locally to the contract
name `chore/round-194-final-consolidation` — no deviation.
