---
status: complete
phase: 15-nutrition-offline-cnf-canadian-nutrient-file
plan: 15-07
source: [15-07-PLAN.md, 15-UI-SPEC.md]
started: 2026-06-08
updated: 2026-06-25
automated_harness: tests/uat-harness/tests/test16-integration.mjs
---

## Current Test

[UAT complete 2026-06-25 — 14/15 items PASS; item 14 (responsive 2-col) FAILED then FIXED. Both issues (item 14 + coverage-toggle) fixed and Playwright-verified 2026-06-25 (see Resolution)]

## Automation note (2026-06-24, Phase 16)

The Phase 16 UAT harness module `tests/uat-harness/tests/test16-integration.mjs` (runs under
`npm test` in `tests/uat-harness/`) now verifies the automatable slice of these items
hands-free against the live app. It creates a throwaway recipe (four CNF-matchable staples +
one deliberately-unmatchable "edible gold flake"), clicks "Calculate nutrition", and asserts
the panel + JSON-LD, then deletes the recipe (idempotent).

**Cleared automatically (PASS):** items 1, 2, 4, 5, 6, 8, 13, 15 (verified 2026-06-24).
**Still need a real browser or further harness work:** items 3 (exact 455 kcal anchor — covered
by unit tests; the browser test asserts a numeric energy, not the precise value), 7 (≈
low-confidence + CNF description), 9-states-3/4/5 (disclaimer is auto-verified in States 1 & 2
only), 10/11 (stale State 3 + recalc), 12 (error State 5), 14 (≤720px 2-col).

**Critical pre-flight (unchanged):** restart the dev server after a merge so the
`AddNutritionTables` migration is applied and the CNF seed is loaded before the harness runs.

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
result: PASS — automated 2026-06-24 (test16, assertion A). Panel renders State 1 with the CTA, "Nutrition not yet calculated.", and the exact disclaimer; no macro grid pre-compute.

### 2. CTA triggers compute; transitions to State 2 with heading "Estimated nutrition"
expected: Click "Calculate nutrition". Confirm the button transitions to "Calculating..." (cb-pulse animation), then to a 4-up macro grid showing Energy (kcal), Protein (g), Carbs (g), and Fat (g). The heading reads "Estimated nutrition" — NOT "Calories". Values are non-zero for a recipe with matched ingredients.
result: PASS — automated 2026-06-24 (test16, assertion B). CTA → State 2 with the 4-up Energy/Protein/Carbs/Fat grid; energy non-zero (569 kcal/serving for the fixture). (cb-pulse transition is too brief to assert reliably under automation.)

### 3. All-purpose flour resolves to approximately 455 kcal per cup (not water-density)
expected: Open or create a recipe containing "all-purpose flour" at 1 cup. Click "Calculate nutrition". Confirm Energy value per serving (or total for 1-serving recipe) is approximately 455 kcal — NOT approximately 240 kcal (which would indicate density=1 g/mL water-density error). The King Arthur density value (0.507 g/mL) should be used.
result: PASS — human-verified 2026-06-25 on recipe 1 (Apple Blueberry Crumble). The recipe's all-purpose flour = 0.5 cup; the flour row shows ≈227 kcal, matched to "Grains, wheat flour, white, all purpose, bleached [4501]". 0.5 cup × 455 = 227.5 → ≈455 kcal/cup, confirming flour density (~0.507 g/mL) is used, NOT water density. SC3 anchor satisfied.

### 4. Per-serving / Total toggle — values update without page reload
expected: In State 2, confirm "Per serving" is the default active tab. Click "Total" — the macro values update to total-recipe values without a server round-trip. Click "Per serving" — values return to per-serving. Keyboard: tab to the toggle, use arrow keys to move between options, confirm aria-checked updates.
result: PASS — automated 2026-06-24 (test16, assertion B). "Per serving" default; clicking "Total" flips aria-checked and updates energy 569 → 2277 (×4 servings) with no round-trip. (Arrow-key navigation not asserted — covered by the radiogroup keyboard handler.)

### 5. Coverage summary reads "Matched n of total ingredients"
expected: Confirm the coverage summary line reads "Matched {n} of {total} ingredients" (e.g. "Matched 3 of 4 ingredients"). The n and total values match the actual ingredient count in the recipe.
result: PASS — automated 2026-06-24 (test16, assertion B). Reads "Matched 3 of 5 ingredients"; total equals the 5-ingredient fixture, matched within [1, total].

