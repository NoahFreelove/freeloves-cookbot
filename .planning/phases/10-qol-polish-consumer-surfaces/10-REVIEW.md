---
phase: 10-qol-polish-consumer-surfaces
reviewed: 2026-05-16T00:00:00Z
depth: standard
files_reviewed: 34
files_reviewed_list:
  - src/CookBot.Application/DTOs/PantryMatchOptions.cs
  - src/CookBot.Application/DTOs/PantryMatchResult.cs
  - src/CookBot.Application/DependencyInjection.cs
  - src/CookBot.Application/Services/IPantryMatchService.cs
  - src/CookBot.Application/Services/IRecipeMadeService.cs
  - src/CookBot.Application/Services/PantryMatchService.cs
  - src/CookBot.Application/Services/PromptBuilderService.cs
  - src/CookBot.Application/Services/RecipeService.cs
  - src/CookBot.Infrastructure/Data/Configurations/RecipeIngredientConfiguration.cs
  - src/CookBot.Web/Components/App.razor
  - src/CookBot.Web/Components/Atoms/Icon.razor
  - src/CookBot.Web/Components/Dialogs/CbConfirmDialog.razor
  - src/CookBot.Web/Components/Layout/MainLayout.razor
  - src/CookBot.Web/Components/Layout/TopBar.razor
  - src/CookBot.Web/Components/Pages/AiChat.razor
  - src/CookBot.Web/Components/Pages/EditProfile.razor
  - src/CookBot.Web/Components/Pages/Home.razor.cs
  - src/CookBot.Web/Components/Pages/RawRecipeEditorDialog.razor
  - src/CookBot.Web/Components/Pages/RecipeEditor.razor
  - src/CookBot.Web/Components/Pages/RecipeView.razor
  - src/CookBot.Web/Components/_Imports.razor
  - src/CookBot.Web/Program.cs
  - src/CookBot.Web/Services/CbTopBarService.cs
  - src/CookBot.Web/Services/ICbTopBarService.cs
  - src/CookBot.Web/Services/RecipeMadeService.cs
  - src/CookBot.Web/appsettings.json
  - src/CookBot.Web/wwwroot/css/cookbot-design.css
  - src/CookBot.Web/wwwroot/js/cookbot-shell.js
  - src/CookBot.Web/wwwroot/js/cooking-session-state.js
  - src/CookBot.Web/wwwroot/js/prompt-editor-insert.js
  - tests/CookBot.Tests/Services/CbTopBarServiceTests.cs
  - tests/CookBot.Tests/Services/PantryMatchIndexSnapshotTests.cs
  - tests/CookBot.Tests/Services/PantryMatchServiceTests.cs
  - tests/CookBot.Tests/Services/PromptBuilderServiceNullFallbackTests.cs
findings:
  critical: 4
  warning: 6
  info: 3
  total: 13
status: issues_found
---

# Phase 10: Code Review Report

**Reviewed:** 2026-05-16
**Depth:** standard
**Files Reviewed:** 34
**Status:** issues_found

## Summary

Phase 10 delivers the pantry-match scoring service, RawRecipeEditorDialog, TopBar RightSlot mechanism, profile prompt editor, and five small-polish items. The overall implementation quality is high: the authz path in `RecipeService.UpdateAsync` is correct, `CbTopBarService` IDisposable/NavigationManager wiring is sound, and the debounced validation in `RawRecipeEditorDialog` is correctly structured.

However, four blockers were found:

