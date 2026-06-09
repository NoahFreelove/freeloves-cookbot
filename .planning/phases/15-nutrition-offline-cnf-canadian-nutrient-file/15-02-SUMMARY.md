---
phase: 15
plan: "02"
subsystem: nutrition-data-contracts
tags: [domain-entities, dto, normalizer, tdd, cnf, nutrition]
dependency_graph:
  requires: []
  provides:
    - CnfFood entity (FoodId PK, four per-100g macros, NormalizedDescription)
    - CnfConversionFactor entity (FoodId FK, MeasureDescription, ConversionFactorValue)
    - RecipeNutritionCache entity (RecipeId 1:1 FK, hash, staleness, total+per-serving macros, coverage, PerIngredientMatchJson)
    - NutritionInfoDto sealed record (per-serving value object)
    - IngredientNormalizer static class (single normalization owner)
  affects:
    - "15-03: seed loader uses IngredientNormalizer.Normalize to pre-compute NormalizedDescription"
    - "15-05: runtime matcher uses IngredientNormalizer.Normalize for ingredient name lookup"
    - "15-06: projector receives NutritionInfoDto as optional third parameter"
    - "15-07: nutrition panel constructs NutritionInfoDto from RecipeNutritionCache"
tech_stack:
  added: []
  patterns:
    - "Pure POCO Domain entities with no framework refs (mirrors RecipePhoto.cs conventions)"
    - "sealed record DTO for pure value objects (mirrors PantryMatchResult)"
    - "Static normalizer class mirroring IngredientResolver.Normalize pipeline (TDD RED/GREEN)"
    - "Whole-word deny-list stripping via Regex.Escape + \b word-boundary (ReDoS-safe)"
key_files:
  created:
    - src/CookBot.Domain/Entities/CnfFood.cs
    - src/CookBot.Domain/Entities/CnfConversionFactor.cs
    - src/CookBot.Domain/Entities/RecipeNutritionCache.cs
    - src/CookBot.Application/DTOs/NutritionInfoDto.cs
    - src/CookBot.Application/Services/IngredientNormalizer.cs
    - tests/CookBot.Tests/Nutrition/IngredientNormalizerTests.cs
  modified: []
decisions:
  - "Multi-word deny-list tokens ('room temperature', 'good quality', 'to taste', 'for garnish', 'plus more') listed first in DenyList array so phrase is matched before constituent single words — avoids leaving orphaned tokens"
  - "Comma/semicolon stripping added to Normalize pipeline (step 3) to handle CNF genus-first descriptions like 'Grains, wheat flour, white, all purpose, enriched'"
  - "IngredientNormalizer is a static class (not DI service) — same pattern as IngredientResolver; usable by both the seeder (no DI) and runtime (DI context)"
metrics:
  duration: "~8 minutes"
  completed: "2026-06-07"
  tasks_completed: 3
  files_created: 6
---

# Phase 15 Plan 02: Data Contracts + Shared Normalizer Summary

**One-liner:** Pure POCO domain entities (CnfFood, CnfConversionFactor, RecipeNutritionCache) + NutritionInfoDto value object + tested IngredientNormalizer with D-15-05 deny-list, giving Plans 03-07 fixed field contracts and a single normalization owner.

## Tasks Completed

| Task | Name | Commit | Files |
|------|------|--------|-------|
| 1 | Three CNF/nutrition Domain entities | 4a141b0 | CnfFood.cs, CnfConversionFactor.cs, RecipeNutritionCache.cs |
| 2 | NutritionInfoDto per-serving value object | 04d28e0 | NutritionInfoDto.cs |
| 3 (RED) | IngredientNormalizer failing tests | 85ee7e9 | IngredientNormalizerTests.cs |
| 3 (GREEN) | IngredientNormalizer implementation | bd6a2d6 | IngredientNormalizer.cs |

## What Was Built

### Domain Entities (CookBot.Domain — no framework refs)

**CnfFood** — CNF food record with `FoodId` as PK (= CNF FoodCode, not the internal FoodID). Stores `FoodDescription`, `NormalizedDescription` (pre-computed at seed time), `FoodGroup?`, and four verbatim per-100g doubles: `EnergyKcalPer100g`, `ProteinGPer100g`, `FatGPer100g`, `CarbGPer100g`. Principal-side `ICollection<CnfConversionFactor> ConversionFactors = []`. XML doc states values are OGL-Canada verbatim.

**CnfConversionFactor** — FK child with surrogate `int Id`, `int FoodId` FK, `string MeasureDescription`, `double ConversionFactorValue` (grams_in_measure / 100), `CnfFood Food = null!` nav.

**RecipeNutritionCache** — 1:1 with Recipe keyed by `RecipeId`. Holds `CanonicalDocHash` (SHA-256 hex), `IsStale`, total energy/macros, `int? Servings` snapshot, per-serving energy/macros, `MatchedIngredients`, `TotalIngredients`, `PerIngredientMatchJson` (TEXT), `ComputedAt`, `Recipe Recipe = null!` nav. XML doc encodes the hard invariant: this entity is NEVER serialized into CanonicalDocumentJson.

