# Phase 15: Nutrition (Offline CNF — Canadian Nutrient File) - Context

**Gathered:** 2026-06-07
**Status:** Ready for planning
**Mode:** `--auto` — all six gray areas auto-resolved with the recommended, lowest-risk option that honors the locked v1.4 hard invariants, the **CNF data-source decision** (PROJECT.md Key Decisions / STATE.md Key v1.4 Decisions, 2026-06-07), and the nutrition pitfall guards P4–P7. The WHAT is fixed by NUTR-01..06; these are the lockable HOW decisions. **Note:** `.planning/research/SUMMARY.md` and `PITFALLS.md` were written pre-CNF (USDA FDC oriented) — translate every "FDC / data-type filter / density-table" guard into CNF terms (CNF Conversion Factors replace the density table for matched foods; CNF `FoodId` + `FoodDescription` shown to user; "Data: Health Canada, Canadian Nutrient File (2015)" disclaimer).

<domain>
## Phase Boundary

Every recipe can show an **estimated** per-serving + total calorie and macro panel computed **fully offline** from the **Canadian Nutrient File (CNF)** — with explicit coverage indicators and a mandatory Health Canada attribution + health disclaimer — and the same numbers wire into the Schema.org JSON-LD block laid in Phase 13. Concretely (NUTR-01..06, locked WHAT):

1. **Bundled CNF seed** (NUTR-01) — the CNF relational-CSV bulk download (~5,993 foods, per-100 g, 2015 edition) ships as a bundled SQLite seed under `seeds/nutrition/`; nutrition works with no API key and **no runtime external calls**. Nutrient values stored **verbatim** (OGL-Canada forbids modifying values; column/food subsetting is allowed).
2. **Compute by matching** (NUTR-02) — calories + protein/carbs/fat computed from `IngredientEntry.Amount`/`Unit`/`Name` by matching names to seeded CNF foods; results cached per-recipe and invalidated when the canonical doc changes.
3. **Volume→mass** (NUTR-03) — CNF per-food household-measure→gram **Conversion Factors first**; CNF-uncovered foods/measures fall back to a **per-ingredient density** (not water), reusing the existing unit converter for pure unit math; assumed (non-CNF) densities flagged lower-confidence.
4. **Panel + coverage** (NUTR-04) — per-serving and total values; unmatched ingredients shown explicitly with a coverage indicator, **never silently zeroed**.
5. **Disclaimer + attribution** (NUTR-05) — every nutrition surface carries "estimated, not certified — Data: Health Canada, Canadian Nutrient File (2015)"; must not imply Health Canada endorsement.
6. **JSON-LD wire** (NUTR-06) — when nutrition exists, `nutrition.calories` (+ macros) appear in the recipe's Schema.org JSON-LD; omitted cleanly when absent.

**Out of scope** (carried from REQUIREMENTS.md / ROADMAP.md): a live CNF/USDA API call path on the recipe-save flow; micronutrients beyond energy + the 3 macros; modifying CNF nutrient values (OGL-forbidden); bilingual (FR) nutrition UI; a per-ingredient interactive match-correction editor (read-only match *visibility* is in scope, manual override is deferred); auto-recompute on every edit. This phase is **additive** — no breaking change to the v4 round-trip or the trusted-LAN posture; nutrition is a **display-only enrichment layer** that never mutates `CanonicalDocumentJson`.

</domain>

<decisions>
## Implementation Decisions

All six gray areas were auto-resolved in `--auto` mode. Recommended (lowest-risk, invariant-honoring) option chosen for each and logged inline.

### CNF seed pipeline & schema (NUTR-01) — `[auto] recommended`
- **D-15-01 (pipeline):** Pre-process the CNF relational CSVs **offline** (one-time build step) into a compact bundled seed under **`seeds/nutrition/`** (JSON, mirroring `seeds/ingredients.json`), loaded by `DatabaseSeeder` into new EF-mapped CNF tables at startup — same `Path.Combine(contentRootPath, "..", "seeds", …)` + idempotent guard convention as the ingredient seed (`DatabaseSeeder.cs:203`). **Rejected:** shipping raw multi-file CNF CSVs parsed at startup (heavier startup + larger repo); an attached standalone `cnf.sqlite` (multi-DB connection complexity). Values stored **verbatim** — column/food subsetting is OGL-allowed, value modification is not.
- **D-15-02 (schema):** Two denormalized tables — `CnfFood { FoodId (CNF FoodID, PK) · FoodDescription (EN) · FoodGroup? · EnergyKcalPer100g · ProteinGPer100g · FatGPer100g · CarbGPer100g }` + `CnfConversionFactor { FoodId (FK) · MeasureDescription · ConversionFactorValue }`. Subset to **energy + 3 macros** (the NUTR-02 scope) → ~6 k food rows, compact. CNF Conversion Factor value is the per-CNF-spec multiplier that converts the named household measure to grams.
- **D-15-03 (offline-only, no key):** Phase 15 ships **fully offline — no API key, no `CookBotSettings.FdcApiKey`, no `HttpClient` nutrition path** (SC1: "no runtime external calls are made"). The optional USDA FDC gap-fill fallback is **deferred** (see Deferred).

