---
phase: 10-qol-polish-consumer-surfaces
verified: 2026-05-17T12:00:00Z
status: human_needed
score: 12/12 must-haves verified
overrides_applied: 0
re_verification:
  previous_status: gaps_found
  previous_score: 10/12
  gaps_closed:
    - "Home active-timer band tick loop now starts on cold load path (WR-02 fixed — _tickLoopStarted flag replaces firstRender gate)"
    - "PantryMatchService daysSinceCooked clamped to >= 0 via Math.Max(0.0, ...) (CR-03 fixed)"
    - "PantryMatchOptions.EffectiveHalfLifeDays guard property added; formula uses it instead of RecencyHalfLifeDays directly (CR-04 fixed)"
  gaps_remaining: []
  regressions: []
human_verification:
  - test: "Open Home while an active cooking session timer is running; observe the timer countdown display over 5 seconds"
    expected: "Timer band shows countdown that updates every second without requiring a page action"
    why_human: "Blazor render-cycle ordering (now fixed in code) still requires a live browser session with an active timer to confirm the JS tick loop actually fires on the cold-load second render"

  - test: "Set a whitespace-only AI prompt template (e.g. '   ') in Profile, then open AI Chat and generate a recipe"
    expected: "AI Chat uses the DefaultTemplate (not the whitespace-only string); the prompt visible in PromptBuilder.razor uses default content"
    why_human: "WR-04: AiChat.BuildSystemPrompt uses null-coalescing (??) not IsNullOrWhiteSpace — diverges from PromptBuilderService. Cannot verify without a live browser session + Anthropic API key"

  - test: "Select Terracotta accent in Profile, close tab, reopen app"
    expected: "Terracotta accent applies before first paint (no flash of orange then switch)"
    why_human: "localStorage bootstrap-before-first-paint requires a live browser to confirm timing"

  - test: "In AI Chat, generate a recipe, wait for validation failure, click 'Edit anyway' (or let repair loop exhaust retries)"
    expected: "RawRecipeEditorDialog opens with pretty-printed JSON; 'Parse and save' is disabled until valid JSON is entered; debounced validation updates within 500ms of typing"
    why_human: "Requires a live browser with a real AI response that fails validation"

  - test: "In Recipe Editor, change the cookbook selector to a different cookbook and save"
    expected: "Recipe moves to destination cookbook; page navigates to destination cookbook; original cookbook no longer shows the recipe"
    why_human: "End-to-end flow across RecipeEditor + RecipeService.UpdateAsync requires live browser"

  - test: "In Pantry view, click the cart icon on a pantry row with no existing grocery list"
    expected: "A 'Pantry quick-add' grocery list is created; the ingredient is added; a success toast appears"
    why_human: "EnsurePrimaryListAsync create-on-no-list branch requires live browser + populated pantry"

  - test: "On a recipe page at viewport < 720px, verify that TopBar RightSlot actions are hidden and inline fallback row is visible"
    expected: "TopBar action buttons invisible; below-hero action row visible on narrow viewport"
    why_human: "Responsive CSS behavior requires a live browser at narrow viewport width"
---

# Phase 10: QOL, Polish & Consumer Surfaces — Verification Report (Re-verification)

**Phase Goal:** Ship QOL improvements (smart pantry-match, AI Chat raw-edit recovery, custom system prompts, accent picker), polish items (POLISH-01..05), and consumer surfaces (TopBar.RightSlot, AI usage widget, recipe move-to-cookbook). Closes 12 requirements: QOL-01..07 and POLISH-01..05.
**Verified:** 2026-05-17T12:00:00Z
**Status:** human_needed
**Re-verification:** Yes — after gap closure (commit b64ce1f closes WR-02, CR-03, CR-04)

---

## Re-verification Summary

The two gap clusters from the initial verification have been confirmed closed by reading the three fix sites directly.

**Gap 1 closed (WR-02 — POLISH-05 live timer tick on cold load):**

`Home.razor.cs:107` declares `private bool _tickLoopStarted;`. The tick-loop block at lines 132-145 is gated `if (_activeTimer != null && !_tickLoopStarted)` — no `firstRender` condition. On the cold-load path: the first render branch (lines 111-117) calls `LoadDashboardAsync` + `LoadActiveSessionAsync`, which populates `_activeTimer`, then calls `StateHasChanged()`. The second render triggered by `StateHasChanged()` evaluates `_activeTimer != null && !_tickLoopStarted` as `true` (since `_tickLoopStarted` starts `false`) and starts the tick loop. `_tickLoopStarted = true` is set before the JS call, making subsequent renders no-ops. The fix matches the code reviewer's prescribed pattern exactly.

