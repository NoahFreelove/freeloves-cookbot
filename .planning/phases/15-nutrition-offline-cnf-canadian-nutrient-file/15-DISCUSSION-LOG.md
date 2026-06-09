# Phase 15: Nutrition (Offline CNF — Canadian Nutrient File) - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-06-07
**Phase:** 15-nutrition-offline-cnf-canadian-nutrient-file
**Mode:** `--auto` (Claude auto-selected the recommended option for every area; no interactive prompts)
**Areas discussed:** CNF seed pipeline & schema; Ingredient→CNF matching + deny-list; Volume→mass conversion + fallback density; Compute trigger / cache / invalidation; JSON-LD nutrition wiring; Panel UI / coverage / disclaimer

---

## CNF seed pipeline & schema (NUTR-01)

| Option | Description | Selected |
|--------|-------------|----------|
| Pre-processed compact bundled seed in `seeds/nutrition/`, loaded by `DatabaseSeeder` into new EF tables | Mirrors `seeds/ingredients.json`; verbatim values; idempotent guard | ✓ |
| Ship raw CNF CSVs, parse at startup | Heavier startup + larger repo | |
| Attached standalone `cnf.sqlite` DB | Multi-DB connection complexity | |

**Auto-selected:** Bundled compact seed → new `CnfFood` + `CnfConversionFactor` tables, subset to energy + 3 macros, values verbatim (OGL-Canada). Fully offline, no API key, no `FdcApiKey` (USDA fallback deferred).
**Notes:** `DatabaseSeeder.cs:203` is the load + idempotent-guard precedent. Column/food subsetting is OGL-allowed; value modification is not.

---

## Ingredient → CNF matching + normalization deny-list (NUTR-02; pitfall P4)

| Option | Description | Selected |
|--------|-------------|----------|
| Offline deterministic name match (normalize + deny-list + token match; confidence tier; "--" below threshold) | No AI, no API; match FoodId/description stored + shown | ✓ |
| AI-assisted matching | Hallucination risk + not offline + needs AI key — violates NUTR-01 | |
| Live CNF API | Intermittent + not offline | |

**Auto-selected:** Offline deterministic. Deny-list starter set strips non-nutritive prep/quality modifiers; keeps nutrition-changing modifiers (unsalted, skinless, lowfat, whole) in the search string. Matched CNF description + FoodId always visible (SC2/P4).
**Notes:** Exact deny-list refined at plan time. Confidence thresholds at plan time.

---

## Volume→mass conversion + fallback density (NUTR-03; pitfall P5)

| Option | Description | Selected |
|--------|-------------|----------|
| CNF Conversion Factors first → curated per-ingredient density fallback (not water), marked "≈" | Flour anchor ≈455 kcal; ≥20-ingredient unit tests | ✓ |
| Generic water density fallback (1 g/mL) | Silently doubles flour mass — the pitfall itself | |

**Auto-selected:** CNF factors → curated `IngredientDensity` (~30–50 ingredients, USDA ARS + FAO/INFOODS, King-Arthur cross-check for baking) → mass-direct → "--" if no density. Density lives in a dedicated `IngredientDensityProvider`; `UnitConversionService` reused for pure unit math only (it has no ingredient identity).
**Notes:** Resolves STATE open question "density source". Flour 1 cup → ≈455 kcal anchor (SC3).

---

## Compute trigger / cache / invalidation (NUTR-02; pitfall P7)

| Option | Description | Selected |
|--------|-------------|----------|
| Explicit "Calculate nutrition" CTA → `RecipeNutritionCache` table → stale-mark on doc-hash change | Never blocks save; never in CanonicalDocumentJson | ✓ |
| Compute on every view, no cache | Recomputes each render; STATE invariant says cache | |
| Store nutrition in canonical doc | FORBIDDEN by hard invariant | |

**Auto-selected:** CTA-triggered compute, `RecipeNutritionCache` keyed by RecipeId with a canonical-doc content hash; on change → marked stale + "recalculate" affordance (no silent auto-recompute). `NutritionService` writes the cache, not `RecipeService`.
**Notes:** Preserves the explicit-action + never-block-save contract (SC1/P7) while satisfying NUTR-02 "invalidated when the canonical doc changes".

---

## JSON-LD nutrition wiring (NUTR-06; pure-projector invariant)

| Option | Description | Selected |
|--------|-------------|----------|
| Optional third param `nutrition?` on `JsonLdRecipeProjector.Project`; emit `NutritionInformation` per-serving, omit when null | Projector stays pure; Web layer passes cached nutrition | ✓ |
| Projector fetches nutrition itself | Violates projector's pure/no-DI doc-comment invariant (P15) | |

**Auto-selected:** Optional per-serving value object; emit Schema.org `nutrition` only when present, omit cleanly when absent (SC5). Re-baseline Phase 13 JSON-LD golden snapshot in the same commit.
**Notes:** `RecipeView` reads `RecipeNutritionCache` and passes the value object in.

---

## Nutrition panel UI / coverage / disclaimer (NUTR-04/05; pitfalls P4/P6)

| Option | Description | Selected |
|--------|-------------|----------|
| Panel on `RecipeView` (Cb atoms), per-serving default + total toggle, coverage list, non-dismissable Health Canada disclaimer | Consumer surface; alongside JSON-LD + hero | ✓ |
| Panel on `RecipeEditor` | Nutrition is a read/consumer concern, not authoring | |

**Auto-selected:** RecipeView panel; heading "Estimated nutrition" (never "Calories", SC4); per-serving default + total; unmatched → "--", low-confidence → "≈" with CNF description + FoodId; non-dismissable disclaimer "Estimated nutrition — not suitable for medical dietary planning. Data: Health Canada, Canadian Nutrient File (2015)" (SC4/NUTR-05; OGL-Canada attribution).
**Notes:** Resolves the "CNF attribution UI placement" open question — disclaimer line on every nutrition surface.

---

## Claude's Discretion

- Bundled-seed format (JSON vs SQL), idempotent load guard, which CNF source files to ingest.
- Exact `IngredientDensity` entries + sources; final deny-list contents; confidence-tier thresholds.
- `NutritionService` shape + signatures; match-memo as separate table vs folded into cache.
- `NutritionInformation` value-object type name/shape; panel layout (possible `ui-phase`).
- Test placement (recommend service + projector unit tests this phase).

## Deferred Ideas

- USDA FDC online gap-fill fallback (+ `FdcApiKey` + HttpClient path) — keep Phase 15 strictly offline.
- Interactive per-ingredient match correction/override (read-only visibility is in scope).
- Micronutrients beyond energy + 3 macros; bilingual (FR) nutrition UI.
- Removing the vestigial `Ingredient.NutritionalInfoJson` column (later cleanup migration).
- Auto-recompute on every edit (deliberately stale-mark instead).
