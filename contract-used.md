# Round 207 — النصوص العربية المؤجلة + BiDi إضافية + نصوص XAML

## Contract & Execution Details

- **Owner:** Edrees
- **Lead / Implementer:** Antigravity (Advanced Agentic Coding)
- **Base commit:** `3f750b2` (Round 206, PR #102 merged)
- **Branch:** `claude/round-207-deferred-text-fixes`
- **Target branch:** `main`
- **Status:** COMPLETED — READY FOR PR
- **Risk Level:** LOW (Cosmetic — Text localization, BiDi alignment, XAML hint resources)

---

## Confirmed Findings & Decisions (F1–F14)

1. **F1 CONFIRMED (TooltipText in AllModels.cs):** `AllModels.cs:528-569` — `TooltipText` contained ~12 hardcoded Arabic strings appearing in English UI when inspecting dose rate.
   - **Resolution:** Replaced all hardcoded text with `TranslationHelper.GetString(...)` and appropriate fallback strings. Added keys in `Strings.ar.xaml` and `Strings.en.xaml`.
2. **F2 CONFIRMED (AddedByName fallback in AllModels.cs):** 6 occurrences across `Source`, `Location`, `BorrowRequest`, `Radioisotope`, `NeutronSourceType`, `NeutronSource` had hardcoded `?? "غير معروف"`.
   - **Resolution:** Replaced all 6 occurrences with `?? TranslationHelper.GetString("LabelUnknown") ?? "غير معروف"`.
3. **F3 CONFIRMED (AlertSeverityDisplay in AllModels.cs):** Returned `"حرج"` and `"تحذير"` directly instead of localization keys.
   - **Resolution:** Updated to lookup `LabelSeverityCritical` / `LabelCritical` ("Critical"/"حرج") and `LabelSeverityWarning` / `LabelWarning` ("Warning"/"تحذير"), preserving existing strict characterization test expectations.
4. **F4 CONFIRMED (StatusDisplayName in User):** Returned `"نشط"` and `"موقوف"` directly.
   - **Resolution:** Added `LabelActive` and `LabelInactive` to `Strings.ar.xaml` and `Strings.en.xaml`, and updated `StatusDisplayName` to use `TranslationHelper.GetString`.
5. **F5 CONFIRMED (ArabicResult in LeakTestRecord):** Confirmed intentional in Arabic by design (named `ArabicResult`) — automatically closed without modification.
6. **F6 CONFIRMED (StatusDisplay in LeakTestRecord):** Returned `"متأخر"` and `"ساري"` directly.
   - **Resolution:** Updated to lookup `DueStatusOverdue`/`LabelOverdue` ("Overdue"/"متأخر") and `LabelCurrent`/`DueStatusValid` ("Valid"/"ساري"), returning English text in English UI and exact Arabic in Arabic UI.
7. **F7–F11 CONFIRMED (BiDi symbol alignment in Strings.ar.xaml):**
   - F7: `HeaderEmissionRate`: `معدل الانبعاث المُعاير (n/s)` -> Added LRM (`\u200e`) before opening parenthesis.
   - F8: `ColDoseRate`: `معدل الجرعة (1م)` -> Added LRM before opening parenthesis.
   - F9: `ColEnergyKeV`: `الطاقة (keV)` -> Added LRM before opening parenthesis.
   - F10: `ColGammaConstant`: `ثابت غاما (Γ)` -> Added LRM before opening parenthesis.
   - F11: `ColAverageEnergyMev`: `متوسط الطاقة (MeV)` -> Added LRM before opening parenthesis.
8. **F12 CONFIRMED (MainWindow.xaml:74 brand text):** `TextBlock Text="مصادر"` was hardcoded in sidebar header.
   - **Resolution:** Replaced with `Text="{DynamicResource BrandAppTitle}"`, adding `BrandAppTitle` = `"مصادر"` in `Strings.ar.xaml` and `"Sources"` in `Strings.en.xaml`.
9. **F13 CONFIRMED (LeakTestsView.xaml:451 inspector name hint):** `materialDesign:HintAssist.Hint="د. أحمد علي"` was hardcoded.
   - **Resolution:** Replaced with `{DynamicResource HintInspectorName}`, adding `HintInspectorName` = `"د. أحمد علي"` in `Strings.ar.xaml` and `"e.g. Dr. Ahmed Ali"` in `Strings.en.xaml`.
10. **F14 CONFIRMED (LeakTestsView.xaml:469 notes hint):** `materialDesign:HintAssist.Hint="أي ملاحظات حول إجراءات المسح..."` was hardcoded.
    - **Resolution:** Replaced with `{DynamicResource HintLeakTestNotes}`, adding `HintLeakTestNotes` = `"أي ملاحظات حول إجراءات المسح والمسحة القطنية..."` in `Strings.ar.xaml` and `"Any notes about the wipe test procedure..."` in `Strings.en.xaml`.

---

## Commits & Changes

- **R207-A (`17bd3ad`):** Localize hardcoded strings in `AllModels.cs` (`TooltipText`, `AddedByName`, `AlertSeverityDisplay`, `StatusDisplayName`, `StatusDisplay`) and add resource keys to `Strings.ar.xaml` and `Strings.en.xaml`. Added 5 tests in `Sources.Tests/Round207TextLocalizationTests.cs`.
- **R207-B (`83afe2f`):** Fix BiDi alignment by inserting LRM (`\u200e`) before opening parentheses in `HeaderEmissionRate`, `ColDoseRate`, `ColEnergyKeV`, `ColGammaConstant`, and `ColAverageEnergyMev`. Added 2 tests in `Sources.Tests/Round207BiDiTests.cs`.
- **R207-C (`041d415`):** Localize XAML texts in `MainWindow.xaml` and `LeakTestsView.xaml` using `{DynamicResource}` with new resource entries `BrandAppTitle`, `HintInspectorName`, and `HintLeakTestNotes`. Added 1 test in `Sources.Tests/Round207XamlLocalizationTests.cs`.
- **R207-D:** Documentation updates in `docs/session-summary.md`, `docs/release-readiness.md`, and `contract-used.md`.

---

## Test Results

- Full Solution Test Suite (`dotnet test Sources.sln -c Debug`): **1646 Passed**, 0 Failed, 0 Skipped (Baseline: 1638 -> +8 new tests).
- Sentinel tests (`TestDataIsolationSentinelTests`): **6/6 passed** (zero test leakage into `%LOCALAPPDATA%\Sources`).
- Build: 0 errors, 3 known warnings (pre-existing CS8604 in LoginWindow/ViewInstantiationTests).