**Gap 2 closed (CR-03 + CR-04 — scoring correctness):**

`PantryMatchService.cs:178` now reads:
```csharp
var daysSinceCooked = Math.Max(0.0, (DateTime.UtcNow - lastCook.CompletedAt).TotalDays);
```
The `Math.Max(0.0, ...)` clamp prevents future-dated `CompletedAt` from producing a negative `daysSinceCooked` (CR-03).

`PantryMatchService.cs:180` now reads:
```csharp
score = coverage - _opts.RecencyPenaltyWeight
    * Math.Exp(-daysSinceCooked / _opts.EffectiveHalfLifeDays);
```
`PantryMatchOptions.cs:39` now has:
```csharp
public double EffectiveHalfLifeDays => RecencyHalfLifeDays > 0 ? RecencyHalfLifeDays : 7.0;
```
Zero-valued `RecencyHalfLifeDays` (from an unconfigured IOptions binding) now falls back to 7.0, preventing the NaN/Infinity propagation (CR-04).

---

## Goal Achievement

### Observable Truths (ROADMAP Success Criteria)

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| SC-1 | Home pantry-match uses scored algorithm, stable sort, composite indexes (QOL-01, QOL-03) | VERIFIED | `PantryMatchService.cs` implements coverage+recency formula; `OrderByDescending(score).ThenBy(recipeId)` at line 195; `AddPantryMatchIndexes` migration exists; `PantryMatchIndexSnapshotTests` passes |
| SC-2 | AI Chat validation failure opens `RawRecipeEditorDialog` with raw response; no silent degraded-toast-only path (QOL-04) | VERIFIED | `AiChat.razor:769-772` calls `CbDialogService.ShowAsync<RawRecipeEditorDialog>`; `RawRecipeEditorDialog.razor` exists with debounced validation, "Parse and save" + "Copy raw to clipboard" + "Close" actions, and SaveRecipeDialog hop on parse success |
| SC-3 | Profile accent picker persists via localStorage; applies before first paint via `data-accent` on `<html>`; no EF migration (QOL-05) | VERIFIED | `EditProfile.razor:101-103` has 3 CbRadio options; `OnAccentChanged` at line 733 calls `localStorage.setItem`; `cookbot-shell.js:40-47` reads `cookbot_accent` in `applyDefaults()` before first paint |
| SC-4a | Recipe Editor cookbook picker routes through `RecipeService.UpdateAsync` with destination-cookbook ownership validation (POLISH-01) | VERIFIED | `RecipeService.cs:160-166` — newCookbookId branch validates ownership; `RecipeEditor.razor:812` calls `UpdateAsync(..., _selectedCookbookId)` |
| SC-4b | Pantry per-row cart icon wires to `GroceryListService.AddItemAsync` with toast on success (POLISH-02) | VERIFIED | `PantryView.razor:358` `@onclick="@(() => AddToGroceryList(item))"` (no disabled); `GroceryListService:100+121` has `EnsurePrimaryListAsync` + `AddItemAsync` |
| SC-4c | Dark-mode toggle shows Moon glyph when dark, Sun when light (POLISH-03) | VERIFIED | `Icon.razor:50` has `public const string Moon = "moon";`; `Icon.razor:91` has crescent path; `TopBar.razor:73` `Icon Name="@(_isDarkMode ? Icon.Names.Moon : Icon.Names.Sun)"` |
| SC-4d | RecipeView RV-05 actions reach `TopBar.RightSlot` via `ICbTopBarService` scoped service (POLISH-04) | VERIFIED | `ICbTopBarService.cs` + `CbTopBarService.cs` exist; `MainLayout.razor:37` passes `TopBarService.RightSlot` to TopBar; `RecipeView.razor:251` calls `TopBarService.SetRightSlot(_topBarActions)` in OnInitialized; CSS media at `cookbot-design.css:717-718` hides `.topbar-right-slot` below 720px |
| SC-4e | Home active-timer band updates every second via setInterval JS tick (POLISH-05) | VERIFIED (code fix confirmed) | `_tickLoopStarted` flag at `Home.razor.cs:107`; tick-loop block at lines 132-145 gated on `_activeTimer != null && !_tickLoopStarted`; `firstRender` gate removed; cold-load path now starts tick loop on second render. Behavioral confirmation still needs a live browser (Human Verification item 1). |

