---
phase: 15
plan: "01"
subsystem: nutrition-seed
tags: [cnf, nutrition, seed, offline, python]
dependency_graph:
  requires: []
  provides:
    - seeds/nutrition/cnf_foods.json
    - seeds/nutrition/cnf_conversion_factors.json
    - tools/build-cnf-seed.py
  affects:
    - plan 15-02 (CnfFood/CnfConversionFactor entities read seed shape)
    - plan 15-03 (DatabaseSeeder loads these files)
    - plan 15-05 (unit tests validate flour anchor + ≥20-ingredient coverage)
tech_stack:
  added: []
  patterns:
    - "One-time offline Python build script → committed JSON seed (mirrors seeds/ingredients.json precedent)"
    - "Verify-before-trust: NutrientNameID assertions before building seed"
    - "Retry-with-backoff + local file cache for API responses"
key_files:
  created:
    - tools/build-cnf-seed.py
    - tools/README-cnf-seed.md
    - seeds/nutrition/cnf_foods.json
    - seeds/nutrition/cnf_conversion_factors.json
  modified: []
decisions:
  - "food_code (user-visible, returned by REST API) used as FoodId PK — NOT the internal food_id; this matches CNF REST API semantics and RESEARCH pitfall notes"
  - "PascalCase keys (FoodId, FoodDescription, EnergyKcalPer100g, etc.) chosen to match seeder PropertyNameCaseInsensitive deserialization without mapping code"
  - "FoodGroup retained as optional field in seed (nullable) — downstream seeder can populate NormalizedDescription column at load time"
  - "/servingsize/ endpoint used for conversion factors; /conversionfactor/ is broken (HTTP 500)"
  - "Seed files pretty-printed with indent=2 for git-diffability while remaining compact enough for startup load"
metrics:
  duration_minutes: 12
  completed_date: "2026-06-07"
  tasks_completed: 2
  tasks_total: 2
  files_created: 4
  files_modified: 0
---

# Phase 15 Plan 01: CNF Offline Seed Builder Summary

One-time offline Python build script + bundled Canadian Nutrient File (CNF) 2015 seed files covering 5,690 foods x 4 macros and 16,656 household-measure->gram conversion factors, with the SC3 flour anchor (FoodCode 4484) verified exactly at 455.0 kcal/US-cup.

## Tasks Completed

| Task | Name | Commit | Key Files |
|------|------|--------|-----------|
| 1 | Write offline CNF seed-build script + README | 2cbb826 | tools/build-cnf-seed.py, tools/README-cnf-seed.md |
| 2 | Generate and commit bundled CNF seed JSON files | 2513e2d | seeds/nutrition/cnf_foods.json, seeds/nutrition/cnf_conversion_factors.json |

## Decisions Made

1. **food_code as FoodId PK:** The CNF REST API exposes `food_code` (user-visible code) — not the internal `food_id` (internal PK). The seed and EF entity use `food_code` as `CnfFood.FoodId` per the RESEARCH anti-pattern note. `FoodId 4484` = all-purpose flour confirmed.

2. **PascalCase JSON keys:** `FoodId`, `FoodDescription`, `EnergyKcalPer100g`, `ProteinGPer100g`, `FatGPer100g`, `CarbGPer100g`, `MeasureDescription`, `ConversionFactorValue` — matches the seeder's `PropertyNameCaseInsensitive` deserialization without a custom mapping layer.

3. **servingsize endpoint:** `/conversionfactor/` returns HTTP 500; `/servingsize/` returns all 16,656 records correctly (verified live).

4. **Verbatim storage:** All per-100g nutrient values are stored exactly as returned by the CNF API — no rounding, no normalization. OGL-Canada forbids modification.

## Verification Results

Task 2 automated check passed:

```
CNF seed OK: foods 5690 cfs 16656
```

Flour anchor spot-check:
```
FoodId=4484  description='Grains, wheat flour, white, all purpose, enriched, calcium fortified'
EnergyKcalPer100g=364.0  OK
250ml CF=1.32079  OK
Computed US-cup kcal: 455.0 kcal  (expected ~455.0)
```

## Deviations from Plan

None — plan executed exactly as written. Network was available; the script fetched the full 5,690-food dataset from the live CNF REST API. No sample/fallback seed was needed.

## Known Stubs

None. All four seed files contain complete, production-ready data.

## Threat Flags

No new threat surface introduced. The script only runs at build time; no new network endpoints, auth paths, or file access patterns exist at runtime. Threat mitigations T-15-01 and T-15-02 are in place:

- T-15-01 (supply chain): Seed committed from official Health Canada CNF REST API, version-pinned to 2015 edition, merged on `food_code`, values verbatim.
- T-15-02 (wrong NutrientNameID): Verify-before-trust step asserts 208/203/204/205 against live `nutrientname` endpoint and exits non-zero on mismatch.
- T-15-03 (runtime HTTP): No runtime HTTP path exists — seed is loaded from committed files only.

## Self-Check: PASSED

Files exist:
- tools/build-cnf-seed.py: FOUND
- tools/README-cnf-seed.md: FOUND
- seeds/nutrition/cnf_foods.json: FOUND (5,690 rows)
- seeds/nutrition/cnf_conversion_factors.json: FOUND (16,656 rows)

Commits exist:
- 2cbb826: FOUND (Task 1)
- 2513e2d: FOUND (Task 2)
