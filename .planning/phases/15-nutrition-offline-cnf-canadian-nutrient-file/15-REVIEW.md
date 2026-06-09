---
phase: 15-nutrition-offline-cnf-canadian-nutrient-file
reviewed: 2026-06-07T00:00:00Z
depth: standard
files_reviewed: 9
files_reviewed_list:
  - src/CookBot.Infrastructure/Services/NutritionService.cs
  - src/CookBot.Application/Services/IngredientNormalizer.cs
  - src/CookBot.Application/Services/IngredientDensityProvider.cs
  - src/CookBot.Application/Services/RecipeService.cs
  - src/CookBot.Application/Recipes/JsonLdRecipeProjector.cs
  - src/CookBot.Infrastructure/Data/DatabaseSeeder.cs
  - src/CookBot.Infrastructure/Migrations/20260608030954_AddNutritionTables.cs
  - src/CookBot.Web/Components/Pages/RecipeView.razor
  - tools/build-cnf-seed.py
findings:
  critical: 1
  warning: 6
  info: 4
  total: 11
status: issues_found
---

# Phase 15: Code Review Report

**Reviewed:** 2026-06-07T00:00:00Z
**Depth:** standard
**Files Reviewed:** 9
**Status:** issues_found

## Summary

Reviewed the offline CNF nutrition feature against the stated invariants. The core invariants
hold well: the runtime makes **no external HTTP calls and uses no API key** (the only HTTP is in
the dev-time Python script); `RecipeService` never calls `NutritionService` and only writes a
SHA-256 stale-mark; nutrition is never serialized into `CanonicalDocumentJson`; the projector uses
the HTML-safe STJ encoder; unmatched ingredients return null (rendered "--") not 0; the per-serving
divisor is clamped with `Math.Max(doc.Servings, 1)` so `Servings=0` cannot divide-by-zero; the
migration only `CreateTable`s (no destructive ops in `Up`); CNF values are inserted verbatim.

However there is one **BLOCKER**: the stale-mark write inside `RecipeService.UpdateAsync` commits
the in-flight recipe mutation prematurely via a shared `DbContext`, so the canonical-doc save and
the stale-mark are split across two non-atomic `SaveChanges` calls in an order that can persist a
stale-marked cache while leaving partial recipe state if the second save throws. Several WARNINGs
cover match-confidence correctness (single-token over-matching mislabeled HIGH), an unbounded
in-memory match scan, a hash-input divergence on the empty-canonical path, a concurrency gap on the
cache row, and a couple of display/state inconsistencies.

## Critical Issues

### CR-01: Stale-mark commits the recipe mid-`UpdateAsync` via shared DbContext — non-atomic save ordering

**File:** `src/CookBot.Application/Services/RecipeService.cs:287-289` (with `MarkNutritionCacheStaleIfChangedAsync` at 367-392)

**Issue:** `RecipeService` and `IRepository<RecipeNutritionCache>` share the **same scoped
`CookBotDbContext`**. The generic `Repository<T>.UpdateAsync` calls `SaveChangesAsync()`
(`Repository.cs:32-36`). In `UpdateAsync`, the recipe entity is mutated in place (Name, Servings,
`RecipeIngredients.Clear()`/Add, `Steps.Clear()`/Add, `CanonicalDocumentJson`) and then
`MarkNutritionCacheStaleIfChangedAsync(recipe)` is invoked **before** `_recipeRepo.UpdateAsync(recipe)`.
When a cache row exists and the hash changed, that helper calls `_nutritionCacheRepo.UpdateAsync(cache)`
→ `SaveChangesAsync()`, which **flushes every tracked change in the context, including the
half-applied recipe mutation**, before the explicit `_recipeRepo.UpdateAsync(recipe)` on line 289.

Consequences:
1. The recipe is persisted as a side effect of the *cache* save, not the recipe save — the save is
   split into two independent transactions in an order the code does not intend.
2. If `_recipeRepo.UpdateAsync(recipe)` (line 289) — which additionally issues `_dbSet.Update(recipe)`
   marking the whole graph Modified — then throws (e.g. a concurrency or constraint error on the
   ingredient/tag graph), the database is left with the cache row already flagged `IsStale=true` and
   the recipe partially written by the earlier flush, with no enclosing transaction to roll either back.
