# Phase 12: Richer Format + v3→v4 Schema Bump - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-06-05
**Phase:** 12-richer-format-v3-v4-schema-bump
**Mode:** `--auto` — all gray areas auto-resolved with the recommended (first) option. No interactive prompts.
**Areas discussed:** Substitution shape, Equipment shape, Provenance shape + link safety, Doneness cue shape, Surface scope + AI fill policy, Substitution scaling

---

## Substitution data shape

| Option | Description | Selected |
|--------|-------------|----------|
| Single freeform string | One `string?` note per ingredient; simplest, no structure | |
| Structured record (Note required + Name?/Amount?/Unit?) | `IngredientSubstitution` POCO; freeform note plus optional structured fields | ✓ |
| Fully structured (required name/amount/unit) | No freeform fallback; rigid | |

**Auto-selected:** Structured record with required `Note` + optional `Name`/`Amount`/`Unit` (D-12-01).
**Notes:** Matches FORMAT-01 verbatim. Degrades to note-only; optional structured fields let downstream (Cooklang/nutrition/display) consume them later. Per-ingredient `IReadOnlyList<>` default `[]` (D-12-02); carries `Extras` (D-12-03).

---

## Equipment data shape

| Option | Description | Selected |
|--------|-------------|----------|
| Recipe-level `string[]` | Flat list of tool names | ✓ |
| `EquipmentEntry[]` record | Name + optional quantity/note (research FEATURES.md) | |

**Auto-selected:** Recipe-level `string[]` (D-12-05).
**Notes:** Matches FORMAT-02 verbatim. Cooklang `#cookware` (Phase 13) is name-only, so structured equipment buys nothing in v1.4. Research's `EquipmentEntry` rejected as over-engineering.

---

## Provenance data shape + link safety + AI guard

| Option | Description | Selected |
|--------|-------------|----------|
| `RecipeProvenance? { SourceUrl?, AuthorName?, SourceName? }` | All-optional record; SourceName = "adapted from" credit; drop AdaptedDate | ✓ |
| Full research shape (+ AdaptedDate) | Adds a field FORMAT-04 doesn't ask for | |
| Flat fields on RecipeDocument | No nesting; clutters the top-level record | |

**Auto-selected:** Nullable `RecipeProvenance` record, all-optional, AdaptedDate dropped (D-12-07).
**Notes:** `SourceUrl` link rendered only through the existing `RecipePhotoUrlValidator` scheme-allowlist (D-12-08). AI must never fabricate `SourceUrl`/`AuthorName` — parallels photo-URL anti-feature P12 (D-12-09).

---

## Doneness cue shape

| Option | Description | Selected |
|--------|-------------|----------|
| Freeform `string?` on ContentStep | Alongside existing Temperature; no enum | ✓ |
| Structured/enum cue | Constrained vocabulary; rigid, low value | |

**Auto-selected:** Per-step freeform `DonenessCue string?` on `ContentStep` only (D-12-06).
**Notes:** Independent of and alongside `Temperature`. `[MaxLength]` guard only. Mirrors the `StepTemperature?`-on-`ContentStep` precedent.

---

## Surface scope + AI default-fill policy

| Option | Description | Selected |
|--------|-------------|----------|
| RecipeEditor + RecipeView only | Matches SC5; Cooking Mode deferred | ✓ |
| + Cooking Mode surfacing | Adds equipment modal + doneness callout (research should-have) | |

**Auto-selected:** RecipeEditor + RecipeView only; Cooking Mode deferred (D-12-10). AI default-fills equipment + doneness, never fabricates provenance (D-12-11).
**Notes:** Keeps Phase 12 to the schema bump + its required surfaces (SC5). Cooking Mode surfacing → backlog.

---

## Substitution amount scaling

| Option | Description | Selected |
|--------|-------------|----------|
| Display-only, does NOT scale | Honors "only RecipeIngredient.Amount scales"; RecipeScalingService untouched | ✓ |
| Scale with servings | Parallel scaling path; violates the guardrail | |

**Auto-selected:** Display-only, no scaling (D-12-04).
**Notes:** Honors the hard guardrail. Proportional scaling noted as a v1.5 candidate.

---

## Claude's Discretion

- `[MaxLength]` caps for new string fields (follow `PhotoUrl=2048` / `Description=4096`).
- Editor authoring affordances (substitution sub-rows, equipment tag input, doneness per-step field, provenance meta block) — refine at plan / `ui-phase` time.
- Upcaster guard naming + test fixture filenames (follow existing conventions).

## Deferred Ideas

- Cooking Mode surfacing of equipment + doneness cues (research should-have).
- Structured `EquipmentEntry` (rejected for v1.4).
- Proportional substitution-amount scaling (v1.5 candidate).
- Provenance `AdaptedDate` (not in FORMAT-04).
- First-class `recipeCategory`/`recipeCuisine` v4 fields (locked as tag-derived in Phase 13).
- AI-assisted substitution generation as a dedicated feature.
