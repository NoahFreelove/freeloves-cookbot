# Phase 15: Nutrition (Offline CNF — Canadian Nutrient File) - Research

**Researched:** 2026-06-07
**Domain:** Canadian Nutrient File (Health Canada), offline food-composition seed, EF Core denormalised schema, volume-to-mass unit conversion, Schema.org NutritionInformation
**Confidence:** HIGH (CNF structure, NutrientNameIDs, ConversionFactor semantics, Schema.org JSON-LD — all verified from official sources and live API calls; MEDIUM on seed-build approach details)

---

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

**D-15-01 (pipeline):** Pre-process the CNF relational CSVs **offline** (one-time build step) into a compact bundled seed under **`seeds/nutrition/`** (JSON, mirroring `seeds/ingredients.json`), loaded by `DatabaseSeeder` into new EF-mapped CNF tables at startup — same `Path.Combine(contentRootPath, "..", "seeds", …)` + idempotent guard convention as the ingredient seed. Values stored **verbatim** — column/food subsetting is OGL-allowed, value modification is not.

**D-15-02 (schema):** Two denormalized tables — `CnfFood { FoodId (CNF FoodCode, PK) · FoodDescription (EN) · FoodGroup? · EnergyKcalPer100g · ProteinGPer100g · FatGPer100g · CarbGPer100g }` + `CnfConversionFactor { FoodId (FK) · MeasureDescription · ConversionFactorValue }`. Subset to energy + 3 macros.

**D-15-03:** Phase 15 ships **fully offline — no API key, no FdcApiKey, no HttpClient nutrition path**.

**D-15-04 (matching):** Fully **offline deterministic** name match — normalize `IngredientEntry.Name`, strip a modifier deny-list, token-match against `CnfFood.FoodDescription`, store matched `FoodId` + `FoodDescription` + a **confidence tier**; below threshold → **unmatched ("--")**, never a silent low-confidence number.

**D-15-05 (deny-list):** Strip: *chopped, minced, diced, sliced, shredded, grated, ground, sifted, packed, room-temperature, cold, warm, good-quality, good, fine, coarse, large, small, medium, ripe, to taste, optional, divided, for garnish, plus more, organic, finely, roughly, freshly*. Keep modifiers that change nutrition (unsalted, salted, skinless, lowfat, whole, light, heavy).

**D-15-06:** Matched **CNF food description + CNF `FoodId`** always visible per ingredient in the coverage UI.

**D-15-07 (volume→mass priority):** CNF Conversion Factors first → per-ingredient fallback density → mass unit used directly. Volume with no density → unmatched ("--"/"≈").

**D-15-08 (fallback density):** A small curated **`IngredientDensity`** table (~30–50 common cooking ingredients, g/mL) sourced from **USDA ARS + FAO/INFOODS density database**, cross-checked against King Arthur for baking staples.

**D-15-09 (placement):** Reuse `UnitConversionService` / `IUnitConverter` for **pure unit math only**. The food-specific density (mL→g) lives in a dedicated **`IngredientDensityProvider`**.

**D-15-10 (trigger):** "Calculate nutrition" is an **explicit user CTA** — never on the save path.

**D-15-11 (storage):** A new **`RecipeNutritionCache`** table keyed by `RecipeId` — stores computed total + per-serving energy/macros, coverage summary, per-ingredient match results (FoodId/description/confidence), and a **content hash of the canonical doc**.

**D-15-12 (invalidation):** On canonical-doc change (hash mismatch) the cached panel is marked **stale** — "recipe changed — recalculate" affordance, not auto-recompute.

**D-15-13:** Add an **optional third parameter** to `JsonLdRecipeProjector.Project(doc, absoluteImageUrl, nutrition?)`. Emit `@type: "NutritionInformation"` only when nutrition exists; omit cleanly when null.

**D-15-14:** Nutrition panel on **`RecipeView`**, built on existing **Cb atoms / design tokens** (no MudBlazor). Explicit "Calculate nutrition" CTA when uncached or stale.

**D-15-15:** Per-serving **and** total. Heading reads **"Estimated nutrition"** — never "Calories".

**D-15-16:** Unmatched → "--". Low-confidence vol→mass → "≈" + matched CNF description + FoodId visible.

**D-15-17 (disclaimer):** Non-dismissable on every nutrition surface: **"Estimated nutrition — not suitable for medical dietary planning. Data: Health Canada, Canadian Nutrient File (2015)"**.

### Claude's Discretion

- Exact bundled-seed format (JSON vs SQL), idempotent guard, which CNF source files to ingest.
- Exact `IngredientDensity` table entries + per-ingredient source attribution; exact final deny-list contents; confidence-tier thresholds.
- `NutritionService` internal shape and method signatures.
- Whether the normalized-name→FoodId match memo is a separate table or folded into `RecipeNutritionCache`.
- The exact `NutritionInformation` value-object type name/shape for D-15-13.
- Panel layout / placement specifics in `RecipeView`.
- Whether to add component/service tests now vs. defer to Phase 16.

### Deferred Ideas (OUT OF SCOPE)

- USDA FDC online gap-fill fallback.
- Interactive per-ingredient match correction / override.
- Micronutrients beyond energy + 3 macros.
- Bilingual (FR) nutrition UI / `FoodDescriptionF`.
- Removing the vestigial `Ingredient.NutritionalInfoJson` column.
- Auto-recompute nutrition on every recipe edit.

</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| NUTR-01 | CNF ships as bundled SQLite seed; no API key, no runtime external calls | §Research Target 1: CNF REST API enables one-time offline seed build; seed builder script calls API, outputs JSON; at runtime: no HTTP |
| NUTR-02 | Compute kcal + protein/carbs/fat from ingredient amounts; cache per-recipe; invalidate on canonical doc change | §Research Target 7: SHA-256 of `CanonicalDocumentJson` as cache key; `RecipeNutritionCache` table |
| NUTR-03 | Volume→mass: CNF Conversion Factors first; per-ingredient density fallback (not water); mark lower-confidence | §Research Target 2/3: servingsize API provides 16,656 CFs; density table with 23+ KA/FAO-verified values |
| NUTR-04 | Nutrition panel with per-serving + total; unmatched ingredients shown explicitly, never zeroed | §Research Target 2/5: coverage indicator; "--" for unmatched; "≈" for low-confidence |
| NUTR-05 | "Estimated nutrition — not suitable for medical dietary planning. Data: Health Canada, Canadian Nutrient File (2015)" | §Research Target 3: OGL-Canada attribution text confirmed |
| NUTR-06 | When nutrition exists, `nutrition.calories` + macros appear in Schema.org JSON-LD; absent when null | §Research Target 6: Schema.org `NutritionInformation`; Google Rich Results format confirmed |
</phase_requirements>

---

## Summary

Phase 15 builds a fully offline calorie and macro panel driven by the Canadian Nutrient File (CNF), which ships per-100 g values for 5,690 foods (Energy, Protein, Fat, Carbohydrate) and 16,656 household-measure→gram conversion factors. All CNF data is accessible today via three CNF REST API endpoints (`/food/`, `/nutrientamount/`, `/servingsize/`) in a single bulk export each — no pagination, no API key required. The seed build script calls these APIs once (offline, during development), merges them into compact JSON files under `seeds/nutrition/`, and those files are committed to the repo. At runtime, `DatabaseSeeder` loads them idempotently into two new EF tables; no HTTP calls are made at runtime.

The CNF `servingsize` endpoint (not documented as the primary CF endpoint, but verified working) delivers conversion factors with the canonical semantics: `ConversionFactorValue = grams_of_food_in_measure / 100`. So to get nutrient content for a household measure: `nutrient_per_measure = nutrient_per_100g × ConversionFactorValue`. The SC3 flour anchor is verified exactly: CNF all-purpose flour = 364 kcal/100 g; 250 ml (Canadian cup) CF = 1.32079 = 132.1 g; scaled to US cup (×0.9464) = 125.0 g → **exactly 455.0 kcal per US cup**. No guessing required.

