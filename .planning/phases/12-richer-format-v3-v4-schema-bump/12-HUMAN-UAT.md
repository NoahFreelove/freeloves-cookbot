---
status: complete
phase: 12-richer-format-v3-v4-schema-bump
source: [12-VERIFICATION.md]
started: 2026-06-05T00:00:00Z
updated: 2026-06-06T00:00:00Z
---

## Current Test

[testing complete]

## Tests

### 1. SC2 — Editor save/reload round-trip (all four v4 field groups)
expected: In the recipe editor, author an equipment list, a Source & Credit block (Adapted from / Author / a valid `https://…` Source URL), at least one ingredient substitution note, and a per-step doneness cue. Save, then re-open the same recipe in the editor — all four field groups are still populated (no wipe). (Automated full-service regression test `RecipeServiceV4FieldsTests` already proves the RecipeService → CanonicalDocumentJson → reload path; this item is the in-browser confirmation of the editor form binding.)
result: pass

### 2. SC5 — RecipeView display of the four new surfaces
expected: Open the recipe at `/recipes/{id}`. The equipment checklist renders (checking an item strikes it through, ephemeral — not persisted); the substitution sub-line shows under its parent ingredient; the per-step doneness cue renders under the step; the provenance credit renders ("Adapted from {SourceName} by {AuthorName}") with the Source URL as a clickable link. Visuals match 12-UI-SPEC §1-4 (custom Cb atoms, no MudBlazor).
result: pass

### 3. D-12-08 — Provenance SourceUrl scheme-allowlist defang
expected: Edit the recipe, set the Source URL to `javascript:alert(1)`, save, reopen RecipeView — the provenance credit renders as PLAIN TEXT (no `<a>` link), proving the `RecipePhotoUrlValidator` http/https allowlist defang. (Statically confirmed in code; this is the live-DOM confirmation.)
result: pass

### 4. D-12-04 — Substitution amounts do not scale
expected: On a recipe with a structured substitution amount, change the servings/scale control in RecipeView — the substitution amount stays static (only the parent ingredient's amount scales). (Statically confirmed: `FormatSubAmount` is static with no servings multiplier; this is the live confirmation.)
result: pass

## Summary

total: 4
passed: 4
issues: 0
pending: 0
skipped: 0
blocked: 0

## Gaps

None. All 4 in-browser items confirmed by human UAT on 2026-06-06 (all pass) — closing the visual/security DOM confirmations the automated pass could not prove. Combined with the 10/10 automated must-haves and 377 green unit tests, Phase 12 is fully verified. The Phase 16 Playwright harness extension (UATAUTO-02) will codify these as regression tests, but the human gate is now closed.
