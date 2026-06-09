---
status: pending
phase: 15-nutrition-offline-cnf-canadian-nutrient-file
plan: 15-07
source: [15-07-PLAN.md, 15-UI-SPEC.md]
started: 2026-06-08
automated_harness: none (browser-only verification; see note below)
---

## Current Test

[pending — all items require a real browser with a running dev server]

## Automation note (2026-06-08)

The nutrition panel on RecipeView is built and the automated checks (dotnet build clean, 548 tests green, disclaimer-string grep) have passed. The items below require a real browser because they depend on:

- A running dev server with the `AddNutritionTables` EF migration applied and the CNF seed loaded (~5,690 foods)
- User interaction (CTA click) to trigger ComputeAsync — the panel never auto-computes
- Visual inspection of the 5-state panel rendering, responsive layout, and JSON-LD content in the browser's developer tools

**Critical pre-flight:** The dev server running on :7000 must be **restarted** after this merge so that:
1. The `AddNutritionTables` EF migration is applied (creates `CnfFoods`, `CnfConversionFactors`, `RecipeNutritionCaches` tables).
2. The CNF seed loads ~5,690 foods from `seeds/nutrition/cnf_foods.json` and their conversion factors from `seeds/nutrition/cnf_conversion_factors.json`.

Run `./run.sh` and confirm in the startup logs that the migration is applied and the seed data is loaded. Only then will nutrition calculation work.

## Tests

### 1. State 1 renders: "Nutrition not yet calculated." + "Calculate nutrition" CTA
expected: Open a recipe at http://localhost:7000/recipes/{id}. Confirm the panel appears below the recipe body with heading "Estimated nutrition", body text "Nutrition not yet calculated.", and a "Calculate nutrition" Accent button. The disclaimer "Estimated nutrition — not suitable for medical dietary planning. Data: Health Canada, Canadian Nutrient File (2015)." is visible below the panel.
result: [pending — real browser needed]

### 2. CTA triggers compute; transitions to State 2 with heading "Estimated nutrition"
expected: Click "Calculate nutrition". Confirm the button transitions to "Calculating..." (cb-pulse animation), then to a 4-up macro grid showing Energy (kcal), Protein (g), Carbs (g), and Fat (g). The heading reads "Estimated nutrition" — NOT "Calories". Values are non-zero for a recipe with matched ingredients.
result: [pending — real browser needed]

### 3. All-purpose flour resolves to approximately 455 kcal per cup (not water-density)
expected: Open or create a recipe containing "all-purpose flour" at 1 cup. Click "Calculate nutrition". Confirm Energy value per serving (or total for 1-serving recipe) is approximately 455 kcal — NOT approximately 240 kcal (which would indicate density=1 g/mL water-density error). The King Arthur density value (0.507 g/mL) should be used.
result: [pending — real browser needed]

### 4. Per-serving / Total toggle — values update without page reload
expected: In State 2, confirm "Per serving" is the default active tab. Click "Total" — the macro values update to total-recipe values without a server round-trip. Click "Per serving" — values return to per-serving. Keyboard: tab to the toggle, use arrow keys to move between options, confirm aria-checked updates.
result: [pending — real browser needed]

### 5. Coverage summary reads "Matched n of total ingredients"
expected: Confirm the coverage summary line reads "Matched {n} of {total} ingredients" (e.g. "Matched 3 of 4 ingredients"). The n and total values match the actual ingredient count in the recipe.
result: [pending — real browser needed]

### 6. Unmatched ingredient shows "--" (NEVER "0" or "0 kcal")
expected: Add an ingredient the CNF is unlikely to match — e.g. "pinch of saffron thread" or "edible gold flake". Click "Calculate nutrition". Confirm the unmatched ingredient's kcal column shows "--" (a literal double dash) in var(--ink-4) color, NOT "0", NOT "0 kcal", NOT blank. The badge shows the neutral "—" chip.
result: [pending — real browser needed]

### 7. Low-confidence match shows "≈" prefix + CNF description + [FoodId]
expected: For any ingredient matched with MEDIUM confidence (density-path match), confirm: (a) the kcal value is prefixed with "≈" (e.g. "≈227"), (b) the amber "≈" badge is shown, (c) the second line shows the CNF food description and [FoodId] (e.g. "Grains, wheat flour, white, all purpose, enriched [4484]").
result: [pending — real browser needed]

