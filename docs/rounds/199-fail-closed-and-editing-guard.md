ROUND 199 — Security & stability: fail-closed admin prompt, stuck-editing hardening + self-healing guard,
test168 origin (read-only)

Save THIS TEXT VERBATIM as docs/rounds/199-fail-closed-and-editing-guard.md (you may append a separate
"Discovery findings" section below it). Commit it in the round's PR.
BASE: main at f2ba9aa (round 198, PR #94 merged). Branch: fix/round-199-fail-closed-and-editing-guard
(if a name collision forces a different name, report it as a deviation).
Risk: MEDIUM-HIGH (authorization prompt + navigation guard). Full pipeline: implementer -> your own diff review ->
change-verifier -> ci-monitor. ONE COMMIT PER GROUP (A, B, D), prefix "R199-<letter>:". Group C is read-only.
Fixups as separate "R199-<letter>-fix" commits (never amend), reported as deviations. Groups are independent: if a
STOP rule fires in one group, skip it, record why, continue the others.

STEP 0 — Worktree housekeeping: same rules as round 198 STEP 0. Additionally, `git worktree list` shows
agent-ab88c6d0af8053afd as locked although its folder no longer exists: run `git worktree unlock <path>` then
`git worktree prune`, and confirm it is gone. Report the table.

STEP 1 — Discovery (read-only)
D-A. PasswordPromptDialog.RequestAdminAccess (after round 198 it goes through DialogHelper.ShowWindowDialog):
     quote every path that returns "access granted" WITHOUT a verified admin password (dialog cannot be shown,
     exception, no owner window, test mode, anything else). For each: can it be reached in PRODUCTION (non-test)?
     List every caller of RequestAdminAccess and what it protects.
D-B1. The «نافذة مفتوحة» guard: every ViewModel whose IsEditing (or equivalent) is checked by
      MainViewModel.NavigateTo, Logout, and MainWindow closing. For each screen: is editing done in a MODAL window
      (ShowDialog) or INLINE in the page?
D-B2. For every Edit/AddNew/open-form path in those ViewModels: quote the order of "IsEditing = true" vs opening
      the window, and list every path where IsEditing can become true but the form window is never shown or its
      result is never processed (early return, null SelectedItem, exception, re-entrancy/double-click while a form
      is already open, Dispatcher re-entry). This is the prime suspect for the Users-screen freeze Edrees hit
      (blank Users tab; «تنبيه: نافذة مفتوحة» blocked navigation, Logout and closing the main window; app had to
      be killed). State whether any such path exists in UsersViewModel.
D-C. test168 origin, READ-ONLY:
     - Search the whole repo (production, tests, scripts, docs, history via `git log -S "test168"`) for "test168".
     - Copy the real SQLite DB (path from DatabasePaths) to a temp folder, query ONLY the copy: the user row for
       username 'test168' (created date, created-by if stored, IsActive, IsDeleted, role) and every audit-log row
       mentioning test168 (date, action, performed-by). Usernames/dates/actions only. Delete the temp copy and
       confirm the real file's hash is unchanged.
     STOP RULE for C: if evidence shows any test, tool or agent wrote to the REAL database, stop the whole round
     after STEP 1 and report — this is a data-isolation breach the architect must handle first.

STEP 2 — Changes
A. Fail-closed admin prompt: every PRODUCTION path in RequestAdminAccess that cannot verify the admin password must
   DENY access (return false), log a warning via LoggerService, and show the existing generic error/refusal message
   if a message can be shown (no new text unless unavoidable; if new, add ar + en keys with exact Arabic fallback).
   Test-mode behaviour may remain ONLY inside the DialogHelper seam (the single place allowed to know about test
   mode). No change to the password check itself or to who counts as admin.
   Tests: dialog-cannot-be-shown -> denied; exception -> denied; correct admin password -> granted; wrong
   password -> denied; cancel -> denied. Existing tests that relied on an automatic grant must be updated to
   supply an explicit result through the seam, without weakening what they verify.
B. Editing-state hardening, two layers:
   B1 Root-cause hardening: for every path found in D-B2, make it impossible for IsEditing to stay true without an
      open form: set IsEditing only immediately around showing the window, reset it in a finally block, and ignore
      a second Edit/AddNew while a form is already open (re-entrancy guard). No change to what is saved.
   B2 Self-healing guard: in MainViewModel.NavigateTo, Logout and the MainWindow closing handler, if IsEditing is
      true but NO editing form window is actually open (for modal screens, per D-B1), log a warning, reset the
      editing state WITHOUT saving, and allow the action. Screens that edit INLINE (if any, per D-B1) keep the
      current blocking behaviour unchanged. Closing the main window must never be blocked indefinitely.
   Tests: each B1 path (null selection, exception while opening, double invoke) leaves IsEditing false; B2: stuck
   IsEditing with no form open -> navigation, logout and close are allowed and IsEditing is reset; a real open
   form still blocks as before (modal case); inline screens still block (if any).
D. Documentation: session-summary.md round 199 entry (CI Release and local Debug counts separately), including the
   freeze incident (symptoms, not reproducible on main 36921cf or PR build ab43268, defensive fix) and the D-C
   finding. release-readiness.md: header (main at round 198 / f2ba9aa, round 199 under review); close the fail-open
   item; record B; record the test168 finding and, if it is harmless, add "deactivate test168 (Edrees, via the
   Users screen)" to "قائمة ما بعد الإصدار".

