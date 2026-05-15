# Phase 6: Marquee surfaces — Home, Cooking Mode, Recipe View, Recipe Editor - Context

**Gathered:** 2026-04-27
**Status:** Ready for planning
**Mode:** `--auto` — Claude picked recommended defaults from the design handoff at `.planning/design-handoff/`

<domain>
## Phase Boundary

Migrate the four surfaces that earn the redesign visually — `Home.razor`, `CookingMode.razor`, `RecipeView.razor`, `RecipeEditor.razor` — from MudBlazor to the custom Razor component system shipped in Phase 5 (atoms / shell / dialogs / dialog service). Each surface consumes the canonical `RecipeDocument` from v1.1 Phases 1+2 directly — no projection from `Recipe.IngredientsJson` / `Recipe.StepsJson` / `Recipe.IngredientRefs` legacy columns. The Recipe Editor absorbs v1.1 EDITOR-01..07 (chip composer, step/section toggle, timer suggestion, immutable id reorder, paste-raw-text routing, cooking-mode link-only highlight, keyboard a11y) — built once, in custom Razor, on top of the new shell.

**This phase delivers the marquee user-visible deliverables.** When it ships, Home looks like a kitchen dashboard (not an admin panel), Cooking Mode looks like it belongs on a tablet propped against the backsplash, Recipe View looks editorial, and Recipe Editor's chip composer is keyboard-driven. MudBlazor still loads (deleted in Phase 7 terminal slice). No application surface outside these four is migrated in Phase 6.

**In scope (24 reqs from REQUIREMENTS.md):**
- HOME-01..HOME-04 — greeting + quick actions, pantry-aware hero, glance strip, recently cooked + up next
- COOK-01..COOK-06 — dark cocoa background, top step rail, adaptive timer/step hero, always-on right rail, bottom step nav, JS-interop preservation
- RV-01..RV-05 — editorial title, sticky scaled-ingredients sidebar, hanging accent numerals on method, last-cook notes callout, top-bar share/cook actions
- ED-01..ED-09 — borderless title/subtitle inputs, ingredients table, chip composer (absorbs EDITOR-01), step/section toggle (EDITOR-02), timer suggestion (EDITOR-03), immutable id reorder (EDITOR-04), paste-raw-text routing (EDITOR-05), cooking-mode link-only highlight (EDITOR-06), keyboard a11y (EDITOR-07)

**Not in scope (deferred — do NOT pull forward):**
- CookbookList, CookbookDetail, PantryView, GroceryListView, AiChat, PromptBuilder, EditProfile, dialogs → Phase 7
- MudBlazor package removal (MIG-01..03) → Phase 7 terminal slice
- Per-step temperature field (FEATURE-V2) → FUTURE-V1.1-01 (deferred to v1.3+)
- Smart pantry-match algorithm → FUTURE-13 (HOME-02 ships deterministic stub)
- Photo upload backend → FUTURE (StripedPlaceholder only this phase)

</domain>

<decisions>
## Implementation Decisions

### A. RecipeDocument access pattern

- **D-01:** All four surfaces consume `RecipeDocument` directly. `Recipe.CanonicalDocumentJson` (added by v1.1 MIGRATION-01) is the source of truth — surfaces deserialize it via `JsonSerializer.Deserialize<RecipeDocument>(...)` once on load and bind the resulting object. Legacy columns (`Recipe.Servings`, `Recipe.IngredientsJson`, `Recipe.StepsJson`, `Recipe.IngredientRefs`) are **NEVER read** by these surfaces — Phase 6 SC#3 forbids legacy projections. They remain in the DB as indexed-query backing for `CookbookList.razor` (Phase 7) and grocery generation; that's outside Phase 6 scope.
- **D-02:** Saves go through the existing `RecipeService.SaveRecipeAsync` pattern — which already canonicalizes through `RecipeDocument` round-trip on save (v1.1 Phase 1). The editor's responsibility ends at "produce a valid `RecipeDocument`"; the service handles the rest.

### B. Home dashboard (HOME-01..04)