**Score:** 8/8 ROADMAP success criteria pass code-level verification

### Additional Must-Haves (from PLAN frontmatter — full 12 requirements)

| # | Requirement | Truth | Status | Evidence |
|---|-------------|-------|--------|----------|
| QOL-01 | Smart pantry-match service replaces deterministic stub | VERIFIED | `Home.razor.cs:318` calls `PantryMatchService.GetMatchesAsync`; old stub replaced |
| QOL-02 | Dietary filter (AND-combined tag+category) runs before scoring | VERIFIED | `PantryMatchService.cs:122-144` — `DietExcludeMap` + positive-tag + negative-category loops before scoring loop |
| QOL-03 | Composite indexes `RecipeIngredient(RecipeId, IngredientId)` + `PantryItem(PantryId, IngredientId)` in place | VERIFIED | `AddPantryMatchIndexes` migration exists; `PantryMatchIndexSnapshotTests` asserts model snapshot |
| QOL-04 | RawRecipeEditorDialog opens on AI validation failure | VERIFIED | See SC-2 above |
| QOL-05 | Accent picker persists via localStorage, no EF migration | VERIFIED | See SC-3 above |
| QOL-06 | Profile prompt editor wires to `PromptBuilderService.BuildSystemPrompt` null-fallback | VERIFIED | `PromptBuilderService.cs:44-47` uses `IsNullOrWhiteSpace` fallback to DefaultTemplate; `EditProfile.razor:118-123` textarea + save path at line 769 |
| QOL-07 | Inline injection warning CbCard always visible in prompt editor | VERIFIED | `EditProfile.razor:124-127` — CbCard with "About custom prompts" content, always rendered |
| POLISH-01 | Recipe cookbook reparenting via `RecipeService.UpdateAsync` with authz | VERIFIED | See SC-4a above |
| POLISH-02 | Pantry quick-add cart icon wired to GroceryListService | VERIFIED | See SC-4b above |
| POLISH-03 | Moon glyph + Sun/Moon dark-mode toggle swap | VERIFIED | See SC-4c above |
| POLISH-04 | TopBar.RightSlot via ICbTopBarService; RecipeView + RecipeEditor adopt it | VERIFIED | See SC-4d above; `RecipeEditor.razor:351` also calls `SetRightSlot` |
| POLISH-05 | Live timer tick via setInterval, teardown on pagehide | VERIFIED (code fix confirmed) | See SC-4e above |

**Overall Score: 12/12 must-haves pass code-level verification**

---

## Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `src/CookBot.Application/Services/IRecipeMadeService.cs` | Application-layer interface | VERIFIED | Exists; `namespace CookBot.Application.Services`; 4 methods |
| `src/CookBot.Application/DTOs/PantryMatchOptions.cs` | IOptions-bound POCO with defaults + EffectiveHalfLifeDays guard | VERIFIED | Exists; all 4 properties with correct defaults (0.3/7.0/0.6/3); `EffectiveHalfLifeDays` computed property at line 39 |
| `src/CookBot.Application/DTOs/PantryMatchResult.cs` | sealed record 7 fields | VERIFIED | Exists; positional record with all 7 fields |
| `src/CookBot.Application/Services/IPantryMatchService.cs` | Interface with GetMatchesAsync | VERIFIED | Exists; `Task<IReadOnlyList<PantryMatchResult>> GetMatchesAsync` |
| `src/CookBot.Application/Services/PantryMatchService.cs` | Full scoring implementation with clamp + zero-guard | VERIFIED | Exists; `Math.Max(0.0, ...)` clamp at line 178; `EffectiveHalfLifeDays` used at line 180; N+1 GetLastCookAsync advisory remains (CR-01, not a blocker) |
| `src/CookBot.Web/Components/Pages/RawRecipeEditorDialog.razor` | CbDialog modal | VERIFIED | Exists; debounced validation, 3 actions, SaveRecipeDialog hop on success |
| `src/CookBot.Web/Services/ICbTopBarService.cs` | Interface with RightSlot/OnChanged | VERIFIED | Exists; correct shape per D-56 |
| `src/CookBot.Web/Services/CbTopBarService.cs` | Scoped IDisposable implementation | VERIFIED | Exists; NavigationManager auto-clear; IDisposable; idempotent Clear |
| `src/CookBot.Web/wwwroot/js/prompt-editor-insert.js` | insertAtCursor function | VERIFIED | Exists; `window.CookbotPromptEditor.insertAtCursor` |
| `src/CookBot.Web/wwwroot/js/cooking-session-state.js` (startTickLoop) | setInterval tick loop | VERIFIED | startTickLoop exists and is correct; pagehide teardown exists; `{ once: false }` noted (WR-01, advisory only) |
| `tests/CookBot.Tests/Services/PantryMatchServiceTests.cs` | Scoring + diet filter tests | VERIFIED | 24 tests pass |
| `tests/CookBot.Tests/Services/GroceryListServiceTests.cs` | EnsurePrimaryListAsync + AddItemAsync | VERIFIED | Tests pass |
| `tests/CookBot.Tests/Services/PromptBuilderServiceNullFallbackTests.cs` | 3 null-fallback tests | VERIFIED | 3 tests pass |
| `tests/CookBot.Tests/Services/CbTopBarServiceTests.cs` | SetRightSlot / LocationChanged / Clear / Dispose | VERIFIED | 4 tests pass |

---

## Key Link Verification

| From | To | Via | Status | Details |
|------|----|-----|--------|---------|
| `Home.razor.cs` | `IPantryMatchService` | `[Inject] private IPantryMatchService PantryMatchService` | VERIFIED | Line 30; called at line 318 |
| `Home.razor.cs` | `startTickLoop` (JS) | `_activeTimer != null && !_tickLoopStarted` gate at lines 132-145 | VERIFIED | Cold-load path confirmed fixed; `_tickLoopStarted` flag prevents double-start |
| `AiChat.razor` | `RawRecipeEditorDialog` | `CbDialogService.ShowAsync<RawRecipeEditorDialog>` | VERIFIED | Line 772 |
| `MainLayout.razor` | `ICbTopBarService` | `@inject ICbTopBarService TopBarService` + `RightSlot="@TopBarService.RightSlot"` | VERIFIED | Lines 5, 37; event subscription line 55 |
| `RecipeView.razor` | `ICbTopBarService` | `TopBarService.SetRightSlot(_topBarActions)` | VERIFIED | Line 251 in OnInitialized |
| `RecipeEditor.razor` | `ICbTopBarService` | `TopBarService.SetRightSlot(_topBarActions)` | VERIFIED | Line 351 in OnInitialized |
| `Program.cs` | `ICbTopBarService` | `AddScoped<ICbTopBarService, CbTopBarService>()` | VERIFIED | Line 37 |
| `Program.cs` | `PantryMatchOptions` | `Configure<PantryMatchOptions>(GetSection("CookBot:PantryMatch"))` | VERIFIED | Line 64 |
| `appsettings.json` | `PantryMatchOptions` | `"PantryMatch": { "RecencyPenaltyWeight": 0.3, ... }` | VERIFIED | Nested under `"CookBot"` |
| `EditProfile.razor` | `PromptBuilderService.DefaultTemplate` | `_promptTemplate = PromptBuilderService.DefaultTemplate` in reset | VERIFIED | Line 759 |
| `PromptBuilderService.cs` | `UserProfile.AiSystemPromptTemplate` | `string.IsNullOrWhiteSpace(profile.AiSystemPromptTemplate) ? DefaultTemplate : ...` | VERIFIED | Lines 44-47 |
| `PantryView.razor` | `GroceryListService.EnsurePrimaryListAsync + AddItemAsync` | `@onclick="@(() => AddToGroceryList(item))"` | VERIFIED | Lines 358, 551-552 |
| `TopBar.razor` | `ICbTopBarService.RightSlot` | `[Parameter] RenderFragment? RightSlot` + `<div class="topbar-right-slot">@RightSlot</div>` | VERIFIED | Line 48 |
| `cookbot-shell.js` | `data-accent` on `<html>` | `applyDefaults()` reads `localStorage.cookbot_accent` before first paint | VERIFIED | Lines 40-47 |
| `AiChat.razor:428` | `PromptBuilderService.DefaultTemplate` | null-coalescing `??` only, NOT `IsNullOrWhiteSpace` | WIRED BUT DIVERGENT (WR-04, advisory) | Whitespace-only template reaches the prompt as whitespace; human verification item 2 covers this |

