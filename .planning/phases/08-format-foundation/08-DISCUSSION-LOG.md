# Phase 8: Format Foundation - Discussion Log

**Gathered:** 2026-05-15
**Mode:** discuss

This log captures the discussion as it happened. The canonical decisions are in `08-CONTEXT.md`; this file is for human reference only (audits, retrospectives).

---

## Area selection

**Question:** Which areas do you want to discuss for Phase 8: Format Foundation?

**Options presented:**
1. TagsJson drop timing — STATE.md flagged this open. CLEAN-02 says column 'drops in a follow-up migration' — within Phase 8 (after backfill verifies), or deferred to a later phase / v1.4? Affects rollback story.
2. Temperature: gas half-stops — SCHEMA-03 specifies `Value: int`. UK gas marks include half-stops (4½, 7½). Accept that with int (round-up convention) or change Value to decimal/double?
3. EF migration grouping — PhotoUrl, Description, RecipeTag + backfill, optional TagsJson drop. Ship as N small migrations (one backup each, clean rollback) or one combined `V3SchemaBump`?
4. Snapshot test + RecipeTag normalization — (a) Phase 1 has hand-rolled `PromptSnapshotTests`; CLEAN-03 adds `Verify.Xunit`. Replace or coexist? (b) RecipeTag.Name backfill: lowercase/trim normalize, or preserve as-typed?

**User selection:** All four (TagsJson drop timing, Temperature gas half-stops, EF migration grouping, Snapshot test + RecipeTag normalization).

---

## Area 1: TagsJson drop timing

**Question:** When should the `TagsJson` column be dropped?

**Options presented:**
1. Same phase, second migration (Recommended) — Phase 8 ships `AddRecipeTagTable` (with backfill) first, all callsites are switched to read/write through the new table, then a follow-up `DropTagsJsonColumn` migration lands later in the same phase once tests pass.
2. Drop in same migration — `AddRecipeTagTable` also drops `TagsJson` atomically.
3. Defer drop to v1.4 — Keep column for one milestone, mirroring how Phase 1 kept `IngredientRefs`.
4. Defer drop to Phase 10 — Wait until QOL-02 (dietary pre-filter) is proven before dropping.

**User selection:** Same phase, second migration.

**Note captured:** Two backup files via `IDatabaseBackupService` (`.pre-AddRecipeTagTable.bak`, `.pre-DropTagsJsonColumn.bak`). v1.3 ships with one source of truth for tags. Decision recorded as **D-26**.

---

## Area 2: Temperature — gas half-stops

**Question:** How should `ContentStep.Temperature` represent gas-mark half-stops (e.g. gas 4½)?

**Options presented:**
1. Keep `Value: int`, no half-stops (Recommended) — Editor UI shows 1–9 only; author rounds.
2. Change to `Value: decimal` — Accepts 4.5, 7.5; custom JsonConverter renders '4½' for display; validator enforces step=0.5 for gas.
3. Keep int + add `HalfStep: bool` field — Awkward shape.
4. Keep int + tenths convention — Non-obvious; not recommended.

**User selection:** Change to `Value: decimal`.

**Note captured:** UK gas-mark half-stops are a real authoring concern; `int` would force authors to round. Final shape: `record StepTemperature(decimal Value, TemperatureUnit Unit)`. Per-unit validator: F/C require whole degrees; gas allows 0.5 steps in range 1.0–9.5. Anthropic schema emits `"type": "number"`. Custom JsonConverter for human-readable export only — wire format stores `4.5`. Decision recorded as **D-27**.

---

## Area 3: EF migration grouping

**Question:** How should the Phase 8 EF migrations be grouped?

**Options presented:**
1. Granular — four migrations (Recommended) — `AddRecipePhotoUrlAndDescription`, `AddRecipeTagTable` (with backfill), `DropTagsJsonColumn`, `AddPantryMatchIndexes`.
2. Bundled — two migrations — `V3SchemaBump` + `TagsJsonCleanup`.
3. Single combined migration — One `Migration_To_V3` does everything.
4. Three migrations (drop AddPantryMatchIndexes) — Defer indexes to Phase 10.

