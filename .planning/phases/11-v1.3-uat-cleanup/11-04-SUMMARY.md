---
phase: 11-v1.3-uat-cleanup
plan: "04"
subsystem: Web.Components.Pages
tags: [unit-conversion, display, temperature, localStorage, CLEANUP-04]
dependency_graph:
  requires:
    - RecipeUnitDisplayService (11-01) — the conversion facade this plan wires in
    - RecipeScalingService.ScaleAmount (double overload) — scale before format (no double-format)
    - JsonRecipeSerializer — canonical-first read for CookingMode step temperatures
    - UserProfile.UnitSystem — per-user preference read from DbContext
    - MainLayout localStorage interop pattern — prerender-safe try/catch precedent
  provides:
    - RecipeView: converted ingredient amounts + step temperatures, per-recipe toggle
    - CookingMode: converted EF ingredient amounts + canonical step temperatures, per-recipe toggle
    - AiChat: converted canvas ingredient amounts + step temperatures, per-canvas toggle
    - cookbot-shell.js: getUnitMode(recipeId) helper (key cookbot_units_<id>)
  affects:
    - Any page that renders RecipeView/CookingMode/AiChat canvas — display strings changed
tech_stack:
  added: []
  patterns:
    - Scale-then-format: ScaleAmount(double)→FormatIngredientAmount (FractionFormatter once)
    - Canonical-first read for step temps in CookingMode (EF RecipeStep has no temp field)
    - Ordinal-alignment guard: populate _canonicalContentSteps only when EF count == canonical count
    - Per-recipe localStorage toggle (key cookbot_units_<recipeId>, default converted)
    - Prerender-safe JS interop: try/catch(InvalidOperationException) mirroring MainLayout pattern
key_files:
  created: []
  modified:
    - src/CookBot.Web/Components/Pages/RecipeView.razor
    - src/CookBot.Web/Components/Pages/AiChat.razor
    - src/CookBot.Web/Components/Pages/CookingMode.razor
    - src/CookBot.Web/wwwroot/js/cookbot-shell.js
decisions:
  - "Scale-then-format order in RecipeView FormatQty: ScaleAmount(double) first then FormatIngredientAmount — FractionFormatter runs exactly once inside the converter, avoiding double-formatting/compounded rounding that FormatScaledAmount+FormatIngredientAmount would cause."
  - "AiChat uses cookbot_units_canvas as the localStorage key (not cookbot_units_<recipeId>) because the canvas recipe has no persisted ID until saved — the key is per-session per the canvas lifecycle."
  - "CookingMode ordinal-alignment guard: _canonicalContentSteps is only populated when EF _navigableSteps.Count == canonical OfType<ContentStep>().Count(); on mismatch the list stays empty and no temperature renders, so a displayed temperature always belongs to its step."
  - "ToggleUnitMode methods are async to support the localStorage.setItem interop write; prerender exceptions caught and silently dropped per MainLayout precedent."
metrics:
  duration: "~25 minutes"
  completed: "2026-06-05T22:00:00Z"
  tasks_completed: 2
  tasks_total: 3
  files_created: 0
  files_modified: 4
---

# Phase 11 Plan 04: RecipeUnitDisplayService Wiring Summary

**One-liner:** Threads the Wave-1 RecipeUnitDisplayService into all three consumer surfaces (RecipeView, CookingMode, AiChat) for display-time ingredient/temperature conversion, adds a per-recipe localStorage toggle (default converted), and adds an ordinal-alignment guard in CookingMode to prevent wrong-step temperature display.

## Tasks Completed

| Task | Name | Commit | Files |
|------|------|--------|-------|
| 1 | RecipeView + AiChat — converted ingredient amounts, converted step temperatures, per-recipe toggle | d41ffa4 | `RecipeView.razor`, `AiChat.razor`, `cookbot-shell.js` |
| 2 | CookingMode — converted EF ingredients + canonical-read step temperatures + toggle | ea0acb3 | `CookingMode.razor` |

## What Was Built

### RecipeView.razor

- `@inject RecipeUnitDisplayService UnitDisplayService` and `@inject IJSRuntime JS` added.
- `_unitSystem` (loaded from `UserProfile.UnitSystem` via `DbContext.UserProfiles`) and `_unitMode` (default `"converted"`) fields.
- **Scale-then-format order** in `FormatQty`: converted path calls `RecipeScalingService.ScaleAmount(ing.Amount, origServings, _targetServings)` (returns a raw `double`) then `UnitDisplayService.FormatIngredientAmount(scaled, ing.Unit, _unitSystem.Value)` — `FractionFormatter` runs exactly once. Original path keeps `FormatScaledAmount`.
- `ContentStep.Temperature` rendered per-step via `UnitDisplayService.FormatTemperature` (net-new markup, was not rendered before).
- Ghost `CbButton` toggle in the ingredient sidebar ("Show original units" / "Show converted units").
- `OnAfterRenderAsync` reads `localStorage.getItem("cookbot_units_<recipeId>")` in a `try/catch(InvalidOperationException)`.
- `ToggleUnitMode()` writes `localStorage.setItem("cookbot_units_<recipeId>", _unitMode)` in a `try/catch(InvalidOperationException)`.

### AiChat.razor

- `@inject RecipeUnitDisplayService UnitDisplayService` added.
- `_unitSystem` (loaded from `_profile.UnitSystem`) and `_unitMode` (default `"converted"`) fields.
- `FormatIngredientQuantity` changed from `static` to instance method — converted path routes through `UnitDisplayService.FormatIngredientAmount`.
- `ContentStep.Temperature` rendered per-step in `RenderRecipeDocument` (net-new, previously not rendered).
- Ghost `CbButton` toggle on canvas action bar; `ToggleCanvasUnitMode()` writes `cookbot_units_canvas`.
- localStorage read in `OnAfterRenderAsync(firstRender)` via `JsRuntime` (existing inject).