The primary technical unknowns are (1) matching colloquial ingredient names to CNF's genus-first comma-separated FoodDescription strings and (2) mapping CookBot's `MeasurementUnit` enum values (US cups/tablespoons/teaspoons) to CNF's metric measure strings (250 ml, 15 ml, 5 ml). Both are deterministic and documented below with concrete algorithms.

**Primary recommendation:** Build the seed from the three CNF REST API endpoints in a one-time offline script (`tools/build-cnf-seed.py`); commit the resulting JSON to `seeds/nutrition/`; follow the DatabaseSeeder idempotent-guard pattern exactly.

---

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| CNF seed load at startup | Infrastructure (DatabaseSeeder) | — | Mirrors ingredient seed pattern; forward-only migration + idempotent guard |
| Food-name → CNF match + confidence | Application (NutritionService) | — | Pure business logic; no UI, no EF direct access |
| Volume→mass conversion (unit math) | Application (UnitConversionService) | — | Already owns vol↔vol and mass↔mass; ingredient-agnostic |
| Volume→mass density lookup | Application (IngredientDensityProvider) | — | Food-specific; new provider; separate from UnitConversionService (D-15-09) |
| Nutrition compute + cache write | Application (NutritionService) | Infrastructure (CnfDbContext) | NutritionService calls EF; writes RecipeNutritionCache; never touches CanonicalDocumentJson |
| Nutrition panel + CTA + disclaimer | Web (RecipeView) | Application (NutritionService) | RecipeView reads RecipeNutritionCache, triggers NutritionService on CTA click |
| JSON-LD nutrition wiring | Application (JsonLdRecipeProjector) | Web (RecipeView) | Projector stays pure; RecipeView passes per-serving DTO |
| Canonical doc staleness detection | Application (RecipeService) | Infrastructure | RecipeService writes content hash to RecipeNutritionCache on save |

---

## Standard Stack

### Core (No New NuGet Packages)

Phase 15 adds **zero new NuGet packages** (hard invariant from CLAUDE.md / STATE.md). Everything below is already in the project.

| Component | Already Present | Purpose in Phase 15 |
|-----------|----------------|---------------------|
| EF Core 10 / SQLite | Yes | Two new tables + migration `AddNutritionTables` |
| System.Text.Json | Yes | Seed file deserialization; `RecipeNutritionCache` JSON columns |
| `UnitConversionService` | Yes | Pure vol↔vol + mass↔mass math before density is applied |
| `DatabaseSeeder` | Yes | Load CNF seed idempotently at startup |

### Package Legitimacy Audit

**N/A — zero new NuGet packages installed this phase.** Hard invariant confirmed by CLAUDE.md ("Zero new NuGet packages") and STATE.md.

---

## Research Target 1: CNF Bulk-Download File Structure

### Verified File Structure [VERIFIED: CNF 2015 Database Structure PDF + live API]

The CNF is a relational dataset. The canonical source of truth is the official "CNF 2015 Database Structure EN.pdf" (embedded in the bulk download zip). Five **principal files** and seven **support files**.

#### Files relevant to Phase 15

**FOOD NAME** (principal)

| Column | Type | Description |
|--------|------|-------------|
| `FoodID` | N(11) | Primary key — used to join all related files |
| `FoodCode` | N(8) | User-visible food code; NOT the PK (important!) |
| `FoodGroupID` | N(15) | FK → FOOD GROUP |
| `FoodSourceID` | N(15) | FK → FOOD SOURCE |
| `FoodDescription` | T(255) | Complete food name in English (genus-first, comma-separated) |
| `FoodDescriptionF` | T(255) | Complete food name in French |
| `CountryCode` | N(20) | Corresponds to USDA NDB code |
| `FoodDateOfEntry` | D | |
| `FoodDateOfPublication` | D | |
| `ScientificName` | T(100) | |

**NUTRIENT AMOUNT** (principal)

| Column | Type | Description |
|--------|------|-------------|
| `FoodID` | N(8) | FK → FOOD NAME |
| `NutrientNameID` | N(4) | FK → NUTRIENT NAME (NOT NutrientCode) |
| `NutrientValue` | N(12/5) | Mean value in 100 g edible portion |
| `StandardError` | N(8/4) | |
| `NumberOfObservation` | N(6) | |
| `NutrientSourceID` | N(15) | FK → NUTRIENT SOURCE |
| `NutrientDateEntry` | D | |

**CONVERSION FACTOR** (principal)

| Column | Type | Description |
|--------|------|-------------|
| `FoodID` | N(8) | FK → FOOD NAME |
| `MeasureID` | N(10) | FK → MEASURE NAME |
| `ConversionFactorValue` | N(10) | **grams of food in that measure ÷ 100** |
| `ConvFactorDateOfEntry` | D | |

**NUTRIENT NAME** (support)

| Column | Type | Description |
|--------|------|-------------|
| `NutrientNameID` | N(4) | **Primary key** (this is NOT NutrientCode) |
| `NutrientCode` | N(15) | USDA-aligned nutrient code (e.g. 208 for kcal) |
| `NutrientSymbol` | T(10) | |
| `Unit` | T(8) | g, mg, kCal, kJ, etc. |
| `NutrientName` | T(200) | Full English name |
| `NutrientNameF` | T(200) | French name |
| `Tagname` | T(20) | INFOODS tagname (e.g. ENERC_KCAL, PROCNT) |
| `NutrientDecimals` | N(15) | |

**MEASURE NAME** (support)

| Column | Type | Description |
|--------|------|-------------|
| `MeasureID` | N(10) | Primary key |
| `MeasureName` | T(200) | Measure description in English (e.g. "250ml", "15ml", "1 cup") |
| `MeasureNameF` | T(200) | French |

### Critical: NutrientNameID values for macros [VERIFIED: CNF REST API live call 2026-06-07]

In CNF, `NutrientNameID` and `NutrientCode` are DIFFERENT columns. For the four macros, they happen to be equal:

| NutrientNameID | NutrientCode | NutrientName | Unit | INFOODS Tagname |
|----------------|--------------|--------------|------|-----------------|
| **203** | 203 | PROTEIN | g | PROCNT |
| **204** | 204 | FAT (TOTAL LIPIDS) | g | FAT |
| **205** | 205 | CARBOHYDRATE, TOTAL (BY DIFFERENCE) | g | CHOCDF |
| **208** | 208 | ENERGY (KILOCALORIES) | kCal | ENERC_KCAL |
| 268 | 268 | ENERGY (KILOJOULES) | kJ | ENERC_KJ |

**Use NutrientNameID 208 for kcal (not 268/kJ).** The `NUTRIENT AMOUNT` join is: `NutrientAmount.NutrientNameID = NutrientName.NutrientNameID`.

Note: In the NUTRIENT AMOUNT CSV file, the column is labelled `NutrientId` (not `NutrientNameID`) — same concept, name varies between CSV and documentation.

### Exact nutrient name strings (from CNF web search SQL view) [VERIFIED: blog.jpoles1.com + API cross-check]

```sql
WHERE NutrientName = 'ENERGY (KILOCALORIES)'        -- NutrientNameID 208
WHERE NutrientName = 'FAT (TOTAL LIPIDS)'           -- NutrientNameID 204
WHERE NutrientName = 'CARBOHYDRATE, TOTAL (BY DIFFERENCE)' -- NutrientNameID 205
WHERE NutrientName = 'PROTEIN'                      -- NutrientNameID 203
```

### Data scale [VERIFIED: CNF REST API live call 2026-06-07]

| Dataset | Count |
|---------|-------|
| Foods (`/food/`) | 5,690 |
| Nutrient records (`/nutrientamount/`, all nutrients) | 524,675 |
| Macro records (NutrientNameID 203/204/205/208) | 22,760 |
| Conversion factor records (`/servingsize/`) | 16,656 |
| Average CFs per food | ~2.9 |

