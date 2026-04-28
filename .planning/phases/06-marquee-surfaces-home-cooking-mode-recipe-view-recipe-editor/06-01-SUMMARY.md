---
phase: 6
plan: 1
subsystem: web/home
tags: [home-dashboard, atoms, pantry-match, ai-off-contract, mud-removal]
requires:
  - Phase 5 atoms (CbButton, CbCard, CbStat, CbEyebrow, CbBadge, StripedPlaceholder, Icon)
  - Phase 5 design tokens (cookbot-design.css — .cb-card, .cb-stat, .eyebrow, .num, .cb-ph)
  - PantryService.GetAllUserAccessibleItemsAsync (existing)
  - CurrentUserService (existing)
  - CookBotDbContext.Recipes / Cookbooks / GroceryLists / CookbookShares (existing)
provides:
  - Pantry-aware hero card driven by deterministic stub matcher (FUTURE-13 extension point)
  - HomePantryMatch / HomeRecentRecipe records (Components.Pages namespace)
  - Glance strip with computed delta sub-text (Recipes / Cookbooks / Pantry / Grocery)
  - Two-up Recently cooked + Up next cards (placeholder queue with TODO marker)
affects:
  - src/CookBot.Web/Components/Pages/Home.razor (rewritten — Mud* fully removed)
  - src/CookBot.Web/Components/Pages/Home.razor.cs (NEW code-behind partial class)
tech-stack:
  added: []
  patterns:
    - First code-behind .razor.cs in the project (clean separation for non-trivial dashboard logic)
    - Deterministic-stub algorithm with explicit FUTURE-13 TODO at the threshold check
    - Same access predicate as RecipeAccessExtensions.UserCanAccessRecipeAsync (own + shared cookbooks)
key-files:
  created:
    - src/CookBot.Web/Components/Pages/Home.razor.cs
  modified:
    - src/CookBot.Web/Components/Pages/Home.razor
decisions:
  - "Pantry-match algo: ratio = matched / total ingredients; threshold 0.6; top 3 sorted desc — matches D-03 verbatim."
  - "Recently cooked falls back to most-recently-updated accessible recipes (no RecipeMade log exists in v1.2). PRAGMATIC per CONTEXT D-05 with TODO for FUTURE-Recently-Cooked."
  - "Up next ships 3 placeholder rows with TODO marker per CONTEXT D-06 PRAGMATIC option (no starred-queue concept yet)."
  - "Pantry low/expiring heuristics: Low = Amount > 0 && < 1; Expiring = ExpirationDate within 7 days. PantryItem has no canonical 'low' flag — sub-text is intentionally heuristic."
  - "First missing ingredient drives 'missing parsley'-style chip; complete matches show 'in stock' (CbBadge.InStock vs Low)."
metrics:
  duration: ~25min
  completed: 2026-04-27
  tasks_completed: 3
  files_changed: 2
---

# Phase 6 Plan 01: Home dashboard rewrite Summary

Migrated `Home.razor` from MudBlazor to the Phase 5 atom system per design handoff `screens/home.jsx`, demoting the stat counters and leading with a pantry-aware hero card backed by a deterministic stub matcher (the FUTURE-13 smart-match extension point).

## What changed

The old Home was a 4-stat MudGrid + 3 quick-action MudButtons. The rewrite restructures the page into four sections that match the design canvas exactly:

1. **Greeting + quick actions (HOME-01).** Eyebrow ("Welcome back, {DisplayName}") + 40px display headline ("What's the kitchen up to tonight?") + three quick actions. The accent "Generate a recipe" button is hidden when `_aiOff` (AI-off contract: host kill-switch AND user opt-in must both be on); "New recipe" and "New list" are always visible.
2. **Pantry-match hero (HOME-02).** Single `<CbCard Padding="28">` with a 1.2fr/1fr grid. Left column: accent eyebrow, dynamic 30px headline ("Three recipes match…" / "One recipe matches…" / empty-state), 14.5px body, then 3 match rows (tabular-numeral 0X + recipe name + meta + status badge + arrow). Right column: 300px `<StripedPlaceholder>` with overlay chip showing the top match. Empty-state replaces the row list with a "Manage Pantry" CTA.
3. **Glance strip (HOME-03).** 4-col grid of `<CbStat>` tiles — Recipes / Cookbooks / Pantry items / Grocery — each with a computed sub-line ("in your collection", "{N} shared with the house" / "private only", "{N} low · {N} expiring" / "all stocked" / "pantry empty", "list updated {ago}").
4. **Two-up: Recently cooked + Up next (HOME-04).** Left card: 4-thumbnail grid of recently-updated accessible recipes (PRAGMATIC fallback — no `RecipeMade` log exists in v1.2). Right card: 3 placeholder rows with TODO marker for the future starred-queue feature.

The page-shell wrappers (Sidebar, TopBar, cb-shell) are not rendered here — `MainLayout.razor` provides them. The page only renders the main column wrapped in `max-width: 1180px;` per the design canvas.

## Pantry-match algorithm (HOME-02 / D-03)

Implemented in `Home.razor.cs#BuildPantryMatchesAsync`:

```
matched = recipe.RecipeIngredients.Count(ri => pantryIngredientIds.Contains(ri.IngredientId));
ratio   = matched / total;
keep if ratio >= 0.6;          // TODO: smart matching — FUTURE-13
sort desc by ratio, then by name asc;
take 3.
```