### 6. Unmatched ingredient shows "--" (NEVER "0" or "0 kcal")
expected: Add an ingredient the CNF is unlikely to match — e.g. "pinch of saffron thread" or "edible gold flake". Click "Calculate nutrition". Confirm the unmatched ingredient's kcal column shows "--" (a literal double dash) in var(--ink-4) color, NOT "0", NOT "0 kcal", NOT blank. The badge shows the neutral "—" chip.
result: PASS — automated 2026-06-24 (test16, assertion B). The fixture's unmatchable "edible gold flake" row renders with a literal "--" present in the panel (never "0"). (Exact var(--ink-4) color not asserted.)

### 7. Low-confidence match shows "≈" prefix + CNF description + [FoodId]
expected: For any ingredient matched with MEDIUM confidence (density-path match), confirm: (a) the kcal value is prefixed with "≈" (e.g. "≈227"), (b) the amber "≈" badge is shown, (c) the second line shows the CNF food description and [FoodId] (e.g. "Grains, wheat flour, white, all purpose, enriched [4484]").
result: PASS — human-verified 2026-06-25. All low-confidence rows show the "≈" prefix + amber badge + CNF description + [FoodId]: flour "≈227 / Grains, wheat flour, white, all purpose, bleached [4501]", cornstarch "≈40 / Grains, cornstarch [4419]", sugar "≈129 / Sweetener, granulated brown sugar [6818]", salt "≈0 / Salt, table [214]". Data-quality note: "granulated sugar" matched to *brown* sugar [6818] — an imperfect match, but correctly surfaced as low-confidence with the CNF description visible (P4 safeguard working as designed).

### 8. "Show all matches" toggle expands and collapses the coverage list
expected: In State 2, the coverage list defaults to showing only unmatched and low-confidence rows. If matched rows exist, a "Show all {n} matches" ghost button appears below the list. Click it — matched rows appear. The button changes to "Hide matched". Click again — matched rows collapse. No server round-trip occurs.
result: PASS — automated 2026-06-24 (test16, assertion B2). "Show all N matches" expands the list (button → "Hide matched") and collapses again, no round-trip.

### 9. Disclaimer visible and non-dismissable in every panel state
expected: Confirm the disclaimer "Estimated nutrition — not suitable for medical dietary planning. Data: Health Canada, Canadian Nutrient File (2015)." appears below the panel card in ALL five states: State 1 (not yet calculated), State 2 (calculated), State 3 (stale), State 4 (calculating), and State 5 (error). There is no dismiss button, no close affordance, and no way to hide it. It has role="note" in the DOM.
result: PASS — States 1 & 2 automated (test16); State 3 human-verified 2026-06-25; States 4 & 5 code-confirmed. The disclaimer (RecipeView.razor:552-557, role="note", no dismiss affordance) is rendered UNCONDITIONALLY outside the `@if (_nutritionError)/else if/else` state switch, so it appears in all five states. Exact CNF (2015) attribution string present.

### 10. State 3: stale banner after recipe edit + "Recalculate nutrition" Ghost CTA
expected: After calculating nutrition (State 2), edit and save the recipe (e.g. change an ingredient name). Return to RecipeView. Confirm: (a) State 3 appears with the amber warn banner "Recipe has changed — values may be outdated.", (b) the macro values remain visible but dimmed (opacity ~0.7), (c) a Ghost-variant "Recalculate nutrition" button is inside the banner. The save itself was instant — it did not block on nutrition.
result: PASS — human-verified 2026-06-25. Editing + saving the recipe produced State 3 (stale amber banner, dimmed macros, Ghost "Recalculate nutrition" CTA); save was instant (did not block on nutrition).

### 11. State 3 "Recalculate nutrition" → transitions back to State 2
expected: From State 3, click "Recalculate nutrition". Confirm it transitions to State 4 (Calculating…), then to State 2 with the stale banner gone and fresh values.
result: PASS — human-verified 2026-06-25. Clicking "Recalculate nutrition" briefly showed Calculating… then returned to State 2 with the stale banner gone and fresh values.