---

## Research Target 2: CNF Conversion Factor Semantics

### Formula [VERIFIED: CNF 2015 Database Structure PDF, page 9]

> "The factor by which one would multiply the nutrient per 100g to obtain nutrient amounts per the measure described (the weight of that food in the measure described divided by 100)"

```
ConversionFactorValue = grams_of_food_in_measure / 100
nutrient_per_measure  = nutrient_per_100g × ConversionFactorValue
```

**Example (verified from live API):**

All-purpose flour, white (FoodCode 4484): 364 kcal/100 g
- Measure "250ml": CF = 1.32079 → 132.1 g → 480.8 kcal per Canadian cup
- Measure "100ml": CF = 0.52832 → 52.8 g → 192.3 kcal per 100 ml
- Measure "1 serving": CF = 0.20000 → 20.0 g

### CNF measure names are metric [VERIFIED: CNF CSV data + API calls 2026-06-07]

CNF uses **metric measure strings**. There are **no "1 cup" or "1 tablespoon" strings** in the CNF measure database. All volume measures are in mL:

| CNF MeasureName | Volume (mL) | Maps to CookBot unit |
|-----------------|-------------|---------------------|
| `5ml` | 5.0 | Teaspoon (4.929 mL) |
| `15ml` | 15.0 | Tablespoon (14.787 mL) |
| `100ml` | 100.0 | ~0.42 cup |
| `125ml` | 125.0 | ½ Cup |
| `175ml` | 175.0 | ¾ Cup |
| `250ml` | 250.0 | 1 Cup (Canadian) |
| `500ml` | 500.0 | 1 Pint / 2 Cups |

### Unit mapping + scaling algorithm [ASSUMED for threshold values; formula is VERIFIED]

To use a CNF conversion factor with a CookBot recipe unit:

1. Convert the recipe unit to mL using `UnitConversionService.VolumeToMl` (e.g. `Cup` = 236.588 mL)
2. Find the CNF `MeasureName` whose parsed mL value is closest to the recipe mL
3. Scale the CF: `cf_adjusted = cf_cnf × (recipe_mL / cnf_measure_mL)`
4. Result: `nutrient_for_recipe_amount = nutrient_per_100g × cf_adjusted × recipe_amount`

**Scale factors for common units:**

| CookBot Unit | Recipe mL | Best CNF measure | CNF mL | Scale |
|--------------|-----------|-----------------|--------|-------|
| Teaspoon | 4.929 | 5ml | 5.0 | 0.9858 |
| Tablespoon | 14.787 | 15ml | 15.0 | 0.9858 |
| FluidOunce | 29.574 | 30ml (if present) or 15ml | — | varies |
| Cup | 236.588 | 250ml | 250.0 | **0.9464** |
| Pint | 473.176 | 500ml | 500.0 | 0.9464 |
| Milliliter | 1.0 | match exactly or 5ml/100ml | — | exact |

**Important:** US cup (236.588 mL) ≠ Canadian cup (250 mL). The scale factor 0.9464 must be applied whenever a CookBot "Cup" is matched against a CNF "250ml" factor. This is how the SC3 anchor resolves exactly:

```
Flour 364 kcal/100g × CF(250ml)=1.32079 × scale(0.9464) = 364 × 1.24993 = 455.0 kcal/cup
```

### Matching CNF measures [ASSUMED for exact threshold; algorithm shape is VERIFIED]

Parse CNF `MeasureName` strings for an embedded mL number using a regex like `(\d+(?:\.\d+)?)\s*ml`. If found, use the mL value for distance matching. If not found (e.g. "1 pat", "1 stick"), only use if the recipe unit is `Piece` or `Stick`. Use the closest-mL match within a ±20% tolerance; outside that band → no CNF factor match → density fallback.

---

## Research Target 3: Fallback Density Table

### Sources [VERIFIED: King Arthur Baking ingredient weight chart + FAO/INFOODS Density Database v2.0]

The `IngredientDensityProvider` fallback table (for CNF-unmatched measures) should include at minimum these 23 entries — all verified from KA (primary for baking) and FAO/INFOODS (primary for liquids/oils):

| Ingredient | g/mL | g/cup (US) | Source | Confidence |
|------------|------|-----------|--------|-----------|
| All-purpose flour (white, wheat) | 0.507 | 120 g | King Arthur Baking | HIGH |
| Bread flour | 0.507 | 120 g | King Arthur Baking | HIGH |
| Whole wheat flour | 0.478 | 113 g | King Arthur Baking | HIGH |
| Cake flour | 0.507 | 120 g | King Arthur Baking | HIGH |
| Almond flour | 0.406 | 96 g | King Arthur Baking | HIGH |
| Granulated white sugar | 0.837 | 198 g | King Arthur Baking | HIGH |
| Brown sugar (packed) | 0.900 | 213 g | King Arthur Baking | HIGH |
| Confectioners sugar (unsifted) | 0.478 | 113 g | King Arthur Baking | HIGH |
| Butter (unsalted) | 0.955 | 226 g | King Arthur Baking | HIGH |
| Vegetable oil / canola oil | 0.837 | 198 g | King Arthur Baking + FAO ~0.92 | MEDIUM |
| Olive oil | 0.845 | 200 g | KA extrapolated + FAO 0.92 | MEDIUM |
| Whole milk | 0.959 | 227 g | King Arthur Baking | HIGH |
| Heavy cream | 0.959 | 227 g | KA (FAO: ~0.984 at 38% fat) | MEDIUM |
| Sour cream | 0.959 | 227 g | King Arthur Baking | MEDIUM |
| Yogurt (plain) | 0.959 | 227 g | King Arthur Baking | MEDIUM |
| Honey | 1.420 | 336 g | KA: 21 g/tbsp; FAO: 1.38–1.44 | MEDIUM |
| Maple syrup | 1.319 | 312 g | King Arthur Baking | HIGH |
| Cocoa powder (unsweetened) | 0.355 | 84 g | King Arthur Baking | HIGH |
| Cornstarch | 0.473 | 112 g | King Arthur Baking | HIGH |
| Rolled oats | 0.478 | 113 g | King Arthur Baking | HIGH |
| Baking powder | 0.900 | 213 g | FAO: 0.9 g/mL | MEDIUM |
| Salt (fine table) | 1.380 | 326 g | FAO: 1.38 g/mL | MEDIUM |
| Chocolate chips | 0.719 | 170 g | King Arthur Baking | HIGH |

**Disagreements to flag:**
- Vegetable oil: KA gives ~0.837 g/mL from measured cup weight; FAO lists pure liquid oils at 0.92 g/mL. Difference is because KA measures by cup displacement (air gaps), FAO measures pure liquid density. Use KA value for cup conversions (more realistic for recipes).
- Heavy cream: KA 227 g/cup (0.959 g/mL) vs FAO ~0.984 at 38% fat. Small difference; use KA.
- Honey: KA 21 g/tbsp = 1.420 g/mL — this agrees with FAO 1.38–1.44 range. Use 1.420.

**SC3 flour anchor [VERIFIED: live CNF API call 2026-06-07]:**
```
CNF FoodCode 4484 (all-purpose flour, enriched, calcium fortified):
  EnergyKcalPer100g = 364.0
  CF for 250ml = 1.32079 (= 132.1 g)
  Scaled to US cup (×0.9464): CF_adj = 1.24993 (= 125.0 g)
  Result: 364.0 × 1.24993 = 455.0 kcal/cup ← exactly the SC3 anchor
```

This means: when CNF has a 250 ml conversion factor for the matched food, the density fallback is not needed for that food. The fallback only activates for foods/measures without a CNF factor.