**User selection:** Granular — four migrations.

**Note captured:** Each migration produces its own pre-migration backup file. `AddPantryMatchIndexes` lands in Phase 8 specifically so Phase 10 (QOL-03) can be a pure code-and-test phase with zero EF migrations. Decision recorded as **D-31**.

---

## Area 4: Snapshot test convergence + RecipeTag normalization

### 4a: Snapshot test

**Question:** Phase 1 has hand-rolled `PromptSnapshotTests.cs`. CLEAN-03 adds `Verify.Xunit 31.12.5`. What's the convergence?

**Options presented:**
1. Replace hand-rolled with Verify (Recommended) — Delete `PromptSnapshotTests.cs` + `expected-system-prompt.txt`; new test uses `[UsesVerify]` + `Verifier.Verify`. `PromptDenylistTests.cs` stays.
2. Keep both — Hand-rolled + Verify both fire on the same assertion.
3. Extend hand-rolled, drop Verify — Skip the Verify NuGet entirely.
4. Verify for snapshot, also Verify-ize denylist — Pull Verify into denylist matching.

**User selection:** Replace hand-rolled with Verify.

**Note captured:** Phase 1's hand-rolled `expected-system-prompt.txt` fixture is deleted in the same commit as the new Verify-based `PromptSnapshotTests.cs`. `PromptDenylistTests.cs` remains and gets extended with SCHEMA-10 alias tokens. Decision recorded as **D-35** + **D-36**.

### 4b: RecipeTag normalization

**Question:** How should `RecipeTag.Name` handle case during backfill and new tag insertion?

**Options presented:**
1. Trim + preserve case (Recommended) — Composite UNIQUE on `(RecipeId, Name)` is case-sensitive default; "Vegan" and "vegan" coexist.
2. Trim + lowercase normalize — Forces case-insensitive dedup; changes display casing.
3. Trim + preserve case + case-insensitive index — `COLLATE NOCASE` on Name; display preserved, dedup enforced.
4. Trim + preserve case + dedup at app layer — Service-layer dedup only.

**User selection:** Trim + preserve case.

**Note captured:** Matches existing `TagsJson` freeform behavior — zero migration semantics change. "Vegan" and "vegan" on the same recipe coexist as two distinct tags. Future case-insensitive dedup is a v1.4+ concern if it turns out to be a real problem. Decision recorded as **D-34**.

---

## Decisions Claude made without asking (Claude's discretion)

Captured in `08-CONTEXT.md` §"Claude's Discretion":
- `Migration_V2_To_V3` is a single class (not three composable mini-upcasters) — matches Phase 1 precedent.
- `StepTemperature.cs` is a sibling record to `ContentStep`, not nested — matches Phase 1's `TimerEntry` placement.
- The null-canonical guard added by CLEAN-01 step (a) is permanent (not `// DELETE-AFTER-V1.1` style) — it's a structural invariant going forward.
- README "Recipe Format" section lives inline in `README.md`, not extracted to a separate `docs/` directory.
- File names within `CookBot.Domain/Recipes/`, xUnit `[Fact]` vs `[Theory]` choices, validator error wording, optional `[MaxLength]` attributes, prompt-snapshot fixture profile contents — all left to the planner.

---

## Scope creep mentions

None surfaced during this discussion.

---

## Deferred ideas surfaced (full list in `08-CONTEXT.md`)

- UI surfacing of PhotoUrl / Description / Temperature → Phase 9
- Profile telemetry widget, smart pantry-match, case-insensitive tag dedup → Phase 10 or v1.4+
- `Recipe.IngredientRefs` column drop → re-evaluate in v1.4
- `CookbookTransferDocument.SchemaVersion` envelope bump → not in Phase 8 (only `RecipeDocument.Version` bumps)

---

*Phase: 08-format-foundation*
*Discussion log: 2026-05-15*
