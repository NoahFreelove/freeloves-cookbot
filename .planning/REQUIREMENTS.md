# Requirements — v1.4 Recipe Data & Interoperability

**Milestone:** v1.4 | **Defined:** 2026-06-05 | **Source:** `/gsd-new-milestone` (4 user-selected themes) + parallel research (`research/SUMMARY.md`)

Additive milestone on the stable v1.3 `RecipeDocument` v3 platform. **Zero new NuGet packages** (research consensus — all hand-rolled on System.Text.Json / EF Core / HttpClient). Hard invariants carry forward: canonical-first reads, display-only layers never mutate canonical, schema bumps ride the versioned upcaster chain, trusted-LAN posture, no MudBlazor/Newtonsoft/Microsoft.Extensions.AI.

User decisions (2026-06-05):
- **AI photo helper IN** — Claude suggests search terms; user pastes a free-licensed URL (HEAD-validated). No image generation, no AI-emitted URLs.
- **Nutrition fully offline** — bundle USDA FoodData Central Foundation Foods (CC0) as a SQLite seed; no API key, no live calls. Unmatched ingredients shown explicitly.
- **Schema.org category/cuisine derived from existing tags** — no new v4 schema fields for these.

---

## v1.4 Requirements

### Richer Recipe Format (FORMAT) — v3→v4 schema bump

- [x] **FORMAT-01**: A recipe ingredient can carry one or more substitutions (freeform note + optional structured name/amount/unit), authored in the editor and displayed on the recipe.
- [x] **FORMAT-02**: A recipe can carry an equipment/tools list (recipe-level `string[]`), authored in the editor and displayed on the recipe.
- [x] **FORMAT-03**: A recipe step can carry a per-step doneness cue (freeform `string?`, alongside the existing per-step Temperature), authored and displayed.
- [x] **FORMAT-04**: A recipe can carry source/provenance — `SourceUrl` and `AuthorName` (and optional "adapted from") — authored and displayed with the source link.
- [x] **FORMAT-05**: A v3 `.cookbook.json`/recipe upcasts to v4 with all new fields null/empty — no data loss, no throw (per-field null-coalescing, the v2→v3 pattern); `RecipeUpcasterChain.CurrentVersion` = 4.
- [x] **FORMAT-06**: `RecipeValidator` enforces the new fields' rules; the AI JSON schema (`RecipeJsonSchemaProvider`) includes them and the prompt-snapshot test is updated in the same change (no AI schema drift).
- [x] **FORMAT-07**: `RecipeFormatParser` + `JsonRecipeSerializer` round-trip all four new field groups; parser tests cover null, present, and edge fixtures; no existing v3 test deleted.

### Export & Interoperability (INTEROP) — read-only projections

