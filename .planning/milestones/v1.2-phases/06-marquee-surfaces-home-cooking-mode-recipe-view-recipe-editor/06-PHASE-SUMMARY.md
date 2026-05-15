---
phase: 6
phase_name: "Marquee surfaces — Home, Cooking Mode, Recipe View, Recipe Editor"
milestone: v1.2
status: complete
plans_complete: 4
plans_total: 4
requirements_complete: [HOME-01, HOME-02, HOME-03, HOME-04, COOK-01, COOK-02, COOK-03, COOK-04, COOK-05, COOK-06, RV-01, RV-02, RV-03, RV-04, RV-05, ED-01, ED-02, ED-03, ED-04, ED-05, ED-06, ED-07, ED-08, ED-09]
absorbed_milestones: [v1.1 EDITOR-01, v1.1 EDITOR-02, v1.1 EDITOR-03, v1.1 EDITOR-04, v1.1 EDITOR-05, v1.1 EDITOR-06, v1.1 EDITOR-07]
completed: 2026-04-27
---

# Phase 6: Marquee surfaces — Home, Cooking Mode, Recipe View, Recipe Editor

## Summary

The four user-visible marquee surfaces of the v1.2 redesign — **Home**, **Cooking Mode**, **Recipe View**, **Recipe Editor** — have all been migrated from MudBlazor to the Phase 5 atom system. Each surface consumes the canonical `RecipeDocument` directly (no projection from legacy columns). The Recipe Editor absorbs the entire v1.1 Phase 3 chip-composer scope (EDITOR-01..07) so the work is built once, in the right component system.

When this phase ships:
- **Home** looks like a kitchen dashboard with a pantry-aware hero, a glance strip of stat tiles, and recently-cooked + up-next strips.
- **Cooking Mode** looks like a tablet propped against a backsplash — dark cocoa surface, 224px adaptive timer hero, always-on right rail with link-driven ingredient highlighting, 1fr/2fr bottom step nav with arrow-key navigation.
- **Recipe View** is editorial — 64px display title, hanging accent numerals on method, sticky 300px sidebar with live servings scaling, last-cook callout (wired for the future RecipeMade log).
- **Recipe Editor** is keyboard-driven — borderless title/subtitle inputs, ingredients grid with Tab/Backspace semantics, chip-aware step composer with explicit ingredient picker, inline non-modal timer-suggestion banner, paste-raw-text routing through the canonical schema parser, AI Suggestions card hidden when AI is off.

MudBlazor still loads (`MainLayout` retains the four MudBlazor providers; the existing dialog content components like `PasteRawTextDialog`, `ShareCookbookDialog`, `SectionDropConfirmationDialog` keep their MudDialog wrapping). Phase 7's terminal slice strips MudBlazor entirely.

## Plans

