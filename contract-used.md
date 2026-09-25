# Round 204 — Arabic text leaking into English UI + BiDi symbol flip + percent sign position

## Contract & Execution Details

- **Owner:** Edrees
- **Lead / Implementer:** Antigravity (Advanced Agentic Coding)
- **Base commit:** `6a722b6` (Round 203, PR #99 merged)
- **Branch:** `claude/round-204-ui-text-fixes`
- **Target branch:** `main`
- **Status:** COMPLETED — READY FOR PR

---

## Confirmed Findings & Decisions (F1–F4)

1. **F1 (Arabic leaking in English UI):** `AllModels.cs:523` `FormattedSummary` returned hardcoded string `"N/A (بيانات غير مسجلة)"`. Replaced with `TranslationHelper.GetString("MsgDoseRateNotRecorded") ?? "N/A (بيانات غير مسجلة)"`. Added resource key `MsgDoseRateNotRecorded` in both `Strings.ar.xaml` ("N/A (بيانات غير مسجلة)") and `Strings.en.xaml` ("N/A (No data recorded)").
2. **F2 (Percent sign flipped in RTL):** In `SourcesView.xaml:780`, changed `StringFormat={}{0:N1}%` to `StringFormat={}{0:N1}&#x200e;%`. In ViewModels (`ReportsViewModel.cs:116`, `NeutronSourceDetailsViewModel.cs:163`, `DeletionsViewModel.cs:393`), changed `$"{value:N1}%"` to `$"{value:N1}\u200e%"`. LRM ensures the % symbol stays to the right of numbers in RTL.
3. **F3 (BiDi flip in column header):** In `Strings.ar.xaml:1329`, updated `HeaderUncertaintyPercent` from `<system:String x:Key="HeaderUncertaintyPercent">عدم اليقين %</system:String>` to `<system:String x:Key="HeaderUncertaintyPercent">عدم اليقين&#x200e; %</system:String>`.
4. **F4 (Language restart button):** Confirmed closed automatically — restart button does not exist in the language settings UI.

---

## Commits & Changes

- **R204-A (`b32b75a`):** Localize `MsgDoseRateNotRecorded` in `AllModels.cs:523` and add resource keys in `Strings.ar.xaml` and `Strings.en.xaml`. Added bilingual unit test `FormattedSummary_ReturnsArabicInArabicUi_AndEnglishInEnglishUi` in `DoseRateCalculationTests.cs`.
- **R204-B (`478b9b9`):** Insert LRM mark before percent sign `%` in `SourcesView.xaml:780`, `ReportsViewModel.cs:116`, `NeutronSourceDetailsViewModel.cs:163`, and `DeletionsViewModel.cs:393`. Added unit tests in `NeutronSourceDetailsViewModelTests.cs`.
- **R204-C (`6f002e4`):** Insert LRM mark in Arabic header `HeaderUncertaintyPercent` in `Strings.ar.xaml:1329`. Added bilingual unit test in `NeutronSourcesUITests.cs`.
- **R204-D:** Documentation in `docs/session-summary.md`, `docs/release-readiness.md`, and `contract-used.md`.

---

## Test Results

- Full Debug Test Suite: **1622 Passed**, 0 Failed, 0 Skipped (Baseline: 1617 -> +5 new tests).
- Sentinel tests (`TestDataIsolationSentinelTests`): **6/6 passed** (zero leakage into `%LOCALAPPDATA%\Sources`).
- Build: 0 errors, 5 known warnings (pre-existing CS8604).
