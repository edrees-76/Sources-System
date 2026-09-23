ROUND 196 — Enter-key stale-save fix (LocationFormWindow, UserFormWindow) + CodeRabbit test-hardening rows

Save THIS TEXT VERBATIM as docs/rounds/196-enter-save-and-test-hardening.md (you may append a separate
"Discovery findings" section below it). Commit it in the round's PR.
BASE: main at fd8ee36 (round 195, PR #91 merged). Branch: fix/round-196-enter-save-test-hardening
(if a name collision forces a different name, report it as a deviation).
Risk: MEDIUM. Full pipeline: implementer -> your own diff review -> change-verifier -> ci-monitor.
ONE COMMIT PER GROUP (A, B, C below), in order, message prefix "R196-<letter>:". Fixups, if any, as separate
"R196-<letter>-fix" commits (never amend), reported as deviations.

STEP 0 — Worktree cleanup (report the table before removing anything)
For EVERY entry in `git worktree list` except D:\Sources-System and the worktree this session runs in:
  path | branch | HEAD | status clean? | unpushed commits | PR state (`gh pr list --state all --head <branch>`)
Merges are SQUASH merges: use PR state, not ancestry, as the merge signal.
Remove (`git worktree remove <path>`, never --force) only if: clean, nothing unpushed, PR MERGED.
Then `git branch -D <branch>` only if PR MERGED and local tip == remote tip (or the remote branch was deleted
after merge and the local tip equals the PR's head commit).
Worktrees with no PR (e.g. session worktrees like coderabbit-review-triage-*, round-194-final-consolidation-*):
remove only if clean, nothing unpushed, and HEAD is an ancestor of main; otherwise keep and report.
Folders under .claude\worktrees NOT registered in `git worktree list`: list contents recursively; delete with
`Remove-Item -Recurse` only if they contain nothing or only bin/, obj/, .vs/; otherwise keep and report.
Finish with `git worktree prune`. Continue to STEP 1 even if some items are kept.

STEP 1 — Discovery (read-only). STOP and report before coding if any assumption is false.
D1. The round-149 fix pattern: quote how SourceFormWindow.xaml (~:607/:690) and RadioisotopeFormWindow.xaml
    make Enter save the CURRENT text (UpdateSourceTrigger=PropertyChanged, or other). Quote the round-149
    regression tests and their technique for simulating "Enter while focus is still inside a TextBox".
D2. LocationFormWindow.xaml: the Enter/Return -> Save binding (~:21) and every bound TextBox (~:51/:65/:71/:75)
    with its current UpdateSourceTrigger. Note any field with validation, numeric parsing or a converter.
D3. UserFormWindow.xaml: same inventory (~:51/:57/:68). How the PasswordBox value is read on save
    (code-behind vs binding) and whether pressing Enter inside the PasswordBox saves the current password.
D4. Sweep ALL other *Window.xaml / dialogs where Enter triggers save (IsDefault="True" button or a Return
    KeyBinding/InputBinding) AND a TextBox binding uses the default (LostFocus) trigger. List file:line.
    Do NOT fix these in this round unless they are exactly the same pattern AND no more than 3 extra files;
    otherwise list them for the backlog.
D5. CodeRabbit test rows — current line numbers on fd8ee36 (they shifted in round 195):
    R10 LocationsFormWindowTests.cs: assertions executed inside the dialog callback before the dialog closes.
    R11 NeutronSourcesUITests.cs: GetAllSources / GetDeletedSources not stubbed (mock returns null; the
        constructor's load throws unobserved).
    R12 NeutronSourcesUITests.cs: Task.Delay(100) used as a wait.
    R13 SourceFormWindowTests.cs: no check that the old inline form container is gone from SourcesView.
    R14 SourceFormWindowTests.cs: "at least one disabled combo box" instead of checking Status and Location.
    Also locate the deterministic-wait helper added in round 195 (R195-I-fix, SourcesViewModelTests.cs).

STEP 2 — Changes
A. Enter-key fix (commit R196-A):
   - LocationFormWindow and UserFormWindow: apply EXACTLY the round-149 pattern from D1 to every bound TextBox
     found in D2/D3, so pressing Enter while focus is inside a field saves the value just typed.
   - If D3 shows Enter inside the PasswordBox would save a stale/empty password, fix it with the smallest
     change consistent with the existing code-behind; if the fix is not small, STOP and report.
   - Extra files from D4: only if the conditions in D4 hold; otherwise backlog.
   - Tests (same commit): per window, a regression test mirroring the round-149 technique: type a new value,
     press Enter with focus still in the field, assert the saved entity has the NEW value. Include the
     PasswordBox case if applicable.
   - No change to validation rules, save logic, services, or messages.
B. Test hardening (commit R196-B), test files only:
   R10 capture values inside the callback, close the dialog, then assert outside it.
   R11 stub GetAllSources and GetDeletedSources with empty lists (or the data each test needs).
   R12 replace Task.Delay(100) with the deterministic helper from round 195 (or an equivalent explicit await).
   R13 assert the old inline form container no longer exists in SourcesView.
   R14 assert Status and Location combo boxes are each disabled, separately.
   Run each changed test class 10 times; report any flakiness.
C. Documentation (commit R196-C):
   - session-summary.md: round 196 entry (CI Release and local Debug counts stated separately).
   - release-readiness.md: header (main at round 195 / fd8ee36, round 196 under review); close CodeRabbit
     rows 2, 10, 11, 12, 13, 14 in the CodeRabbit follow-up section; record the D4 sweep result; add any
     D4 windows not fixed to "قائمة ما بعد الإصدار".

FORBIDDEN
- LoginWindow, LoginView, SplashWindow. PhraseFactoryResetConfirmation / RequiredResetPhrase.
- UserService (including UnlockAccount), LocationService, NeutronSourceService.Restore, AppDbContext.
- Language-switch / culture / FlowDirection code (reserved for round 197).
- Any production file other than the XAML (and, only if D3 requires, the code-behind) of the windows in A.
- No EF migration. No `git add .` / `-A`; explicit pathspecs; `git show --stat` per commit with a one-line
  justification per file.

GIT
- Commits A, B, C in order, push, open a DRAFT PR to main. Do not merge.

REPORT
- STEP 0 table (removed / kept + reason).
- D1–D5 findings with quotes/evidence.
- Per commit: hash, files, justification.
- Test counts: local Debug and CI "Build and Test" (CI baseline 1309, Debug baseline 1311; state new tests).
- CI warnings vs the 3 known CS8604.
- Worktree path holding the final commit + exact PowerShell commands written in lowercase
  (cd, git log -1 --oneline, dotnet build sources.sln -c debug, launch exe) for Edrees's visual check.
- Visual checklist limited to SAFE actions on real data: edit a location and a non-admin test user, change
  one text field, press Enter with the cursor still in the field, reopen, confirm, then restore the original
  value. No deletions, no new users, no password changes on real accounts.

---

## Discovery findings (lead, on fd8ee36)

### STEP 0 — worktree cleanup

| path | branch | HEAD | clean | unpushed | PR | action |
|---|---|---|---|---|---|---|
| agent-ab88c6d0af8053afd | chore/round-195-small-items-batch | 17f1cdb | yes | none (remote branch deleted; tip == PR #91 head) | #91 MERGED | **kept**: worktree is `locked` by claude agent pid 16192, which is still running; branch kept with it |
| coderabbit-review-triage-f578d9 | claude/coderabbit-review-triage-f578d9 | 9769602 | yes | no upstream | none | removed from git (ancestor of main); empty folder left behind because another process holds it open; branch kept (no PR, so deletion not authorized) |
| round-194-final-consolidation-073ef3 | claude/round-194-final-consolidation-073ef3 | 354128b | yes | no upstream | none | removed from git (ancestor of main); empty folder held open, same as above; branch kept |
| round-195-small-items-52ea68 | claude/round-195-small-items-52ea68 | 9769602 | **no** (untracked `docs/rounds/195-small-items-batch.md`, identical to main's copy) | no upstream | none | **kept** (not clean) |
| neutron-sources-current-emission-rate-642203 (unregistered) | — | — | 0 files | — | — | deletion failed: folder held open by another process; kept |
| round-193-neutron-current-activity-928b45 (unregistered) | — | — | 0 files | — | — | deletion failed: folder held open by another process; kept |

`git worktree prune` was run.

### D1 — the round-149 pattern (assumption FALSE)
The decisive round-149 fix is in **code-behind**, not XAML. `SourceFormWindow.xaml.cs:23,38-45` and
`RadioisotopeFormWindow.xaml.cs:23,39-46` both register a window-level `PreviewKeyDown` (tunnel) handler:
`if (e.Key != Key.Enter) return; if (Keyboard.FocusedElement is not TextBox focusedTextBox) return;
focusedTextBox.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();`
The XAML `UpdateSourceTrigger=PropertyChanged` (SourceFormWindow.xaml:607,690,…; RadioisotopeFormWindow.xaml:360,387,396,428)
is a secondary layer only; Radioisotope TextBoxes EditSymbol/EditName/EditArabicName/EditNotes/EditEnglishNotes keep
the default LostFocus trigger and rely on the handler alone.
Round-149 tests: `RadioisotopeFormWindowTests.cs:296-389` and `SourceFormWindowTests.cs:~397`: edit an existing
entity, `textBox.Focus(); textBox.Text = newValue; SimulateEnterKeyPress(textBox)` (raises `Keyboard.KeyDownEvent`
on the TextBox), then assert via a Moq `Callback` on the service's Update that the persisted value is the new one,
that Update ran exactly once, and that `IsEditing` became false.
Limitation: the simulation raises only the bubbling `KeyDownEvent`, so it never reaches the window's
`PreviewKeyDown` flush. On PropertyChanged fields the source is already updated by the `Text` assignment.

### D2 — LocationFormWindow.xaml
`:21` `<KeyBinding Key="Return" Command="{Binding SaveCommand}"/>`; `:80` Save button `IsDefault="True"`.
TextBoxes (all default LostFocus, plain strings, no converter or validation): `:51` EditName, `:65` EditBuilding,
`:71` EditRoom, `:75` EditPerson. The only validation is `LocationsViewModel.Save`, which rejects an empty name.

### D3 — UserFormWindow.xaml
`:21` Return KeyBinding; `:105` Save `IsDefault="True"`. TextBoxes (default LostFocus, plain strings):
`:51` EditFullName, `:57` EditUsername (`IsEnabled="{Binding IsNew}"`), `:68` EditEmail.
PasswordBox `:61`: `materialDesign:PasswordBoxAssist.Password="{Binding EditPassword, Mode=TwoWay}"` with
`FormPasswordBox` based on `MaterialDesignOutlinedPasswordBox` (MDIX 5.3.0). The value flows on every PasswordChanged,
and `UsersViewModel.Save` reads `EditPassword` from the VM, so no code-behind read exists. Expectation: Enter inside
the PasswordBox already saves the current password; to be confirmed by a regression test (new-user path).

### D4 — sweep
Enter→save windows: ActivationDialog (reads `TxtSerial.Text` directly in code-behind; no binding), AlertDialog
(no TextBox), ForceChangePasswordDialog / PasswordPromptDialog (PasswordBoxes read in code-behind),
FirstRunWizardWindow (only TextBox `:56` BackupPath is `IsReadOnly`), RadioisotopeFormWindow / SourceFormWindow
(already covered by the round-149 flush). **No other window matches the pattern; nothing goes to the backlog.**

### D5 — test rows on fd8ee36
- R10 `LocationsFormWindowTests.cs`: asserts inside `BeginInvoke` callbacks at `:125-136`, `:189-200`, `:214-223`.
- R11 `NeutronSourcesUITests.cs`: `ISourceService` mocks without GetAllSources/GetDeletedSources stubs at
  `:71`, `:120`, `:168`, `:275`, `:328`, `:791` (`:749-751` is already stubbed).
- R12 `NeutronSourcesUITests.cs:358` `await Task.Delay(100);` after `vm.SelectedReport = "NeutronInventoryReport"`.
  `ReportsViewModel.OnSelectedReportChanged → LoadReport()` is **synchronous** (ReportsViewModel.cs:160-201), so the
  deterministic fix is to remove the delay. The round-195 helper `WaitUntilConstructorLoadSettledAsync`
  (SourcesViewModelTests.cs:1187) is specific to SourcesViewModel and not applicable here.
- R13 `SourceFormWindowTests.cs:95-123` checks only `IsEditing` and that no SourceFormWindow is open.
- R14 `SourceFormWindowTests.cs:324-333` "at least one disabled".

### Lead decision
The round-149 pattern is applied in full to LocationFormWindow and UserFormWindow: `UpdateSourceTrigger=PropertyChanged`
on every bound TextBox, plus the identical `PreviewKeyDown` Enter flush handler in each code-behind.
**Authorized deviation (architect, 2026-09-23):** the code-behind restriction was written on a wrong assumption about
D1. Code-behind changes are limited to that PreviewKeyDown flush handler (its registration and method), with no other
code-behind change. No PasswordBox change unless the regression test proves the password is stale.

### Lead decision 2 — PasswordBox (D3 corrected)
The regression test written for commit A (`UserFormWindow_EnterAfterTyping_PersistsCurrentPassword_OnNewUserPath`)
proved D3's expectation wrong: setting `passwordBox.Password` did not flow into `UsersViewModel.EditPassword` at
all before `SaveCommand` ran, with or without Enter. Diagnosis: MaterialDesignThemes 5.3's
`PasswordBoxAssist.PasswordProperty` metadata sets `DefaultUpdateSourceTrigger = LostFocus` (unlike a plain
`TextBox.TextProperty`, whose default is `PropertyChanged`). A lead probe confirmed the attached property itself
receives `"NewPass#196"` and the underlying `BindingExpression` is `Active` with `IsDirty=true` — the value is not
lost, it is simply not pushed to the source until `LostFocus` (or an explicit `UpdateSource()`) fires. Since the
window's `PreviewKeyDown` flush handler only targets `TextBox` (`Keyboard.FocusedElement is not TextBox
focusedTextBox`), it never reaches the `PasswordBox`, so Enter always saw a stale (empty, on the new-user path)
`EditPassword`. Effect on users: new-user creation was refused with "password required" even after typing one;
editing an existing user showed a false "success" message while `UserService.ResetPassword` was silently skipped
(the typed password was never sent).

**Fix (authorized, smallest possible, XAML-only):** `UserFormWindow.xaml:61` —
`materialDesign:PasswordBoxAssist.Password="{Binding EditPassword, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}"`.
No other XAML change, no code-behind change (the existing `PreviewKeyDown` flush handler is irrelevant to this fix
and was left untouched).

**Proof:** both regression tests
(`UserFormWindow_EnterAfterTyping_PersistsCurrentPassword_OnNewUserPath`,
`UserFormWindow_EnterAfterTyping_PersistsCurrentPassword_OnEditPath_CallsResetPassword`) were run against the
pre-fix XAML first and both failed (`Assert.Equal() Failure: Expected: 1, Actual: 0` on the mocked
`CreateUser`/`ResetPassword` call count), then passed after the one-line XAML change (9/9 `UserFormWindowTests`
passing, confirmed over 10 consecutive `dotnet test` runs with zero flakiness).

**New post-release backlog item (not fixed in this round):** `UsersViewModel.Save` (edit path) calls
`_userService.ResetPassword(_editingId!.Value, EditPassword)` unconditionally whenever `EditPassword` is
non-empty, even when the preceding `_userService.UpdateUser(user)` call failed (`r.Success == false`). This means
a failed profile update can still silently change the user's password. Out of scope for round 196 (behavioral
change to `UsersViewModel.Save`, not a WPF binding/Enter-key fix); recorded in
`docs/release-readiness.md` → "قائمة ما بعد الإصدار".
