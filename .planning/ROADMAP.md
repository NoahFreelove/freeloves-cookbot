# Roadmap: FreelovesCookBot

## Milestones

- ✅ **v1.0 (pre-GSD existing app)** — codebase mapped in `.planning/codebase/`
- ⏸ **v1.1 Canonical Format & AI Conformance** — Phases 1–2 shipped 2026-04-25/26; Phase 3 absorbed into v1.2; Phase 4 deferred to v1.3+
- ✅ **v1.2 UI Redesign** — Phases 5–7 shipped 2026-04-27, 16 plans, 75/75 reqs ([archive](milestones/v1.2-ROADMAP.md))
- ✅ **v1.3 Production-Ready & Format Maturity** — Phases 8–11 shipped 2026-06-05, 39 plans ([archive](milestones/v1.3-ROADMAP.md))
- 🔄 **v1.4 Recipe Data & Interoperability** — Phases 12–16, active

## Phases

<details>
<summary>✅ v1.2 UI Redesign (Phases 5–7) — SHIPPED 2026-04-27</summary>

- [x] Phase 5: Foundation — Design tokens, atoms, shell, dialogs (5/5 plans) — completed 2026-04-27
- [x] Phase 6: Marquee surfaces — Home, Cooking Mode, Recipe View, Recipe Editor (4/4 plans, absorbs v1.1 EDITOR-01..07) — completed 2026-04-27
- [x] Phase 7: Remaining surfaces, accessibility, MudBlazor strip (7/7 plans + 2 post-ship slices) — completed 2026-04-27

Full details: [`milestones/v1.2-ROADMAP.md`](milestones/v1.2-ROADMAP.md) · [requirements](milestones/v1.2-REQUIREMENTS.md) · [audit](milestones/v1.2-MILESTONE-AUDIT.md)

</details>

<details>
<summary>⏸ v1.1 Canonical Format & AI Conformance (Phases 1–4) — partial</summary>

- [x] Phase 1: Canonical Format Foundation (4/4 plans) — completed 2026-04-25
- [x] Phase 2: AI Structured Output & Conformance (5/5 plans) — completed 2026-04-26
- [~] Phase 3: Editor UX Without Special Syntax — absorbed into v1.2 Phase 6 (EDITOR-01..07 → ED-03..09)
- [→] Phase 4: Format-Driven New Field & Cleanup — deferred to v1.3+ (FUTURE-V1.1-01..05)

Phase artifacts remain in `.planning/phases/01-canonical-format-foundation/` and `.planning/phases/02-ai-structured-output-conformance/` (load-bearing for v1.2 surfaces).

</details>

<details>
<summary>✅ v1.3 Production-Ready & Format Maturity (Phases 8–11) — SHIPPED 2026-06-05</summary>

- [x] Phase 8: Format Foundation (13/13 plans) — V2→V3 canonical schema bump, LegacyRecipeProjector deletion, TagsJson→RecipeTag, prompt-snapshot test, README format section — completed 2026-05-16
- [x] Phase 9: Photos + Prod-Ready Infrastructure (7/7 plans) — file upload + paste-URL safety, Docker + compose, encrypt-at-rest API key, token-cost telemetry, README deploy docs — completed 2026-05-16
- [x] Phase 10: QOL, Polish & Consumer Surfaces (14/14 plans) — scored pantry-match, AI-Chat raw-edit recovery, accent picker, prompt editor, token-cost widget, 5 polish items — completed 2026-05-17
- [x] Phase 11: v1.3 UAT Cleanup & Automated UAT Harness (5/5 plans) — CLEANUP-01..04 (Edit clip, responsive ≤720px, sidebar, unit-system display conversion) + reusable Playwright UAT harness — completed 2026-06-05

Full details: [`milestones/v1.3-ROADMAP.md`](milestones/v1.3-ROADMAP.md) · [requirements](milestones/v1.3-REQUIREMENTS.md)

</details>

### v1.4 Recipe Data & Interoperability (Phases 12–16) — ACTIVE

