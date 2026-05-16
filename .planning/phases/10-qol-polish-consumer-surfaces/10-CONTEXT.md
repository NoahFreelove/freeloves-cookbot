# Phase 10: QOL, Polish & Consumer Surfaces - Context

**Gathered:** 2026-05-16
**Status:** Ready for planning
**Mode:** discuss (4 user-selected gray areas, all resolved by user)

<domain>
## Phase Boundary

Close v1.3 by converting infrastructure laid down in Phases 8 + 9 into consumer-visible features and clearing the five polish-debt items. Twelve requirements across two buckets — `QOL-01..07` (smart pantry-match service, AI Chat raw-edit dialog, accent picker, Profile prompt editor, prompt-injection warning) and `POLISH-01..05` (cookbook reparenting, pantry quick-add, moon glyph, TopBar.RightSlot service, live timer tick) — all **code-only**: no new entities, no EF migrations, no NuGet adds beyond what Phase 8 (`Verify.Xunit`) and Phase 9 (`Microsoft.AspNetCore.DataProtection.EntityFrameworkCore`, `HealthChecks.EntityFrameworkCore`) already shipped.

**This phase delivers the consumer-surface payoff of v1.3.** A fresh self-host install at v1.3 close behaves observably better than v1.2: the Home pantry-match hero produces ranked, recency-aware, dietary-filtered matches instead of the deterministic 60%-coverage stub; AI Chat validation failures open a recoverable raw-edit dialog instead of a degraded toast; the per-key-owner AI cost widget surfaces the `AiUsageLog` rows Phase 9 started writing; Profile gains a custom-prompt editor with a clickable variable palette; and the small-stuff (cookbook reparenting, pantry quick-add, moon glyph, TopBar.RightSlot, live timer tick) is closed.

**In scope:** QOL-01..07, POLISH-01..05 (12 requirements). All target existing files; the only new files are a new `IPantryMatchService` (Application layer), a new `IPantryMatchOptions` (Application layer), a new `ICbTopBarService` (Web layer), a new `RawRecipeEditorDialog` Razor component, and a Moon glyph entry in `Icon.razor`. Phase 10 wires the per-user AI usage widget on EditProfile that consumes Phase 9's `AiUsageLog` rows (PROD-17 read surface — Phase 9 wrote the rows; Phase 10 renders them).

**Not in scope** (do not pull forward — explicit v1.4+ deferrals):
- Per-user spending caps / billing quotas → v1.4+
- Cross-user admin telemetry view (PITFALL M9 admin surface) → v1.4+
- `CookBotSettings.TelemetryEnabled` killswitch → v1.4+
- Case-insensitive tag dedup (Phase 8 D-34 trim+case-preserve stays) → v1.4+
- Multi-photo / photo gallery (single hero photo, Phase 9 D-38) → v1.4+
- Photo-in-PDF (Phase 9 D-40 omits) → v1.4+
- Smart pantry-match expiration-aware scoring (REQUIREMENTS Out of Scope — anti-feature at this scope) → v1.4+
- AI prompt editor auto-complete in textarea (we ship clickable chip row instead) → v1.4+
- TopBar.LeftSlot symmetry — Phase 10 only ships RightSlot
- TopBar slot adoption in pages beyond RecipeView + RecipeEditor → opportunistic later
- AI usage widget chart-over-time / per-model breakdown → single rolling-30d card only in v1.3

</domain>

<decisions>
## Implementation Decisions

### Smart pantry-match algorithm (Area 1 — QOL-01..03)

