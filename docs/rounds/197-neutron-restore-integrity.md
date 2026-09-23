ROUND 197 — Neutron source restore integrity (restore with a deleted parent; legacy Am-241/Be startup handling)

Save THIS TEXT VERBATIM as docs/rounds/197-neutron-restore-integrity.md (you may append a separate
"Discovery findings" section below it). Commit it in the round's PR.
BASE: main after round 196 (PR #92 merged). Branch: fix/round-197-neutron-restore-integrity
(if a name collision forces a different name, report it as a deviation).
Risk: HIGH (soft-delete/restore lifecycle of regulatory inventory). Full pipeline: implementer -> your own
diff review -> change-verifier -> ci-monitor. One commit per group (A, B, C, D), prefix "R197-<letter>:".
Fixups as separate "R197-<letter>-fix" commits (never amend), reported as deviations.

STEP 0 — Worktree cleanup: same rules as round 196 STEP 0 (PR state as merge signal; never --force; unregistered
folders deleted only if empty or only bin/obj/.vs). Report the table. Continue even if some items are kept.

STEP 1 — Discovery (read-only). STOP and report before coding if any assumption is false or any STOP rule fires.
D1. NeutronSourceService.Restore: quote it. Confirm it checks only duplicate SourceCode and not whether the
    source's NeutronSourceType and Location are active (not soft-deleted).
D2. SourceService restore (regular radioactive sources): does it check that the source's Location (and any other
    REQUIRED parent) is active? Quote it.
D3. EF behaviour: confirm with a test or the model config whether a NeutronSource whose REQUIRED NeutronSourceType
    (or Location) is soft-deleted is dropped by the global query filters from BOTH the active list and the deleted
    list (the "ghost record" CodeRabbit described). Same question for regular sources and their Location.
D4. Restore paths for parents: is there a UI/service path to RESTORE a soft-deleted NeutronSourceType? And a
    soft-deleted Location? (The Deletions log lists sources, neutron sources, locations, users, radioisotopes —
    confirm whether neutron types are restorable anywhere.) Quote file:line.
    STOP RULE: if a deleted NeutronSourceType has NO restore path, stop and report — the architect decides how
    a user can recover in that case.
D5. AppDbContext legacy Am-241/Be block (~:231-237): quote it. How exactly is the legacy row identified
    (code string? seeded Id? other)? Does it move/check linked NeutronSources before soft-deleting? Could it ever
    match a USER-CREATED active type with the same code? STOP RULE: if the legacy row cannot be identified
    reliably without risking a user-created type, stop and report.
D6. READ-ONLY check of the REAL database (this is regulatory data — do not open it with the app, do not write):
    copy the SQLite database file from the path given by DatabasePaths to a temp folder, and query ONLY the
    copy. Report counts:
      a) NeutronSources (IsDeleted = 0 and = 1) whose NeutronSourceType has IsDeleted = 1
      b) NeutronSources whose Location has IsDeleted = 1
      c) regular Sources whose Location has IsDeleted = 1
      d) NeutronSourceTypes with code 'Am-241/Be' (all rows, with IsDeleted)
    List codes only (no other personal data). Delete the temp copy afterwards and say so.
    Do NOT repair anything. If any ghost exists, report it — Edrees decides what to do.

STEP 2 — Changes (only after discovery passes)
A. NeutronSourceService.Restore: refuse the restore when the source's NeutronSourceType or Location is
   soft-deleted. Return a failure with a clear message naming WHICH parent is deleted and telling the user to
   restore that parent first (new keys ar + en; Arabic fallback exact). Keep the existing duplicate-code check
   and its message exactly as they are, and keep the order: parent checks first, then duplicate code.
   No automatic restore of parents. No change to delete guards.
B. SourceService restore: only if D2 shows the same gap for a REQUIRED Location, apply the same refusal
   (Location deleted -> refuse with its own clear message). If D2 shows no gap, skip B and say so.
C. AppDbContext legacy Am-241/Be block: make it safe with the smallest change:
   - never soft-delete the legacy type if ANY NeutronSource (active or deleted) references it — skip and log
     a warning instead;
   - identify the legacy row precisely (per D5) so a user-created type is never touched;
   - idempotent across launches.
   No data migration, no moving of sources, no schema change.
