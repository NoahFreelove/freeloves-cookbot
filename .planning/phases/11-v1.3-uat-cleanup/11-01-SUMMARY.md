---
phase: 11-v1.3-uat-cleanup
plan: "01"
subsystem: Application.Services
tags: [unit-conversion, display, temperature, CLEANUP-04]
dependency_graph:
  requires:
    - IUnitConverter (CookBot.Domain.Interfaces) — already built; delegated to for weight/volume
    - UnitConversionService (existing) — the IUnitConverter implementation
    - UnitParser (existing) — unit normalisation + non-convertible passthrough
    - FractionFormatter (existing) — cooking-rounded output formatting
    - StepTemperature + TemperatureUnit (CookBot.Domain.Recipes) — reused, not duplicated
    - UnitSystem enum (CookBot.Domain.Enums)
  provides:
    - RecipeUnitDisplayService — display-time conversion facade; Wave 2 (Plan 11-04) wires this into RecipeView/CookingMode/AiChat
  affects: []
tech_stack:
  added: []
  patterns:
    - TDD RED/GREEN cycle (failing tests first, then implementation)
    - Pure Application-layer service (stateless, injectable singleton)
    - Delegate-to-existing-converter pattern (IUnitConverter reuse)
    - Gas mark → °C → °F table (standard UK/EU gas mark reference)
    - Cooking rounding: °F to nearest 25 (oven temps), °C to nearest 5
key_files:
  created:
    - src/CookBot.Application/Services/RecipeUnitDisplayService.cs
    - tests/CookBot.Tests/Services/RecipeUnitDisplayServiceTests.cs
  modified:
    - src/CookBot.Application/DependencyInjection.cs
decisions:
  - "Temperature cooking rounding: °F to nearest 25 for oven temps (≥100°F), nearest 5 for lower temps; °C to nearest 5. Results: 200°C=392°F rounds to 400°F, 180°C=356°F rounds to 350°F."
  - "Canadian UnitSystem target uses Celsius for oven temperatures (per CONTEXT §CLEANUP-04), matching RecipeDocument canonical expectation. Weight=grams, volume=cups."
  - "Gas mark table: integer keys 1-9 with standard °C reference values; out-of-range gas marks clamped to min/max."
  - "Non-convertible passthrough: UnitParser.TryParse returns null → converter not called → original amount+unit passed through. Never throws."
  - "IUnitConverter.IsWeight / IsVolume used to determine source unit family before selecting destination unit per UnitSystem."
metrics:
  duration: "~15 minutes"
  completed: "2026-06-05T20:33:00Z"
  tasks_completed: 2
  tasks_total: 2
  files_created: 2
  files_modified: 1
---

# Phase 11 Plan 01: RecipeUnitDisplayService Summary

**One-liner:** Pure Application-layer facade that wraps the existing IUnitConverter for weight/volume and adds a °C↔°F↔gas-mark temperature table, returning cooking-rounded display strings without ever mutating the canonical RecipeDocument.

## Tasks Completed

| Task | Name | Commit | Files |
|------|------|--------|-------|
| 1 | RecipeUnitDisplayService ingredient conversion + DI registration | dc826ff | `RecipeUnitDisplayService.cs`, `DependencyInjection.cs` |
| 2 | Temperature conversion table + RecipeUnitDisplayServiceTests | 554df46 | `RecipeUnitDisplayServiceTests.cs` |

## What Was Built

### RecipeUnitDisplayService

New pure Application-layer service (`CookBot.Application.Services`) injected with `IUnitConverter`.

**`FormatIngredientAmount(double amount, string unit, UnitSystem target)`**
- Delegates weight/volume conversion to the existing `IUnitConverter` (g↔oz/lb, ml↔cup) — zero new conversion math for that path.
- Per-`UnitSystem` target-unit table mirrors `PromptBuilderService.ResolveUnitSystem`:
  - Imperial: weight=oz, volume=cups
  - Metric: weight=g, volume=mL
  - Canadian: weight=g, volume=cups (mixed — metric weight, imperial volume)
- Non-convertible units (pinch, clove, to taste, empty, amount≤0) pass through unchanged via `UnitParser.TryParse` returning null.
- All numeric output runs through `FractionFormatter.Format` (cooking rounding — no "13.9876 oz").

**`FormatTemperature(StepTemperature temp, UnitSystem target)`**
- Net-new temperature path: linear °C↔°F formula plus a 9-entry gas mark → °C lookup table (gas 1–9).
- All sources resolve to Celsius first, then convert to target scale.
- Target scale: Imperial → °F, Metric → °C, Canadian → °C.
- Cooking rounding: °F rounded to nearest 25 for oven temps (≥100°F), °C rounded to nearest 5.
- Reuses `TemperatureUnit {F, C, Gas}` from `StepTemperature.cs` — no duplicate enum.

### DI Registration

`services.AddSingleton<RecipeUnitDisplayService>()` added adjacent to `IUnitConverter` registration in `DependencyInjection.AddApplication`.

### Tests (20 cases)

Reference values verified:
- 100 g → ≈3.53 oz (Imperial) ✓
- 250 ml → ≈1 cup (Imperial) ✓
- 200°C → 400°F (cook-rounded) ✓
- 180°C → 350°F (cook-rounded) ✓
- Gas mark 6 → 200°C (Metric) ✓
- Gas mark 6 → 400°F (Imperial) ✓
- Gas mark 3 → 170°C ✓

Passthrough cases verified:
- "to taste", "clove", "a pinch", empty unit, amount=0 all pass through without throwing.

## Deviations from Plan

None — plan executed exactly as written. The only judgment call was temperature rounding (to nearest 25 for °F oven temps), which matches the CONTEXT "cook-rounded" specification (200°C=392°F → 400°F, 180°C=356°F → 350°F).

## Known Stubs

None. This plan builds the conversion engine only; Wave 2 (Plan 11-04) wires it into the render call sites.

## Threat Flags

None. This is a pure display-time transform: no network endpoints, no auth paths, no file access, no schema changes. Returns formatted strings only; no canonical data written.

## Self-Check: PASSED

- `src/CookBot.Application/Services/RecipeUnitDisplayService.cs` — FOUND
- `tests/CookBot.Tests/Services/RecipeUnitDisplayServiceTests.cs` — FOUND
- `src/CookBot.Application/DependencyInjection.cs` — modified (RecipeUnitDisplayService registered)
- Commit dc826ff — EXISTS (`feat(11-01): add RecipeUnitDisplayService with ingredient weight/volume conversion`)
- Commit 554df46 — EXISTS (`test(11-01): add RecipeUnitDisplayServiceTests with temperature conversion + passthrough cases`)
- `dotnet test --filter "FullyQualifiedName~RecipeUnitDisplayService"` — Passed! 20/20
- `dotnet build FreelovesCookBot.sln` — Build succeeded, 0 errors