- **D-03:** "Tonight from your pantry" hero (HOME-02) ships a **deterministic stub matcher** in `Components/Pages/Home.razor`'s code block. Algorithm: enumerate user's recipes, for each compute `ingredientMatch = recipeIngredients.Count(i => pantryHasIngredient(i.IngredientId)) / recipeIngredients.Count`, sort descending, pick top 3 with `match >= 0.6`. Empty-state CTA when `match < 0.6` for everything OR pantry is empty: "Add to your pantry to see what you can cook tonight" with a button linking to `/pantry`. The smart matcher (expiration-aware, %-of-pantry-used, dietary-filtered) is **FUTURE-13** — leave a `// TODO: smart matching` comment at the algorithm boundary.
- **D-04:** Glance strip (HOME-03) reads counts from the DB directly via existing services. Delta sub-text:
  - Recipes — "+N this week" computed from `RecipeMade.MadeAt > DateTime.UtcNow.AddDays(-7)` count, OR `+N` count of `Recipe.CreatedAt > AddDays(-7)` if MadeAt isn't tracked. PRAGMATIC: just show `Recipes: {total}` with a static sub like "in your collection" if the delta is hard to compute cheaply.
  - Cookbooks — "{N} shared with the house" from `CookbookShare` count; "private only" if zero
  - Pantry items — "{N} low · {N} expiring" from pantry status
  - Grocery — "list updated {ago}" from latest `GroceryList.UpdatedAt`
- **D-05:** "Recently cooked" (HOME-04) — query `RecipeMade` joined to `Recipe`, ordered by `MadeAt DESC`, take 4. Each tile uses `<StripedPlaceholder>` for the photo (no real photos until photo-upload backend ships); shows recipe name + day-of-week + frequency-this-month.
- **D-06:** "Up next" (HOME-04) — query top 3 starred recipes (if a star/favorite concept exists) OR fall back to last 3 `RecipeMade` entries that haven't been cooked in the last 14 days. PRAGMATIC: if no queue concept exists, ship 3 placeholder rows with a `// TODO: replace with starred queue` comment. Either is acceptable; the design needs the visual slot filled with realistic data.

### C. Cooking Mode (COOK-01..06)

- **D-07:** Background flips to `--ink` (cocoa). Existing `CookingMode.razor` keeps all its non-presentational logic — JS-interop timers in `wwwroot/js/cooking-timers.js`, browser notifications when timers fire, `RecipeCookingAiContext` (v1.1 Phase 2) for "Ask about this step", step navigation, recipe-scaling state. ONLY the Razor markup is replaced (MudPaper → custom div, MudButton → CbButton, MudIcon → Icon, MudChip → CbChip).
- **D-08:** Adaptive hero (COOK-03) — when `_currentTimerRunning` (existing field), render the 224px tabular-numeral countdown (`<div class="num" style="font-size:224px;letter-spacing:-0.05em;">@_timerDisplay</div>`) + Pause / +30s / Reset controls; when not running, render the 52px step text + "Start N-min timer" + "Ask about this step" buttons. The handoff's `tweaks-panel.jsx` had `cookHero` toggle (timer/step/ingredients/adaptive); production ships `adaptive` only — no mode picker.
- **D-09:** Right rail (COOK-04) — "Ingredients · scaled {scale}×" eyebrow; ingredients are projected from `RecipeDocument.Ingredients`; current step's referenced ingredients (from `RecipeDocument.Steps[i].IngredientLinks` — the canonical `[name](#id)` link list) render in an accent-tint card; non-referenced ingredients dimmed. Bottom of rail: "Serves {N}" with −/+ scale buttons. **Servings-only scaling** (v1.1 D-Q9 invariant): only `RecipeIngredient.Amount` scales; oven temperatures, prep/cook times, descriptive text never auto-scale.
- **D-10:** Bottom step nav (COOK-05) — "Previous: {prev step name}" (1fr) + "Next: {next step name}" accent button (2fr); 64px height; left/right arrow keys also navigate steps (existing keyboard handler if present, else add). Existing `_currentStepIndex` / `MoveToStep(int)` API preserved.
- **D-11:** Browser notifications (COOK-06) — existing `cooking-timers.js` + `Notification.requestPermission()` flow preserved verbatim. Top bar's notification chip ("notifications on" / "off") reads the existing permission state via JS interop.
- **D-12:** "Ask about this step" — existing wiring through `RecipeCookingAiContext` preserved. Hidden when `UserProfile.AiEnabled = false` — the button just returns null in the render path (matching the AI-off contract).

### D. Recipe View (RV-01..05)