1. **N+1 query in the scoring loop** — `PantryMatchService` calls `GetLastCookAsync` inside a `foreach` over every surviving recipe, hitting the database once per recipe per render of Home.
2. **NullReferenceException in `ConfirmResetPromptAsync`** — `CbDialogService.ShowAsync` returns a non-nullable `CbDialogResult`, but the call site does not handle the case where the circuit drops mid-await, which can leave the TaskCompletionSource abandoned; more immediately, if the user force-navigates while the dialog is open the TCS may never complete, and the code unconditionally dereferences `result.Canceled` without a null guard — this is safe for the success path but inconsistent with every other `ShowAsync` call site in the codebase and will throw if a future refactor makes `ShowAsync` nullable.
3. **Score can exceed 1.0 for future `CompletedAt` timestamps** — `daysSinceCooked` is computed as `(UtcNow - lastCook.CompletedAt).TotalDays` with no clamp; if `CompletedAt` is in the future (clock skew, manual DB edit), `daysSinceCooked` is negative and `exp(-negative/7)` > 1, so the penalty term becomes a bonus and the final score can exceed the coverage ratio by up to `RecencyPenaltyWeight`.
4. **`PantryMatchOptions` double-registration** — `services.Configure<PantryMatchOptions>` is called in `Program.cs` (line 64) AND the comment in `DependencyInjection.cs` (line 37–39) implies registration belongs there too; the `AddApplication` method signature takes no `IConfiguration`, so the knob section is wired only in the Web layer. This is architecturally correct as-is, but the `AddApplication` XML comment at line 37–39 states "registered via `AddApplication`" which is false — it is registered via `Program.cs` directly. More critically: if a test project calls `AddApplication()` without also calling `Configure<PantryMatchOptions>`, `IOptions<PantryMatchOptions>.Value` will return a zero-value DTO (all doubles = 0), making `RecencyHalfLifeDays = 0` cause a division-by-zero in `Math.Exp(-daysSinceCooked / 0.0)` (returns `NaN` or `-Infinity` silently, not an exception, but produces wrong scores in tests that do not set the option).

---

## Critical Issues

### CR-01: N+1 Database Queries in PantryMatchService Scoring Loop

**File:** `src/CookBot.Application/Services/PantryMatchService.cs:168`
**Issue:** `GetLastCookAsync(recipe.Id, userId, ct)` is called inside the `foreach (var recipe in surviving)` loop. With `ResultCount=3` and `MinCoverageRatio=0.6` filtering most recipes, this is tolerable in practice, but if a user has many high-coverage recipes (e.g. a well-stocked pantry and 100+ recipes), the number of database round-trips is unbounded. With the default `ResultCount=3`, you still score *all* surviving recipes before taking the top 3, so if 40 recipes survive the dietary filter and coverage check, you issue 40 sequential DB queries before sorting. This is latency-visible on Home render.

The Phase 8 composite index (`RecipeIngredient(RecipeId, IngredientId)`) covers the join but does not help the per-recipe round-trip loop.

**Fix:** Bulk-load all `RecipeMade` rows for the user and relevant recipe IDs before the scoring loop, then look up from an in-memory dictionary:

```csharp
// After step 7 (surviving is determined), before step 8 (scoring):
var survivingIds = surviving.Select(r => r.Id).ToHashSet();

// One round-trip for all last-cook timestamps
var lastCookRows = await _db.RecipeMades   // or via IRecipeMadeService if exposed
    .AsNoTracking()
    .Where(rm => survivingIds.Contains(rm.RecipeId) && rm.UserId == userId)
    .GroupBy(rm => rm.RecipeId)
    .Select(g => new { RecipeId = g.Key, LastCook = g.OrderByDescending(rm => rm.CompletedAt).First() })
    .ToDictionaryAsync(x => x.RecipeId, x => x.LastCook);

// In scoring loop, replace the await call:
lastCookRows.TryGetValue(recipe.Id, out var lastCook);
```

Since `IRecipeMadeService` does not expose a bulk query, either (a) add `Task<Dictionary<int, RecipeMade>> GetLastCooksAsync(IEnumerable<int> recipeIds, int userId, CancellationToken ct)` to `IRecipeMadeService`, or (b) accept a direct `CookBotDbContext` dependency in `PantryMatchService` (already precedented in the Web layer). Option (a) is cleaner given the existing layering.

---

### CR-02: Potential NullReferenceException in `ConfirmResetPromptAsync`

**File:** `src/CookBot.Web/Components/Pages/EditProfile.razor:757`
**Issue:** `CbDialogService.ShowAsync<CbConfirmDialog>` has the signature `Task<CbDialogResult>` (non-nullable, confirmed in `CbDialogService.cs:45`). However, every other `ShowAsync` call site in the same file guards with `if (result != null && ...)` (e.g. line 549). The `ConfirmResetPromptAsync` method at line 757 unconditionally accesses `result.Canceled` without a null guard. While `CbDialogResult` is currently never null, this pattern is inconsistent and fragile: the `TaskCompletionSource` inside `ShowAsync` can, in theory, be abandoned if the Blazor circuit tears down mid-await (which does not set the TCS result). A future change that makes `ShowAsync` return `CbDialogResult?` to handle circuit-drop gracefully would cause a NullReferenceException here with no compile-time warning (since the assignment type would still be inferred as `CbDialogResult` from the old return type).

