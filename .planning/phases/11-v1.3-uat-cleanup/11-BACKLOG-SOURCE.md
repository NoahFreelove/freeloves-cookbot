# Phase 11 — Source material (promoted backlog + new harness scope)

Promoted from ROADMAP `## Backlog` on 2026-06-05 during `/gsd-progress --next`
(user-chosen "Backlog + auto-UAT, parallel" closeout path). All four items were
surfaced during Phase 10 UAT (999.1 already resolved; not promoted). The harness
item (UATAUTO-01) is new scope added to make future UAT hands-free.

These are the raw reproducers/notes — discuss/plan-phase should turn them into
plans. Original backlog numbering kept in parentheses for traceability.

---

## CLEANUP-01 — RecipeView inline-fallback action row missing Edit button (was 999.5)

**Goal:** Find out why the Edit button is absent from the inline
`.recipe-actions-inline-fallback` row even though `RecipeView._topBarActions`
includes it as the first CbButton in the RenderFragment.

**Reproducer:** Open `/recipes/{id}` at ≤720px viewport. The inline fallback row
shows Share / Schedule / Cook this, but Edit is absent.

**Suspects to investigate:**
- Edit may be clipped left by the row's `justify-content:flex-end` if total width
  exceeds container — but other buttons would also overflow then.
- `CbButton` with `StartIcon="Pencil"` may be conditionally hidden by some
  auth/owner check we haven't traced.
- The `@<text>` RenderFragment construction may be dropping the first child in
  some Blazor/MarkupString quirk.
- A separate CSS rule may be `display:none`-ing buttons matching a Pencil icon pattern.

**Surfaced:** Phase 10 UAT Test 7 (2026-05-22). Visible in user screenshot at narrow viewport.

---

## CLEANUP-02 — RecipeView responsive layout broken at narrow viewports (was 999.4)

**Goal:** Make RecipeView usable at ≤720px viewports. POLISH-04 wired the
TopBar/inline-fallback toggle, but the rest of RecipeView's layout still assumes
wide viewport.

**Reproducer:** Open any `/recipes/{id}` page, resize browser to 719px. The hero
`display:grid; grid-template-columns:1fr 1fr` doesn't stack — title is clipped,
the hero photo placeholder compresses to a vertical strip on the right.
Ingredients column overflows; method column wraps text per-word into a too-narrow
strip. `<article style="max-width:1080px;padding:24px 32px 80px">` has no
narrow-viewport variant.

**Notes:** Likely needs `@media (max-width: 720px)` rules on the hero grid, the
ingredient/method grid, and the step number grid. Or a single CSS class for
"responsive recipe layout" that switches grid-template-columns to a single column
below the breakpoint.

**Surfaced:** Phase 10 UAT Test 7 (2026-05-22). User: "everything is squished but
nothing condensed properly".

---

## CLEANUP-03 — Sidebar polish: Profile row clipped, body bg ends before sidebar bottom (was 999.3)

**Goal:** Fix the `.cb-shell .side` grid cell so (a) the Profile row at the
sidebar bottom is fully visible (not clipped on the left), and (b) the `--cream`
body background extends to the full sidebar height instead of cutting off short.

**Reproducer:** Open any page at default desktop zoom on a typical 1080p viewport.
The Profile button at the bottom of the sidebar is partially hidden (text "rofile"
visible, leading icon clipped). The main column's cream background stops short of
the sidebar bottom edge, exposing the body background underneath.

**Notes:** Both symptoms point at the `.cb-shell { display:grid; height:100% }`
rule — `height:100%` may not be inheriting correctly from the
`<div class="cb-shell" style="height:100vh">` wrapper, OR the sidebar is being
given an explicit height that exceeds the grid row.

**Surfaced:** Phase 10 UAT Test 4 retest (2026-05-22). User screenshot showed both
issues simultaneously.

---

## CLEANUP-04 — Recipe amounts not in user-selected unit system (was 999.2)

**Goal:** Render `RecipeIngredient.Amount` + `Unit` (and per-step
`StepTemperature`) through the user's `UserProfile.UnitSystem` preference on
RecipeView, CookingMode, and the AiChat canvas.

**Reproducer:** Set `UserProfile.UnitSystem = "imperial"` (or "metric"). Generate
or view a recipe whose AI-emitted units are the other system (e.g. `400 g
spaghetti`). The view displays the raw AI unit, not a converted display unit.

**Notes:** This is a feature gap, not a regression. The canonical
`RecipeDocument.Ingredient.Unit` is authoritative; display-side conversion would
need a unit-conversion table (g↔oz, ml↔fl oz, °F↔°C, etc.) and a
per-recipe-per-user toggle so the user can opt back to the original units.
`UserProfile.UnitSystem` exists today and is read by `PromptBuilderService` for AI
guidance, but no display-time conversion layer exists. **Largest item in this
phase** — treat as a real feature slice, not a polish fix.

**Surfaced:** Phase 10 UAT Test 4 retest (2026-05-22). User report: "its not
displaying the units in the user selected units".

---

## UATAUTO-01 — Automated browser-UAT harness (new scope)

**Goal:** A reusable harness that drives the running app through the Phase 10
human-UAT flows hands-free, so the user no longer hand-runs UAT each milestone.

**Why:** User feedback during this `/gsd-progress` run — "UAT takes sooo long to
do, we should automate as much as possible because im quite busy."

**Environment (confirmed 2026-06-05):**
- chromium at `/snap/bin/chromium`, headless works.
- Node v24.15.0 + Python 3.14 + pip available. No package.json yet (pure .NET repo).
- bUnit 1.40.0 is in the test project but is component-only — cannot exercise JS
  interop, localStorage-before-paint, responsive CSS, or the SignalR circuit.
- App runs via `./run.sh` → `dotnet run --project src/CookBot.Web` on
  `http://localhost:7000`. Trusted-LAN auth posture (`CurrentUserService`); the
  harness must discover how a user/session is selected.

**Must drive at minimum (the still-open Phase 10 UAT reruns):**
- **UAT Test 5 — cookbook reparenting (POLISH-01):** open a recipe → Edit → change
  cookbook selector → Save → assert navigation to destination cookbook + recipe no
  longer in original cookbook. (Just unblocked by the 999.1 fix.)
- **UAT Test 7 — responsive collapse (POLISH-04):** load a recipe at 719px viewport
  → assert TopBar actions hidden + inline fallback row visible + (after CLEANUP-02)
  the rest of the layout stacks + (after CLEANUP-01) Edit present in the fallback row.

**Known limitation to document, not fake:**
- **UAT Test 4 — validation-fail fallback (QOL-04):** the `RawRecipeEditorDialog`
  only opens when the AI returns malformed output. The happy path currently
  succeeds, so this cannot be triggered without synthetic schema-mismatch fault
  injection. The harness should expose a fault-injection seam OR the phase should
  record this as a manual/deferred check rather than pretend it ran.

**Design constraints (carry the CLAUDE.md guardrails):**
- Pure .NET repo today — adding a Node/Playwright harness is acceptable as test
  tooling, but keep it isolated (e.g. `tests/uat-harness/` or `tools/`), out of the
  shipped app, and out of the .NET solution build. Decide at plan time whether
  Playwright manages its own chromium or reuses `/snap/bin/chromium`.
- Do NOT add browser-test packages to the .NET test project (`CookBot.Tests`).
