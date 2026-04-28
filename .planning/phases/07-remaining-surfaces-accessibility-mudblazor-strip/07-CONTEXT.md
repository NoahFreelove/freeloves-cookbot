# Phase 7: Remaining surfaces + accessibility + MudBlazor strip - Context

**Gathered:** 2026-04-27
**Status:** Ready for planning
**Mode:** `--auto` — Claude picked recommended defaults from the design handoff

<domain>
## Phase Boundary

Migrate the six remaining application surfaces — Cookbooks (list + detail), Pantry, Grocery (mobile-first), AI Chat (live recipe canvas), Prompt Builder, Profile — to the Phase 5 atom system. Migrate the ~14 remaining MudBlazor dialogs (`AddUserDialog`, `AdminManageUsersDialog`, `PasswordPromptDialog`, `CookbookFormDialog`, `ShareCookbookDialog`, `ImportCookbookDialog`, `PasteRawTextDialog`, `SaveRecipeDialog`, `CookbookReferenceDialog`, `AddPantryItemDialog`, `AddGroceryListItemDialog`, `NewGroceryListDialog`, `CreateSharedPantryDialog`, `ManagePantryMembersDialog`, `AiPopulatePantryDialog`, `AiStandardizePantryDialog`, `SectionDropConfirmationDialog`, `SharedKeysDialog`) to use `<CbDialog>` + `CbDialogService`. Run the cross-cutting accessibility audit. Delete the `MudBlazor` and `MudBlazor.Services` package references and remove `AddMudServices()` from `Program.cs` and the `@using MudBlazor` from `_Imports.razor`. Delete the `/design-sandbox` route. Phase 5's MudBlazor coexistence is done.

**This phase delivers the full visual replatform.** When Phase 7 ships, `dotnet build` succeeds with zero MudBlazor in the dependency graph and a repo-wide `grep "Mud[A-Z]"` in `src/CookBot.Web/` returns zero hits.

**In scope (27 reqs):**
- CB-01, CB-02 — Cookbook list + detail
- PA-01..PA-04 — Pantry
- GR-01..GR-04 — Grocery (mobile-first)
- AIC-01..AIC-05 — AI Chat (live recipe canvas)
- PB-01..PB-03 — Prompt Builder
- PROF-01, PROF-02 — Profile
- A11Y-01..A11Y-04 — accessibility audit (cross-cutting)
- MIG-01, MIG-02, MIG-03 — terminal MudBlazor strip

**Not in scope:**
- Anything not in Phase 7 reqs (per-step temp, smart pantry-match, accent picker UI, etc. — all carry forward as FUTURE-* per REQUIREMENTS.md)

</domain>

<decisions>
## Implementation Decisions

### A. Per-surface migrations (CB / PA / GR / AIC / PB / PROF)

- **D-01:** Each surface migrates as one atomic plan. Markup is fully rewritten against atoms; non-presentational logic preserved verbatim. AI-off contract verified at every relevant surface (Pantry AI populate/standardize buttons, AI Chat + Prompt Builder route contents).
- **D-02:** Cookbook list/detail (CB-01..02) — list shows 3-col grid with collage-thumbnail cookbook cards (3×2 striped tiles tinted by accent); detail shows hero with share/PDF/export action row + member chips + recipe row list. Existing PDF export (`CookbookPdfService` via QuestPDF) preserved.
- **D-03:** Pantry (PA-01..04) — 4-tile summary strip (In stock / Running low / Expiring / Out, each with colored vertical bar), categorized stock cards, status `<CbBadge>` per row. AI populate / AI standardize buttons hidden when AI off.
- **D-04:** Grocery (GR-01..04) — mobile-first layout that works on desktop too. Aisle-categorized sections with 24px circle checkboxes, sticky bottom "Add item" button.
- **D-05:** AI Chat (AIC-01..05) — 380px chat rail + flex canvas. Recipe canvas pulls from canonical `RecipeDocument` produced by `IAiRecipeGenerator` (v1.1 Phase 2). Streaming animations via CSS keyframes.
- **D-06:** Prompt Builder (PB-01..03) — 320px config rail + flex preview with dark mono `<pre>` panel. Sources from `RecipeSchemaDocumentationProvider` (v1.1 AI-05).
- **D-07:** Profile (PROF-01..02) — settings cards (Display name / API key / AI toggle / Theme & density / Equipment / Dietary preferences). Density toggle ships here per Phase 5 D-05. SharedKeysDialog migrates to `CbDialogService`.

### B. Dialog migrations (~18 dialogs)

- **D-08:** Each existing dialog component (`AddUserDialog.razor`, `AdminManageUsersDialog.razor`, etc.) gets its content wrapped in `<CbDialog>` slots (Header/Body/Footer) instead of `<MudDialog>`. The launching code at the call site (`IDialogService.ShowAsync<TDialog>(...)`) migrates to `CbDialogService.ShowAsync<TDialog>(...)` — the API mirrors MudBlazor's surface, so the call sites are mechanical replacements.
- **D-09:** `MudButton`, `MudTextField`, `MudSelect`, `MudCheckBox`, `MudRadio`, `MudIcon` etc. inside dialog content are replaced with their Cb equivalents.
- **D-10:** Dialog migrations are bundled by call-site rather than per-dialog (e.g., all cookbook-related dialogs migrate within the CB plan; pantry dialogs migrate with PA).

### C. Accessibility audit (A11Y-01..04)

- **D-11:** A dedicated plan runs the cross-cutting a11y audit after all surface migrations are done. Verifies: visible focus rings everywhere; keyboard-only navigation across all 9 surfaces with no mouse traps; ARIA roles on atoms (button/dialog/menu/list/progressbar/status/radio/checkbox/switch); WCAG AA contrast on warm-cream and cocoa-dark themes; manual smoke pass in dark mode for every surface.
- **D-12:** A small set of fixes lands in this plan (focus ring CSS rules, missing ARIA labels, etc.). Most a11y is already correctly handled by the atom system; audit catches edge cases.

