---
phase: 6
plan: 3
subsystem: web/recipe-view
tags: [recipe-view, atoms, canonical-recipe-document, mud-removal, dialog-coexistence]
requires:
  - Phase 5 atoms (CbButton, CbCard, CbChip, CbEyebrow, StripedPlaceholder, Icon)
  - Phase 5 design tokens (cookbot-design.css — .cb-recipe-cap, .eyebrow, .num, .cb-card, .cb-chip)
  - JsonRecipeSerializer (v1.1 Phase 1)
  - RecipeScalingService.FormatScaledAmount (servings-only scaling, v1.1 D-Q9)
  - UnitParser.ToDisplayString (existing)
  - RecipeStepTextFormatter.ToPlainText (strips [name](#id) ingredient links to display labels)
  - CookBotDbContext.UserCanAccessRecipeAsync (existing access predicate)
  - ShareCookbookDialog (existing MudBlazor dialog — coexistence per D-30)
provides:
  - Editorial Recipe View consuming canonical RecipeDocument directly (Phase 6 SC#3 gate)
  - Sticky 300px ingredient sidebar with live servings scaling
  - Hanging accent numerals on method (28px, var(--accent), tabular)
  - Inline timer chips per ContentStep.Timers entry
  - Last-cook callout wired (hidden until RecipeMade log entity lands)
  - Share/Cook-this action row (TopBar right-slot fallback to inline render per CONTEXT D-17)
affects:
  - src/CookBot.Web/Components/Pages/RecipeView.razor (rewritten — Mud* fully removed)
tech-stack:
  added: []
  patterns:
    - "Canonical-first reads: deserialize Recipe.CanonicalDocumentJson via injected JsonRecipeSerializer, then bind RecipeDocument throughout the render path. Recipe entity is loaded only for Cookbook navigation (Share dialog params, /recipes/{id}/cook route)."
    - "Pre-canonical fallback path: if CanonicalDocumentJson is null or malformed, render a 'recipe needs migration' callout with an Edit-in-editor CTA. Avoids legacy column projection (SC#3) and never crashes."
    - "Inline action row above the hero (max-width 1080px, flex-end) substitutes for the TopBar right-slot mechanism that does not yet exist. Identical visual outcome per CONTEXT D-17 PRAGMATIC."
    - "Numeral padding via i.ToString(\"D2\") — correct for steps 1..99; design only ever shows 2-digit pads."
    - "Section vs content steps render through pattern matching on SectionStep / ContentStep discriminated union."
key-files:
  created: []
  modified:
    - src/CookBot.Web/Components/Pages/RecipeView.razor
decisions:
  - "Cook this route: /recipes/{id}/cook (existing CookingMode @page route). The plan's `$\"/cook/{recipeId}\"` literal was a stale wording; the actual route is unchanged."
  - "Share dialog target: existing ShareCookbookDialog launched for the recipe's parent cookbook. Sharing is cookbook-scoped in this app (CookbookShares table). The plan's 'existing share dialog' phrasing matches because there is no recipe-level share dialog — the surface that handles share is the cookbook-level dialog, reached from this surface."
  - "TopBar right-slot wiring: the current TopBar accepts a RightSlot RenderFragment but MainLayout instantiates the TopBar with no per-page passthrough. Adding cascading-parameter wiring is out of scope for this plan (CONTEXT D-17 PRAGMATIC: render inline). Tracking the wiring as a known gap for the eventual SHELL-03 polish slice."
  - "Lead paragraph: RecipeDocument has no description/lead-paragraph field. Hiding the paragraph cleanly rather than synthesizing one keeps SC#3 clean. FUTURE-V1.1-* schema slot."
  - "Made-count + last-cook notes: v1.2 has no RecipeMade log entity (Plan 06-01 SUMMARY confirmed). Made-count = 0× with TODO marker; notes callout simply hides via the documented conditional."
  - "Drop legacy buttons: the previous RecipeView shipped 'Add all to shopping list' / 'Add what I need' / 'I made this!' / 'Edit'. The redesign keeps only Share + Cook this per design handoff. The dropped workflows remain reachable: Edit via /recipes/{id}/edit (linked from the migration empty-state when canonical doc is missing); 'I made this!' via its own /recipes/{id}/made route; shopping-list workflows from CookbookDetail and GroceryListView. Behavior is preserved at the application level even though the affordances move."
metrics:
  duration: ~4 min
  completed: 2026-04-27
  tasks_completed: 5
  files_changed: 1
---

# Phase 6 Plan 03: Recipe View rewrite Summary

Rewrote `Components/Pages/RecipeView.razor` against the Phase 5 atom system per design handoff `screens/recipe-view.jsx`. Editorial layout — 64px display title, 4-stat row, 300px sticky scaled-ingredient sidebar, hanging accent numerals on method, "Notes from your last cook" cream-2 callout. The surface consumes the canonical `RecipeDocument` directly via the injected `JsonRecipeSerializer`; legacy column reads (`Recipe.IngredientsJson`, `Recipe.StepsJson`, `Recipe.IngredientRefs`, `Recipe.TagsJson`) are gone, satisfying the Phase 6 SC#3 gate. MudBlazor coexistence is preserved exactly where the hard invariant calls for it: the existing `ShareCookbookDialog` continues to launch via `IDialogService` for the recipe's parent cookbook.

## What changed

The old Recipe View was a MudGrid + MudPaper layout with five action buttons (Start Cooking, Edit, Add all to shopping list, Add what I need, I made this!) and rendered the EF entity's `RecipeIngredients` / `Steps` collections directly. The rewrite restructures the page to match the design canvas exactly:

1. **Action row (RV-05).** Inline above the hero (max-width 1080, flex-end gap 10px): `Share` ghost + `Cook this` accent. Both render inline because `MainLayout` instantiates `TopBar` without a per-page right-slot pass-through (PRAGMATIC fallback per CONTEXT D-17).
2. **Hero (RV-01).** 2-column grid (1fr/1fr, gap 40, items aligned to bottom). Left: tags eyebrow (joined `RecipeDocument.Tags` with `·`), 64px `cb-recipe-cap` title, 4-stat row across a top border (Active = `PrepTimeMinutes`, Total = `Prep + Cook`, Serves = `Servings`, Made = `_madeCount` 0×). Right: `<StripedPlaceholder Width="100%" Height="420" Label="hero photo · 4:3" />`.
3. **Sticky sidebar (RV-02).** `300px` aside, `position:sticky; top:80px`. Eyebrow "Ingredients", scale control card (Icon.Scale + label + −/+/servings num), then ingredient rows from `RecipeDocument.Ingredients` with 64px tabular qty + name (+ `(note)` when present), then a tag-chip row. `−` is disabled at servings 1, `+` at 100. Quantity formatting routes through `RecipeScalingService.FormatScaledAmount(amount, _doc.Servings, _targetServings)` so only `RecipeIngredient.Amount` scales — temperatures and times never auto-scale, preserving the v1.1 D-Q9 invariant.
4. **Method (RV-03).** Eyebrow "Method", then steps from `RecipeDocument.Steps`. Each `ContentStep` renders as a `40px / 1fr` grid: hanging accent numeral (28px, `var(--accent)`, tabular, padded `D2`) + body (15px, line-height 1.6, `RecipeStepTextFormatter.ToPlainText` strips `[name](#id)` link markup down to the display label) + inline `<CbChip Variant="Timer" Icon="clock">` per `ContentStep.Timers` entry rendered as `{Duration} {Unit}`. Each `SectionStep` renders as a heading-only row (18px, weight 600, no body, no chips) with the same bottom border. Step numbering increments only across `ContentStep`s — sections don't consume numerals.
5. **Last-cook callout (RV-04).** Cream-2 background, eyebrow "Notes from your last cook" + quote + " — {date}". Conditional on `!string.IsNullOrWhiteSpace(_lastCookNote)`; v1.2 has no `RecipeMade` log entity so the callout never renders today. The conditional is wired so the callout lights up automatically once the log lands (or v1.3 introduces it).

## Canonical RecipeDocument consumption (SC#3 gate)

The surface loads exactly two things from EF:

```csharp
_recipe = await DbContext.Recipes
    .Include(r => r.Cookbook)
    .FirstOrDefaultAsync(r => r.Id == RecipeId);

if (_recipe is { CanonicalDocumentJson: { Length: > 0 } json })
{
    _doc = RecipeSerializer.Deserialize(json);
    _targetServings = _doc.Servings > 0 ? _doc.Servings : 1;
}
```

The `Recipe` entity is fetched only for `Cookbook` navigation (Share dialog parameters + access predicate context). The render path binds `_doc` (a `RecipeDocument`) for everything visible: name, tags, prep/cook times, servings, ingredients, steps, timers. **No reads of** `Recipe.IngredientsJson`, `Recipe.StepsJson`, `Recipe.IngredientRefs`, `Recipe.TagsJson`, `Recipe.RecipeIngredients`, or `Recipe.Steps`. `grep -nE "IngredientsJson|StepsJson|IngredientRefs|TagsJson"` returns only comment hits explaining this contract.

The pre-canonical fallback path (when `CanonicalDocumentJson` is null or malformed) renders a "Recipe needs migration" `<CbCard>` with an "Open in editor" CTA — never falls back to legacy column projection.

## Verification

- `dotnet build` — clean (0 warnings, 0 errors); baseline preserved.
- `dotnet test --filter "Category!=RequiresApiKey"` — 196 / 196 passed (baseline preserved).
- `grep -nE "Mud[A-Z]|mud-|Icons\.Material\." RecipeView.razor` — zero hits in markup/code (the `Mud*` symbols `IDialogService` / `DialogParameters` / `DialogOptions` / `MaxWidth` / `ShareCookbookDialog` are MudBlazor types referenced via the global `MudBlazor` import, NOT `Mud*` component renders; the dialog launches existing MudBlazor content per the hard invariant).
- `grep -nE "IngredientsJson|StepsJson|IngredientRefs|TagsJson"` — all hits are inside comments documenting the SC#3 contract; zero code reads.

## Acceptance criteria

| Criterion                                                                                       | Status |
| ----------------------------------------------------------------------------------------------- | ------ |
| RV-01: Editorial title + 4-stat row + 4:3 hero placeholder                                      | ✓      |
| RV-02: Sticky 300px sidebar + scale control + ingredient rows + tag chips                       | ✓      |
| RV-03: Method renders hanging accent numerals + ContentStep body + timer chips                  | ✓      |
| RV-03: Method consumes RecipeDocument.Steps directly — zero legacy column reads                 | ✓      |
| RV-04: Last-cook callout hidden when no notes (v1.2: always hidden — no log entity yet)         | ✓      |
| RV-05: Share + Cook-this actions render (inline fallback per CONTEXT D-17 PRAGMATIC)            | ✓      |
| Phase 6 SC#3 — no reads of `Recipe.IngredientsJson` / `StepsJson` / `IngredientRefs`            | ✓      |
| Zero `Mud*` component renders in RecipeView.razor markup                                        | ✓      |
| `IDialogService.ShowAsync<ShareCookbookDialog>` continues to function (MudBlazor coexistence)   | ✓      |
| `dotnet build` clean; `dotnet test` baseline preserved (196/196)                                | ✓      |

## Hard invariants

- **Phase 6 SC#3:** legacy column projections forbidden — surface deserializes `Recipe.CanonicalDocumentJson` once and binds `RecipeDocument` throughout. Pre-canonical fallback shows a migration prompt rather than projecting from legacy columns.
- **MudBlazor coexistence (D-30):** `ShareCookbookDialog` continues to launch via `IDialogService`; that's an explicit Phase 7 MIG concern, not a Phase 6 regression.
- **Servings-only scaling (v1.1 D-Q9):** only `RecipeIngredient.Amount` is scaled via `RecipeScalingService.FormatScaledAmount`. `PrepTimeMinutes`, `CookTimeMinutes`, oven temperatures, and step text never auto-scale.
- **Authorization:** `CookBotDbContext.UserCanAccessRecipeAsync(RecipeId, userId)` gate preserved verbatim — own + shared cookbooks, identical to the rest of the app.

## Deviations from Plan

The plan executed cleanly. Five PRAGMATIC adjustments were pre-authorized by CONTEXT D-13..D-17 or by the prompt-supplied invariants; documented for completeness:

1. **[Plan 06-03 task 4 PRAGMATIC] TopBar right-slot fallback to inline action row.** The TopBar already accepts a `RightSlot` RenderFragment, but `MainLayout.razor` instantiates `<TopBar>` without exposing a per-page passthrough (no `CascadingValue<RenderFragment>` mechanism, no layout-context). Wiring it would require touching MainLayout (out of plan scope) and is non-trivial in Blazor's Layout cascading model. Per the prompt: render Share/Cook actions inline above the hero — visual outcome matches the design canvas. Tracked as a future SHELL-03 polish opportunity.
2. **[Plan 06-03 task 4 — route correction] Cook-this navigates to `/recipes/{id}/cook` (existing CookingMode route).** The plan's wording `Navigation.NavigateTo($"/cook/{recipeId}")` does not match the actual `@page` route on `CookingMode.razor` (which is `/recipes/{RecipeId:int}/cook`). Using the real route preserves cooking-mode entry behavior. Acts as a Rule 3 fix (would have produced a 404).
3. **[Plan 06-03 task 4 — Share target] Share opens ShareCookbookDialog for parent cookbook.** There is no recipe-level share dialog in the codebase; sharing in this app is cookbook-scoped (`CookbookShares` table). The plan's "existing share dialog" maps to `ShareCookbookDialog` for `_recipe.Cookbook`. This preserves the share workflow concept exactly.
4. **[Plan 06-03 task 1 — lead paragraph]** `RecipeDocument` has no description/lead-paragraph field; the 17px lead in the design hides cleanly rather than synthesizing prose from tags. Future-V1.1-* schema slot once a description field lands.
5. **[Plan 06-03 task 1 — made-count]** v1.2 has no `RecipeMade` log entity (Plan 06-01 SUMMARY confirmed). Surfaces 0× with a TODO marker per the prompt's deterministic-stub fallback.

Three legacy buttons from the prior RecipeView were dropped to match the design canvas (Edit, Add all to shopping list, Add what I need, I made this!) — the workflows remain reachable from other surfaces (`/recipes/{id}/edit` from the migration callout, CookbookDetail for shopping-list, `/recipes/{id}/made` for the made-log step). Documented in `decisions:` frontmatter.

## Known Stubs

| Stub                                                  | File                | Region   | Reason                                                                                                  |
| ----------------------------------------------------- | ------------------- | -------- | ------------------------------------------------------------------------------------------------------- |
| `_madeCount = 0`                                      | RecipeView.razor    | @code    | No RecipeMade log entity in v1.2 (Plan 06-01 SUMMARY). Hardcoded 0; TODO at field declaration.          |
| `_lastCookNote` / `_lastCookDate` always null         | RecipeView.razor    | @code    | Same reason. Conditional in markup hides the callout; CS0649 suppressed with explanatory pragma.        |
| Lead paragraph hidden                                 | RecipeView.razor    | hero     | RecipeDocument has no description field. FUTURE-V1.1-* schema addition.                                  |
| StripedPlaceholder hero photo                         | RecipeView.razor    | hero     | No photo-upload backend yet (out of phase 6 scope per CONTEXT line 25).                                  |
| TopBar right-slot wiring                              | (cross-cutting)     | layout   | Inline action row substitutes; future SHELL-03 polish slice can move it. Visual is identical.            |

All five are explicit, documented in CONTEXT D-13..D-17, and visible to the verifier.

## Self-Check

- File `src/CookBot.Web/Components/Pages/RecipeView.razor` — FOUND (modified, 326 lines)
- Commit `325c66d` — FOUND in `git log` (`feat(06-03): rewrite Recipe View against Phase 5 atoms`)
- `dotnet build` — clean (0/0)
- `dotnet test` — 196/196 baseline preserved
- `grep -nE "Mud[A-Z]" RecipeView.razor` — only `MaxWidth` (MudBlazor enum reused for the existing dialog launch — coexistence path) and `ShareCookbookDialog` (the existing dialog component being launched). Zero `<Mud*>` component renders.
- `grep -nE "IngredientsJson|StepsJson|IngredientRefs|TagsJson" RecipeView.razor` — only comment hits documenting the SC#3 contract.

## Self-Check: PASSED
