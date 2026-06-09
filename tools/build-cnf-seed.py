#!/usr/bin/env python3
"""
tools/build-cnf-seed.py
=======================
ONE-TIME OFFLINE CNF seed builder. Run during development to regenerate the
bundled Canadian Nutrient File seed files that ship in the repository under
seeds/nutrition/. The runtime .NET application NEVER calls these endpoints.

Usage:
    python3 tools/build-cnf-seed.py

Requirements:
    pip install requests          (dev-only; not a .NET runtime dependency)

Output:
    seeds/nutrition/cnf_foods.json             (~5,690 foods, ~680 KB)
    seeds/nutrition/cnf_conversion_factors.json (~16,656 rows, ~815 KB)

Source: Canadian Nutrient File (CNF), 2015 edition
        Health Canada — https://food-nutrition.canada.ca/
        Licence: Open Government Licence – Canada (OGL-Canada)
        Attribution required: "Data: Health Canada, Canadian Nutrient File (2015)"
        Values must be stored VERBATIM — modification forbidden by OGL-Canada.
        Column/food subsetting is permitted; value rounding/rescaling is NOT.

NutrientNameIDs used:
    208 = ENERGY (KILOCALORIES)  [NOT 268 kJ]
    203 = PROTEIN
    204 = FAT (TOTAL LIPIDS)
    205 = CARBOHYDRATE, TOTAL (BY DIFFERENCE)

Endpoint notes:
    /food/             — 5,690 food records
    /nutrientamount/   — 524,675 nutrient records (we filter to 4 macro IDs)
    /servingsize/      — 16,656 conversion factor records
                         (/conversionfactor/ endpoint is BROKEN — returns 500; use /servingsize/)

ConversionFactorValue semantics (from CNF 2015 DB Structure PDF, p.9):
    ConversionFactorValue = grams_of_food_in_measure / 100
    nutrient_per_measure  = nutrient_per_100g × ConversionFactorValue

Flour anchor (FoodCode 4484 — all-purpose flour, white, enriched):
    EnergyKcalPer100g = 364.0
    CF for 250ml      = 1.32079  →  132.1 g per Canadian cup
    Scaled to US cup (×0.9464): 125.0 g → 455.0 kcal/cup  [SC3 anchor]
"""

import json
import os
import sys
import time
import tempfile
import hashlib
from pathlib import Path

try:
    import requests
except ImportError:
    print("ERROR: 'requests' is not installed. Run: pip install requests", file=sys.stderr)
    sys.exit(1)

# ── Configuration ──────────────────────────────────────────────────────────────

CNF_BASE = "https://food-nutrition.canada.ca/api/canadian-nutrient-file"
MACRO_IDS = {208, 203, 204, 205}

# Expected NutrientNameID → name mappings for verify-before-trust step
EXPECTED_NUTRIENT_NAMES = {
    208: "ENERGY (KILOCALORIES)",
    203: "PROTEIN",
    204: "FAT (TOTAL LIPIDS)",
    205: "CARBOHYDRATE, TOTAL (BY DIFFERENCE)",
}

# Output files relative to repo root
REPO_ROOT = Path(__file__).parent.parent
SEEDS_DIR = REPO_ROOT / "seeds" / "nutrition"
OUT_FOODS = SEEDS_DIR / "cnf_foods.json"
OUT_CFS = SEEDS_DIR / "cnf_conversion_factors.json"

# Cache raw API responses in a temp dir so re-runs are fast
CACHE_DIR = Path(tempfile.gettempdir()) / "cnf_seed_cache"

# HTTP config
REQUEST_TIMEOUT = 120  # seconds; the full nutrientamount dump is ~15 MB
MAX_RETRIES = 3
RETRY_BACKOFF = 2.0  # seconds; doubles on each retry


# ── HTTP helpers ───────────────────────────────────────────────────────────────

def _cache_key(url: str) -> Path:
    """Return a stable filesystem path for the cached response of `url`."""
    h = hashlib.md5(url.encode()).hexdigest()
    CACHE_DIR.mkdir(parents=True, exist_ok=True)
    return CACHE_DIR / f"{h}.json"


def fetch_json(url: str, label: str) -> list | dict:
    """Fetch `url` as JSON with retry-backoff; cache the raw response on disk."""
    cache_path = _cache_key(url)
    if cache_path.exists():
        print(f"  [cache] {label}")
        with cache_path.open(encoding="utf-8") as f:
            return json.load(f)

    print(f"  [fetch] {label} …", end="", flush=True)
    last_exc: Exception | None = None
    for attempt in range(1, MAX_RETRIES + 1):
        try:
            resp = requests.get(url, timeout=REQUEST_TIMEOUT)
            resp.raise_for_status()
            data = resp.json()
            with cache_path.open("w", encoding="utf-8") as f:
                json.dump(data, f, ensure_ascii=False)
            print(f" {len(data)} records")
            return data
        except Exception as exc:
            last_exc = exc
            if attempt < MAX_RETRIES:
                wait = RETRY_BACKOFF * (2 ** (attempt - 1))
                print(f"\n  [retry {attempt}/{MAX_RETRIES}] {exc} — waiting {wait:.1f}s …",
                      end="", flush=True)
                time.sleep(wait)
    raise RuntimeError(f"Failed to fetch {label} after {MAX_RETRIES} attempts: {last_exc}")


