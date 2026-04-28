---
gsd_state_version: 1.0
milestone: v1.2
milestone_name: UI Redesign
status: milestone_complete
stopped_at: Plan 07-07 shipped + manual-smoke fix sweep landed (12/12 issues resolved across 6 atomic commits b2df97b…bfe73fe). v1.2 milestone complete; ready for /gsd-audit-milestone v1.2 + /gsd-complete-milestone v1.2.
paused_at: 2026-04-27 (mid-Phase 3 execution; Phase 3 had 8 plans planned, 0 complete).
last_updated: "2026-04-27T23:30:00.000Z"
last_activity: "2026-04-27 — v1.2 manual-smoke bug-fix sweep. Six atomic commits fixed all 12 issues that surfaced during smoke pass: dark-mode button contrast + cooking-mode fixed-dark + cook contrast bumps (b2df97b); hamburger drawer toggle + logo home link + user-switcher reload (5258dd5); recipe view edit button + ingredient row layout + scale label (98fbf5a); RecipeEditor drag-drop event prevention (d1755e3); cooking mode pause/resume + AI assist visible from timer hero (55c4b15); AI generate-recipe error surfacing with actionable copy + canvas warn chip (bfe73fe). Hard invariants preserved end-to-end: dotnet build 0/0, dotnet test --filter 'Category!=RequiresApiKey' 196/196, repo-wide grep -rn 'Mud[A-Z]' src/ tests/ zero hits, dark-mode toggle wiring untouched, canonical RecipeDocument round-trip preserved, AI-off contract preserved, SecretRedactor invariant preserved (raw-error fallback consumes already-redacted strings). Slice summary at .planning/phases/07-remaining-surfaces-accessibility-mudblazor-strip/07-08-SMOKE-FIXES-SUMMARY.md."
progress:
  total_phases: 7
  completed_phases: 5
  total_plans: 34
  completed_plans: 34
  percent: 100
---

# Project State

## Project Reference

See: .planning/PROJECT.md (updated 2026-04-27)

**Core value:** A durable home for the recipes the user actually cooks, captured in one standardized format that round-trips cleanly between AI generation, manual editing, cooking mode, and import/export — without the user (or the AI) having to know special syntax.
**Current focus:** Milestone v1.2 — UI Redesign (replace MudBlazor; rebuild against `.planning/design-handoff/`).

## Current Position

Phase: **7 — Remaining surfaces + accessibility + MudBlazor strip** (7/7 plans shipped — Phase 7 COMPLETE)
Plan: 07-07 (Terminal MudBlazor strip + sandbox cleanup) complete — MIG-01..03 satisfied
Status: **v1.2 milestone COMPLETE.** All 3 phases (5/6/7) shipped, 16/16 plans, 75/75 requirements. Ready for `/gsd-audit-milestone v1.2` followed by `/gsd-complete-milestone v1.2`.
Last activity: 2026-04-27 — Plan 07-07 shipped terminal MudBlazor strip (MIG-01..03). MudBlazor 8.15.0 deleted from CookBot.Web.csproj; AddMudServices() removed from Program.cs; @using MudBlazor removed from _Imports.razor; 4 MudBlazor providers (theme/popover/dialog/snackbar) deleted from MainLayout.razor (MudTheme + PaletteLight/PaletteDark + Typography also gone — _isDarkMode field retained, drives body.dark-mode JS interop); Mud static link/script tags removed from App.razor; DesignSandbox.razor + SampleDialogContent.razor deleted. Pre-existing unmigrated production code handled inline as Rule 3 blockers: RecipeMade.razor full Mud→Cb markup migration + ISnackbar→ICbToastService; CookingMode + RecipeEditor Snackbar.Add → Toast.Show (8 call sites). Three bUnit test files de-Mudded: StepSectionToggleTests dropped AddMudServices()+IPopoverService stub; RecipeChipComposerTests JsInteropFails fallback test renamed MudTextField→CbTextarea; PasteFlowTests dropped MudDialogProvider scaffolding (renders PasteRawTextDialog as CbDialog content directly). Mud documentation comments stripped from 9 production files per Hard Invariant #1. dotnet restore: 0 MudBlazor entries in project.assets.json. dotnet build: 0/0. dotnet test --filter "Category!=RequiresApiKey": 196/196 baseline preserved. Repo-wide grep -rn "Mud[A-Z]" src/ tests/: ZERO hits.

