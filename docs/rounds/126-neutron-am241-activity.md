# Round 126 — ب4: add optional Am-241 activity (value + unit) with decay calculation to NeutronSource

## Identity
- Owner: Edrees
- Lead: Claude Code
- Base branch: `main`
- Base commit: `abe1fbe34abbdf24582a9ef642b575be4e830dcb`
- Working branch: `round-126-neutron-am241-activity`
- Risk: `high` (schema migration + scientific decay calculation on a regulated entity)
- Parallel-safe: `no`

## Goal
Add an optional, independently-entered Am-241 source activity (value + unit,
reusing the existing `ActivityUnit` table) to `NeutronSource`, plus a decay
calculation capability so the current Am-241 activity can be computed. UI
display of the calculated result is deferred to a follow-up round (mirrors
the 123→125 split: schema/calculation first, UI wiring later).

## Evidence and diagnosis
- ISO 8529-1:2021 §4.4: for Am-Be sources, the neutron emission rate "is
  related to the 241Am activity **and is subject to variations according to
  manufacturing process and degree of mixing**" — no universal formula
  converts one to the other; both must be independently recorded from the
  source's certificate.
- **Decision confirmed with Edrees**: the reference date for Am-241 activity
  decay is `NeutronSource.CalibrationDate` (the same date already used for
  the source's general calibration) — **no new date field**.
- `Radioisotope` table already seeds `Am-241` with `HalfLife = 432.2`,
  `HalfLifeUnit = "years"` (`AppDbContext.cs` `isotopeLibraryData`) — this is
  the canonical half-life value.
- `NeutronDecayCalculationService.cs` is a **self-contained, dependency-free**
  calculator (no injected services, no DB access) — its existing
  `CalculateEmissionRate(...)` method implements `B(t) = B₀ × exp(-ln(2) ×
  Δt / T½)` using raw parameters and a private `TryConvertToSeconds` helper
  supporting "years"/"days". Mirror this exact structure for activity
  rather than injecting `IRadioisotopeService`/`IDecayCalculationService` —
  hardcode `432.2`/`"years"` as the Am-241 half-life with an explicit code
  comment noting this intentionally duplicates the canonical `Radioisotope`
  seed value to keep this calculator dependency-free, matching the file's
  existing style.
- `NeutronSourceService.cs`'s five `LogWithChanges` snapshot objects
  (established in rounds 119–123) must be extended with the two new fields
  or this round would silently reopen the closed audit gap.
- `Source.InitialActivityValue`(double, required)/`InitialActivityUnitId`
  (Guid, required FK, `Restrict`) is the reference pattern for a
  certificate-stated activity + selectable unit — mirror it, but nullable
  (this is optional documentation, not the source's primary tracked
  quantity).

## Architectural decision
1. **`AllModels.cs`**: add to `NeutronSource`:
````csharp
   /// <summary>نشاط الأمريسيوم-241 في المصدر كما في شهادته — قيمة مستقلة لا
   /// تُشتق من معدل الانبعاث المُعاير. ISO 8529-1:2021 §4.4: العلاقة بينهما
   /// تتأثر بعملية التصنيع ودرجة الخلط ولا تخضع لصيغة عامة. تاريخ المرجع
   /// لاضمحلالها هو CalibrationDate نفسه (بقرار معتمد، لا حقل تاريخ جديد).</summary>
   public double? Am241ActivityValue { get; set; }
   public Guid? Am241ActivityUnitId { get; set; }
   public ActivityUnit? Am241ActivityUnit { get; set; }
````
2. **`AppDbContext.cs`** `OnModelCreating`, inside the existing
   `NeutronSource` entity block: add the FK exactly like `LocationId`'s
   pattern:
````csharp
   entity.HasOne(n => n.Am241ActivityUnit)
       .WithMany()
       .HasForeignKey(n => n.Am241ActivityUnitId)
       .IsRequired(false)
       .OnDelete(DeleteBehavior.Restrict);
````
3. **Migration**: `dotnet ef migrations add AddNeutronSourceAm241Activity
   --project Sources-System-Project`. Report full output and generated
   `Up()`/`Down()` verbatim.
4. **`NeutronSourceService.cs`**: `IsFinite`+`>0` guard for
   `Am241ActivityValue` when provided (mirroring `CapsuleLengthMm`'s guard),
   in `Create`/`Update`. If exactly one of value/unit is set, reject with a
   clear message (both-or-neither). Extend all five snapshot objects.
5. **`INeutronDecayCalculationService.cs`** / **`NeutronDecayCalculationService.cs`**:
   add a new public method mirroring `CalculateEmissionRate`'s exact
   structure and status-enum pattern (reuse `NeutronDecayCalculationStatus`
   if its cases fit, or note explicitly if a new minimal status is
   needed — do not invent unrelated new statuses):
````csharp
   NeutronDecayResult CalculateCurrentAm241Activity(NeutronSource? source);
   NeutronDecayResult CalculateAm241ActivityAtDate(NeutronSource? source, DateTime calculationDate);
````
   Internally: if `Am241ActivityValue`/`Am241ActivityUnitId` are null, return
   a clear "not recorded" status (do not treat this as an error — the field
   is optional). If both are set, convert the value to Bq using
   `Am241ActivityUnit.ConversionToBq` (the caller must have loaded the
   navigation property, or the method receives the unit's `ConversionToBq`
   directly — decide the cleanest signature and justify it in the report),
   then apply the same decay formula using `CalibrationDate` as the
   reference date and the hardcoded `432.2`/`"years"` half-life.

## Allowed files
- `Sources-System-Project/Models/AllModels.cs`
- `Sources-System-Project/Data/AppDbContext.cs`
- `Sources-System-Project/Services/NeutronSourceService.cs`
- `Sources-System-Project/Services/INeutronDecayCalculationService.cs`
- `Sources-System-Project/Services/NeutronDecayCalculationService.cs`
- `Sources-System-Project/Migrations/*`
- `Sources.Tests/NeutronSourceServiceTests.cs`
- `Sources.Tests/NeutronDecayTests.cs`
- `docs/release-readiness.md`
- `docs/session-summary.md`

## Forbidden scope
- `LoginWindow`, `LoginView`, `SplashWindow`
- Any XAML view or ViewModel — no UI/display wiring in this round
- `SeedData()` — do not touch existing type/data seeding
- `Source.cs`/`SourceService.cs`/`DecayCalculationService.cs` (the
  general-source decay service) — not touched
- `IDecayCalculationService.cs` — do not inject it; keep
  `NeutronDecayCalculationService` dependency-free as it is today
- `CalculateEmissionRate`'s existing logic/signature — additive only

## Acceptance criteria
1. Migration applies cleanly; both new columns nullable, FK correctly
   `Restrict`/optional.
2. `Create`/`Update` accept a valid finite positive value + a set unit;
   reject non-finite/`<=0`; reject exactly-one-of-pair-set (tests required
   for each case).
3. `CalculateCurrentAm241Activity`/`CalculateAm241ActivityAtDate` return a
   clear "not recorded" result when the fields are null, and a correctly
   decayed value (verify against a hand-computed expected value in the
   test, not just "returns non-null") when both are set — test required,
   mirroring `NeutronDecayTests.cs`'s existing style for emission rate.
4. A test proves the audit log's `NewValues` contains both new fields when
   set.
5. All existing tests continue to pass unmodified.
6. Documentation updated, citing ISO 8529-1:2021 §4.4, noting
   `CalibrationDate` as the confirmed reference date, and that UI wiring is
   deferred to a follow-up round.

## Required commands
````powershell
dotnet ef migrations add AddNeutronSourceAm241Activity --project Sources-System-Project
dotnet test -c Debug
dotnet test -c Release
````

## Migration protocol
**Applicable.** Report full output and generated `Up()`/`Down()` verbatim.

## Expected test baseline
- Report exact count; at least 5-6 new tests expected (baseline 1109/1107).

## Visual verification by Edrees
Not required (no UI in this round).

## Completion report requirements
- Base/result SHA, Draft PR URL, per-file justification, exact test counts,
  build warnings, full migration output, deviations or `none`, remaining
  risks, explicit confirmation `SeedData()`, `DecayCalculationService.cs`,
  and `IDecayCalculationService.cs` were not touched.
````