### Ingredient → CNF matching + normalization deny-list (NUTR-02; resolves STATE open Q "deny-list"; pitfall P4) — `[auto] recommended`
- **D-15-04 (strategy):** Fully **offline deterministic** name match — normalize `IngredientEntry.Name`, strip a modifier deny-list, token-match against `CnfFood.FoodDescription`, store matched `FoodId` + `FoodDescription` + a **confidence tier**; below threshold → **unmatched ("--")**, never a silent low-confidence number. **Rejected:** AI-assisted matching (hallucination risk + not offline + needs AI key — violates NUTR-01); live CNF API (intermittent, not offline).
- **D-15-05 (deny-list starter set):** Strip non-nutritive prep/quality modifiers before search — *chopped, minced, diced, sliced, shredded, grated, ground, sifted, packed, room-temperature / room temperature, cold, warm, good-quality / good, fine, coarse, large, small, medium, ripe, to taste, optional, divided, for garnish, plus more, organic, finely, roughly, freshly*. **Keep IN the search string** any modifier that changes nutrition (e.g. *unsalted, salted, skinless, lowfat / low-fat, whole, light, heavy*). Exact list refined at plan time (discretion).
- **D-15-06 (match visibility, P4):** The matched **CNF food description + CNF `FoodId`** are always visible per ingredient in the coverage UI (SC2); low-confidence matches flagged. No silent auto-persist of a low-confidence match.

### Volume→mass conversion + fallback density (NUTR-03; resolves STATE open Q "density source"; pitfall P5) — `[auto] recommended`
- **D-15-07 (priority):** **CNF Conversion Factors first** (matched food + matching household measure → grams; high confidence). When CNF has no factor for that food/measure → **per-ingredient fallback density** (not water), result marked **low-confidence ("≈")**. When neither applies and the unit is already mass → use directly. Volume with no density available → that ingredient is unmatched-for-conversion ("--"/"≈" per NUTR-04). The flour anchor (SC3): "1 cup all-purpose flour" → CNF factor (or curated flour density ≈120–125 g/cup) → **≈455 kcal**, never water density (≈237 g → ~860 kcal).
- **D-15-08 (fallback density source):** A small curated **`IngredientDensity`** table (~30–50 common cooking ingredients, g/mL) sourced from **USDA ARS measurement-conversion tables + FAO/INFOODS density database** (authoritative, public/redistributable), cross-checked against King Arthur for baking staples. Unit-tested for **≥20 common ingredients** (NUTR-03 SC3).
- **D-15-09 (placement):** Reuse `UnitConversionService` / `IUnitConverter` for **pure unit math only** (cup→mL, oz→g — it is ingredient-agnostic and already handles vol↔vol / mass↔mass). The food-specific density (mL→g) lives in a dedicated **`IngredientDensityProvider`** consumed by `NutritionService`. **Do not overload `UnitConversionService`** with food-specific density (it has no ingredient identity); keeping density separate makes it unit-testable in isolation (P5 prevention).

### Compute trigger, cache & invalidation (NUTR-02; pitfall P7) — `[auto] recommended`
- **D-15-10 (trigger):** "Calculate nutrition" is an **explicit user CTA** — **never** on the save path (`RecipeService.Create/UpdateAsync` must not block on nutrition; SC1/P7). Save always succeeds regardless of nutrition state.
- **D-15-11 (storage):** A new **`RecipeNutritionCache`** table keyed by `RecipeId` — stores computed **total + per-serving** energy/macros, a coverage summary, the per-ingredient match results (FoodId / description / confidence), and a **content hash of the canonical doc**. **Never** stored in `CanonicalDocumentJson` (hard invariant); `NutritionService` writes this table, not `RecipeService`.
- **D-15-12 (invalidation):** On canonical-doc change (hash mismatch) the cached panel is marked **stale** and shows a "recipe changed — recalculate" affordance rather than auto-recomputing — satisfies NUTR-02 "invalidated when the canonical doc changes" while preserving the explicit-action + never-block-save contract. (Recompute happens on the next CTA, not silently.)

