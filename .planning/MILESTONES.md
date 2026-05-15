# Milestones — FreelovesCookBot

Historical record of shipped milestones. Each entry summarizes scope, stats, and accomplishments; full details live in the per-milestone archive under `.planning/milestones/`.

| Milestone | Shipped | Phases | Plans | Requirements | Tag | Archive |
|-----------|---------|--------|-------|--------------|-----|---------|
| v1.0 (existing app, pre-GSD) | pre-2026-04-25 | — | — | — (codebase mapped in `.planning/codebase/`) | — | — |
| v1.1 Canonical Format & AI Conformance | partial (Phases 1+2 only, 2026-04-26) | 2 / 4 (3 absorbed into v1.2; 4 deferred to v1.3+) | 9 / TBD | 30 of 46 (Phases 1+2 reqs) | — (paused, not tagged) | — |
| **v1.2 UI Redesign** | **2026-04-27** | **3** | **16** | **75 / 75** | **`v1.2`** | [`milestones/v1.2-ROADMAP.md`](milestones/v1.2-ROADMAP.md) · [requirements](milestones/v1.2-REQUIREMENTS.md) · [audit](milestones/v1.2-MILESTONE-AUDIT.md) |

---

## v1.2 — UI Redesign

**Shipped:** 2026-04-27 (executed); 2026-05-01 audit; 2026-05-15 closed.
**Phases:** 5–7 (16 plans total).
**Requirements:** 75 / 75 satisfied.
**Tag:** `v1.2`.
**Audit status:** `tech_debt` — all requirements / integration / E2E flows pass; 5 minor warnings (4 fixed at close, 1 deferred as `FUTURE-15`).

### Delivered

Wholesale replacement of MudBlazor with a custom Razor component system matching the Claude Design handoff bundle (`.planning/design-handoff/`). Warm-cream surfaces / cocoa ink / dialed-back orange accent across 9 routable surfaces (Home, CookingMode, RecipeView, RecipeEditor, CookbookList, CookbookDetail, PantryView, GroceryListView, AiChat, PromptBuilder, Profile). The Recipe Editor work absorbed the entirety of v1.1 Phase 3 (chip composer) so EDITOR-01..07 ship in the new component system rather than being authored twice. UI surfaces consume canonical `RecipeDocument` (v1.1 Phase 1 + 2) directly — no legacy column projection.

### Key accomplishments

1. **Design tokens + atom library (Phase 5)** — 24 requirements in a single global stylesheet (`wwwroot/css/cookbot-design.css`) + 17 atom components (CbButton, CbChip, CbCard, CbStat, CbEyebrow, CbBadge, StripedPlaceholder, CbToggle, CbCheckbox, CbRadio, CbInput, CbTextarea, CbSelect/CbOption, CbDialog/CbDialogHost, CbToastHost, CbDropdown/CbDropdownItem, ConfirmDialog) + 36 outline icons (single `Icon.razor` component) + new shell (MainLayout/Sidebar/TopBar/NavRow).
2. **Pantry-aware Home dashboard + adaptive Cooking Mode + editorial Recipe View + chip-composer Recipe Editor (Phase 6)** — the four marquee surfaces. Home leads with "Tonight from your pantry"; Cooking Mode adapts between 224px timer hero and 52px step hero with always-on ingredient rail; Recipe View ships 64px display title with hanging accent numerals + sticky scaled-ingredients sidebar; Recipe Editor introduces the chip composer with explicit keyboard-navigable ingredient picker, inline non-modal timer-suggestion banner, and immutable-id reorder (absorbs v1.1 EDITOR-01..07).
3. **Cookbooks + Pantry + Grocery + AI Chat + Prompt Builder + Profile (Phase 7)** — the remaining six surfaces. ~22 dialog content components migrated to the `<CbDialog>` slot pattern. AI Chat's recipe canvas binds to canonical `RecipeDocument` from `IAiRecipeGenerator` — POLISH-01 invariant preserved (no extractor revival).
4. **Accessibility audit (Phase 7 / Plan 07-06)** — 9-surface walkthrough, 13 targeted fixes: unified `:focus-visible` 2px accent outline; ARIA roles on atoms (button/dialog/menu/list/progressbar/status/radio/checkbox/switch); WCAG AA contrast on warm-cream + cocoa-dark; per-surface dark-mode smoke pass. Recorded in `07-06-AUDIT.md`.
5. **MudBlazor wholesale removed (Phase 7 / Plan 07-07)** — package deleted from csproj; `AddMudServices()` removed from `Program.cs`; `@using MudBlazor` removed from `_Imports.razor`; all four MudBlazor providers removed from `MainLayout.razor`; static Mud CSS/JS tags removed from `App.razor`; `DesignSandbox.razor` + `SampleDialogContent.razor` deleted. Repo-wide `grep "Mud[A-Z]"` returns zero hits.
6. **Post-ship slices (07-08 + 07-09)** — 12 manual-smoke bug fixes across 6 atomic commits + RecipeMade log entity with `IRecipeMadeService` (lights up RV-04 last-cook callout + Home recently-cooked grid + made-count) + Home live timer band.