FORBIDDEN
- LoginWindow, LoginView, SplashWindow (including the username dropdown — a separate future decision).
- The admin password verification logic, AuthorizationGuard, role definitions, UserService.
- Any change to what is saved or to validation. Any real-database write (D-C is strictly read-only on a copy).
- PhraseFactoryResetConfirmation / RequiredResetPhrase, SystemResetService. Status strings (rounds 200–201).
  Date/time handling (round 202).
- No EF migration. No `git add .` / `-A`; explicit pathspecs; `git show --stat` per commit with a one-line
  justification per file.

GIT
- Commits in order, push, open a DRAFT PR to main. Do not merge.

REPORT
- STEP 0 table.
- D-A, D-B1, D-B2, D-C findings with quotes/evidence (D-C: confirm temp copy deleted and real file hash unchanged).
- Per commit: hash, files, justification. Groups skipped, with reason.
- Test counts: local Debug and CI "Build and Test" (CI baseline 1341, Debug baseline 1343; state new tests).
- CI warnings vs the 3 known CS8604.
- Worktree path holding the final commit + exact PowerShell commands in lowercase for Edrees's visual check.
- Visual checklist, SAFE actions only. For EVERY save it must include "navigate to another section, then come back,
  then navigate again". Include: Deletions screen admin prompt -> Cancel (must NOT open); -> correct admin password
  (opens). Users/Locations/Sources forms: open, then Save / Cancel / X, each followed by the navigate-back-navigate
  loop. No deletions, no new users, no password changes on real accounts.

---

## Discovery findings (STEP 1, lead, 2026-09-24) — ROUND STOPPED by STOP rule C

- D-A: `RequestAdminAccess` (PasswordPromptDialog.xaml.cs:107-151) grants without a password on:
  (1) `CustomPromptResult.HasValue` — static test seam living in production code; (2) `Application.Current?.Dispatcher == null`
  -> `return true` (fail-open, production-reachable only at startup/shutdown); (3) `ShowWindowDialog` returning false
  -> `return true` (only when `DialogHelper.IsTestMode`). Exceptions from the dialog propagate (not a grant).
  Sole caller: MainViewModel.NavigateTo -> Deletions screen.
- D-B1: guard = `CurrentView is IEditableViewModel { IsEditing: true }` in MainViewModel.NavigateTo (:319), Logout (:388),
  MainWindow.OnClosing (:132). Implementers Users, Locations, Sources, Radioisotopes, Borrow — all MODAL; no inline screen.
- D-B2: the VM sets `IsEditing = true`; the *View code-behind* opens the form from a PropertyChanged handler that is
  subscribed only in `Loaded`. Stuck paths (IsEditing true, no window): (a) exception in form ctor / Owner / ShowDialog
  inside the handler -> propagates out of the IsEditing setter, IsEditing stays true, `_formWindow` may stay non-null
  so later attempts are skipped; (b) IsEditing set while the view is not loaded/subscribed. Both apply to UsersViewModel
  (Edit :620-636, AddNew :611-617). Null Selected returns before IsEditing is set (safe).
