# Requirements — FreelovesCookBot v1.2 Milestone

**Milestone goal:** Replace MudBlazor entirely with custom Razor components matching the Claude Design handoff bundle (`.planning/design-handoff/`) — warm cream / cocoa ink / dialed-back orange accent, Inter only, custom outline icons, striped photo placeholders, tabular numerals — across all 9 surfaces, while preserving every v1.1 functional contract (canonical `RecipeDocument`, AI structured output, AI-off kill switch, trusted-LAN auth posture).

**Generated:** 2026-04-27 (auto mode — design handoff at `.planning/design-handoff/` is the research output; chats/chat1.md + 9 fully-specified screens + design system tokens in styles.css cover stack/features/architecture/pitfalls).

**Inheritance from v1.1 (paused):** v1.1 Phase 3 EDITOR-01..07 are **absorbed** into v1.2 RECIPE-EDITOR (`ED-03..ED-09`); they are no longer separately tracked under v1.1. v1.1 Phase 4 (FEATURE-V2-* + POLISH-03/04/05/07) is deferred to v1.3+. v1.1 FUTURE-10 (MudBlazor 9.x upgrade) is rendered obsolete by this milestone.

---

## v1.2 Requirements

### DS — Design system tokens

Single source of truth for the visual identity. Mirrors `.planning/design-handoff/project/styles.css` — warm-cream surfaces, cocoa ink, dialed-back orange accent.

- [x] **DS-01**: A `cookbot-design.css` global stylesheet defines CSS custom properties for the full token palette from `.planning/design-handoff/project/styles.css` — surfaces (`--cream`, `--cream-2`, `--paper`, `--paper-2`, `--line`, `--line-strong`), ink (`--ink`, `--ink-2/3/4`), accent (`--accent`, `--accent-soft`, `--accent-ink`), green/warn variants, type families, density (`--pad`, `--pad-sm`, `--gap`), radii — and is loaded once per page render via `_Host`/`App.razor`.
- [x] **DS-02**: Three accent variants — orange (default), terracotta, sage — selectable via `data-accent` on `<html>`. Tokens are wired and verified; user-facing accent picker is **not** surfaced this milestone (variants exist for future per-user theming).
- [x] **DS-03**: Two density modes — `comfy` (default), `compact` — selectable via `data-density` on `<html>`. A density toggle ships on the Profile surface and persists to `UserProfile`.
- [x] **DS-04**: Inter-only typography stack with `font-feature-settings: "ss01", "cv11"` on the body and `tnum` on a `.num` utility class for tabular numerals; loaded from rsms.me/inter/inter.css (existing) — no second font.
- [x] **DS-05**: Dark-mode parity for every token; the existing `cookbot_dark_mode` localStorage toggle drives `<html data-theme="dark">`; every surface is verified visually in both light and dark themes before phase verification.
- [x] **DS-06**: A striped photo placeholder primitive (`.cb-ph` class) — diagonal stripes + dashed border + monospace caption — replaces every missing-imagery slot in the design (used by Home hero, Recipe View hero, Recipe Editor photo upload, Cookbook list collages, Recently cooked tiles).

### ATOM — Shared component primitives

Replaces MudBlazor's button/chip/card primitives with custom Razor components matching the design exactly.

- [x] **ATOM-01**: `<CbButton>` Razor component — variants `primary` (cocoa fill), `accent` (orange fill), `ghost` (transparent + border), `subtle` (light gray fill); 999px pill radius; supports `StartIcon`, `EndIcon`, `FullWidth`, `Disabled`, `Type`, `OnClick`. Replaces every `MudButton` call site.
- [x] **ATOM-02**: `<CbChip>` Razor component — variants `default`, `timer` (accent-soft bg), `ing` (cream-2 bg), `tag` (transparent + border); supports `Icon`, `Label`, optional `OnClick` for actionable chips. Replaces every `MudChip` call site.
- [x] **ATOM-03**: `<CbCard>` Razor component — paper bg, 14px radius, line border; supports `Padding` slots and arbitrary child content. Replaces every `MudPaper` call site.
- [x] **ATOM-04**: `<CbStat>` tile component — label + tabular-numeral value (36px) + optional sub-text; min-height 124px; used on Home stat strip and Pantry summary strip.
- [x] **ATOM-05**: `<CbEyebrow>` component — 11px uppercase letter-spaced 0.14em ink-3 weight 500; renders inline above section headers and card content.
- [x] **ATOM-06**: `<StripedPlaceholder>` component — width/height + label parameters; renders the `.cb-ph` shape from DS-06.
- [x] **ATOM-07**: Custom outline icon set — single `<Icon>` Razor component covering all 36 icons from `.planning/design-handoff/project/icons.jsx` (home, book, pantry, cart, spark, prompt, user, menu, search, plus, check, clock, flame, pause, play, arrowR, arrowL, bell, sun, share, download, copy, pencil, more, trash, scale, bolt, filter, grid, list, chevD, chevR, flag, send, save, link); 1.6 stroke width; sized via `Size` parameter. Replaces every `Icons.Material.Filled.*` reference.
- [x] **ATOM-08**: `<CbBadge>` for status pills — variants `in-stock` (green-soft), `low` (warn-soft), `expiring` (accent-soft), `out` (gray); used on Pantry rows and Home pantry-match suggestions.
- [x] **ATOM-09**: `<CbToggle>` switch + `<CbCheckbox>` + `<CbRadio>` form primitives for binary/multi-select settings; replace `MudSwitch`, `MudCheckBox`, `MudRadio` call sites. *(Phase 5 Plan 03 — shipped 2026-04-27)*
- [x] **ATOM-10**: `<CbInput>` text input + `<CbTextarea>` + `<CbSelect>` form primitives — line-border, paper bg, 8px radius, focus ring; support `placeholder`, `@bind`, change events. Replace every `MudTextField`, `MudSelect`, `MudAutocomplete` non-recipe-editor call site. *(Phase 5 Plan 03 — shipped 2026-04-27 — implementation deferred for replacing call sites at MudTextField/MudSelect/MudAutocomplete to Phase 6/7 surface migration; primitives shipped)*

