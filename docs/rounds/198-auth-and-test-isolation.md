ROUND 198 — Accounts & authorization fixes + test-isolation cleanup (IsTestMode, IMessenger)

Save THIS TEXT VERBATIM as docs/rounds/198-auth-and-test-isolation.md (you may append a separate
"Discovery findings" section below it). Commit it in the round's PR.
BASE: main at 36921cf (round 197, PR #93 merged). Branch: fix/round-198-auth-and-test-isolation
(if a name collision forces a different name, report it as a deviation).
Risk: MEDIUM (auth-adjacent changes + mechanical refactor across ViewModels). Full pipeline: implementer ->
your own diff review -> change-verifier -> ci-monitor. ONE COMMIT PER GROUP (A..F), prefix "R198-<letter>:".
Fixups as separate "R198-<letter>-fix" commits (never amend), reported as deviations.
Groups are independent: if a STOP rule fires in one group, skip that group, record why, and continue the others.

STEP 0 — Worktree cleanup: same rules as round 197 STEP 0 (PR state as merge signal; never --force; unregistered
folders deleted only if empty or only bin/obj/.vs). Report the table. Continue even if some items are kept.

STEP 1 — Discovery (read-only)
D1. UsersViewModel.Save: quote the edit path. Confirm ResetPassword is called even when UpdateUser fails.
    Also: if UpdateUser succeeds but ResetPassword fails, what does the user see? Quote.
D2. NeutronSourceTypeService Create/Update/Delete/Restore: which AuthorizationGuard checks each method has.
    Quote the equivalent pattern in a sibling service (e.g. RadioisotopeService Create/Update).
    List every test that constructs NeutronSourceTypeService without an editor/admin CurrentUser.
D3. Keys MsgErrCannotDeleteRadioisotopeHasSources and MsgErrCannotDeleteNeutronSourceTypeHasSources:
    confirm zero usages in .cs, .xaml and tests.
D4. DialogHelper.IsTestMode: list EVERY reference in production code (not tests) with file:line and what the
    branch does. The backlog says 4 places — state the real number.
D5. IMessenger: quote the release-readiness §3 backlog entry for the IMessenger sweep and the round-115
    BorrowViewModel pattern it refers to. List the seven ViewModels, and for each: how it gets its messenger
    (static WeakReferenceMessenger.Default vs injected IMessenger), what it registers/sends, and whether it
    unregisters. List every message type and every sender -> receiver pair affected.

STEP 2 — Changes
A. Password on failed update (UsersViewModel): call ResetPassword ONLY after UpdateUser succeeds. If UpdateUser
   fails, show its failure and do not touch the password. If UpdateUser succeeds and ResetPassword fails, show a
   clear message that the user data was saved but the password was NOT changed (new keys ar + en, exact Arabic
   fallback). Tests: failed update -> ResetPassword never called; success + reset failure -> the partial-save
   message; success + success -> unchanged behaviour.
B. NeutronSourceTypeService: add the same editor authorization the sibling services use to Create and Update
   (and to Delete/Restore only if D2 shows they lack it). Same message keys the sibling services use — no new text.
   Update existing tests to supply an authorized CurrentUser; add tests: non-editor Create refused, non-editor
   Update refused, editor still succeeds.
C. Remove the two unused keys from BOTH dictionaries (only if D3 confirms zero usages).
D. IsTestMode: remove production-code branching on DialogHelper.IsTestMode by moving each decision behind the
   single existing seam (DialogHelper itself, or an injectable dialog abstraction if one already exists). No
   user-visible behaviour change in production. STOP RULE for this group: if D4 shows more than 6 production
   references, or the change needs a new service registered in DI, skip D and record it.
E. IMessenger sweep: apply EXACTLY the round-115 BorrowViewModel pattern (per D5) to the seven ViewModels.
   Mechanical change only: no new messages, no changed message semantics, no change to who sends/receives what.
   Every registration keeps an equivalent unregistration if one existed. STOP RULE for this group: if any
   ViewModel needs more than a mechanical change, apply E to the mechanical ones only and list the rest; if the
   total production diff for E exceeds ~300 lines, stop after the first three ViewModels and list the rest.
   Tests: existing messenger-related tests must pass unchanged; add one isolation test proving two ViewModels
   built with separate messengers do not receive each other's messages.
F. Documentation: session-summary.md round 198 entry (CI Release and local Debug counts stated separately).
   release-readiness.md: header (main at round 197 / 36921cf, round 198 under review); close completed items;
   move any skipped group (D or the unfinished part of E) to "قائمة ما بعد الإصدار" with its reason; record
   the D4 and D5 real counts.

FORBIDDEN
- LoginWindow, LoginView, SplashWindow. PhraseFactoryResetConfirmation / RequiredResetPhrase, SystemResetService.
- UserService logic (UnlockAccount, UpdateUser, ResetPassword internals) — A changes only the call order in the
  ViewModel. AuthorizationGuard itself. Any delete guard. Any restore logic.
- New message types, changed message payloads, or changed sender/receiver relationships.
- Status strings / SimpleArabicStatus (reserved for rounds 199–200). Any date/time handling (round 201).
- No EF migration. No `git add .` / `-A`; explicit pathspecs; `git show --stat` per commit with a one-line
  justification per file.

GIT
- Commits in order, push, open a DRAFT PR to main. Do not merge.

REPORT
- STEP 0 table.
- D1–D5 findings with quotes/evidence (real counts for D4 and D5).
- Per commit: hash, files, justification. Groups skipped or partially done, with reason.
- Test counts: local Debug and CI "Build and Test" (CI baseline 1335, Debug baseline 1337; state new tests).
- CI warnings vs the 3 known CS8604.
- Worktree path holding the final commit + exact PowerShell commands in lowercase for Edrees's visual check.
- Visual checklist limited to SAFE actions on real data. For E, list which cross-screen refreshes depend on the
  changed messages and give one harmless check for each (e.g. edit a location's notes, confirm another screen
  reflects it, then restore the original). No deletions, no new users, no password changes on real accounts.

---

## Discovery findings (lead, on 36921cf)

### STEP 0 — worktree cleanup

| path | branch | HEAD | clean | unpushed | PR | action |
|---|---|---|---|---|---|---|
| agent-aab44de6b9b1f2416 | fix/round-197-neutron-restore-integrity | b8b1058 | yes | none (remote branch deleted; local tip == PR #93 head b8b1058) | #93 MERGED | **removed** (`git worktree remove`), branch deleted (`git branch -D`) |
| agent-ab88c6d0af8053afd | chore/round-195-small-items-batch | 17f1cdb | yes | none (tip == PR #91 head) | #91 MERGED | **kept**: still `locked` by claude agent pid 16192, and that process (claude.exe) is still running |
| round-195-small-items-52ea68 | claude/round-195-small-items-52ea68 | 9769602 | **no** (untracked round-195 doc) | no upstream | none | **kept** (not clean) |
| round-196-enter-save-test-b16245 | claude/round-196-enter-save-test-b16245 | fd8ee36 | **no** (untracked round-196 doc) | no upstream | none | **kept** (not clean) |
| round-197-neutron-restore-49f897 | claude/round-197-neutron-restore-49f897 | 1779d78 | **no** (untracked round-197 doc) | no upstream | none | **kept** (not clean) |
| coderabbit-review-triage-f578d9, neutron-sources-current-emission-rate-642203, round-193-neutron-current-activity-928b45, round-194-final-consolidation-073ef3 (unregistered) | — | — | 0 files each | — | — | **kept**: all are empty, but the session's permission policy denied the deletion (`Remove-Item` and `rmdir`). Edrees may delete them manually. |

`git worktree prune` was run.

### D1 — UsersViewModel.Save, edit path (UsersViewModel.cs:689-701) — CONFIRMED
```
var r = _userService.UpdateUser(user);
if (!string.IsNullOrWhiteSpace(EditPassword))
    _userService.ResetPassword(_editingId!.Value, EditPassword);
ShowMsg(r.Message);
if (r.Success) { IsEditing = false; LoadData(); }
else { DialogHelper.ShowError(r.Message); }
```
- ResetPassword runs whenever EditPassword is non-empty, **regardless of `r.Success`**.
- ResetPassword's return value is discarded. If UpdateUser succeeds but ResetPassword fails, the user sees only
  `r.Message` (the update-success text) and the form closes. There is no hint that the password was not changed.
  ResetPassword (UserService.cs:209-237) fails for: not admin (`RequireAdmin`), user not found, or the base
  `admin` account being changed from another account. None of these can be fixed by retrying.

### D2 — NeutronSourceTypeService guards
| method | RequireActivated | RequireEditor |
|---|---|---|
| Create (:62) | yes (:64) | **no** |
| Update (:117) | yes (:119) | **no** |
| Delete (:201) | yes (:203) | yes, `"Sources"` (:206) |
| Restore (:255) | yes (:257) | yes, `"Sources"` (:260) |

Sibling pattern (RadioisotopeService.cs:47/104; NeutronSourceService.cs:114/207), placed straight after the activation check:
```
var guard = AuthorizationGuard.RequireEditor(_userService.CurrentUser, "Sources");
if (!guard.Allowed) return (false, guard.Message);
```
Messages come from AuthorizationGuard (MsgErrNotLoggedIn / MsgErrReadOnlyUser / MsgErrNoSectionPermission), so no new text is needed.
Tests that construct NeutronSourceTypeService (6):
- NeutronSourceTypeServiceTests.cs:42: editor with `Permissions="Sources"`. Authorized.
- AddedByUnificationTests.cs:70: `_testUser` is an admin ("مدير النظام"); the ghost user at :178 has `Permissions="All"` and `IsEditor` defaults to true. Authorized.
- NonFinitePersistenceGuardTests.cs:418/:448: mock user `IsEditor=true, Permissions="All"`. Authorized.
- AuthorizationEnforcementTests.cs:601: **null CurrentUser**. It only calls Delete/Restore and asserts refusal, so it is unaffected.
- LicenseGuardRejectionTests.cs:59: default FakeUserService user (IsEditor true, **Permissions null → no "Sources"**).
  Create is refused earlier by the activation guard, so the result is unchanged.
Expected: no existing test needs a different CurrentUser. The implementer must confirm this by running the tests.

### D3 — unused keys — CONFIRMED zero usages
`MsgErrCannotDeleteRadioisotopeHasSources` (Strings.ar/en.xaml:1610) and `MsgErrCannotDeleteNeutronSourceTypeHasSources`
(:1626) have no usage in .cs, .xaml or tests. The only matches are the longer successor keys `...IncludingDeleted` / `...Count`,
and no key is built by string concatenation (the only `"MsgErrCannotDelete..."` construction is a literal Location key).

### D4 — DialogHelper.IsTestMode in production — REAL COUNT: 5 external references (backlog said 4)
DialogHelper.cs declares the flag (:8) and reads it internally 5 times (:17, :34, :51, :69, :92). That is the seam itself.
External production references:
1. SourceNavigationHelper.cs:68: `if (DialogHelper.IsTestMode) return;` skips opening SourceDetailsWindow.
2. SourceNavigationHelper.cs:122: the same, for NeutronSourceDetailsWindow.
3. LocationsViewModel.cs:260: the same, for LocationDetailsWindow.
4. MainViewModel.cs:280: `OpenActivation` in test mode skips ActivationDialog and activates with the static
   `TestActivationSerialOverride` instead (the backlog missed this one). There is also a doc comment at :37.
5. PasswordPromptDialog.xaml.cs:115: `RequestAdminAccess` returns **true** (access granted) when
   `IsTestMode || Application.Current?.Dispatcher == null`.
No injectable dialog abstraction exists, so the seam is DialogHelper. 5 ≤ 6 and no DI registration is needed, so the STOP rule does not fire.

### D5 — IMessenger
Backlog entry (release-readiness.md §3, :759): «كنس الوسيط الكامل في بقية الشاشات (سبعة ViewModels + `IDisposable` في
الخمسة الناقصة + عزل وسائط الاختبارات القائمة) مؤجَّل موثَّق لما بعد النشر.» The same text is at :840-841.
Round-115 pattern (commit 04d3b21, BorrowViewModel.cs):
- field `private readonly IMessenger _messenger;`
- last constructor parameter `IMessenger? messenger = null`
- first constructor line `_messenger = messenger ?? WeakReferenceMessenger.Default;`
- every `WeakReferenceMessenger.Default.X` replaced with `_messenger.X`

**IDisposable was not added by round 115** (it already existed). IMessenger is not registered in DI (App.xaml.cs), so production always falls back to `.Default`.

| ViewModel | messenger today | registers | sends | unregisters |
|---|---|---|---|---|
| AlertsViewModel | static Default | SourcesUpdatedMessage (:129) | — | yes, Dispose `UnregisterAll` (:75) |
| LeakTestsViewModel | static Default | SourcesUpdatedMessage as IRecipient (:73, Receive :82) | SourcesUpdatedMessage (:316, :350, :382) | no |
| LocationsViewModel | static Default | NavigateToSearchResultMessage (:109) | — | no |
| MainViewModel (singleton) | static Default | SourcesUpdatedMessage (:51) | — | yes, Dispose `UnregisterAll` (:508) |
| RadioisotopesViewModel | static Default | NavigateToSearchResultMessage (:119) | — | no |
| SourcesViewModel | static Default | NavigateToSearchResultMessage (:388) | SourcesUpdatedMessage (:996, :1280, :1482, :1557) | no |
| UsersViewModel | static Default | NavigateToSearchResultMessage (:128) | — | no |

Real count: **7 ViewModels, 7 registrations, 7 sends, 2 unregistrations.** Two message types are affected.
- SourcesUpdatedMessage: senders SourcesViewModel, LeakTestsViewModel → receivers MainViewModel, AlertsViewModel,
  LeakTestsViewModel, BorrowViewModel (already injected).
- NavigateToSearchResultMessage: sender DashboardViewModel.cs:2070 (outside the sweep, stays on `.Default`) → receivers
  SourcesViewModel, LocationsViewModel, RadioisotopesViewModel, UsersViewModel.
- Unaffected: FocusDashboardSearchMessage (MainWindow.xaml.cs:55 → DashboardView.xaml.cs:20, view code-behind).
In production every one of these resolves to `.Default`, so all sender→receiver pairs stay the same.

## Lead decisions (on 36921cf)

- **A:** Edit path order: UpdateUser. If it fails → `ShowMsg(r.Message)` + `DialogHelper.ShowError(r.Message)` and return; the password is untouched.
  If it succeeds and EditPassword is non-empty → ResetPassword. If that fails → build the message from the new key
  `MsgWarnUserSavedPasswordNotChanged` (ar fallback exactly «تم حفظ بيانات المستخدم، لكن لم تُغيَّر كلمة المرور: {0}», en «User data was
  saved, but the password was NOT changed: {0}») formatted with the reset message. Then `ShowMsg(msg)` + `DialogHelper.ShowWarning(msg)`,
  then close the form and reload (the data really was saved, and no ResetPassword failure reason can be fixed by retrying).
  Success path unchanged: `ShowMsg(r.Message)`, close, reload. The new-user path is untouched.
- **B:** Add the sibling two-line `RequireEditor(_userService.CurrentUser, "Sources")` guard straight after the activation guard in Create and Update.
  Delete/Restore already have it, so they are unchanged.
- **C:** Remove both keys from Strings.ar.xaml and Strings.en.xaml.
- **D:** Add to DialogHelper one method following its existing test-override style:
  `public static bool ShowWindowDialog(Action showWindow)`. In test mode it returns false without invoking; otherwise it invokes and returns true.
  Each external site routes through it with identical production behaviour:
  sites 1-3 move their existing post-guard body into a private `...Core` method called as `DialogHelper.ShowWindowDialog(() => ...Core(...))`.
  In site 5 the `Dispatcher == null → true` production check stays; the test-mode short-circuit becomes
  `if (!DialogHelper.ShowWindowDialog(() => granted = ...)) return true;`.
  Site 4: `if (!DialogHelper.ShowWindowDialog(ShowActivationDialogCore)) { existing TestActivationSerialOverride block }`.
  Acceptance: `grep IsTestMode Sources-System-Project` matches DialogHelper.cs only (update the MainViewModel :37 comment).
  Recorded for the backlog, not fixed: `TestActivationSerialOverride` still lives in MainViewModel, and RequestAdminAccess still
  auto-grants when the dialog is suppressed (existing behaviour, unchanged).
- **E:** Apply the round-115 pattern as-is to the seven. **No IDisposable is added to the five ViewModels that lack it.**
  They are transients resolved from the root ServiceProvider (MainViewModel.cs:336-350), and MainViewModel never disposes
  the previous CurrentView. The root container would keep every IDisposable instance, and with it that instance's live messenger
  registration (stale screens still reacting to NavigateToSearchResultMessage/SourcesUpdatedMessage). That is a behavioural and memory change,
  not a mechanical one. It moves to «قائمة ما بعد الإصدار» with this reason.

## Implementation notes (agent, on branch fix/round-198-auth-and-test-isolation)

Worktree: `D:\Sources-System\.claude\worktrees\agent-a4e4a5c609a8f9cfc` (branch name matches the contract exactly;
no collision occurred).

Commits, in order, one per group:
- `R198-A` (05011bd): `UsersViewModel.cs`, `Strings.ar.xaml`, `Strings.en.xaml`, `UsersViewModelTests.cs`.
- `R198-B` (75523f2): `NeutronSourceTypeService.cs`, `NeutronSourceTypeServiceTests.cs`.
- `R198-C` (9144557): `Strings.ar.xaml`, `Strings.en.xaml` (only the two unused keys removed).
- `R198-D` (d593608): `DialogHelper.cs`, `SourceNavigationHelper.cs`, `LocationsViewModel.cs`, `MainViewModel.cs`,
  `PasswordPromptDialog.xaml.cs`.
- `R198-E` (d5cc16c): `AlertsViewModel.cs`, `LeakTestsViewModel.cs`, `LocationsViewModel.cs`, `MainViewModel.cs`,
  `RadioisotopesViewModel.cs`, `SourcesViewModel.cs`, `UsersViewModel.cs`, `MessengerIsolationTests.cs` (new).
- `R198-F`: this file + `docs/session-summary.md` + `docs/release-readiness.md`.

No STOP rule fired in any group. D4 real count: 5 (not 4 as the old backlog said). D5 real count: 7 ViewModels,
7 registrations, 7 sends, 2 unregistrations (matches the lead's discovery exactly).

Test results: `dotnet test Sources.Tests/Sources.Tests.csproj -c Debug` full run — **1342 passed, 0 failed,
0 skipped** (baseline 1337; 6 new `[Fact]` methods confirmed via `git diff 36921cf HEAD -- Sources.Tests`;
net +5 is reported as-is, unexplained beyond the 6 additions, rather than silently assumed to match).
`dotnet build Sources.sln -c Debug` and `-c Release` both succeeded with the same 5 known `CS8604` warnings
(2 in `LoginWindow.xaml.cs`, doubled by the `wpftmp` intermediate build, + 1 in `ViewInstantiationTests.cs`),
zero new warnings introduced by this round.

Deviations from the contract: none.