# ── Step 1: Verify-before-trust ───────────────────────────────────────────────

def verify_nutrient_ids() -> None:
    """
    Assert that NutrientNameIDs 208/203/204/205 map to the expected nutrient
    names. Exit non-zero if any mapping differs — a wrong-ID seed cannot be
    silently built (T-15-02 mitigation).
    """
    print("\n== Step 1: Verify NutrientNameID mappings ==")
    url = f"{CNF_BASE}/nutrientname/?lang=en&type=json"
    nutrient_names = fetch_json(url, "nutrientname")

    id_to_name: dict[int, str] = {
        item["nutrient_name_id"]: item["nutrient_name"]
        for item in nutrient_names
    }

    errors: list[str] = []
    for nid, expected in EXPECTED_NUTRIENT_NAMES.items():
        actual = id_to_name.get(nid, "<NOT FOUND>")
        if actual != expected:
            errors.append(
                f"  NutrientNameID {nid}: expected '{expected}', got '{actual}'"
            )
        else:
            print(f"  OK  ID={nid:3d}  {actual}")

    if errors:
        print("\nERROR: NutrientNameID mapping mismatch:", file=sys.stderr)
        for e in errors:
            print(e, file=sys.stderr)
        print(
            "\nDo NOT commit a seed built from mismatched IDs. "
            "Check the CNF API for schema changes.",
            file=sys.stderr,
        )
        sys.exit(1)

    print("  All NutrientNameID mappings verified OK.\n")


# ── Step 2: Fetch all data ────────────────────────────────────────────────────

def fetch_foods() -> list[dict]:
    """Fetch the full CNF food list (~5,690 records)."""
    print("== Step 2a: Fetch foods ==")
    url = f"{CNF_BASE}/food/?lang=en&type=json"
    return fetch_json(url, "food")


def fetch_nutrient_amounts() -> dict[int, dict[int, float]]:
    """
    Fetch all nutrient amounts and filter to macro IDs {208,203,204,205}.
    Returns: {food_code: {nutrient_name_id: nutrient_value}}
    Note: the REST API merges on food_code, NOT on the internal food_id.
    """
    print("== Step 2b: Fetch nutrient amounts (full dump ~15 MB, cached after first run) ==")
    url = f"{CNF_BASE}/nutrientamount/?lang=en&type=json"
    raw = fetch_json(url, "nutrientamount")

    # Build: food_code → {nutrient_name_id → value}
    macros: dict[int, dict[int, float]] = {}
    for record in raw:
        nid = record.get("nutrient_name_id")
        if nid not in MACRO_IDS:
            continue
        # The REST API exposes food_code (user-visible code) as the join key.
        # food_id is the internal CNF PK and is NOT surfaced reliably here.
        food_code = record.get("food_code")
        if food_code is None:
            continue
        food_code = int(food_code)
        nid = int(nid)
        value = float(record.get("nutrient_value", 0.0))
        macros.setdefault(food_code, {})[nid] = value

    print(f"  Filtered to {len(macros)} foods with ≥1 macro record.")
    return macros


def fetch_conversion_factors() -> list[dict]:
    """
    Fetch all CNF conversion factors via the /servingsize/ endpoint.
    NOTE: /conversionfactor/ is BROKEN (returns 500) — always use /servingsize/.
    """
    print("== Step 2c: Fetch conversion factors via /servingsize/ ==")
    url = f"{CNF_BASE}/servingsize/?lang=en&type=json"
    return fetch_json(url, "servingsize")


# ── Step 3: Merge and write ───────────────────────────────────────────────────

def build_cnf_foods(
    foods: list[dict],
    macros_by_food_code: dict[int, dict[int, float]],
) -> list[dict]:
    """
    Merge food list with macro nutrient amounts on food_code.
    Skips any food with no NutrientNameID-208 (energy) record.
    Values are stored VERBATIM (OGL-Canada forbids modification).
    PascalCase keys match the seeder's PropertyNameCaseInsensitive deserialization.
    """
    print("\n== Step 3a: Build cnf_foods records ==")
    cnf_foods: list[dict] = []
    skipped_no_energy = 0

    for food in foods:
        # food_code is the user-visible code; it is what the /nutrientamount/ endpoint
        # also uses as its join key. This is NOT the same as the internal food_id.
        food_code = food.get("food_code")
        if food_code is None:
            continue
        food_code = int(food_code)

        m = macros_by_food_code.get(food_code, {})
        if 208 not in m:
            skipped_no_energy += 1
            continue  # skip foods with no caloric data

        cnf_foods.append({
            "FoodId": food_code,                       # CNF FoodCode (user-visible)
            "FoodDescription": food.get("food_description", ""),
            "FoodGroup": food.get("food_group_name") or food.get("food_group", None),
            # Verbatim per-100g values — OGL-Canada forbids rounding or rescaling
            "EnergyKcalPer100g": m[208],
            "ProteinGPer100g": m.get(203, 0.0),
            "FatGPer100g": m.get(204, 0.0),
            "CarbGPer100g": m.get(205, 0.0),
        })

    print(f"  Built {len(cnf_foods)} food records (skipped {skipped_no_energy} with no energy).")
    return cnf_foods


