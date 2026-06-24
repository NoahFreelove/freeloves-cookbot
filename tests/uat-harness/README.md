# CookBot UAT Harness

Playwright/Node browser-UAT harness for FreelovesCookBot. Runs hands-free against
the live app on `http://localhost:7000` using the system snap chromium.

## Session / user

CookBot has no login (trusted-LAN). The default circuit user is the **first user**
— `CurrentUserService` picks the lowest user id when sessionStorage has no
selection. On the seeded DB that is **Noah** (id 1, admin, UnitSystem=Canadian),
who owns every cookbook and recipe. So the harness session is already Noah, which
is what Tests 5/7/Conversion need (Noah owns the recipes they touch).

## Recipe discovery

The home page exposes **no** `/recipes/{id}` anchors. Recipes are only reachable
from `/cookbooks/{id}`, where each recipe renders as
`<div role="button" class="cb-card" @onclick=ViewRecipe(id)>`. `findFirstRecipe`
(in `lib/session.mjs`) probes cookbook ids, finds the first one with a recipe
card, clicks the card, and captures the resulting `/recipes/{id}` URL + name.

## What it tests

| Test | Name | Status |
|------|------|--------|
| Test 5 | Cookbook reparenting navigation (POLISH-01) | Automated |
| Test 7 | TopBar responsive collapse at 719px (POLISH-04 + CLEANUP-01/02) | Automated |
| Conversion | Per-recipe unit display toggle (CLEANUP-04) | Automated |
| Test 4 | RawRecipeEditorDialog validation-fail (QOL-04) | SKIP — see below |
| JSON-LD Prerender | Schema.org `Recipe` ld+json in RAW HTTP response (INTEROP-01) | Automated |
| Test 14 | Photo gallery (GALLERY-01..04) | Partial — upload-dependent items SKIP (Blazor `<InputFile>` SignalR streaming is not drivable under Playwright); paste-URL reject, AI text-only, disclaimer automated |
| Test 16 | v1.4 integration: nutrition panel + JSON-LD nutrition + Cooklang export | Automated (Phase 16 / UATAUTO-02) |

### Test 16: v1.4 integration (Phase 16 / UATAUTO-02)