### 12. State 5: error state shows error banner + "Try again" CTA
expected: (If reproducible) Trigger an error during compute (e.g. temporarily corrupt the DB or intercept with network devtools on a future hosted version). Confirm: (a) the error banner appears with role=status "Nutrition calculation failed — try again.", (b) the "Try again" Accent button is rendered, (c) clicking "Try again" re-enters State 4 and retries. The panel does NOT get stuck with no affordance.
result: PASS (code-verified) — live trigger skipped 2026-06-25 (needs deliberate error injection; impractical on the offline trusted-LAN box; item explicitly permits code verification). CalculateNutrition `catch { _nutritionError = true; }` (RecipeView.razor:1181) → `finally` re-renders into the State-5 branch (lines 366-381): (a) role="status" banner "Nutrition calculation failed — try again." ✓, (b) "Try again" Accent button ✓, (c) OnClick=CalculateNutrition resets `_nutritionError=false` + `_nutritionCalculating=true` → State 4 → retry ✓. The "Try again" affordance is always rendered in State 5, so the panel cannot get stuck. Disclaimer (unconditional) still visible.

### 13. JSON-LD `<script type="application/ld+json">` contains nutrition.calories after compute
expected: After clicking "Calculate nutrition" and reaching State 2, view the page source (Ctrl+U) or open DevTools > Elements > head. Find the `<script type="application/ld+json">` block. Confirm it contains a `"nutrition"` object with `"@type": "NutritionInformation"` and a `"calories"` field (e.g. `"455 calories"`). BEFORE calculating (State 1), confirm the `"nutrition"` key is absent from the script block.
result: PASS — automated 2026-06-24 (test16, assertions A2 + C). Pre-compute the ld+json parses with no `nutrition` key; post-compute it carries `nutrition.@type=NutritionInformation` + `calories="569 calories"`.

### 14. Responsive layout: 2-column macro grid at ≤720px
expected: Resize the browser to a viewport width of 720px or narrower (or use DevTools device emulation). Confirm the 4-up macro grid collapses to 2 columns (Energy + Protein on row 1; Carbs + Fat on row 2) with gap:8px. The panel remains full-width and readable. The disclaimer, coverage list, and CTA buttons remain accessible.
result: ISSUE (FAIL) → FIXED 2026-06-25. At ~700px the macro grid stayed 4-across (NOT 2×2). Fixed via the new `.nutrition-macro-grid` class + `@media(max-width:720px)` rule; Playwright-verified 4→2 tracks at 700px. See Resolution.

### 15. Panel never auto-computes on page load or recipe save
expected: Load a recipe that has never had nutrition calculated — confirm the panel shows State 1 (CTA only) immediately with no loading spinner or background computation. Save a recipe edit — confirm the save completes instantly with no delay caused by nutrition. No nutrition HTTP requests appear in the Network tab except the explicit CTA click.
result: PASS (load path) — automated 2026-06-24 (test16, assertion A). A freshly-created recipe shows State 1 with the CTA only and no macro grid on load (no auto-compute). The save-stays-instant half is not separately asserted.

## Summary

total: 15
passed: 14       # items 1-13, 15 (9 & 12 code-verified; 3,7,10,11 human-verified 2026-06-25)
issues: 2        # item 14 (nutrition-macro-grid-not-responsive) + cross-cutting nutrition-coverage-rows-ignore-toggle
pending: 0
skipped: 0
blocked: 0

## Gaps

- truth: "When 'Per serving' is selected, the per-ingredient coverage rows show per-serving contributions consistent with the per-serving headline macros"
  status: failed
  reason: "Discovered during items 3/7: in 'Per serving' mode the headline reads 274 kcal but the ingredient rows show whole-recipe totals (e.g. flour ≈227 for 0.5 cup, sugar ≈129, cornstarch ≈40 — already summing to ~396 > 274). The rows do not change when toggling Per serving ↔ Total; only the headline does."
  severity: minor
  test: 3/7 (analytic + code-confirmed; not one of the 15 enumerated items)
  root_cause: "RecipeView.razor coverage list (lines ~495-530) renders `row.EnergyKcal` (a fixed total-recipe per-ingredient value) regardless of `_nutritionPerServing`. The headline macro grid DOES honor the toggle (`_nutritionPerServing ? cache.PerServing* : cache.Total*`, lines ~463-466). So the breakdown is always totals while the headline flips — consistent in Total mode, mismatched in Per-serving mode."
  artifacts:
    - path: "src/CookBot.Web/Components/Pages/RecipeView.razor"
      issue: "coverage-row kcal (`row.EnergyKcal`, ~line 524/528) is not per-serving-aware; diverges from the toggle-aware headline (~line 463)"
  missing:
    - "Either divide each row's EnergyKcal by Servings when `_nutritionPerServing` is true (needs per-serving per-row values from NutritionService), OR label the coverage list explicitly as 'per recipe / total' so it reads as intentional regardless of the headline toggle"
  note: "May be partly by design (per-ingredient per-serving values would be small); the fix could be as light as a 'totals' label. Confirm intended behavior before coding."