- D-C: test168 = admin account (FullName احمد الجيلانى), created 2026-09-16 23:08, self password change 23:13, last login
  23:15, re-activated by admin 2026-09-24 04:11; no repo/history reference. Harmless by itself.
  BREACH: BackupServiceTests.cs:32 builds BackupService without a certificates folder -> defaults to the real
  %LOCALAPPDATA%\Sources\Certificates; its 8 RestoreBackup tests copy/clear/extract there (BackupService.cs:176-234).
  Evidence: 103 empty `Certificates_pre_restore_*` folders (2026-09-16..24) in the real app-data dir, real Backups empty.
  Also DatabaseWalTests.cs:73 opens the real Sources.db read-write; BackupServiceTests.cs:241 reads the real DB;
  PasswordHelperTests writes the real log. Temp DB copy deleted; real DB SHA-256 unchanged (4D2F112D...0B72).

---

## Architect decisions

1. Round 199 continues on the same branch, re-scoped. NEW group T (test data isolation) is done FIRST.
   HARD RULE: do not run the full test suite, or any test class that touches DatabasePaths / LoggerService /
   BackupService / SettingsHelper / certificates / logs, until commit R199-T exists and its sentinel test passes.
   Do NOT touch anything in the real %LOCALAPPDATA%\Sources folder (including the 103 empty
   Certificates_pre_restore_* folders — Edrees handles them).

2. Group T — test data isolation (commit "R199-T:"):
   T0 Discovery: list EVERY test that can resolve a real path: DatabasePaths (DB, certificates, backups, logs,
      settings), LoggerService, SettingsHelper, BackupService default folders, Environment.SpecialFolder.LocalApplicationData,
      and any hardcoded %LOCALAPPDATA% path. file:line each.
   T1 Assembly-wide redirection: give DatabasePaths a test-only override of its root data directory (internal,
      reachable from Sources.Tests via InternalsVisibleTo, or an equivalent seam with the SAME effect; production
      default path unchanged). Set it once for the whole test assembly (module initializer or assembly fixture)
      to a unique temp folder per test run, deleted at the end. Every derived path (DB, Certificates, Backups,
      Logs, settings) must follow the override.
   T2 Fix the known offenders explicitly as well: BackupServiceTests (pass an explicit temp certificates folder),
      DatabaseWalTests (use a temp DB, never the real Sources.db), BackupServiceTests:241 (no real DB read),
      PasswordHelperTests (logs go to the redirected folder).
   T3 Sentinel test: fails if, during the test run, DatabasePaths or LoggerService resolves to any path under the
      real %LOCALAPPDATA%\Sources.
   STOP RULE for T: if the redirection needs production changes beyond DatabasePaths (and the minimal
   InternalsVisibleTo), stop and report before coding.
   Verification: after T, run the full suite once and prove the real folder was not touched — record the real
   Sources.db SHA-256 and a listing (names + timestamps) of the real %LOCALAPPDATA%\Sources tree BEFORE and AFTER
   the run; they must be identical.

3. Group A (after T): fix D-A paths 1 and 2. Path 2 (Dispatcher == null -> return true) must DENY. Move the
   static CustomPromptResult test hook out of PasswordPromptDialog into the DialogHelper seam (the single place
   allowed to know about test mode). Update the 4 tests that use the hook to go through the seam, same strength.

4. Group B (after T): fix stuck paths (a) exception while building/showing the form in the view's IsEditing
   handler — catch, log, reset IsEditing and _formWindow, show the existing generic error; and (b) IsEditing set
   while the view is not loaded — do not leave it true (reset or defer until Loaded; choose the simpler safe
   option and justify). Apply to all five screens (Users, Locations, Sources, Radioisotopes, Borrow). Keep B2
   (self-healing guard in NavigateTo, Logout, MainWindow closing) exactly as the contract says. The Save-exception
   path is NOT in scope.

5. Group D docs as in the contract, plus: record the test-isolation breach (evidence, no data lost on this machine,
   what would have been lost with real certificates), the fix, and the before/after proof. test168 finding:
   legitimate UI-created admin account "احمد الجيلانى" (2026-09-16 23:08), re-activated 2026-09-24 04:11 — Edrees
   decides whether to deactivate it (no code change).

Commit order: T, A, B, D. Then push, Draft PR, change-verifier, ci-monitor, report as the contract requires.