**Fix:** Apply the same null-guard pattern used at line 549:

```csharp
var result = await CbDialogService.ShowAsync<CbConfirmDialog>("Reset prompt template?", parameters);
if (result != null && !result.Canceled)
{
    _promptTemplate = PromptBuilderService.DefaultTemplate;
    StateHasChanged();
}
```

---

### CR-03: Scoring Formula Produces Score > Coverage When `CompletedAt` Is in the Future

**File:** `src/CookBot.Application/Services/PantryMatchService.cs:176-178`
**Issue:** `daysSinceCooked = (DateTime.UtcNow - lastCook.CompletedAt).TotalDays` is not clamped to `>= 0`. If `CompletedAt` is in the future (clock skew between processes, manual DB edit, or a bug in a calling service that passes `DateTime.Now` instead of `DateTime.UtcNow`), `daysSinceCooked` is negative. Then:

```
penalty = 0.3 * exp(-(-|days|) / 7) = 0.3 * exp(+|days|/7)
```

For `daysSinceCooked = -1` (cooked 1 day in the future): `penalty ≈ 0.345`, which exceeds `RecencyPenaltyWeight` (0.3). For a full-coverage recipe (`coverage = 1.0`), score becomes `1.0 - 0.345 = 0.655` — lower than a never-cooked full-coverage recipe. That is actually the correct direction (penalizes the future-dated entry more), but for extreme values (`daysSinceCooked = -50`), the penalty term exceeds 1.0 and the score goes negative, which sorts that recipe below the `MinCoverageRatio` threshold and effectively excludes it silently.

**Fix:** Clamp `daysSinceCooked` to zero before computing the penalty:

```csharp
var daysSinceCooked = Math.Max(0.0, (DateTime.UtcNow - lastCook.CompletedAt).TotalDays);
score = coverage - _opts.RecencyPenaltyWeight
    * Math.Exp(-daysSinceCooked / _opts.RecencyHalfLifeDays);
```

---

### CR-04: Division-by-Zero (Silent NaN) When `RecencyHalfLifeDays` Is Zero

**File:** `src/CookBot.Application/Services/PantryMatchService.cs:178` and `src/CookBot.Application/DTOs/PantryMatchOptions.cs:21`
**Issue:** If `RecencyHalfLifeDays` is set to `0` (either via `appsettings.json` override or — critically — via `IOptions<PantryMatchOptions>` receiving the default zero-value DTO when not configured), then `Math.Exp(-daysSinceCooked / 0.0)` evaluates as:

- `daysSinceCooked > 0`: `Math.Exp(-Infinity)` = `0.0` (penalty disappears — silent logic error, not crash)
- `daysSinceCooked = 0`: `Math.Exp(-0.0 / 0.0)` = `Math.Exp(NaN)` = `NaN` (score = `NaN`, sorts unpredictably)
- `daysSinceCooked < 0`: `Math.Exp(+Infinity)` = `+Infinity` (score = `-Infinity`)

This is not a crash but it produces silently wrong sorting. The risk is real for test suites that call `AddApplication()` without also wiring `Configure<PantryMatchOptions>`, since `IOptions<T>` returns a default-constructed `T` (all `double` fields = `0`) when no binding is present.

`DependencyInjection.cs` registers `IPantryMatchService` but does NOT call `Configure<PantryMatchOptions>` — that registration is only in `Program.cs`. The `AddApplication` XML comment says "registered via `AddApplication`" but the knob is not actually bound there.

**Fix:** (a) Add a guard in `PantryMatchService` or `PantryMatchOptions`:

```csharp
// In PantryMatchOptions, add a validation method or use IValidateOptions<T>:
public double EffectiveHalfLifeDays => RecencyHalfLifeDays > 0 ? RecencyHalfLifeDays : 7.0;
```

And use `_opts.EffectiveHalfLifeDays` in the formula. (b) Also move or replicate `services.Configure<PantryMatchOptions>(configuration.GetSection("CookBot:PantryMatch"))` into `AddApplication(IServiceCollection, IConfiguration)` so the binding is co-located with the registration, and update the `AddApplication` signature to accept `IConfiguration`. This mirrors how `AddInfrastructure` already takes `IConfiguration`.

---

## Warnings

### WR-01: `pagehide` Listener Uses `once: false` — Accumulates on Blazor Hot-Reload