- [x] **INTEROP-01**: A recipe page emits valid Schema.org `Recipe` JSON-LD (`<script type="application/ld+json">` in server-rendered HeadContent) passing Google Rich Results structural rules.
- [x] **INTEROP-02**: JSON-LD includes `name` + `image` (required) plus recommended fields mapped from the canonical doc; durations are ISO-8601 (`PT30M`); `image` is omitted (not relative) when no absolute HTTPS URL is available; `recipeCategory`/`recipeCuisine` are derived from existing tags; `aggregateRating` is never fabricated.
- [x] **INTEROP-03**: A user can export a single recipe to Cooklang (`.cook`) text, with ingredient chip refs mapped to `@name{amount%unit}`, cookware to `#items`, timers to `~{n%unit}`, sections to `== Section ==`; doneness/substitutions/temperature emitted as `--` comments. _(Phase 13 clarification: the canonical `RecipeDocument` has only recipe-level `Equipment[]` with no inline/step-scoped cookware, and Cooklang's `#cookware` is an inline-in-step token — so recipe-level equipment exports as `>>`/`--` metadata rather than inline `#items`, to avoid fabricating step-scoping the model lacks.)_
- [x] **INTEROP-04**: The Cooklang export action is labeled **export-only** (no re-import implied); special characters (`@`, `#`, `~`) in step text are escaped/sanitized before emission.

### Nutrition (NUTR) — offline USDA FoodData Central

- [ ] **NUTR-01**: USDA Foundation Foods (CC0) ships as a bundled SQLite seed (`seeds/nutrition/`) — nutrition works fully offline with no API key and no runtime external calls.
- [ ] **NUTR-02**: A recipe's nutrition (calories + macros: protein/carbs/fat) is computed from ingredient amounts by matching ingredient names to seeded foods, with results cached per-recipe and invalidated when the canonical doc changes.
- [ ] **NUTR-03**: Volume→mass conversion uses a per-ingredient density lookup (not water-equivalent), reusing the existing unit converter; conversions with assumed density are marked lower-confidence.
- [ ] **NUTR-04**: A recipe shows a nutrition panel with per-serving and total values; ingredients that couldn't be matched are shown explicitly with a coverage indicator — never silently zeroed.
- [ ] **NUTR-05**: Every nutrition surface carries a clear "estimated, not certified — Data: USDA FoodData Central" disclaimer (CC0 attribution + health disclaimer).
- [ ] **NUTR-06**: When nutrition exists, `nutrition.calories` (and macros) are included in the recipe's Schema.org JSON-LD output.

### Photo Gallery & AI Helper (GALLERY) — continues PHOTO-01..14 from v1.3

- [x] **GALLERY-01**: A recipe supports multiple photos via a `RecipePhoto` entity (ordered, optional caption, one primary); an EF migration backfills the existing `Recipe.PhotoUrl` to a primary `RecipePhoto` row (no data loss).
- [x] **GALLERY-02**: A user can upload multiple photos, reorder them, set captions, and choose the primary/hero photo from the recipe editor (respecting the v1.3 12 MB / magic-byte / scheme-allowlist safeguards).
- [x] **GALLERY-03**: The recipe view displays the photo gallery (primary as hero); deleting a photo or recipe removes its file from disk (no orphaned files in the Docker volume).
- [x] **GALLERY-04**: An optional AI helper (gated by `AiFeaturesEnabled`) describes the dish and suggests photo search terms for free-licensed photo sites; the user pastes a URL that is HEAD-validated before persist. The AI never emits or auto-embeds an image URL.

### UAT Automation (UATAUTO) — continues the v1.3 harness

- [ ] **UATAUTO-02**: The `tests/uat-harness/` Playwright harness is extended with hands-free checks for the v1.4 flows (new format fields visible, JSON-LD present + structurally valid, Cooklang export downloads, nutrition panel renders with coverage, gallery primary/reorder), reusing the existing session/discovery libs.

---

## Future Requirements (deferred to v1.5+)

- Cooklang **import** (one-way export ships in v1.4; import needs an NLP-level parser) — out of scope.
- Per-step photo linking (Paprika-style `[photo: name]`) — high complexity, defer.
- Live USDA FDC API fallback (branded/SR Legacy coverage) + per-user/host API key — deferred; v1.4 is offline-bundled-only.
- Unsplash/Pexels API integration for bulk photo backfill — adds an external key dependency; conflicts with self-host posture.
- First-class `RecipeCategory`/`RecipeCuisine` schema fields — v1.4 derives from tags; promote to v4.x only if tag-derivation proves too lossy.
- Tool-use fallback for structured-output regressions (`FUTURE-09`); per-sharer cookbook-import consent banner (`FUTURE-12`).

## Out of Scope

- AI image **generation** (DALL-E / Stable Diffusion) — shows fictional dishes, not the user's food; anti-feature for a tracker.
- AI-emitted/auto-embedded photo URLs — copyright + hallucination risk; the helper only suggests search terms.
- New public/internet-exposed endpoints — trusted-LAN posture preserved; nutrition + export are local computation / static markup.
- A second AI provider or `Microsoft.Extensions.AI` — existing `AnthropicAiService` HttpClient (incl. its vision path) is sufficient.
- Newtonsoft.Json / new schema libraries — System.Text.Json only.
- Aggregate ratings / reviews — no rating system exists; fabricating them violates Schema.org policy.

## Traceability

| REQ-ID | Phase | Status |
|--------|-------|--------|
| FORMAT-01 | Phase 12 | Complete |
| FORMAT-02 | Phase 12 | Complete |
| FORMAT-03 | Phase 12 | Complete |
| FORMAT-04 | Phase 12 | Complete |
| FORMAT-05 | Phase 12 | Complete |
| FORMAT-06 | Phase 12 | Complete |
| FORMAT-07 | Phase 12 | Complete |
| INTEROP-01 | Phase 13 | Complete |
| INTEROP-02 | Phase 13 | Complete |
| INTEROP-03 | Phase 13 | Complete |
| INTEROP-04 | Phase 13 | Complete |
| GALLERY-01 | Phase 14 | Complete |
| GALLERY-02 | Phase 14 | Complete |
| GALLERY-03 | Phase 14 | Complete |
| GALLERY-04 | Phase 14 | Complete |
| NUTR-01 | Phase 15 | Pending |
| NUTR-02 | Phase 15 | Pending |
| NUTR-03 | Phase 15 | Pending |
| NUTR-04 | Phase 15 | Pending |
| NUTR-05 | Phase 15 | Pending |
| NUTR-06 | Phase 15 | Pending |
| UATAUTO-02 | Phase 16 | Pending |
