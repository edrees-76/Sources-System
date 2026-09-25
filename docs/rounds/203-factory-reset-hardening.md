ROUND 203 — Factory reset hardening: masked admin password, service-level authorization, single-source
confirmation phrase, permanent pre-reset backup

Save THIS TEXT VERBATIM as docs/rounds/203-factory-reset-hardening.md (you may append a separate "Discovery findings"
section below it). Commit it in the round's PR.
BASE: main at 257cbb7 (round 202, PR #98 merged). Branch: fix/round-203-factory-reset-hardening
(if a name collision forces a different name, report it as a deviation).
Risk: HIGH (the operation that erases the inventory). Full pipeline: implementer -> your own diff review ->
change-verifier -> ci-monitor. One commit per group (A..E), prefix "R203-<letter>:". Fixups as separate
"R203-<letter>-fix" commits (never amend, not even before push), reported as deviations. CodeRabbit is not a merge
requirement. SystemResetService.cs, PhraseFactoryResetConfirmation and RequiredResetPhrase are IN SCOPE this round
(lifting the earlier ban), exactly as described below.

ARCHITECT'S READING OF THE CODE ON 257cbb7 (verify each point in discovery; STOP and report if any is false):
R1. Views/SettingsView.xaml, factory-reset stage 2: the admin password is entered in a plain TextBox bound to
    ResetPassword — the password is visible on screen.
R2. Services/SystemResetService.ResetSystemAsync(string executedByUsername) has NO AuthorizationGuard call; the admin
    check exists only in SettingsViewModel.ExecuteFactoryResetAsync (IsAdmin). The service trusts the username string
    passed in. By contrast BackupService.RestoreBackup calls AuthorizationGuard.RequireActivated.
R3. SettingsViewModel.RequiredResetPhrase is a hard-coded literal "إعادة ضبط المنظومة", while the view displays the
    resource PhraseFactoryResetConfirmation (value "إعادة ضبط المنظومة" in BOTH Strings.ar.xaml and Strings.en.xaml).
R4. SystemResetService takes its forced backup via IBackupService.CreateBackup(), which names the file
    SOURCES_backup_<timestamp>.zip and then calls CleanOldBackups(30, targetDir) — so the pre-reset backup is subject
    to the same 30-day cleanup as ordinary backups.

STEP 0 — Worktree housekeeping: same rules as round 202 STEP 0. Report the table and the base hash.

STEP 1 — Discovery (read-only)
D1. Verify R1–R4 with file:line quotes.
D2. The exact AuthorizationGuard calls used by the closest comparable destructive operations (BackupService.RestoreBackup
    and any other admin-only destructive service method). Which guard(s) and in what order.
D3. CleanOldBackups: its exact selection rule (by name pattern? by age? which folders?). What is the SMALLEST change that
    makes the pre-reset backup permanent (never auto-deleted) without changing ordinary backups, restore, or the
    "last backup" display in Settings? Propose it; STOP RULE: if it needs more than ~30 production lines or a change to
    RestoreBackup, stop and report instead of implementing D below.
D4. How a PasswordBox value reaches the ViewModel elsewhere in the project (round 196 UserFormWindow pattern:
    UpdateSourceTrigger=PropertyChanged on the MaterialDesign password binding). Quote it.
D5. Existing tests covering factory reset (service and ViewModel) and what they assert.

STEP 2 — Changes
A. Masked password (commit R203-A): replace the stage-2 TextBox with a PasswordBox using EXACTLY the round-196
   pattern from D4 (value reaches the ViewModel on every keystroke, so Enter/verify never uses a stale value). No other
   UI change. Test: the view binds a PasswordBox (not a TextBox) for ResetPassword; verification with the correct
   password passes and with a wrong one fails.
B. Service-level authorization (commit R203-B): at the start of ResetSystemAsync, before the backup is taken, enforce
   AuthorizationGuard.RequireAdmin using the CURRENT logged-in user from the user service (inject IUserService if
   needed), plus the same activation guard that RestoreBackup uses (per D2). Record the audit row with the current
   user's identity from the session, not the passed-in string (keep the parameter only if removing it would ripple
   beyond SettingsViewModel; state which). Failure returns (false, existing localized admin-only message, null) and
   takes NO backup and deletes NOTHING. Tests: non-admin -> refused, no backup, no rows deleted; admin -> succeeds as
   before; the audit row carries the current admin's UserId.
