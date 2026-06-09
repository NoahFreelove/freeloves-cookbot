---
phase: 15-nutrition-offline-cnf-canadian-nutrient-file
plan: "04"
subsystem: Application/Services
tags: [nutrition, density, tdd, nutr-03]
dependency_graph:
  requires: []
  provides: [IngredientDensityProvider, density-fallback-table]
  affects: [NutritionService]
tech_stack:
  added: []
  patterns: [static-readonly-dictionary, nullable-return, ordinal-ignore-case-lookup, singleton-di]
key_files:
  created:
    - src/CookBot.Application/Services/IngredientDensityProvider.cs
    - tests/CookBot.Tests/Nutrition/IngredientDensityProviderTests.cs
  modified:
    - src/CookBot.Application/DependencyInjection.cs
decisions:
  - "27 entries (exceeds ≥23 minimum) including aliases (granulated white sugar, unsalted butter, plain yogurt) for natural language name coverage"
  - "EntryCount exposed as static property for count assertion without instantiation"
  - "KA values preferred over FAO for volume-displaced ingredients (vegetable oil, heavy cream) per RESEARCH §disagreements-to-flag"
metrics:
  duration: 15m
  completed: "2026-06-07"
---

# Phase 15 Plan 04: IngredientDensityProvider — Curated g/mL Fallback Table Summary

Curated per-ingredient density fallback table (NUTR-03) — the water-density bug guard. `IngredientDensityProvider` returns g/mL for 27 common cooking ingredients (King Arthur Baking HIGH confidence + FAO/INFOODS MEDIUM confidence sourced) and `null` for unknowns, so the matcher can convert volume→mass with ingredient-specific density and never fall back to 1.0 g/mL.

## Tasks Completed

| # | Task | Commit | Files |
|---|------|--------|-------|
| 1a | RED: failing tests | f091985 | tests/CookBot.Tests/Nutrition/IngredientDensityProviderTests.cs |
| 1b | GREEN: implementation + DI | c89b35a | src/CookBot.Application/Services/IngredientDensityProvider.cs, src/CookBot.Application/DependencyInjection.cs |

## What Was Built

`IngredientDensityProvider` — a `public sealed class` in `CookBot.Application.Services` with:

- A `private static readonly Dictionary<string, double>` initialized with `StringComparer.OrdinalIgnoreCase` containing **27 entries** (≥23 required)
- Entries grouped by category: Flours (5), Sugars (3+alias), Fats/Oils (4), Dairy (6+aliases), Syrups (2), Baking Staples (6), Additional (4)
- Inline `// KA` and `// FAO` source attribution per entry
- `public double? GetDensityGPerMl(string normalizedName)` — returns value or `null`
- `public static int EntryCount` — exposed for ≥23-entry count assertion

**SC3 flour anchor:** `all-purpose flour` = 0.507 g/mL → ~120 g/US cup. Combined with CNF's 364 kcal/100 g and US-cup scale factor (×0.9464), this produces ~455 kcal/cup — exactly the SC3 anchor verified against the live CNF API.

**DI registration:** `services.AddSingleton<IngredientDensityProvider>()` in `Application/DependencyInjection.cs` with `// Phase 15 / NUTR-03` comment alongside the IUnitConverter registration.

## Test Coverage

31 tests in `IngredientDensityProviderTests` — all pass:

- **≥20 named-ingredient assertions**: 27 ingredient density tests (each within ±0.01 of RESEARCH values)
- **SC3/P5 guard**: `all-purpose flour` ∈ [0.45, 0.55] AND ≠ 1.0 (explicit no-water-density assertion)
- **Null-on-unknown**: `GetDensityGPerMl("unobtainium")` returns null
- **Case-insensitive lookup**: `"All-Purpose Flour"` resolves to same value as `"all-purpose flour"`
- **Count assertion**: `EntryCount >= 23` (actual: 27)

## TDD Gate Compliance

- RED gate: `test(15-04)` commit f091985 — failing build (CS0246) confirmed before implementation
- GREEN gate: `feat(15-04)` commit c89b35a — all 31 tests pass
- REFACTOR: no separate commit needed; code was structured with category groupings and source comments from the GREEN pass

## Deviations from Plan

None — plan executed exactly as written. Density values verbatim from RESEARCH §"Research Target 3". 27 entries implemented (4 beyond the required ≥23 baseline, using the additional densities listed in RESEARCH's "Additional densities needed for ≥20-ingredient unit test coverage" note).

## Known Stubs

None. Provider is fully implemented; no placeholder values.

## Threat Flags

None. `IngredientDensityProvider` is a bounded dictionary with no network surface, no execution surface, and no injection risk. T-15-09 (water-density fallback) is mitigated: no entry is 1.0 g/mL, and unknown ingredients return `null` forcing the caller to mark the conversion unmatched.

## Self-Check: PASSED

- FOUND: `src/CookBot.Application/Services/IngredientDensityProvider.cs`
- FOUND: `tests/CookBot.Tests/Nutrition/IngredientDensityProviderTests.cs`
- FOUND: commit f091985 (RED)
- FOUND: commit c89b35a (GREEN)
- 31/31 tests pass