## Paused Milestones

### v1.1 — Canonical Format & AI Conformance

**Paused at:** 2026-04-27 (mid-Phase 3 execution; Phase 3 had 8 plans planned, 0 complete).

| Phase | Status | Disposition for v1.2 |
|-------|--------|----------------------|
| 1. Canonical Format Foundation | ✅ Shipped 2026-04-25 | Carries forward — `RecipeDocument` is foundational |
| 2. AI Structured Output & Conformance | ✅ Shipped 2026-04-26 | Carries forward — orchestrator + repair loop preserved |
| 3. Editor UX Without Special Syntax | 🔁 Absorbed into v1.2 | Chip-composer requirements re-mapped to v1.2 ED-03..ED-09; built in custom Razor instead of MudBlazor |
| 4. Format-Driven New Field & Cleanup | ⏭ Deferred | FEATURE-V2-* + POLISH-03/04/05/07 carry to v1.3+ as FUTURE-V1.1-01..05 |

**Reason for pause:** The Phase 3 chip composer would have been built in MudBlazor and then immediately rewritten in v1.2 once MudBlazor was stripped. Authoring it once in the new component system is the cheaper path.

## Performance Metrics

**Velocity:**

- Total plans completed: 25 (v1.1 Phases 1 + 2 = 9 plans; v1.2 Phase 5 = 5 plans; v1.2 Phase 6 = 4 plans; v1.2 Phase 7 = 7 plans)
- Average duration: Phase 6 ~14.5 min/plan (4 plans, ~58 min total wall-clock); Phase 7 Plan 07-07 ~25 min wall-clock
- Total execution time: —

**By Phase:**

| Milestone | Phase | Plans | Status |
|-----------|-------|-------|--------|
| v1.1 | 1 | 4 | Complete (2026-04-25) |
| v1.1 | 2 | 5 | Complete (2026-04-26) |
| v1.1 | 3 | 8 planned | Absorbed into v1.2 (Plan 06-04 delivered EDITOR-01..07 in custom Razor) |
| v1.1 | 4 | TBD | Deferred to v1.3+ |
| v1.2 | 5 | 5 | Complete (2026-04-27 — all 5 plans shipped) |
| v1.2 | 6 | 4 | Complete (2026-04-27 — all 4 plans shipped: 06-01, 06-02, 06-03, 06-04) |
| v1.2 | 7 | 7 | **Complete (2026-04-27 — all 7 plans shipped: 07-01 Cookbooks + 7 dialogs; 07-02 Pantry + 5 dialogs; 07-03 Grocery + 2 dialogs; 07-04 AI Chat + Prompt Builder; 07-05 Profile + 4 profile/sharing dialogs — last IDialogService consumer removed; 07-06 cross-cutting a11y audit + small fixes; 07-07 terminal MudBlazor strip — MudBlazor deleted, repo-wide Mud[A-Z] grep zero hits, 196/196 tests preserved)** |

## Accumulated Context

### Decisions

Decisions are logged in PROJECT.md Key Decisions table. v1.2-specific decisions:

- v1.2 milestone scope: full visual rebuild from `.planning/design-handoff/` (Claude Design handoff bundle) — 9 surfaces × design system + atoms + shell
- v1.2 / D1: Replace MudBlazor entirely (not skin, not hybrid) — visual fidelity requirement
- v1.2 / D2: Pause v1.1; Phase 3 chip composer absorbed into v1.2 RECIPE-EDITOR; Phase 4 deferred
- v1.2 / D3: Skip milestone research step — design handoff is the research output
- v1.2 / D4: Phase numbering continues from v1.1 (next phase = 5)
- v1.2 / D5: 3-phase shape — (5) Foundation, (6) Marquee surfaces, (7) Remaining surfaces
- v1.2 / D6 *(new at roadmapping)*: MIG-01..03 moved from Phase 5 to terminal slice of Phase 7 — package removal cannot complete until every `Mud*` reference across all 28 pages has been migrated, which by definition requires Phases 6 + 7 surfaces to ship first. Phase 5 builds atoms alongside MudBlazor; Phase 7 deletes MudBlazor.
- v1.2 / D7 *(new at roadmapping)*: Phases 6 and 7 are technically parallel-safe but the executor serializes them by default to avoid converging conflicts on `MainLayout.razor` / `_Imports.razor` / `Program.cs`.
- v1.2 / D8 *(Plan 05-04)*: `CbDialogService.OnRequest` is fire-and-forget invoked from `ShowAsync` so the awaited `Tcs.Task` is never blocked by host `StateHasChanged`; missing-host fail-fast via `InvalidOperationException` rather than hanging.
- v1.2 / D9 *(Plan 05-04)*: `CbDialogHost` uses Blazor's `<DynamicComponent>` for type-erased rendering rather than reflection-based parameter setters — fewer moving parts, fewer attack surfaces.
- v1.2 / D10 *(Plan 05-04)*: `CbToastHost` evicts FIFO when count > 3 AND cancels the evicted toast's `CancellationTokenSource` to avoid leaking timers in long-running toast-heavy circuits.
- v1.2 / D11 *(Plan 05-04)*: `<CbDialogHost />` and `<CbToastHost />` mount on `/design-sandbox` in Plan 05-04 (Plan 05-05 moves them to `MainLayout.razor`) so this plan is independently verifiable without the shell rewrite.
- v1.2 / D12 *(Plan 05-05)*: D-30 coexistence reinterpretation of D-19. MainLayout REMOVES Mud layout chrome (MudLayout/MudAppBar/MudDrawer/MudMainContent/MudContainer) but RETAINS the four MudBlazor providers (MudThemeProvider, MudPopoverProvider, MudDialogProvider, MudSnackbarProvider) so unmigrated pages and dialogs keep working. Phase 7 MIG slice deletes the four providers in the terminal cleanup. Reason: D-19's idealized "remove all four providers" cannot ship in Phase 5 without breaking 32 existing pages and 14+ dialogs.
- v1.2 / D13 *(Plan 05-05)*: Alternative A carve-out for TopBar dialog launches. TopBar @inject IDialogService MudDialogService (NOT ICbDialogService) for PasswordPromptDialog + AdminManageUsersDialog because those dialog content components still use `<MudDialog>` internally and need a MudDialogProvider parent. Phase 7 MIG slice migrates the launch path AND the dialog internals together — at that point the @inject IDialogService line is deleted alongside the Mud providers in MainLayout.
- v1.2 / D14 *(Plan 05-05)*: NavMenu.razor deleted now (not at Phase 7). Plan-file Task 4 said "leave NavMenu unreferenced as a fallback through Phase 6"; executor task prompt explicitly requested deletion. Repo-wide `grep -rn "NavMenu" src/CookBot.Web/` found zero live references after MainLayout rewrite — only doc-comment mentions in Sidebar.razor describing the supersession. Leaving it would be dead code.
- v1.2 / D15 *(Plan 05-05)*: Dark-mode icon stays as Sun for both light/dark states. The 36-icon Plan 05-01 set has sun but no moon. The button itself is the toggle; tooltip provides the directional cue ("Switch to light mode" / "Switch to dark mode"). Phase 6 polish item if a clearer glyph is desired.
- v1.2 / D16 *(Plan 06-03)*: TopBar right-slot — `<TopBar>` exposes a `RightSlot` RenderFragment, but `MainLayout` instantiates it without a per-page passthrough mechanism. RecipeView (and future surfaces with right-side actions) render their action row inline above the hero per CONTEXT D-17 PRAGMATIC. Adding a CascadingValue<RenderFragment> bridge would require touching MainLayout (not in scope for any Phase 6 plan). Tracked as a future SHELL-03 polish slice; not blocking.
- v1.2 / D17 *(Plan 06-03)*: Recipe View Share opens existing `ShareCookbookDialog` for the recipe's parent cookbook. Sharing in this codebase is cookbook-scoped (`CookbookShares` table) — there is no recipe-level share concept. `ShareCookbookDialog` continues to launch via MudBlazor `IDialogService` per the Phase 6 D-30 coexistence carve-out; Phase 7 MIG slice migrates the dialog content.
- v1.2 / D18 *(Plan 06-03)*: Made-count + last-cook notes are stubbed with deterministic `0×` and a hidden conditional, respectively. v1.2 has no `RecipeMade` log entity (Plan 06-01 SUMMARY confirmed). The conditional in markup wires the callout to light up automatically once a log entity lands; CS0649 is suppressed locally with an explanatory pragma. FUTURE-Recently-Cooked extension point.
- v1.2 / D19 *(Plan 06-04)*: `recipe-chip-composer.js` is preserved verbatim from v1.1 Phase 3 partial work. The interop API (`ping` / `getCaretCoords` / `bindSegmentEvents` / `unbindSegmentEvents` / `scrollIntoViewWithHighlight`) and the contenteditable=plaintext-only segment-span model already align with how the v1.2 RecipeChipComposer.razor renders chips and segments. No new JS bridge methods were needed — the C# side already serializes by holding `Text` as canonical `[name](#id)` markdown and reconciling segment edits via `OnSegmentInputFromJs`.
- v1.2 / D20 *(Plan 06-04)*: Non-modal timer-suggestion banner replaces the v1.1 MudMenu popover. The banner renders inline above the step body (same DOM tree, no portal, no JS coordination), so it composes cleanly with the chip composer's caret position and the v1.2 visual language (accent-soft pill bar with Yes / No buttons). Per-step session-dismissed durations live in a `HashSet<int>` on the RecipeStepEditor instance — ephemeral; resets on re-render. Saves never auto-rewrite step text; explicit chip acceptance is the only persisted source of timers.
- v1.2 / D21 *(Plan 06-04)*: Step kind toggle is a custom segmented control (cream-2 track, paper thumb on the active segment) instead of a MudToggleGroup. Step→Section conversion still uses the existing `SectionDropConfirmationDialog` when content would be lost; that dialog continues to use MudDialog content (Phase 7 D-30 coexistence).
- v1.2 / D22 *(Plan 06-04)*: Ingredient picker is a custom keyboard-navigable dropdown (input + listbox of `cb-row` buttons; ArrowUp/Down + Enter + Escape) instead of MudAutocomplete. Anchored to the chip-flow surface via `getBoundingClientRect` coords from the existing JS interop helper `getCaretCoords`. The picker is rendered inside the page DOM (no portal); z-index 1300 keeps it above the meta rail.
- v1.2 / D23 *(Plan 06-04)*: The v1.1 prototype's IngredientChip "Replace…" MudMenu is dropped. The v1.2 chip click instead emits `OnRequestReplace`; the parent re-opens the picker scoped to the same chip range, and the user picks a replacement ingredient (or cancels). Equivalent functional outcome with simpler DOM.
- v1.2 / D24 *(Plan 06-04)*: TimerChip's edit popover is rendered inline (CbCard absolutely positioned below the chip, no portal). The popover edits Duration / Unit / Label and writes through `OnChanged`; ParsedTimer is mutated in place, so the parent's `StateHasChanged` from `RemoveTimer` / `UpdateTimer` is enough — no model rebuild required.
- v1.2 / D25 *(Plan 06-04)*: Description input is wired in markup (38px title + 15px description per design handoff) but the Recipe entity has no Description column today. `_description` is held locally and discarded on save; once the schema gains a description field (FUTURE-V1.1-* slot), wiring up persistence is a one-line change in `PopulateFromRecipe` + the `SaveRecipe` ParsedRecipe builder. Same architectural shape as RecipeView's hidden lead-paragraph slot (Plan 06-03 D-25).
- v1.2 / D26 *(Plan 06-04)*: Cookbook switching at edit-time. The right-rail Cookbook card uses `CbDropdown<int>` populated from the user's owned cookbooks. Switching updates `_selectedCookbookId` so a save targets the new cookbook. Edit mode does NOT reparent existing recipes (`RecipeService.UpdateAsync` looks up the recipe by id, then writes whatever ParsedRecipe is supplied — but the Cookbook association is fixed by the recipe's existing CookbookId; the dropdown is a no-op for edits today). Cookbook reparenting is FUTURE; the dropdown selector still surfaces visually for parity with the design handoff.
- v1.2 / D27 *(Plan 07-01)*: Dialog content components in the CbDialogService pattern do NOT include a `<CbDialog>` wrapper. CbDialogHost wraps the content in `<CbDialog>` slots automatically (Header from the ShowAsync title arg; Body from the dynamic component). Migrated dialogs only ship the body + footer markup. Matches the existing `SampleDialogContent` pattern.
- v1.2 / D28 *(Plan 07-01)*: CookbookDownloadHelper migrated from `ISnackbar` to `ICbToastService` alongside the cookbook-pages migration. Required to strip Mud from CookbookList/CookbookDetail (Rule 3). All three call sites updated atomically. QuestPDF / `CookbookPdfService` export path unchanged.
- v1.2 / D29 *(Plan 07-01)*: `Components/Dialogs/ConfirmDialog.razor` added as a Cb-native generic confirm primitive (Title / Message / ConfirmLabel / CancelLabel / Destructive). Replaces `IDialogService.ShowMessageBox` calls used by CookbookDetail's delete-cookbook + delete-recipe prompts. Reusable by future plans 07-02..07-05.
- v1.2 / D30 *(Plan 07-01)*: Cookbook accent color cycles by `Math.Abs(cookbookId) % 4` over the four design-handoff palette tokens (`var(--accent-soft)`, `var(--cream-2)`, `#E1ECDF`, `#F0E2C8`) since v1.2 has no AccentColor field on the Cookbook entity. Stable per cookbook across renders. User-facing accent picker remains FUTURE-14.
- v1.2 / D31 *(Plan 07-01)*: Inline edit + delete on cookbook list cards REMOVED to match the design-handoff intent (clean cookbook collage cards without action chrome). Edit (rename) + delete + share now live on the cookbook detail page hero so no operation is lost. Click on a card navigates to detail.
- v1.2 / D32 *(Plan 07-01)*: ShareCookbookDialog's MudTabs replaced with a small inline pill segmented-button switcher (People / Export). No new atom needed — directly uses cb-btn classes for the tab buttons. Equivalent UX with simpler DOM.
- v1.2 / D33 *(Plan 07-02)*: MudAutocomplete (ingredient picker in AddPantryItemDialog) replaced by `<CbSelect>` over the eagerly-loaded ingredient list + a companion `<CbInput>` "or add new ingredient" field. The original supported free-text fall-through; the Cb-native pattern splits selection vs new-name into two explicit inputs. No Cb autocomplete atom needed for this plan; FUTURE if desired.
- v1.2 / D34 *(Plan 07-02)*: MudNumericField → `<CbInput Type="number">` with culture-invariant `double.TryParse` and a `HasAmount`-derived disabled state on the unit `<CbSelect>`. Preserves "leave blank to track only that you have it" UX without adding a Cb numeric atom.
- v1.2 / D35 *(Plan 07-02)*: MudDatePicker → native `<input type="date">` styled with `class="cb-input"` (matches the .cb-input visual baseline). No Cb date atom is needed; modern browsers ship native date pickers.
- v1.2 / D36 *(Plan 07-02)*: PantryView item-status (in-stock / low / expiring / out) computed via a static `StatusOf(PantryItem)` helper. Out = measured row with `Amount<=0`. Expiring = `ExpirationDate` within 7 days. Low = measured row below a per-unit heuristic threshold (kg/liter<0.5, g/ml<100, cup/pint/quart<1, pieces<2). The heuristic is intentionally conservative; smart pantry-match (FUTURE-13) will replace it with user-configurable thresholds.
- v1.2 / D37 *(Plan 07-02)*: PantryView per-row cart-icon (Add to grocery list) wired as a disabled affordance for now (`title="Add to grocery list"`). Grocery-list quick-add lives in Plan 07-03; doing it here would create a circular dep. Trash icon (delete from pantry) is fully wired.
- v1.2 / D38 *(Plan 07-04)*: AiChat assistant-instructions panel REMOVED from this surface. Previous (Mud-based) AiChat hosted a MudExpansionPanels block w/ chip-based token insertion + MudTextField template editor + Save/Reset. The design-handoff ai-chat.jsx removes it. UserProfile.AiSystemPromptTemplate is still loaded and consumed by BuildSystemPrompt → IAiService.StreamMessageAsync; only the editor UI is gone. Plan 07-05 (Profile) is the natural home for the editor — flagged as DEFERRED-PROF-AIPROMPT.
- v1.2 / D39 *(Plan 07-04)*: Recipe canvas binds to canonical RecipeDocument directly via _lastStructuredRecipe.Value — no projection from rendered text, no extractor revival. POLISH-01 invariant preserved. Active-step accent-soft circle highlights the LAST ContentStep (matches design's "live streaming caret on the active step" intent — RecipeDocument is delivered atomically by IAiRecipeGenerator, so a token-level caret would misrepresent reality).
- v1.2 / D40 *(Plan 07-04)*: Dual send buttons in AiChat input. Spark (accent-soft 30×30 circle) → GenerateRecipeFromInput → IAiRecipeGenerator (recipe canvas). Send (accent 30×30 circle) → SendMessage → IAiService.StreamMessageAsync (free-form chat). Splits the design's single send button to preserve the existing v1.1 distinction between orchestrator path and free-form chat without forcing model-routing into prompt heuristics.
- v1.2 / D41 *(Plan 07-04)*: PromptBuilder Output format radio + Voice select are reserved UI state (FUTURE-OUT-FMT, FUTURE-VOICE). PromptBuilderService.BuildCopyablePrompt accepts (userRequest, profile, pantryItems, includeProfile, includePantry) — no format/voice args. Markdown / Plain text / Warm / Technical are captured in page state and ready for a future service extension to honor without a layout change. Equipment list + Past favorites Include checkboxes are also reserved (FUTURE-INCLUDE-EQUIP, FUTURE-INCLUDE-FAV).
- v1.2 / D42 *(Plan 07-04)*: cb-blink + cb-pulse @keyframes added globally to cookbot-design.css. .cb-caret class lives inline in AiChat.razor (single consumer). cb-pulse used by save-bar drafting dot AND skeleton blocks AND PromptBuilder dot (none yet but reserved for future surfaces).
- v1.2 / D43 *(Plan 07-05)*: Density storage via localStorage instead of UserProfile field. UserProfile.cs has no Density column; adding one now would need a migration solely for a UI-pref toggle. localStorage scoped per-browser matches modern dark-mode patterns (cookbot_dark_mode is already localStorage-backed). cookbot.setDensity / cookbot.getDensity / extended applyDefaults handle persistence; data-density attribute is set on <html> before first paint so reload preserves the choice without flicker.
- v1.2 / D44 *(Plan 07-05)*: AdminManageUsersDialog reuses ConfirmDialog (Phase 7 / Plan 07-01 D-29) for the delete-user confirmation. Mirrors CookbookDetail's delete-cookbook + delete-recipe pattern; keeps a single Cb-native generic confirm primitive across the codebase. No yes/no message-box primitive needed.
- v1.2 / D45 *(Plan 07-05)*: SharedKeysDialog inline alerts use ad-hoc `<div class="cb-card">` with severity-tinted backgrounds (var(--accent-soft) / var(--warn-soft)) instead of a dedicated CbAlert atom. Two existing migrated dialogs (AdminManageUsersDialog, EditProfile API-key card) use the same inline pattern; introducing an atom for three consumers is premature. CbAlert is FUTURE if a fourth use case lands.
- v1.2 / D46 *(Plan 07-05)*: Profile equipment + dietary multi-selects render as `<button class="cb-chip">` with aria-pressed (single-element toggle) instead of the previous MudChipSet. No new atom needed; chip pressed-state styling pulled from existing cb-chip variants (timer / ing for selected; tag for unselected).
- v1.2 / D47 *(Plan 07-05)*: MainLayout's four MudBlazor providers (`<MudThemeProvider>` / `<MudPopoverProvider>` / `<MudDialogProvider>` / `<MudSnackbarProvider>`) are kept mounted through Plan 07-05 even though no consumer remains in the Razor tree after the TopBar IDialogService migration. Removing them now would entail also editing csproj + _Imports.razor + Program.cs + App.razor, which is the express scope of Plan 07-07 (per Phase 7 D-13). One atomic terminal strip is cleaner than splitting cleanup across two plans.
- v1.2 / D48 *(Plan 07-07)*: RecipeMade.razor was unmigrated through Plan 07-06 — discovered at strip time as a Rule 3 blocker (its full Mud markup would prevent compile after MudBlazor package removal). Migrated inline as part of Plan 07-07 rather than deferring or splitting into a separate plan. New markup uses CbCard + native input type=number for the servings multiplier (with culture-invariant double.TryParse) + Cb atoms throughout; ICbToastService replaces ISnackbar. All @code-block logic preserved verbatim.
- v1.2 / D49 *(Plan 07-07)*: CookingMode.razor + RecipeEditor.razor each kept @inject ISnackbar + Snackbar.Add() calls through Plan 07-06. Replaced inline as Rule 3 blockers — they would prevent compile after the MudBlazor.Services using removal in Program.cs. 8 call sites total (CookingMode 1 + RecipeEditor 7) mapped: Snackbar.Add(msg, Severity.X) → Toast.Show(msg, CbToastSeverity.X). Behavior identical.
- v1.2 / D50 *(Plan 07-07)*: SampleDialogContent.razor deleted alongside DesignSandbox.razor. SampleDialogContent had exactly one consumer (DesignSandbox) and no production callers; leaving it would be dead code. The plan-file's Files-affected list named only DesignSandbox; SampleDialogContent's deletion is a Rule 3 cleanup of orphaned dead code.
- v1.2 / D51 *(Plan 07-07)*: Test scaffolding cleanup mandatory. tests/CookBot.Tests had three files (StepSectionToggleTests / RecipeChipComposerTests / PasteFlowTests) using MudBlazor types directly — left over from when those test subjects rendered MudMenu/MudPopover/MudDialogProvider. With production code Cb-only, the Mud scaffolding was dead infrastructure preventing test compile after the strip. The IPopoverService stub, AddMudServices() calls, and MudDialogProvider rendering all dropped; tests still pass 196/196. Pre-flight grep was scoped to src/CookBot.Web/ per the plan; tests/ blockers were caught by the post-strip dotnet build (13 errors), not by pre-flight.
- v1.2 / D52 *(Plan 07-07)*: RecipeChipComposerTests.JsInteropFails test renamed FallsBackToMudTextField → FallsBackToCbTextarea and its assertion changed from a MudTextField marker to a `<textarea` substring (CbTextarea renders a plain textarea). The fallback path itself was migrated in Plan 06-04 (D-D4 evolution) — only the test marker needed updating.
- v1.2 / D53 *(Plan 07-07)*: Documentation comments referring to MudBlazor history were stripped from production .razor files even when no executing code remained, per Hard Invariant #1 ("clean those too if found"). Edits applied to MainLayout, TopBar, RecipeView, RecipeEditor, IngredientChip, RecipeStepEditor, ConfirmDialog, AddPantryItemDialog, AddGroceryListItemDialog. Internal documentation like "MudTextField fallback" became "CbTextarea fallback"; longer phase-history blocks were rephrased as forward-only descriptions.

Inherited decisions from v1.1 (still load-bearing in v1.2):

- AI-off toggle hides ALL AI surfaces (`UserProfile.AiEnabled`) — sidebar items + AI buttons + AI suggestion cards (verified across 5 surfaces in Phase 7 success criterion #2)
- Recipe screens round-trip canonical `RecipeDocument` (v1.1 Phase 1 schema) — Phase 6 success criteria gate this
- AI chat uses structured-output orchestrator (v1.1 Phase 2) — no three-tier extractor (Phase 7 AIC-04 preserves POLISH-01 invariant)
- No Newtonsoft.Json, no `Microsoft.Extensions.AI` migration, no second AI provider
- Trusted-LAN auth posture; `AuthMode` reserved for future hardening

### Pending Todos

[From .planning/todos/pending/ — ideas captured during sessions]

None yet for v1.2.

### Blockers/Concerns

[Issues that affect future work]

- ~~v1.2 Phase 5 (Foundation) must finish before Phases 6 and 7 can start~~ — RESOLVED 2026-04-27. Phase 5 complete; Phases 6/7 can begin.
- ~~The recipe-editor chip composer (v1.2 ED-* requirements) integrates with v1.1's canonical RecipeDocument — round-trip integrity is non-negotiable (Phase 6 success criterion #4)~~ — RESOLVED 2026-04-27 (Plan 06-04 ships, SC#4 gate passed).
- v1.1 Phase 3 plan documents in `.planning/phases/03-editor-ux-without-special-syntax/` are NOT being deleted; they encode chip-composer design intent still relevant to v1.2 ED-03..ED-09
- ~~MIG-01..03 cannot run until Phases 6 + 7 surfaces are migrated; planned as the terminal Phase 7 slice (D6)~~ — RESOLVED 2026-04-27 (Plan 07-07 shipped; MudBlazor entirely removed).
- ~~Phase 5 leaves a documented deletion-target list for Phase 7 MIG cleanup: 4 MudBlazor providers in MainLayout, TopBar's @inject IDialogService line + 2 dialog launch paths, AddMudServices() in Program.cs, @using MudBlazor in _Imports.razor, MudBlazor packages in csproj, MudBlazor static assets in App.razor, /design-sandbox route + DesignSandbox.razor + SampleDialogContent.razor (D-12, D-13)~~ — RESOLVED 2026-04-27 (Plan 07-07 deleted everything on the list, plus the SampleDialogContent it noted).
- **No active blockers.** v1.2 milestone is shippable; ready for /gsd-audit-milestone v1.2 and /gsd-complete-milestone v1.2.

## Deferred Items

Items captured in REQUIREMENTS.md "Future Requirements" — not in v1.2 scope:

| Category | Item | Status | Deferred At |
|----------|------|--------|-------------|
| Format fields | FUTURE-V1.1-01: per-step temperature (was FEATURE-V2-01..05) | Deferred (v1.1 → v1.3+) | 2026-04-27 |
| Cleanup | FUTURE-V1.1-02: TagsJson → relational table (was POLISH-04) | Deferred | 2026-04-27 |
| Cleanup | FUTURE-V1.1-03: LegacyRecipeProjector deletion-target comment (was POLISH-03) | Deferred | 2026-04-27 |
| Cleanup | FUTURE-V1.1-04: Snapshot test on assembled system prompt (was POLISH-05) | Deferred | 2026-04-27 |
| Cleanup | FUTURE-V1.1-05: README "Recipe Format" section (was POLISH-07) | Deferred | 2026-04-27 |
| Security | FUTURE-01: Encrypt-at-rest for `UserProfile.AiApiKey` | Deferred | 2026-04-25 |
| Telemetry | FUTURE-02: Token-cost telemetry per key owner | Deferred | 2026-04-25 |
| Format fields | FUTURE-03..06: substitutions, equipment, doneness cues, source provenance | Deferred | 2026-04-25 |
| Export | FUTURE-07/11: Schema.org rich-results, Cooklang one-way export | Deferred | 2026-04-25 |
| Nutrition | FUTURE-08: USDA FDC nutrition computation | Deferred | 2026-04-25 |
| AI fallback | FUTURE-09: Tool-use fallback if Structured Outputs regresses | Deferred | 2026-04-25 |
| Maintenance | FUTURE-10: MudBlazor 9.x upgrade | Obsolete (v1.2 strips MudBlazor) | 2026-04-27 |
| AI consent | FUTURE-12: Per-sharer cookbook-import consent banner | Deferred | 2026-04-26 |
| UX | FUTURE-13: Smart pantry-match for HOME-02 (expiration + %-pantry-used + dietary) | Deferred | 2026-04-27 (HOME-02 ships deterministic stub) |
| UX | FUTURE-14: User-facing accent variant picker (terracotta/sage) | Deferred | 2026-04-27 (DS-02 wires tokens; no surface) |

## Session Continuity

Last session: 2026-04-27T23:30:00Z
Stopped at: v1.2 manual-smoke bug-fix sweep landed (6 commits b2df97b…bfe73fe, 12/12 issues fixed). v1.2 milestone COMPLETE — all 3 phases (5/6/7), 16/16 plans, 75/75 requirements + post-ship smoke-fix slice.
Resume file: None

**Next:** Run `/gsd-audit-milestone v1.2` to verify milestone deliverables match goals (the auditor will spot-check SUMMARY frontmatter, requirement coverage, success-criteria, and any gaps); then `/gsd-complete-milestone v1.2` to seal the milestone and roll into v1.3 planning. v1.3 is currently empty — no requirements have been authored. The deferred-items table above (FUTURE-V1.1-01..05, FUTURE-13, FUTURE-14, DEFERRED-PROF-AIPROMPT, FUTURE-Recently-Cooked, plus the v1.1 FUTURE-01..12 carryovers) is the natural seed for v1.3 milestone scoping. The v1.2 strip + production migration + tests are committed as `71c0dce`; documentation + state updates follow as a second commit.