### JSON-LD nutrition wiring (NUTR-06; pure-projector invariant) — `[auto] recommended`
- **D-15-13:** Add an **optional third parameter** to `JsonLdRecipeProjector.Project(doc, absoluteImageUrl, nutrition?)` — a small **pure value object** (per-serving kcal + macros). Emit a Schema.org `nutrition` `NutritionInformation` object (`calories`: `"N calories"`, `proteinContent` / `carbohydrateContent` / `fatContent`: `"N g"`) **only when nutrition exists**; omit cleanly when null (SC5). **Per-serving** values (Schema.org `nutrition` is per-serving). The Web layer (`RecipeView`) reads the `RecipeNutritionCache` and passes the value object in — the projector **stays pure** (no DI, no data-service, no `CanonicalDocumentJson` access — per its own doc-comment invariant; P15). **Rejected:** projector self-fetching nutrition.

### Nutrition panel UI, coverage indicator & disclaimer (NUTR-04/05; resolves attribution-placement open Q; pitfalls P4/P6) — `[auto] recommended`
- **D-15-14 (surface):** A nutrition panel on **`RecipeView`** (the consumer surface where JSON-LD + hero already live), below the recipe body — built on existing **Cb atoms / design tokens** (no MudBlazor). An explicit **"Calculate nutrition" CTA** shown when uncached or stale.
- **D-15-15 (values):** Per-serving **and** total, default **per-serving** (matches Schema.org + the panel framing). Heading reads **"Estimated nutrition"** — never "Calories" (SC4).
- **D-15-16 (coverage, P4/NUTR-04):** Unmatched ingredients listed **explicitly with "--"** (never zero). Low-confidence volume→mass conversions (no CNF factor) shown with **"≈"** + the matched **CNF description + `FoodId`** visible (SC2). A coverage summary (e.g. "matched 11/13 ingredients") makes silent gaps impossible.
- **D-15-17 (disclaimer, P6/SC4/NUTR-05):** A **non-dismissable** line on **every** nutrition surface (panel + any future surface): **"Estimated nutrition — not suitable for medical dietary planning. Data: Health Canada, Canadian Nutrient File (2015)."** — satisfies the OGL-Canada attribution requirement **and** the health disclaimer; must not imply Health Canada endorsement.

### Claude's Discretion
- Exact bundled-seed format (JSON vs a generated SQL/seed file), the idempotent load guard (skip when `CnfFood` rows exist — mirror the ingredients-seed guard), and which CNF source files to ingest (Food Name + Nutrient Amount + Conversion Factor + Measure Name files; subset to energy + 3 macros).
- Exact `IngredientDensity` table entries + per-ingredient source attribution; exact final deny-list contents (starter set in D-15-05); confidence-tier thresholds for match accept / low-confidence / unmatched.
- `NutritionService` internal shape and method signatures; whether the normalized-name→`FoodId` match memo is a separate table or folded into `RecipeNutritionCache`.
- The exact `NutritionInformation` value-object type name/shape for D-15-13.
- Panel layout / placement specifics in `RecipeView` (a `ui-phase` may refine; SC fixes *that* the panel, coverage indicator, per-serving/total, and disclaimer appear — not pixel layout).
- Whether to add component/service tests now vs. defer harness coverage to Phase 16 (recommend service + projector unit tests this phase: ≥20-ingredient conversion suite, flour anchor, unmatched-not-zeroed, stale-on-doc-change, JSON-LD nutrition present/omitted, disclaimer present).

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents (researcher, planner) MUST read these before planning or implementing.**

### Requirements & milestone decisions (authoritative)
- `.planning/REQUIREMENTS.md` §"Nutrition (NUTR)" — **NUTR-01..06**, the locked WHAT + the REQ→Phase-15 traceability rows.
- `.planning/ROADMAP.md` §"Phase 15: Nutrition (Offline CNF — Canadian Nutrient File)" — the **Data source** decision block, goal, and **5 success criteria (SC1–SC5)** including the verbatim disclaimer + flour-anchor + coverage requirements.
- `.planning/PROJECT.md` §"Key Decisions" — the **"v1.4: Phase 15 nutrition data source = Canadian Nutrient File (CNF), not USDA FDC"** row (OGL-Canada licensing, attribution requirement, Conversion Factors, FdcApiKey repurpose note).
- `.planning/STATE.md` §"Accumulated Context" — **Hard Invariants** ("Nutrition never stored in CanonicalDocumentJson … cached in `RecipeNutritionCache`"; "Display-only layers never mutate canonical"; "Zero new NuGet packages"), **Key v1.4 Decisions** (CNF data source, nutrition fully offline, nutrition is post-save enrichment only), **Pitfall Guards P4–P7**, and the **Open Questions** (density source + deny-list — resolved here in D-15-05/08).