def build_cnf_conversion_factors(raw_serving_sizes: list[dict]) -> list[dict]:
    """
    Convert /servingsize/ records into CnfConversionFactor seed rows.
    Merges on food_code (the user-visible code exposed by the REST API).
    PascalCase keys match the seeder's PropertyNameCaseInsensitive deserialization.
    """
    print("== Step 3b: Build cnf_conversion_factors records ==")
    cfs: list[dict] = []

    for record in raw_serving_sizes:
        food_code = record.get("food_code")
        if food_code is None:
            continue

        measure_desc = (
            record.get("measure_name")
            or record.get("serving_description")
            or record.get("MeasureDescription")
            or ""
        )
        cf_value = record.get("conversion_factor_value")
        if cf_value is None:
            cf_value = record.get("ConversionFactorValue")
        if cf_value is None:
            continue

        cfs.append({
            "FoodId": int(food_code),
            "MeasureDescription": str(measure_desc),
            "ConversionFactorValue": float(cf_value),
        })

    print(f"  Built {len(cfs)} conversion factor records.")
    return cfs


def write_seed(data: list[dict], path: Path, label: str) -> None:
    """Write a JSON seed array to `path`, UTF-8, compact."""
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("w", encoding="utf-8") as f:
        json.dump(data, f, ensure_ascii=False, indent=2)
    size_kb = path.stat().st_size / 1024
    print(f"  Written: {path}  ({len(data)} rows, {size_kb:.0f} KB)")


# ── Step 4: Spot-check flour anchor ───────────────────────────────────────────

def spot_check(cnf_foods: list[dict], cnf_cfs: list[dict]) -> None:
    """
    Verify the SC3 flour anchor (FoodCode 4484, all-purpose flour):
      - EnergyKcalPer100g ≈ 364
      - 250ml CF ≈ 1.32079
    Prints warnings but does NOT exit non-zero — the full dataset may still be
    correct even if this specific record shifted slightly in a future CNF edition.
    """
    print("\n== Step 4: Spot-check flour anchor (FoodCode 4484) ==")
    flour = next((f for f in cnf_foods if f["FoodId"] == 4484), None)
    if flour is None:
        print("  WARNING: FoodId 4484 (all-purpose flour) not found in foods seed!")
    else:
        kcal = flour["EnergyKcalPer100g"]
        ok = 350 <= kcal <= 375
        print(f"  FoodId=4484  description='{flour['FoodDescription']}'")
        print(f"  EnergyKcalPer100g={kcal}  {'OK' if ok else 'WARNING: out of [350,375]'}")

    flour_cfs = [c for c in cnf_cfs if c["FoodId"] == 4484]
    ml250 = [c for c in flour_cfs if "250" in c["MeasureDescription"].replace(" ", "")]
    if not ml250:
        print("  WARNING: No 250ml conversion factor found for FoodId 4484!")
    else:
        cf = ml250[0]["ConversionFactorValue"]
        ok = 1.2 <= cf <= 1.45
        print(f"  250ml CF={cf}  {'OK' if ok else 'WARNING: out of [1.2,1.45]'}")
        kcal_cup = flour["EnergyKcalPer100g"] * cf * (236.588 / 250.0)
        print(f"  Computed US-cup kcal: {kcal_cup:.1f} kcal  (expected ~455.0)")


# ── Main ───────────────────────────────────────────────────────────────────────

def main() -> None:
    print("=" * 60)
    print("CNF seed builder — Canadian Nutrient File (2015)")
    print("Source: https://food-nutrition.canada.ca/")
    print("Licence: Open Government Licence – Canada (OGL-Canada)")
    print("=" * 60)

    # 1. Verify-before-trust: assert NutrientNameID mappings
    verify_nutrient_ids()

    # 2. Fetch all data from CNF REST API
    foods = fetch_foods()
    macros_by_food_code = fetch_nutrient_amounts()
    raw_serving_sizes = fetch_conversion_factors()

    # 3. Merge and build seed arrays
    cnf_foods = build_cnf_foods(foods, macros_by_food_code)
    cnf_cfs = build_cnf_conversion_factors(raw_serving_sizes)

    # 4. Write seed files
    print("\n== Step 4: Write seed files ==")
    write_seed(cnf_foods, OUT_FOODS, "cnf_foods")
    write_seed(cnf_cfs, OUT_CFS, "cnf_conversion_factors")

    # 5. Spot-check flour anchor
    spot_check(cnf_foods, cnf_cfs)

    print("\nDone. Commit seeds/nutrition/ to lock the CNF 2015 snapshot.")
    print("The runtime app will load these files at startup via DatabaseSeeder.")
    print("NO runtime HTTP calls are made to the CNF API.")


if __name__ == "__main__":
    main()