- [x] **Phase 12: Richer Format + v3→v4 Schema Bump** — ingredient substitutions, equipment list, per-step doneness cues, source/provenance; upcaster chain to v4; AI prompt + snapshot test (4/4 plans; automated-verified 10/10 + 377 tests; human UAT 4/4 pass) — completed 2026-06-06
- [x] **Phase 13: Export & Interoperability** — Schema.org JSON-LD in RecipeView head; Cooklang (.cook) one-way export; depends on Phase 12 (completed 2026-06-06)
- [x] **Phase 14: Photo Gallery** — RecipePhoto entity + multi-upload + gallery UI + AI search-term helper; depends on Phase 12 (completed 2026-06-07)
- [x] **Phase 15: Nutrition (Offline CNF)** — bundled Canadian Nutrient File seed, NutritionService, per-serving panel with coverage indicator + Health Canada attribution disclaimer; nutrition wired into JSON-LD; depends on Phases 12–14 (completed 2026-06-08)
- [ ] **Phase 16: UAT + Integration** — Playwright harness extended for v1.4 flows + cross-theme integration verification

## Phase Details

### Phase 12: Richer Format + v3→v4 Schema Bump

**Goal**: Recipes carry the four deferred format fields (substitutions, equipment, doneness cues, provenance) and the canonical schema is stably v4 before any export or enrichment consumer is written
**Depends on**: Nothing (first v1.4 phase; builds on v1.3 stable platform)
**Requirements**: FORMAT-01, FORMAT-02, FORMAT-03, FORMAT-04, FORMAT-05, FORMAT-06, FORMAT-07
**Success Criteria** (what must be TRUE):

  1. A v3 `.cookbook.json` imported after this phase upcasts to v4 with all four new field groups null/empty — no throw, no data loss, no bundle-throw across field guards
  2. A recipe can be authored with ingredient substitutions, an equipment list, per-step doneness cues, and source/provenance — all fields round-trip through save/reload without corruption
  3. The AI generates recipes that include the new v4 fields (even when null) — the prompt-snapshot test is updated and passing; no AI schema drift
  4. `RecipeUpcasterChain.CurrentVersion` equals 4; `Migration_V3_To_V4` is registered in DI; the gap-detection test covers v3→v4 explicitly
  5. The new fields are displayed in RecipeView (equipment checklist, substitution chips, doneness cue per step, provenance link/author credit)

**Plans**: 4 plans (3 waves)

  - [x] 12-01-PLAN.md — v4 Domain POCOs + Migration_V3_To_V4 + DI/CurrentVersion + validator + upcaster tests (wave 1)
  - [x] 12-02-PLAN.md — round-trip path: editor DTOs + RecipeFormatParser bridge/serialize + round-trip tests (wave 2)
  - [x] 12-03-PLAN.md — AI prompt v4 prose + snapshot regen + schema assertions (wave 2)
  - [x] 12-04-PLAN.md — RecipeEditor authoring + RecipeView display of all four field groups (wave 3, checkpoint)

**UI hint**: yes

### Phase 13: Export & Interoperability

**Goal**: Recipes are readable by external tools — a structured-data script block enables rich results for public deployments, and a Cooklang download gives users a portable plain-text copy
**Depends on**: Phase 12 (both projectors consume v4 `RecipeDocument` fields: `Equipment`, `Provenance.AuthorName`, per-step `DonenessCue`)
**Requirements**: INTEROP-01, INTEROP-02, INTEROP-03, INTEROP-04
**Success Criteria** (what must be TRUE):

  1. RecipeView server-renders a `<script type="application/ld+json">` block that passes Google Rich Results structural validation — `name` and `image` (absolute HTTPS only, omitted when relative/local) are present; durations are ISO-8601 (`PT30M` / `PT1H30M`); `aggregateRating` is never present
  2. Clicking "Export as .cook" downloads a valid Cooklang file where ingredient chip refs become `@name{amount%unit}` tokens, timers become `~{n%unit}`, sections become `== Section ==`, and special characters (`@`, `#`, `~`) in step text are sanitized before emission
  3. The Cooklang download affordance is labeled "Export only (one-way)" — no re-import path is implied or present
  4. `JsonLdRecipeProjector` and `CooklangRecipeProjector` are pure Application-layer functions that receive `RecipeDocument` and return a string — they never call `RecipeService.UpdateAsync` or touch `CanonicalDocumentJson`