D. Tests + docs:
   Tests: A — restore refused when type deleted; refused when location deleted; succeeds when both active;
   duplicate-code case still returns the existing message. B — same for regular sources if B applied.
   C — legacy type with a linked source is NOT soft-deleted; legacy type without linked sources is handled as
   before; a user-created active 'Am-241/Be' is never touched; running startup twice changes nothing more.
   Docs: session-summary.md round 197 entry, AND fill in the round-196 CI result that was left "pending"
   (CI run 35876315471: 1320 passed, 0 failed, 0 skipped). release-readiness.md: header (main at round 196,
   round 197 under review); close CodeRabbit rows 1, 3, 4; record the D6 result (counts only); record the
   round-124 session-summary claim ("doesn't break linked sources") as corrected (append-only note, do not edit
   the old line).

FORBIDDEN
- Delete guards in NeutronSourceTypeService / LocationService (do not change what may be deleted).
- Any automatic restore of parents; any data repair or UPDATE on the real database.
- UserService, UsersViewModel, LoginWindow, LoginView, SplashWindow, PhraseFactoryResetConfirmation /
  RequiredResetPhrase, SystemResetService.
- EF global query filters themselves; any schema change; no EF migration.
- No `git add .` / `-A`; explicit pathspecs; `git show --stat` per commit with a one-line justification per file.

GIT
- Commits in order, push, open a DRAFT PR to main. Do not merge.

REPORT
- STEP 0 table.
- D1–D6 findings with quotes/evidence (D6 as counts + codes only; confirm the temp copy was deleted).
- Per commit: hash, files, justification.
- Test counts: local Debug and CI "Build and Test" (CI baseline 1320, Debug baseline 1322; state new tests).
- CI warnings vs the 3 known CS8604.
- Worktree path holding the final commit + exact PowerShell commands in lowercase for Edrees's visual check.
- Visual checklist limited to SAFE, read-only actions on real data (open lists and the Deletions log; confirm
  counts unchanged). No deleting or restoring on real data — restore behaviour is covered by automated tests.

---

## Discovery findings (lead, 2026-09-23) — STOPPED at D4

- D1: `NeutronSourceService.Restore` (Services/NeutronSourceService.cs:383-430) checks license, editor, exists, IsDeleted and duplicate SourceCode (:396-398) only. It does not check NeutronSourceType or Location. Confirmed.
- D2: `SourceService.RestoreSource` (Services/SourceService.cs:437-445) already refuses when the source's Location is soft-deleted (`MsgErrSourceRestoreLocationDeleted`; test DeletionsAndAdminPromptTests.cs:781). There is no gap for Location, so B would be skipped. It does not check the REQUIRED Radioisotope (Source.RadioisotopeId is `Guid`, non-nullable).
- D3 (from model config, not a test run): NeutronSource→NeutronSourceType is required (`Guid NeutronSourceTypeId`, AppDbContext.cs:632-635), and GetAll/GetById/GetByLocation Include it (NeutronSourceService.cs:35/65/92). The type filter (:683) turns that Include into an INNER JOIN, so a source whose type is deleted disappears from the ACTIVE list. GetDeleted uses IgnoreQueryFilters (:48), so it still shows in Deletions until restored. After restore it appears in neither list (the ghost). NeutronSource→Location and Source→Location are optional (`Guid?`, IsRequired(false)/SetNull), so they become a LEFT JOIN: the row stays visible and its Location is null. There is no ghost from a deleted Location.
- D4: STOP. `NeutronSourceTypeService.Restore` exists (Services/NeutronSourceTypeService.cs:252), but nothing in the UI calls it. DeletionsViewModel.cs:355-367 restores Sources, NeutronSources, Locations, Users and Radioisotopes only, and NeutronSourceTypesViewModel has no restore command. Locations can be restored (DeletionsViewModel.cs:361). Also, the type delete guard (NeutronSourceTypeService.cs:213) counts only ACTIVE neutron sources because of the query filter, so a type that only deleted sources reference can still be deleted.
- D5: AppDbContext.cs:231-238 finds the legacy row by `Code == "Am-241/Be"` only, with IgnoreQueryFilters and FirstOrDefault (no ordering). It has no seeded Id and does not check linked sources. A user may create an active 'Am-241/Be' after the legacy row was soft-deleted (uniqueness is WHERE IsDeleted = 0), and nothing guarantees FirstOrDefault returns the legacy row. A candidate fingerprint for the legacy row is Code + `AddedBy IS NULL` + the pre-round-124 seed StandardReference (157840d~1). It is not a guaranteed key, so the architect has to confirm it.
- D6 (read-only copy of the real DB; copy deleted): a) 0; b) 0; c) 0; d) 1 row: IsDeleted=1, AddedBy NULL, 0 linked neutron sources. No ghosts.

---

## Architect decisions (verbatim, amend the contract; the amended FORBIDDEN list is authoritative)