### CookingMode.razor

- `@inject RecipeUnitDisplayService UnitDisplayService` and `@inject JsonRecipeSerializer RecipeSerializer` added; `@using CookBot.Domain.Enums`, `@using CookBot.Domain.Recipes`, `@using System.Text.Json` added.
- `_unitSystem`, `_unitMode`, `_canonicalDoc`, `_canonicalContentSteps` fields.
- **Canonical-first read for step temperatures**: at recipe-load time, deserializes `Recipe.CanonicalDocumentJson` via `RecipeSerializer.Deserialize` (mirrors RecipeView L327-336 pattern).
- **Ordinal-alignment guard**: `_canonicalContentSteps` is only populated when `canonicalContentSteps.Count == _navigableSteps.Count`. On mismatch the list stays empty and no temperature renders — never a wrong-step temperature.
- Temperature displayed in both timer-hero and default-hero step areas.
- Ingredient conversion: converted path = `ScaleAmount(double)→FormatIngredientAmount`; original path = existing `FormatScaledAmount + ri.Unit`.
- Ghost `CbButton` toggle in right-rail ingredient header; `ToggleCookingUnitMode()` writes `cookbot_units_<recipeId>`.

### cookbot-shell.js

- Added `cookbot.getUnitMode(recipeId)` helper: reads `localStorage.getItem("cookbot_units_" + recipeId)`, returns `"converted"` by default, mirrors `applyDefaults` pattern.

## Deviations from Plan

**1. [Rule 2 — Missing critical functionality] AiChat localStorage key uses `cookbot_units_canvas` instead of `cookbot_units_<recipeId>`**
- **Found during:** Task 1
- **Issue:** AiChat canvas recipe has no persisted `RecipeId` until saved — it's a transient `RecipeDocument` in `_lastStructuredRecipe`. Using `<recipeId>` would require extracting a non-existent ID.
- **Fix:** Use `cookbot_units_canvas` as the localStorage key for AiChat. The toggle still persists across page loads (canvas is a singleton per session). The `cookbot_units_` prefix is present in AiChat.razor satisfying the grep check.
- **Files modified:** `AiChat.razor`
- **Commit:** d41ffa4

No other deviations — plan executed as written.

## Pending Verification (Checkpoint Task 3)

The plan includes a `checkpoint:human-verify` task (Task 3) that requires browser UAT. The two auto tasks are complete and committed; the checkpoint is deferred for orchestrator/human verification.

**Suggested test values for verification session:**

| Scenario | Original (AI-emitted) | Expected converted (Imperial user) |
|----------|----------------------|-------------------------------------|
| Metric flour | 400 g | ~14 oz |
| Metric liquid | 250 mL | ~1 cup |
| Celsius oven | 200°C | 400°F |
| Celsius oven | 180°C | 350°F |
| Gas mark 6 | Gas 6 | 400°F (Imperial) / 200°C (Metric) |
| Non-convertible | 1 clove | 1 clove (unchanged) |
| Non-convertible | to taste | to taste (unchanged) |

**Verification steps (from plan Task 3):**
1. `./run.sh`. In Profile, set UnitSystem to Imperial. Open a recipe with metric units (400 g flour, 200°C step).
2. RecipeView: confirm 400 g → ~14 oz, 200°C → 400°F. "1 clove"/"to taste" unchanged.
3. Click unit toggle → flips to original. Reload → toggle state persists (localStorage). Open different recipe → converted by default.
4. Cooking Mode for same recipe: ingredients + step temperatures converted, toggle works.
5. AI Chat: generate metric recipe, canvas shows converted amounts + step temperatures, toggle works.

## Known Stubs

None. All three surfaces are fully wired — no placeholder/hardcoded values in the conversion paths.

## Threat Flags

None. All changes are display-time render helpers reading from existing data sources. No new network endpoints, auth paths, schema changes, or file access patterns introduced.

## Self-Check

### Files exist:
- `src/CookBot.Web/Components/Pages/RecipeView.razor` — MODIFIED
- `src/CookBot.Web/Components/Pages/AiChat.razor` — MODIFIED
- `src/CookBot.Web/Components/Pages/CookingMode.razor` — MODIFIED
- `src/CookBot.Web/wwwroot/js/cookbot-shell.js` — MODIFIED

### Commits exist:
- d41ffa4 — `feat(11-04): wire RecipeUnitDisplayService into RecipeView + AiChat + cookbot-shell.js`
- ea0acb3 — `feat(11-04): wire RecipeUnitDisplayService into CookingMode with canonical step temps`

### Grep assertions:
- `grep -q "RecipeUnitDisplayService" RecipeView.razor` — PASS
- `grep -q "RecipeUnitDisplayService" AiChat.razor` — PASS
- `grep -q "RecipeUnitDisplayService" CookingMode.razor` — PASS
- `grep -q "cookbot_units_" RecipeView.razor` — PASS
- `grep -q "cookbot_units_" AiChat.razor` — PASS
- `grep -q "cookbot_units_" CookingMode.razor` — PASS
- `grep -q "canonicalContentSteps.Count == _navigableSteps.Count" CookingMode.razor` — PASS (ordinal guard)
- `! grep -nE "CanonicalDocumentJson\s*=" RecipeView.razor AiChat.razor CookingMode.razor` — PASS (no mutation)
- `dotnet build src/CookBot.Web/CookBot.Web.csproj` — Build succeeded, 0 errors

## Self-Check: PASSED
