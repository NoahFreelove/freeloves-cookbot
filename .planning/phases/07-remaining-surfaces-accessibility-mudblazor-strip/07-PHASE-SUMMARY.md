---
phase: 7
phase_name: "Remaining surfaces, accessibility, MudBlazor strip"
milestone: v1.2
status: complete
plans_complete: 7
plans_total: 7
requirements_complete: [CB-01, CB-02, PA-01, PA-02, PA-03, PA-04, GR-01, GR-02, GR-03, GR-04, AIC-01, AIC-02, AIC-03, AIC-04, AIC-05, PB-01, PB-02, PB-03, PROF-01, PROF-02, A11Y-01, A11Y-02, A11Y-03, A11Y-04, MIG-01, MIG-02, MIG-03]
completed: 2026-04-27
---

# Phase 7: Remaining surfaces, accessibility, MudBlazor strip

## Summary

The remaining six surfaces of the v1.2 redesign — **Cookbooks** (list + detail), **Pantry**, **Grocery** (mobile-first), **AI Chat** (live recipe canvas), **Prompt Builder**, **Profile** — have all been migrated from MudBlazor to the Phase 5 atom system. ~18 dialog content components were migrated to the `<CbDialog>` slot pattern with `CbDialogService` launches. The cross-cutting accessibility audit (Plan 07-06) signed off all 9 v1.2 surfaces in light + dark with 13 targeted a11y fixes shipped. The terminal MudBlazor strip (Plan 07-07) deleted the package reference, removed `AddMudServices()`, removed `@using MudBlazor`, removed all four MudBlazor providers from `MainLayout`, removed the static MudBlazor CSS+JS tags from `App.razor`, and deleted `DesignSandbox.razor` + `SampleDialogContent.razor`.

When this phase ships:
- **Cookbooks list** is a 3-col grid of cookbook collage cards (3×2 striped tiles tinted by accent) with a search bar + grid/list view toggle.
- **Cookbook detail** shows hero + share/PDF/export action row + member chips above the recipe row list.
- **Pantry** ships the 4-tile summary strip (In stock / Running low / Expiring / Out — each with a colored vertical bar) above categorized stock cards with `<CbBadge>` status pills.
- **Grocery** is mobile-first (works on desktop too) — aisle-categorized sections with 24px circle checkboxes + sticky bottom "Add item" button.
- **AI Chat** has a 380px chat rail + flex canvas with a streaming caret + drafting pulse on the recipe canvas (sourced from canonical `RecipeDocument` via `IAiRecipeGenerator` — POLISH-01 invariant preserved).
- **Prompt Builder** has a 320px config rail + flex preview with a dark mono `<pre>` panel (sourced from `RecipeSchemaDocumentationProvider`).
- **Profile** ships density toggle + equipment + dietary multi-select chip rows + AI key card + display-name card.
- **Accessibility:** every interactive element has a 2px accent focus ring, ARIA roles on atoms (button/dialog/menu/list/progressbar/status/radio/checkbox/switch), WCAG AA contrast on warm-cream and cocoa-dark, manual dark-mode smoke pass for every surface.
- **MudBlazor:** completely gone from the dependency graph. Repo-wide `grep "Mud[A-Z]"` returns zero hits.

## Plans

