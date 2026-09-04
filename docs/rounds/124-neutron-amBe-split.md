# Round 124 — ب4: split ambiguous Am-241/Be seed row into small/large source types

## Identity
- Owner: Edrees
- Lead: Claude Code
- Base branch: `main`
- Base commit: `9c52261ea83f8f2e5423c0962212b90247457082`
- Working branch: `round-124-neutron-amBe-split`
- Risk: `high` (scientific reference data correction on a regulated entity)
- Parallel-safe: `no`

## Goal
`SeedData()` in `AppDbContext.cs` currently seeds a single `Am-241/Be` type
row with `MeanNeutronEnergyMeV = null` and `AmbientDoseConversionCoefficient
= null`, with `StandardReference` explicitly noting the values depend on
source size (small=393, large=387) and are therefore left undetermined for
the type. ISO 8529-1:2021 Table 1 and ISO 8529-3:2023 Table 2 (both project
reference files, already read and cross-verified this session) give distinct,
authoritative values for "small" and "large" Am-Be sources. This round
replaces the single ambiguous seed entry with two precise ones.

## Evidence and diagnosis
- `Sources-System-Project/Data/AppDbContext.cs`, `SeedData()`, the
  `neutronTypeData` array: the `Am-241/Be` entry (Code = "Am-241/Be") has
  `MeanNeutronEnergyMeV = null`, `AmbientDoseConversionCoefficient = null`,
  and `StandardReference` text stating the small/large ambiguity explicitly —
  confirmed already read this session, not re-quoted here for brevity but
  verify it yourself before implementing.
- ISO 8529-1:2021 Table 1 (page 10 of the project file
  `ISO_85291_2021.pdf`): `241Am-Be(α,n)` "small source" fluence-averaged
  energy = 4,17 MeV; "large source" = 4,05 MeV. Both share half-life 432,6 a.
- ISO 8529-3:2023 Table 2 (page 11 of `ISO_85293_2023.pdf`): `241Am-Be small
  source` h*Φ(10;E) = 393 pSv·cm²; `241Am-Be large source` = 387 pSv·cm².
  (The former undifferentiated value, 391, is explicitly footnoted in that
  table as "Former versions of this document" — i.e., superseded.)
- The seed's upsert loop matches existing rows by `Code` only
  (`NeutronSourceTypes.IgnoreQueryFilters().FirstOrDefault(nt => nt.Code ==
  item.Code)`). If the single `Am-241/Be` code is replaced by two new codes
  in the `neutronTypeData` array without removing the old one, the seed loop
  will **add two new rows** but leave the old `Am-241/Be` row untouched and
  orphaned in the database (not deleted, not soft-deleted) — this must be
  handled explicitly, not left as a side effect.
- Per `docs/release-readiness.md`, the deployment target database starts
  completely empty (no production data migration risk), so an orphaned
  leftover row is a development/testing hygiene concern, not a live-data
  risk — but it must still be handled cleanly, not ignored.

## Architectural decision
1. In the `neutronTypeData` array, replace the single `Am-241/Be` entry with
   two entries:
   - `Code = "Am-241/Be-Small"`, `NameEn = "Americium-241/Beryllium (small source)"`,
     `NameAr = "أمريسيوم-241 / بيريليوم (مصدر صغير)"`, same `ReactionType`/
     `TargetMaterial`/`ParentNuclide`/`HalfLife`/`HalfLifeUnit` as before,
     `MeanNeutronEnergyMeV = 4.17`, `AmbientDoseConversionCoefficient = 393.0`,
     `StandardReference = "ISO 8529-1:2021 Table 1; ISO 8529-3:2023 Table 2"`.
   - `Code = "Am-241/Be-Large"`, mirrored with `NameAr` "(مصدر كبير)",
     `MeanNeutronEnergyMeV = 4.05`, `AmbientDoseConversionCoefficient = 387.0`.
   - Both entries' `Notes` field should briefly explain the size distinction
     in Arabic (e.g., referencing typical activity ranges from ISO 8529-1
     §4.4 if you want to be concrete — small ≈ 37 GBq, large ≈ 370–555 GBq —
     but do not fabricate a precise numeric boundary between "small" and
     "large" if the standard doesn't give one explicitly; if uncertain, keep
     the note qualitative).
2. Explicitly handle the now-superseded old `Am-241/Be` row: after the
   upsert loop for `neutronTypeData`, add logic to find any existing row
   with `Code == "Am-241/Be"` (exact legacy code) and soft-delete it
   (`IsDeleted = true`, `DeletedAt = DateTime.Now`) if found, rather than
   leaving it silently orphaned. Do not hard-delete it (matches the
   project's established soft-delete-only convention for this entity).
3. No change to `NeutronSourceType.cs` model, no migration (`IsDeleted`
   already exists on the entity), no change to any service or UI.
4. Any existing `NeutronSource` records referencing the old `Am-241/Be`
   type's `Id` by foreign key remain valid (the FK relationship is
   `DeleteBehavior.Restrict`, and soft-delete doesn't remove the row) — they
   will now point to a soft-deleted type. This is acceptable for this round
   (no production data exists per release-readiness.md); flag it in the
   completion report as a known consequence, not a defect to fix here.

## Allowed files
- `Sources-System-Project/Data/AppDbContext.cs`
- `docs/release-readiness.md`
- `docs/session-summary.md`

## Forbidden scope
- `LoginWindow`, `LoginView`, `SplashWindow`
- `Sources-System-Project/Models/AllModels.cs` — no model/schema change
- Any XAML view or ViewModel
- `NeutronSourceService.cs`, `NeutronSourceTypeService.cs`,
  `NeutronDecayCalculationService.cs`
- Any `Migrations/*` file — no migration needed this round
- Any other `neutronTypeData` entry (Cf-252, Pu-239/Be, etc.) — touch only
  the Am-241/Be entry

## Acceptance criteria
1. After running the app once (or a test that calls `SeedData()`), the
   database contains `Am-241/Be-Small` and `Am-241/Be-Large` as two active
   (`IsDeleted = false`) `NeutronSourceType` rows with the exact values
   specified above.
2. The old `Am-241/Be` row, if it existed from a prior seed run, is
   soft-deleted, not duplicated and not left active alongside the new two.
3. A test (new or extended in `Sources.Tests`, find the appropriate existing
   seed-data test file — search for one before assuming none exists) proves
   both new rows' `MeanNeutronEnergyMeV` and `AmbientDoseConversionCoefficient`
   match the ISO values exactly, and proves the legacy row ends up
   soft-deleted.
4. Running `SeedData()` a second time (idempotency — already the pattern
   throughout this method) does not create duplicates or re-activate the
   soft-deleted legacy row.
5. Documentation updated in the same commit, explicitly citing both ISO
   documents and table numbers as the source of the new values.

## Required commands
````powershell
dotnet test -c Debug
dotnet test -c Release
````

## Migration protocol
Not applicable — no schema change, `IsDeleted` already exists.

## Expected test baseline
- Report exact count; a new or extended test is required per acceptance
  criterion 3, so the count will increase from 1092/1094 by at least 1.

## Visual verification by Edrees
Not required (no UI in this round — but note for planning: the *next* round
after this one will need it, once screens are touched).

## Completion report requirements
- Base/result SHA, Draft PR URL, per-file justification, exact test counts,
  build warnings, deviations or `none`, remaining risks (explicitly restate
  the orphaned-FK consequence from decision point 4 above), and confirmation
  that only the Am-241/Be entry was touched in `neutronTypeData`.
````