**Plans**: 3 plans (2 waves)
Plans:
**Wave 1**

- [x] 13-01-PLAN.md — JsonLdRecipeProjector + Iso8601DurationFormatter (pure Schema.org JSON-LD projector + golden/unit tests)
- [x] 13-02-PLAN.md — CooklangRecipeProjector (pure one-way .cook projector with always-braces + sanitization + golden tests)

**Wave 2** *(blocked on Wave 1 completion)*

- [x] 13-03-PLAN.md — RecipeView integration: prerender-safe load + HeadContent JSON-LD + "Export as .cook" one-way action

### Phase 14: Photo Gallery

**Goal**: A recipe can have a curated gallery of multiple photos with a user-chosen hero, and an AI helper accelerates finding a free-licensed photo without introducing hallucination or copyright risk
**Depends on**: Phase 12 (reads v4 `RecipeDocument`; EF migration backfills from `Recipe.PhotoUrl` which is a v3/v4 field)
**Requirements**: GALLERY-01, GALLERY-02, GALLERY-03, GALLERY-04
**Success Criteria** (what must be TRUE):

  1. Existing recipes with a hero photo are not disrupted — the EF migration backfills `Recipe.PhotoUrl` into a primary `RecipePhoto` row with no data loss
  2. A user can upload multiple photos (sequentially), reorder them, set captions, and designate the primary/hero — changes persist and display correctly in RecipeView's gallery strip
  3. Deleting a photo or recipe removes the corresponding file from `wwwroot/uploads/` — no orphaned files accumulate in the Docker volume; external paste-URL photos leave no local file to clean
  4. The AI photo helper suggests search terms (text only) — the AI never emits or auto-embeds an image URL; a copyright disclaimer is visible on every photo input surface; the user's pasted URL is HEAD-validated before persist

**Plans**: 4 plans (3 waves)
Plans:
**Wave 1** *(foundation, parallel — no file overlap)*

- [x] 14-01-PLAN.md — RecipePhoto entity + EF config + migration with GALLERY-01 backfill + MaxPhotosPerRecipe setting + backfill/cascade test (GALLERY-01)
- [x] 14-02-PLAN.md — PhotoUrlHeadValidator (HEAD + 405→ranged-GET image-URL gate) + DI + unit tests (GALLERY-04 HEAD path)

**Wave 2** *(services, blocked on Wave 1)*

- [x] 14-03-PLAN.md — RecipePhotoService (add/reorder/caption/set-primary/delete + one-primary + cap + ownership) + RecipeService PhotoUrl re-sync + DeleteAsync file cleanup + LocalRecipePhotoStorage.DeletePhysicalFile + service tests (GALLERY-02, GALLERY-03)

**Wave 3** *(UI, blocked on Wave 2, checkpoint)*

- [x] 14-04-PLAN.md — RecipePhotoGalleryManager (sequential upload, reorder, caption, set-hero, delete, paste-URL HEAD, gated AI helper, disclaimer) + RecipeView gallery strip + human-verify checkpoint (GALLERY-02, GALLERY-03, GALLERY-04)

**UI hint**: yes

### Phase 15: Nutrition (Offline CNF — Canadian Nutrient File)

**Data source** (decided 2026-06-07, pre-Phase-15): **Canadian Nutrient File (CNF)** from Health Canada — NOT USDA FDC. User is Canadian (`UnitSystem=Canadian`). CNF is the offline relational-CSV bulk download (~5,993 foods, per-100 g, bilingual) under the **Open Government Licence – Canada** (redistribution allowed; *requires* Health Canada attribution; nutrient values must not be modified — per-serving re-expression is allowed). CNF ships household-measure→gram **Conversion Factors**, which replace most of the external density table. USDA FDC optionally retained as a gap-fill fallback. See PROJECT.md Key Decisions / STATE.md Key v1.4 Decisions.