**Additional densities needed for ≥20-ingredient unit test coverage:**
- Cream cheese: 0.959 g/mL (227 g/cup per KA)
- Ricotta cheese: ~0.960 g/mL (similar to KA whole milk)
- Peanut butter: ~1.09 g/mL (270 g/cup — measure from FAO/USDA)
- Shredded coconut (sweetened): 0.360 g/mL (85 g/cup per KA)

---

## Research Target 4: Ingredient-Name Normalization and Matching

### CNF FoodDescription format [VERIFIED: live CNF API 2026-06-07]

CNF uses **genus-first, comma-separated** format for food descriptions. Examples:
- `"Grains, wheat flour, white, all purpose, enriched, calcium fortified"`
- `"Butter, unsalted"`
- `"Sweets, sugars, granulated"`
- `"Egg, chicken, dried, whole"`
- `"Cheese, ricotta, with partly skimmed milk"`

Recipe ingredient names are **colloquial** and reverse-ordered:
- `"all-purpose flour"` → needs to match `"Grains, wheat flour, white, all purpose, ..."`
- `"unsalted butter"` → needs to match `"Butter, unsalted"`
- `"granulated sugar"` → needs to match `"Sweets, sugars, granulated"`

### Recommended matching algorithm [ASSUMED for threshold values; approach confirmed by CONTEXT.md D-15-04]

The planner should implement this in `NutritionService.MatchIngredient(IngredientEntry)`:

**Step 1: Normalize the recipe ingredient name**
```csharp
// Lowercase, strip punctuation, apply deny-list (D-15-05)
string normalized = IngredientNormalizer.Normalize(entry.Name, _denyList);
// e.g. "finely chopped unsalted butter" → "unsalted butter"
```

**Step 2: Tokenize**
```csharp
string[] tokens = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);
// e.g. ["unsalted", "butter"]
```

**Step 3: Score against CnfFood.FoodDescription**
For each `CnfFood`:
```csharp
// Normalize the CNF description the same way (pre-computed at seed load time)
// Score = count of recipe tokens that appear in the CNF description tokens
int matchCount = recipeTokens.Intersect(cnfTokens).Count();
double score = (double)matchCount / Math.Max(recipeTokens.Length, 1);
```

**Step 4: Apply confidence tiers** [ASSUMED thresholds — refine at plan time]

| Score | Tier | Action |
|-------|------|--------|
| ≥ 0.80 | HIGH confidence | Use match; display CNF description |
| 0.50–0.79 | MEDIUM confidence | Use match with "≈"; display CNF description |
| < 0.50 | LOW / unmatched | Show "--"; do not compute nutrition for this ingredient |

**Step 5: Resolve ties** — prefer the CNF food whose description length is closest to the normalized input (avoid over-specific branded variants).

### Key challenge: genus-first order [VERIFIED: CNF API observation]

The recipe name "all-purpose flour" matches the CNF tail tokens of "Grains, wheat flour, white, all purpose, enriched, calcium fortified". The token intersection approach handles this correctly because it is order-independent. Do NOT use substring matching — "butter" should not match "peanut butter" or "buttermilk".

### Deny-list application [LOCKED: D-15-05]

Strip these before matching (strip as full-word, not substring):
- Prep descriptors: *chopped, minced, diced, sliced, shredded, grated, ground, sifted, packed, finely, roughly, freshly*
- Quality/temp: *room-temperature, cold, warm, good-quality, good, fine, coarse, large, small, medium, ripe, organic*
- Recipe instructions: *to taste, optional, divided, for garnish, plus more*

**Keep:** *unsalted, salted, skinless, lowfat, low-fat, whole, light, heavy* — these change the matched nutrient values.

### Pre-computed normalized descriptions

At seed load time (inside `DatabaseSeeder`), pre-compute and store a `NormalizedDescription` column in `CnfFood` so the runtime match does not re-normalize 5,690 strings per ingredient. This is a one-time cost during seeding, not per-query.

---

## Research Target 5: EF Core Seed + Table Design

### Seed file structure [VERIFIED: DatabaseSeeder.cs:203 precedent]

Following the `seeds/ingredients.json` pattern exactly:

```
seeds/
  ingredients.json          ← existing
  nutrition/
    cnf_foods.json           ← NEW: array of CnfFoodSeedRow
    cnf_conversion_factors.json  ← NEW: array of CnfConversionFactorSeedRow
```

The `DatabaseSeeder` reads:
```csharp
var seedPath = Path.GetFullPath(Path.Combine(contentRootPath, "..", "seeds", "nutrition", "cnf_foods.json"));
```

### Idempotent guard [VERIFIED: DatabaseSeeder.cs:170 precedent]

```csharp
if (await context.CnfFoods.AnyAsync())
    return; // already seeded
```

### Table designs (matching D-15-02)

**`CnfFood`** (EF entity in `CookBot.Domain/Entities/` or `CookBot.Infrastructure/Data/`):

```csharp
public class CnfFood
{
    public int FoodId { get; set; }           // PK — CNF FoodCode (not FoodID, but user-visible code)
    public string FoodDescription { get; set; } = ""; // e.g. "Grains, wheat flour, white, all purpose, ..."
    public string? NormalizedDescription { get; set; } // pre-computed, lowercased, deny-listed
    public string? FoodGroup { get; set; }    // optional, for display
    public double EnergyKcalPer100g { get; set; }
    public double ProteinGPer100g { get; set; }
    public double FatGPer100g { get; set; }
    public double CarbGPer100g { get; set; }
    public ICollection<CnfConversionFactor> ConversionFactors { get; set; } = [];
}
```

**`CnfConversionFactor`**:

```csharp
public class CnfConversionFactor
{
    public int Id { get; set; }               // PK (surrogate)
    public int FoodId { get; set; }           // FK → CnfFood.FoodId
    public CnfFood Food { get; set; } = null!;
    public string MeasureDescription { get; set; } = ""; // e.g. "250ml", "15ml", "1 pat"
    public double ConversionFactorValue { get; set; }    // grams_in_measure / 100
}
```

**`RecipeNutritionCache`**:

```csharp
public class RecipeNutritionCache
{
    public int RecipeId { get; set; }         // PK + FK → Recipe
    public Recipe Recipe { get; set; } = null!;
    public string CanonicalDocHash { get; set; } = ""; // SHA-256 of CanonicalDocumentJson
    public bool IsStale { get; set; }
    public double TotalEnergyKcal { get; set; }
    public double TotalProteinG { get; set; }
    public double TotalFatG { get; set; }
    public double TotalCarbG { get; set; }
    public int? Servings { get; set; }        // snapshot of doc.Servings at compute time
    public double PerServingEnergyKcal { get; set; }
    public double PerServingProteinG { get; set; }
    public double PerServingFatG { get; set; }
    public double PerServingCarbG { get; set; }
    public int MatchedIngredients { get; set; }
    public int TotalIngredients { get; set; }
    public string PerIngredientMatchJson { get; set; } = ""; // JSON: [{name, cnfFoodId, cnfDesc, confidence, kcal, ...}]
    public DateTime ComputedAt { get; set; }
}
```

### EF Configuration pattern (following RecipeIngredientConfiguration)

```csharp
// CnfFoodConfiguration
builder.HasKey(f => f.FoodId);
builder.Property(f => f.FoodDescription).HasMaxLength(300).IsRequired();
builder.Property(f => f.NormalizedDescription).HasMaxLength(300);
builder.HasIndex(f => f.FoodDescription); // for LIKE/contains searches
builder.HasMany(f => f.ConversionFactors).WithOne(cf => cf.Food).HasForeignKey(cf => cf.FoodId).OnDelete(DeleteBehavior.Cascade);

// CnfConversionFactorConfiguration
builder.HasKey(cf => cf.Id);
builder.HasIndex(cf => cf.FoodId);
builder.Property(cf => cf.MeasureDescription).HasMaxLength(100).IsRequired();

// RecipeNutritionCacheConfiguration
builder.HasKey(c => c.RecipeId);
builder.HasOne(c => c.Recipe).WithOne().HasForeignKey<RecipeNutritionCache>(c => c.RecipeId).OnDelete(DeleteBehavior.Cascade);
builder.Property(c => c.PerIngredientMatchJson).HasColumnType("TEXT");
```