C. Single-source phrase (commit R203-C): RequiredResetPhrase returns the value of the resource
   PhraseFactoryResetConfirmation (via TranslationHelper, exact Arabic fallback "إعادة ضبط المنظومة"). The resource
   stays Arabic in BOTH dictionaries (final decision: the phrase is always typed in Arabic). No user-visible change.
   Tests: the required phrase equals the displayed resource in Arabic UI AND English UI; typing the Arabic phrase passes
   in both UIs; any other text fails.
D. Permanent pre-reset backup (commit R203-D): apply the smallest change proposed in D3 so the forced pre-reset backup is
   never removed by CleanOldBackups, while ordinary backups keep the 30-day rule. It must remain restorable through the
   normal Restore flow and visible where backups are listed. Tests: a pre-reset backup older than 30 days survives
   cleanup; an ordinary backup older than 30 days is still removed.
E. Documentation (commit R203-E): session-summary.md round 203 entry (CI Release and local Debug separately);
   release-readiness.md: header (main at 257cbb7, round 203 under review); close the PhraseFactoryResetConfirmation
   review item; record R1–R4, the decisions, and the final decision "the reset phrase is always typed in Arabic".

FORBIDDEN
- What the reset deletes or keeps (table list, order, transaction, settings defaults) — unchanged.
- The password hashing/verification logic, AuthorizationGuard itself, role definitions, UserService internals.
- RestoreBackup logic. Ordinary backup naming/retention (only the pre-reset backup becomes permanent).
- LoginWindow, LoginView, SplashWindow. Any audit-log message TEXT (stays Arabic).
- Running a real factory reset against the REAL database: every reset test must use the isolated test data folder
  (round-199 redirection); the sentinel tests must pass.
- No EF migration. No `git add .` / `-A`; explicit pathspecs; `git show --stat` per commit with a one-line
  justification per file.

GIT
- Commits in order A..E, push, open a DRAFT PR to main. Do not merge.

REPORT
- STEP 0 table and base hash.
- D1–D5 findings with quotes (state explicitly whether R1–R4 were confirmed).
- Per commit: hash, files, justification.
- Test counts: local Debug and CI "Build and Test" (CI baseline 1608, Debug baseline 1610; state new tests).
  Confirm the TestDataIsolation sentinel tests passed and that no test touched the real %LOCALAPPDATA%\Sources.
- CI warnings vs the 3 known CS8604.
- Worktree path holding the final commit + exact PowerShell commands in lowercase (with quotes).
- A numbered, step-by-step visual checklist in ARABIC that NEVER executes a real reset on Edrees's data: open Settings →
  Factory Reset tab as admin; confirm the password field shows dots, not text; type a WRONG phrase (fails), then the
  correct Arabic phrase (passes); type a WRONG password (fails); then STOP — do not type the correct password and do not
  press the final button. Repeat the phrase check in English UI (restart after changing language, by design). Log in as
  a non-admin user and confirm the Factory Reset tab is not visible. Include the navigate-away-and-back loop.

---

## Discovery findings (appended by lead)

### STEP 0 — Worktree table