### D. Terminal MudBlazor strip (MIG-01..03)

- **D-13:** Final plan in Phase 7. Removes `MudBlazor` and `MudBlazor.Services` from `CookBot.Web.csproj`; removes `@using MudBlazor` from `_Imports.razor`; removes `AddMudServices()` from `Program.cs`; removes `<MudThemeProvider>` / `<MudPopoverProvider>` / `<MudDialogProvider>` / `<MudSnackbarProvider>` from `MainLayout.razor` (they no longer have any consumers).
- **D-14:** Removes the `IDialogService` injection from `TopBar.razor` (the Alternative A carve-out from Phase 5) and migrates the password prompt + admin manage users to `CbDialogService` calls.
- **D-15:** Removes the static MudBlazor link tags from `App.razor` (`MudBlazor.min.css`, `MudBlazor.min.js`) and `MudBlazor.web.config` lookups if any.
- **D-16:** Deletes `Components/Pages/DesignSandbox.razor` and its route (sandbox was the Phase 5 verification surface; production doesn't need it).
- **D-17:** Repo-wide grep verification: `grep -r "Mud[A-Z]" src/CookBot.Web/ --include="*.razor" --include="*.cs"` returns ZERO hits (excluding any deliberate string in comments — those should be cleaned up too).
- **D-18:** Final `dotnet build` clean + `dotnet test` baseline preserved. Manual smoke pass on every surface in light + dark.

### E. Plan shape

- **D-19:** Recommended plan split:
  - Plan 07-01: Cookbooks (list + detail) + cookbook dialogs (CookbookFormDialog, ShareCookbookDialog, ImportCookbookDialog, CookbookReferenceDialog, SaveRecipeDialog, PasteRawTextDialog, SectionDropConfirmationDialog) → CB-01, CB-02
  - Plan 07-02: Pantry + pantry dialogs (AddPantryItemDialog, CreateSharedPantryDialog, ManagePantryMembersDialog, AiPopulatePantryDialog, AiStandardizePantryDialog) → PA-01..04
  - Plan 07-03: Grocery + grocery dialogs (AddGroceryListItemDialog, NewGroceryListDialog) → GR-01..04
  - Plan 07-04: AI Chat + Prompt Builder → AIC-01..05, PB-01..03
  - Plan 07-05: Profile + profile dialogs (AddUserDialog, AdminManageUsersDialog, PasswordPromptDialog, SharedKeysDialog) → PROF-01..02
  - Plan 07-06: Accessibility audit + small fixes → A11Y-01..04
  - Plan 07-07: Terminal MudBlazor strip + sandbox cleanup → MIG-01..03
- **D-20:** Plans 07-01..07-05 are parallel-safe but executor serializes by default. Plan 07-06 must run after 07-01..07-05. Plan 07-07 MUST run last.

</decisions>

<canonical_refs>
- `.planning/design-handoff/project/screens/cookbook-list.jsx`, `pantry.jsx`, `grocery-phone.jsx`, `ai-chat.jsx`, `prompt-builder.jsx`
- `.planning/REQUIREMENTS.md` — CB/PA/GR/AIC/PB/PROF/A11Y/MIG
- `.planning/ROADMAP.md` — Phase 7 SC#1..SC#5
- `.planning/phases/05-foundation-design-tokens-atoms-shell-dialogs/05-PHASE-SUMMARY.md`
- `.planning/phases/06-marquee-surfaces-home-cooking-mode-recipe-view-recipe-editor/06-PHASE-SUMMARY.md`
- `src/CookBot.Web/Components/Pages/CookbookList.razor`, `CookbookDetail.razor`, `PantryView.razor`, `GroceryListView.razor`, `AiChat.razor`, `PromptBuilder.razor`, `EditProfile.razor`
- `src/CookBot.Web/Components/Pages/AddUserDialog.razor` and the rest of the dialog list
- `src/CookBot.Web/Services/CbDialogService.cs`, `CbToastService.cs`
- `src/CookBot.Web/CookBot.Web.csproj`, `Program.cs`, `Components/_Imports.razor`, `Components/Layout/MainLayout.razor`, `Components/App.razor`, `Components/Pages/DesignSandbox.razor`
- `src/CookBot.Application/AI/IAiRecipeGenerator.cs` (used by AI Chat canvas)
- `src/CookBot.Application/AI/RecipeSchemaDocumentationProvider.cs` (used by Prompt Builder)

</canonical_refs>

<code_context>
- Each existing surface page is ~150-700 lines; migrations are mostly markup rewrites with preserved `@code` blocks
- ~18 existing dialogs to migrate; each is small (50-150 lines)
- Existing `cookbot-design.css` from Phase 5 is the canonical stylesheet — Phase 7 may append rules for new surface-specific patterns (collage thumbnails, summary tiles, aisle sections) but doesn't introduce new tokens

</code_context>

<specifics>
- The cookbook-list collage thumbnail is 3×2 grid of striped tiles tinted by cookbook accent — implemented via inline gradients matching `cookbook-list.jsx`
- The grocery list mobile-first layout works on desktop without responsive breakpoint (just narrower main column on mobile)
- The AI Chat streaming caret + drafting pulse are CSS keyframe animations; SSE streaming under the hood is unchanged

</specifics>

<deferred>
- Photo upload backend (still FUTURE)
- Smart pantry-match algorithm (FUTURE-13)
- User-facing accent variant picker (FUTURE-14)
- bUnit tests for migrated surfaces (defer per Phase 5 D-28 pattern)

</deferred>