- truth: "At ≤720px the nutrition macro grid collapses to 2 columns (Energy+Protein / Carbs+Fat)"
  status: failed
  reason: "User-verified at ~700px: the 4-up macro grid stays 4 across in one row (values just shrink), never becomes 2×2. Spec/UI-SPEC item 14 requires 2-col at ≤720px."
  severity: minor
  test: 14
  root_cause: "RecipeView.razor:460 sets the macro grid columns INLINE: `style=\"display:grid;grid-template-columns:repeat(4,1fr);...\"` with no class. The div has no class hook, there is no `repeat(2,1fr)` / nutrition-macro media rule anywhere in cookbot-design.css, and the only `@media (max-width:720px)` block (line ~769) targets the TopBar fallback, not this grid. Even if a media query existed, it could not override an INLINE grid-template-columns (inline styles outrank stylesheet selectors). So the grid stays 4-up and only shrinks."
  artifacts:
    - path: "src/CookBot.Web/Components/Pages/RecipeView.razor"
      issue: "macro grid (line ~460) hardcodes grid-template-columns:repeat(4,1fr) inline; no responsive collapse"
    - path: "src/CookBot.Web/wwwroot/css/cookbot-design.css"
      issue: "no @media(max-width:720px) rule for the nutrition macro grid"
  missing:
    - "Move the grid columns off the inline style onto a class (e.g. .nutrition-macro-grid { grid-template-columns:repeat(4,1fr) }) and add `@media (max-width:720px){ .nutrition-macro-grid{ grid-template-columns:repeat(2,1fr); gap:8px } }` — so the media query can actually take effect"

## Resolution (2026-06-25 — both issues fixed in this session)

Fixed directly after the UAT walkthrough (user chose "Fix all 6 now"). Build clean (0 errors); harness green; both fixes Playwright-verified against recipe 1.

| Gap | Fix | Verified |
|-----|-----|----------|
| nutrition-macro-grid-not-responsive (item 14) | Added `.nutrition-macro-grid` class to cookbot-design.css with `repeat(4,1fr)` + `@media(max-width:720px){ repeat(2,1fr); gap:8px }`; RecipeView.razor uses the class instead of inline columns (so the media query can override) | Playwright: 4 tracks at 1200px → 2 tracks at 700px — PASS |
| nutrition-coverage-rows-ignore-toggle | Coverage-row kcal now divides by `servings` when "Per serving" is active (RecipeView.razor), so rows track the toggle | Playwright: per-serving rows (flour ≈76) ↔ total (≈455); per-serving rows now sum to the 312 headline — PASS |


- **7 items remain** for a real browser or future harness work: 3 (exact 455 kcal anchor — covered by unit tests), 7 (≈ low-confidence + CNF description), 9 (disclaimer in States 3/4/5), 10/11 (stale State 3 + recalc — automatable later by editing the recipe between loads), 12 (error State 5 — needs error injection), 14 (≤720px 2-col responsive — automatable via viewport resize).
- **The 8 automated items require a running dev server** with the `AddNutritionTables` migration applied and the CNF seed loaded. Run `./run.sh` fresh (do not reuse a stale :7000 process).
- **Item 12 (error state)** may require deliberate error injection — skipping it is acceptable if the code path is verified by reading the `_nutritionError = true` catch branch in the implementation.
- **Item 3 (flour 455 kcal/cup)** is the key SC3 anchor test — covered by the Phase 15 density unit tests; the browser harness asserts a numeric per-serving energy, not the precise anchor value.