3. The two `SaveChangesAsync` calls are not wrapped in a transaction, so there is no atomic
   "canonical doc + stale-mark" unit — directly contradicting the intent that the stale-mark tracks
   the committed canonical doc.

This is the same class of atomicity defect that Phase 14 fixed in commit `a957efd`
("wrap clear+set-primary in a transaction for atomicity").

**Fix:** Do not let the cache-repo save flush the recipe. Either (a) mark the cache stale **without**
its own `SaveChanges` and let the single recipe save commit both, or (b) wrap the whole update in an
explicit transaction. Minimal form of (a):

```csharp
// In MarkNutritionCacheStaleIfChangedAsync — mutate the tracked entity only, no SaveChanges here.
if (cache.CanonicalDocHash != newHash)
{
    cache.IsStale          = true;
    cache.CanonicalDocHash = newHash;
    // Do NOT call _nutritionCacheRepo.UpdateAsync(cache); the entity is already tracked.
    // The single _recipeRepo.UpdateAsync(recipe) → SaveChangesAsync() below commits both atomically.
}
```

If the cache entity is not guaranteed tracked (it is, because `FindAsync` returns a tracked entity),
prefer wrapping `UpdateAsync`'s body in `await using var tx = await _db.Database.BeginTransactionAsync();`
and committing once at the end. Note the same premature-flush also exists in `CreateAsync`
(`recipe.Id == 0` short-circuits there, so it is latent, not active) and in `SyncPrimaryPhotoUrlAsync`.

## Warnings

### WR-01: Single-token ingredients over-match and are mislabeled HIGH confidence

**File:** `src/CookBot.Infrastructure/Services/NutritionService.cs:208-227`

**Issue:** The match score is `matchCount / recipeTokens.Length` — the denominator is the **recipe**
token count only, never the CNF description's token count. A one-token ingredient ("salt", "sugar",
"butter", "eggs", "flour") scores `1/1 = 1.0` → **HIGH** against *any* CNF food whose genus-first
description merely contains that token (e.g. "salt" matches "Crackers, soda, salt-topped" or
"Cake mix, with salt"). The first such food with the smallest `|descLen - nameLen|` tie-break wins.
This silently produces plausible-but-wrong calories/macros stamped HIGH confidence, violating SC2's
confidence contract ("matched CNF FoodId/description surfaced; low-confidence flagged"). Because the
panel hides HIGH rows by default, a confidently-wrong match is the *least* visible to the user.

**Fix:** Incorporate CNF-side coverage so a 1-of-8-CNF-token match cannot score 1.0. For example use
an F1/Jaccard blend and/or require a minimum CNF coverage for HIGH:

```csharp
int cnfTokenCount = cnfTokens.Count;
double recall    = recipeTokens.Length == 0 ? 0 : (double)matchCount / recipeTokens.Length;
double precision = cnfTokenCount == 0 ? 0 : (double)matchCount / cnfTokenCount;
double score     = (precision + recall) == 0 ? 0 : 2 * precision * recall / (precision + recall); // F1
```

At minimum, gate HIGH on the CNF description not being dramatically longer than the matched token set.

### WR-02: Unbounded in-memory match scan — compute cost scales with (ingredients × 5,690) with no cap

**File:** `src/CookBot.Infrastructure/Services/NutritionService.cs:93-116, 201-218`

**Issue:** `ComputeAsync` loads **all** CNF foods (`~5,690` rows) with their conversion factors into
memory, then for **each** ingredient re-iterates the full list and, per food, re-`Split`s and rebuilds
a `HashSet<string>` of CNF tokens (lines 204-206). The recipe ingredient count is fully user-controlled
and is **not bounded anywhere** (no cap in `RecipeService` or the editor). A pathological recipe with
thousands of ingredient lines turns one CTA click into a multi-million-iteration, multi-allocation
synchronous scan on a Blazor Server circuit thread, blocking that user's circuit. The phase context
explicitly calls out "compute-DoS on a recipe with many ingredients." (Pure-perf O(n) is out of v1
scope, but the unbounded, user-driven, circuit-blocking aspect is a robustness defect.)

**Fix:** (1) Pre-tokenize each `CnfFood` once per compute into a `HashSet<string>` outside the
per-ingredient loop (build a `List<(CnfFood food, HashSet<string> tokens)>` once). (2) Add a hard cap
on ingredient count processed (e.g. first N, or reject with the error state) so a single CTA cannot be
weaponized into an unbounded scan.