### Stats

- 179 files changed in the squashed milestone commit (`a084783`)
- +27,722 / -3,086 lines total (+12,069 / -2,796 in C# / Razor / CSS / JS — 99 files)
- 4 new bUnit test files (PasteFlowTests, RecipeChipComposerTests, StepSectionToggleTests, TimerSuggestionTests)
- 196 / 196 tests passing under `dotnet test --filter "Category!=RequiresApiKey"`
- `dotnet build` clean (0 warnings, 0 errors)
- Timeline: 2026-04-27 (execution) → 2026-05-01 (audit) → 2026-05-15 (close)

### Known deferred items at close

11 items recorded in `.planning/milestones/v1.2-MILESTONE-AUDIT.md` `tech_debt` section, plus 14 carry-forward `FUTURE-*` items in the archived requirements doc. The v1.3 phase candidate (`.planning/v1.3-PHASE-CANDIDATE-recipe-photos.md`) drafts the first format-driven slice for v1.3 (paste-URL recipe photos).

### Inheritance

v1.2 absorbed v1.1 EDITOR-01..07 (built in Cb atoms, not MudBlazor). v1.1 Phase 4 (`FEATURE-V2-*` per-step temperature + `POLISH-03/04/05/07`) was deferred to v1.3+ as `FUTURE-V1.1-01..05`. v1.1 FUTURE-10 (MudBlazor 9.x upgrade) is obsolete — MudBlazor stripped entirely.

---

## v1.1 — Canonical Format & AI Conformance (PARTIAL)

**Status:** Paused mid-flight. Phases 1 (Canonical Format Foundation) + 2 (AI Structured Output & Conformance) shipped 2026-04-25 / 2026-04-26. Phase 3 (Editor UX chip composer) was absorbed into v1.2 Phase 6 (Recipe Editor). Phase 4 (per-step temperature + cleanup) was deferred to v1.3+.

**No tag created** — milestone never reached a coherent shipping boundary; the work that did ship was rolled into the v1.2 release.

### What did ship under v1.1

- Canonical `RecipeDocument` v2 — single source of truth for YAML wire / JSON export / DB column / AI prompt
- `JsonRecipeSerializer` + `IRecipeFormatParser` rewrite + `RecipeUpcasterChain` (V1 → V2)
- `LegacyRecipeProjector` + EF migration backfilling `Recipe.CanonicalDocumentJson` for every existing row
- `IDatabaseBackupService` with last-3-backup retention
- `PromptBuilderService` consolidated against single `RecipeSchemaDocumentationProvider` — opt-out clause removed
- Anthropic structured-output (`output_config.format`) transport with 2-retry repair loop
- `SecretRedactor` (API-key redaction in error surfaces) + `PromptInjectionGuard` (XML-tagged user content)
- `IAiRecipeGenerator` orchestrator + `RecipeCookingAiContext` wrap

Full details in `.planning/phases/01-canonical-format-foundation/` and `.planning/phases/02-ai-structured-output-conformance/` (preserved in place; consumed by v1.2 marquee surfaces).

### v1.1 carry-forwards into v1.3+

- FUTURE-V1.1-01..05: per-step temperature, tags relational, LegacyRecipeProjector cleanup, prompt snapshot test, README format section
- FUTURE-01..09, FUTURE-11..12: encrypt-at-rest API key, token cost telemetry, format extensions (substitutions / equipment / doneness cues / source provenance), Schema.org export, USDA FDC nutrition, tool-use fallback, Cooklang export, per-sharer consent banner

---

## v1.0 — Pre-GSD existing app

The pre-GSD app shipped before this planning system was adopted. Codebase mapped in `.planning/codebase/` (7-doc audit: ARCHITECTURE, STACK, STRUCTURE, CONVENTIONS, TESTING, INTEGRATIONS, CONCERNS). Validated requirements from this era live in `PROJECT.md` Validated section.

---

*MILESTONES.md created at v1.2 close (2026-05-15). Future entries will be appended above the v1.2 block.*