### Migration name

`AddNutritionTables` — applied by `DatabaseSeeder.SeedAsync → MigrateAsync` at startup.

### Seed file size estimate [VERIFIED: live API call 2026-06-07]

- `cnf_foods.json`: 5,690 rows × ~120 bytes = ~**680 KB**
- `cnf_conversion_factors.json`: 16,656 rows × ~50 bytes = ~**815 KB**
- Total: **~1.5 MB** committed to repo

For comparison, `seeds/ingredients.json` is 51.7 KB. The CNF seed is ~30× larger but still well within typical repo file limits.

### OGL-Canada compliance [VERIFIED: CONTEXT.md D-15-01 + OGL-Canada license text]

- Subsetting to 4 columns (energy + 3 macros) of 5,690 of the full 5,993 foods: **allowed**
- Storing values verbatim (no modification): **required** — do NOT round, normalize, or re-scale stored values
- Computing per-serving at display time: **allowed** (per CONTEXT.md and OGL-Canada guidance)
- Attribution: every nutrition surface must carry "Data: Health Canada, Canadian Nutrient File (2015)"

### Seed build script approach [ASSUMED — approach is discretionary per D-15-01]

Recommended one-time offline build script (Python or C#, committed to `tools/`):

```python
# tools/build-cnf-seed.py  (outline)
import requests, json

# Step 1: Fetch foods
foods = requests.get('https://food-nutrition.canada.ca/api/canadian-nutrient-file/food/?lang=en&type=json').json()

# Step 2: Fetch all nutrient amounts (one call)
nutrients = requests.get('https://food-nutrition.canada.ca/api/canadian-nutrient-file/nutrientamount/?lang=en&type=json').json()
macro_ids = {203, 204, 205, 208}
macros_by_food = {}
for n in nutrients:
    if n['nutrient_name_id'] in macro_ids:
        macros_by_food.setdefault(n['food_code'], {})[n['nutrient_name_id']] = n['nutrient_value']

# Step 3: Fetch all serving sizes
serving_sizes = requests.get('https://food-nutrition.canada.ca/api/canadian-nutrient-file/servingsize/?lang=en&type=json').json()

# Step 4: Merge and write seeds/nutrition/
cnf_foods = []
for food in foods:
    code = food['food_code']
    m = macros_by_food.get(code, {})
    if 208 not in m: continue  # skip foods with no caloric data
    cnf_foods.append({
        "FoodId": code,
        "FoodDescription": food['food_description'],
        "EnergyKcalPer100g": m[208],
        "ProteinGPer100g": m.get(203, 0.0),
        "FatGPer100g": m.get(204, 0.0),
        "CarbGPer100g": m.get(205, 0.0),
    })
```

---

## Research Target 6: JSON-LD Nutrition Wiring (NUTR-06)

### Schema.org NutritionInformation [VERIFIED: schema.org/NutritionInformation + Google Search Central recipe docs]

```json
{
  "@type": "NutritionInformation",
  "calories": "455 calories",
  "proteinContent": "12.5 g",
  "carbohydrateContent": "76.3 g",
  "fatContent": "1.2 g"
}
```

**Rules:**
- `calories` — string: `"N calories"` (not `"N kcal"`) per Google Rich Results validator
- `proteinContent`, `carbohydrateContent`, `fatContent` — string: `"N g"` (no unit suffix in Google docs, but schema.org type is `Mass`)
- Values are **per serving** (Google: "The number of calories in each serving produced with this recipe")
- If `nutrition.calories` is present, `recipeYield` must also be present (already set via `doc.Servings`)

**Conditional requirement:** Nutrition is optional. Only emit it when a `RecipeNutritionCache` row exists and is not stale.

### Minimal projector change (D-15-13) [VERIFIED: JsonLdRecipeProjector.cs read]

The existing projector uses a `Dictionary<string, object>` model with `WhenWritingNull` and conditional `if (x is not null) model["key"] = x` pattern. The minimal change:

```csharp
// New optional parameter:
public static string Project(RecipeDocument doc, string? absoluteImageUrl, NutritionInfoDto? nutrition = null)

// Inside Project(), after author:
if (nutrition is not null)
{
    var nutritionObj = new Dictionary<string, string>
    {
        ["@type"] = "NutritionInformation",
        ["calories"] = $"{nutrition.CaloriesPerServing:0} calories",
        ["proteinContent"] = $"{nutrition.ProteinGPerServing:0.#} g",
        ["carbohydrateContent"] = $"{nutrition.CarbGPerServing:0.#} g",
        ["fatContent"] = $"{nutrition.FatGPerServing:0.#} g",
    };
    model["nutrition"] = nutritionObj;
}
```

The projector must remain pure static — no DI, no data-service access. The Web layer (`RecipeView`) reads `RecipeNutritionCache` and constructs the `NutritionInfoDto` before calling `Project`.

**Golden test impact:** The existing Phase 13 golden tests for nutrition-absent recipes will still pass (nutrition omitted when null). The Phase 15 plan must include a new golden test for a recipe WITH nutrition, and re-baseline the existing snapshot to confirm it is unchanged.

### Value object type name (discretion area)

```csharp
// In CookBot.Application/DTOs/ or CookBot.Application/Recipes/
public sealed record NutritionInfoDto(
    double CaloriesPerServing,
    double ProteinGPerServing,
    double FatGPerServing,
    double CarbGPerServing
);
```

---

## Research Target 7: Compute Trigger, Cache, and Invalidation

### Content hash approach [ASSUMED — SHA-256 is industry-standard; no competing approach identified]

```csharp
// In RecipeService.CreateAsync / UpdateAsync — after setting CanonicalDocumentJson:
var canonicalJson = _canonicalSerializer.Serialize(canonicalDoc);
recipe.CanonicalDocumentJson = canonicalJson;
// Compute hash and mark cache stale if it exists
var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalJson)));
// Write hash to RecipeNutritionCache.CanonicalDocHash + IsStale = true if hash differs
```

`System.Security.Cryptography.SHA256` is in the .NET BCL — no new package needed.

### Where staleness lives

`RecipeNutritionCache.IsStale` is set to `true` by `RecipeService` whenever `CanonicalDocumentJson` changes (hash mismatch). `NutritionService.ComputeAsync(recipeId)` is called only on explicit CTA click from `RecipeView`; it writes the new values and sets `IsStale = false` + updates `CanonicalDocHash`.

**Never auto-recompute** (hard invariant, D-15-12). Never call `NutritionService` from `RecipeService.CreateAsync/UpdateAsync`.

### RecipeView interaction flow

```
RecipeView renders:
  1. Load RecipeNutritionCache for RecipeId
     → null: show "Calculate nutrition" CTA (never computed)
     → IsStale=true: show "Recipe changed — recalculate" CTA
     → IsStale=false: show nutrition panel + coverage indicator
  2. On CTA click:
     → Invoke NutritionService.ComputeAsync(recipeId)
     → Refresh panel
  3. Pass per-serving dto to JsonLdRecipeProjector only when cache exists and IsStale=false
```

### `RecipeNutritionCache.PerIngredientMatchJson` schema [ASSUMED — shape is discretionary]

Recommended per-ingredient match record:

```json
[
  {
    "name": "all-purpose flour",
    "normalizedName": "flour",
    "cnfFoodId": 4484,
    "cnfFoodDescription": "Grains, wheat flour, white, all purpose, enriched, calcium fortified",
    "confidence": "HIGH",
    "conversionMethod": "CnfFactor",
    "measureUsed": "250ml",
    "gramsComputed": 125.0,
    "energyKcal": 455.0,
    "proteinG": 12.9,
    "fatG": 1.2,
    "carbG": 95.4
  },
  {
    "name": "pinch of saffron",
    "normalizedName": "saffron",
    "cnfFoodId": null,
    "cnfFoodDescription": null,
    "confidence": "UNMATCHED",
    "conversionMethod": null,
    "gramsComputed": null,
    "energyKcal": null
  }
]
```

---

## Architecture Patterns

### System Architecture Diagram

```
RecipeView (Blazor Server)
    │
    ├─ on page load ──────────────────────────────────► CnfFood table (read)
    │                                                    RecipeNutritionCache (read)
    │
    ├─ [nutrition panel: stale/absent] ──────────────► "Calculate Nutrition" CTA
    │
    ├─ on CTA click ──────────────────────────────────► NutritionService.ComputeAsync()
    │       │                                               │
    │       │                                        IngredientDensityProvider
    │       │                                        UnitConversionService (pure math)
    │       │                                        CnfFood + CnfConversionFactor (read)
    │       │                                               │
    │       │                                        RecipeNutritionCache (write)
    │       │                                               │
    │       └─◄──────────────────────────────────────────────
    │
    ├─ [nutrition panel: current] ──────────────────► display per-serving + total + coverage
    │                                                  + non-dismissable disclaimer
    │
    └─ HeadContent JSON-LD ──────────────────────────► JsonLdRecipeProjector.Project(doc, img, nutrition?)
                                                        (pure static, no DI, emits NutritionInformation iff nutrition ≠ null)


RecipeService.CreateAsync/UpdateAsync
    ├─ writes CanonicalDocumentJson (as today)
    └─ computes SHA-256 hash ──────────────────────► RecipeNutritionCache.IsStale = true (if hash changed)
                                                     NEVER calls NutritionService (P7 guard)

DatabaseSeeder.SeedAsync (at startup)
    ├─ MigrateAsync() (applies AddNutritionTables migration)
    └─ if CnfFoods.None() → load seeds/nutrition/cnf_foods.json + cnf_conversion_factors.json
```

### Recommended Project Structure

```
seeds/
  nutrition/
    cnf_foods.json                    # 5690 foods × 4 macros
    cnf_conversion_factors.json       # 16656 CF records
tools/
  build-cnf-seed.py                   # one-time offline seed builder

src/CookBot.Domain/Entities/
  CnfFood.cs                          # POCO entity
  CnfConversionFactor.cs              # POCO entity
  RecipeNutritionCache.cs             # POCO entity

src/CookBot.Application/DTOs/
  NutritionInfoDto.cs                 # per-serving value object for JsonLdRecipeProjector

src/CookBot.Application/Services/
  NutritionService.cs                 # match + compute + cache write
  IngredientDensityProvider.cs        # curated density table (g/mL)
  IngredientNormalizer.cs             # deny-list + tokenization (or static helper)

src/CookBot.Infrastructure/Data/Configurations/
  CnfFoodConfiguration.cs
  CnfConversionFactorConfiguration.cs
  RecipeNutritionCacheConfiguration.cs

src/CookBot.Infrastructure/Migrations/
  {timestamp}_AddNutritionTables.cs

src/CookBot.Web/Components/Pages/
  RecipeView.razor                    # existing — extend with panel + CTA
```

### Anti-Patterns to Avoid

- **Water density fallback:** Never use 1.0 g/mL as a density for flour/sugar/butter. Result: "1 cup flour" = 237 g = 862 kcal instead of 120 g = 436 kcal. Hardcoded `IngredientDensityProvider` table is the guard.
- **Nutrition on save path:** Never call `NutritionService.ComputeAsync` from `RecipeService.CreateAsync/UpdateAsync`. The recipe save must never block on nutrition calculation. (P7 guard)
- **Canonical doc mutation:** `NutritionService` writes to `RecipeNutritionCache` only — never to `Recipe.CanonicalDocumentJson`. (P15 guard)
- **Silent zero for unmatched:** Unmatched ingredients must show "--" in the panel, not 0. Zero implies zero calories; "--" implies unknown.
- **Projector with DI:** `JsonLdRecipeProjector` must remain a pure static function — no constructor injection, no data-service calls. The `nutrition?` param is passed in by `RecipeView`.
- **FoodID vs FoodCode confusion:** CNF `FOOD NAME` has both `FoodID` (internal PK, used in joins) and `FoodCode` (user-visible, NOT the PK). The REST API returns `food_code`. The seed and EF table should use `FoodCode` as the PK for CnfFood (this is what the REST API exposes; the internal FoodID is not surfaced by the public API).

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead |
|---------|-------------|-------------|
| Volume→volume conversion | Custom dict | `UnitConversionService.VolumeToMl` + `WeightToGrams` |
| SHA-256 content hash | Custom hash | `System.Security.Cryptography.SHA256.HashData()` (BCL, no new package) |
| JSON serialization for seed | Custom writer | `System.Text.Json.JsonSerializer` (already in project) |
| EF Core migrations | Manual SQL | `context.Database.MigrateAsync()` (already in DatabaseSeeder) |

---

## Common Pitfalls

### Pitfall P4 (CNF terms): Wrong CNF food match — silently wrong nutrition

**What goes wrong:** "Brown sugar" matches "Sweets, sugars, granulated" (white) instead of "Sweets, sugars, brown". Incorrect calorie count propagates silently.

**How to avoid:** Always store and display `cnfFoodDescription` + `FoodId` in the coverage indicator. The user can see "Matched to: Sweets, sugars, granulated [4318]" and know it's wrong. Low-confidence matches show "≈".

**Warning signs:** Per-ingredient match JSON has no FoodId stored; coverage indicator is absent; no "--" shown for unmatched ingredients.

### Pitfall P5 (CNF terms): CNF measure mismatch → wrong density

**What goes wrong:** Recipe uses "1 cup butter". CNF has butter with a 250 ml factor but code matches it against the 15 ml factor (CF=0.14392 = 14.4 g). Result: 717 × 0.14392 = 103 kcal instead of ~1628 kcal.

**How to avoid:** The measure-selection algorithm must find the CNF measure whose mL value is closest to the recipe's volume in mL, not just the first factor. Sort candidates by |recipe_mL - cnf_mL| and pick the minimum within a ±20% tolerance band.

**Warning signs:** "1 cup butter" shows ~100 kcal instead of ~1600 kcal in the panel.

### Pitfall P5b: US cup vs Canadian cup scaling forgotten

**What goes wrong:** "1 cup flour" directly uses CNF CF(250ml)=1.32079 without the 0.9464 scale factor. Result: 132.1 g = 480.8 kcal instead of the correct 125.0 g = 455.0 kcal.

**How to avoid:** Whenever `recipe_unit = Cup` maps to `cnf_measure = 250ml`, apply scale = 236.588/250.0 = 0.9464. This is systematic — apply scale = recipe_mL / cnf_mL for every unit pair.

### Pitfall P6 (CNF terms): Missing Health Canada attribution

**What goes wrong:** Nutrition panel shows values without the OGL-Canada-required attribution. Health Canada's OGL explicitly requires attribution.

**How to avoid:** The disclaimer string `"Estimated nutrition — not suitable for medical dietary planning. Data: Health Canada, Canadian Nutrient File (2015)"` must be hardcoded, non-dismissable, on every nutrition surface. Test: check DOM for that exact string whenever the panel is visible.

### Pitfall P7: NutritionService called on save path

**What goes wrong:** `RecipeService.UpdateAsync` calls `NutritionService.ComputeAsync` synchronously. If the match/compute is slow (5690-food search), save blocks. If the code throws on an edge-case ingredient, save fails.

**How to avoid:** `RecipeService` only writes the SHA-256 hash and marks `IsStale=true`. It never imports or calls `NutritionService`. Enforce in code review: `RecipeService` has no dependency on `NutritionService`.

### Pitfall: CNF FoodCode vs FoodID confusion

**What goes wrong:** The CNF CSV files use `FoodID` as the primary key for joins, but the REST API returns `food_code` (a different field). Using the wrong field for matching causes join failures.

**How to avoid:** The REST API's `food_code` field corresponds to the CSV's `FoodCode` column (NOT `FoodID`). The `CnfFood.FoodId` entity property should store the `FoodCode` value from the API. Verify during seed build: `CnfFood[4484]` should have description "Grains, wheat flour, white, all purpose, enriched, calcium fortified".

### Pitfall: Seed file contains only 2015 update records (not full dataset)

**What goes wrong:** If the seed builder reads the Health Canada bulk CSV download (`cnf-fcen-csv-update-miseajour.zip`), it gets only the 2015 UPDATE records (607 new foods, 2823 new CFs) — not the full 5,690-food database.

**How to avoid:** The seed builder **must** use the CNF REST API, not just the CSV download. The REST API returns all 5,690 foods, 22,760 macro records, and 16,656 conversion factors. The CSV zip is the delta-only update package.

---

## Code Examples

### ConversionFactor lookup with unit scaling [VERIFIED: formula from CNF 2015 DB Structure PDF]

```csharp
// In NutritionService
private double? TryGetGramsFromCnfFactor(int cnfFoodId, double recipeAmountInMl, IEnumerable<CnfConversionFactor> factors)
{
    // Find CNF measure with closest mL value
    var best = factors
        .Select(f => new { Factor = f, CnfMl = ParseMlFromMeasureDescription(f.MeasureDescription) })
        .Where(x => x.CnfMl > 0)
        .OrderBy(x => Math.Abs(x.CnfMl - recipeAmountInMl))
        .FirstOrDefault();

    if (best is null) return null;
    var tolerance = best.CnfMl * 0.20; // ±20% tolerance
    if (Math.Abs(best.CnfMl - recipeAmountInMl) > tolerance) return null;

    // Scale CF to recipe volume
    var scale = recipeAmountInMl / best.CnfMl;
    var grams = best.Factor.ConversionFactorValue * scale * 100; // CF × scale × 100 = grams
    return grams;
}

private static double ParseMlFromMeasureDescription(string measure)
{
    var match = Regex.Match(measure, @"^(\d+(?:\.\d+)?)\s*ml", RegexOptions.IgnoreCase);
    return match.Success ? double.Parse(match.Groups[1].Value) : 0;
}
```

### NutritionInfoDto → JSON-LD emission [VERIFIED: JsonLdRecipeProjector.cs pattern]

```csharp
// JsonLdRecipeProjector.Project signature change:
public static string Project(RecipeDocument doc, string? absoluteImageUrl, NutritionInfoDto? nutrition = null)
{
    // ... (existing code unchanged) ...

    // Add AFTER author block, BEFORE the final Serialize call:
    if (nutrition is not null)
    {
        model["nutrition"] = new Dictionary<string, string>
        {
            ["@type"] = "NutritionInformation",
            ["calories"] = $"{nutrition.CaloriesPerServing:0} calories",
            ["proteinContent"] = $"{nutrition.ProteinGPerServing:0.#} g",
            ["carbohydrateContent"] = $"{nutrition.CarbGPerServing:0.#} g",
            ["fatContent"] = $"{nutrition.FatGPerServing:0.#} g",
        };
    }

    return JsonSerializer.Serialize(model, LdOptions);
}
```

### Content hash for staleness detection [VERIFIED: BCL SHA-256 API]

```csharp
// In RecipeService.UpdateAsync, after writing CanonicalDocumentJson:
using var sha256 = SHA256.Create();
var hash = Convert.ToHexString(
    sha256.ComputeHash(Encoding.UTF8.GetBytes(recipe.CanonicalDocumentJson!)));

var cache = await _context.RecipeNutritionCaches.FindAsync(recipeId);
if (cache is not null && cache.CanonicalDocHash != hash)
{
    cache.IsStale = true;
    cache.CanonicalDocHash = hash;
    // Do NOT recompute - stale flag surfaces in RecipeView as a CTA
}
```

---

## State of the Art

| Old Approach | Current Approach | Impact |
|--------------|------------------|--------|
| USDA FDC (USDA-oriented, pre-CNF) | Canadian Nutrient File (Health Canada, 2026-06-07 decision) | Canadian food data, OGL-Canada attribution required |
| FDC "density" via foodPortions data | CNF `servingsize` API (CF = grams/100) | Mathematically simpler; works offline |
| Generic water density fallback | Curated KA + FAO/INFOODS table | SC3 flour anchor verified exactly |
| Nutrition computed on save path | Explicit "Calculate nutrition" CTA; post-save enrichment | P7 guard: save never blocks |

**Deprecated / superseded:**
- Any reference to USDA FDC API, FdcApiKey, or HttpClient nutrition path: out of scope for Phase 15 (deferred to optional future fallback).
- The vestigial `Ingredient.NutritionalInfoJson` column: do not use; leave untouched per D-15-18.

---

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | Confidence-tier thresholds: HIGH ≥0.80, MEDIUM 0.50–0.79, LOW <0.50 | Target 4 | Wrong threshold → too many false positives or false negatives; refine at plan time |
| A2 | ±20% tolerance for CNF measure mL matching | Target 2 | Too tight: misses valid matches; too loose: wrong measure used |
| A3 | Seed build uses CNF REST API (one-time during development, committed to repo) | Target 5 | If CNF API goes offline permanently, a cached/committed copy is the backup |
| A4 | `NutritionInfoDto` as a separate record in Application/DTOs | Target 6 | Shape is discretionary per CONTEXT.md; alternative: tuple or anonymous type |
| A5 | SHA-256 of full CanonicalDocumentJson string as the staleness hash | Target 7 | Alternative: hash only ingredient names + amounts; simpler but misses step/tag changes that affect nothing nutritionally |
| A6 | CNF REST API will remain available during seed build | Target 5 | If API is temporarily unavailable, fallback: download Excel zip and extract from base Access file; or use the 2015 update CSV + manually obtain base |

---

## Open Questions

1. **CNF `FoodCode` vs `FoodID` in the seed** — The REST API exposes `food_code` but not `food_id` (internal PK). The CSV CONVERSION FACTOR file uses `FoodID` (PK). Since the seed builder will use the REST API for CF data (via `servingsize` endpoint), and both the food list and servingsize endpoint use `food_code`, the PK for `CnfFood` should be `food_code`. This is confirmed by the fact that the `servingsize` API links via `food_code`. No planner action needed; just verify the seed build script uses `food_code` consistently.

2. **Per-ingredient IngredientDensity table placement** — `IngredientDensityProvider` is an Application-layer service (D-15-09). The density data can be hardcoded as a static dictionary or loaded from a small seed file. Hardcoded is simpler for ~30 entries; a seed file allows extending without a deploy. Recommend hardcoded for this phase.

3. **CNF API rate limits** — The `nutrientamount` endpoint returns 524,675 records in a single call, confirmed working. No rate-limit errors observed. However, the Health Canada API is a public government service without a documented SLA. The seed builder should include retry logic and cache the API responses locally before building the JSON files.

---

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|-------------|-----------|---------|----------|
| .NET 10 SDK | Build + run | ✓ | 10.0.108 | — |
| SQLite | EF Core storage | ✓ | (bundled with EF) | — |
| Python 3 (seed builder) | `tools/build-cnf-seed.py` | ✓ | 3.14.4 | Rewrite in C# if needed |
| CNF REST API | Seed build (one-time) | ✓ | 2015 data confirmed live | Bulk CSV download (partial CF data) |
| `System.Security.Cryptography.SHA256` | Content hash | ✓ | BCL (no new package) | — |

**No blocking dependencies.**

---

## Validation Architecture

### Phase 15 Unit Tests (Nyquist-style — service + projector level)

The CONTEXT.md (discretionary area) recommends service + projector unit tests this phase. Recommended coverage:

| Test | What | Command | Exists? |
|------|------|---------|---------|
| Flour anchor | `NutritionService` computes "1 cup all-purpose flour" → 455 ± 20 kcal | `dotnet test --filter NutritionService_FlourAnchor` | ❌ Wave 0 |
| ≥20 ingredients density | `IngredientDensityProvider` has g/mL for KA-verified list | `dotnet test --filter IngredientDensityProvider` | ❌ Wave 0 |
| Unmatched not zeroed | Unmatched ingredient → "--" not 0 in match result | `dotnet test --filter NutritionService_UnmatchedNotZero` | ❌ Wave 0 |
| Stale on doc change | `RecipeService.UpdateAsync` → `RecipeNutritionCache.IsStale=true` | `dotnet test --filter RecipeService_NutritionStale` | ❌ Wave 0 |
| JSON-LD nutrition present | `JsonLdRecipeProjector.Project(doc, img, nutrition)` includes `NutritionInformation` | `dotnet test --filter JsonLdProjector_NutritionPresent` | ❌ Wave 0 |
| JSON-LD nutrition absent | `JsonLdRecipeProjector.Project(doc, img, null)` omits `nutrition` key | `dotnet test --filter JsonLdProjector_NutritionAbsent` | ❌ Wave 0 |
| Disclaimer present | Nutrition panel DOM contains the exact disclaimer string | Manual UAT / Phase 16 harness | — |
| Water density guard | 1 cup flour with density=1.0 g/mL → ~862 kcal (test confirms this is WRONG and test passes using correct density) | `dotnet test --filter IngredientDensityProvider_NoWaterDensity` | ❌ Wave 0 |
| CF scale factor | US cup (236.588 mL) → scale 0.9464 applied when matching "250ml" | `dotnet test --filter NutritionService_CupScaleFactor` | ❌ Wave 0 |
| Golden test: nutrition-absent recipe | Phase 13 JSON-LD output unchanged when nutrition=null | `dotnet test --filter JsonLdProjector` (existing) | ✅ Exists |

---

## Security Domain

Security enforcement is enabled (trusted-LAN posture; no new external HTTP calls at runtime). ASVS categories for Phase 15:

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | No | Nutrition is read-only display; existing auth pattern unchanged |
| V3 Session Management | No | No new session state |
| V4 Access Control | Yes | `RecipeNutritionCache` read/write must be gated to the recipe owner (same check as RecipeService); `NutritionService.ComputeAsync(recipeId, userId)` should verify ownership |
| V5 Input Validation | No | Seed data is Health Canada's verbatim values; no user-supplied nutrient values |
| V6 Cryptography | Yes — trivially | SHA-256 for content hash; BCL `System.Security.Cryptography.SHA256` — no custom crypto |

**No SSRF risk:** The seed is built offline; no HTTP calls at runtime. The CNF REST API is called only during the one-time seed build step.

**OGL-Canada compliance:** Storing CNF values verbatim and attributing "Data: Health Canada, Canadian Nutrient File (2015)" satisfies both the redistribution requirement and the no-modification requirement. The non-dismissable disclaimer also satisfies the health-context requirement.

---

## Sources

### Primary (HIGH confidence)

- CNF 2015 Database Structure PDF (embedded in `cnf-fcen-csv-update-miseajour.zip` from Health Canada) — all column names, primary keys, ConversionFactorValue semantics
- Health Canada CNF REST API (`food-nutrition.canada.ca/api/canadian-nutrient-file/`) — live calls 2026-06-07: food list (5690), nutrientamount (all macros), servingsize (16656 CFs), nutrientname (all IDs)
- [King Arthur Baking Ingredient Weight Chart](https://www.kingarthurbaking.com/learn/ingredient-weight-chart) — g/cup for 23 baking staples
- [FAO/INFOODS Density Database v2.0](https://www.fao.org/4/ap815e/ap815e.pdf) — g/mL for oils, dairy, flour, sugar
- [Schema.org NutritionInformation](https://schema.org/NutritionInformation) — property names and value types
- [Google Search Central: Recipe structured data](https://developers.google.com/search/docs/appearance/structured-data/recipe) — `nutrition.calories` per-serving rule; `recipeYield` conditional requirement
- `src/CookBot.Application/Recipes/JsonLdRecipeProjector.cs` — existing projector pattern (read directly)
- `src/CookBot.Infrastructure/Data/DatabaseSeeder.cs` — seed-load + idempotent guard (read directly)

### Secondary (MEDIUM confidence)

- [Canadian Nutrient File about us](https://www.canada.ca/en/health-canada/services/food-nutrition/healthy-eating/nutrient-data/canadian-nutrient-file-about-us.html) — food count (5993 canonical; API shows 5690), OGL-Canada license
- [CNF Open Government Portal](https://open.canada.ca/data/en/dataset/089885f9-ed53-44e6-854a-14d21a1ec2e0) — download URLs
- [Jordan Poles: Extracting Canadian Nutrition Data](https://blog.jpoles1.com/archives/288) — SQL view confirming exact NutrientName strings

### Tertiary (LOW confidence / training knowledge)

- NutrientNameID=NutrientCode for macros (203/204/205/208): confirmed via live API call; not a training-data assumption

---

## Metadata

**Confidence breakdown:**
- CNF file structure: HIGH — verified from official PDF + live API
- NutrientNameIDs: HIGH — verified from live API
- ConversionFactor semantics: HIGH — verbatim from official DB structure PDF + live data cross-check
- SC3 flour anchor: HIGH — computed from live CNF API data (exactly 455.0 kcal)
- Density table (KA values): HIGH — verified from King Arthur official website
- Density table (FAO values): MEDIUM — extracted from FAO PDF; some values are ranges
- Matching algorithm thresholds: LOW (tagged ASSUMED) — discretionary per CONTEXT.md
- Seed build approach: MEDIUM — API endpoints confirmed working; script shape is discretionary

**Research date:** 2026-06-07
**Valid until:** 2026-12-07 (CNF 2015 is a static release; API may change)

---

## RESEARCH COMPLETE

**Phase:** 15 — Nutrition (Offline CNF — Canadian Nutrient File)
**Confidence:** HIGH

**Key findings:**
1. **CNF macro NutrientNameIDs confirmed**: 203 (PROTEIN), 204 (FAT), 205 (CARBOHYDRATE), 208 (ENERGY kcal) — NutrientNameID equals NutrientCode for all four macros; verified via live CNF REST API call.
2. **ConversionFactorValue semantics locked**: `CF = grams_in_measure / 100`; apply scale `recipe_mL / cnf_mL` for US-to-metric-cup correction (US cup 236.588 mL ÷ CNF 250 mL = 0.9464). SC3 flour anchor verified exactly: 364 kcal/100g × 1.24993 = **455.0 kcal/cup**.
3. **Full CNF accessible via REST API**: `/food/` (5690 foods), `/nutrientamount/` (all macros in one call), `/servingsize/` (16656 CFs) — no API key, no pagination, all confirmed working today. The bulk CSV download is update-only (delta); seed builder must use the API.
4. **Density table fully sourced**: 23 common baking ingredients with g/mL from King Arthur Baking (HIGH confidence, official) and FAO/INFOODS Density Database v2.0 (MEDIUM confidence, authoritative); covers all ≥20 unit test ingredients required by SC3.
5. **Schema.org `nutrition` format confirmed**: `"calories": "N calories"`, `"proteinContent": "N g"` etc., per-serving, emitted only when `nutrition ≠ null`; `recipeYield` must be present when calories present (already set via `doc.Servings`).
6. **Zero new NuGet packages confirmed**: all implementation uses BCL (`System.Security.Cryptography.SHA256`), EF Core, and System.Text.Json — all already in project.
7. **CNF FoodCode ≠ FoodID**: the REST API and `servingsize` endpoint use `food_code` (user-visible); the seed PK for `CnfFood` should use `food_code`. The internal `FoodID` join key is not exposed by the API and is not needed.