### WR-03: Hash-input divergence between compute and stale-mark on the empty-canonical path

**File:** `src/CookBot.Infrastructure/Services/NutritionService.cs:79, 129` vs `src/CookBot.Application/Services/RecipeService.cs:369-371`

**Issue:** `NutritionService.ComputeAsync` hashes `canonicalJson = recipe.CanonicalDocumentJson ?? string.Empty`
and, when the canonical is null/empty, computes nutrition from a *synthesized* default `RecipeDocument`
but writes `CanonicalDocHash = ComputeHash("")` (hash of the empty string). `RecipeService.MarkNutritionCacheStaleIfChangedAsync`
**returns early** when `CanonicalDocumentJson` is null/empty (lines 370-371) and never writes a hash.
The structural invariant (DatabaseSeeder guard at `DatabaseSeeder.cs:88-94`) makes a persisted
null-canonical recipe a hard error, so in practice this branch should be unreachable for saved
recipes — but the compute path silently hashes `""` for a recipe that *can* be computed, so if such a
row ever exists the staleness comparison is undefined (compute writes `hash("")`, save never updates).
This is a latent correctness gap, not a live one.

**Fix:** Make `ComputeAsync` hash the *same* bytes the stale-mark uses, or refuse to compute when the
canonical is empty:

```csharp
if (string.IsNullOrEmpty(canonicalJson))
    throw new InvalidOperationException("Recipe has no canonical document — cannot compute nutrition.");
```

Or hash the serialized synthesized doc instead of `""` so both sides agree on what was computed.

### WR-04: No concurrency guard on the single 1:1 cache row — lost updates / duplicate-insert race

**File:** `src/CookBot.Infrastructure/Services/NutritionService.cs:131-176` and `src/CookBot.Application/Services/RecipeService.cs:381-391`

**Issue:** The cache row has no concurrency token (`RowVersion`/`xmin`), and two paths write it on
separate DbContext instances: `NutritionService.ComputeAsync` does a `FindAsync` → upsert →
`SaveChangesAsync`, and `RecipeService.MarkNutritionCacheStaleIfChangedAsync` does
`FindAsync` → set `IsStale=true` → save. Interleavings:
1. A save marks the row stale while a `ComputeAsync` is mid-flight; `ComputeAsync` then writes
   `IsStale=false` last, clobbering the stale-mark — the panel shows current values for a recipe that
   actually changed (correctness/freshness loss).
2. Two concurrent `ComputeAsync` calls (double-click, two tabs) both observe no existing row, both
   `Add`, and the second `SaveChangesAsync` throws a PK-violation `DbUpdateException` — surfaced as the
   panel's State 5 error rather than a clean result.

**Fix:** Add an EF concurrency token to `RecipeNutritionCache` (new migration — note this requires an
additive `AddColumn`, see also IN-04) and handle `DbUpdateConcurrencyException`/`DbUpdateException`
in `ComputeAsync` with a re-read-and-retry or an upsert that re-fetches on conflict. At minimum,
re-fetch the row inside `ComputeAsync` immediately before writing and re-evaluate staleness so a
concurrent stale-mark is not silently overwritten.

### WR-05: Panel's `matchedCount` (HIGH+MEDIUM) diverges from `cache.MatchedIngredients` (energy-bearing only)

**File:** `src/CookBot.Web/Components/Pages/RecipeView.razor:408, 484` vs `src/CookBot.Infrastructure/Services/NutritionService.cs:108-114`

**Issue:** The coverage summary renders `cache.MatchedIngredients`, which the service increments only
when `record.Confidence != "UNMATCHED" && record.EnergyKcal.HasValue` (line 108). The panel-local
`matchedCount` (line 408) counts `m.Confidence != "UNMATCHED"` from the parsed JSON — but a record can
be non-UNMATCHED yet have null energy only via the early-return at lines 286-304, which forces
`Confidence = "UNMATCHED"`. So today they coincide; however the "Show all N matches" button label
(line 538) and the default-hidden/visible row partition (lines 405-407) are computed from the
panel-local definition while the headline count comes from the service field. Any future change to
either definition will silently desync the headline number from the toggle count with no test guard.

**Fix:** Render a single source of truth — compute `matchedCount` in the panel from
`cache.MatchedIngredients`, or recompute the headline from the same `matches` list the toggle uses,
so the two numbers cannot drift.

