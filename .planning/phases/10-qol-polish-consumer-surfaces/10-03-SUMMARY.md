---
phase: 10-qol-polish-consumer-surfaces
plan: "03"
subsystem: pantry-match-algorithm
tags: [pantry-match, application-layer, scoring, dietary-filter, tdd, tests]
dependency_graph:
  requires: ["10-01", "10-02"]
  provides: ["10-04"]
  affects: ["src/CookBot.Application", "tests/CookBot.Tests"]
tech_stack:
  added: []
  patterns: [TDD-RED/GREEN, in-memory SQLite, IOptions<T>, Repository<T> multi-call in-memory join, Dictionary static map, exponential decay formula]
key_files:
  created:
    - src/CookBot.Application/Services/PantryMatchService.cs
    - tests/CookBot.Tests/Services/PantryMatchServiceTests.cs
  modified:
    - src/CookBot.Application/DependencyInjection.cs
decisions:
  - "PantryMatchService constructor extended beyond plan minimum to include IRepository<RecipeTag> and IRepository<RecipeIngredient> — required for correctness (Rule 2: critical functionality); Repository<T> FindAsync does not eager-load navigation properties so related data must be loaded with separate round-trips and joined in memory"
  - "IRepository<CookbookShare> added to constructor for authz-predicate data — EF translates the authz predicate r.Cookbook.Shares.Any(...) correctly at query time but nav props on returned entities are empty; tags and ingredients loaded separately"
  - "PantryService injected directly (not replicated) per plan option (a) — reuses canonical GetAllUserAccessibleItemsAsync for owned + member pantries"
  - "Diet filter AND-combines: first positive RecipeTag match filters list, then negative IngredientCategory exclude applied per preference (unknown labels skip negative filter per D-47)"
metrics:
  duration_minutes: 25
  completed: "2026-05-17"
  tasks_completed: 2
  tasks_total: 2
  files_created: 2
  files_modified: 1
---

# Phase 10 Plan 03: PantryMatchService Implementation Summary

**One-liner:** D-44 exponential-decay scoring, D-45 AND-combined dietary filter, and D-47 corrected diet→category map — implemented with TDD (RED then GREEN) and 14 passing tests covering formula, stable sort, authz, coverage cutoff, and dietary filter edge cases.

## Tasks Completed

| Task | Name | Commit | Files |
|------|------|--------|-------|
| 1 | Write PantryMatchServiceTests (RED gate) | f021086 | `tests/CookBot.Tests/Services/PantryMatchServiceTests.cs` |
| 2 | Implement PantryMatchService + register in AddApplication (GREEN) | eeb393a | `src/CookBot.Application/Services/PantryMatchService.cs`, `src/CookBot.Application/DependencyInjection.cs` |

## What Was Built

### PantryMatchService (D-44..47)

`src/CookBot.Application/Services/PantryMatchService.cs` — full implementation of `IPantryMatchService.GetMatchesAsync(userId, ct)`:

**Scoring formula (D-44 verbatim):**
```
score = (matched / total) - RecencyPenaltyWeight * exp(-daysSinceCooked / RecencyHalfLifeDays)
```
- `matched`: distinct RecipeIngredient.IngredientId values in user's pantry set
- `total`: recipe.RecipeIngredients.Count
- `daysSinceCooked`: from `RecipeMade.CompletedAt` via `IRecipeMadeService.GetLastCookAsync`; null → penalty term = 0
- Constants sourced from `IOptions<PantryMatchOptions>` (D-46)

**Dietary filter (D-45 AND-combined):** For each preference string, both gates must pass:
1. Positive RecipeTag match: recipe must have a tag name matching the preference (case-insensitive)
2. Negative IngredientCategory exclude: recipe must have no ingredient in the diet's excluded categories (per D-47 map)
3. Unknown diet labels (e.g. "keto"): skip negative filter; positive tag match still required

