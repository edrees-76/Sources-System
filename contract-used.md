# Round 206 — إدارة عمر الشاشات وتنظيف الموارد (IDisposable)

## Contract & Execution Details

- **Owner:** Edrees
- **Lead / Implementer:** Antigravity (Advanced Agentic Coding)
- **Base commit:** `c096e78` (Round 205, PR #101 merged)
- **Branch:** `claude/round-206-idisposable-cleanup`
- **Target branch:** `main`
- **Status:** COMPLETED — READY FOR PR
- **Risk Level:** MEDIUM (Structural — Screen lifecycle & resource cleanup)

---

## Confirmed Findings & Decisions (F1–F8)

1. **F1 CONFIRMED (Navigation lifecycle in MainViewModel):** `MainViewModel.cs:316-373` — `NavigateTo` replaced `CurrentView` without invoking `(CurrentView as IDisposable)?.Dispose()` on the previous view. Same omission existed in `ForceLogout` (~line 435) and `Dispose` (~line 534).
   - **Resolution:** Added `(CurrentView as IDisposable)?.Dispose();` before assigning new view in `NavigateTo`, before resetting view in `ForceLogout`, and inside `Dispose()`.
2. **F2 CONFIRMED (SettingsViewModel event leak):** `SettingsViewModel.cs:156-162` — Lambda subscription to `_autoBackupService.BackupCompleted` (Singleton service) without `IDisposable` or unsubscription. Because `SettingsViewModel` is registered Transient, visiting Settings repeatedly caused an accumulating memory leak.
   - **Resolution:** Implemented `IDisposable`, stored the event handler in private field `_backupCompletedHandler`, and unsubscribed from `_autoBackupService.BackupCompleted` in `Dispose()`.
3. **F3 CONFIRMED (5 ViewModels registering to WeakReferenceMessenger without IDisposable):**
   - `LeakTestsViewModel.cs:81`
   - `SourcesViewModel.cs:397`
   - `UsersViewModel.cs:132`
   - `LocationsViewModel.cs:114`
   - `RadioisotopesViewModel.cs:121`
   - **Resolution:** Implemented `IDisposable` on all 5 ViewModels, calling `_messenger.UnregisterAll(this);` (and cancelling/disposing `_msgCts` in `RadioisotopesViewModel`) on `Dispose()`.
4. **F4 CONFIRMED (DashboardView search message registration on construction):** `DashboardView.xaml.cs:20-27` — registered `FocusDashboardSearchMessage` in constructor without an `Unloaded` handler or unregistration.
   - **Resolution:** Added `Unloaded += (_, _) => WeakReferenceMessenger.Default.Unregister<FocusDashboardSearchMessage>(this);`.
5. **F5 CONFIRMED (AlertsViewModel & BorrowViewModel):** Both already implemented `IDisposable`, but `Dispose()` was never called on view navigation (resolved automatically by F1 fix).
6. **F6 NOTE (DashboardViewModel):** Fully implements `IDisposable`, but `Dispose()` was never invoked when navigating away (resolved automatically by F1 fix, stopping `_clockTimer` and `_searchDebounceTimer`).
7. **F7 NOTE (MainWindow screensaver dismiss):** `MainWindow.xaml.cs:104` — `screensaver.Dismissed` subscription on each lock — acceptable per contract, no modification required.
8. **F8 NOTE (FirstRunWizardWindow close request):** `FirstRunWizardWindow.xaml.cs:19` — `CloseRequested` transient subscription with wizard window — acceptable per contract, no modification required.

---

## Commits & Changes

- **R206-A (`c961cc3`):** Fix navigation lifecycle in `MainViewModel.cs` by calling `(CurrentView as IDisposable)?.Dispose()` in `NavigateTo`, `ForceLogout`, and `Dispose`. Added 4 unit tests in `Sources.Tests/MainViewModelLifecycleTests.cs` verifying view disposal on navigation, logout, and ViewModel disposal, as well as stopping dashboard timers.
- **R206-B (`9d68f88`):** Fortify `SettingsViewModel` with `IDisposable` and explicit unsubscription of `_backupCompletedHandler`. Implement `IDisposable` with `_messenger.UnregisterAll(this)` across `LeakTestsViewModel`, `SourcesViewModel`, `UsersViewModel`, `LocationsViewModel`, and `RadioisotopesViewModel`. Add `Unloaded` event handler to `DashboardView.xaml.cs` unregistering `FocusDashboardSearchMessage`. Added 7 unit tests in `Sources.Tests/Round206IDisposableTests.cs`.
- **R206-C:** Documentation updates in `docs/session-summary.md`, `docs/release-readiness.md`, and `contract-used.md`.

---

## Test Results

- Full Solution Test Suite (`dotnet test Sources.sln -c Debug`): **1638 Passed**, 0 Failed, 0 Skipped (Baseline: 1627 -> +11 new tests: 4 in R206-A, 7 in R206-B).
- Sentinel tests (`TestDataIsolationSentinelTests`): **6/6 passed** (zero test leakage into `%LOCALAPPDATA%\Sources`).
- Build: 0 errors, 3 known warnings (pre-existing CS8604 in LoginWindow/ViewInstantiationTests).
