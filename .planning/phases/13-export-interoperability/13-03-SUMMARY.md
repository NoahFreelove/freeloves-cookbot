---
phase: 13-export-interoperability
plan: 03
subsystem: ui
tags: [blazor, prerender, head-content, json-ld, schema-org, cooklang, export, recipe-view, seo]

# Dependency graph
requires:
  - phase: 13-export-interoperability
    provides: JsonLdRecipeProjector.Project (Plan 01) + CooklangRecipeProjector.Project (Plan 02)
  - phase: 12-richer-format
    provides: RecipeDocument v4 consumed by both projectors
provides:
  - Server-rendered Schema.org Recipe JSON-LD in RecipeView <head> (present in the INITIAL HTTP response, not post-hydration)
  - Prerender-safe canonical-document load (DB read + deserialize moved to OnParametersSetAsync)
  - "Export as .cook" one-way export action (Cooklang download via existing cookBotDownloadFile JS + SafeFileStem)
  - Automated uat-harness prerender assertion (tests/uat-harness/tests/test-jsonld-prerender.mjs)
affects:
  - 15-nutrition (will extend the JSON-LD block with nutrition.calories/macros)

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Prerender-safe data load: DB-only read in OnParametersSetAsync; JS-interop (localStorage) stays in OnAfterRenderAsync"
    - "JSON-LD emitted as a single raw MarkupString <script> so the type attribute stays literal (avoids HtmlEncoder.Default encoding '+')"
    - "Trusted-LAN posture: prerendered JSON-LD is NOT per-user gated (TODO(AuthMode) marks future server-side-auth gating)"
    - "Shared atom CbButton forwards unmatched HTML attributes via CaptureUnmatchedValues"

key-files:
  created:
    - tests/uat-harness/tests/test-jsonld-prerender.mjs
  modified:
    - src/CookBot.Web/Components/Pages/RecipeView.razor
    - tests/uat-harness/run.mjs
    - src/CookBot.Web/Components/Atoms/CbButton.razor  # checkpoint deviation — see below

key-decisions:
  - "Trusted-LAN: prerender JSON-LD load does NOT call UserCanAccessRecipeAsync (server cannot resolve the real requester at prerender; identity is client-side/sessionStorage). Decided with the user; documented as accepted-risk + TODO(AuthMode)."
  - "Emit the whole <script type=\"application/ld+json\"> tag as one MarkupString — a Razor element routes the type attr through HtmlEncoder.Default and encodes '+' → '&#x2B;', which is spec-decodable but breaks literal string matching (uat-harness, naive validators)."
  - "Added CaptureUnmatchedValues to CbButton (outside this plan's declared scope) to fix a render-time crash from the export button's title attribute — additive, backward-compatible."

patterns-established:
  - "Prerender-safe lifecycle split: DB reads in OnParametersSetAsync, JS-interop in OnAfterRenderAsync"
  - "Raw-MarkupString <script> emission for structured-data blocks whose payload is already HTML-safe-encoded"

requirements-completed: [INTEROP-01, INTEROP-02, INTEROP-03, INTEROP-04]

# Metrics
duration: 12min
completed: 2026-06-06
---

# Phase 13 / Plan 03: RecipeView Export Integration Summary

**RecipeView now server-renders a valid Schema.org Recipe JSON-LD block into `<head>` in the initial HTTP response and offers a one-way "Export as .cook" download — both wired to the Wave-1 pure projectors.**

## Performance

- **Duration:** ~12 min (execution) + checkpoint verification
- **Started:** 2026-06-06T18:03:08-04:00
- **Completed:** 2026-06-06T18:14:29-04:00
- **Tasks:** 4 (3 autonomous + 1 human-verify checkpoint)
- **Files modified:** 4 (RecipeView.razor, run.mjs, CbButton.razor; +1 created: test-jsonld-prerender.mjs)

## Accomplishments

- **INTEROP-01 (the make-or-break):** Moved the canonical-document DB read + deserialization into `OnParametersSetAsync` (a prerender-safe lifecycle method, no JS interop), so the JSON-LD `<script>` appears in the RAW initial HTTP response a crawler sees — not only after Blazor hydration. `<HeadContent>` → `<HeadOutlet>` (already wired) carries it to `<head>`.
- **INTEROP-02:** JSON-LD includes `name`, ISO-8601 durations (`PT20M`/`PT45M`/`PT1H5M`), ingredients/instructions; `image` is omitted on the plain-http localhost (no absolute-HTTPS URL); `aggregateRating`/`review` are never emitted. Verified live by curling `/recipes/1`.
- **INTEROP-03/04:** "Export as .cook" top-bar action downloads Cooklang text via `CooklangRecipeProjector.Project(_doc)` + `CookbookDownloadHelper.SafeFileStem` + the existing `cookBotDownloadFile` JS helper (no new endpoint, no re-import path). Affordance labeled "Export only · one-way (no re-import)".
- **UAT automation:** Added `test-jsonld-prerender.mjs` (plain `fetch` of the raw response asserting `application/ld+json` + `"@type":"Recipe"`) and wired it into `run.mjs`. Verified passing against the live server.

## Checkpoint deviations (found at the Plan 13-03 human-verify checkpoint via live `curl` + uat-harness run)

Two real defects were found by actually running the app (not auto-approving) and fixed before completion:

1. **HTTP 500 on every recipe page** — the export button passed `title="…"` to `CbButton`, which declared no `CaptureUnmatchedValues`; Blazor throws `InvalidOperationException` for unmatched attributes, crashing the whole RecipeView render. **Fix (`65c5bdd`):** `CbButton` now splats unmatched attributes onto its `<button>` (additive; outside this plan's declared `files_modified` — documented deviation).
2. **`type="application/ld&#x2B;json"`** — emitting the `<script>` as a Razor element routed the `type` attribute through `HtmlEncoder.Default`, encoding `+`→`&#x2B;`; spec-decodable but non-standard and it broke the uat-harness literal match. **Fix (`770c290`):** emit the whole tag as one raw `MarkupString` (payload already HTML-safe-encoded by the projector → no `</script>` breakout).

## Verification (live, against `dotnet run` on :7000)

- `/recipes/1` → HTTP 200 (was 500 before the CbButton fix)
- Raw HTML contains exactly one literal `<script type="application/ld+json">` block; `@type:Recipe`, `name`, ISO-8601 durations present; no `aggregateRating`; `image` omitted on http; no `</script>` breakout
- `runJsonLdPrerender()` uat-harness test → PASS
- Export button renders with the one-way tooltip; `ExportCooklang` wired to projector + download helper
- Full suite (excl. gated live-API tests): 407 passed, 0 failed — no regression from the shared `CbButton` change

## Self-Check: PASSED