A `HomePantryMatch` record carries `RecipeId`, `RecipeName`, `MatchedCount`, `TotalCount`, `MetaLine` ("X min · uses N of M ingredients" or "uses N of M ingredients" if no time), and `MissingIngredientName?` for the "missing parsley"-style chip. The TODO marker sits exactly at the threshold check so FUTURE-13 (expiration-aware, %-of-pantry-used, dietary-filtered) has a clear extension point. Empty pantry / no matches → empty list → UI shows the "Manage Pantry" CTA.

## AI-off contract (HOME-01 / D-12)

`_aiOff` is computed once per user load:

```csharp
var aiHostOn = CookBotSettingsOptions.Value.AiFeaturesEnabled;
var aiUserOn = _user.Profile?.AiEnabled ?? false;
_aiOff = !(aiHostOn && aiUserOn);
```

Hidden when AI off: the "Generate a recipe" accent button (clicking it would route to `/ai`).
Always visible: "New recipe" (routes `/cookbooks`), "New list" (routes `/grocery-lists`).

## Authorization

Reuses the same access predicate the rest of the app relies on — a recipe/cookbook is visible when `Cookbook.UserId == userId` OR `Cookbook.Shares.Any(s => s.SharedWithUserId == userId)`. This matches `RecipeAccessExtensions.UserCanAccessRecipeAsync` exactly. Pantry items come through `PantryService.GetAllUserAccessibleItemsAsync` (existing, owner + member). The "{N} shared with the house" sub-text uses `CookbookShares.Count(s => s.Cookbook.UserId == userId)` — outbound shares from this user's cookbooks.

## Verification

- `dotnet build` — clean (0 warnings, 0 errors).
- `dotnet test --filter "Category!=RequiresApiKey"` — 196 / 196 passed (baseline preserved).
- `grep -nE "Mud[A-Z]|Icons\\.Material\\." Home.razor Home.razor.cs` — zero hits.
- `grep -nE "mud-" Home.razor` — zero hits (no inline `mud-*` CSS classes).

## Acceptance criteria

| Criterion                                                                           | Status |
| ----------------------------------------------------------------------------------- | ------ |
| HOME-01: greeting + headline + 3 quick-action buttons render                        | ✓      |
| HOME-01: Generate-recipe hidden when AI off (`!_aiOff` guard)                       | ✓      |
| HOME-02: hero card renders up to 3 pantry matches OR empty-state CTA                | ✓      |
| HOME-03: 4-tile stat strip renders with real counts + computed sub-text             | ✓      |
| HOME-04: Recently cooked grid (up to 4) + Up next list (3 placeholders)             | ✓      |
| Zero `Mud*` symbols and `mud-*` classes in `Home.razor` / `Home.razor.cs`           | ✓      |
| `dotnet build` clean; `dotnet test` baseline preserved                              | ✓      |
| AI-off contract: `_aiOff = !(AiFeaturesEnabled && Profile.AiEnabled)`               | ✓      |
| `CurrentUserService` and per-user authorization preserved                           | ✓      |

## Deviations from Plan

None — plan executed exactly as written, with two PRAGMATIC fallbacks already pre-authorized by CONTEXT D-05 / D-06:

- **Recently cooked source.** No `RecipeMade` log entity exists in the schema (only the one-shot `RecipeMade.razor` page that deducts ingredients without persisting). CONTEXT D-05 PRAGMATIC pre-authorizes the fallback to "fill the visual slot with realistic data" — implemented as the 4 most-recently-updated accessible recipes with relative timestamps. TODO marker added for FUTURE-Recently-Cooked.
- **Up next placeholders.** No starred/queue concept exists. CONTEXT D-06 PRAGMATIC explicitly authorizes 3 placeholder rows with a TODO comment — implemented as the design-handoff trio ("Tartine country loaf", "Slow short rib", "Citrus tart").

Both fallbacks are explicit in the code (TODO markers at the relevant statements) and match the CONTEXT decisions verbatim.

## Known Stubs

| Stub                          | File                | Line region | Reason                                                                                  |
| ----------------------------- | ------------------- | ----------- | --------------------------------------------------------------------------------------- |
| `_upNextPlaceholders` table   | Home.razor.cs       | ~46–50      | No starred-queue concept yet. CONTEXT D-06 PRAGMATIC pre-authorized; FUTURE-Up-Next.    |
| `_recentlyCooked` fallback    | Home.razor.cs       | ~117–127    | No RecipeMade log entity. CONTEXT D-05 PRAGMATIC pre-authorized; FUTURE-Recently-Cooked.|
| StripedPlaceholder photos     | Home.razor          | hero + grid | No photo-upload backend yet (out of phase 6 scope per CONTEXT line 25).                 |
| `// TODO: smart matching` marker | Home.razor.cs    | ~167        | FUTURE-13 extension point at the 0.6 threshold check (deliberate per D-03).             |

All four are explicit, documented in CONTEXT, and visible to the verifier.

## Self-Check

- File `src/CookBot.Web/Components/Pages/Home.razor.cs` — FOUND
- File `src/CookBot.Web/Components/Pages/Home.razor` — FOUND (modified)
- Commit `25ab902` — FOUND in `git log`

## Self-Check: PASSED