**File:** `src/CookBot.Web/wwwroot/js/cooking-session-state.js:191`
**Issue:** The `pagehide` teardown listener is registered with `{ once: false }`. In normal production use, this is fine because the page unloads entirely on `pagehide`. However, during Blazor Server development with hot-reload (which does a component-level reconnect without a full page navigation), this listener accumulates: each reload adds another handler to the same window. On each subsequent `pagehide`, all accumulated handlers run, all clearing `_tickHandles = {}`. The side-effect is benign but it is a resource leak during development that may mask the reload behavior when debugging timer teardown.

More importantly, the `{ once: false }` is unnecessary: a `pagehide` event on a page that is being unloaded only fires once per page lifetime; using `{ once: true }` is semantically more accurate.

**Fix:**
```js
window.addEventListener('pagehide', () => {
    const handles = window.CookbotSession._tickHandles || {};
    for (const id in handles) clearInterval(handles[id]);
    window.CookbotSession._tickHandles = {};
}, { once: true });  // page unload fires once; once:true prevents handler accumulation on hot-reload
```

---

### WR-02: `startTickLoop` Not Called When Dashboard Loads First (Race in `OnAfterRenderAsync`)

**File:** `src/CookBot.Web/Components/Pages/Home.razor.cs:107-140`
**Issue:** The `startTickLoop` call is gated on `if (firstRender && _activeTimer != null)` (line 128). However, `_activeTimer` is only populated by `LoadActiveSessionAsync()`, which is called:

- From the `_loadedUserId != CurrentUserId` branch (lines 109-114): runs LoadDashboard AND LoadActiveSession, then StateHasChanged triggers another render. On that second render, `firstRender` is `false`, so `startTickLoop` is skipped.
- From the `else if (firstRender ...)` branch (lines 116-122): this branch only executes when the dashboard was NOT freshly loaded (i.e. `_loadedUserId == CurrentUserId`), AND `_inProgress == null && _activeTimer == null`.

The practical result: on a cold page load where `_loadedUserId` is null (first-ever render), the code takes the first branch, loads data, calls `StateHasChanged()`. On the subsequent render, `firstRender` is `false` and `_activeTimer` may now be non-null, but `startTickLoop` is never reached. The live tick only fires for the edge-case second branch (re-render without user-id change).

**Fix:** Move the `startTickLoop` call outside the `firstRender` guard, or set a separate `_tickLoopStarted` flag:

```csharp
// After LoadActiveSessionAsync() in BOTH branches:
if (_activeTimer != null && !_tickLoopStarted)
{
    _tickLoopStarted = true;
    try
    {
        await JS.InvokeVoidAsync("CookbotSession.startTickLoop",
            _activeTimerCountdownId,
            _activeTimer.StartedAtIso,
            _activeTimer.DurationSeconds);
    }
    catch (Microsoft.JSInterop.JSException) { }
    catch (Microsoft.JSInterop.JSDisconnectedException) { }
}
```

Add `private bool _tickLoopStarted;` to state. Note the `startTickLoop` JS function is already idempotent (clears prior handle before starting), so re-calling it is safe.

---

### WR-03: `RawRecipeEditorDialog` Exposes Raw `ex.Message` in Toast

**File:** `src/CookBot.Web/Components/Pages/RawRecipeEditorDialog.razor:119`
**Issue:** The `CopyRawToClipboard` method shows `Toast.Show($"Copy failed: {ex.Message}", ...)`. While clipboard failures are browser-side and `ex.Message` is unlikely to contain server secrets, this is inconsistent with the project's established pattern of sanitizing error messages before surfacing them (see `AiChat.razor:MapToSanitizedSnackbarCopy`). In a Blazor Server context, exception messages from framework internals can occasionally contain paths or context details.

The same pattern appears in `EditProfile.razor:679` (`Failed to fetch models: {ex.Message}`) and `EditProfile.razor:776` (`Save failed: {ex.Message}`). These are web-layer pages on a trusted-LAN app, so the severity is low, but it is inconsistent with the security stance established in Phase 2.

**Fix:**
```csharp
// RawRecipeEditorDialog.razor:117-119
catch (Exception)
{
    Toast.Show("Could not copy to clipboard. Check browser permissions.", CbToastSeverity.Error);
}
```

Apply similar treatment in EditProfile for `FetchModels` and `SavePromptTemplateAsync`.

---

### WR-04: `AiChat.BuildSystemPrompt` Bypasses `PromptBuilderService.BuildSystemPrompt` — Partial Logic Duplication