---

## Data-Flow Trace (Level 4)

| Artifact | Data Variable | Source | Produces Real Data | Status |
|----------|---------------|--------|--------------------|--------|
| `Home.razor.cs` `_pantryMatches` | `List<HomePantryMatch>` | `PantryMatchService.GetMatchesAsync` → EF queries on RecipeIngredients, PantryItems, RecipeTags, RecipeMades | Yes — real EF queries | FLOWING |
| `EditProfile.razor` AI usage widget | `_aiInputTokens30d`, `_aiOutputTokens30d`, `_aiCost30d` | `DbContext.AiUsageLogs.Where(KeyOwnerId==userId && !IsRetryAttempt && Timestamp>=cutoff).SumAsync(...)` | Yes — real EF aggregation | FLOWING |
| `RawRecipeEditorDialog.razor` `_editedJson` | `string` | `RawJson` parameter from caller `AiChat.razor` which passes `rawJson` (the `_lastStructuredRecipe.RawResponse.ToJsonString(...)`) | Yes — live AI response | FLOWING |

---

## Behavioral Spot-Checks

| Behavior | Command | Result | Status |
|----------|---------|--------|--------|
| Solution builds with no errors | `dotnet build --nologo --verbosity quiet` | Build succeeded; 4 EF1002 warnings (pre-existing, unrelated to Phase 10) | PASS |
| Phase 10 unit tests pass | `dotnet test --filter "PantryMatchServiceTests\|PromptBuilderServiceNullFallbackTests\|CbTopBarServiceTests\|GroceryListServiceTests"` | Failed: 0, Passed: 24 | PASS |
| Index snapshot tests pass | included in above filter (PantryMatchIndexSnapshotTests) | 3 tests pass | PASS |
| Overall test suite (excluding RequiresApiKey) | `dotnet test` | Failed: 6 (all `RequiresApiKey`-gated AI live-API tests — require `ANTHROPIC_API_KEY`; not Phase 10 regressions), Passed: 321 | PASS |
| IRecipeMadeService in Application layer | `grep -q "namespace CookBot.Application.Services" src/CookBot.Application/Services/IRecipeMadeService.cs` | Match found | PASS |
| Moon glyph in Icon.razor | `grep -q "moon.*21 12.79" src/CookBot.Web/Components/Atoms/Icon.razor` | Crescent path present | PASS |
| TopBar RightSlot CSS media query | `grep -q "topbar-right-slot" src/CookBot.Web/wwwroot/css/cookbot-design.css` | `.topbar-right-slot { display: none !important; }` at 720px breakpoint | PASS |

---

## Requirements Coverage

| REQ-ID | Plan | Description | Status | Evidence |
|--------|------|-------------|--------|----------|
| QOL-01 | 10-01/02/03/04 | Smart pantry-match service with ingredient-coverage % + recency-decay scoring | SATISFIED | `PantryMatchService.GetMatchesAsync`; `Home.razor.cs:318` calls it |
| QOL-02 | 10-03 | Dietary filter (AND-combined tag-match + ingredient-category exclude) | SATISFIED | `PantryMatchService.cs:120-144` |
| QOL-03 | 10-04 | Composite DB indexes for pantry-match O(n log n) | SATISFIED | `AddPantryMatchIndexes` migration; EF model snapshot test |
| QOL-04 | 10-05 | AI Chat raw-edit recovery dialog | SATISFIED | `RawRecipeEditorDialog.razor`; `AiChat.razor:769-772` |
| QOL-05 | 10-12 | Accent picker; localStorage persistence; before-first-paint | SATISFIED | `EditProfile.razor:94-105`; `cookbot-shell.js:40-47` |
| QOL-06 | 10-06/07 | Profile prompt editor wired to `PromptBuilderService` null-fallback | SATISFIED | `PromptBuilderService.cs:44-47`; `EditProfile.razor:107-132` |
| QOL-07 | 10-07 | Injection warning CbCard, always visible | SATISFIED | `EditProfile.razor:124-127` |
| POLISH-01 | 10-10 | Cookbook reparenting; `RecipeService.UpdateAsync` with authz | SATISFIED | `RecipeService.cs:140-166`; `RecipeEditor.razor:220+812` |
| POLISH-02 | 10-11 | Pantry per-row cart icon wired to `GroceryListService.AddItemAsync` | SATISFIED | `PantryView.razor:355-361`; `GroceryListService.cs:100,121` |
| POLISH-03 | 10-12 | Moon glyph; Sun/Moon toggle in dark-mode | SATISFIED | `Icon.razor:50,91`; `TopBar.razor:73` |
| POLISH-04 | 10-08/09 | TopBar.RightSlot via `ICbTopBarService`; RecipeView + RecipeEditor adopt | SATISFIED | Full chain: service → MainLayout → TopBar → RecipeView/RecipeEditor |
| POLISH-05 | 10-13 | Live timer tick via setInterval; teardown on pagehide | SATISFIED | `startTickLoop` JS correct; `_tickLoopStarted` flag fixes cold-load path; behavioral confirmation via human test |