**Goal**: Every recipe can show an estimated calorie and macro panel computed entirely offline from Canadian Nutrient File data — with explicit coverage indicators and a mandatory disclaimer, never blocking the recipe save path
**Depends on**: Phases 12, 13, 14 (NUTR-06 wires `nutrition.calories` into the JSON-LD block already laid in Phase 13; photo gallery hero feeds `image` in the same block)
**Requirements**: NUTR-01, NUTR-02, NUTR-03, NUTR-04, NUTR-05, NUTR-06
**Success Criteria** (what must be TRUE):

  1. Nutrition is computed fully offline — the Canadian Nutrient File seed is bundled in SQLite; no API key is required, no runtime external calls are made; "Calculate nutrition" CTA triggers computation only on explicit user action, never blocking recipe save
  2. Ingredients that could not be matched show "--" (not zero) with their names listed explicitly; low-confidence volume→mass conversions (where no CNF conversion factor applies) show "≈" and the matched CNF food description + CNF food code are visible to the user
  3. "1 cup all-purpose flour" resolves via the CNF conversion factor (or, when CNF has no factor, an ingredient-specific fallback density — not water density) to approximately 455 kcal — conversions covered by unit tests for at least 20 common cooking ingredients
  4. Every nutrition panel carries a non-dismissable "Estimated nutrition — not suitable for medical dietary planning. Data: Health Canada, Canadian Nutrient File (2015)." disclaimer (satisfies the OGL-Canada attribution requirement); the heading reads "Estimated nutrition," never "Calories"
  5. When nutrition data exists, `nutrition.calories` (and protein/carbs/fat) appear in the recipe's Schema.org JSON-LD output; when absent, the field is omitted cleanly

**Plans**: 7 plans (4 waves)
Plans:
**Wave 1** *(foundation, parallel — no file overlap)*