Creates a throwaway recipe (four CNF-matchable staples + one unmatchable "edible gold
flake") via the editor's Paste-raw seam, then asserts hands-free against the live app:
- **Nutrition State 1** — "Calculate nutrition" CTA, "not yet calculated", and the exact
  non-dismissable Health Canada disclaimer; no macro grid pre-compute (NUTR-04/05; never
  auto-computes on load).
- **JSON-LD pre-compute** — `application/ld+json` parses, `@type` Recipe, `nutrition` key
  absent.
- **Compute → State 2** — 4-up macro grid (Energy/Protein/Carbs/Fat), "Matched N of M
  ingredients" coverage line, `--` (never `0`) for the unmatched ingredient, Per-serving↔Total
  toggle, and the "Show all matches" expander (NUTR-02/03/04).
- **JSON-LD post-compute** — now carries `nutrition.@type=NutritionInformation` +
  `calories` (NUTR-06; the achievable half of the Phase 16 SC2 cross-theme check).
- **Cooklang export** — captures the base64 payload handed to `cookBotDownloadFile` and
  asserts a non-empty `.cook` with `@ingredient` tokens (INTEROP-02). (It captures the
  payload rather than the browser download because `download.js` revokes the blob URL
  synchronously after the click, so Playwright's download artifact races to ENOENT.)

Deletes the throwaway recipe on exit; idempotent (a leftover from a crashed run is removed
at start). **Known gap:** the gallery-hero `image` half of SC2 needs an absolute-HTTPS host
(omitted by design on localhost http), and a few nutrition states (≈ low-confidence,
stale/error banners, ≤720px 2-col) are not yet automated — see `15-HUMAN-UAT.md`.

### Test 5: Cookbook reparenting

Navigates to a recipe's edit page, changes the Cookbook `CbSelect` to a different
cookbook (destination chosen at runtime from the live option set — never
hard-coded, so the test survives repeated runs that flip the recipe back and
forth), clicks the visible Save button, then asserts:
- Browser navigates to the **recipe view** `/recipes/{id}` — the PLANNED behaviour
  on a cookbook change (plan 10-10 L20/L148; `RecipeEditor.razor:817`).
- The recipe now appears as a card on the **destination** cookbook page.
- The recipe no longer appears on the **origin** cookbook page.

> **Documented spec/plan conflict:** the Phase-10 UAT spec sentence
> (`10-HUMAN-UAT.md:49`) says Save "navigates to destination cookbook's page".
> The implementation (and plan 10-10) instead navigates to the **recipe view**.
> The substantive reparent (moved out of origin, into destination) is fully
> verified either way; the stale spec wording is a reported finding, not a bug
> in the reparent itself. This test asserts the app's actual planned behaviour.

**Mutates data** (moves the recipe between cookbooks) — intended and idempotent
across runs. **Requires:** ≥1 recipe and ≥2 cookbooks owned by the default user.

### Test 7: TopBar responsive collapse

Loads `/recipes/{id}` at 719px viewport width and asserts:
1. `.topbar-right-slot` is hidden via `display:none` (POLISH-04)
2. `.recipe-actions-inline-fallback` is visible (POLISH-04)
3. An **Edit** `<button>` is present in the fallback row AND is rendered
   (non-zero size, not clipped off the left edge) — the exact CLEANUP-01 fix
4. `.recipe-hero` grid collapses to a single column (CLEANUP-02)

**Requires:** Plan 11-02 (CLEANUP-01/02) applied.

### Conversion: per-recipe unit toggle (CLEANUP-04)

Loads `/recipes/1` ("Apple Blueberry Crumble", which has a 900 g ingredient) and
asserts (unit-system-agnostic — proves the convert↔original flip is wired):
1. The per-recipe unit toggle `<button>` ("...units") exists.
2. Toggling it **changes** at least one displayed ingredient amount.
3. ORIGINAL mode surfaces the canonical `900 g` verbatim AND CONVERTED mode
   differs from ORIGINAL on ≥1 ingredient — proving display conversion runs
   without mutating the canonical document.

Restores the recipe's pre-existing `localStorage["cookbot_units_1"]` afterward and
never touches the user's `UnitSystem` — leaves no residue.

**Requires:** Plan 11-04 (CLEANUP-04) applied; recipe id 1 seeded with the 900 g
ingredient.

## Artifacts

Screenshots are written to `artifacts/` at key assertion points (git-ignored) for
human spot-checking.

### Test 4: Validation-fail fallback (SKIP — manual/deferred)

**Disposition:** SKIP — cannot be triggered while the AI happy-path succeeds.

The `RawRecipeEditorDialog` only opens when the AI returns malformed output that fails CookBot's schema validation (`AiChat.razor` L312 gates on `_lastStructuredRecipe.Ok == true`). Without a server-side fault-injection seam that forces a schema-mismatch AI response, there is no reliable way to trigger this dialog from a browser session.

Source: `10-HUMAN-UAT.md` §Gaps — "validation_fail_fallback: deferred — cannot be exercised while the happy path succeeds."

**To implement in the future:**
1. Add a harness-only server route or query parameter that instructs `AnthropicAiService` / `AiRecipeGenerator` to return a canned invalid JSON body.
2. Update `tests/test4-validation-fail.mjs` to drive that seam and assert the full dialog flow.

A SKIP result does **not** cause a non-zero exit code. It is printed distinctly (`UAT Test 4: SKIP`).

## Prerequisites

- **Node.js** v18+ (tested with v24.15.0)
- **Chromium** — the harness uses the system snap chromium at `/snap/bin/chromium` (verified working 2026-06-05 on this machine). If snap confinement prevents launch, install the Playwright-bundled browser:
  ```
  npx playwright install chromium
  ```
  Then remove `executablePath` from `run.mjs` (or set `PLAYWRIGHT_CHROMIUM_EXECUTABLE_PATH=""`).
- **App running** on `http://localhost:7000` — the harness does **not** start the app. Run it yourself first:
  ```
  # From the project root:
  ./run.sh
  ```
- At least **one recipe** and **two cookbooks** owned by the default user (the
  first user, Noah, on the seeded DB) for Test 5, plus recipe id 1 with the 900 g
  ingredient for the Conversion check.

## How to run

```sh
# From the project root:
./run.sh &     # start the app in the background, or in a separate terminal

# From tests/uat-harness/:
cd tests/uat-harness
npm install    # first time only — installs playwright
npm test       # or: node run.mjs
```

Check the exit code:
```sh
echo $?        # 0 = all tests passed (SKIP is not a failure)
               # 1 = one or more tests FAILED
```

## Expected output

```
[app] App is ready (healthz 200).
[session] Navigating to app root to establish default-user (Noah) session...
[session] Found recipe via cookbook 1: id=2 name="Quick Weeknight Pasta"

[harness] Using recipe: id=2, name="Quick Weeknight Pasta"

[test5] Starting UAT Test 5 (reparenting) for recipe 2 ("Quick Weeknight Pasta")...
[test5] Origin cookbook id: 1
[test5] Destination cookbook id: 2 ("Desserts")
[test5] Navigated to recipe view http://localhost:7000/recipes/2 — PASS (post-save nav)
[test5] Recipe "Quick Weeknight Pasta" found on destination cookbook 2 — PASS
[test5] Recipe "Quick Weeknight Pasta" is ABSENT from origin cookbook 1 — PASS

[test7] Starting UAT Test 7 (responsive) at 719px viewport...
[test7] .topbar-right-slot is hidden (display:none) — PASS
[test7] .recipe-actions-inline-fallback is visible — PASS
[test7] "Edit" button present + rendered in fallback (w=89px left=272px) — PASS (CLEANUP-01)
[test7] .recipe-hero single-column at 719px (columns="407px") — PASS (CLEANUP-02)

[conversion] Starting UAT Conversion (CLEANUP-04) on /recipes/1...
[conversion] Unit toggle button exists — PASS (a)
[conversion] Toggle changed 7 amount(s), e.g. ingredient #4: "0.19 cups" ↔ "3 tbsp" — PASS (b)
[conversion] ORIGINAL mode shows canonical "900 g" verbatim — PASS (c.1)
[conversion] CONVERTED differs from ORIGINAL ... — PASS (c.2)

[test4] UAT Test 4 (validation-fail): SKIP — manual/deferred
...

────────────────────────────────────────────────────────────
UAT HARNESS RESULTS
────────────────────────────────────────────────────────────
UAT Test 5: PASS
UAT Test 7: PASS
UAT Conversion (CLEANUP-04): PASS
UAT Test 4: SKIP
  -> UAT Test 4 (validation-fail): SKIP — manual/deferred. ...
────────────────────────────────────────────────────────────
RESULT: PASS — 3 passed, 1 skipped, 0 failed.
```

## Isolation

This harness is **isolated from the .NET solution**:

- It has its own `package.json` and `node_modules` (gitignored)
- It is **not** referenced in `FreelovesCookBot.sln`
- The `.NET` test project (`tests/CookBot.Tests`) has **no** Playwright or Selenium packages
- `node_modules/` is excluded from git via `.gitignore`