### Research (read with the CNF caveat — written pre-CNF, USDA-oriented)
- `.planning/research/PITFALLS.md` §"Pitfall 4/5/6/7" — the nutrition risk set: wrong fuzzy match (→ CNF `FoodId`/description visible, confidence threshold, "--" for unmatched), water-density error (→ CNF factors first + curated density table, flour anchor), missing disclaimer (→ non-dismissable Health Canada line), save-path blocking (→ explicit CTA, post-save enrichment). **Translate USDA/FDC terms → CNF.**
- `.planning/research/SUMMARY.md` §"Nutrition" / §"Recommended Stack" — zero-new-NuGet consensus, two-level cache idea, normalization + per-100 g model. Ignore USDA-specific data-type-filter detail (CNF has one data type).

### Codebase precedents to copy / reuse
- `src/CookBot.Infrastructure/Data/DatabaseSeeder.cs` (`:203` `Path.Combine(contentRootPath, "..", "seeds", "ingredients.json")`, `:210` `ReadAllTextAsync`) — the **seed-load + idempotent-guard precedent** for the CNF seed (D-15-01).
- `seeds/ingredients.json` — the bundled-seed file precedent; `seeds/nutrition/` follows it.
- `src/CookBot.Application/Services/UnitConversionService.cs` (`IUnitConverter`) — **reuse for pure unit math** (vol↔vol via `VolumeToMl`, mass↔mass via `WeightToGrams`); it has **no density / volume→mass** path — that is new and lives in `IngredientDensityProvider` (D-15-09).
- `src/CookBot.Domain/Recipes/IngredientEntry.cs` — the match input shape: `Name`, `Amount (double)`, `Unit (string)`, `Note?`, `Substitutions`, `Extras`. `RecipeDocument.Servings` is the per-serving divisor.
- `src/CookBot.Application/Recipes/JsonLdRecipeProjector.cs` — the **pure static projector**; `Project(doc, absoluteImageUrl)` gains an optional `nutrition?` param (D-15-13). Note the existing "NEVER emit aggregateRating/review" + WhenWritingNull + ordered-dictionary patterns; add `nutrition` as a non-null-only entry. **Do not give it DI or data-service access** (doc-comment invariant).
- `src/CookBot.Web/Components/Pages/RecipeView.razor` — where the nutrition panel + CTA render and where the cached nutrition is read and passed to the projector (`<HeadContent>` JSON-LD already wired in Phase 13). Reuse the Cb atom + design-token system (no MudBlazor).
- `src/CookBot.Application/Services/RecipeService.cs` — the **single owner of `CanonicalDocumentJson` writes**; nutrition must **not** be written here (P7: never block save; nutrition is a separate `NutritionService` + `RecipeNutritionCache`). Hook canonical-doc-change → cache-stale via the existing save path's content hash.
- `src/CookBot.Infrastructure/Data/Configurations/RecipeConfiguration.cs` / `RecipeIngredientConfiguration.cs` — the FK/index config pattern for the new `CnfFood` / `CnfConversionFactor` / `RecipeNutritionCache` table configs + a new migration (`AddNutritionTables`), applied by `DatabaseSeeder.SeedAsync → MigrateAsync`.
- `src/CookBot.Application/DTOs/CookBotSettings.cs` — the clamped-int / nullable-setting precedent. **No `FdcApiKey` added this phase** (D-15-03); the v1.3 `DatabaseBackupRetention` / `MaxPhotosPerRecipe` shapes are the template if any nutrition setting is later needed.

### Phase 13 integration (must not regress)
- `src/CookBot.Application/Recipes/Iso8601DurationFormatter.cs` + the Phase 13 JSON-LD golden tests — adding `nutrition` must not perturb existing field order/output for nutrition-absent recipes (re-baseline the golden snapshot in the same commit; SC5 omit-when-absent).

