# Round 205 — توحيد معاملات التحويل الزمني وإزالة الازدواج في حسابات الاضمحلال

## Contract & Execution Details

- **Owner:** Edrees
- **Lead / Implementer:** Antigravity (Advanced Agentic Coding)
- **Base commit:** `c9b6a29` (Round 204, PR #100 merged)
- **Branch:** `claude/round-205-decay-unit-unification`
- **Target branch:** `main`
- **Status:** COMPLETED — READY FOR PR
- **Risk Level:** MEDIUM (Scientific computation - service logic update)

---

## Confirmed Findings & Decisions (F1–F4)

1. **F1 (Year conversion constant in DecayCalculationService):** `DecayCalculationService.cs:447` used `365.25` days per year (`value * 365.25 * 86400`).
2. **F2 (Year conversion constant in NeutronDecayCalculationService):** `NeutronDecayCalculationService.cs:13` used `public const double DaysPerYear = 365.2422;`. Direct discrepancy between the two calculation engines.
   - **Resolution:** Added `public const double DaysPerYear = 365.2422;` and unified all year conversion in `DecayCalculationService.cs` on the tropical year standard $365.2422 \times 86400 = 31,556,926.08\text{ s}$.
3. **F3 (Duplication of activity unit conversion factors):** `DecayCalculationService.cs:76-87` and `103-114` duplicated activity unit conversion factors via internal `switch` statements, while the exact same conversion factors are seeded in the database `AppDbContext.cs:76-109` in table `ActivityUnits`.
   - **Resolution:** Removed the internal switch from `ConvertFromBq` and `ConvertToBq`. Injected `IDbContextFactory<AppDbContext>` to read factors dynamically with thread-safe caching and fallback dictionary matching seeded factors. Unknown units strictly throw `ArgumentException($"Unknown unit: {unitSymbol}")`.
4. **F4 (ActivityCalculatorViewModel hardcoded collections):** Confirmed deferred as cosmetic (does not affect calculations or scientific accuracy).

---

## Commits & Changes

- **R205-A (`e755637`):** Unify year conversion constant `DaysPerYear = 365.2422` in `DecayCalculationService.cs` and replace `365.25`. Added unit tests in `DecayCalculationServiceTests.cs`.
- **R205-B (`6228d6f`):** Remove duplicated switch statements in `ConvertFromBq` and `ConvertToBq`, load unit conversion factors from `ActivityUnits` table via `IDbContextFactory<AppDbContext>`, support `"uCi"` alias, and enforce unknown unit validation with `ArgumentException`. Added unit and database integration tests in `DecayCalculationServiceTests.cs`.
- **R205-A-fix (`d908bae`):** Update strict Cs-137 fixed-point values in `Round202TimeHandlingCharacterizationTests.cs` to match the tropical year constant $365.2422 \times 86400 = 31,556,926.08\text{ s}$.
- **R205-C:** Documentation updates in `docs/session-summary.md`, `docs/release-readiness.md`, and `contract-used.md`.

---

## Test Results

- Full Solution Test Suite (`dotnet test Sources.sln -c Debug`): **1627 Passed**, 0 Failed, 0 Skipped (Baseline: 1622 -> +5 new tests).
- Sentinel tests (`TestDataIsolationSentinelTests`): **6/6 passed** (zero leakage into `%LOCALAPPDATA%\Sources`).
- Build: 0 errors, 3 known warnings (pre-existing CS8604 in LoginWindow/ViewInstantiationTests).