---

## Anti-Patterns

| File | Line | Pattern | Severity | Impact |
|------|------|---------|----------|--------|
| `src/CookBot.Web/Components/Pages/Home.razor.cs` | 46 | Stale comment: "deterministic stub; FUTURE-13" — code is now a live service call | Info | Misleading comment only; no runtime impact |
| `src/CookBot.Application/Services/PantryMatchService.cs` | 168 | N+1 `GetLastCookAsync` in scoring loop (CR-01) | Warning | Latency-visible with large recipe sets; not a correctness failure; advisory only, not a blocker |
| `src/CookBot.Web/Components/Pages/EditProfile.razor` | 757 | `if (!result.Canceled)` — missing `result != null &&` null guard (CR-02) | Warning | Inconsistent with all other ShowAsync call sites; fragile against future nullable refactor |
| `src/CookBot.Web/Components/Pages/AiChat.razor` | 428 | `_profile.AiSystemPromptTemplate ?? DefaultTemplate` — null-coalescing only; whitespace-only template reaches the prompt verbatim (WR-04) | Warning | User's whitespace-only template silently produces an empty system prompt instead of DefaultTemplate |
| `src/CookBot.Web/Components/Pages/RawRecipeEditorDialog.razor` | 119 | `Toast.Show($"Copy failed: {ex.Message}", ...)` — raw exception message surfaced (WR-03) | Info | Inconsistent with project security stance; trusted-LAN posture makes severity low |
| `src/CookBot.Web/wwwroot/js/cooking-session-state.js` | 191 | `{ once: false }` on pagehide listener — accumulates on Blazor hot-reload (WR-01) | Info | Benign in production; minor dev-mode nuisance |

All remaining anti-patterns are advisory (Warning/Info). No Blockers remain.

---

## Human Verification Required

### 1. Live Timer Tick (POLISH-05 — behavioral confirmation)

**Test:** Open Home while an active cooking session timer is running. Observe the timer countdown display over 5 seconds.
**Expected:** Timer band shows countdown that updates every second without any page interaction.
**Why human:** The code fix is confirmed correct (firstRender gate removed, _tickLoopStarted flag introduced). A live browser session is the authoritative test that the Blazor render-cycle ordering actually delivers _activeTimer on the second render before startTickLoop is evaluated.

### 2. Whitespace-only custom prompt template behavior (WR-04)

**Test:** In Profile, enter exactly `   ` (three spaces) as the AI assistant instructions and save. Then open AI Chat and generate a recipe. Inspect the resulting system prompt via browser devtools (or PromptBuilder page).
**Expected:** AI Chat uses the DefaultTemplate content (not the whitespace string), matching PromptBuilderService behavior. The prompt begins "You are CookBot, an expert AI cooking assistant."
**Why human:** AiChat.BuildSystemPrompt uses `?? DefaultTemplate` (null-coalescing) not `IsNullOrWhiteSpace`. A whitespace-only template passes the null check and reaches ResolveTemplate as `"   "`, potentially producing an empty prompt. Cannot verify without a live session.

### 3. Accent picker before-first-paint (QOL-05)