### Legacy stub — do NOT use
- `src/CookBot.Domain/Entities/Ingredient.cs:14` `NutritionalInfoJson` + `IngredientConfiguration.cs:18` (max 500) + `src/CookBot.Domain/Models/NutritionalInfo.cs` — **vestigial**: declared and EF-mapped but **never read or written** anywhere. CNF foods live in dedicated tables; **do not overload this column** (D-15-18). Leave untouched (removal is out of scope).

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- **`DatabaseSeeder` seed-load pattern** (`:203/:210`): `seeds/nutrition/` + idempotent guard reuses the exact `seeds/ingredients.json` convention — no new seed primitive needed.
- **`UnitConversionService` / `IUnitConverter`**: pure vol↔vol + mass↔mass math reused for unit normalization before density is applied (NUTR-03 "reusing the existing unit converter").
- **`JsonLdRecipeProjector` (pure static)**: extended by one optional param; its WhenWritingNull + ordered-dictionary + omit-when-null machinery already does exactly what SC5 needs.
- **`RecipeView` + Phase 13 `<HeadContent>` JSON-LD**: the consumer surface already renders the projector output server-side; nutrition rides the same path.
- **Cb atoms / design tokens (no MudBlazor)**: the panel, CTA, coverage list, and disclaimer use the existing custom component system.

### Established Patterns
- **`RecipeService` is the single owner of `CanonicalDocumentJson` writes** — nutrition is computed by a separate `NutritionService` into `RecipeNutritionCache`; the canonical doc is never touched (hard invariant; P15).
- **Post-save enrichment, never block save** — mirrors the milestone's firm P7 stance; explicit CTA only (SC1).
- **Bundled-seed-at-startup** — CNF seed loads like the 600-ingredient seed; offline, idempotent, forward-only migration.
- **Verbatim external data** — OGL-Canada (like the canonical-format "store verbatim" discipline) forbids modifying nutrient values; per-serving re-expression is computed at display, not stored back over the source.

### Integration Points
- New `CnfFood` + `CnfConversionFactor` tables (seeded) + `RecipeNutritionCache` table (computed) + migration `AddNutritionTables`, applied at startup.
- New `NutritionService` (Application) + `IngredientDensityProvider` (Application) consuming `IUnitConverter`.
- `JsonLdRecipeProjector.Project` gains `nutrition?`; `RecipeView` reads `RecipeNutritionCache`, passes per-serving nutrition to the projector and renders the panel + CTA + coverage + disclaimer.
- Canonical-doc-change → `RecipeNutritionCache` stale-mark (content hash), surfaced as "recalculate".

</code_context>

<specifics>
## Specific Ideas

- Flour anchor (SC3): "1 cup all-purpose flour" must land at **≈455 kcal** via a CNF Conversion Factor (or curated flour density ≈120–125 g/cup) — the canonical "did we avoid water density?" test. Unit-test ≥20 common ingredients around this.
- Coverage line reads like honesty, not a spec dump: *"Estimated nutrition (per serving) · matched 11 of 13 ingredients · 2 not matched: 'pinch of saffron', 'garnish parsley'."* Unmatched → "--", low-confidence vol→mass → "≈" with the CNF match (description + FoodId) shown on hover/expand.
- Disclaimer is one fixed, non-dismissable line under every panel — verbatim from D-15-17 (also the OGL-Canada attribution).
- Heading: **"Estimated nutrition"**, never "Calories" (SC4).
- JSON-LD `nutrition` is per-serving `NutritionInformation` — appears only when a recipe has a computed cache row (SC5).

</specifics>

<deferred>
## Deferred Ideas

- **USDA FDC online gap-fill fallback** (+ a `CookBotSettings.FdcApiKey` setting + `HttpClient` path) — PROJECT/STATE note it as *optional*. Deferred to keep Phase 15 strictly offline (SC1). Revisit only if CNF coverage proves insufficient in practice.
- **Interactive per-ingredient match correction / override** — read-only match *visibility* (description + FoodId) is in scope; letting the user pick a different CNF food and persist the override is a future polish (would need its own override table).
- **Micronutrients beyond energy + 3 macros** (sodium, fiber, sugar, vitamins) — CNF carries them, but NUTR scope is calories + protein/carbs/fat. Subset the seed now; widen later if requested.
- **Bilingual (FR) nutrition UI / `FoodDescriptionF`** — CNF is bilingual; the app UI is EN. Store EN description for matching/display; FR is a future i18n item.
- **Removing the vestigial `Ingredient.NutritionalInfoJson` column** — unused legacy; a cleanup-migration candidate for a later phase, not this one.
- **Auto-recompute nutrition on every recipe edit** — deliberately not done (stale-mark + recompute-on-CTA preserves the never-block + explicit-action contract).

### Reviewed Todos (not folded)
None — `todo.match-phase 15` → 0 matches.

</deferred>

---

*Phase: 15-nutrition-offline-cnf-canadian-nutrient-file*
*Context gathered: 2026-06-07*