- **D-13:** Editorial layout (RV-01) — eyebrow tags + 64px display title using `.cb-recipe-cap` class (already in cookbot-design.css from Phase 5 plan 01) + 17px lead paragraph + 4-stat row (Active / Total / Serves / Made-count). Stats consume `RecipeDocument.PrepTimeMinutes`, `RecipeDocument.CookTimeMinutes` (sum for Total), `RecipeDocument.Servings`, `await RecipeService.GetMadeCountAsync(recipeId)`.
- **D-14:** Two-column body (RV-02) — sticky 300px ingredient sidebar with scale control card (CbCard wrapping a Servings widget); ingredient rows from `RecipeDocument.Ingredients`. Tag chips render `RecipeDocument.Tags` (or empty if absent).
- **D-15:** Method (RV-03) — steps from `RecipeDocument.Steps`. Each step renders with hanging accent-colored numeral (`<div class="num" style="font-size:28px;color:var(--accent);">@($"0{i+1}")</div>`) + step title (from `step.Heading` if set, or step type marker if section/etc., or empty for content steps) + 15px body paragraph. If `step.TimerMinutes` set → inline `<CbChip Variant="Timer" Icon="clock"> {step.TimerMinutes} min</CbChip>`.
- **D-16:** "Notes from your last cook" callout (RV-04) — query latest `RecipeMade.Notes` (not null/empty) for this recipe; show in cream-2 bg card with eyebrow + quote + date. Hidden when none.
- **D-17:** Top-bar actions (RV-05) — Share ghost button → existing share dialog (still uses `IDialogService`/MudBlazor — Phase 7 migrates dialog content); Cook this accent button → `Navigation.NavigateTo($"/cook/{recipeId}")`. The TopBar component supports a right-slot via cascading parameter; Recipe View provides actions via that mechanism.

### E. Recipe Editor (ED-01..09 — absorbing v1.1 EDITOR-01..07)

- **D-18:** Borderless title/subtitle inputs (ED-01) — `<input>` with `border:0;outline:none;background:transparent;font-family:inherit;` styled via inline styles per design handoff `recipe-editor.jsx`. Two-way bind to `RecipeDocument.Title` / `RecipeDocument.Description`.
- **D-19:** Ingredients table (ED-02) — grid (qty 60px / unit 70px / name 1fr / actions 28px) inside a CbCard. Each row is bound to a `RecipeIngredient` from `RecipeDocument.Ingredients`. "Add ingredient" footer button. Tab/Backspace keyboard semantics: Tab advances field-by-field within row, then to next row's qty; Backspace on empty row deletes the row.
- **D-20:** Step composer (ED-03 — absorbs EDITOR-01) — built fresh in custom Razor. Each step renders as a CbCard with step-number circle + chip-aware text editor + actions. The chip-aware editor is a `contenteditable=true` div with JS interop providing:
  - `@`-trigger autocomplete: typing `@` opens a dropdown of `RecipeDocument.Ingredients` (filtered by typed text); selecting one inserts an `<IngredientChip>` element with `data-id="@ingredient.Id"`. Visual chip; underlying serialized text is `[name](#id)` markdown
  - `/timer` or numeric+min detection: triggers EDITOR-03 timer suggestion (D-22)
  - On save, JS interop converts the contenteditable HTML (with embedded chip elements) back to canonical `[name](#id)` markdown text for serialization
  - Reuses `wwwroot/js/recipe-chip-composer.js` (created earlier in v1.1 Phase 3 plans 03-05/06/07 — check if it exists; if so, port it; if not, write it fresh per the design intent in `.planning/phases/03-editor-ux-without-special-syntax/03-01-PLAN.md`)
- **D-21:** Step/Section toggle (ED-04 — absorbs EDITOR-02) — explicit `<CbToggle>` or `<CbRadio>` (Step | Section header) above each step's text. Section steps disable timer + ingredient-chip controls; render as a heading-style block (no chip composer, just a CbInput for the heading text). Maps to `RecipeDocument.Steps[i]` discriminated union (`SectionStep` vs `ContentStep`).
- **D-22:** Timer suggestion (ED-05 — absorbs EDITOR-03) — when typing detects "25 min" / "two hours" / etc. patterns, shows a non-modal banner above the step: "Detected 25 min — convert to a timer? [Yes / No]". Yes inserts a `<TimerChip>` (similar to IngredientChip) and sets `step.TimerMinutes`; No dismisses. **Saving never auto-rewrites step text** — explicit chips are the only persisted source of timers.
- **D-23:** Reorder preserves immutable `id` (ED-06 — absorbs EDITOR-04) — drag-handle implementation reuses existing reorder logic if present (check `RecipeEditor.razor` for current implementation); the `RecipeIngredient.Id` is never mutated by reorder.
- **D-24:** Paste-raw-text dialog (ED-07 — absorbs EDITOR-05) — existing `PasteRawTextDialog.razor` (a MudBlazor dialog) gets wrapped in `<CbDialog>` shell or migrated. PRAGMATIC: keep the existing dialog as-is for Phase 6 (it still works via MudBlazor coexistence); Phase 7's dialog migration handles it. The editor button that opens it just calls the existing `IDialogService.ShowAsync<PasteRawTextDialog>(...)` — no behavior change.
- **D-25:** Cooking-mode highlight (ED-08 — absorbs EDITOR-06) — `CookingMode.razor` highlights ingredients in the right rail by parsing `step.IngredientLinks` (the canonical `[name](#id)` link list extracted by `IRecipeFormatParser` during canonical save). NO substring matching. NO reads of dead `RecipeStep.IngredientRefs` field.
- **D-26:** Keyboard a11y (ED-09 — absorbs EDITOR-07) — chip composer is keyboard-navigable: Tab/Shift+Tab between chips, Backspace deletes prior chip when caret is immediately after it, Arrow keys move caret. Axe-core or equivalent smoke pass clean. Degrades gracefully if JS interop fails (recipe still saves with current `[name](#id)` text — the contenteditable simply renders the markdown as plain text without chip widgets).