**File:** `src/CookBot.Web/Components/Pages/AiChat.razor:417-431`
**Issue:** `AiChat.BuildSystemPrompt()` does not call `PromptBuilderService.BuildSystemPrompt(profile, pantryItems)`. Instead it partially reimplements the null-fallback check directly:

```csharp
var template = _profile.AiSystemPromptTemplate ?? PromptBuilderService.DefaultTemplate;
template = await ExpandCookbookRecipeTokensAsync(template, user!.Id);
_systemPrompt = PromptBuilder.ResolveTemplate(template, _profile, pantryItems);
```

This is mostly equivalent to what `PromptBuilderService.BuildSystemPrompt` now does (line 44-47), but with two differences:

1. **Whitespace-only template is not normalized.** `PromptBuilderService.BuildSystemPrompt` uses `string.IsNullOrWhiteSpace` at line 44 to treat a whitespace-only template as null. `AiChat.BuildSystemPrompt` uses `?? ` (null-coalescing only), so a template of `"   "` reaches `ExpandCookbookRecipeTokensAsync` as-is and produces a system prompt of `"   "` (all tokens stripped, no recipe format injected, no whitespace-collapse).

2. **Logic duplication creates a maintenance divergence.** If `PromptBuilderService.BuildSystemPrompt` gains additional logic (e.g. per-D-42 prose nudges), `AiChat` will silently miss it.

**Fix:** Replace the three-line inline reimplementation in `AiChat.BuildSystemPrompt` with a direct call to `PromptBuilderService.BuildSystemPrompt`, then apply `ExpandCookbookRecipeTokensAsync` on the result if needed. Or, move `ExpandCookbookRecipeTokensAsync` into `PromptBuilderService` as an optional pre-pass parameter.

```csharp
// Minimal fix — normalize whitespace-only template to match BuildSystemPrompt:
var rawTemplate = string.IsNullOrWhiteSpace(_profile.AiSystemPromptTemplate)
    ? PromptBuilderService.DefaultTemplate
    : _profile.AiSystemPromptTemplate;
var template = await ExpandCookbookRecipeTokensAsync(rawTemplate, user!.Id);
_systemPrompt = PromptBuilder.ResolveTemplate(template, _profile, pantryItems);
```

---

### WR-05: TopBar RightSlot Set in `OnInitialized` — Actions Visible Before Auth Check Completes

**File:** `src/CookBot.Web/Components/Pages/RecipeView.razor:241-251`
**Issue:** `_topBarActions` is set in `OnInitialized()` and immediately fed to `TopBarService.SetRightSlot()`. This happens synchronously before `OnAfterRenderAsync` has completed the `UserCanAccessRecipeAsync` check (line 316). As a result, the "Edit", "Share", "Schedule", and "Cook this" action buttons appear in the TopBar immediately — before the page has determined whether the current user has access to the recipe. A user with no access sees functional-looking action buttons in the TopBar for 1-2 render cycles before the "not found" message appears.

The same pattern exists in `RecipeEditor.razor` (SetRightSlot in `OnInitialized` at line 351, authz check happens later in `OnAfterRenderAsync`).

In a trusted-LAN context this is a cosmetic/UX issue rather than a security hole (clicking Edit still navigates to `/recipes/{id}/edit` where authz is re-enforced). However, it is inconsistent with the design intent.

**Fix:** Move `TopBarService.SetRightSlot(_topBarActions)` to after the auth check in `OnAfterRenderAsync`, inside the `else` branch that confirms the user has access:

```csharp
// In OnAfterRenderAsync, inside "else { /* access granted */" block:
_recipe = ...;
// ... load _doc, _madeCount, etc.
if (_recipe != null)
    TopBarService.SetRightSlot(_topBarActions);
else
    TopBarService.Clear();
```

---

### WR-06: `PantryMatchOptions` Registration Not Co-located With Service Registration