### WR-06: `RecipeNutritionCache.Servings` snapshot stores the unclamped value, contradicting the per-serving math

**File:** `src/CookBot.Infrastructure/Services/NutritionService.cs:89, 141-145, 162-166`

**Issue:** `int servings = Math.Max(doc.Servings, 1)` is used as the per-serving divisor (correct,
no div-by-zero), but the persisted snapshot `existing.Servings = doc.Servings` stores the **raw**
`doc.Servings`, which can be `0` or negative. A consumer that later recomputes per-serving values as
`Total / cache.Servings` (or that displays "per N servings") will divide by zero / show "per 0
servings" while the stored `PerServingEnergyKcal` was actually divided by 1. The XML doc on the entity
even claims "Zero when Servings is null or zero," which the code does not implement.

**Fix:** Persist the clamped divisor (`existing.Servings = servings;`) so the stored snapshot matches
the divisor that was actually applied, or store both raw and effective servings explicitly.

## Info

### IN-01: Double JSON-LD `absoluteImageUrl` resolution logic duplicated three times

**File:** `src/CookBot.Web/Components/Pages/RecipeView.razor:755-779, 911-933, 1140-1162`

**Issue:** The "validate `PhotoUrl` → https, else compose against `BaseUri`" block is copy-pasted in
`LoadRecipeDocumentForPrerenderAsync`, the `OnAfterRenderAsync` JSON-LD rebuild, and `CalculateNutrition`.
Three identical ~20-line blocks invite drift (a fix to one will be missed in the others).

**Fix:** Extract `private string? ResolveAbsoluteImageUrl(string? photoUrl)` and call it from all three.

### IN-02: `EnergyKcal == 0` real CNF values are indistinguishable from "no energy" in the panel

**File:** `src/CookBot.Web/Components/Pages/RecipeView.razor:510-522`

**Issue:** Rows render `row.EnergyKcal.HasValue ? value.ToString("0") : "--"`. A legitimately matched
zero-calorie CNF food (e.g. water, some spices/extracts, diet sweeteners) has `EnergyKcal = 0.0`
(`HasValue == true`), so it renders "0" — which is correct — but the matcher's `matchedCount`/totals
treat a HIGH-confidence 0-kcal match as matched-with-zero-contribution, which is fine. No action
required beyond awareness: the "never render 0 for unmatched" invariant is satisfied because unmatched
sets energy to `null`, not `0`. Flagging only to document that the `0` vs `--` distinction is doing
real work and must not be "simplified."

### IN-03: `IngredientDensityProvider.EntryCount` reports 28 but doc/DI comments claim "≥23 entries"

**File:** `src/CookBot.Application/Services/IngredientDensityProvider.cs:118` and `src/CookBot.Application/DependencyInjection.cs:16-18`

**Issue:** `EntryCount => Densities.Count` returns the count of the **raw** dictionary (28 entries),
not the effective lookup surface (raw + normalized aliases). The XML summary says "Exposed for
≥23-entry count assertion"; the table currently has 28. This is fine, but any test asserting an exact
count will be brittle, and `NormalizedDensities` collisions (`TryAdd` "first wins") silently drop
alias coverage without surfacing in `EntryCount`.

**Fix:** If a test asserts entry count, assert `>= 23` (not `==`); consider exposing both raw and
normalized counts if precise coverage assertions are needed.

### IN-04: Migration `Down` drops nutrition tables (data loss on rollback) — additive-only invariant is satisfied on `Up` but not `Down`

**File:** `src/CookBot.Infrastructure/Migrations/20260608030954_AddNutritionTables.cs:97-107`

**Issue:** The stated invariant ("the migration only ADDS tables, no destructive ops") holds for `Up`
(three `CreateTable`s only — verified). The auto-generated `Down` drops `CnfConversionFactors`,
`RecipeNutritionCaches`, and `CnfFoods`. Migrations are forward-only per CLAUDE.md
("Migrations are forward-only"), so `Down` should never run — but if someone invokes
`dotnet ef database update <previous>` it silently destroys computed caches and the bundled CNF seed.
This matches EF's default and the project's other migrations, so it is informational, not a defect.

**Fix:** None required under the forward-only policy. Optionally make `Down` throw
`NotSupportedException` to enforce forward-only at the code level.

---

_Reviewed: 2026-06-07T00:00:00Z_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
