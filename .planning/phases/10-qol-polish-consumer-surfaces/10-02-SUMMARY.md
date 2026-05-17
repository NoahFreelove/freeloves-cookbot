---
phase: 10-qol-polish-consumer-surfaces
plan: "02"
subsystem: pantry-match-contracts
tags: [pantry-match, contracts, application-layer, configuration, options-pattern]
dependency_graph:
  requires: ["10-01"]
  provides: ["10-03", "10-04"]
  affects: ["src/CookBot.Application", "src/CookBot.Web"]
tech_stack:
  added: []
  patterns: [IOptions-bound POCO, file-scoped namespace, sealed record positional ctor, Configure<T> binding]
key_files:
  created:
    - src/CookBot.Application/DTOs/PantryMatchOptions.cs
    - src/CookBot.Application/DTOs/PantryMatchResult.cs
    - src/CookBot.Application/Services/IPantryMatchService.cs
  modified:
    - src/CookBot.Web/appsettings.json
    - src/CookBot.Web/Program.cs
decisions:
  - "Registered Configure<PantryMatchOptions> in Program.cs alongside CookBotSettings binding (PATTERNS.md correction #6 — AddApplication signature unchanged)"
  - "PantryMatchResult sealed record positional parameter order mirrors Home.razor.cs HomePantryMatch so Plan 10-04 Home swap is mechanical"
  - "IPantryMatchService placed in CookBot.Application.Services namespace; depends on PantryMatchResult DTO from same project (no cross-layer reference needed for interface)"
metrics:
  duration_minutes: 8
  completed: "2026-05-17"
  tasks_completed: 3
  tasks_total: 3
  files_created: 3
  files_modified: 2
---

# Phase 10 Plan 02: Pantry-Match Contracts and Configuration Scaffold Summary

**One-liner:** IOptions-bound `PantryMatchOptions` POCO, `PantryMatchResult` sealed record, `IPantryMatchService` interface, and `CookBot:PantryMatch` appsettings block with DI binding — full contracts scaffold enabling Plans 10-03 and 10-04 to proceed in parallel.

## Tasks Completed

| Task | Name | Commit | Files |
|------|------|--------|-------|
| 1 | Create PantryMatchOptions and PantryMatchResult DTOs | 2fcd646 | `DTOs/PantryMatchOptions.cs`, `DTOs/PantryMatchResult.cs` |
| 2 | Create IPantryMatchService interface | b628299 | `Services/IPantryMatchService.cs` |
| 3 | Add CookBot:PantryMatch to appsettings.json and Configure binding in Program.cs | 2319925 | `appsettings.json`, `Program.cs` |

## What Was Built

### PantryMatchOptions (D-46)

`src/CookBot.Application/DTOs/PantryMatchOptions.cs` — sealed class with four properties and defaults baked in:
- `RecencyPenaltyWeight = 0.3` — D-44 linear-decay coefficient
- `RecencyHalfLifeDays = 7.0` — D-44 decay half-life in days
- `MinCoverageRatio = 0.6` — minimum pantry coverage ratio before scoring
- `ResultCount = 3` — maximum results returned

XML docs on each property reference D-44/D-46. Safe-start: defaults apply when the `CookBot:PantryMatch` section is absent.

### PantryMatchResult (mirrors HomePantryMatch)

`src/CookBot.Application/DTOs/PantryMatchResult.cs` — sealed positional record with seven fields in the exact order matching `Home.razor.cs:470 HomePantryMatch`: `RecipeId, RecipeName, MatchedCount, TotalCount, Score, PhotoUrl?, FirstMissingIngredientName?`. Plan 10-04's Home swap is purely mechanical.

### IPantryMatchService

`src/CookBot.Application/Services/IPantryMatchService.cs` — interface with single method `Task<IReadOnlyList<PantryMatchResult>> GetMatchesAsync(int userId, CancellationToken ct = default)`. XML docs explain: stable sort per PITFALL H8 (score desc, recipeId asc, recipe-name asc), empty-list behavior, dietary-preference sourcing inside the service.

### appsettings.json + Program.cs

Added `CookBot:PantryMatch` block inside the existing top-level `CookBot` object after `AiPricingVerifiedDate`. Added `builder.Services.Configure<PantryMatchOptions>(builder.Configuration.GetSection("CookBot:PantryMatch"))` immediately after the `CookBotSettings` Configure call in Program.cs. `AddApplication` signature unchanged per PATTERNS.md correction #6.

## Verification Results

- Source assertions: all passed
- JSON validity: `python3 -c "import json; json.load(open('src/CookBot.Web/appsettings.json'))"` exits 0
- `dotnet build src/CookBot.Application/CookBot.Application.csproj`: 0 warnings, 0 errors
- `dotnet build FreelovesCookBot.sln`: 0 errors (4 pre-existing EF/SDK warnings in test project, unrelated)
- `AddApplication` signature confirmed unchanged: `public static IServiceCollection AddApplication(this IServiceCollection services)`

## Deviations from Plan

None — plan executed exactly as written.

## Known Stubs

None. This plan is a pure contracts scaffold — no stub values flow to UI rendering. The implementation (Plan 10-03) and Home swap (Plan 10-04) will wire the live data.

## Threat Flags

None. The `CookBot:PantryMatch` block contains only numerical tuning knobs; no secrets or credentials introduced. T-10-02-01 and T-10-02-02 are accepted per the plan threat register.

## Self-Check: PASSED

- `src/CookBot.Application/DTOs/PantryMatchOptions.cs` — FOUND
- `src/CookBot.Application/DTOs/PantryMatchResult.cs` — FOUND
- `src/CookBot.Application/Services/IPantryMatchService.cs` — FOUND
- Commits 2fcd646, b628299, 2319925 — FOUND