### F. Migration strategy (per-surface)

- **D-27:** Each surface migrates as one atomic plan: read existing Razor → rewrite the entire markup against the new atom system → preserve all `@code` block logic → migrate dialogs/snackbars to `CbDialogService`/`CbToastService` only when straightforward, otherwise leave existing `IDialogService` calls (MudBlazor coexistence per D-30 from Phase 5). The surface's NON-MARKUP behavior is preserved verbatim. Phase 6 success criteria forbid behavior regressions.
- **D-28:** Existing tests (xUnit) verify service-layer behavior; bUnit tests for these surfaces are NOT added in Phase 6 unless trivially helpful. The verification gate is `dotnet build` clean + `dotnet test` baseline preserved + manual smoke pass on each surface in light + dark mode.

### G. Plan shape

- **D-29:** Recommended plan split (4 plans, one per surface):
  - Plan 06-01: Home dashboard rewrite (HOME-01..04 + pantry-match stub algorithm)
  - Plan 06-02: Cooking Mode rewrite (COOK-01..06 + JS interop preservation + adaptive hero)
  - Plan 06-03: Recipe View rewrite (RV-01..05 + canonical RecipeDocument consumption + last-cook notes)
  - Plan 06-04: Recipe Editor rewrite (ED-01..09 — absorbs v1.1 EDITOR-01..07; chip composer + step/section toggle + timer suggestion + paste-raw-text dialog reuse + keyboard a11y)
- **D-30:** Plans 06-01..06-04 are parallel-safe (each touches a different `Components/Pages/*.razor`) but the executor serializes them by default per the v1.2 D7 invariant. No shared file in Phase 6 (the design tokens and atoms are stable from Phase 5).

</decisions>

<canonical_refs>
## Canonical Refs (MANDATORY for downstream agents)