- **D-44 (Area 1, scoring formula):** Pantry-match score is **`(matched / total) − 0.3 * exp(−daysSinceCooked / 7)`** — linear-decay recency penalty, NOT a hard cutoff or step penalty. `daysSinceCooked` reads from `IRecipeMadeService.GetLastCookAsync(recipeId, userId)`; null (never cooked) sets the penalty term to 0. Stable sort by `(score desc, recipeId asc)` per PITFALL H8 to prevent reload-volatility. Rationale: user explicitly picked the smooth-decay variant over the bounded 7-day cutoff; matches REQUIREMENTS QOL-01 "recipes cooked in the last 7 days score lower than fresher candidates" with a continuous-not-cliff curve.
- **D-45 (Area 1, dietary filter):** Dietary filter (QOL-02) is **AND-combined** — a recipe survives only if BOTH (a) its `RecipeTag` rows include all of the user's `UserProfile.DietaryPreferences` (positive tag-match, case-insensitive per Phase 8 D-34's join convention) AND (b) none of its `RecipeIngredient.Ingredient.Category` rows match a diet→excluded-category map (negative ingredient-category exclude). Both checks run BEFORE scoring, so the score is computed only over the survived set. Rationale: user picked the strictest variant; minimizes false positives at the cost of needing both tags AND ingredient-category data — accepted as quality bar for v1.3.
- **D-46 (Area 1, configurability — explicit departure from v1.3 "bounded no-knob" precedent):** Pantry-match weights live in **`appsettings.json` under `CookBot:PantryMatch`**, bound via `IOptions<PantryMatchOptions>` (DTO class in `CookBot.Application/DTOs/`). Default shape:
  ```json
  "CookBot": {
    "PantryMatch": {
      "RecencyPenaltyWeight": 0.3,
      "RecencyHalfLifeDays": 7,
      "MinCoverageRatio": 0.6,
      "ResultCount": 3
    }
  }
  ```
  README PROD-19 "Configuration" section (Phase 9) gets an addendum for this block. Rationale: user explicitly chose configurable over hardcoded — STATE.md Open-question prediction confirmed. This is the ONE place in v1.3 where the user opens a tuning surface; everything else (Phase 9 D-41 365-day cleanup, accent picker, pantry-match formula was Phase 9's bounded-no-knob default) stayed knobless.
- **D-47 (Claude's discretion, diet→category map):** Diet→excluded-`IngredientCategory` map is a **hardcoded static `Dictionary<string, IngredientCategory[]>` in `PantryMatchService`** keyed by lowercase diet label — e.g. `["vegan"] = [Meat, Poultry, Fish, Seafood, Dairy, Eggs]`, `["vegetarian"] = [Meat, Poultry, Fish, Seafood]`, `["dairy-free"] = [Dairy]`, `["gluten-free"] = [Grains]` (planner curates against the existing `IngredientCategory` enum + `seeds/ingredients.json`). NOT in appsettings — this is curated domain knowledge, not a tuning knob. Unknown diet labels skip the negative filter (positive tag-match still applies). Phase 10 plan must enumerate the full map in the plan body.

### AI Chat raw-edit dialog (Area 2 — QOL-04)

- **D-48 (Area 2, surface):** `RawRecipeEditorDialog` is a **CbDialog modal**, opened from the existing `AiChat.razor` "Edit anyway" code path that today (line 769) shows a degraded toast. Same CbDialogService pattern Phase 9 D-38 / Phase 7 SaveRecipeDialog migration established. Title "Edit raw AI response"; primary action "Parse and save"; secondary actions "Copy raw to clipboard" + "Close". Rationale: user picked modal over side-panel and over inline-bubble-expansion — consistent UX surface, doesn't disrupt the AiChat layout, leverages existing CbDialog infrastructure.
- **D-49 (Area 2, initial textarea content):** Dialog opens with **pretty-printed JSON** — `_lastStructuredRecipe.RawResponse.ToJsonString(new JsonSerializerOptions { WriteIndented = true })`. The textarea is monospace (Cb mono font), at least 400px tall, with `resize: vertical` so the user can grow it. No length cap — display whatever the RawResponse contains. Rationale: pretty-printed is the only humanly editable form for hand-fixing structured JSON; matches the recipe schema shape the user has been seeing in PromptBuilder.
- **D-50 (Area 2, validation feedback):** **Debounced live validation** — after 500ms keystroke-idle, run `Parser.TryParse(textareaValue, out _, out _)` and display a small inline status: green check + "Valid recipe — ready to save" / red X + "Validation failed: [first error message]". The "Parse and save" button is enabled only when the live check passes. Implementation: Blazor `oninput` + debounced state via `System.Threading.Timer` or `TaskDelay+Token`; no JS interop needed. Rationale: user picked the most responsive option over on-action-only and over syntax-highlight-only.
- **D-51 (Area 2, success flow):** On "Parse and save" success (the textarea text parses), close the RawRecipeEditorDialog and **open the existing SaveRecipeDialog** with the edited text (mirrors the existing parser-success path at AiChat.razor:744-765 — `["RecipeContent"] = editedJson`). Two-dialog hop. NOT save-inline (avoids duplicating SaveRecipeDialog's cookbook-picker UI), NOT auto-persist-to-drafts (no "Drafts" cookbook concept in v1.3). Rationale: user picked SaveRecipeDialog path — preserves Phase 1 invariant "never persist non-conforming recipes" and keeps SaveRecipeDialog as the single canonical persist surface.

### Profile prompt editor + injection warning (Area 3 — QOL-06, QOL-07)

- **D-52 (Area 3, wiring — corrects REQUIREMENTS error):** `PromptBuilderService.BuildSystemPrompt` is rewired with a **null-fallback override**: if `profile.AiSystemPromptTemplate` is non-null and non-whitespace, use it as the template argument to `ResolveTemplate`; otherwise pass `DefaultTemplate`. Implementation: change line 39 to `return ResolveTemplate(string.IsNullOrWhiteSpace(profile.AiSystemPromptTemplate) ? DefaultTemplate : profile.AiSystemPromptTemplate, profile, pantryItems);`. **This corrects REQUIREMENTS QOL-06's "already loaded" claim** — BuildSystemPrompt always used DefaultTemplate pre-Phase-10; the field on UserProfile existed but was dead code. Phase 8's CLEAN-03 Verify prompt-snapshot test re-`verified` once when this lands (Phase 9 D-42's prompt prose change already established the re-verify pattern). Rationale: user picked the smallest-change variant; `ResolveTemplate` already takes a `template` parameter, so the entire wiring is one substitution.
- **D-53 (Area 3, variable insertion affordance):** A **clickable CbChip row** sits directly above the prompt-template `<CbTextarea>`. Each chip is a token (`{{experience_level}}`, `{{unit_system}}`, `{{equipment}}`, `{{dietary_preferences}}`, `{{pantry}}`, `{{recipe_format}}`); clicking inserts the token at the current cursor position in the textarea via small JS interop helper (`window.CookbotPromptEditor.insertAtCursor(elementId, token)` — mirrors the existing `recipe-chip-composer.js` pattern). Read-only tokens display with the standard CbChip styling — no special highlight. Rationale: user picked discoverable over read-only-labels and over auto-complete; matches Cb design language and reuses the chip-insertion JS pattern already established in v1.2 Phase 6.
- **D-54 (Area 3, reset affordance):** "Reset to default" is a **secondary CbButton** adjacent to Save in the prompt-template card, with a **CbDialog confirm step** ("Reset prompt template? Your custom template will be lost.") before clearing. On confirm, the textarea swaps to the DefaultTemplate text (displayed for editing, NOT just placeholder — the user can then re-customize before Save). Save persists; cancelling the page leaves AiSystemPromptTemplate unchanged. Rationale: user picked confirm-protected reset over no-confirm and over no-reset-button — surfaces the irreversibility without making the affordance hidden.
- **D-55 (Area 3, injection warning placement — QOL-07):** Warning copy lives in an **inline `<CbCard>` directly below the textarea** with subtle warning styling (e.g. `var(--accent-soft)` background, small heading "About custom prompts"). Always visible while editing. Copy explains: (a) custom templates ARE injected verbatim into the system prompt, (b) `PromptInjectionGuard` wraps user-supplied content but NOT the system-prompt template itself, (c) avoid instructions that attempt to override safety (i.e. any wording that tells the model to disregard or supersede the rest of the system prompt). Rationale: user picked inline-always-visible over click-to-expand and over first-edit dialog — read-once-and-internalize, no friction; matches Phase 9 D-42's "prose nudge" philosophy.

### TopBar slot mechanism (Area 4 — POLISH-04)

- **D-56 (Area 4, plumbing — ROADMAP-literal compliance):** TopBar.RightSlot is fed by a new **`ICbTopBarService` scoped service** in `src/CookBot.Web/Services/`. Shape:
  ```csharp
  public interface ICbTopBarService
  {
      RenderFragment? RightSlot { get; }
      event Action? OnChanged;
      void SetRightSlot(RenderFragment? content);
      void Clear();
  }
  ```
  `MainLayout.razor` injects `ICbTopBarService`, subscribes to `OnChanged`, and passes `service.RightSlot` to `<TopBar RightSlot="@TopBarService.RightSlot" ... />` (TopBar's existing `[Parameter] RenderFragment? RightSlot` at line 80 is unchanged). Pages call `TopBarService.SetRightSlot(builder => { ... })` in `OnInitializedAsync`. Rationale: user picked ICbTopBarService over CascadingValue/CascadingParameter — honors ROADMAP success criteria 4's literal text; event-driven update mechanism is future-proof for multi-slot expansion (LeftSlot, CenterSlot) without refactor.
- **D-57 (Area 4, lifecycle — auto-clear on navigation):** `ICbTopBarService` registers on `NavigationManager.LocationChanged` in its constructor and **auto-clears RightSlot on every location change**. Pages that want TopBar content must re-set it in `OnInitializedAsync` (or `OnParametersSet` for parameterized pages). Pages with nothing to inject simply don't call SetRightSlot. Rationale: user picked auto-clear over "page must clear" and over sticky-until-replaced — predictable, no boilerplate per page, no stale-fragment surprises. Matches Blazor lifecycle expectations.
- **D-58 (Area 4, adoption scope):** Phase 10 migrates **two pages** to RightSlot: `RecipeView.razor` (RV-05 actions per the explicit POLISH-04 requirement) AND `RecipeEditor.razor` (Save/Cancel/Delete actions). Other pages stay on their current inline-action layouts and migrate opportunistically in later milestones. Rationale: user picked "RecipeView + RecipeEditor" over RecipeView-only and over all-Phase-10-surfaces — exercises the mechanism in two real cases (one read surface, one write surface), gives a second test of the scoped-service pattern, but doesn't balloon Phase 10 into a layout refactor.
- **D-59 (Area 4, responsive collapse):** RightSlot is **hidden below 720px viewport width** via a CSS media-query on the TopBar `RightSlot` container; each migrating page keeps an inline fallback (RecipeView keeps its existing inline-above-hero PRAGMATIC fallback; RecipeEditor keeps a footer action row). The page is responsible for rendering its own narrow-viewport actions. Rationale: user picked hide-on-narrow over always-visible-scrolls and over collapse-to-overflow-menu — simplest mobile path, no new overflow-menu component, each page handles its own narrow layout.

### Claude's Discretion

These were not gray areas the user weighed in on; the planner can make the calls during planning.

- **`IPantryMatchService` location & lifetime** — interface + implementation in `src/CookBot.Application/Services/`; Scoped lifetime (depends on `CookBotDbContext` indirectly via repositories). Replaces `Home.razor.cs:297-339 BuildPantryMatchesAsync` deterministic stub. Home.razor.cs injects the service.
- **Per-cookbook reparenting UI (POLISH-01)** — RecipeEditor gains a "Move to cookbook…" affordance. Recommended placement: a small `CbSelect` in the editor header (above the photo composite, alongside the cookbook badge), or alternately a "Move to…" menu item in a kebab dropdown. Save calls `RecipeService.UpdateAsync(recipeId, ..., cookbookId)` which Phase 10 extends with the cookbookId param + `db.UserCanAccessCookbookAsync` ownership check. Toast on success; redirect to the destination cookbook's RecipeView if the cookbook changed (page-level handling).
- **Pantry quick-add target list (POLISH-02 — DESIGN GAP)** — `GroceryListService` has no `AddItem(Async)` method today; only `GenerateFromRecipeAsync` / `GenerateAllFromRecipeAsync`. There is also no concept of a "primary grocery list" — users have many. Planner SHOULD: (a) add `Task<GroceryList> EnsurePrimaryListAsync(int userId)` to `GroceryListService` that returns the most-recently-created open list for the user OR creates a new list named "Pantry quick-add" if none exists; (b) add `Task AddItemAsync(int groceryListId, int ingredientId, decimal amount = 0, string unit = "")` that appends a `GroceryListItem` with `IsCompleted = false`; (c) wire PantryView.razor:354-360 cart button to call both. Toast "Added {ingredient} to grocery list" on success. The disabled-affordance styling at line 358 must be replaced with active styling.
- **Moon glyph weight (POLISH-03)** — Moon icon goes into `Icon.razor` at line 49-area as `public const string Moon = "moon";` + path at line 89-area. Stroke-1 outline crescent matching Sun's weight (line 89: `<circle cx="12" cy="12" r="3.5"/>` + 8 rays). Recommended path: `<path d="M21 12.79A9 9 0 1 1 11.21 3 7 7 0 0 0 21 12.79z"/>` — classic crescent at default orientation, matches the Sun's visual weight. The TopBar dark-mode toggle at MainLayout.razor:30-area swaps `Icon.Names.Sun` ↔ `Icon.Names.Moon` based on `_isDarkMode`.
- **Live timer tick (POLISH-05)** — `cooking-session-state.js` adds a `startTickLoop()` function that runs `setInterval(updateTick, 1000)` writing the formatted MM:SS (or HH:MM:SS) string directly into the Home active-timer DOM band by `document.getElementById('home-active-timer-{guid}')` — bypasses SignalR roundtrips entirely (cheap; no Blazor render on every tick). Teardown on `pagehide` (matches Phase 9 D-43 surfaceable preference). Home.razor.cs:464 already generates the per-render guid; `OnAfterRenderAsync` calls `JSRuntime.InvokeVoidAsync("CookbotSession.startTickLoop", elementId, startedAtIso, durationSeconds)` after first render. CookingMode does NOT need a parallel tick — `cooking-timers.js` already runs its own per-step interval.
- **Accent picker UI (QOL-05)** — Profile page (EditProfile.razor) gains a "Accent color" card with three CbRadio options: "Default" (the warm-orange default), "Terracotta", "Sage" — in that order (most-conservative first). Selection persists to `localStorage.setItem("cookbot_accent", value)` and sets `data-accent` on `<html>` before first paint via a small JS bootstrap in cookbot-shell.js (matches the existing `cookbot_dark_mode` pattern). No EF migration; no `UserProfile` column. Selection takes effect immediately on click (no Save button — like the existing dark-mode toggle).
- **AI usage widget (PROD-17 Phase 10 read surface)** — EditProfile.razor gains an "AI usage" card showing rolling 30-day input tokens + output tokens + estimated cost USD, sourced from `AiUsageLog` filtered by `KeyOwnerId == currentUserId AND IsRetryAttempt == false AND Timestamp >= UtcNow.AddDays(-30)`. Single card (no chart, no per-model breakdown), with a footnote "Pricing as of {AiPricingVerifiedDate}" per Phase 9 PITFALL H10. Empty state: "No AI activity in the last 30 days." Cross-user disclosure note: small text "Includes spending by users sharing your key" per Phase 9 README PROD-18 disclosure.
- **`PantryMatchOptions` DTO location** — `src/CookBot.Application/DTOs/PantryMatchOptions.cs`, bound via `services.Configure<PantryMatchOptions>(configuration.GetSection("CookBot:PantryMatch"))` in `AddApplication`. Default values match D-46's appsettings block; if the section is missing the defaults apply (Phase 9 PROD-19 env-var override pattern carries forward).
- **Stable sort key construction** — When two recipes tie on score, secondary sort by `recipeId asc` (deterministic). Tertiary sort by `Recipe.Name` ascending (defensive against duplicate scores from O(0.5²) coverage values that exactly cancel decay terms).
- **`AddPantryMatchIndexes` migration verification** — Phase 8 D-31 #4 already shipped composite indexes on `RecipeIngredient(RecipeId, IngredientId)` + `PantryItem(UserId, IngredientId)`. Plan SHOULD include one EF-snapshot test that asserts these indexes still exist in the model snapshot at Phase 10 — guards against accidental removal during Phase 10 work.
- **Plan / wave structure** — 12 reqs across two natural workstreams. Suggested split (planner's call): Wave 1 = `IPantryMatchService` + PantryMatchOptions + diet→category map + Home.razor.cs swap + unit tests (QOL-01..03 ≈ 5 plans of work); Wave 2 = `RawRecipeEditorDialog` + AiChat wiring (QOL-04); Wave 3 = Profile prompt editor + chip insertion JS + reset confirm + injection warning + BuildSystemPrompt rewire (QOL-06..07); Wave 4 = `ICbTopBarService` + MainLayout/TopBar wiring + RecipeView + RecipeEditor migration (POLISH-04); Wave 5 = polish bundle — cookbook reparenting (POLISH-01) + pantry quick-add (POLISH-02) + moon glyph (POLISH-03) + live tick (POLISH-05) + accent picker (QOL-05) + AI usage widget (PROD-17 read surface). Planner may merge or split.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents (researcher, planner, executor) MUST read these before planning or implementing.**

### Project & Roadmap
- `.planning/PROJECT.md` — project context, validated capabilities, active scope, key decisions, constraints, hard invariants (canonical-first reads, AI-off contract, POLISH-01 no extractor revival, no MudBlazor / Newtonsoft / MEAI / NJsonSchema / Identity middleware)
- `.planning/REQUIREMENTS.md` §"QOL (`QOL-*`)" (QOL-01..07) and §"Small-stuff polish (`POLISH-*`)" (POLISH-01..05) — 12 REQ-IDs Phase 10 owns; each row is spelled out in detail. **NOTE:** QOL-06's "already loaded by PromptBuilderService.BuildSystemPrompt" claim is **incorrect** — D-52 corrects this.
- `.planning/ROADMAP.md` §"Phase 10: QOL, Polish & Consumer Surfaces" — phase goal, success criteria (4), dependency invariants (depends on Phase 8 RecipeTag/composite indexes + Phase 9 AiUsageLog rows)
- `.planning/STATE.md` §"Open questions" — Phase 10 pantry-match weights (resolved by D-44 + D-46 — configurable in appsettings)

### Research (load-bearing)
- `.planning/research/SUMMARY.md` — synthesis routing layer; especially §"Phase 10" pantry-match algorithm decisions, recency-debounce sourcing, dietary-filter shape; §"Critical Pitfalls" #5–10 (Phase 10 risk surface — H7 composite indexes, H8 stable sort, H9 retry double-count, H10 pricing config, M8 telemetry composite index, M9 cross-user disclosure)
- `.planning/research/FEATURES.md` §"Should have (P2)" — smart pantry-match, AI Chat raw-edit hardening, accent picker, Profile widgets all P2 (with FUTURE-13/14/15 reference)
- `.planning/research/ARCHITECTURE.md` §"Phase 10" — `IPantryMatchService` shape, EF query plan with composite indexes, AiUsageLog 30-day aggregation query, TopBar slot service pattern
- `.planning/research/PITFALLS.md` H7 (Home load O(n²) without indexes), H8 (stable sort prevents reload-volatility), H9 (telemetry retry double-count — Phase 9 mitigated structurally, Phase 10 widget queries `IsRetryAttempt = false` only), H10 (pricing-as-of disclosure footnote on widget), M8 (composite index for widget query — already shipped via Phase 9 PROD-14), M9 (cross-user disclosure — widget shows key-owner spend including share recipients per Phase 9 README PROD-18)

### Codebase
- `.planning/codebase/ARCHITECTURE.md` §"AI Chat" + §"Recipe authoring (manual editor)" + §"Cooking Mode" — current page shapes Phase 10 modifies
- `.planning/codebase/CONVENTIONS.md` — `#nullable enable` everywhere; xUnit Theory + MemberData for fixture-driven tests; singleton lifetimes for pure validators; scoped for per-circuit state
- `.planning/codebase/STRUCTURE.md` §"Where to Add New Code" — new application service / new web-layer service / new JS interop patterns

### Phase 8 Reference (load-bearing — Phase 10 depends on Phase 8 infrastructure)
- `.planning/phases/08-format-foundation/08-CONTEXT.md` — D-26 (TagsJson → RecipeTag relational), D-31 (`AddPantryMatchIndexes` composite indexes already shipped in Wave 4 — QOL-03 verifies usage only, does NOT re-add), D-34 (RecipeTag.Name is trim+case-preserve — dietary filter join is case-insensitive)
- `.planning/phases/08-format-foundation/08-PHASE-SUMMARY.md` — final shape of v3 schema landed in Phase 9 (PhotoUrl + Description + per-step Temperature)

### Phase 9 Reference (load-bearing — Phase 10 reads Phase 9 telemetry)
- `.planning/phases/09-photos-prod-ready-infrastructure/09-CONTEXT.md` — D-41 (365-day AiUsageLog cleanup at startup), D-42 (prompt prose nudge precedent — Phase 10 prompt-editor changes do NOT alter DefaultTemplate, just consume profile.AiSystemPromptTemplate as override), D-43 (healthcheck "on-failure" + max_retries surfaceable preference; informs Phase 10's pantry-match knob choice but Phase 10 explicitly departs to "configurable in appsettings" per D-46)

### Source files this phase modifies (start here)
- `src/CookBot.Web/Components/Pages/Home.razor.cs` — replace `BuildPantryMatchesAsync` (lines 297-339) with injection of `IPantryMatchService.GetMatchesAsync(userId, ct)` (QOL-01); active-timer band keeps `_activeTimerCountdownId` (line 464) but `OnAfterRenderAsync` adds `JSRuntime.InvokeVoidAsync("CookbotSession.startTickLoop", ...)` call (POLISH-05)
- `src/CookBot.Web/Components/Pages/AiChat.razor` — `OpenDraftInEditor` D-09 fallback path (lines 769-770) opens `RawRecipeEditorDialog` instead of toast (QOL-04 / D-48..51)
- `src/CookBot.Web/Components/Pages/EditProfile.razor` — add "AI assistant instructions" card with `<CbTextarea>` + variable chip row + reset button + inline warning CbCard (QOL-06 / D-52..55, QOL-07); add "Accent color" CbRadio card (QOL-05); add "AI usage" card reading AiUsageLog rows (PROD-17 Phase 10 read surface — Claude's discretion)
- `src/CookBot.Web/Components/Pages/PantryView.razor` — remove `disabled` from cart button (line 357-360); wire `@onclick` to `GroceryListService.AddItemAsync` on the user's primary list (POLISH-02)
- `src/CookBot.Web/Components/Pages/RecipeView.razor` — RV-05 actions move from inline-above-hero to `ICbTopBarService.SetRightSlot(...)` in `OnInitializedAsync` (POLISH-04); inline fallback kept for < 720px viewport per D-59
- `src/CookBot.Web/Components/Pages/RecipeEditor.razor` — add cookbook reparenting CbSelect (POLISH-01 / Claude's discretion); Save/Cancel actions move to `ICbTopBarService.SetRightSlot(...)` (POLISH-04 / D-58); inline footer-action fallback kept for < 720px
- `src/CookBot.Web/Components/Layout/MainLayout.razor` — inject `ICbTopBarService`, subscribe to `OnChanged`, pass `TopBarService.RightSlot` to `<TopBar RightSlot="@..." />` (D-56); swap Sun ↔ Moon glyph for dark-mode toggle (POLISH-03)
- `src/CookBot.Web/Components/Layout/TopBar.razor` — no API change; existing `[Parameter] RenderFragment? RightSlot` at line 80 is the consumer; CSS media-query at 720px in cookbot-design.css hides the slot container on narrow viewports (D-59)
- `src/CookBot.Web/Components/Atoms/Icon.razor` — add `public const string Moon = "moon";` at line 49 area + crescent path at line 89 area (POLISH-03 / Claude's discretion D-15-like)
- `src/CookBot.Application/Services/PromptBuilderService.cs` — line 39 `BuildSystemPrompt` rewired to null-fallback override on `profile.AiSystemPromptTemplate` (D-52). Phase 8's `PromptSnapshotTests.cs` Verify-based test needs the existing `.verified` regeneration in the SAME commit (third re-verify after Phase 8's initial + Phase 9 D-42 prose addition)
- `src/CookBot.Application/Services/GroceryListService.cs` — add `EnsurePrimaryListAsync(int userId)` + `AddItemAsync(int groceryListId, int ingredientId, decimal amount, string unit)` methods (Claude's discretion D-supplement; design-gap closure)
- `src/CookBot.Application/Services/RecipeService.cs` — `UpdateAsync` accepts new optional `int? newCookbookId` parameter; on non-null, validates via `db.UserCanAccessCookbookAsync(newCookbookId, userId)` before reparent (POLISH-01)
- `src/CookBot.Application/DependencyInjection.cs` — register `IPantryMatchService` Scoped; `services.Configure<PantryMatchOptions>(configuration.GetSection("CookBot:PantryMatch"))` (D-46)
- `src/CookBot.Web/Program.cs` — register `ICbTopBarService` Scoped (D-56)
- `src/CookBot.Web/appsettings.json` — add `CookBot:PantryMatch` block with default values (D-46)
- `src/CookBot.Web/wwwroot/js/cooking-session-state.js` — add `startTickLoop(elementId, startedAtIso, durationSeconds)` + matching `stopTickLoop`; `pagehide` listener tears down (POLISH-05 / Claude's discretion)
- `src/CookBot.Web/wwwroot/js/cookbot-shell.js` — add accent bootstrap (read `localStorage.cookbot_accent`, set `data-accent` on `<html>` before first paint) (QOL-05 / Claude's discretion)
- `src/CookBot.Web/wwwroot/css/cookbot-design.css` — add `@media (max-width: 720px)` rule hiding TopBar RightSlot container (D-59)
- `README.md` — Phase 9's "Configuration" section gets an addendum documenting `CookBot:PantryMatch` block + AI usage widget cross-user disclosure (already covered by Phase 9 PROD-18 PITFALL M9 note; Phase 10 plan SHOULD verify the note is sufficient, not duplicate)

### Source files this phase creates
- `src/CookBot.Application/Services/IPantryMatchService.cs` + `PantryMatchService.cs` — interface + implementation; reads `IRepository<Recipe>` + `IRepository<PantryItem>` + `IRecipeMadeService` + `IOptions<PantryMatchOptions>`; returns `IReadOnlyList<PantryMatchResult>` ordered by score desc, recipeId asc (D-44..47)
- `src/CookBot.Application/DTOs/PantryMatchOptions.cs` — POCO with `RecencyPenaltyWeight`, `RecencyHalfLifeDays`, `MinCoverageRatio`, `ResultCount` properties (defaults baked in) (D-46)
- `src/CookBot.Application/DTOs/PantryMatchResult.cs` — `record PantryMatchResult(int RecipeId, string RecipeName, int MatchedCount, int TotalCount, double Score, string? PhotoUrl, string? FirstMissingIngredientName)` (mirrors HomePantryMatch shape so Home.razor.cs swap is mechanical)
- `src/CookBot.Web/Components/Pages/RawRecipeEditorDialog.razor` — CbDialog modal; debounced live-validation textarea; "Parse and save" + "Copy raw to clipboard" + "Close" actions; opens SaveRecipeDialog on parse-OK (D-48..51)
- `src/CookBot.Web/Services/ICbTopBarService.cs` + `CbTopBarService.cs` — scoped service implementing event-based RightSlot updates; auto-clears on NavigationManager.LocationChanged (D-56, D-57)
- `src/CookBot.Web/wwwroot/js/prompt-editor-insert.js` (or extend an existing JS file) — `window.CookbotPromptEditor.insertAtCursor(elementId, token)` for variable chip row insertion (D-53 / Claude's discretion)
- `tests/CookBot.Tests/Services/PantryMatchServiceTests.cs` — scoring formula verification (matched/total exact; recency-decay term at days 0/1/3/7/30; stable sort on score tie); diet-filter behavior (tag-match positive, ingredient-exclude negative, AND-combined survival)
- `tests/CookBot.Tests/Services/GroceryListServiceTests.cs` — `EnsurePrimaryListAsync` returns existing list / creates "Pantry quick-add" when none / `AddItemAsync` appends correct GroceryListItem
- `tests/CookBot.Tests/Services/PromptBuilderServiceNullFallbackTests.cs` — `BuildSystemPrompt` with profile.AiSystemPromptTemplate = null uses DefaultTemplate; non-null/non-whitespace uses custom template; whitespace-only template falls back to default (D-52)
- `tests/CookBot.Tests/Services/CbTopBarServiceTests.cs` — SetRightSlot raises OnChanged; LocationChanged auto-clears; Clear is idempotent

### Source files this phase deletes
- None this phase. The deterministic `BuildPantryMatchesAsync` stub at Home.razor.cs:297-339 is REPLACED (becomes a thin wrapper or is deleted in favor of direct `IPantryMatchService` injection — planner's call).

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets

- **`IRecipeMadeService`** (`src/CookBot.Web/Services/RecipeMadeService.cs`) — `GetLastCookAsync(recipeId, userId)` + `GetRecentForUserAsync(userId, take)` already exist and are battle-tested from v1.2 Phase 7 slice-09. PantryMatchService's recency-decay term reads directly from `GetLastCookAsync`. Service is Web-layer Scoped — Phase 10's PantryMatchService is Application-layer; the Application service depends on `IRecipeMadeService`, which means either (a) move IRecipeMadeService interface to `CookBot.Application/Services/` (recommended — it's an abstraction, the implementation stays Web-layer) or (b) Phase 10 PantryMatchService becomes Web-layer instead. Planner picks; recommended (a) for cleaner layering.
- **`Home.razor.cs:297-339` BuildPantryMatchesAsync stub** — the entire deterministic-stub method body is the replacement target. `HomePantryMatch` record at line 470 mirrors PantryMatchResult shape — keep HomePantryMatch as a view-layer DTO, project from PantryMatchResult in Home.razor.cs.
- **`AiChat.razor` `_lastStructuredRecipe.RawResponse`** — already preserved canonical-doc-shape JsonNode; RawRecipeEditorDialog consumes directly via `_lastStructuredRecipe.RawResponse.ToJsonString(new JsonSerializerOptions { WriteIndented = true })`. POLISH-01 invariant preserved — no extractor revival.
- **`IRecipeFormatParser.TryParse`** — used by AiChat at line 744 for the existing parser-success path; RawRecipeEditorDialog's debounced live validation uses the same surface. No changes to parser; just an additional consumer.
- **CbDialogService + SaveRecipeDialog** — Phase 7 Plan 07-01 migrated these to CbDialogService; RawRecipeEditorDialog opens via same `CbDialogService.ShowAsync<RawRecipeEditorDialog>` pattern. On parse-OK, raw dialog closes and reopens SaveRecipeDialog with `["RecipeContent"] = editedJson` — mirror of AiChat.razor:756-763.
- **`<CbTextarea>` Cb atom** — already used by EditProfile / RecipeEditor; reused for prompt-template editor + RawRecipeEditorDialog raw JSON textarea.
- **`<CbChip>` Cb atom** — used in editor chip composer; reused for variable-insertion chip row in prompt editor (D-53).
- **`<CbRadio>` Cb atom** — already used by EditProfile for unit-system / experience-level; reused for accent picker (QOL-05).
- **`recipe-chip-composer.js`** — JS pattern reference for `window.CookbotPromptEditor.insertAtCursor` — same `selectionStart` + `setRangeText` approach.
- **`cookbot_dark_mode` localStorage pattern** — direct template for `cookbot_accent` localStorage handling; bootstrap-before-first-paint via cookbot-shell.js (QOL-05).
- **`AiUsageLog` entity + composite index `IX_AiUsageLogs_KeyOwnerId_Timestamp`** — Phase 9 PROD-14 shipped. Phase 10 widget query is a single `db.AiUsageLogs.AsNoTracking().Where(r => r.KeyOwnerId == userId && !r.IsRetryAttempt && r.Timestamp >= cutoff).Sum(...)` — index makes it O(log n).
- **`AddPantryMatchIndexes` composite indexes (Phase 8 D-31 #4)** — `RecipeIngredient(RecipeId, IngredientId)` + `PantryItem(UserId, IngredientId)` already shipped. EF Core picks them up automatically for the pantry-match join.
- **`Icon.razor` outline icon system** — single Razor file with name→path dictionary at line 49+ (constants) and line 89+ (SVG paths). Adding Moon is one line in each section.
- **`TopBar.razor` `[Parameter] RenderFragment? RightSlot`** — line 80 — the existing parameter is what CbTopBarService feeds. No TopBar API change.

### Established Patterns

- **DI registration via per-project extension** — `AddApplication` registers `IPantryMatchService` + `IOptions<PantryMatchOptions>`; `Program.cs` registers Web-layer `ICbTopBarService`.
- **Scoped lifetimes for per-circuit Web services** — `CbTopBarService` is Scoped (one per SignalR circuit); event subscriptions live for the circuit's lifetime.
- **Singleton lifetimes for pure validators** — N/A this phase (PantryMatchService depends on DbContext-scoped repos, so Scoped).
- **xUnit Theory + MemberData for fixture-driven tests** — used for the pantry-match scoring matrix and the diet-filter matrix.
- **`#nullable enable` + implicit usings** — every new file.
- **No new EF migrations** — Phase 10 is code-only. Phase 8 + Phase 9 pre-positioned all the schema Phase 10 needs.
- **JS interop helpers under `wwwroot/js/`** — small `window.<Namespace>.<method>` shapes per the existing `CookbotSession.*` and `cookbot.dialog.*` patterns.
- **localStorage UI prefs without UserProfile column** — v1.2 D-43 (density) + Phase 10 D-supplement (accent) — `localStorage.setItem(key, value)` + bootstrap-before-first-paint via cookbot-shell.js; never a server-persisted column for a pure-UI preference.

### Integration Points

- **`Home.razor.cs`** — injects `IPantryMatchService`; line 232's `BuildPantryMatchesAsync(...)` call becomes `PantryMatchService.GetMatchesAsync(userId, ct)`. Line 464's `_activeTimerCountdownId` gets a new `OnAfterRenderAsync` JS-interop hook to start the live tick (POLISH-05).
- **`AiChat.razor`** — line 769 toast is replaced by `await CbDialogService.ShowAsync<RawRecipeEditorDialog>(...)` invocation; dialog parameters include `["RawJson"] = rawJson` and a callback for the parse-success → SaveRecipeDialog hop.
- **`EditProfile.razor`** — gains three new cards in the existing two-column layout: (a) "Accent color" CbRadio (QOL-05); (b) "AI usage" rolling 30-day card (PROD-17 Phase 10 read surface); (c) "AI assistant instructions" prompt-template editor with chip row + warning CbCard (QOL-06..07).
- **`MainLayout.razor`** — injects `ICbTopBarService`; subscribes to `OnChanged` event in `OnInitializedAsync` (triggers `StateHasChanged`); passes `TopBarService.RightSlot` to TopBar parameter. Dark-mode toggle icon swap (Sun ↔ Moon) lives in the existing dark-mode toggle binding.
- **`RecipeView.razor` + `RecipeEditor.razor`** — both inject `ICbTopBarService`; both call `TopBarService.SetRightSlot(builder => { ... })` in `OnInitializedAsync`. NavigationManager auto-clears on page change. Both keep their existing inline fallbacks for < 720px (D-59).
- **`PantryView.razor`** — line 357-360 cart button gets `disabled` removed and `@onclick="@(() => AddToGroceryList(item))"` added; new method calls `GroceryListService.EnsurePrimaryListAsync` + `AddItemAsync`; toast on success.
- **`PromptBuilderService.BuildSystemPrompt`** — line 39 change is a single statement replacement (`ResolveTemplate(string.IsNullOrWhiteSpace(profile.AiSystemPromptTemplate) ? DefaultTemplate : profile.AiSystemPromptTemplate, ...)`). Phase 8's Verify `.verified` regenerates ONE more time in the same commit (planner notes this in the plan body).
- **`RecipeService.UpdateAsync`** — signature gains optional `int? newCookbookId = null`; when non-null, `db.UserCanAccessCookbookAsync(newCookbookId.Value, userId)` precedes the update; throws `UnauthorizedAccessException` on fail (consistent with line 33 of RecipeMadeService.cs pattern).
- **`GroceryListService`** — two new methods (`EnsurePrimaryListAsync`, `AddItemAsync`); existing methods unchanged. The new methods do NOT take a `recipeId` — they're for ad-hoc pantry-driven adds.
- **`appsettings.json`** — new `CookBot:PantryMatch` section; env-var overrides via Phase 9 PROD-19 pattern (`CookBot__PantryMatch__RecencyPenaltyWeight=0.4` etc.).
- **`cookbot-design.css`** — adds responsive `@media (max-width: 720px)` rule hiding `.topbar-right-slot` (D-59). The 3 accent variants (default + terracotta + sage) are already defined in lines 27-66 from v1.2; no CSS work for QOL-05.

</code_context>

<specifics>
## Specific Ideas

- **Linear-decay scoring** — user picked the smooth-curve recency penalty (`-0.3 * exp(-daysSinceCooked/7)`) over the cliff cutoff. This is a quality signal: the user values gradient over discrete buckets in the pantry-match UX.
- **AND-combined dietary filter** — user picked the strictest variant. Implication: planner must commit to BOTH a curated diet→category map (Claude's discretion D-47) AND ensure RecipeTag rows are reliable. False negatives from missing tags will be more visible than false positives.
- **`appsettings.json` knobs for pantry-match** — explicit, deliberate departure from v1.3's broader "bounded no-knob" pattern (Phase 9 D-41 365-day cleanup, Phase 9 D-43 healthcheck on-failure). Pantry-match is the ONE tuning surface the user wants exposed in v1.3.
- **CbDialog modal + SaveRecipeDialog hop for raw-edit** — user picked the consistent UX path. Phase 1's "never persist non-conforming recipes" invariant is preserved structurally — RawRecipeEditorDialog cannot bypass SaveRecipeDialog's cookbook-picker.
- **Debounced live validation** — user picked the most-responsive variant. Implementation cost (500ms idle timer + state flag) is small relative to UX gain.
- **Null-fallback prompt-template wiring** — user picked the smallest-change variant. Important: this means custom templates can omit `{{recipe_format}}` or other required tokens — the prompt-injection warning copy must explicitly call this out (D-55).
- **Clickable chip row for variable insertion** — user picked discoverable over read-only-labels. Matches Cb design-language and recipe-chip-composer.js precedent.
- **CbDialog confirm for prompt reset** — user picked the safer affordance. Consistent with destructive-action UX in v1.2.
- **Inline warning CbCard, always visible** — user picked read-once-and-internalize over click-to-expand and over first-edit dialog. Matches Phase 9 D-42 prose-nudge precedent.
- **`ICbTopBarService` scoped service** — user picked the ROADMAP-literal compliance path over the simpler CascadingValue. Future-proof; one more DI registration; cleaner page-side API.
- **Auto-clear on navigation** — user picked predictable lifecycle over per-page-boilerplate. No stale-fragment surprises.
- **Two pages adopt slot in Phase 10** — user picked the middle scope (RecipeView + RecipeEditor) — exercises both read and write surface patterns without ballooning Phase 10.
- **Hide RightSlot below 720px** — user picked simplest mobile path. Each page handles its own narrow-viewport actions.

</specifics>

<deferred>
## Deferred Ideas

Surfaced during analysis but not in scope for this phase:

- **AI usage widget chart / per-model breakdown** — Phase 10 ships single rolling-30d card only. Chart over time + per-model breakdown deferred to v1.4+ if the user reports the single number is insufficient.
- **TopBar.LeftSlot symmetry** — Phase 10 only ships RightSlot per POLISH-04; LeftSlot (for page title customization) is a v1.4+ surface if pages need it.
- **TopBar slot adoption in pages beyond RecipeView + RecipeEditor** — AiChat, EditProfile, CookbookList, PantryView, etc. keep their current inline-action layouts; migrate opportunistically in later milestones.
- **Per-user spending caps / billing quotas** — explicit v1.4+ per REQUIREMENTS Out of Scope.
- **Cross-user admin telemetry view** — PITFALL M9 admin surface; v1.4+ with proper privacy disclosure.
- **`CookBotSettings.TelemetryEnabled` killswitch** — v1.4+ if a self-hoster requests opt-out.
- **Case-insensitive tag dedup** — Phase 8 D-34 trim+case-preserve stays for v1.3; revisit v1.4+ if duplicate-casing tags become a real authoring problem.
- **Auto-complete in prompt-template textarea** — user picked clickable chips; auto-complete is v1.4+ if chip discoverability proves insufficient.
- **Pantry-match expiration-aware scoring** — REQUIREMENTS Out of Scope (anti-feature at this scope — users won't maintain expiration dates).
- **Pantry-match scoring weights as per-user override** — v1.3 lives in appsettings.json (host-scoped); per-user override is v1.4+ if requested via Profile.
- **CookingMode parallel live-tick** — `cooking-timers.js` already runs its own per-step interval; no parallel POLISH-05 tick needed in CookingMode.
- **"Drafts" cookbook for failed raw-edit auto-save** — Phase 10 raw-edit explicitly hops to SaveRecipeDialog for cookbook selection; no auto-drafts surface.
- **Pantry quick-add to shared pantry's owner's grocery list** — Phase 10 quick-add resolves to the CURRENT user's primary grocery list (shared-pantry inverse case is v1.4+).
- **Reverse-cookbook reparenting from CookbookDetail / RecipeView** — Phase 10 cookbook reparenting lives in RecipeEditor only; bulk-reparent from cookbook detail is v1.4+.
- **Moon glyph filled variant** — Phase 10 ships matching-weight crescent outline; filled crescent is v1.4+ if requested for visual contrast.

### Reviewed Todos (not folded)

(No pending todos in `.planning/STATE.md` or todo system to evaluate.)

</deferred>

---

*Phase: 10-qol-polish-consumer-surfaces*
*Context gathered: 2026-05-16 (discuss mode — 4 user-selected gray areas, all 4 resolved by user)*