| # | Plan | Requirements | Outcome |
|---|------|-------------|---------|
| 07-01 | Cookbooks (list + detail) + 7 cookbook dialogs | CB-01, CB-02 | Cookbook collage thumbnails (3×2 striped tiles tinted by accent), detail hero with share/PDF/export, recipe row list. 7 cookbook-related dialogs migrated to `<CbDialog>` (CookbookFormDialog, ShareCookbookDialog, ImportCookbookDialog, CookbookReferenceDialog, SaveRecipeDialog, PasteRawTextDialog, SectionDropConfirmationDialog). New ConfirmDialog atom (Cb-native generic confirm). CookbookDownloadHelper migrated from ISnackbar to ICbToastService. |
| 07-02 | Pantry + 5 pantry dialogs | PA-01..04 | 4-tile summary strip + categorized stock cards + per-row CbBadge status pills + AI populate/standardize buttons hidden when AI off. 5 pantry dialogs migrated (AddPantryItemDialog, CreateSharedPantryDialog, ManagePantryMembersDialog, AiPopulatePantryDialog, AiStandardizePantryDialog). MudAutocomplete → CbSelect + CbInput pair; MudNumericField → CbInput type=number; MudDatePicker → native HTML input type=date. |
| 07-03 | Grocery + 2 grocery dialogs | GR-01..04 | Mobile-first layout with aisle-categorized sections + 24px circle checkboxes + sticky "Add item" button. 2 grocery dialogs migrated (AddGroceryListItemDialog, NewGroceryListDialog). |
| 07-04 | AI Chat + Prompt Builder | AIC-01..05, PB-01..03 | AiChat 380px chat rail + recipe canvas (canonical RecipeDocument, no extractor revival — POLISH-01 invariant preserved). Dual send buttons (Spark = orchestrator path; Send = free-form chat). Streaming caret + drafting pulse via CSS keyframes. PromptBuilder 320px config rail + dark mono preview from RecipeSchemaDocumentationProvider. |
| 07-05 | Profile + 4 profile/sharing dialogs | PROF-01, PROF-02 | Profile settings cards (Display name / API key / AI toggle / Theme & density / Equipment / Dietary). Density toggle stored in localStorage (UserProfile has no Density column today). 4 profile dialogs migrated (AddUserDialog, AdminManageUsersDialog, PasswordPromptDialog, SharedKeysDialog). LAST IDialogService consumer (TopBar's @inject IDialogService line) removed. |
| 07-06 | Cross-cutting accessibility audit + small fixes | A11Y-01..04 | 9-surface walkthrough recorded in 07-06-AUDIT.md (per-surface ARIA gap matrix, WCAG AA contrast tables for warm-cream + cocoa-dark, dark-mode smoke pass per surface, deferred items). 13 targeted fixes: unified :focus-visible 2px accent outline; CbInput/CbTextarea/CbSelect AriaLabel parameter; CbToggle role='switch'; CbDialog aria-labelledby; CbToastHost role='status' + aria-live='polite'; CookingMode step rail role='progressbar'; aria-label on previously title-only icon-only buttons across TopBar / CookingMode / CookbookList / PantryView / AiChat. |
| 07-07 | Terminal MudBlazor strip + sandbox cleanup | MIG-01..03 | MudBlazor package + AddMudServices() + @using MudBlazor + 4 MainLayout providers + Mud static link/script tags + DesignSandbox.razor + SampleDialogContent.razor all deleted in one atomic commit. Pre-existing unmigrated production code (RecipeMade.razor full migration, CookingMode + RecipeEditor Snackbar usages) handled inline as Rule 3 blockers. Three bUnit test files de-Mudded (StepSectionToggleTests / RecipeChipComposerTests / PasteFlowTests). dotnet build clean (0/0); dotnet test 196/196 baseline preserved; repo-wide Mud[A-Z] grep returns zero hits. |

## Phase success criteria (SC#1..SC#5)

| Criterion | Status | Notes |
|-----------|--------|-------|
| **SC#1** Cookbooks / Pantry / Grocery / AI Chat / Prompt Builder / Profile migrated to Cb atoms; zero `mud-*` classes on those pages' DOM | ✅ | All 6 surfaces shipped (07-01..07-05). Plan 07-07's verification grep confirms zero `Mud[A-Z]` hits across the entire src/CookBot.Web/. |
| **SC#2** AI-off contract enforced on all 5 AI surfaces (sidebar AI rows + Home Generate + Recipe Editor AI Suggestions + Pantry AI populate/standardize + AI Chat + Prompt Builder routes' contents) | ✅ | Sidebar (Phase 5), Home (Phase 6), Recipe Editor (Phase 6), Pantry (Plan 07-02), AI Chat + Prompt Builder route contents (Plan 07-04). Each surface gates on `_aiOff` derived from host `AiFeaturesEnabled` AND user `AiEnabled`. |
| **SC#3** AI Chat + Prompt Builder ship the design-handoff layouts; AI Chat canvas pulls from canonical RecipeDocument (POLISH-01 invariant preserved) | ✅ | Plan 07-04. AiChat binds canvas to `_lastStructuredRecipe.Value` (RecipeDocument from IAiRecipeGenerator); the legacy three-tier ExtractRecipeContent extractor stays deleted. PromptBuilder reads from `RecipeSchemaDocumentationProvider`. |
| **SC#4** Accessibility audit passes — visible focus rings, keyboard-only nav across all 9 surfaces, WCAG AA contrast on warm-cream + cocoa-dark, ARIA roles, dark-mode smoke pass | ✅ | Plan 07-06 audit (07-06-AUDIT.md). 13 targeted fixes shipped. The previously deferred v1.1 EDITOR-07 chip composer keyboard semantics signed off as part of this audit. |
| **SC#5** MudBlazor + MudBlazor.Services package references deleted; _Imports.razor + Program.cs + MainLayout cleaned; repo-wide Mud[A-Z] grep zero hits; dotnet build + dotnet test pass; all preserved behaviors (dark mode toggle, user-switcher with password, admin manage users, session-scoped current user, AI-off, browser notifications, JS interop) work as before | ✅ | Plan 07-07. dotnet restore: 0 MudBlazor entries in project.assets.json. dotnet build: 0/0. dotnet test --filter "Category!=RequiresApiKey": 196/196. All preserved behaviors verified by inspection — none of those code paths reference MudBlazor today. |

## Requirements completed

**Cookbooks (CB-01, CB-02):** list + detail, collage thumbnails, share/PDF/export action row, member chips, recipe row list.

**Pantry (PA-01..04):** 4-tile summary strip, categorized stock cards, status badges, AI-off contract on populate/standardize buttons.

**Grocery (GR-01..04):** mobile-first aisle layout, 24px circle checkboxes, sticky bottom add-item button, recipe-driven shopping list creation.

**AI Chat (AIC-01..05):** 380px chat rail + flex canvas + recipe canvas from canonical RecipeDocument + streaming caret + drafting pulse + suggestion-chip input bar.

**Prompt Builder (PB-01..03):** 320px config rail + flex preview + dark mono pre panel + Copy prompt action.

**Profile (PROF-01, PROF-02):** settings cards (Display name / API key / AI / Theme & density / Equipment / Dietary), density toggle, AI off kill-switch.

**Accessibility (A11Y-01..04):** focus rings, keyboard nav, ARIA roles, contrast, dark-mode smoke pass — all 9 surfaces.

**MudBlazor strip (MIG-01..03):** package reference deleted, AddMudServices removed, @using MudBlazor removed, MainLayout providers + Program.cs + _Imports + App.razor + csproj all cleaned. Repo-wide Mud[A-Z] grep returns zero hits.

## Decisions logged in this phase

These were added to STATE.md / PROJECT.md throughout the phase. See plan-level summaries for full rationale; the headlines:

- **D-27 (Plan 07-01):** Dialog content components in the CbDialogService pattern do NOT include a `<CbDialog>` wrapper. CbDialogHost wraps content automatically. Migrated dialogs ship body + footer markup only.
- **D-28 (Plan 07-01):** CookbookDownloadHelper migrated from ISnackbar to ICbToastService alongside the cookbook-pages migration.
- **D-29 (Plan 07-01):** New ConfirmDialog atom — Cb-native generic confirm primitive. Replaces IDialogService.ShowMessageBox calls.
- **D-30 (Plan 07-01):** Cookbook accent color cycles by `Math.Abs(cookbookId) % 4` over 4 design-handoff palette tokens (no AccentColor field on Cookbook entity in v1.2). Stable per cookbook. User-facing accent picker is FUTURE-14.
- **D-31 (Plan 07-01):** Inline edit + delete on cookbook list cards REMOVED to match design-handoff intent. Edit/delete/share moved to detail page hero.
- **D-32 (Plan 07-01):** ShareCookbookDialog's MudTabs replaced with inline pill segmented switcher (People / Export). No new atom needed.
- **D-33 (Plan 07-02):** AddPantryItemDialog ingredient picker — MudAutocomplete → CbSelect + companion CbInput "or add new ingredient" field. Splits selection vs new-name into two explicit inputs.
- **D-34 (Plan 07-02):** MudNumericField → CbInput Type="number" with culture-invariant double.TryParse.
- **D-35 (Plan 07-02):** MudDatePicker → native `<input type="date">` styled with class="cb-input".
- **D-36 (Plan 07-02):** PantryView item-status (in-stock / low / expiring / out) computed via static StatusOf(PantryItem). Smart pantry-match (FUTURE-13) will replace heuristic.
- **D-37 (Plan 07-02):** PantryView per-row cart-icon (Add to grocery list) wired as disabled affordance for now (Plan 07-03 wires it).
- **D-38 (Plan 07-04):** AiChat assistant-instructions panel REMOVED from this surface per design-handoff. UserProfile.AiSystemPromptTemplate still loaded; only the editor UI is gone. Plan 07-05 (Profile) is the natural home — flagged DEFERRED-PROF-AIPROMPT.
- **D-39 (Plan 07-04):** Recipe canvas binds to canonical RecipeDocument directly (`_lastStructuredRecipe.Value`); no extractor revival. POLISH-01 invariant preserved.
- **D-40 (Plan 07-04):** Dual send buttons. Spark (accent-soft) → IAiRecipeGenerator (canvas). Send (accent) → IAiService.StreamMessageAsync (free-form chat).
- **D-41 (Plan 07-04):** PromptBuilder Output format radio + Voice select are reserved UI state (FUTURE-OUT-FMT, FUTURE-VOICE).
- **D-42 (Plan 07-04):** cb-blink + cb-pulse @keyframes added globally to cookbot-design.css.
- **D-43 (Plan 07-05):** Density storage via localStorage instead of UserProfile field. data-density attribute set on `<html>` before first paint.
- **D-44 (Plan 07-05):** AdminManageUsersDialog reuses ConfirmDialog (Phase 7 / Plan 07-01 D-29) for delete confirmation.
- **D-45 (Plan 07-05):** SharedKeysDialog inline alerts use ad-hoc `<div class="cb-card">` with severity-tinted backgrounds. CbAlert atom is FUTURE if a fourth use case lands.
- **D-46 (Plan 07-05):** Profile equipment + dietary multi-selects render as `<button class="cb-chip">` with aria-pressed (single-element toggle).
- **D-47 (Plan 07-05):** MainLayout's four MudBlazor providers kept mounted through Plan 07-05 even though no consumer remains in the Razor tree. Removed atomically by Plan 07-07.
- **D-48 (Plan 07-07):** RecipeMade.razor unmigrated through Plan 07-06 — discovered at strip time as a Rule 3 blocker. Migrated inline.
- **D-49 (Plan 07-07):** CookingMode + RecipeEditor Snackbar usages migrated to ICbToastService inline (Rule 3 blockers).
- **D-50 (Plan 07-07):** SampleDialogContent.razor deleted alongside DesignSandbox (no production callers).
- **D-51 (Plan 07-07):** Test scaffolding cleanup mandatory — three bUnit test files de-Mudded.
- **D-52 (Plan 07-07):** RecipeChipComposerTests JsInteropFails fallback test renamed MudTextField → CbTextarea (production fallback was migrated in 06-04).
- **D-53 (Plan 07-07):** Mud documentation comments stripped per Hard Invariant #1.

## Manual smoke-pass plan (recommended before milestone audit)

For each surface, verify renders + functions correctly in light + dark mode:

- **/** (Home), **/cookbooks**, **/cookbooks/{id}**, **/pantry**, **/grocery**, **/ai**, **/prompt-builder**, **/recipes/{id}**, **/recipes/{id}/edit**, **/recipes/{id}/cook**, **/recipes/{id}/made**, **/profile**

Verify:
- Dark-mode toggle in TopBar flips every surface to cocoa-dark
- User-switcher dropdown opens, password prompt works on switch
- Admin "Manage users" dialog opens (CbDialogService now)
- Browser notification fires when a cooking-mode timer completes
- AI off → sidebar AI rows hide, Home Generate button hides, Pantry AI buttons hide, AI Chat + Prompt Builder routes show "AI is disabled" copy
- /design-sandbox 404s

## Pull-forward to next milestone

v1.2 is the terminal milestone for the visual replatform — there is no Phase 8. v1.3 (TBD) will pick up:

- **FUTURE-V1.1-01..05** — per-step temperature, TagsJson → relational, LegacyRecipeProjector deletion, snapshot test on system prompt, README "Recipe Format" section (deferred from v1.1 Phase 4)
- **FUTURE-13** — Smart pantry-match algorithm for HOME-02 (replaces deterministic stub)
- **FUTURE-14** — User-facing accent variant picker (terracotta / sage)
- **DEFERRED-PROF-AIPROMPT** — Profile-side editor for `UserProfile.AiSystemPromptTemplate` (the AiChat assistant-instructions panel removed in Plan 07-04 needs a new home)
- **FUTURE-Recently-Cooked** — RecipeMade log entity to wire HOME-04 + RecipeView last-cook callout + RecipeView made-count to live data
- **FUTURE-01..12** — security hardening, telemetry, additional format fields

## Phase deliverables

**Migrated surfaces:** 6 (Cookbooks list+detail, Pantry, Grocery, AI Chat, Prompt Builder, Profile)
**Migrated dialogs:** ~18 (cookbook 7 + pantry 5 + grocery 2 + profile 4)
**A11y fixes:** 13 (focus rings, ARIA roles, aria-labels on icon-only buttons)
**Dependency removed:** MudBlazor 8.15.0 + MudBlazor.Services
**Files deleted:** DesignSandbox.razor, SampleDialogContent.razor
**Repo grep verification:** `Mud[A-Z]` returns 0 hits across src/ and tests/
**Build:** 0 warnings, 0 errors
**Tests:** 196/196 (baseline preserved across all 7 plans)

---
*Phase: 07-remaining-surfaces-accessibility-mudblazor-strip*
*Completed: 2026-04-27*
