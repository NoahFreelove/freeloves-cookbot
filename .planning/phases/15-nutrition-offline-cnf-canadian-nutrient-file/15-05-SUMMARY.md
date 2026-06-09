---
phase: 15-nutrition-offline-cnf-canadian-nutrient-file
plan: "05"
subsystem: nutrition-compute
tags: [nutrition, cnf, offline-compute, cache, sha256, tdd]
dependency_graph:
  requires: ["15-02", "15-03", "15-04"]
  provides: ["INutritionService", "NutritionService", "RecipeService-stale-mark"]
  affects: ["RecipeService", "DependencyInjection", "IngredientDensityProvider"]
tech_stack:
  added: []
  patterns:
    - "Token-intersection scoring over pre-normalized CNF descriptions (offline, in-memory)"
    - "CNF CF closest-mL match with ±20% tolerance + US-cup 0.9464 scale"
    - "SHA-256 BCL hash for canonical-doc staleness detection"
    - "IRepository<RecipeNutritionCache> stale-mark in RecipeService (no NutritionService dependency)"
key_files:
  created:
    - src/CookBot.Application/Services/INutritionService.cs
    - src/CookBot.Infrastructure/Services/NutritionService.cs
    - tests/CookBot.Tests/Nutrition/NutritionServiceTests.cs
  modified:
    - src/CookBot.Infrastructure/DependencyInjection.cs
    - src/CookBot.Application/Services/RecipeService.cs
    - src/CookBot.Application/Services/IngredientDensityProvider.cs
    - tests/CookBot.Tests/Services/RecipePhotoServiceTests.cs
    - tests/CookBot.Tests/Services/RecipeServiceV4FieldsTests.cs
    - tests/CookBot.Tests/Services/OwnershipTests.cs
decisions:
  - "NutritionService in Infrastructure (mirrors RecipePhotoService): injects CookBotDbContext directly for AsNoTracking bulk CNF food load"
  - "RecipeService uses IRepository<RecipeNutritionCache> for stale-mark; zero NutritionService/INutritionService imports (grep=0)"
  - "IngredientDensityProvider.GetDensityGPerMl builds a second lookup indexed by IngredientNormalizer.Normalize(key) at class init to handle hyphen→space mismatch"
  - "ComputeAsync returns PerIngredientMatchRecord with null EnergyKcal for UNMATCHED/no-grams (never 0)"
metrics:
  duration: "~40 minutes"
  completed: "2026-06-08"
  tasks_completed: 3
  tasks_total: 3
  files_created: 3
  files_modified: 6
---

# Phase 15 Plan 05: Offline Nutrition Compute Engine Summary

Implemented the offline CNF nutrition compute engine (NUTR-02/03/04): token-intersection food matching, CNF-factor-first volume→grams conversion with US-cup 0.9464 scale, density fallback, mass-direct path, RecipeNutritionCache upsert with SHA-256 content hash, and RecipeService stale-mark — all with the flour anchor verified at ≈455 kcal and the P7 invariant (save never calls NutritionService) proven by grep = 0.

## Tasks Completed

| Task | Commit | Description |
|------|--------|-------------|
| 1: INutritionService + NutritionService + DI | cd62b00 | Interface + full compute engine + scoped registration |
| 2: RecipeService SHA-256 stale-mark | dc5e8e9 | IRepository<RecipeNutritionCache> injection, hash + IsStale on every canonical write |
| 3: NutritionServiceTests (TDD) | ab25271 | 12 tests: flour anchor, density fallback, unmatched-null, cup scale, mass-direct, coverage, stale-on-change |

## Success Criteria Verification

- [x] NutritionService computes offline: CNF factor → density fallback → mass-direct → UNMATCHED(null, not zero)
- [x] Flour anchor test: 1 cup all-purpose flour ≈ 455 kcal (±15) — PASSES (440–470 range asserted)
- [x] Unmatched → null energy (renders "--"), low-confidence → MEDIUM; match FoodId/description stored
- [x] RecipeNutritionCache written with SHA-256 canonical-doc hash; stale-on-doc-change works
- [x] RecipeService stale-marks via IRepository, NEVER calls NutritionService (grep returns 0)
- [x] dotnet build clean; 548 non-API tests green (6 pre-existing API key failures unchanged)
- [x] SUMMARY.md created and committed

## Architecture

`INutritionService` (Application) / `NutritionService` (Infrastructure) follows the RecipePhotoService precedent:
- Injects `CookBotDbContext` directly for bulk AsNoTracking CNF food load (O(5,690 foods) per compute, in-memory scoring)
- Copies `AssertOwnershipAsync` verbatim from RecipePhotoService (ASVS V4 / T-15-10)
- Registered as `Scoped<INutritionService, NutritionService>` alongside RecipePhotoService in DI

`RecipeService` additions:
- Injects `IRepository<RecipeNutritionCache>` (new ctor param after `recipePhotoRepo`)
- Private helper `MarkNutritionCacheStaleIfChangedAsync` called after each canonical doc write (CreateAsync, UpdateAsync, SyncPrimaryPhotoUrlAsync)
- Zero functional NutritionService references (grep = 0 confirmed)

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] IngredientDensityProvider hyphen→space key mismatch**
- **Found during:** Task 3 test execution (DensityFallback test returned 0 kcal)
- **Issue:** Table keys use human-readable hyphens (e.g. `"all-purpose flour"`) but `IngredientNormalizer.Normalize` converts hyphens to spaces (`"all purpose flour"`), causing lookup misses
- **Fix:** Added `NormalizedDensities` secondary lookup built at class init by normalizing each key; `GetDensityGPerMl` tries both the raw key and the normalized-key lookup
- **Files modified:** `src/CookBot.Application/Services/IngredientDensityProvider.cs`
- **Commit:** ab25271 (included in Task 3 commit)

**2. [Rule 3 - Blocking] RecipeService constructor signature changed — test files needed updating**
- **Found during:** Task 2 (adding IRepository<RecipeNutritionCache> param)
- **Issue:** Three existing test files hard-coded the RecipeService constructor without the new param
- **Fix:** Added `var nutritionCacheRepo = new Repository<RecipeNutritionCache>(_db)` and passed it in all three test ctors
- **Files modified:** `RecipePhotoServiceTests.cs`, `RecipeServiceV4FieldsTests.cs`, `OwnershipTests.cs`
- **Commit:** dc5e8e9

## Known Stubs

None — NutritionService computes real values from the seeded CNF data. Cache rows are written on explicit `ComputeAsync` calls only.

## Threat Flags

No new unplanned security surface introduced. T-15-10 (ownership guard) and T-15-11 (no raw SQL) are mitigated as planned:
- `AssertOwnershipAsync` gates both `GetCacheAsync` and `ComputeAsync`
- All CNF matching is in-memory token scoring over EF entities loaded via parameterized LINQ (no string-concatenated queries)
- T-15-SC confirmed: zero new NuGet packages

## Self-Check

### Check created files exist

- `src/CookBot.Application/Services/INutritionService.cs` — created
- `src/CookBot.Infrastructure/Services/NutritionService.cs` — created
- `tests/CookBot.Tests/Nutrition/NutritionServiceTests.cs` — created

### Check commits exist

- cd62b00 — feat(15-05): INutritionService + NutritionService
- dc5e8e9 — feat(15-05): SHA-256 stale-mark
- ab25271 — test(15-05): NutritionService tests

### Self-Check: PASSED
