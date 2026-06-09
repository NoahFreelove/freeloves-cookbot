# Phase 11: v1.3 UAT Cleanup & Automated UAT Harness - Context

**Gathered:** 2026-06-05
**Status:** Ready for planning
**Source:** Backlog promotion (999.2–999.5) + user decisions during `/gsd-progress --next`. Full reproducers in `11-BACKLOG-SOURCE.md`.

<domain>
## Phase Boundary

Close the four defects/gaps surfaced during Phase 10 UAT and add a reusable
automated browser-UAT harness. This is a **cleanup + tooling** phase on
already-shipped v1.3 surfaces — not net-new product surface. Three items are
fixes to existing designed components (RecipeView, the app shell); one is a real
display-layer feature (unit conversion); one is test infrastructure.

**In scope:** CLEANUP-01..04, UATAUTO-01 (see ROADMAP success criteria).
**Out of scope:** any change to the canonical `RecipeDocument` shape or the AI
schema (v3 is frozen); auth/identity changes; new product pages.
</domain>

<decisions>
## Implementation Decisions

### CLEANUP-01 — RecipeView inline-fallback Edit button (was 999.5)
- This is a **find-the-root-cause-then-fix** task, not a workaround. The Edit
  CbButton is first in `RecipeView._topBarActions` RenderFragment yet absent from
  the inline `.recipe-actions-inline-fallback` row at ≤720px.
- Investigate the four suspects in `11-BACKLOG-SOURCE.md` (flex clipping, owner/auth
  conditional, `@<text>` RenderFragment first-child drop, CSS `display:none` on
  Pencil) and fix the actual cause. Acceptance: Edit visible alongside Share /
  Schedule / Cook this at ≤720px.

### CLEANUP-02 — RecipeView responsive layout ≤720px (was 999.4)
- Add `@media (max-width: 720px)` handling so the hero grid
  (`grid-template-columns:1fr 1fr`) stacks to one column, ingredient/method columns
  reflow to full width, the step-number grid stops per-word wrapping, and
  `<article style="max-width:1080px;padding:24px 32px 80px">` gets a narrow variant.
- Breakpoint is **720px** — same threshold POLISH-04 already uses for the
  TopBar/inline-fallback toggle, so the two stay consistent.
- Prefer a CSS class in `cookbot-design.css` over inline styles where the current
  markup uses inline `style=` (so the media query can target it).

### CLEANUP-03 — Sidebar Profile-row clip + body-bg gap (was 999.3)
- Fix `.cb-shell .side` (in `cookbot-design.css`) so the bottom Profile row is fully
  visible (no left clip) and the `--cream` body background reaches the full sidebar
  height. Root cause likely the `.cb-shell { display:grid; height:100% }` vs the
  `height:100vh` wrapper interaction. Fix the height/grid inheritance, don't mask
  with overflow hacks.

### CLEANUP-04 — Unit-system display conversion (was 999.2) — USER DECISIONS
- **Default behavior (user decision 2026-06-05): AUTO-CONVERT to the user's
  `UserProfile.UnitSystem`.** When a recipe's stored units differ from the user's
  preference, RecipeView / CookingMode / AiChat canvas display the **converted**
  amount/temperature by default (e.g. `400 g → 14 oz`, `200°C → 400°F`).
- **Per-recipe toggle** flips that recipe back to the AI-emitted original units.
  Toggle state persists client-side via **localStorage keyed by recipe id** — NO
  new `UserProfile` column, NO EF migration (mirrors the QOL-05 accent-picker
  precedent in `cookbot-shell.js`). Default state = converted.
- **Scope (user decision 2026-06-05): FULL** — bidirectional metric↔imperial for
  **weights** (g↔oz/lb), **volumes** (ml↔fl oz/cup), and **temperatures**
  (°C↔°F + gas mark), on **all three** surfaces (RecipeView, CookingMode, AiChat).
- **Display-only, never mutate canonical.** The canonical `RecipeDocument`
  (`Recipe.CanonicalDocumentJson`) stays authoritative and untouched. Conversion is
  a render-time transform. Reads stay canonical-first (per the hard invariant).
- Conversion belongs in a **pure converter in `CookBot.Application`** (e.g.
  `UnitConversionService` / `UnitConverter`) with a factor table; unit-tested for
  round-trip and known reference values. Handle "non-convertible" units (e.g.
  "1 clove", "to taste", "pinch") by passing them through unchanged.
- Reuse `StepTemperature` (`src/CookBot.Domain/Recipes/StepTemperature.cs`) units
  (F/C/gas) for temperature; do not invent a parallel temperature model.

### UATAUTO-01 — Automated browser-UAT harness (new tooling)
- **Playwright + Node** (node v24.15.0 present) driving chromium. Default to
  **reusing the system chromium at `/snap/bin/chromium`** via `executablePath` /
  `channel` to avoid a large browser download; if snap confinement fights Playwright
  at execution time, fall back to `npx playwright install chromium`. (Claude's
  discretion at execute time — pick whichever actually launches.)
- **Isolated from the shipped app and the .NET solution build.** Live under
  `tests/uat-harness/` (own `package.json`, own `node_modules`, gitignored). Do NOT
  add it to `FreelovesCookBot.sln`. Do NOT add Playwright/Selenium packages to
  `CookBot.Tests` (.NET test project stays component-only via bUnit).