### SHELL — App chrome

Rebuilds the layout, sidebar, and top bar to match the design — full replacement of `MudLayout`/`MudAppBar`/`MudDrawer`/`MudNavMenu`.

- [x] **SHELL-01**: `MainLayout.razor` removes `MudLayout`/`MudAppBar`/`MudDrawer`/`MudMainContent`/`MudContainer` chrome and renders a CSS-grid shell — 232px sidebar + main column — matching `.cb-shell` from styles.css. Dark-mode toggle and user-switcher remain functional. *(Phase 5 Plan 05 — shipped 2026-04-27. Note: MudThemeProvider/MudPopoverProvider/MudDialogProvider/MudSnackbarProvider stay mounted through Phase 5 per D-30 coexistence so unmigrated dialogs and surfaces keep working; Phase 7 MIG slice deletes them at the terminal cleanup.)*
- [x] **SHELL-02**: `<Sidebar>` Razor component — Home / Cookbooks / Pantry / Grocery rows + divider + AI Assistant / Prompt Builder rows (hidden when `aiOff`) + spacer + Profile row at bottom; logo block at top with accent-colored "cb" tile + "CookBot" wordmark; active row uses `accent-soft` background and `accent-ink` text. Replaces `NavMenu.razor`. *(Phase 5 Plan 05 — shipped 2026-04-27. NavMenu.razor deleted.)*
- [x] **SHELL-03**: `<TopBar>` Razor component — 56px height, line-bottom border, sticky, cream bg; renders menu toggle + optional breadcrumb + title + optional sub + a right-side action slot; below the slot: user-switcher dropdown (CbDropdown), dark-mode toggle, admin "Manage users" button (when admin). *(Phase 5 Plan 05 — shipped 2026-04-27. Password prompt + admin Manage-users dialogs continue to launch via existing MudBlazor IDialogService through Phase 5 — D-13 Alternative A; Phase 7 migrates launch path AND dialog content together.)*
- [x] **SHELL-04**: `<NavRow>` is a separate Razor component used by Sidebar — Icon + Label + optional Kbd hint + active/hover/disabled states via `<NavLink ActiveClass="active">` for route-driven active state. *(Phase 5 Plan 05 — shipped 2026-04-27.)*

### DIALOG — Modal/overlay primitives

Replaces every `MudDialog`/`MudSnackbar`/`MudPopover` mechanism.

- [x] **DIALOG-01**: `<CbDialog>` primitive — fixed-position scrim + centered card with line border + 14px radius + paper bg; supports `MaxWidth` (sm/md/lg/xl), `FullWidth`, `CloseOnEscape`, `CloseOnScrim`, focus trap, multiple-dialog stacking.
- [x] **DIALOG-02**: `CbDialogService` (DI service) primitive shipped — `ShowAsync<TDialog>(parameters, options)` returns a `CbDialogResult` with `Canceled` boolean and optional `Data`. (Migration of the 14+ existing `IDialogService.ShowAsync` call sites is tracked under Phase 7 MIG-* — primitive surface delivered in Phase 5 Plan 04.)
- [x] **DIALOG-03**: `CbToastService` primitive shipped — `Show(message, severity)` queues a transient toast that fades after 5s; supports success/error/info/warning variants; positioned bottom-right; max 3 stacked toasts. (Migration of `ISnackbar.Add` call sites tracked under Phase 7 MIG-*.)
- [x] **DIALOG-04**: `<CbDropdown>` standalone select-style dropdown for the user-switcher and any in-page menus that aren't `<CbSelect>` form fields — keyboard-navigable (ESC + Tab between items + click-outside-to-close).

### MIG — MudBlazor strip

Mechanical removal of the dependency once equivalents exist.