Architect decisions for round 197 (these amend the contract; record them verbatim in the round doc as
"Architect decisions" and treat the amended FORBIDDEN list below as authoritative):

1+3. Do NOT add a neutron-type restore path to the Deletions log or the types window. Instead, prevent the ghost
   at its root: change the NeutronSourceType delete guard (NeutronSourceTypeService ~:213) to count ALL linked
   NeutronSources, active AND soft-deleted. A type referenced by any source (active or deleted) cannot be
   deleted. New refusal message (ar + en keys, exact Arabic fallback) stating the type is still referenced by
   N neutron source(s), including deleted ones kept in the Deletions log. Keep group A (Restore refuses when the
   type or location is soft-deleted) as defense-in-depth; its message states the reason plainly.
   The contract's ban on delete guards is lifted ONLY for this one guard (and the radioisotope guard per item 4).

2. D5 fingerprint accepted (Code == "Am-241/Be" AND AddedBy IS NULL AND the legacy StandardReference text),
   combined with group C's rule "never soft-delete if ANY NeutronSource (active or deleted) references it".
   Before relying on it, verify and report that every UI/service path that creates a NeutronSourceType always
   sets AddedBy. If any path can leave AddedBy null, STOP and report.

4. Radioisotope on regular-source restore: first verify (a) whether a regular source's link to its radioisotope(s)
   is REQUIRED such that a soft-deleted radioisotope makes the source disappear (ghost), and (b) whether the
   radioisotope delete guard counts only active sources. Only if BOTH are true: apply the same two layers
   (guard counts active + deleted sources; SourceService restore refuses when a linked radioisotope is deleted),
   with tests. If either is false: do not change it, record the finding, and add it to "قائمة ما بعد الإصدار".

Amended groups: A (neutron Restore refusal), B (radioisotope, only per item 4), C (legacy block with fingerprint +
linked-source rule), D (NEW: neutron type delete guard counts deleted sources), E (tests + docs, as in the original
group D). Tests for D: deleting a type used only by deleted sources is refused; deleting an unused type still works.

Amended FORBIDDEN: Location delete guard unchanged (Location is optional — no ghost). No new restore UI. No
automatic parent restore. No data repair. Everything else in the original FORBIDDEN list still applies.
Continue from STEP 2.

---

## Architect decisions — second set (verbatim)

1. Accept the D5 fingerprint as is (Code == "Am-241/Be" AND AddedBy IS NULL AND the exact legacy
   StandardReference text), combined with the rule "never soft-delete if ANY NeutronSource (active or deleted)
   references it". Do NOT add RequireEditor to NeutronSourceTypeService Create/Update in this round — record it in
   release-readiness "قائمة ما بعد الإصدار" as: "NeutronSourceTypeService Create/Update lack RequireEditor
   (service-level authorization gap; UI reachable only after login) — scheduled for round 198."

2. Apply group B (radioisotope), both layers:
   - RadioisotopeService delete guard: also refuse when db.Sources.IgnoreQueryFilters().Any(s => s.RadioisotopeId == id)
     (keep the existing SourceIsotopes check). Message (ar + en keys, exact Arabic fallback) states the radioisotope
     is still referenced by source(s), including deleted ones kept in the Deletions log.
   - SourceService restore: refuse when the source's primary Radioisotope OR any radioisotope linked via
     SourceIsotopes is soft-deleted. Message names the deleted radioisotope(s) and tells the user to restore it
     first from the Deletions log. Order: existing Location check, then radioisotope check, then duplicate code.
   - Tests: guard refuses deletion of a radioisotope used only by a deleted single-isotope source; guard still
     allows deleting an unused radioisotope; restore refused when primary radioisotope deleted; refused when a
     SourceIsotopes-linked radioisotope deleted; restore succeeds when all parents are active.
   - Extend D6 (same read-only temp-copy method, delete the copy afterwards): count regular sources (active and
     deleted) whose primary Radioisotope is soft-deleted, and sources with a SourceIsotopes link to a soft-deleted
     radioisotope. Codes only. Report; do not repair.

Proceed with the full contract A–E and hand it to round-implementer.

## D6 extension (read-only copy; copy deleted; real DB hash unchanged)

- e) Regular sources whose primary Radioisotope is soft-deleted: 0 (active 0, deleted 0).
- f) Regular sources with a SourceIsotopes link to a soft-deleted radioisotope: 0.
- Context: 0 soft-deleted radioisotopes; 242 of 302 sources have no SourceIsotopes rows (single-isotope), so the primary-radioisotope guard gap was reachable on real data.
