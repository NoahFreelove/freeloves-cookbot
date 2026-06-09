---
phase: 15-nutrition-offline-cnf-canadian-nutrient-file
verified: 2026-06-08T00:00:00Z
status: human_needed
score: 5/5 must-haves verified (automated); 15 browser-UAT items pending
overrides_applied: 0
human_verification:
  - test: "State 1 renders: 'Nutrition not yet calculated.' + 'Calculate nutrition' CTA visible"
    expected: "Panel below recipe body shows heading 'Estimated nutrition', body text 'Nutrition not yet calculated.', Accent 'Calculate nutrition' button, and the verbatim disclaimer"
    why_human: "Blazor Server component rendering, 5-state panel layout, and button visibility require a running dev server with CNF seed loaded"
  - test: "CTA triggers compute; transitions to State 2 with heading 'Estimated nutrition' (never 'Calories')"
    expected: "Button shows cb-pulse 'Calculating...' then transitions to 4-up macro grid; heading reads 'Estimated nutrition'"
    why_human: "User interaction (CTA click), async state transitions, and heading typography require a real browser"
  - test: "All-purpose flour resolves to approximately 455 kcal per cup (not water-density ~240 kcal)"
    expected: "A recipe with '1 cup all-purpose flour' shows Energy ~455 kcal per serving after CTA click — the SC3 anchor"
    why_human: "End-to-end computation through live CNF seed, EF Core, NutritionService, and Blazor rendering requires a running server"
  - test: "Per-serving / Total toggle updates values without page reload"
    expected: "Default tab is 'Per serving'; clicking 'Total' updates macro values client-side; arrow-key navigation updates aria-checked"
    why_human: "Client-side StateHasChanged toggle behavior and accessibility (keyboard/ARIA) require a real browser"
  - test: "Coverage summary reads 'Matched n of total ingredients'"
    expected: "Coverage line shows correct counts; n and total match actual recipe ingredient count"
    why_human: "Dynamic count rendering from parsed PerIngredientMatchJson requires live compute and browser inspection"
  - test: "Unmatched ingredient shows '--' (never '0' or blank)"
    expected: "An ingredient like 'pinch of saffron thread' shows '--' in var(--ink-4), neutral badge, never '0'"
    why_human: "Visual rendering of null-energy rows and CSS color token application require a real browser"
  - test: "Low-confidence match shows '≈' prefix + CNF description + [FoodId]"
    expected: "MEDIUM-confidence row shows amber '≈' badge, kcal prefixed with '≈', second line with CNF description and [FoodId]"
    why_human: "Conditional markup path and visual rendering of confidence badges require a real browser"
  - test: "'Show all matches' toggle expands/collapses the coverage list"
    expected: "By default only unmatched/low-confidence rows show; ghost button expands to all; no server round-trip"
    why_human: "Client-side toggle behavior and row visibility logic require a real browser"
  - test: "Disclaimer visible and non-dismissable in all five panel states"
    expected: "Verbatim 'Estimated nutrition — not suitable for medical dietary planning. Data: Health Canada, Canadian Nutrient File (2015).' appears in states 1–5; role=note; no dismiss affordance"
    why_human: "Presence across all states and absence of a dismiss button require live state-transition testing"
  - test: "State 3 (stale): amber banner after recipe edit + 'Recalculate nutrition' Ghost CTA"
    expected: "After calculating nutrition, editing and saving the recipe shows State 3 with warn banner and dimmed values (opacity ~0.7); save was instant"
    why_human: "Stale-mark flow (RecipeService SHA-256 hash change) and visual dimming require a running server + edit cycle"
  - test: "State 3 'Recalculate nutrition' transitions back to State 2"
    expected: "Clicking 'Recalculate nutrition' shows State 4 (Calculating...) then fresh State 2 with stale banner gone"
    why_human: "State machine transitions require real browser + server interaction"
  - test: "State 5 (error): error banner + 'Try again' CTA"
    expected: "Error banner with role=status 'Nutrition calculation failed — try again.' and Accent 'Try again' button; retry re-enters State 4"
    why_human: "Error injection and error-state rendering require a real browser; may require deliberate error injection"
  - test: "JSON-LD contains nutrition.calories after compute; absent before"
    expected: "After CTA click, page-source `<script type=\"application/ld+json\">` contains NutritionInformation with calories/macros; absent in State 1"
    why_human: "JSON-LD script block content and its dynamic update after Blazor re-render require DevTools inspection in a live browser"
  - test: "Responsive layout: 2-column macro grid at <=720px viewport"
    expected: "At <=720px the 4-up macro grid collapses to 2 columns; panel stays full-width; disclaimer/coverage/CTAs remain accessible"
    why_human: "CSS responsive layout at breakpoints requires visual browser inspection"
  - test: "Panel never auto-computes on page load or recipe save"
    expected: "Loading a recipe shows State 1 immediately with no computation; saving an edit completes instantly with no nutrition delay; no nutrition HTTP requests except on CTA click"
    why_human: "Absence of auto-compute and latency of save require a running server; Network tab inspection confirms no unexpected requests"