- **Must automate the two still-open Phase 10 UAT reruns:**
  - UAT Test 5 — cookbook reparenting (POLISH-01): open a recipe → Edit → change
    cookbook selector → Save → assert navigation to destination cookbook + recipe
    gone from original.
  - UAT Test 7 — responsive collapse (POLISH-04): load `/recipes/{id}` at 719px →
    assert TopBar actions hidden + inline fallback visible + (post-CLEANUP-02) layout
    stacks + (post-CLEANUP-01) Edit present.
- The harness must **discover/establish a logged-in session** (trusted-LAN
  `CurrentUserService` posture — find how a user is selected and script it).
- **UAT Test 4 (validation-fail fallback) honesty rule:** it cannot be triggered
  while the AI happy-path succeeds. The harness must EITHER expose a fault-injection
  seam (force a schema-mismatch AI response so `RawRecipeEditorDialog` opens) OR
  the plan records Test 4 as a manual/deferred check. **Do not fake a pass.**
- Harness emits a clear pass/fail summary (exit code + per-test result) so it can be
  re-run each milestone hands-free. A short README documents how to run it.

### Claude's Discretion
- Exact converter API shape, factor constants, rounding rules (sensible cooking
  rounding — e.g. don't show `13.9876 oz`).
- Where the per-recipe unit-toggle control sits in each surface's header (match the
  existing v1.2 control styling — CbButton / icon-toggle conventions).
- Harness internal structure, selector strategy (prefer stable `data-` hooks or
  visible text over brittle CSS), and whether it spins up the app itself or expects
  it already running on :7000.
- CLEANUP-01 fix mechanism, once the root cause is identified.
</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Recipe consumer surfaces (CLEANUP-01, 02, 04)
- `src/CookBot.Web/Components/Pages/RecipeView.razor` — hero grid, `_topBarActions`, inline fallback row (CLEANUP-01/02/04)
- `src/CookBot.Web/Components/Pages/CookingMode.razor` — unit display (CLEANUP-04)
- `src/CookBot.Web/Components/Pages/AiChat.razor` — generated-recipe canvas unit display (CLEANUP-04)

### App shell / layout / styling (CLEANUP-02, 03)
- `src/CookBot.Web/wwwroot/css/cookbot-design.css` — `.cb-shell`, `.side`, responsive rules live here (CLEANUP-02/03)
- `src/CookBot.Web/wwwroot/app.css` — secondary stylesheet
- `src/CookBot.Web/Components/Layout/MainLayout.razor` — `.cb-shell` wrapper + `height:100vh` (CLEANUP-03)
- `src/CookBot.Web/Components/Layout/Sidebar.razor` — Profile row at sidebar bottom (CLEANUP-03)
- `src/CookBot.Web/Components/Layout/TopBar.razor` — RightSlot / responsive (CLEANUP-01 context)

### Unit conversion (CLEANUP-04)
- `src/CookBot.Domain/Recipes/StepTemperature.cs` — temperature record + F/C/gas units (reuse, don't duplicate)
- `src/CookBot.Domain/Recipes/IngredientEntry.cs` — canonical ingredient (Amount/Unit) read shape
- `src/CookBot.Domain/Entities/RecipeIngredient.cs` — EF ingredient entity
- `src/CookBot.Application/Services/PromptBuilderService.cs` — existing reader of `UserProfile.UnitSystem` (see how the preference is accessed)
- `UserProfile` entity (grep `UnitSystem`) — preference source of truth

### Client-state persistence precedent (CLEANUP-04 toggle)
- `src/CookBot.Web/wwwroot/js/cookbot-shell.js` — QOL-05 accent picker localStorage-before-paint pattern (mirror for the per-recipe unit toggle)

### Harness (UATAUTO-01)
- `run.sh` / `src/CookBot.Web/Program.cs` — how the app launches on :7000
- `src/CookBot.Web/Services/CurrentUserService.cs` (grep) — trusted-LAN user selection the harness must script
- `.planning/phases/10-qol-polish-consumer-surfaces/10-HUMAN-UAT.md` — Tests 5 & 7 expected behavior

### Project guardrails (MANDATORY)
- `CLAUDE.md` — canonical-first reads, no MudBlazor, no Newtonsoft, System.Text.Json only, don't auto-scale temps/times
- `.planning/STATE.md` — v1.3 hard invariants (canonical-first, no auto-rewrite, MudBlazor stays out)
</canonical_refs>

<specifics>
## Specific Ideas

- Conversion reference values worth unit-testing: `200°C = 392°F` (≈400°F cooking-rounded),
  `gas mark 6 = 200°C = 400°F`, `100 g ≈ 3.53 oz`, `250 ml ≈ 8.45 fl oz ≈ 1 cup (US)`,
  `1 lb = 16 oz = 453.6 g`.
- Non-convertible passthrough cases to test: `"to taste"`, `"1 clove"`, `"a pinch"`,
  `Amount` null / unit empty.
- POLISH-04 already established the 720px breakpoint and the
  `.recipe-actions-inline-fallback` class — reuse them; don't introduce a second breakpoint.
</specifics>

<deferred>
## Deferred Ideas

- Per-user (not per-recipe) global "always convert" preference and a DB-persisted
  toggle — out of scope; localStorage per-recipe is the v1.3 answer.
- Automating UAT Tests 1/2/3/6 (already passed in session 2) — harness may add them
  opportunistically but they are not required this phase.
- A general E2E/CI pipeline around the harness — this phase ships the harness +
  the two open reruns, not CI wiring.
</deferred>

---

*Phase: 11-v1.3-uat-cleanup*
*Context gathered: 2026-06-05 via backlog promotion + user decisions*