### 8. "Show all matches" toggle expands and collapses the coverage list
expected: In State 2, the coverage list defaults to showing only unmatched and low-confidence rows. If matched rows exist, a "Show all {n} matches" ghost button appears below the list. Click it — matched rows appear. The button changes to "Hide matched". Click again — matched rows collapse. No server round-trip occurs.
result: [pending — real browser needed]

### 9. Disclaimer visible and non-dismissable in every panel state
expected: Confirm the disclaimer "Estimated nutrition — not suitable for medical dietary planning. Data: Health Canada, Canadian Nutrient File (2015)." appears below the panel card in ALL five states: State 1 (not yet calculated), State 2 (calculated), State 3 (stale), State 4 (calculating), and State 5 (error). There is no dismiss button, no close affordance, and no way to hide it. It has role="note" in the DOM.
result: [pending — real browser needed]

### 10. State 3: stale banner after recipe edit + "Recalculate nutrition" Ghost CTA
expected: After calculating nutrition (State 2), edit and save the recipe (e.g. change an ingredient name). Return to RecipeView. Confirm: (a) State 3 appears with the amber warn banner "Recipe has changed — values may be outdated.", (b) the macro values remain visible but dimmed (opacity ~0.7), (c) a Ghost-variant "Recalculate nutrition" button is inside the banner. The save itself was instant — it did not block on nutrition.
result: [pending — real browser needed]

### 11. State 3 "Recalculate nutrition" → transitions back to State 2
expected: From State 3, click "Recalculate nutrition". Confirm it transitions to State 4 (Calculating…), then to State 2 with the stale banner gone and fresh values.
result: [pending — real browser needed]

### 12. State 5: error state shows error banner + "Try again" CTA
expected: (If reproducible) Trigger an error during compute (e.g. temporarily corrupt the DB or intercept with network devtools on a future hosted version). Confirm: (a) the error banner appears with role=status "Nutrition calculation failed — try again.", (b) the "Try again" Accent button is rendered, (c) clicking "Try again" re-enters State 4 and retries. The panel does NOT get stuck with no affordance.
result: [pending — real browser needed; may require deliberate error injection]

### 13. JSON-LD `<script type="application/ld+json">` contains nutrition.calories after compute
expected: After clicking "Calculate nutrition" and reaching State 2, view the page source (Ctrl+U) or open DevTools > Elements > head. Find the `<script type="application/ld+json">` block. Confirm it contains a `"nutrition"` object with `"@type": "NutritionInformation"` and a `"calories"` field (e.g. `"455 calories"`). BEFORE calculating (State 1), confirm the `"nutrition"` key is absent from the script block.
result: [pending — real browser needed]

### 14. Responsive layout: 2-column macro grid at ≤720px
expected: Resize the browser to a viewport width of 720px or narrower (or use DevTools device emulation). Confirm the 4-up macro grid collapses to 2 columns (Energy + Protein on row 1; Carbs + Fat on row 2) with gap:8px. The panel remains full-width and readable. The disclaimer, coverage list, and CTA buttons remain accessible.
result: [pending — real browser needed]

### 15. Panel never auto-computes on page load or recipe save
expected: Load a recipe that has never had nutrition calculated — confirm the panel shows State 1 (CTA only) immediately with no loading spinner or background computation. Save a recipe edit — confirm the save completes instantly with no delay caused by nutrition. No nutrition HTTP requests appear in the Network tab except the explicit CTA click.
result: [pending — real browser needed]

## Summary

total: 15
passed: 0        # automated pre-flight passed (build + tests + grep), but panel items require a real browser
issues: 0
pending: 15      # all items require a real browser + running dev server with CNF seed loaded
skipped: 0
blocked: 0

## Gaps

- **All items require a running dev server** with the `AddNutritionTables` migration applied and the CNF seed loaded. Run `./run.sh` fresh (do not reuse a stale :7000 process).
- **Item 12 (error state)** may require deliberate error injection — skipping it is acceptable if the code path is verified by reading the `_nutritionError = true` catch branch in the implementation.
- **Item 3 (flour 455 kcal/cup)** is the key SC3 anchor test — if this fails it indicates a density lookup error.