---

# Phase 15: Nutrition (Offline CNF) Verification Report

**Phase Goal:** Every recipe can show an estimated calorie and macro panel computed entirely offline from Canadian Nutrient File data — with explicit coverage indicators and a mandatory disclaimer, never blocking the recipe save path.
**Verified:** 2026-06-08T00:00:00Z
**Status:** human_needed
**Re-verification:** No — initial verification

## Goal Achievement

All five success criteria are verifiable in code or by automated tests. The remaining 15 items require a real browser with a running dev server (the phase's Plan 15-07 is an `autonomous: false` checkpoint plan by design). All code-review blockers and warnings from 15-REVIEW.md have been fixed (commits f5c38c0, 6e515ac, 28cead6, 730f3b4, 9bb06c1).

### Observable Truths

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | SC1: Nutrition computed fully offline — CNF seed bundled in SQLite; no API key; no runtime external calls; CTA-only compute, never on save | VERIFIED | `seeds/nutrition/cnf_foods.json` (5,690 rows), `cnf_conversion_factors.json` (16,656 rows) committed. `NutritionService.cs` has zero HttpClient/http references. `grep -c "INutritionService\|NutritionService" RecipeService.cs` returns 0. `CalculateNutrition()` is the only call site for `ComputeAsync`. |
| 2 | SC2: Unmatched shows "--" (not zero); low-confidence shows "≈" + CNF description + FoodId | VERIFIED (code); browser rendering human_needed | `RecipeView.razor:518-528` renders `--` when `Confidence == "UNMATCHED"`, `≈` prefix for MEDIUM. CNF description + `[{FoodId}]` at line 514. NutritionService sets null energy (not 0) for unmatched. Visual rendering requires browser. |
| 3 | SC3: "1 cup all-purpose flour" → ~455 kcal via CNF factor; unit tests cover >=20 common ingredients | VERIFIED | `FlourAnchor_OneCupAllPurposeFlour_Returns455Kcal` passes (3/3 flour-anchor tests). FoodId 4484 seed data: 364 kcal/100g, 250ml CF=1.32079, confirmed by Python spot-check. 31/31 `IngredientDensityProvider` tests pass (>=20 ingredient assertions, >= 23-entry count guard). |
| 4 | SC4: Non-dismissable disclaimer "Estimated nutrition — not suitable for medical dietary planning. Data: Health Canada, Canadian Nutrient File (2015)." in every state; heading "Estimated nutrition" never "Calories" | VERIFIED (code); disclaimer in all-states human_needed | `grep -c` of verbatim string returns 1 in RecipeView.razor (line 556). All `<CbEyebrow>` headings in the nutrition section read "Estimated nutrition" — grep for "Calories" heading returns 0. Non-dismissable presence across all 5 states requires browser walk-through. |
| 5 | SC5: nutrition.calories (+macros) in Schema.org JSON-LD when present; omitted cleanly when absent | VERIFIED | `JsonLdRecipeProjector.Project` line 78 has `NutritionInfoDto? nutrition = null` param. Lines 152-161 emit `NutritionInformation` dict with `calories`, `proteinContent`, `carbohydrateContent`, `fatContent` only when non-null. RecipeView line 909 passes `nutritionDto` only when `_nutritionCache is { IsStale: false }`. 19/19 JsonLd tests pass (present/absent/rounding/baseline-golden-unchanged). |

**Score:** 5/5 success criteria verified in code and automated tests.

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `tools/build-cnf-seed.py` | Dev-only offline CNF seed builder | VERIFIED | Exists, valid Python, food_code merge, /servingsize/ endpoint, NutrientNameID verify-before-trust step |
| `seeds/nutrition/cnf_foods.json` | 5,690 CNF foods per-100g | VERIFIED | 5,690 rows, FoodId 4484 present, EnergyKcalPer100g=364.0, verbatim |
| `seeds/nutrition/cnf_conversion_factors.json` | 16,656 CNF conversion factors | VERIFIED | 16,656 rows, FoodId 4484 250ml CF=1.32079 |
| `src/CookBot.Domain/Entities/CnfFood.cs` | CNF food POCO with verbatim macros | VERIFIED | EnergyKcalPer100g, NormalizedDescription, ConversionFactors collection, no EF refs |
| `src/CookBot.Domain/Entities/CnfConversionFactor.cs` | CNF factor POCO with FK | VERIFIED | ConversionFactorValue, FoodId FK, CnfFood nav |
| `src/CookBot.Domain/Entities/RecipeNutritionCache.cs` | Per-recipe cache with hash + staleness | VERIFIED | CanonicalDocHash, IsStale, PerServing* + Total* doubles, MatchedIngredients, TotalIngredients, PerIngredientMatchJson |
| `src/CookBot.Application/DTOs/NutritionInfoDto.cs` | Sealed record per-serving DTO | VERIFIED | `sealed record NutritionInfoDto(double CaloriesPerServing, ProteinGPerServing, FatGPerServing, CarbGPerServing)` |
| `src/CookBot.Application/Services/IngredientNormalizer.cs` | Shared deny-list normalizer | VERIFIED | Single `Normalize` static method, deny-list strips prep/quality words as whole words, keeps nutrition-changing modifiers (unsalted, whole, lowfat, low-fat, heavy, light, skinless, salted) |
| `src/CookBot.Application/Services/IngredientDensityProvider.cs` | >=23-entry g/mL density table | VERIFIED | All-purpose flour = 0.507 (SC3 anchor), null-on-unknown, >=23 entries, EntryCount=28 |
| `src/CookBot.Application/Services/INutritionService.cs` | GetCacheAsync / ComputeAsync contract | VERIFIED | Both methods present with ownership enforcement documented |
| `src/CookBot.Infrastructure/Services/NutritionService.cs` | Offline matcher + CNF-factor + density + cache write | VERIFIED | ComputeAsync present; F1 scoring (WR-01 fix applied); pre-tokenized CNF index (WR-02 fix); no HttpClient; concurrency token (WR-04 fix) |
| `src/CookBot.Infrastructure/Data/Configurations/CnfFoodConfiguration.cs` | EF config with ValueGeneratedNever | VERIFIED | HasKey, ValueGeneratedNever, NormalizedDescription index, cascade to CnfConversionFactors |
| `src/CookBot.Infrastructure/Data/Configurations/RecipeNutritionCacheConfiguration.cs` | 1:1 cascade FK, TEXT column, 64-char hash | VERIFIED | OnDelete(DeleteBehavior.Cascade) present |
| `src/CookBot.Infrastructure/Migrations/20260608030954_AddNutritionTables.cs` | Creates CnfFoods/CnfConversionFactors/RecipeNutritionCaches | VERIFIED | All three CreateTable calls present; FoodId NOT autoincrement; NormalizedDescription index |
| `src/CookBot.Infrastructure/Data/DatabaseSeeder.cs` | Idempotent CNF seed load with Normalize pre-compute | VERIFIED | SeedCnfDataAsync at line 83 (before user early-return at line 143); idempotent guard `if (await context.CnfFoods.AnyAsync()) return`; calls `IngredientNormalizer.Normalize` for NormalizedDescription |
| `src/CookBot.Application/Recipes/JsonLdRecipeProjector.cs` | Optional NutritionInfoDto param + NutritionInformation emission | VERIFIED | Lines 78, 152-161 |
| `src/CookBot.Application/Services/RecipeService.cs` | SHA-256 stale-mark; zero NutritionService refs | VERIFIED | MarkNutritionCacheStaleIfChangedAsync at 367+; 0 NutritionService refs; CR-01 fix applied (no SaveChanges in stale-mark helper) |
| `src/CookBot.Web/Components/Pages/RecipeView.razor` | 5-state nutrition panel + CTA + disclaimer + JSON-LD wiring | VERIFIED (code); visual rendering human_needed | Verbatim disclaimer grep=1; "Estimated nutrition" heading; INutritionService injected; 5-state markup present; JSON-LD nutrition wiring at lines 909-942, 1141-1171 |
| `tests/CookBot.Tests/Nutrition/NutritionServiceTests.cs` | Flour anchor, unmatched-null, stale, cup-scale, mass-direct | VERIFIED | 12/12 pass |
| `tests/CookBot.Tests/Nutrition/IngredientDensityProviderTests.cs` | >=20 ingredient assertions + no-water-density guard | VERIFIED | 31/31 pass |
| `tests/CookBot.Tests/Nutrition/IngredientNormalizerTests.cs` | Deny-list behavior + whole-word guard | VERIFIED | Pass (included in 548 total) |
| `tests/CookBot.Tests/Nutrition/CnfSeedLoadTests.cs` | Load, normalize, idempotent, flour normalizes, verbatim | VERIFIED | Pass (included in 548 total) |
| `tests/CookBot.Tests/Nutrition/JsonLdNutritionProjectorTests.cs` | Present/absent/rounding/Phase 13 golden unchanged | VERIFIED | 19/19 pass |

### Key Link Verification

| From | To | Via | Status | Details |
|------|----|-----|--------|---------|
| `seeds/nutrition/cnf_foods.json` | `DatabaseSeeder.SeedCnfDataAsync` | `Path.Combine(contentRootPath, "..", "seeds", "nutrition", ...)` | VERIFIED | Lines 285-286 in DatabaseSeeder.cs |
| `DatabaseSeeder.SeedCnfDataAsync` | `IngredientNormalizer.Normalize` | Pre-computes NormalizedDescription at seed load | VERIFIED | Line 299 in DatabaseSeeder.cs |
| `NutritionService.ComputeAsync` | `CnfConversionFactor` | Closest-mL measure match + recipe_mL/cnf_mL scale | VERIFIED | Lines 286-335 in NutritionService.cs |
| `RecipeService.MarkNutritionCacheStaleIfChangedAsync` | `RecipeNutritionCache` | IRepository<RecipeNutritionCache>; no SaveChanges in helper (CR-01 fix) | VERIFIED | Lines 381-394; comment at line 390-393 documents atomicity invariant |
| `RecipeView.razor` | `INutritionService` | GetCacheAsync on load + ComputeAsync on CTA | VERIFIED | @inject at line 24; GetCacheAsync at 895; ComputeAsync in CalculateNutrition at 1134 |
| `RecipeView.razor` | `JsonLdRecipeProjector.Project` | NutritionInfoDto passed when cache is current (IsStale=false) | VERIFIED | Lines 909-942 and 1141-1171 |
| `JsonLdRecipeProjector.Project` | `NutritionInfoDto` | Optional third param consumed only when non-null | VERIFIED | Lines 78, 152-161 |

### Data-Flow Trace (Level 4)

| Artifact | Data Variable | Source | Produces Real Data | Status |
|----------|---------------|--------|--------------------|--------|
| `RecipeView.razor` nutrition panel | `_nutritionCache` | `NutritionSvc.GetCacheAsync` (load) / `NutritionSvc.ComputeAsync` (CTA) | Yes — NutritionService queries real EF DbContext with CnfFoods/CnfConversionFactors seeded from JSON | FLOWING |
| `JsonLdRecipeProjector.Project` | `nutrition` param | RecipeView constructs from `_nutritionCache.PerServing*` only when `IsStale: false` | Yes — real computed cache values | FLOWING |
| `DatabaseSeeder.SeedCnfDataAsync` | CnfFoods table | `seeds/nutrition/cnf_foods.json` (5,690 verbatim CNF rows) | Yes — real CNF data, committed | FLOWING |

### Behavioral Spot-Checks

| Behavior | Command | Result | Status |
|----------|---------|--------|--------|
| FlourAnchor tests pass | `dotnet test --filter "FullyQualifiedName~FlourAnchor"` | 3/3 Passed | PASS |
| IngredientDensityProvider tests pass (>=20 ingredients, >=23 entries, no-water-density) | `dotnet test --filter "FullyQualifiedName~IngredientDensityProvider"` | 31/31 Passed | PASS |
| NutritionService tests pass (flour anchor, unmatched-null, stale, cup-scale, mass-direct, coverage) | `dotnet test --filter "FullyQualifiedName~NutritionService"` | 12/12 Passed | PASS |
| JsonLd tests pass (present/absent/rounding/Phase 13 golden unchanged) | `dotnet test --filter "FullyQualifiedName~JsonLd"` | 19/19 Passed | PASS |
| Full non-AI test suite | `dotnet test --filter "Category!=RequiresApiKey"` | 548/548 Passed | PASS |
| CNF seed data integrity (flour 4484, food count, CF count) | Python spot-check | 5,690 foods, 16,656 CFs, FoodId 4484 EnergyKcalPer100g=364.0, 250ml CF=1.32079 | PASS |
| RecipeService has 0 NutritionService references | `grep -c "INutritionService\|NutritionService" RecipeService.cs` | 0 | PASS |
| Verbatim disclaimer in RecipeView | `grep -c "Estimated nutrition — not suitable for medical dietary planning. Data: Health Canada, Canadian Nutrient File (2015)."` | 1 | PASS |

### Requirements Coverage

| Requirement | Source Plan | Description | Status | Evidence |
|-------------|------------|-------------|--------|----------|
| NUTR-01 | 15-01, 15-02, 15-03 | CNF ships as bundled SQLite seed; offline; verbatim values | SATISFIED | `seeds/nutrition/*.json` committed; migration creates tables; DatabaseSeeder loads idempotently; no runtime HTTP |
| NUTR-02 | 15-02, 15-03, 15-05 | Nutrition computed from ingredient amounts; cached per-recipe; invalidated on canonical change | SATISFIED | NutritionService.ComputeAsync writes RecipeNutritionCache; RecipeService.MarkNutritionCacheStaleIfChangedAsync marks IsStale on hash change; 12/12 service tests pass |
| NUTR-03 | 15-04, 15-05 | Volume→mass via CNF factors first; per-ingredient density fallback (not water); low-confidence marked | SATISFIED | IngredientDensityProvider (31 tests), NutritionService CNF-factor-first path with density fallback; flour anchor = 455 kcal via CNF CF; density fallback test confirms not-water |
| NUTR-04 | 15-05, 15-07 | Nutrition panel per-serving + total; unmatched explicit (not zeroed); coverage indicator | SATISFIED (code) / browser visual pending | Panel markup in RecipeView.razor; "--" for UNMATCHED, "≈" for MEDIUM; coverage summary present; visual/interactive rendering requires human UAT |
| NUTR-05 | 15-07 | Verbatim non-dismissable disclaimer; "Estimated nutrition" heading | SATISFIED (code) / all-states rendering pending | Verbatim disclaimer grep=1; all CbEyebrow headings read "Estimated nutrition"; no "Calories" heading; non-dismissable rendering across all 5 states requires human UAT |
| NUTR-06 | 15-02, 15-06, 15-07 | nutrition.calories (+macros) in JSON-LD when present; omitted when absent | SATISFIED | JsonLdRecipeProjector NutritionInformation emission; RecipeView passes NutritionInfoDto only when cache is current; 19/19 JsonLd tests pass |

### Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
|------|------|---------|----------|--------|
| (none) | — | No TBD/FIXME/XXX unresolved debt markers found in files modified by this phase | — | — |

Anti-pattern scan of phase-modified files: no `TBD`, `FIXME`, or `XXX` markers found. The code-review BLOCKER (CR-01) and all 6 WARNINGs from 15-REVIEW.md are confirmed fixed (commits f5c38c0 through 9bb06c1). Four INFO items from the review were accepted as minor or informational and require no action.

### Human Verification Required

This phase includes a `checkpoint:human-verify` gate task (Plan 15-07 Task 2, `gate: blocking`) that is explicitly `autonomous: false`. All 15 items in 15-HUMAN-UAT.md are pending browser verification. The items are organized from the 15-HUMAN-UAT.md file and cover:

#### 1. State 1 renders: "Nutrition not yet calculated." + "Calculate nutrition" CTA

**Test:** Open a recipe at http://localhost:7000/recipes/{id}. Confirm the panel appears below the recipe body with heading "Estimated nutrition", body text "Nutrition not yet calculated.", and a "Calculate nutrition" Accent button. The disclaimer is visible.
**Expected:** All elements present; disclaimer verbatim text visible.
**Why human:** Blazor Server component rendering and layout require a running dev server with AddNutritionTables migration applied and CNF seed loaded (~5,690 foods).

#### 2. CTA triggers compute; heading "Estimated nutrition" (not "Calories")

**Test:** Click "Calculate nutrition". Confirm cb-pulse animation, then 4-up macro grid with heading "Estimated nutrition".
**Expected:** Non-zero macro values for a recipe with matched ingredients; heading never reads "Calories".
**Why human:** Async Blazor state transitions and heading typography require a real browser.

#### 3. All-purpose flour resolves to approximately 455 kcal per cup (SC3 anchor)

**Test:** Open or create a recipe containing "all-purpose flour" at 1 cup. Click "Calculate nutrition". Confirm Energy ~455 kcal — NOT ~240 kcal (water density).
**Expected:** ~455 kcal (the CNF 250ml CF=1.32079 + 0.9464 US-cup scale path, or the KA density fallback).
**Why human:** End-to-end CNF seed → EF → NutritionService → Blazor rendering chain requires a live server.

#### 4. Per-serving / Total toggle updates values without page reload

**Test:** In State 2, confirm "Per serving" is default. Click "Total" — values update without a server round-trip. Arrow-key navigation updates aria-checked.
**Expected:** Client-side StateHasChanged only; no network requests.
**Why human:** Client-side toggle and accessibility (ARIA) require a real browser.

#### 5. Coverage summary reads "Matched n of total ingredients"

**Test:** Confirm coverage line reads "Matched {n} of {total} ingredients".
**Expected:** Correct counts matching actual recipe ingredient count.
**Why human:** Dynamic count from parsed PerIngredientMatchJson requires live compute.

#### 6. Unmatched ingredient shows "--" (never "0")

**Test:** Add an ingredient like "pinch of saffron thread". Click "Calculate nutrition". Confirm kcal column shows "--" in var(--ink-4), neutral badge.
**Expected:** "--" literal, never "0", never blank.
**Why human:** Visual rendering of null-energy rows and CSS color tokens require a real browser.

#### 7. Low-confidence match shows "≈" prefix + CNF description + [FoodId]

**Test:** For any MEDIUM-confidence ingredient, confirm amber "≈" badge, kcal prefixed with "≈", second line with CNF description and [FoodId].
**Expected:** e.g. "≈227" with "Grains, wheat flour, white, all purpose, enriched [4484]".
**Why human:** Conditional markup and visual badge rendering require a real browser.

#### 8. "Show all matches" toggle expands/collapses coverage list

**Test:** Confirm default shows only unmatched/low-confidence rows; ghost button expands to all; no server round-trip.
**Expected:** Client-side toggle only.
**Why human:** Row visibility and button behavior require a real browser.

#### 9. Disclaimer visible and non-dismissable in all five panel states

**Test:** Walk through all five panel states. Confirm the verbatim disclaimer appears in every state with role="note" and no dismiss affordance.
**Expected:** Disclaimer present across all states; no close button; role=note in DOM.
**Why human:** Multi-state traversal and DOM inspection require a running server.

#### 10. State 3: stale banner after recipe edit + "Recalculate nutrition" Ghost CTA

**Test:** After calculating, edit+save the recipe; return to RecipeView. Confirm State 3 with amber warn banner, dimmed values (opacity ~0.7), Ghost "Recalculate nutrition" button. Confirm save was instant.
**Expected:** SHA-256 hash change triggers IsStale=true; banner and dimming render correctly.
**Why human:** Full edit-save-view cycle and visual opacity require a running server.

#### 11. State 3 "Recalculate nutrition" transitions back to State 2

**Test:** From State 3, click "Recalculate nutrition". Confirm State 4 → State 2 with fresh values.
**Expected:** Stale banner gone; fresh macro values.
**Why human:** State transitions require real browser + server interaction.

#### 12. State 5 (error): error banner + "Try again" CTA

**Test:** Trigger a compute error (e.g. deliberate error injection). Confirm role=status error banner and Accent "Try again" button; retry re-enters State 4.
**Expected:** Error state renders with actionable CTA; panel not stuck.
**Why human:** Error injection and rendering require a real browser; may require deliberate error injection.

#### 13. JSON-LD contains nutrition.calories after compute; absent before

**Test:** View page source/DevTools after State 2. Confirm `<script type="application/ld+json">` has NutritionInformation with `calories`. Before calculating (State 1), confirm `nutrition` key is absent.
**Expected:** Dynamic JSON-LD update matches IsStale=false condition.
**Why human:** JSON-LD script block content requires DevTools inspection in a live browser.

#### 14. Responsive layout: 2-column macro grid at <=720px

**Test:** Resize browser to <=720px. Confirm 4-up grid collapses to 2 columns (Energy+Protein row 1, Carbs+Fat row 2). Panel stays full-width; disclaimer/coverage/CTAs remain accessible.
**Expected:** Responsive CSS works correctly at the breakpoint.
**Why human:** CSS responsive layout at breakpoints requires visual browser inspection.

#### 15. Panel never auto-computes on page load or recipe save

**Test:** Load a recipe with no prior nutrition calculation — confirm immediate State 1 with no computation spinner. Save a recipe edit — confirm instant save with no nutrition delay. Check Network tab for unexpected requests.
**Expected:** Compute is strictly CTA-only; save never blocks on nutrition.
**Why human:** Timing and Network tab inspection require a running server.

---

**Critical pre-flight for human UAT:** Restart the dev server (`./run.sh`) so the `AddNutritionTables` EF migration applies and the CNF seed loads (~5,690 foods from `seeds/nutrition/cnf_foods.json`). A stale dev server will not have the new tables.

### Gaps Summary

No automated gaps. All five success criteria (SC1–SC5) and all six requirements (NUTR-01 through NUTR-06) are satisfied by code evidence and 548/548 automated tests. The one code-review BLOCKER (CR-01: non-atomic stale-mark save) was fixed before this verification (commit f5c38c0); all six WARNINGs were also fixed (commits 6e515ac, 28cead6, 730f3b4, 9bb06c1). The phase is ready for human browser UAT to close the 15 visual/interactive items in 15-HUMAN-UAT.md.

---

_Verified: 2026-06-08T00:00:00Z_
_Verifier: Claude (gsd-verifier)_
