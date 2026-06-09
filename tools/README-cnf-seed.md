# CNF Seed Builder

`tools/build-cnf-seed.py` is a **one-time offline development script** that fetches
data from the Canadian Nutrient File (CNF) REST API and writes two seed JSON files
under `seeds/nutrition/`. These files are committed to the repository and loaded by
the runtime app at startup — **the runtime app NEVER calls the CNF API**.

## Quick start

```bash
pip install requests   # one-time dev dependency (not a .NET runtime dependency)
python3 tools/build-cnf-seed.py
```

The script writes (or overwrites):
- `seeds/nutrition/cnf_foods.json` — ~5,690 foods × 4 fields (energy + 3 macros)
- `seeds/nutrition/cnf_conversion_factors.json` — ~16,656 household-measure→gram factors

After running, commit both files to lock the CNF 2015 edition snapshot:

```bash
git add seeds/nutrition/cnf_foods.json seeds/nutrition/cnf_conversion_factors.json
git commit -m "chore: regenerate CNF seed (YYYY-MM-DD)"
```

## Why this script exists

The Canadian Nutrient File (CNF, 2015 edition) is Health Canada's official
food-composition database. It ships per-100 g values for 5,690 foods and 16,656
household-measure conversion factors. It is available via a free REST API — no
API key required.

FreelovesCookBot uses CNF data to compute nutrition estimates from ingredient
amounts in recipes. The bundled seed approach (offline, committed to the repo)
ensures:

1. **SC1 — Fully offline at runtime:** Users do not need internet access, an API
   key, or a Health Canada service to be available. The `DatabaseSeeder` loads the
   seed files into two SQLite tables (`CnfFood`, `CnfConversionFactor`) at startup.

2. **Version-pinned:** The committed files represent the CNF 2015 edition
   exactly as returned by the API. The app does not drift with live API changes.

3. **No supply-chain surprise:** The one-time build step is auditable. The
   resulting JSON is reviewed and committed; the runtime reads only local files.

## Data source and edition

| Property | Value |
|----------|-------|
| Database | Canadian Nutrient File (CNF) |
| Edition | 2015 |
| Publisher | Health Canada |
| URL | https://food-nutrition.canada.ca/ |
| API | https://food-nutrition.canada.ca/api/canadian-nutrient-file/ |
| Licence | Open Government Licence – Canada (OGL-Canada) |

## OGL-Canada compliance — READ THIS BEFORE MODIFYING THE SEED

The Canadian Nutrient File is released under the
[Open Government Licence – Canada (OGL-Canada)](https://open.canada.ca/en/open-government-licence-canada).

**Permitted by OGL-Canada:**
- Subsetting the dataset (we use only energy + 3 macros of the full nutrient set)
- Distributing the subset in another format (JSON instead of CSV)
- Computing derived values for display (e.g. per-serving from per-100 g)

**Forbidden by OGL-Canada:**
- **Modifying the stored nutrient values** — rounding, normalizing, or rescaling
  the per-100 g values in the seed files is NOT permitted
- Misrepresenting the source

**Required by OGL-Canada:**
- Attribution on every surface that displays CNF data:
  **"Data: Health Canada, Canadian Nutrient File (2015)"**

The build script stores all `EnergyKcalPer100g`, `ProteinGPer100g`,
`FatGPer100g`, and `CarbGPer100g` values **verbatim** from the API — no rounding,
no rescaling. Do not modify these values after the fact.

Per-serving computation at display time is permitted. The runtime
`NutritionService` applies conversion factors and amounts there; it does not
modify the stored seed values.

## Endpoints used

| Endpoint | Purpose |
|----------|---------|
| `/nutrientname/?lang=en&type=json` | Verify-before-trust: assert NutrientNameID 208/203/204/205 map to expected names |
| `/food/?lang=en&type=json` | Food list (5,690 records) |
| `/nutrientamount/?lang=en&type=json` | Nutrient amounts (filtered to macro IDs 208/203/204/205) |
| `/servingsize/?lang=en&type=json` | Conversion factors (household-measure → grams) |

> **Note:** The `/conversionfactor/` endpoint returns HTTP 500 and is unusable.
> Always use `/servingsize/` for conversion factors.

## NutrientNameIDs

| NutrientNameID | Nutrient | Unit |
|---------------|----------|------|
| 208 | ENERGY (KILOCALORIES) | kCal |
| 203 | PROTEIN | g |
| 204 | FAT (TOTAL LIPIDS) | g |
| 205 | CARBOHYDRATE, TOTAL (BY DIFFERENCE) | g |

> **Important:** Use NutrientNameID 208 for kilocalories — NOT 268 (kilojoules).

## Conversion factor semantics

From the CNF 2015 Database Structure PDF, page 9:

```
ConversionFactorValue = grams_of_food_in_measure / 100

nutrient_per_measure = nutrient_per_100g × ConversionFactorValue
```

Example — all-purpose flour (FoodCode 4484):

```
EnergyKcalPer100g = 364.0
CF for 250 ml     = 1.32079  →  132.1 g per Canadian cup
Scaled to US cup (×236.588/250 = 0.9464): 125.0 g → 455.0 kcal/cup
```

## Verify-before-trust step

The script asserts the NutrientNameID → name mappings before building the seed.
If any ID maps to an unexpected name (e.g. Health Canada renumbered a nutrient),
the script exits non-zero and prints an error. This prevents silently building a
seed with wrong nutritional data.

## Caching

Raw API responses are cached to a temp directory (`/tmp/cnf_seed_cache/` or
OS equivalent) keyed by URL MD5. Subsequent runs reuse the cache for speed.
Delete the cache directory to force a fresh fetch.

## Runtime loading

The `DatabaseSeeder` in `src/CookBot.Infrastructure/` reads these files at
startup via `Path.Combine(contentRootPath, "..", "seeds", "nutrition", ...)` and
loads them idempotently into the `CnfFood` and `CnfConversionFactor` SQLite
tables (guarded by `if (await context.CnfFoods.AnyAsync()) return`).

**The runtime app makes zero HTTP calls to the CNF API.**