| Item | Value |
|------|-------|
| Working directory | `D:\Sources-System\.claude\worktrees\round-203-factory-reset-hardening-63013d` |
| Branch | `claude/round-203-factory-reset-hardening-63013d` |
| Base commit | `257cbb7` |
| Base description | الجولة 202: فاصل الوقت TimeProvider واختبارات تثبيت الاضمحلال (#98) |
| Working tree | Clean (no uncommitted changes) |

**Deviation**: The app created the branch as `claude/round-203-factory-reset-hardening-63013d` rather than `fix/round-203-factory-reset-hardening`. Reported per contract.

---

### D1 — R1–R4 verification

**R1 — CONFIRMED.** `Sources-System-Project/Views/SettingsView.xaml:1151–1153`:
```xml
<TextBox Grid.Column="0" Text="{Binding ResetPassword, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}"
         IsEnabled="{Binding IsStage2Passed, Converter={StaticResource InverseBoolConverter}}"
         Style="{StaticResource FormTextBox}" Margin="0,0,12,0"/>
```
Plain `TextBox` — password is visible on screen.

**R2 — CONFIRMED.** `Sources-System-Project/Services/SystemResetService.cs:31–122` contains zero `AuthorizationGuard` calls. The admin check exists only in `SettingsViewModel.cs:500`:
```cs
if (!IsAdmin)
{
    DialogHelper.ShowError(...);
    return;
}
```
`BackupService.cs:140`:
```cs
var activation = AuthorizationGuard.RequireActivated(_licenseService!);
if (!activation.Allowed) return (false, activation.Message);
```

**R3 — CONFIRMED.** `Sources-System-Project/ViewModels/SettingsViewModel.cs:88`:
```cs
public string RequiredResetPhrase => "إعادة ضبط المنظومة";
```
`Sources-System-Project/Resources/Strings.ar.xaml:221`:
```xml
<system:String x:Key="PhraseFactoryResetConfirmation">إعادة ضبط المنظومة</system:String>
```
`Sources-System-Project/Resources/Strings.en.xaml:224`:
```xml
<system:String x:Key="PhraseFactoryResetConfirmation">إعادة ضبط المنظومة</system:String>
```
Both dictionaries have the Arabic value. The hard-coded property is independent of the resource key.

**R4 — CONFIRMED.** `Sources-System-Project/Services/SystemResetService.cs:34`:
```cs
var backupResult = _backupService.CreateBackup();
```
`BackupService.CreateBackup(string customPath)` at `BackupService.cs:120`:
```cs
CleanOldBackups(30, targetDir);
```
The pre-reset backup is named `SOURCES_backup_<timestamp>.zip` and falls under the 30-day cleanup.

---

### D2 — AuthorizationGuard pattern for destructive service methods

`BackupService.RestoreBackup` (`BackupService.cs:140`):
```cs
var activation = AuthorizationGuard.RequireActivated(_licenseService!);
if (!activation.Allowed) return (false, activation.Message);
```
Only `RequireActivated` — no `RequireAdmin`.

`UserService.AddUser`, `EditUser`, `DeleteUser`, `UnlockAccount`, `ChangePassword` (representative, e.g. `UserService.cs:121–124`):
```cs
var activation = AuthorizationGuard.RequireActivated(_licenseService!);
if (!activation.Allowed) return (false, activation.Message);

var guard = AuthorizationGuard.RequireAdmin(CurrentUser);
if (!guard.Allowed) return (false, guard.Message);
```
Order: `RequireActivated` first, then `RequireAdmin`.

**Decision for R203-B**: Call `RequireActivated` first (matches RestoreBackup + UserService), then `RequireAdmin` (matches all admin-only service methods). Both checks before any backup or data change.

---

### D3 — CleanOldBackups selection rule and permanent-backup proposal

`BackupService.CleanOldBackups(int maxAgeDays, string? dir)` (`BackupService.cs:415–445`):
- Searches `targetDir` (the subfolder where the backup was written)
- Selects files ending in `.zip` or `.db`
- **AND** filename `Contains("_backup_", StringComparison.OrdinalIgnoreCase)`
- **AND** `CreationTime < DateTime.Now.AddDays(-maxAgeDays)`
- Deletes those files one by one.

**Smallest change (estimated ~14 production lines, no change to RestoreBackup):**

1. **`IBackupService.cs`**: add one signature:
   ```cs
   (bool Success, string Message, string? BackupPath) CreatePreResetBackup();
   ```

2. **`BackupService.cs`**:
   - Add `public (bool Success, string Message, string? BackupPath) CreatePreResetBackup()` that calls the internal helper with `isPermanent: true`.
   - In `CreateBackup(string customPath)` add optional `bool isPermanent = false`:
     - When `isPermanent = true`, use `SOURCES_pre_reset_<timestamp>.zip` filename prefix instead of `SOURCES_backup_`.
     - When `isPermanent = true`, skip the `CleanOldBackups` call.
   - In `GetBackups()`, extend the filename filter to also include `_pre_reset_` (two extra chars in the Where clause).

3. **`SystemResetService.cs`**: call `_backupService.CreatePreResetBackup()` instead of `_backupService.CreateBackup()`.

Effect:
- `SOURCES_pre_reset_<timestamp>.zip` does **not** contain `_backup_` → `CleanOldBackups` never touches it.
- `GetBackups()` returns it → visible in the list, selectable for restore.
- `RestoreBackup` uses any `.zip` path → unchanged, works with pre-reset backups.
- "Last backup" display in Settings (`UpdateLastBackupInfo`) still filters on `_backup_` → pre-reset backups do **not** appear as the last ordinary backup (acceptable per contract: the contract only requires visibility in GetBackups, not in the last-backup chip).

---

### D4 — Round-196 PasswordBox pattern

`Sources-System-Project/Views/UserFormWindow.xaml:61`:
```xml
<PasswordBox materialDesign:PasswordBoxAssist.Password="{Binding EditPassword, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}"
             Style="{StaticResource FormPasswordBox}"/>
```
`materialDesign:PasswordBoxAssist.Password` with `UpdateSourceTrigger=PropertyChanged` pushes the value to the ViewModel on every keystroke. The style `FormPasswordBox` is defined in `Sources-System-Project/Resources/Styles.xaml:577`.

---

### D5 — Existing factory-reset tests

**`Sources.Tests/SystemResetServiceTests.cs`** (6 tests):
1. `ResetSystemAsync_SuccessfulReset_ClearsTargetTablesAndPreservesCoreEntities` — verifies full data deletion, settings reset, single audit row with UserId and backup filename.
2. `ResetSystemAsync_SoftDeletedRecords_AreCompletelyRemoved` — soft-deleted Sources/Locations/NeutronSources removed.
3. `ResetSystemAsync_CertificateFilesOnDisk_DeletesFilesAndPreservesFolder` — disk files deleted, folder preserved.
4. `ResetSystemAsync_CertificateFileDeletionError_DoesNotFailReset` — tolerance to cert deletion failure.
5. `ResetSystemAsync_BackupFails_AbortsAndPreservesAllData` — no data deleted when backup fails.
6. `ResetSystemAsync_BackupFails_PreservesCertificateFilesOnDisk` — disk files preserved when backup fails.

**`Sources.Tests/SettingsViewModelFactoryResetTests.cs`** (4 tests):
1. `IsAdmin_ReturnsTrue_WhenUserIsAdmin`
2. `IsAdmin_ReturnsFalse_WhenUserIsNotAdmin`
3. `VerifyResetPhrase_ExactMatch_PassesStage1`
4. `VerifyResetPhrase_IncorrectPhrase_FailsStage1`
5. `VerifyResetPassword_CorrectPassword_PassesStage2`
6. `VerifyResetPassword_IncorrectPassword_FailsStage2`

**Gaps (new tests required by this round):**
- R203-A: PasswordBox binding in view (not TextBox)
- R203-B: non-admin → refused at service level, no backup taken, no rows deleted; admin → succeeds; audit row carries `UserId` from injected service
- R203-C: `RequiredResetPhrase` equals `PhraseFactoryResetConfirmation` resource in Arabic and English UI; Arabic phrase passes in both
- R203-D: pre-reset backup older than 30 days survives cleanup; ordinary backup older than 30 days is removed