- `.planning/design-handoff/project/screens/home.jsx` — Home dashboard intent (greeting, hero card, glance strip, recently cooked, up next)
- `.planning/design-handoff/project/screens/cooking.jsx` — Cooking Mode adaptive hero, right rail, step rail, bottom nav
- `.planning/design-handoff/project/screens/recipe-view.jsx` — Editorial Recipe View, sticky sidebar, hanging numerals, notes callout
- `.planning/design-handoff/project/screens/recipe-editor.jsx` — Recipe Editor chip composer, ingredients grid, right meta rail
- `.planning/design-handoff/project/styles.css` — design tokens (already ported to `wwwroot/css/cookbot-design.css` in Phase 5)
- `.planning/REQUIREMENTS.md` — HOME-01..04, COOK-01..06, RV-01..05, ED-01..09 in Phase 6 scope
- `.planning/ROADMAP.md` — Phase 6 success criteria SC#1..SC#5
- `.planning/phases/05-foundation-design-tokens-atoms-shell-dialogs/05-PHASE-SUMMARY.md` — Phase 5 deliverables (atoms, shell, dialog primitives)
- `.planning/phases/03-editor-ux-without-special-syntax/03-01-PLAN.md` through `03-08-PLAN.md` — v1.1 Phase 3 chip composer design intent (NOT executed under v1.1; absorbed into Phase 6 ED-03..ED-09)
- `src/CookBot.Web/Components/Pages/Home.razor` — current Home (about to be rewritten)
- `src/CookBot.Web/Components/Pages/CookingMode.razor` — current Cooking Mode (rewrite markup; preserve JS-interop wiring)
- `src/CookBot.Web/Components/Pages/RecipeView.razor` — current Recipe View
- `src/CookBot.Web/Components/Pages/RecipeEditor.razor` — current Recipe Editor (rewrite markup; integrate chip composer per v1.1 Phase 3 design intent)
- `src/CookBot.Web/Components/Pages/RecipeEditorParts/` — existing IngredientChip / TimerChip / RecipeChipComposer / RecipeStepEditor (created during v1.1 Phase 3 partial work — check what's already in place)
- `src/CookBot.Web/wwwroot/js/cooking-timers.js` — existing JS interop, preserve
- `src/CookBot.Web/wwwroot/js/recipe-chip-composer.js` — chip composer JS interop (may exist from v1.1 Phase 3 partial; verify)
- `src/CookBot.Application/Services/RecipeFormatParser.cs` — `IRecipeFormatParser` consumed by paste-raw-text and chip composer save path
- `src/CookBot.Domain/Recipes/RecipeDocument.cs` — canonical schema (v1.1 Phase 1)
- `src/CookBot.Application/Services/RecipeCookingAiContext.cs` — "Ask about this step" wiring (v1.1 Phase 2)
- `src/CookBot.Web/Services/CurrentUserService.cs` — used across all surfaces
- `src/CookBot.Web/Services/CbDialogService.cs` + `CbToastService.cs` — Phase 5 deliverables (replace MudBlazor `IDialogService` and `ISnackbar` where practical)

</canonical_refs>

<code_context>
## Existing Code Insights

- **`Home.razor`** (current ~150 lines) is mostly a 4-stat MudGrid + 3 quick-action MudButtons. The redesign demotes the stat tiles and leads with the pantry-aware hero — ~80% of the rewrite is new content; keep only the user-greeting and quick-action behavior.
- **`CookingMode.razor`** (current ~600 lines) has substantial logic — timer state, step navigation, JS interop, AI assist. Markup rewrite touches every render block but `@code` is largely preserved. Trickiest part: the adaptive hero condition.
- **`RecipeView.razor`** (current ~250 lines) — straightforward port; the editorial layout is mostly markup. Method-step rendering needs the canonical RecipeDocument access pattern.
- **`RecipeEditor.razor`** (current ~700 lines + 6 RecipeEditorParts components) — biggest rewrite. v1.1 Phase 3 partially built `RecipeEditorParts/IngredientChip.razor`, `TimerChip.razor`, `RecipeChipComposer.razor`, `RecipeStepEditor.razor`, `InlineTimerSuggestion.razor`, `SectionDropConfirmationDialog.razor` — these are MudBlazor-based starting points; rewrite each against custom Razor + `<CbCard>`/`<CbChip>`/`<CbToggle>` per the design.
- **`cooking-timers.js`** is a small, focused module — ~50 lines. Preserve.
- **`recipe-chip-composer.js`** — check if exists. Per v1.1 plan 03-01, this was supposed to be created. If it exists, port verbatim; if not, write per `.planning/phases/03-editor-ux-without-special-syntax/03-01-PLAN.md` design.
- **Existing `RecipeService.SaveRecipeAsync`** already canonicalizes through `RecipeDocument` round-trip; the editor's only obligation is to produce a valid `RecipeDocument`.

</code_context>

<specifics>
## Specific Ideas

- For the Home pantry-match stub, the simplest implementation lives in a new `Components/Pages/Home.razor.cs` (code-behind) with a `BuildPantryMatches()` method called from `OnInitializedAsync`.
- The Recipe View "Notes from your last cook" callout is hidden when `RecipeMade.Notes` is null/empty — don't render an empty card.
- The Cooking Mode tablet target is 1024×720 (per design canvas's `<Tablet>` frame); the layout works at desktop sizes too. No responsive breakpoint adjustments in Phase 6.
- The Recipe Editor's chip composer must preserve graceful degradation: if `recipe-chip-composer.js` interop fails to load, the contenteditable falls back to plain text and the recipe still saves (the underlying `[name](#id)` markdown is just rendered visibly). v1.1 EDITOR-07 verification.

</specifics>

<deferred>
## Deferred Ideas

- Photo upload backend → FUTURE (StripedPlaceholder is Phase 6's photo answer)
- Smart pantry-match algorithm → FUTURE-13 (HOME-02 ships deterministic stub)
- bUnit tests for migrated surfaces → defer per D-28 unless trivially helpful
- Rich-media support in Recipe View (videos, embedded comparisons, before/after photos) → FUTURE
- Recipe View "edit inline" affordance (click a paragraph to edit in place) → FUTURE; current pattern is "Cook this" / "Edit" via separate route navigations
- Cooking Mode side-by-side recipes ("compare two recipes") → FUTURE
- Recipe Editor's AI Suggestions card on the right rail (per design `recipe-editor.jsx`) — INCLUDED in Phase 6 ED-01 right rail; hidden when AI off

</deferred>
