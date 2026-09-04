# Round 123 — ب4 schema: add NeutronSource manufacturing/capsule fields and NeutronSourceType photon ratio

## Identity
- Owner: Edrees
- Lead: Claude Code
- Base branch: `main`
- Base commit: `2d53d30eb0da7693e6eb720ffaceace91d0517e4`
- Working branch: `round-123-neutron-b4-schema`
- Risk: `high` (schema migration + scientific reference data on a regulated entity)
- Parallel-safe: `no`

## Goal
First round of ب4 (neutron source expansion). Adds four new columns identified
by cross-referencing ISO 8529-1:2021 §4.2/4.3 and ISO 8529-3:2023 Table 2
against the current schema:
- `NeutronSource.Manufacturer` (string?) — mirrors `Source.Manufacturer` exactly;
  currently absent on `NeutronSource` despite existing on its sibling entity.
- `NeutronSource.Model` (string?) — same rationale.
- `NeutronSource.CapsuleLengthMm` (double?) and `NeutronSource.CapsuleDiameterMm`
  (double?) — ISO 8529-1 §4.3 confirms encapsulation shape/size is
  source-specific, not standardizable per type, so these belong on the
  instance, not `NeutronSourceType`.
- `NeutronSourceType.PhotonToNeutronDoseRatio` (double?) — ISO 8529-1:2021
  Table 1's "Ratio of photon to neutron ambient-dose-equivalent rates" column,
  a dimensionless ratio (not currently represented by any existing field).

This round is schema + validation + audit-snapshot updates only. No seed-data
correction (splitting the ambiguous "Am-241/Be" type row into small/large —
a separate, later round), no UI screens (later rounds).

## Evidence and diagnosis
- `Sources-System-Project/Models/AllModels.cs`: `NeutronSource` class confirmed
  missing `Manufacturer`/`Model` (present on `Source`, same file). No capsule/
  dimension field exists on either `Source` or `NeutronSource` today.
  `NeutronSourceType` confirmed missing any photon/neutron ratio field;
  `AmbientDoseConversionCoefficient` and `MeanNeutronEnergyMeV` already exist
  and are correctly documented — do not touch them.
- `Sources-System-Project/Data/AppDbContext.cs` `SeedData()`: the seeded
  `Am-241/Be` row already has `AmbientDoseConversionCoefficient = null` with
  `StandardReference` explicitly noting the small/387 vs large/393 ambiguity —
  confirming this is a known, deliberately-deferred gap, not new scope
  invention. **Do not modify `SeedData()` in this round** — the type-split
  correction is intentionally deferred to the next round.
- `Sources-System-Project/Services/NeutronSourceService.cs` (round 119-style
  pattern, closed round 122): `Create`/`Update`/`Delete`/`Restore` already
  call `LogWithChanges` with **hand-written anonymous-object field lists**
  (not reflection-based). Adding `Manufacturer`/`Model`/`CapsuleLengthMm`/
  `CapsuleDiameterMm` to the model does **not** automatically appear in these
  snapshots — the four `newValuesObj`/`oldValuesObj` anonymous objects in this
  file (`Create`, both in `Update`, `Delete`) must be extended to include the
  four new fields, or this round would silently reopen the audit gap just
  closed in round 122.
- `Sources-System-Project/Services/NeutronSourceTypeService.cs`: same pattern;
  its four snapshot objects must be extended to include
  `PhotonToNeutronDoseRatio`.
- Existing validation convention (confirmed in both services today):
  nullable numeric fields get an `IsFinite` guard when provided
  (`AnisotropyFactor`, `RelativeExpandedUncertaintyPercent`,
  `MeanNeutronEnergyMeV`, `AmbientDoseConversionCoefficient` all follow this).
  The new nullable doubles must follow the same convention.
- Migration precedent: `Migrations/20260901184302_AddNeutronCalibrationAndDecayFields.cs`
  is the direct stylistic precedent for this round (same entities, same
  "add nullable columns" shape).

## Architectural decision
1. **`AllModels.cs`**: add to `NeutronSource`:
````csharp
   [MaxLength(100)]
   public string? Manufacturer { get; set; }
   [MaxLength(100)]
   public string? Model { get; set; }
   /// <summary>طول الكبسولة بالملم كما في شهادة المصدر. ISO 8529-1 §4.3: لا معيار
   /// موحّد للأبعاد — خاصية المصدر الفردي لا النوع المرجعي.</summary>
   public double? CapsuleLengthMm { get; set; }
   public double? CapsuleDiameterMm { get; set; }
````
   Add to `NeutronSourceType`:
````csharp
   /// <summary>نسبة معدل جرعة الفوتون المصاحب إلى معدل جرعة النيترون (بلا وحدة).
   /// ISO 8529-1:2021 Table 1 "Ratio of photon to neutron ambient-dose-equivalent
   /// rates". فراغه يعني عدم توفر قيمة مرجعية لهذا النوع.</summary>
   public double? PhotonToNeutronDoseRatio { get; set; }