| # | Plan | Requirements | Outcome |
|---|------|-------------|---------|
| 06-01 | Home dashboard rewrite | HOME-01..04 | Pantry-aware hero (deterministic stub matcher; FUTURE-13 extension point), glance strip (Recipes / Cookbooks / Pantry / Grocery), recently cooked + up next. AI-off contract enforced on the Generate-a-recipe quick action. |
| 06-02 | Cooking Mode rewrite | COOK-01..06 | Dark cocoa background, segmented step rail, 224px tabular adaptive timer hero, always-on right rail with `[name](#id)` link-only ingredient highlighting (ED-08 satisfied transitively), 1fr/2fr bottom step nav, arrow-key + PageUp/Dn + Esc navigation, AI assist panel collapsed to single "Ask about this step" button. JS-interop preservation: `cooking-timers.js`, browser notifications, `RecipeChipComposer.scrollIntoViewWithHighlight`, `RecipeCookingAiContext`. |
| 06-03 | Recipe View rewrite | RV-01..05 | Editorial layout (64px `cb-recipe-cap` title + 4-stat row), 4:3 striped hero, 300px sticky sidebar with -/+ servings widget driving `RecipeScalingService.FormatScaledAmount` (servings-only scaling), hanging accent numerals on method (28px tabular), inline timer chips per `ContentStep.Timers`, last-cook callout (always hidden in v1.2 — no `RecipeMade` log entity). Canonical-first reads — zero legacy column projection (Phase 6 SC#3 gate). |
| 06-04 | Recipe Editor rewrite | ED-01..09 (absorbs v1.1 EDITOR-01..07) | Borderless title/subtitle inputs (38px / 15px), ingredients grid with keyboard-driven add/remove + immutable-id reorder, chip-aware step composer (contenteditable spans + custom keyboard-navigable picker), step/section toggle with confirmation dialog, inline non-modal timer-suggestion banner, paste-raw-text routing, AI Suggestions card hidden when AI off. Six existing RecipeEditorParts components rewritten against Phase 5 atoms. |

## Phase success criteria (SC#1..SC#5)

| Criterion | Status | Notes |
|-----------|--------|-------|
| **SC#1** Each marquee surface migrated from MudBlazor to Phase 5 atoms | ✅ | All four surfaces shipped. Zero `Mud*` component refs in any surface's primary markup. The four surviving MudDialog content components (PasteRawTextDialog, ShareCookbookDialog, SectionDropConfirmationDialog, SaveRecipeDialog) launch via `IDialogService` per the Phase 6 D-30 coexistence carve-out; Phase 7 terminal slice migrates dialog content. |
| **SC#2** AI-off contract enforced on every AI surface in Phase 6 | ✅ | Home: "Generate a recipe" quick action hidden (`_aiOff`). Cooking Mode: "Ask about this step" button hidden (existing gate preserved). Recipe View: no AI surface in design. Recipe Editor: AI Suggestions card hidden (host `AiFeaturesEnabled` AND user `AiEnabled` must both be true). |
| **SC#3** Surfaces consume `RecipeDocument` directly (no legacy column projection) | ✅ | Recipe View deserializes `Recipe.CanonicalDocumentJson` via `JsonRecipeSerializer`; legacy reads (`IngredientsJson`, `StepsJson`, `IngredientRefs`, `TagsJson`) are gone. Cooking Mode reads `RecipeStep.Text` (which is canonical `[name](#id)` markdown) via `IngredientLinkPatterns.Pattern`. Recipe Editor saves through the existing `RecipeService.UpdateAsync` / `CreateAsync` which canonicalize on every save. Home and surface-level dashboard counts use direct EF queries that don't touch `Recipe.IngredientsJson` etc. |
| **SC#4** Round-trip canonical RecipeDocument integrity (Project → Serialize → Parse → Validate returns identical doc) | ✅ | The save path is unchanged from v1.1 Phase 1. Editor produces `ParsedRecipe`; `RecipeService` runs `_projector.Project(recipe)` → `_canonicalSerializer.Serialize(canonicalDoc)` → persist. The validator catches drift before persistence. ED-06 immutable-id invariant on reorder preserves chip references; chip composer only inserts `[name](#id)` for explicit picker selections (no inferred matches). |
| **SC#5** Saving never auto-rewrites step text; explicit chips are the only persisted source of timers | ✅ | The v1.1 EDITOR-03 final clause is preserved at the service layer (`RecipeService.CreateAsync` / `UpdateAsync` write `Step.Timers` from the user-supplied `ParsedTimer` list — there is no regex-based auto-write). The editor's inline timer-suggestion banner is opt-in: Yes inserts a chip; No dismisses for the session. **Step.Text never gets rewritten.** ED-05 gate. |

## Requirements completed

**Home (HOME-01..04):** greeting + quick actions, pantry-aware hero, glance strip, recently cooked + up next.
**Cooking Mode (COOK-01..06):** dark cocoa background, top step rail, adaptive timer/step hero, always-on right rail, bottom step nav, JS-interop preservation.
**Recipe View (RV-01..05):** editorial title, sticky scaled-ingredients sidebar, hanging accent numerals on method, last-cook notes callout, top-bar share/cook actions.
**Recipe Editor (ED-01..09 — absorbing v1.1 EDITOR-01..07):** borderless title/subtitle inputs, ingredients table, chip composer, step/section toggle, timer suggestion, immutable id reorder, paste-raw-text routing, cooking-mode link-only highlight (verified), keyboard a11y.

## Decisions logged in this phase

These were added to STATE.md / PROJECT.md throughout the phase. See plan-level summaries for full rationale; the headlines:

- **D-16 (Plan 06-03):** TopBar exposes a `RightSlot` parameter but `MainLayout` instantiates it without per-page passthrough. Surfaces with right-side actions render inline above content as a PRAGMATIC fallback. Tracked as future SHELL-03 polish.
- **D-17 (Plan 06-03):** Recipe View Share opens existing `ShareCookbookDialog` (sharing is cookbook-scoped — no recipe-level share concept).
- **D-18 (Plan 06-03):** Made-count + last-cook notes stubbed (0× / hidden conditional). v1.2 has no `RecipeMade` log entity. Wired so callout lights up automatically once a log lands.
- **D-19..D-26 (Plan 06-04):** Recipe Editor decisions — JS-interop preservation, non-modal timer banner, custom segmented step toggle, custom keyboard-navigable picker, simplified IngredientChip (drop replace popover), inline TimerChip popover, description input wired but not persisted (no column today), cookbook switcher visual-only on edits.

## Pull-forward to Phase 7

Phase 7 is the terminal MudBlazor strip + remaining surfaces. The deletion-target list inherited from Phase 5 + Phase 6:

**Phase 5 deletion targets (unchanged):**
- 4 MudBlazor providers in `MainLayout.razor`
- TopBar's `@inject IDialogService` + 2 dialog launch paths (PasswordPromptDialog, AdminManageUsersDialog)
- `AddMudServices()` in `Program.cs`
- `@using MudBlazor` in `_Imports.razor`
- MudBlazor packages in `csproj`
- MudBlazor static assets in `App.razor`
- `/design-sandbox` route + `DesignSandbox.razor` + `SampleDialogContent.razor`

**Phase 6 deletion targets (new):**
- `PasteRawTextDialog.razor` — migrate dialog content to `<CbDialog>`-style markup
- `ShareCookbookDialog.razor` — migrate dialog content
- `SectionDropConfirmationDialog.razor` — migrate dialog content (in `RecipeEditorParts/`)
- `SaveRecipeDialog.razor`, `ImportCookbookDialog.razor`, `CookbookFormDialog.razor`, `AddPantryItemDialog.razor`, `AddGroceryListItemDialog.razor`, `NewGroceryListDialog.razor`, `CreateSharedPantryDialog.razor`, `ManagePantryMembersDialog.razor`, `AiPopulatePantryDialog.razor`, `AiStandardizePantryDialog.razor`, `SharedKeysDialog.razor`, `CookbookReferenceDialog.razor` — all dialog content components still using MudDialog
- All `@inject IDialogService` lines in surfaces that only launch the above dialogs (RecipeEditor, RecipeView, RecipeStepEditor) — once dialog content is migrated, the line can be replaced with `@inject ICbDialogService`

**Phase 6 surfaces that did NOT migrate** (still on MudBlazor — Phase 7 territory):
- `CookbookList.razor`, `CookbookDetail.razor`, `PantryView.razor`, `GroceryListView.razor`, `AiChat.razor`, `PromptBuilder.razor`, `EditProfile.razor`, `RecipeMade.razor`, plus all the dialogs above.

## Build / test gates

- ✅ `dotnet build` clean (0 warnings, 0 errors) at the end of every plan and at phase end.
- ✅ `dotnet test --filter 'Category!=RequiresApiKey'` — 196 / 196 passed at the end of every plan and at phase end.
- ✅ Tests updated (Plan 06-04) where existing assertions targeted MudBlazor-specific markup that the v1.2 rewrite invalidated. The same DA4 / DA6 invariants are still gated; only the surface markup is new.

## Velocity

| Plan | Duration |
|------|----------|
| 06-01 | ~25 min |
| 06-02 | ~5 min |
| 06-03 | ~4 min |
| 06-04 | ~24 min |
| **Phase 6 total** | **~58 min wall-clock** |

The four marquee surfaces shipped in under an hour of focused execution. The Recipe Editor (Plan 06-04) was the longest because it absorbed all of v1.1 Phase 3 (six existing parts components rewritten + the top-level page rebuilt + two test assertions updated). The other three plans were predominantly markup-replacement against atoms with `@code` block logic preserved verbatim.

## Status

**Phase 6: COMPLETE.** Phase 7 (remaining surfaces + terminal MudBlazor strip) is next.