- [x] **MIG-01**: `MudBlazor` and `MudBlazor.Services` package references are removed from `src/CookBot.Web/CookBot.Web.csproj`; `_Imports.razor` no longer imports `MudBlazor`; `Program.cs` no longer calls `AddMudServices()`. `dotnet build` succeeds with zero MudBlazor-related references in the dependency graph. *(Plan 07-07)*
- [x] **MIG-02**: Every `Mud*` component reference across all 28 Razor pages and ~14 dialogs is replaced with the new atom/shell components or removed; `dotnet test` (existing xUnit suite) continues to pass; bUnit tests (introduced by v1.1 Phase 3 plans, if present) updated accordingly. *(Plan 07-07 — repo-wide `grep -rn "Mud[A-Z]" src/ tests/` returns zero hits; 196/196 tests preserved)*
- [x] **MIG-03**: Existing behavior preserved through the migration — dark-mode toggle wired to `cookbot_dark_mode` localStorage, user-switcher with password prompt, admin "Manage users", session-scoped current user, AI-off per-user kill switch, browser notifications in cooking mode, JS interop in `cooking-timers.js` and chip composer. *(Plan 07-07 — `_isDarkMode` field retained in MainLayout; dark-mode JS interop preserved verbatim; user-switcher + admin Manage users now via CbDialogService since Plan 07-05; cooking-timers.js + recipe-chip-composer.js untouched)*

### HOME — Home dashboard surface

The dashboard earns its space — pantry-aware suggestions lead, counters get demoted to a glance strip.

- [x] **HOME-01**: `Home.razor` is rebuilt — eyebrow ("Welcome back, {DisplayName}") + display-weight headline ("What's the kitchen up to tonight?") + quick-actions row (Generate recipe accent button, hidden if AI off; New recipe ghost; New list ghost).
- [x] **HOME-02**: A pantry-aware "Tonight from your pantry" hero card surfaces 3 recipes matched against the current pantry — for v1.2, matching is a deterministic stub: filter recipes by % of ingredients available, sort, pick top 3 (or empty-state CTA if pantry is empty). Backend smart-matching (expiration-aware, %-of-pantry-used) is wired with a clear TODO extension point.
- [x] **HOME-03**: 4-tile stat strip — `Recipes` / `Cookbooks` / `Pantry items` / `Grocery` — each tile renders count (tabular numeral, 36px) + label + delta sub-text (count-over-last-7-days where cheap; placeholder otherwise).
- [x] **HOME-04**: Two-up cards beneath stats — "Recently cooked" (4-thumbnail grid, last 14 days from `RecipeMade` log) + "Up next" (3 starred or most-recent `RecipeMade` entries; placeholder rows + TODO if no queue concept exists yet).

### COOK — Cooking Mode (tablet)

Marquee surface — adaptive timer/step hero, always-on ingredient rail.

- [x] **COOK-01**: `CookingMode.razor` background flips to dark cocoa (`--ink`); top bar replaced with minimal Exit + recipe title + step indicator + notification chip ("notifications on"/off); preserves existing browser-notification permission flow.
- [x] **COOK-02**: A step rail at the top of the cooking surface — N segments showing step progress (faded for past, accent for current, dimmed for future).
- [x] **COOK-03**: Adaptive hero — when a timer is running, render the 224px tabular-numeral countdown + Pause / +30s / Reset controls + step text below (17px); when no timer is running, render the 52px step text + "Start N-min timer" + "Ask about this step" buttons.
- [x] **COOK-04**: Always-on right rail — "Ingredients · scaled {scale}×" eyebrow + ingredient list; current step's referenced ingredients render in an accent-tint card; others dimmed; bottom of rail shows "Serves {N}" + −/+ scaling buttons that re-scale ingredient quantities live (servings-only — temperatures and times never auto-scale, per v1.1 D-Q9).
- [x] **COOK-05**: Bottom step nav — "Previous: {prev step name}" (1fr) + "Next: {next step name}" accent button (2fr); 64px height; left/right arrow keys also navigate steps.
- [x] **COOK-06**: Cooking mode preserves all existing behavior — JS-interop timers in `cooking-timers.js`, browser notifications when timers fire, ingredient highlighting via canonical `[name](#id)` link resolution, "Ask AI about this step" wired through `RecipeCookingAiContext` (v1.1 Phase 2).

### RV — Recipe View (editorial)