- [x] 15-01-PLAN.md — Offline CNF seed build script + bundled seeds/nutrition/*.json (NUTR-01 data)
- [x] 15-02-PLAN.md — Domain entities (CnfFood/CnfConversionFactor/RecipeNutritionCache) + NutritionInfoDto + shared IngredientNormalizer (NUTR-01/02/06)
- [x] 15-04-PLAN.md — IngredientDensityProvider curated g/mL table + DI + >=20-ingredient tests (NUTR-03)

**Wave 2** *(blocked on Wave 1)*

- [x] 15-03-PLAN.md — EF configs + DbContext DbSets + AddNutritionTables migration + idempotent seed load (NUTR-01)
- [x] 15-06-PLAN.md — JsonLdRecipeProjector nutrition param + present/absent golden tests (NUTR-06)

**Wave 3** *(blocked on Wave 2)*

- [x] 15-05-PLAN.md — NutritionService matcher + CNF-factor/density compute + cache + DI + RecipeService stale-mark + flour-anchor/unmatched/stale tests (NUTR-02/03/04)

**Wave 4** *(blocked on Wave 3, checkpoint)*

- [x] 15-07-PLAN.md — RecipeView 5-state nutrition panel + CTA + coverage + disclaimer + JSON-LD wiring + human-verify (NUTR-04/05/06)

**UI hint**: yes

### Phase 16: UAT + Integration

**Goal**: The v1.4 flows are verified end-to-end by the automated Playwright harness and cross-theme integration scenarios (nutrition in JSON-LD, gallery hero as Schema.org image) pass hands-free
**Depends on**: Phases 12, 13, 14, 15 (all v1.4 themes complete)
**Requirements**: UATAUTO-02
**Success Criteria** (what must be TRUE):

  1. The Playwright harness (`tests/uat-harness/`) runs all v1.4 checks hands-free: new format fields (substitutions, equipment, doneness, provenance) visible in RecipeView; JSON-LD `<script>` block present and structurally valid; Cooklang export downloads a non-empty `.cook` file; nutrition panel renders with coverage indicator; gallery primary photo displays as hero
  2. Cross-theme integration: a recipe with a gallery hero (absolute HTTPS URL) and computed nutrition emits a Schema.org JSON-LD block containing both `image` and `nutrition.calories` — verified by the harness via DOM inspection
  3. A cookbook exported after Phase 12 (v4 format) re-imports cleanly through the upcaster chain — the round-trip produces identical field values for all four new v4 field groups

**Plans**: TBD

## Progress

| Phase | Milestone | Plans Complete | Status | Completed |
|-------|-----------|----------------|--------|-----------|
| 1. Canonical Format Foundation | v1.1 | 4/4 | Complete | 2026-04-25 |
| 2. AI Structured Output & Conformance | v1.1 | 5/5 | Complete | 2026-04-26 |
| 3. Editor UX Without Special Syntax | v1.1 → v1.2 | 0/8 | Absorbed into v1.2 Phase 6 | — |
| 4. Format-Driven New Field & Cleanup | v1.1 → v1.3+ | 0/TBD | Deferred | — |
| 5. Foundation — Design tokens, atoms, shell, dialogs | v1.2 | 5/5 | Complete | 2026-04-27 |
| 6. Marquee surfaces — Home, CookingMode, RecipeView, RecipeEditor | v1.2 | 4/4 | Complete | 2026-04-27 |
| 7. Remaining surfaces, accessibility, MudBlazor strip | v1.2 | 7/7 | Complete | 2026-04-27 |
| 8. Format Foundation | v1.3 | 13/13 | Complete | 2026-05-16 |
| 9. Photos + Prod-Ready Infrastructure | v1.3 | 7/7 | Complete | 2026-05-16 |
| 10. QOL, Polish & Consumer Surfaces | v1.3 | 14/14 | Complete | 2026-05-17 |
| 11. v1.3 UAT Cleanup & Automated UAT Harness | v1.3 | 5/5 | Complete | 2026-06-05 |
| 12. Richer Format + v3→v4 Schema Bump | v1.4 | 4/4 | Needs UAT (automated-verified) | — |
| 13. Export & Interoperability | v1.4 | 3/3 | Complete    | 2026-06-06 |
| 14. Photo Gallery | v1.4 | 4/4 | Complete   | 2026-06-07 |
| 15. Nutrition (Offline CNF) | v1.4 | 7/7 | Complete   | 2026-06-08 |
| 16. UAT + Integration | v1.4 | 0/TBD | Not started | — |

---

*v1.4 roadmap created 2026-06-05 — 5 phases (12–16), 22 requirements mapped. v1.4 continues phase numbering from v1.3 (ended Phase 11).*

## Backlog

### Phase 999.1: RecipeView Cook button missing — TopBarService navigation race ✅ RESOLVED 2026-05-23

**Goal:** Fix `CbTopBarService` so the TopBar.RightSlot survives a route change to a page that re-sets the slot in `OnInitialized` (RecipeView, RecipeEditor).
**Status:** Resolved 2026-05-23 — see commit history.

**Reproducer (now fixed):** Generate a recipe in AiChat → save → navigate to `/recipes/{id}` (RecipeView). Cook / Edit / Share / Schedule buttons were absent from both `TopBar.RightSlot` (≥721px viewport) and the inline `.recipe-actions-inline-fallback` row (≤720px viewport).

**Actual root cause** (opposite of original hypothesis): Diagnostic traces showed `NavigationManager.LocationChanged` fires ~4ms *AFTER* the new page's `OnInitialized` returns, not before. The original D-57 auto-clear was wiping the slot the new page had just set. Fix: `SetRightSlot` now stamps the URL it was called at, and `HandleLocationChanged` preserves the slot when the destination URL matches the stamp (slot belongs to this page); clears only when URL differs (stale slot from prior page).

> **999.2, 999.3, 999.4, 999.5 promoted to Phase 11** (2026-06-05, `/gsd-progress --next`).
> Full reproducers, suspects, and notes preserved in
> `phases/11-v1.3-uat-cleanup/11-BACKLOG-SOURCE.md` and summarized as Phase 11
> success criteria above (CLEANUP-01..04). Only the resolved 999.1 record is kept here.