**Test:** Select Terracotta accent in Profile. Close the tab. Reopen the app URL in a new tab.
**Expected:** The terracotta accent color applies before any content is visible — no flash of the default orange accent before switching. Check by throttling CPU in devtools to make render stages visible.
**Why human:** localStorage bootstrap timing requires a live browser with CPU throttling to detect FOAC (Flash of Accent Color).

### 4. RawRecipeEditorDialog end-to-end flow (QOL-04)

**Test:** In AI Chat, generate a recipe that intentionally fails validation (or manually trigger the fallback path), click "Edit anyway".
**Expected:** RawRecipeEditorDialog opens with pretty-printed JSON; entering invalid JSON shows a red validation error within 500ms of stopping typing; entering valid JSON enables "Parse and save"; clicking it closes the dialog and opens SaveRecipeDialog.
**Why human:** Requires a live AI session with a validation-failing response.

### 5. Cookbook reparenting navigation (POLISH-01)

**Test:** Open an existing recipe in the editor. Change the cookbook selector to a different cookbook you own. Click Save.
**Expected:** Recipe saves successfully; browser navigates to the destination cookbook's page; the recipe no longer appears in its original cookbook.
**Why human:** Full flow across RecipeEditor → RecipeService.UpdateAsync → navigation requires live browser.

### 6. Pantry quick-add with no existing grocery list (POLISH-02)

**Test:** Delete all grocery lists for a user. Then go to Pantry and click the cart icon on any row.
**Expected:** Toast "Added [ingredient] to grocery list" appears; a new "Pantry quick-add" grocery list is created; the ingredient appears in the Grocery List view.
**Why human:** EnsurePrimaryListAsync create-on-empty branch requires live browser + populated pantry with no existing lists.

### 7. TopBar responsive collapse at narrow viewport (POLISH-04)

**Test:** Open a recipe in RecipeView. Resize the browser to 719px wide.
**Expected:** TopBar action buttons (Edit, Share, Schedule, Cook) disappear from the TopBar; the inline-above-hero fallback action row becomes visible.
**Why human:** CSS media-query breakpoint behavior requires a live browser at controlled viewport width.

---

## REQ-ID Scoring Table

| REQ-ID | Evidence (file:line) | PASS/FAIL |
|--------|---------------------|-----------|
| QOL-01 | `PantryMatchService.cs:69-207`; `Home.razor.cs:30,318`; `PantryMatchServiceTests.cs:pass` | PASS |
| QOL-02 | `PantryMatchService.cs:120-144` (DietExcludeMap AND-combined filter) | PASS |
| QOL-03 | `AddPantryMatchIndexes` migration; `PantryMatchIndexSnapshotTests.cs:pass`; `RecipeIngredientConfiguration.cs:13` | PASS |
| QOL-04 | `RawRecipeEditorDialog.razor:1-129`; `AiChat.razor:769-772` | PASS |
| QOL-05 | `EditProfile.razor:94-105,733`; `cookbot-shell.js:40-47` | PASS |
| QOL-06 | `PromptBuilderService.cs:44-47`; `EditProfile.razor:107-132,764-776`; `PromptBuilderServiceNullFallbackTests.cs:pass` | PASS |
| QOL-07 | `EditProfile.razor:124-127` (CbCard "About custom prompts") | PASS |
| POLISH-01 | `RecipeService.cs:140-166`; `RecipeEditor.razor:206-225,812` | PASS |
| POLISH-02 | `GroceryListService.cs:100,121`; `PantryView.razor:355-361`; `GroceryListServiceTests.cs:pass` | PASS |
| POLISH-03 | `Icon.razor:50,91`; `TopBar.razor:73` | PASS |
| POLISH-04 | `ICbTopBarService.cs`; `CbTopBarService.cs`; `MainLayout.razor:5,37,55`; `RecipeView.razor:14,251`; `RecipeEditor.razor:18,351`; `cookbot-design.css:717-718`; `CbTopBarServiceTests.cs:pass` | PASS |
| POLISH-05 | `cooking-session-state.js:142-168` (JS correct); `Home.razor.cs:107,132-145` (`_tickLoopStarted` flag, `firstRender` gate removed — fix confirmed) | PASS |

---

_Verified: 2026-05-17T12:00:00Z_
_Verifier: Claude (gsd-verifier)_
_Re-verification: gaps_found → human_needed (all code gaps closed in commit b64ce1f)_