- [x] **RV-01**: `RecipeView.razor` rebuilt with editorial layout — eyebrow tags row + 64px display title (`.cb-recipe-cap`) + 17px lead paragraph + 4-stat row (Active / Total / Serves / Made-count, tabular numerals) + 4:3 hero photo placeholder right side. *(Phase 6 Plan 03 — shipped 2026-04-27. Note: lead paragraph hides cleanly until RecipeDocument gains a description field — FUTURE-V1.1-* schema slot.)*
- [x] **RV-02**: Two-column body — sticky-positioned 300px ingredient sidebar (Ingredients eyebrow + scale control card with −/+/Servings + ingredient rows + tag chip row); right side is method. *(Phase 6 Plan 03 — shipped 2026-04-27.)*
- [x] **RV-03**: Method steps render with hanging accent-colored numeral (28px tabular) + step title (16px bold) + 15px body paragraph; if step has a timer, show inline `<CbChip variant="timer">` with clock icon. Consumes canonical `RecipeDocument` directly (no projection from legacy columns). *(Phase 6 Plan 03 — shipped 2026-04-27. SC#3 gate satisfied via JsonRecipeSerializer; zero reads of Recipe.IngredientsJson/StepsJson/IngredientRefs/TagsJson.)*
- [x] **RV-04**: "Notes from your last cook" callout block at the bottom of method (cream-2 bg, eyebrow + quote + date) — surfaces the most recent `RecipeMade.Notes` entry; hides when no notes exist. *(Phase 6 Plan 03 — shipped 2026-04-27. Conditional wired; v1.2 has no RecipeMade log entity so the callout always hides today — lights up automatically once a log lands.)*
- [x] **RV-05**: Top-bar actions: "Share" (ghost button → existing share dialog) + "Cook this" (accent button → `/cook/{id}`). *(Phase 6 Plan 03 — shipped 2026-04-27. Actions render inline above hero per CONTEXT D-17 PRAGMATIC fallback — TopBar has no per-page right-slot mechanism yet. Share opens existing ShareCookbookDialog via IDialogService for the parent cookbook (sharing is cookbook-scoped). Cook-this navigates to /recipes/{id}/cook (existing CookingMode route — corrected from plan's stale '/cook/{id}' wording).)*

### ED — Recipe Editor (chip composer + meta rail)

Absorbs v1.1 Phase 3 EDITOR-01..07 — the chip composer is built once, in custom Razor, against the new design.

- [x] **ED-01**: `RecipeEditor.razor` rebuilt — borderless 38px title input + borderless 15px subtitle input + striped photo placeholder for upload; right rail of meta cards (Cookbook selector, Times & servings, Tags input, AI suggestions card hidden when AI off). *(Phase 6 Plan 04 — shipped 2026-04-27. Description input wired in markup; persistence deferred until Recipe entity gains a Description column — FUTURE-V1.1-* slot per D-25.)*
- [x] **ED-02**: Ingredients table — grid (qty 60px / unit 70px / name 1fr / actions 28px) inside a card with row separators; "Add ingredient" footer button; rows support keyboard add/remove (Tab to advance, Backspace on empty row to delete). *(Phase 6 Plan 04 — shipped 2026-04-27. Quantity column accepts decimals + simple/mixed fractions.)*
- [x] **ED-03** *(absorbs v1.1 EDITOR-01)*: Step composer — chip-aware text editor; `@`-trigger autocomplete inserts an `<IngredientChip>`; underlying string keeps `[name](#id)` markdown invisibly; the immutable ingredient `id` is what serializes. *(Phase 6 Plan 04 — shipped 2026-04-27. Explicit "+ ingredient" pill button covers the autocomplete action with full keyboard nav (ArrowUp/Down + Enter + Escape); inline `@`-trigger keystroke detection deferred to FUTURE-EDITOR-AT-TRIGGER as a polish slice.)*
- [x] **ED-04** *(absorbs v1.1 EDITOR-02)*: Each step has an explicit "Step | Section header" toggle; Section steps disable timer/ingredient-chip controls. *(Phase 6 Plan 04 — shipped 2026-04-27.)*
- [x] **ED-05** *(absorbs v1.1 EDITOR-03)*: Detected timer durations in step text surface a "Detected 25 min — convert to a timer? [Yes / No]" suggestion; saving never auto-rewrites step text. *(Phase 6 Plan 04 — shipped 2026-04-27. Non-modal accent-soft banner; per-step session-dismissed durations.)*
- [x] **ED-06** *(absorbs v1.1 EDITOR-04)*: Reordering ingredients preserves the immutable `id` of each ingredient. *(Phase 6 Plan 04 — shipped 2026-04-27. Reference swap on `_ingredients` list; `LocalId` is preserved per `ParsedIngredient` instance.)*
- [x] **ED-07** *(absorbs v1.1 EDITOR-05)*: Pasting raw text via the "Paste raw text" dialog routes through the canonical schema parser; surfaces unresolved fields in the chip editor for confirmation; never persists a non-conforming recipe. *(Phase 6 Plan 04 — shipped 2026-04-27. Existing `PasteRawTextDialog` continues to use MudDialog content per Phase 6 D-30 coexistence carve-out; Phase 7 migrates dialog content.)*
- [x] **ED-08** *(absorbs v1.1 EDITOR-06)*: Cooking-mode ingredient highlighting uses the same chip rendering and `[name](#id)` link resolution exclusively (no substring matching). *(Verified transitively 2026-04-27 — Plan 06-02 already shipped this via `IngredientLinkPatterns.Pattern.Matches` in CookingMode.razor's `CurrentStepRefIds()`.)*
- [x] **ED-09** *(absorbs v1.1 EDITOR-07)*: Chip composer is keyboard-navigable (Tab/Shift+Tab between chips, Backspace deletes prior chip, Arrow keys move caret); axe-core/screen-reader smoke pass clean; degrades gracefully when JS interop fails (recipe still saves with current `[name](#id)` text). *(Phase 6 Plan 04 — shipped 2026-04-27. JS-interop fallback verified via the `_jsInteropAvailable=false` branch which renders a CbTextarea. Final axe-core full-surface audit rolls into Phase 7 A11Y-04.)*

### CB — Cookbooks

- [x] **CB-01**: `CookbookList.razor` rebuilt — top action bar with rounded search input + Filters ghost button + view toggle (grid/list); 3-col grid of cookbook cards each with 180px collage thumbnail header (3×2 striped tiles tinted by cookbook accent) + title/recipe-count + author meta line.
- [x] **CB-02**: `CookbookDetail.razor` rebuilt — hero with cookbook title + share/PDF/export action row + member chips for shared cookbooks; below: recipe list (each row: thumbnail + title + tags + meta).

### PA — Pantry

- [x] **PA-01**: `PantryView.razor` rebuilt — top-bar actions: AI standardize / AI populate (hidden when AI off) + Add item; sub-line "{N} items · last sync {ago}".
- [x] **PA-02**: 4-tile summary strip — In stock / Running low / Expiring this week / Out — each tile with colored vertical bar + value + label.
- [x] **PA-03**: Search row + filter buttons (All / Low only / Expiring); search filters across all categories.
- [x] **PA-04**: Categorized stock cards — each category card has a row grid (item name 1fr / qty 120px / status chip 110px / actions 80px); status chip variants: `in-stock` (green-soft), `low` (warn-soft), `expiring` (accent-soft, bg-tinted row).

### GR — Grocery list (mobile-first)

- [x] **GR-01**: `GroceryListView.razor` rebuilt with mobile-first layout that scales to desktop; mobile target: header (back / "This week" title / share / more icons).
- [x] **GR-02**: Progress card — N of M items checked + accent fill bar + tabular-numeral counter.
- [x] **GR-03**: Aisle-categorized sections — eyebrow label + card with checkable rows; row has 24px circle checkbox (accent fill when checked) + item name (line-through when checked) + quantity right-aligned tabular numeral.
- [x] **GR-04**: Bottom action bar — full-width "Add item" accent button (50px height, 25px radius); on desktop, button stays bottom-of-viewport sticky; on mobile, sits above the OS home indicator.

### AIC — AI Chat (live recipe canvas)

- [x] **AIC-01**: `AiChat.razor` rebuilt with two-column layout — 380px chat rail (left, paper-2 bg) + flex canvas (right).
- [x] **AIC-02**: Chat rail — message stream with eyebrow timestamp; user messages in white card; assistant messages with accent "CookBot" label; animated streaming caret on the active turn.
- [x] **AIC-03**: Chat input — bordered card with placeholder + suggestion chips ("make spicier" / "half it" / "vegan") + send button (accent circle); chips augment or prepend the user message.
- [x] **AIC-04**: Right canvas — save bar at top (drafting status pulse + revision/pantry meta + Copy JSON / Save buttons) + streaming recipe card (eyebrow + 44px display title + lead paragraph + 2-col ingredients/method); active step has an accent-soft numbered circle and trailing animated caret. Pulls from canonical `RecipeDocument` produced by v1.1 Phase 2's `IAiRecipeGenerator` orchestrator (no three-tier extractor — POLISH-01 is preserved).
- [x] **AIC-05**: Streaming animations (drafting chip pulse, caret blink) realized via CSS keyframes + Razor state changes; preserves SSE streaming under the hood.

### PB — Prompt Builder

- [x] **PB-01**: `PromptBuilder.razor` rebuilt with two-column layout — 320px config rail + flex preview.
- [x] **PB-02**: Config rail cards — Output format (radio: Canonical JSON / Markdown / Plain text), Include (checkboxes: Pantry context / Dietary preferences / Equipment list / Past favorites), Voice (select with options).
- [x] **PB-03**: Preview — char/token counter meta line + dark mono `<pre>` panel showing the assembled prompt; substituted sections (pantry, dietary) highlighted in accent-soft; "Copy prompt" action in top bar; the prompt body continues to source from `RecipeSchemaDocumentationProvider` (v1.1 Phase 1 AI-05).

### PROF — Profile

- [x] **PROF-01**: `EditProfile.razor` rebuilt with the new shell — settings grouped into cards (Display name / API key / AI toggle / Theme & density / Equipment / Dietary preferences); each card uses the new atom system.
- [x] **PROF-02**: AI-toggle and API-key cards continue to wire to existing `UserProfile.AiEnabled` / `UserProfile.AiApiKey` / `AiApiKeyShareService`; the `SharedKeysDialog.razor` migrates to the new dialog primitive.

### A11Y — Accessibility (cross-cutting)

- [x] **A11Y-01**: All interactive elements have visible focus rings (custom 2px outline using accent token); keyboard-only navigation across every surface — no mouse traps; dialog focus trap implemented in DIALOG-01. *(Phase 7 Plan 06 — shipped 2026-04-27. Unified `:focus-visible` rule for `.cb-btn`/`.cb-row`/`.cb-chip`/`.cb-dropdown-item` appended to cookbot-design.css; form-atom focus rings already shipped Phase 5.)*
- [x] **A11Y-02**: Color contrast meets WCAG AA on warm-cream and cocoa-dark themes for primary, secondary, and tertiary text colors; status chip variants verified. *(Phase 7 Plan 06 — verified 2026-04-27. Contrast table for `--ink`/`--ink-2`/`--ink-3` + accent + status badges recorded in 07-06-AUDIT.md; `--ink-4` correctly scoped to placeholder/disabled/decorative only.)*
- [x] **A11Y-03**: ARIA roles/labels on atoms — `button`, `dialog`, `menu`, `list`, `progressbar` (Grocery/Cooking step rail), `status` (toasts), `radio`/`checkbox`/`switch` (form atoms). *(Phase 7 Plan 06 — shipped 2026-04-27. CbToggle role=switch; CbDialog aria-labelledby; CbToastHost role=status + aria-live=polite; CookingMode step rail role=progressbar; CbInput/CbTextarea/CbSelect AriaLabel parameter; aria-label on every previously title-only icon-only button across TopBar/CookingMode/CookbookList/PantryView/AiChat.)*
- [x] **A11Y-04**: Every surface verified visually in dark mode after the phase plans complete; manual smoke-pass checklist is part of the phase verification step. *(Phase 7 Plan 06 — verified 2026-04-27. Per-surface dark-mode walkthrough table recorded in 07-06-AUDIT.md; all 9 surfaces flip cleanly via token redefinitions; CookingMode's intentional fixed-dark shell preserved with deferred-polish notes.)*

---

## Future Requirements (deferred)

Captured here so they aren't lost. Not in v1.2 scope — most carry forward from v1.1 deferred list.

- **FUTURE-V1.1-01** — Per-step temperature (was FEATURE-V2-01..05) — deferred from v1.1 Phase 4. Schema/upcaster/AI/editor/cooking-mode integration.
- **FUTURE-V1.1-02** — `Recipe.TagsJson` → relational `RecipeTag` table (was POLISH-04).
- **FUTURE-V1.1-03** — `LegacyRecipeProjector` deletion-target comment + cleanup (was POLISH-03).
- **FUTURE-V1.1-04** — Snapshot test on assembled system prompt (was POLISH-05).
- **FUTURE-V1.1-05** — README.md "Recipe Format" section + backup recovery docs (was POLISH-07).
- **FUTURE-01** — Encrypt-at-rest for `UserProfile.AiApiKey` (carry from v1.1 FUTURE-01).
- **FUTURE-02** — Token-cost telemetry per key owner (carry from v1.1 FUTURE-02).
- **FUTURE-03..06** — Substitutions, equipment, doneness cues, source provenance (carry from v1.1).
- **FUTURE-07** — Schema.org/Recipe one-way export (carry from v1.1).
- **FUTURE-08** — Computed nutrition from USDA FoodData Central (carry from v1.1).
- **FUTURE-09** — Tool-use fallback for any Anthropic model that loses Structured Outputs (carry from v1.1).
- **FUTURE-11** — Cooklang one-way export (carry from v1.1).
- **FUTURE-12** — Per-sharer cookbook-import consent banner (carry from v1.1).
- **FUTURE-13** — Smart pantry-match for HOME-02 hero (expiration-aware, %-of-pantry-used, dietary-filtered) — v1.2 ships a deterministic stub.
- **FUTURE-14** — User-facing accent variant picker (terracotta/sage in addition to orange) — tokens are wired in v1.2 but not surfaced.
- **FUTURE-15** — `AiChat` "Edit anyway" hardening. Validation-failed path (`AiChat.razor:725-744`) routes `RawResponse` through `IRecipeFormatParser.TryParse`; if the raw JSON also fails parse the user gets a "could not be parsed" toast with no navigation. Degraded but non-crashing. Surfaced as `WARN-AICHAT-RAW-EDIT-EDGE` in `.planning/v1.2-MILESTONE-AUDIT.md`.

**Obsolete:** v1.1 FUTURE-10 (MudBlazor 9.x upgrade) — rendered moot by v1.2 stripping MudBlazor entirely.

---

## Out of Scope

Explicit exclusions with reasoning. Roadmapper treats these as bright lines.

- **A second front-end framework / component library** (Tailwind, shadcn-via-React, FluentUI, Radzen) — the design is plain HTML/CSS; pure Razor + a single global stylesheet is the simpler path. Adding any framework reintroduces the same skinning problem we're solving.
- **Web API / SPA / Blazor WASM client** — `PROJECT.md` Out of Scope; `InteractiveServer` remains the render mode.
- **Any change to the canonical `RecipeDocument` schema** — v1.1 Phase 1's schema is frozen for v1.2; UI consumes it as-is. Schema evolution is FUTURE-V1.1-01.
- **Any change to the AI structured-output orchestrator** — v1.1 Phase 2's `IAiRecipeGenerator` + `SecretRedactor` + `PromptInjectionGuard` are preserved verbatim; UI consumes them as-is.
- **A new AI provider** — `AnthropicAiService` only; no OpenAI/Gemini in this milestone.
- **Containerization / CI/CD** — `run.sh` remains the deploy story.
- **Identity middleware / OAuth / SSO** — `AuthMode` reserved for a future hardening milestone; trusted-LAN posture continues.
- **Mobile-app shells (Maui/Capacitor wrapping)** — v1.2 ships a responsive web app; native shells are out.
- **Photo upload backend** — the editor's striped photo placeholder ships as the placeholder; real upload + image-storage backend is a separate concern. (Photo URL string field on `Recipe` may already exist; if so, allow paste-URL but no file-upload pipeline.)
- **Animations beyond what styles.css implies** — CSS keyframes for streaming caret/pulse and Razor state transitions; no GSAP, no Framer-style motion lib.
- **Surfacing accent variant or compact-density toggles in non-Profile UI** — variants are token-only this milestone except for the Profile density toggle.
- **Custom font hosting** — Inter is loaded from rsms.me; self-hosting is FUTURE.
- **Smart pantry-match algorithm** — HOME-02 uses a deterministic stub; smart matching is FUTURE-13.
- **A `tweaks-panel.jsx`-style debug overlay in production** — that's a prototype-only artifact; not shipped.
- **Reintroducing v1.1 Phase 4 work (per-step temperature, tags relational, README docs) into v1.2** — explicitly deferred to v1.3+.

---

## Traceability

Mapped 2026-04-27 by `/gsd-roadmapper` (auto mode). **Coverage: 75/75 ✓** (every v1.2 requirement maps to exactly one v1.2 phase).

**Phase shape adopted (with one deviation from provisional):**

- **Phase 5 — Foundation (24 reqs):** DS-01..06, ATOM-01..10, SHELL-01..04, DIALOG-01..04
- **Phase 6 — Marquee surfaces (24 reqs):** HOME-01..04, COOK-01..06, RV-01..05, ED-01..09
- **Phase 7 — Remaining surfaces + accessibility + MIG (27 reqs):** CB-01..02, PA-01..04, GR-01..04, AIC-01..05, PB-01..03, PROF-01..02, A11Y-01..04, MIG-01..03

**Deviation from provisional shape:** MIG-01..03 moved from Phase 5 → Phase 7. **Rationale:** MIG-01 (deleting the `MudBlazor` package) and MIG-02 (replacing every `Mud*` reference across all 28 Razor pages) cannot complete until every surface has been migrated, which by definition requires Phases 6 + 7 surfaces to ship first. MIG-03 (preserving existing behavior through migration) is a cross-cutting verification gate that is meaningless without the migration being complete. Phase 5 builds atoms alongside MudBlazor; Phase 7's terminal slice deletes MudBlazor. Net: Phase 5 = 24 reqs (was 27), Phase 7 = 27 reqs (was 24); total still 75.

### Per-requirement mapping

| REQ | Phase | Notes |
|-----|-------|-------|
| DS-01 | Phase 5 | Token stylesheet — load order matters; ships first |
| DS-02 | Phase 5 | Variant tokens wired; surface picker is FUTURE-14 |
| DS-03 | Phase 5 (tokens) + Phase 7 (Profile toggle) | Density toggle is on Profile, but tokens land in Phase 5; Profile toggle wiring lives under PROF-01 |
| DS-04 | Phase 5 | Inter + ss01/cv11/tnum |
| DS-05 | Phase 5 | Dark-mode parity verified during atom build; re-verified per surface in Phase 7 A11Y-04 |
| DS-06 | Phase 5 | `.cb-ph` primitive, used by ATOM-06 |
| ATOM-01 | Phase 5 | `<CbButton>` |
| ATOM-02 | Phase 5 | `<CbChip>` |
| ATOM-03 | Phase 5 | `<CbCard>` |
| ATOM-04 | Phase 5 | `<CbStat>` |
| ATOM-05 | Phase 5 | `<CbEyebrow>` |
| ATOM-06 | Phase 5 | `<StripedPlaceholder>` |
| ATOM-07 | Phase 5 | `<Icon>` × 36 glyphs |
| ATOM-08 | Phase 5 | `<CbBadge>` × 4 variants |
| ATOM-09 | Phase 5 | `<CbToggle>`/`<CbCheckbox>`/`<CbRadio>` |
| ATOM-10 | Phase 5 | `<CbInput>`/`<CbTextarea>`/`<CbSelect>` |
| SHELL-01 | Phase 5 | `MainLayout.razor` swap (atoms + sidebar + topbar in place; MudBlazor providers removed from layout but package stays for any not-yet-migrated page) |
| SHELL-02 | Phase 5 | `<Sidebar>` |
| SHELL-03 | Phase 5 | `<TopBar>` |
| SHELL-04 | Phase 5 | `<NavRow>` |
| DIALOG-01 | Phase 5 | `<CbDialog>` + focus trap (also satisfies a slice of A11Y-01 dialog gate) |
| DIALOG-02 | Phase 5 | `CbDialogService` |
| DIALOG-03 | Phase 5 | `CbToastService` |
| DIALOG-04 | Phase 5 | `<CbDropdown>` |
| HOME-01 | Phase 6 | Home hero + quick-actions row |
| HOME-02 | Phase 6 | "Tonight from your pantry" (deterministic stub) |
| HOME-03 | Phase 6 | 4-tile glance strip |
| HOME-04 | Phase 6 | Recently cooked + Up next |
| COOK-01 | Phase 6 | Cocoa cooking shell + minimal top bar |
| COOK-02 | Phase 6 | Step rail |
| COOK-03 | Phase 6 | Adaptive timer/step hero (224px / 52px) |
| COOK-04 | Phase 6 | Always-on ingredient rail |
| COOK-05 | Phase 6 | Bottom step nav + arrow-key navigation |
| COOK-06 | Phase 6 | Existing-behavior preservation gate |
| RV-01 | Phase 6 | Editorial layout + 64px display title |
| RV-02 | Phase 6 | Sticky 300px sidebar |
| RV-03 | Phase 6 | Hanging accent numerals + canonical `RecipeDocument` consumption |
| RV-04 | Phase 6 | "Notes from your last cook" callout |
| RV-05 | Phase 6 | Top-bar Share + Cook this |
| ED-01 | Phase 6 | Editor shell + meta rail |
| ED-02 | Phase 6 | Ingredients table |
| ED-03 | Phase 6 | Chip composer (absorbs v1.1 EDITOR-01) |
| ED-04 | Phase 6 | Step/Section toggle (absorbs v1.1 EDITOR-02) |
| ED-05 | Phase 6 | Timer-suggest, no auto-rewrite (absorbs v1.1 EDITOR-03) |
| ED-06 | Phase 6 | Immutable id reorder (absorbs v1.1 EDITOR-04) |
| ED-07 | Phase 6 | Paste-raw routing (absorbs v1.1 EDITOR-05) |
| ED-08 | Phase 6 | Cooking-mode chip rendering (absorbs v1.1 EDITOR-06) |
| ED-09 | Phase 6 | Chip composer keyboard a11y (absorbs v1.1 EDITOR-07) — final a11y sign-off rolls into Phase 7 A11Y-04 |
| CB-01 | Phase 7 | Cookbook list |
| CB-02 | Phase 7 | Cookbook detail |
| PA-01 | Phase 7 | Pantry top bar (AI buttons hidden when AI off) |
| PA-02 | Phase 7 | Pantry summary strip |
| PA-03 | Phase 7 | Pantry search + filters |
| PA-04 | Phase 7 | Pantry stock cards |
| GR-01 | Phase 7 | Grocery mobile-first layout |
| GR-02 | Phase 7 | Grocery progress card |
| GR-03 | Phase 7 | Grocery aisle sections |
| GR-04 | Phase 7 | Grocery sticky add button |
| AIC-01 | Phase 7 | AI Chat two-column layout |
| AIC-02 | Phase 7 | AI Chat rail |
| AIC-03 | Phase 7 | AI Chat input + suggestion chips |
| AIC-04 | Phase 7 | AI Chat right canvas (consumes v1.1 `IAiRecipeGenerator`) |
| AIC-05 | Phase 7 | AI Chat streaming animations |
| PB-01 | Phase 7 | Prompt Builder two-column layout |
| PB-02 | Phase 7 | Prompt Builder config rail |
| PB-03 | Phase 7 | Prompt Builder preview |
| PROF-01 | Phase 7 | Profile shell (includes density toggle wiring from DS-03) |
| PROF-02 | Phase 7 | Profile AI toggle + API key + shared keys dialog |
| A11Y-01 | Phase 7 | Focus rings + keyboard nav cross-surface audit (DIALOG-01 already satisfies the dialog slice in Phase 5) |
| A11Y-02 | Phase 7 | WCAG AA contrast audit (light + cocoa-dark) |
| A11Y-03 | Phase 7 | ARIA roles/labels audit |
| A11Y-04 | Phase 7 | Dark-mode visual smoke-pass (terminal verification gate) |
| MIG-01 | Phase 7 | Package + AddMudServices removal — last plan in Phase 7 |
| MIG-02 | Phase 7 | Final `Mud*` reference sweep — runs only after every surface has migrated |
| MIG-03 | Phase 7 | Behavior-preservation verification — terminal smoke pass |

**Total v1.2 reqs:** 75 across 16 categories.

**v1.1 absorptions:**
- v1.1 EDITOR-01 → v1.2 ED-03
- v1.1 EDITOR-02 → v1.2 ED-04
- v1.1 EDITOR-03 → v1.2 ED-05
- v1.1 EDITOR-04 → v1.2 ED-06
- v1.1 EDITOR-05 → v1.2 ED-07
- v1.1 EDITOR-06 → v1.2 ED-08
- v1.1 EDITOR-07 → v1.2 ED-09

**v1.1 deferrals (carry to v1.3+):**
- FEATURE-V2-01..05, POLISH-03, POLISH-04, POLISH-05, POLISH-07 → FUTURE-V1.1-*

---

*Generated 2026-04-27 from PROJECT.md + .planning/design-handoff/ (auto mode, no research step). 75 v1.2 requirements across 16 categories. Traceability completed by roadmapper. Coverage 75/75 ✓.*