**DietExcludeMap (D-47 corrected):**
```csharp
["vegan"]       => [Meat, Seafood, Dairy]
["vegetarian"]  => [Meat, Seafood]
["dairy-free"]  => [Dairy]
["gluten-free"] => [Grains, Bakery]
```
Only real IngredientCategory enum values used (Poultry, Fish, Eggs are NOT in the enum — absent from map per PATTERNS.md correction #4).

**Stable sort (PITFALL H8):**
```csharp
.OrderByDescending(t => t.Score)
.ThenBy(t => t.RecipeId)
.ThenBy(t => t.RecipeName, StringComparer.OrdinalIgnoreCase)
```

**Authz predicate (canonical from RecipeMadeService.cs:75-77):**
```csharp
r => r.Cookbook.UserId == userId || r.Cookbook.Shares.Any(s => s.SharedWithUserId == userId)
```

**Data loading strategy:** `Repository<T>.FindAsync` translates EF predicates to SQL (authz/ingredient/tag predicates work at query time) but does not eagerly load navigation properties on returned entities. The service makes multiple focused repository calls and joins in memory — acceptable at trusted-LAN scale (<1000 recipes typical per PITFALL H7 with composite indexes from Phase 8).

**Canonical property names used:**
- `UserProfile.DietaryPreferencesJson` (NOT DietaryPreferences) — matches PromptBuilderService.cs:156-161 deserialization pattern
- `RecipeMade.CompletedAt` (NOT MadeAt or MadeAtUtc) — verified at RecipeMade.cs:13

### DependencyInjection.cs

Added `services.AddScoped<IPantryMatchService, PantryMatchService>();`. `AddApplication` signature unchanged: `public static IServiceCollection AddApplication(this IServiceCollection services)`.

### PantryMatchServiceTests.cs (TDD RED then GREEN)

14 test cases (9 `[Fact]` + 1 `[Theory]` with 5 `[InlineData]`):

| # | Test | Covers |
|---|------|--------|
| 1 | `RecencyPenalty_ExponentialDecay` | Formula at days 0/1/3/7/30 within tolerance 0.01 |
| 2 | `GetMatchesAsync_StableSort_TieBreaksByRecipeIdAscending` | Lower RecipeId first on equal score |
| 3 | `GetMatchesAsync_AppliesMinCoverageRatio_ExcludesLowCoverage` | 1/10 coverage below 0.6 threshold excluded |
| 4 | `GetMatchesAsync_NeverCooked_NoPenalty` | score = 1.0 for 5/5 match, no RecipeMade row |
| 5 | `GetMatchesAsync_RecentlyCooked_AppliesPenalty` | score ≈ 0.74 for 1-day-ago cook |
| 6 | `GetMatchesAsync_DietFilter_VeganExcludesMeatCategory` | Meat ingredient excluded for vegan |
| 7 | `GetMatchesAsync_DietFilter_VegetarianRequiresMatchingRecipeTag` | Positive tag match required |
| 8 | `GetMatchesAsync_UnknownDietLabel_SkipsNegativeFilter_KeepsPositiveTagMatch` | Unknown label → positive tag still filters |
| 9 | `GetMatchesAsync_OnlyReturnsAccessibleRecipes` | Owned + shared appear; unshared excluded |
| 10 | `GetMatchesAsync_RespectsResultCount` | Exactly 3 of 10 returned when ResultCount=3 |

Bootstrap pattern: in-memory SQLite (`UseSqlite("DataSource=:memory:")` + `OpenConnection()` + `EnsureCreated()`) from OwnershipTests, real `RecipeMadeService(db)` for recency reads.

## Deviations from Plan

### Auto-added: Additional Repository Parameters (Rule 2 — Missing Critical Functionality)

**Found during:** Task 1 planning / Task 2 implementation
**Issue:** The plan specified the constructor as `IRepository<Recipe>`, `IRepository<UserProfile>`, `IRecipeMadeService`, `PantryService`, `IOptions<PantryMatchOptions>`. However, `Repository<T>.FindAsync` does not eager-load navigation properties (RecipeIngredients, RecipeTags) on returned entities. Loading these via Recipe navigation properties would silently return empty collections in production.
**Fix:** Extended constructor to include `IRepository<RecipeIngredient>`, `IRepository<Ingredient>`, `IRepository<RecipeTag>`, `IRepository<CookbookShare>` — enabling correct separate round-trip loads with in-memory joins.
**Why not architectural:** No new table, no schema change, no new service boundary. The generic `IRepository<>` registration in `AddInfrastructure` (`services.AddScoped(typeof(IRepository<>), typeof(Repository<>))`) automatically resolves all added types — no additional DI line needed.
**Files modified:** `PantryMatchService.cs` (constructor), `PantryMatchServiceTests.cs` (BuildService helper updated to pass all repos)
**Commits:** f021086 (test updated), eeb393a (service updated)

## Known Stubs

None. All algorithm logic is fully implemented and tested. The `PantryMatchService` returns real scored results — no hardcoded/placeholder values flow to UI rendering.

## Threat Flags

None beyond what the plan's threat register covers. T-10-03-01 (Information Disclosure) is mitigated: the authz predicate `r.Cookbook.UserId == userId || r.Cookbook.Shares.Any(...)` runs server-side at the EF query layer before any scoring. Test 9 (`GetMatchesAsync_OnlyReturnsAccessibleRecipes`) gates this.

## TDD Gate Compliance

- RED gate commit: `f021086 — test(10-03): add failing PantryMatchServiceTests (RED gate)` — build failed with CS0246 (PantryMatchService type not found)
- GREEN gate commit: `eeb393a — feat(10-03): implement PantryMatchService and register in AddApplication (GREEN)` — all 14 tests pass

## Self-Check: PASSED

- `tests/CookBot.Tests/Services/PantryMatchServiceTests.cs` — FOUND
- `src/CookBot.Application/Services/PantryMatchService.cs` — FOUND
- `src/CookBot.Application/DependencyInjection.cs` (modified) — FOUND
- Commit f021086 — FOUND (test(10-03): add failing PantryMatchServiceTests)
- Commit eeb393a — FOUND (feat(10-03): implement PantryMatchService)
- `dotnet test --filter PantryMatchServiceTests` — Passed: 14, Failed: 0
- `dotnet build FreelovesCookBot.sln` — 0 warnings, 0 errors