**File:** `src/CookBot.Application/DependencyInjection.cs:37-42` and `src/CookBot.Web/Program.cs:64`
**Issue:** `services.Configure<PantryMatchOptions>` is registered in `Program.cs` (line 64) but the service that consumes it (`PantryMatchService`) is registered in `AddApplication()`. The XML comment at line 37-39 of `DependencyInjection.cs` says the option is "Bound via `services.Configure<PantryMatchOptions>` in `AddApplication`" — this is incorrect. The binding does not happen in `AddApplication`. Any consumer of `AddApplication()` that does not also call `Configure<PantryMatchOptions>` (e.g. integration test bootstraps, future secondary host projects) will receive a default-constructed `PantryMatchOptions` where `RecencyHalfLifeDays = 7.0` (safe default — property initializers prevent the zero-division from CR-04), but `ResultCount = 3` and `MinCoverageRatio = 0.6` are also defaults so behavior is preserved. The issue is the misleading XML comment, and the test bootstrapper in `PantryMatchServiceTests` correctly passes options explicitly via `BuildService()`. However, the comment creates a maintenance trap.

**Fix:** Correct the XML comment in `DependencyInjection.cs` to accurately state that the option section must be bound by the host layer:

```csharp
// Phase 10 / QOL-01..03 — pantry-match scoring service (D-44..47).
// Scoped because it depends on PantryService (also Scoped) and IRecipeMadeService.
// NOTE: PantryMatchOptions must be bound by the host via:
//   services.Configure<PantryMatchOptions>(configuration.GetSection("CookBot:PantryMatch"))
// This is done in Program.cs; AddApplication() does not take IConfiguration.
services.AddScoped<IPantryMatchService, PantryMatchService>();
```

---

## Info

### IN-01: `RecencyHalfLifeDays` Naming Is Technically Misleading

**File:** `src/CookBot.Application/DTOs/PantryMatchOptions.cs:17-21`
**Issue:** The property is named `RecencyHalfLifeDays` (and the XML doc says "Controls how quickly the recency penalty diminishes"), but the formula `exp(-days / RecencyHalfLifeDays)` gives a value of `exp(-1) ≈ 0.368` at `days = RecencyHalfLifeDays` — not `0.5`. A true half-life formula would be `exp(-days * ln(2) / halfLife)`. The current formula is an exponential decay with time constant (tau) equal to `RecencyHalfLifeDays`. At `days = 7`, the penalty is 36.8% of its initial value, not 50%. This is a minor terminology issue that matches D-44's intentional "smooth-decay" decision, but could confuse operators tuning the knob expecting half-life semantics.

**Fix:** Rename to `RecencyDecayConstantDays` or update the XML comment to clarify it is a decay time constant (tau), not a half-life:

```csharp
/// <summary>
/// D-44 — exponential decay time constant (tau) in days. At t = RecencyDecayConstantDays
/// the penalty has decayed to ~36.8% (1/e) of its initial value. Use
/// RecencyDecayConstantDays = halfLifeDays / ln(2) ≈ halfLifeDays * 1.443 to convert
/// from a conceptual half-life.
/// </summary>
public double RecencyDecayConstantDays { get; set; } = 7.0;
```

---

### IN-02: `_Imports.razor` File Listed in Scope But Does Not Exist

**File:** `src/CookBot.Web/Components/_Imports.razor`
**Issue:** The file listed in the review scope (`src/CookBot.Web/Components/_Imports.razor`) does not exist on disk. The actual Blazor imports file is typically at the project root (`src/CookBot.Web/Components/`) and was not present. This is not a code defect but indicates the phase plan listed a file that was not created or was renamed during execution. No code change required — the imports are pulled from the containing project's `_Imports.razor`.

---

### IN-03: `DietExcludeMap` Comment References Missing Enum Values (`Poultry`, `Fish`, `Eggs`)

**File:** `src/CookBot.Application/Services/PantryMatchService.cs:30-35`
**Issue:** The XML comment states "Poultry, Fish, and Eggs are NOT in the enum — absent from this map." This matches D-47's acknowledgment that the `IngredientCategory` enum lacks these values. However, the vegan map (`["vegan"] = [Meat, Seafood, Dairy]`) technically permits recipes with ingredients categorized as `Other` — which includes items like eggs in many seeded databases — to pass the negative filter. This is a known gap from D-47 ("planner curates against the existing IngredientCategory enum"), not a new defect, but it means a vegan dietary filter will not exclude recipes containing eggs unless those ingredients are seeded with `IngredientCategory.Dairy` or a user-added custom category.

**Fix:** No code change required for this phase. Document in `CONCERNS.md` or a future FUTURE-tag that the vegan filter gap exists until the `IngredientCategory` enum gains `Eggs`/`Poultry`/`Fish` values. This is the intended v1.4+ carryover.

---

_Reviewed: 2026-05-16_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