````
2. **Migration**: run `dotnet ef migrations add AddNeutronManufacturingAndPhotonRatioFields`
   from the project directory. Report the full command output including all
   warnings, and the generated migration file's `Up()`/`Down()` content
   verbatim in the completion report.
3. **`NeutronSourceService.cs`**: add `IsFinite` guards for
   `CapsuleLengthMm`/`CapsuleDiameterMm` (only when provided, `<= 0` should
   also be rejected as invalid for a physical dimension — mirror the pattern
   used for `RelativeExpandedUncertaintyPercent`) in both `Create` and
   `Update`. Extend all four snapshot anonymous objects (Create's
   `newValuesObj`, Update's `oldValuesObj` and `newValuesObj`, Delete's
   `oldValuesObj`) to include `Manufacturer`, `Model`, `CapsuleLengthMm`,
   `CapsuleDiameterMm`. `Restore`'s `newValuesObj` must also be extended.
4. **`NeutronSourceTypeService.cs`**: add `IsFinite` guard for
   `PhotonToNeutronDoseRatio` (when provided) — no `<=0` rejection needed
   for `Create`/`Update` (a ratio of exactly 0 is physically meaningless but
   not mathematically invalid the way a negative dimension is; **do not**
   add a `<=0` rejection unless you find an existing precedent requiring it
   for a similar dimensionless-ratio field — if uncertain, stop and ask
   rather than guessing the business rule). Extend all five snapshot
   anonymous objects (Create, Update ×2, Delete, Restore) to include
   `PhotonToNeutronDoseRatio`.
5. No change to `NeutronDecayCalculationService.cs`, `SeedData()`, any XAML
   view, or any other service.

## Allowed files
- `Sources-System-Project/Models/AllModels.cs`
- `Sources-System-Project/Services/NeutronSourceService.cs`
- `Sources-System-Project/Services/NeutronSourceTypeService.cs`
- `Sources-System-Project/Migrations/*` (new migration files generated by
  `dotnet ef migrations add`, plus the updated `AppDbContextModelSnapshot.cs`)
- `Sources.Tests/NeutronSourceServiceTests.cs`
- `Sources.Tests/NeutronSourceTypeServiceTests.cs`
- `docs/release-readiness.md`
- `docs/session-summary.md`

## Forbidden scope
- `LoginWindow`, `LoginView`, `SplashWindow`
- `Sources-System-Project/Data/AppDbContext.cs` `SeedData()` method — the
  Am-241/Be type-split correction is a separate, later round
- Any XAML view, ViewModel, or UI-facing code — no screens in this round
- `NeutronDecayCalculationService.cs` — the Cf-252 multi-isotope decay
  limitation stays out of scope, undocumented change here
- `Source.cs`/`SourceService.cs` (the non-neutron sibling) — not touched

## Acceptance criteria
1. Migration applies cleanly (`Database.Migrate()` path, exercised via the
   existing test fixture) and the four new columns exist with correct
   nullability.
2. `NeutronSourceService.Create`/`Update` reject non-finite or `<=0`
   `CapsuleLengthMm`/`CapsuleDiameterMm` when provided, with tests proving
   this (mirroring the existing `RelativeExpandedUncertaintyPercent` test
   pattern).
3. `NeutronSourceTypeService.Create`/`Update` reject non-finite
   `PhotonToNeutronDoseRatio` when provided (test required); no `<=0`
   rejection unless explicitly approved during implementation review.
4. A test proves the audit log's `NewValues` for `NeutronSource.Create`
   contains `Manufacturer`/`Model`/`CapsuleLengthMm`/`CapsuleDiameterMm`,
   and similarly for `NeutronSourceType.Create` with
   `PhotonToNeutronDoseRatio` — proving the snapshot-extension step (item 3/4
   above) was actually done, not just the model field added.
5. All existing tests in both service test files continue to pass unmodified
   in their pre-existing assertions.
6. Documentation records this as ب4's first sub-round, explicitly noting what
   remains: seed-data type-split (next round), then screens/UI (later rounds).

## Required commands
````powershell
dotnet ef migrations add AddNeutronManufacturingAndPhotonRatioFields --project Sources-System-Project
dotnet test -c Debug
dotnet test -c Release
````

## Migration protocol
**Applicable this round.** Report the full `dotnet ef migrations add` output
verbatim, including every warning. Include the generated migration's
`Up()`/`Down()` methods verbatim in the completion report. Confirm
`AppDbContextModelSnapshot.cs` was regenerated (not hand-edited).

## Expected test baseline
- Debug/local expected count: 1088 + N (new validation + audit-snapshot
  tests; report exact N and list the new test names)
- Release/CI expected count: matching Debug delta
- Documented conditional-test difference: `TestDataGeneratorTests` under
  `#if DEBUG`

## Visual verification by Edrees
Not required (no UI in this round).

## Completion report requirements
- Base/result SHA, Draft PR URL, per-file justification, exact test counts,
  build warnings, full migration command output and generated
  `Up()`/`Down()` content, deviations or `none`, remaining risks, and
  explicit confirmation that `SeedData()` was not touched.
````