### Application DTO

**NutritionInfoDto** — `public sealed record NutritionInfoDto(double CaloriesPerServing, double ProteinGPerServing, double FatGPerServing, double CarbGPerServing)`. Pure value type in `CookBot.Application.DTOs`. No EF, no DI — consumable by the pure static `JsonLdRecipeProjector` (D-15-13 / NUTR-06).

### Application Service + Tests (TDD)

**IngredientNormalizer** — `public static class` in `CookBot.Application.Services`. Single `public static string Normalize(string name)` method. Pipeline:
1. `ToLowerInvariant` + `Trim`
2. Replace `[-_]` with space (hyphenated compounds decompose; "room-temperature" → "room temperature")
3. Strip commas/semicolons (CNF genus-first descriptions)
4. Collapse `\s+`
5. Strip D-15-05 deny-list tokens as whole words via `\b{Regex.Escape(token)}\b` (ReDoS-safe — all linear-time regexes, T-15-08)
6. Re-collapse + trim

Deny-list strip: chopped, minced, diced, sliced, shredded, grated, ground, sifted, packed, finely, roughly, freshly, room temperature, cold, warm, good quality, good, fine, coarse, large, small, medium, ripe, organic, to taste, optional, divided, for garnish, plus more.

NOT in deny-list (nutrition-changing): unsalted, salted, skinless, lowfat, low-fat, whole, light, heavy.

**48 tests** covering all plan behavior cases, strip cases, keep cases, CNF description tokenization, and whole-word guard ("groundnut" not damaged by "ground" deny-list token).

## Verification

- `dotnet build src/CookBot.Domain/CookBot.Domain.csproj` — Build succeeded, 0 errors
- `dotnet build src/CookBot.Application/CookBot.Application.csproj` — Build succeeded, 0 errors
- `dotnet test --filter IngredientNormalizer` — Passed: 48, Failed: 0
- `grep "using Microsoft" CnfFood.cs CnfConversionFactor.cs RecipeNutritionCache.cs` — zero hits (no framework refs in domain)

## Deviations from Plan

### Auto-added Missing Functionality

**1. [Rule 2 - Missing] Comma/semicolon stripping in Normalize pipeline**
- **Found during:** Task 3 (implementing IngredientNormalizer)
- **Issue:** The plan's `<behavior>` case "Grains, wheat flour, white, all purpose, enriched" required comma handling, but the base `IngredientResolver.Normalize` pipeline and the PATTERNS.md code snippet only covered `[-_]` and `\s+` — commas would remain in the normalized string, breaking CNF description tokenization.
- **Fix:** Added Step 3 (`Regex.Replace(lower, @"[,;]", " ")`) before the whitespace-collapse step. Linear-time, no ReDoS risk.
- **Files modified:** IngredientNormalizerTests.cs (test for comma removal), IngredientNormalizer.cs (step 3)

**2. [Rule 1 - Order] Multi-word deny tokens listed before single-word constituents**
- **Found during:** Task 3 test authoring
- **Issue:** If "good" (single word) were stripped before "good quality" (phrase), the phrase match would fail because "good" would already be a space and "quality" would become an orphaned token. The multi-word phrases must run first.
- **Fix:** DenyList array begins with multi-word tokens: "room temperature", "good quality", "to taste", "for garnish", "plus more" — then single-word tokens. No test assertions broken; the ordered array is self-documenting.

## TDD Gate Compliance

- RED gate: `test(15-02)` commit `85ee7e9` — 48 tests, all failing (CS0103 compile error confirms class absent)
- GREEN gate: `feat(15-02)` commit `bd6a2d6` — 48 tests passing

## Threat Flags

None. No new network endpoints, auth paths, file access, or schema changes at trust boundaries introduced by this plan. The normalizer operates on bounded in-memory strings only (T-15-08 ReDoS mitigated by linear-time regexes with Regex.Escape).

## Self-Check: PASSED

- [x] `src/CookBot.Domain/Entities/CnfFood.cs` — exists
- [x] `src/CookBot.Domain/Entities/CnfConversionFactor.cs` — exists
- [x] `src/CookBot.Domain/Entities/RecipeNutritionCache.cs` — exists
- [x] `src/CookBot.Application/DTOs/NutritionInfoDto.cs` — exists
- [x] `src/CookBot.Application/Services/IngredientNormalizer.cs` — exists
- [x] `tests/CookBot.Tests/Nutrition/IngredientNormalizerTests.cs` — exists
- [x] Commit 4a141b0 — feat(15-02): domain entities
- [x] Commit 04d28e0 — feat(15-02): NutritionInfoDto
- [x] Commit 85ee7e9 — test(15-02): RED tests
- [x] Commit bd6a2d6 — feat(15-02): GREEN implementation
