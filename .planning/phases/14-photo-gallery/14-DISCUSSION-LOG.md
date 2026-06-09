# Phase 14: Photo Gallery - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-06-07
**Phase:** 14-photo-gallery
**Mode:** `--auto` — all gray areas auto-resolved with the recommended option (no interactive prompts). Recommended = lowest-risk option that honors the locked v1.4 hard invariants + the milestone's additive/no-breaking-changes stance + the v1.3 photo precedents.
**Areas discussed:** Storage model & legacy PhotoUrl reconciliation, Photo count cap, Multi-upload/reorder/circuit-safety, AI helper contract, Paste-URL HEAD-validation, Delete/orphan cleanup, .cookbook.json export behavior

---

## Storage model + reconciliation with legacy `Recipe.PhotoUrl` (⚠ flagged)

| Option | Description | Selected |
|--------|-------------|----------|
| (A) Keep `PhotoUrl` as a synced primary mirror; gallery rows live only in `RecipePhoto` | Zero downstream rewiring; JSON-LD `image`, RecipeView hero, Home, collage all keep reading `PhotoUrl`; gallery data never enters canonical | ✓ |
| (B) Remove `PhotoUrl` from canonical; all readers query `RecipePhoto` | More literally honors "no photo paths in canonical" but large blast radius on shipped v3 design; contradicts no-breaking-changes | |

**Auto-selected:** (A). **Notes:** Flagged as the highest-stakes decision (D-14-01) — interprets the STATE invariant against the already-shipped canonical `PhotoUrl` field. User should confirm before planning; (B) materially changes the plan and touches the Phase 13 projector.

## RecipePhoto persistence shape

| Option | Description | Selected |
|--------|-------------|----------|
| Relational FK child entity (like RecipeIngredient), cascade delete | Stable per-row identity for reorder/delete/primary flag | ✓ |
| Owned-JSON array on Recipe | No stable identity; awkward for individual delete + file cleanup | |

**Auto-selected:** Relational FK entity (D-14-02/03). One `IsPrimary` per recipe enforced in service layer.

## Photo count cap (STATE open question #1)

| Option | Description | Selected |
|--------|-------------|----------|
| `CookBotSettings.MaxPhotosPerRecipe` default 10, clamped [1,20] | Configurable; follows DatabaseBackupRetention precedent; server-enforced | ✓ |
| Hard-coded ≤5 | More conservative on disk; less flexible | |

**Auto-selected:** Configurable, default 10 (D-14-04-cap).

## Multi-upload + reorder + circuit safety (P14)

| Option | Description | Selected |
|--------|-------------|----------|
| `<InputFile multiple>` + strictly sequential per-file persist; move-up/down + "Set as hero" buttons | Each frame under 12 MB SignalR cap; reuses LocalRecipePhotoStorage; keyboard-accessible reorder | ✓ |
| Parallel upload + HTML5 drag-and-drop reorder | Risks SignalR message-size blowups (P14) and fragile Blazor-Server drag interop | |

**Auto-selected:** Sequential upload (D-14-05) + button reorder (D-14-06). Drag-and-drop deferred.

## AI photo helper contract (GALLERY-04, P12)

| Option | Description | Selected |
|--------|-------------|----------|
| Text-only search-term suggestion via existing IAiService; user pastes/uploads; AI never emits URL | Zero hallucination/copyright risk; gated by hostOn && userOn | ✓ |
| AI returns/embeds candidate image URLs | Violates P12 + REQUIREMENTS out-of-scope; copyright + hallucination risk | |
| Add a vision/image-input path to AnthropicAiService | Out of scope; not needed for text suggestion | |

**Auto-selected:** Text-only helper (D-14-07-ai / D-14-08-transport / D-14-09-disclaimer).

## Paste-URL HEAD-validation (GALLERY-04 — new this phase)

| Option | Description | Selected |
|--------|-------------|----------|
| scheme-allowlist → HTTP HEAD (2xx + image/* content-type), block on failure; 405→ranged GET fallback | Satisfies "HEAD-validated before persist" as a gate; reuses existing validator + HttpClient | ✓ |
| Scheme-allowlist only (current behavior) | Does not meet GALLERY-04's HEAD requirement | |

**Auto-selected:** Add HEAD-validation (D-14-10); apply to existing single paste-URL too.

## Delete / orphaned-file cleanup (GALLERY-03, P13)

| Option | Description | Selected |
|--------|-------------|----------|
| Explicit service-layer file delete on photo-delete + recipe-delete; only local /uploads paths; AssertPathInsideUploadsDirectory guard | EF cascade removes rows not files; external URLs skipped | ✓ |
| Rely on EF cascade alone | Leaves orphaned files in the Docker volume (P13) | |

**Auto-selected:** Explicit cleanup (D-14-11).

## `.cookbook.json` export behavior (STATE open question #2)

| Option | Description | Selected |
|--------|-------------|----------|
| Omit photos entirely, no schema change | Photos already excluded today; honors "stripped from exports" invariant; zero DTO churn | ✓ |
| Add photo rows / explicit note field to transfer DTO | Unnecessary churn; re-download/copyright risk on import | |

**Auto-selected:** Omit, no schema change (D-14-12). UI note about exclusion is Claude's discretion.

## Claude's Discretion

- RecipePhoto column max-lengths (Caption ~512); migration-SQL vs DatabaseSeeder backfill (recommend migration-SQL); gallery UI layout in RecipeView/RecipeEditor (refine at ui-phase); exact AI-helper prompt wording + IAiService method; `RecipePhotoService` vs extending `RecipeService`.

## Deferred Ideas

- HTML5 drag-and-drop reorder; per-step photo linking; Unsplash/Pexels API integration; AI vision "find a photo"; including photos in .cookbook.json with re-download; strict interpretation (B) of the canonical invariant.
