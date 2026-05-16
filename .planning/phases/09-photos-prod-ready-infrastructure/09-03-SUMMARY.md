---
phase: 09-photos-prod-ready-infrastructure
plan: 03
subsystem: ui

tags: [blazor, recipephoto, photourl, recipedocument, recipeview, home, aichat, cookbooklist, polish-01]

# Dependency graph
requires:
  - phase: 08-format-foundation
    provides: Recipe.PhotoUrl (entity column) + RecipeDocument.PhotoUrl (canonical field), Recipe.Description (entity column) + RecipeDocument.Description (canonical field)
  - phase: 09-photos-prod-ready-infrastructure
    provides: 09-01 PHOTO storage/validator pipeline (RecipePhotoUrlValidator gate ensures stored PhotoUrls are http/https only)
provides:
  - PHOTO-08 referrerpolicy="no-referrer" + loading="lazy" applied uniformly to every <img> rendered from Recipe.PhotoUrl across four read surfaces
  - PHOTO-10 RecipeView hero swaps StripedPlaceholder for <img> bound to _doc.PhotoUrl (canonical-first read) with PITFALL H4 one-shot Blazor state-flag fallback
  - PHOTO-11 Home tonight-from-your-pantry hero card + recently-cooked tiles render Recipe.PhotoUrl thumbnails with per-recipe-id failure tracking
  - PHOTO-12 AiChat streaming canvas card surfaces doc.PhotoUrl as a direct property access on _lastStructuredRecipe.Value (POLISH-01 invariant preserved — no extractor revival)
  - PHOTO-13 CookbookList 3×2 collage samples up to 6 real Recipe.PhotoUrls per cookbook, falls back to accent-tinted striped tile per missing/failed cell
  - D-39 view-side mirror: Recipe.Description rendered as <p class="recipe-lede"> under the recipe title in RecipeView, matching the editor's name → description → ingredients reading order
affects: [10-qol-and-polish (Profile telemetry widget may surface photos in future card views), 09-04 (photo composite editor — write side), 09-05 (AnthropicAiService AI-emitted PhotoUrl plumbing)]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Blazor state-flag onerror debounce — pure-C# guard via `_<feature>PhotoFailed` (bool) or `_<feature>FailedFor` (HashSet / Dictionary) instead of inline JS `this.onerror=null`; the @if branch in markup removes the <img> element entirely so the browser cannot loop (PITFALL H4)"
    - "Canonical-first read for view surfaces — prefer _doc.PhotoUrl (RecipeDocument) over _recipe.PhotoUrl (entity) when both are bound; matches PROJECT.md hard invariant"
    - "Eager-load Recipes for collage projection — CookbookList.razor's existing .Include(c => c.Recipes) carries PhotoUrl through without a separate query (no new round trip)"

key-files:
  created: []
  modified:
    - src/CookBot.Web/Components/Pages/RecipeView.razor
    - src/CookBot.Web/Components/Pages/Home.razor
    - src/CookBot.Web/Components/Pages/Home.razor.cs
    - src/CookBot.Web/Components/Pages/AiChat.razor
    - src/CookBot.Web/Components/Pages/CookbookList.razor

key-decisions:
  - "Used _doc.PhotoUrl (canonical document) over _recipe.PhotoUrl (entity column) for RecipeView hero, honoring PROJECT.md canonical-first reads invariant; the entity row is the persistence shape, the canonical doc is the view-side contract"
  - "HomeRecentRecipe and HomePantryMatch records were extended with PhotoUrl: string? rather than introducing a parallel side-channel dictionary — keeps the data shape colocated with the tile metadata that the markup already consumes"
  - "AiChat canvas renders NOTHING (no striped tile) when doc.PhotoUrl is null — the AI legitimately may omit a photo and a placeholder there would be visual noise. Contrast with RecipeView / Home where StripedPlaceholder is the conscious empty-state hero"
  - "CookbookList collage stable-sorts Recipes by RecipeId asc for the 6-cell sampler — deterministic, matches the QOL-01 stable-sort convention, and survives recipe edits (UpdatedAt would reorder)"
  - "_collageFailedFor uses Dictionary<int, HashSet<int>> keyed by (cookbookId, cellIndex) — each cell fails independently so one broken photo does not gate the rest of the cookbook's collage"

patterns-established:
  - "PHOTO-08 conformance pattern — every photo <img> in the codebase MUST carry referrerpolicy='no-referrer' AND loading='lazy'. Five surfaces now match"
  - "Photo-failure reset cadence — flags/sets cleared at the same point data is reloaded (LoadDashboardAsync, LoadCookbooks, _lastStructuredRecipe = null) so newly-set PhotoUrls get fresh load attempts"

requirements-completed: [PHOTO-08, PHOTO-10, PHOTO-11, PHOTO-12, PHOTO-13]

# Metrics
duration: 8min
completed: 2026-05-16
---

# Phase 09 Plan 03: Photo + Description Read Surfaces Summary

**Recipe.PhotoUrl now renders on five read surfaces (RecipeView hero, Home tonight + recently-cooked tiles, AiChat canvas, CookbookList collage) with PHOTO-08 referrer/lazy policy and PITFALL H4 one-shot Blazor state-flag debounce; Recipe.Description renders as the lede under the title in RecipeView (D-39 view-side mirror). POLISH-01 invariant preserved — AiChat reads doc.PhotoUrl as a single direct property access on _lastStructuredRecipe.Value, no extractor revival.**

## Performance

- **Duration:** 8 min
- **Started:** 2026-05-16T18:32:45Z
- **Completed:** 2026-05-16T18:40:39Z
- **Tasks:** 3
- **Files modified:** 5

## Accomplishments

- RecipeView hero conditionally renders `<img>` bound to `_doc.PhotoUrl` when the canonical doc has a photo, falls back to `StripedPlaceholder` otherwise; broken URLs trip a one-shot `_heroPhotoFailed` flag and the placeholder takes over without any browser retry loop
- Recipe Description renders as a `<p class="recipe-lede">` directly under the recipe title in RecipeView when non-whitespace — D-39 view-side mirror closing the visual pair name + lede that the editor already pairs the same way
- Home "Tonight from your pantry" hero card swaps placeholder for the top pantry match's `Recipe.PhotoUrl` when available; "Recently cooked" tiles each render their own `Recipe.PhotoUrl` thumbnail; per-recipe-id `_photoFailedFor` HashSet scopes failures so one broken thumbnail does not gate the row
- AiChat streaming canvas card prepends an `<img src="@doc.PhotoUrl">` at the top of the recipe-card render — single direct property access on the existing `_lastStructuredRecipe.Value` accumulator (POLISH-01 invariant verified; the only `Extract*` reference in the file is the doc-comment header asserting the extractor stays deleted)
- CookbookList 3×2 collage samples up to 6 real `Recipe.PhotoUrl`s per cookbook (ordered by RecipeId asc for stability) using the recipes already eager-loaded by `LoadCookbooks`'s existing `.Include(c => c.Recipes)`; per-cell `_collageFailedFor[cookbookId][cellIndex]` tracking keeps independent failure state across the 6 cells
- Every `<img>` carries `referrerpolicy="no-referrer"` AND `loading="lazy"` per PHOTO-08

## Task Commits

Each task was committed atomically:

1. **Task 1: RecipeView hero + Description lede (PHOTO-10 + D-39)** — `76b0a9e` (feat)
2. **Task 2: Home tile + CookbookList collage thumbnails (PHOTO-11 + PHOTO-13)** — `2bf0ffb` (feat)
3. **Task 3: AiChat canvas surfaces canonical doc.PhotoUrl (PHOTO-12)** — `6d54677` (feat)

## Files Created/Modified

- `src/CookBot.Web/Components/Pages/RecipeView.razor` — Hero photo conditional render with `_heroPhotoFailed` state flag + `HandleHeroPhotoError` Blazor `@onerror` handler; Description lede `<p>` under the title; flag reset alongside `_recipe`/`_doc` in OnParametersSetAsync
- `src/CookBot.Web/Components/Pages/Home.razor` — Tonight-from-your-pantry hero conditional render bound to `heroMatch.PhotoUrl`; Recently-cooked tile conditional render per `r.PhotoUrl`; both wired to `HandlePhotoError(recipeId)` `@onerror`
- `src/CookBot.Web/Components/Pages/Home.razor.cs` — `HomePantryMatch` and `HomeRecentRecipe` records extended with `PhotoUrl: string?`; data-load projections (`madeLog` path AND fallback `recent` query) populate PhotoUrl; `_photoFailedFor` HashSet + `HandlePhotoError` one-shot debounce; flag set cleared at top of `LoadDashboardAsync`
- `src/CookBot.Web/Components/Pages/AiChat.razor` — `RenderRecipeDocument` prepends `<img src="@doc.PhotoUrl">` conditional block at the top of the recipe card; `_aiChatPhotoFailed` + `HandleAiChatPhotoError` one-shot handler; flag reset everywhere `_lastStructuredRecipe = null` (6 sites)
- `src/CookBot.Web/Components/Pages/CookbookList.razor` — 3×2 collage cells render `<img>` when `card.SampledPhotoUrls[i]` is available and the cell has not failed; `SamplePhotoUrls(Cookbook)` static helper takes up to 6 PhotoUrls ordered by RecipeId; `CookbookCardModel` record extended with `SampledPhotoUrls`; `_collageFailedFor` Dictionary<int, HashSet<int>> + `HandleCollagePhotoError(cookbookId, cellIndex)` per-cell tracker; cleared in `LoadCookbooks`

## Decisions Made

See `key-decisions` frontmatter above. Highlights:

- **Canonical-first source for RecipeView hero:** Used `_doc.PhotoUrl` (the canonical RecipeDocument) rather than `_recipe.PhotoUrl` (the entity column). The plan's interfaces section flagged "prefer _doc.PhotoUrl per PROJECT.md canonical-first reads invariant" — confirmed both are in scope at the hero render site, picked `_doc`.
- **Home record shape over side-channel:** Extended the existing `HomePantryMatch` and `HomeRecentRecipe` records with `PhotoUrl: string?` rather than building a parallel `Dictionary<int, string>` lookup. Records already pass through the data-load → markup contract, so colocating the PhotoUrl keeps the projection and consumption in one place.
- **AiChat empty-state semantics:** Render NOTHING when `doc.PhotoUrl` is null on the AI canvas (no placeholder tile). The plan explicitly called this out — the AI may legitimately omit a photo and a striped tile there would be visual noise. Contrast with RecipeView / Home where the empty-state placeholder IS the intended visual.
- **CookbookList projection approach:** No new query needed — the existing `LoadCookbooks` already eager-loads Recipes via `.Include(c => c.Recipes)`. `SamplePhotoUrls(Cookbook)` is a pure in-memory projection over `cb.Recipes` (filter non-null PhotoUrls → order by RecipeId asc → Take(6)). Stable sort matches the QOL-01 convention.
- **POLISH-01 invariant audit (AiChat):** The doc-comment header in AiChat.razor (line 35) reads `POLISH-01: legacy three-tier ExtractRecipeContent extractor stays DELETED` — that's the invariant assertion, not a revival. The plan's `! grep -E 'Extract(PhotoUrl|RecipeContent|StructuredFromText)'` verifier flags this comment as a false positive, but a tighter check (grep for method definitions or call sites: `private.*Extract|void Extract|Task.*Extract|=.*Extract.*\(`) confirms zero extractor methods or call sites exist. POLISH-01 preserved. Documented in the Deviations section below.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] AiChat verifier regex false-positive on POLISH-01 doc-comment**
- **Found during:** Task 3 (AiChat canvas)
- **Issue:** The plan's automated verify includes `! grep -E 'Extract(PhotoUrl|RecipeContent|StructuredFromText)' src/CookBot.Web/Components/Pages/AiChat.razor` to enforce the POLISH-01 no-extractor-revival invariant. The pre-Phase-9 doc-comment header (line 35) intentionally contains the string `ExtractRecipeContent` — it asserts the extractor stays DELETED. The regex matches this comment string and would fail the gate even though no extractor method or call site exists.
- **Fix:** Verified POLISH-01 with a tighter check that scopes to code, not comments: `grep -nE 'private.*Extract|void Extract|Task.*Extract|=.*Extract.*\(' src/CookBot.Web/Components/Pages/AiChat.razor` — returns zero matches. The doc-comment is preserved verbatim because it is the load-bearing invariant assertion (the comment IS the documented guard). Documented the false positive in this SUMMARY so future verifier work can refine the regex.
- **Files modified:** none (verifier scope clarification only)
- **Verification:** `grep -nE 'private.*Extract|void Extract|Task.*Extract|=.*Extract.*\(' src/CookBot.Web/Components/Pages/AiChat.razor` returns no matches
- **Committed in:** 6d54677 (Task 3 commit) — included as documentation in the commit body

---

**Total deviations:** 1 verifier-regex scope clarification (no code change)
**Impact on plan:** No code drift; POLISH-01 invariant fully preserved; the deviation is a verification-process refinement rather than an implementation change.

## Issues Encountered

None. All three tasks executed cleanly in linear order.

Note: `dotnet test` reports 6 pre-existing failures in `AiRecipeFixtureTests` and `PromptInjectionResistanceTests` — those tests require a live `ANTHROPIC_API_KEY` environment variable and fail outside of the API-integration environment. They are NOT regressions from this plan; the full test suite was filtered to exclude them and the remaining 279 unit/integration tests pass with the changes applied.

## Output Disclosures (per plan output spec)

- **RecipeView hero source variable:** `_doc.PhotoUrl` (canonical document via RecipeDocument.CanonicalDocumentJson deserialized at OnAfterRenderAsync). Canonical-first invariant honored.
- **Home.razor.cs data shape:** `HomePantryMatch` and `HomeRecentRecipe` records were extended with a `PhotoUrl: string?` field rather than adding a separate dictionary. The data-load methods (`BuildPantryMatchesAsync` and the recently-cooked projection — both madeLog and fallback paths) now populate PhotoUrl in their record constructors.
- **CookbookList projection approach:** No new query. Used the existing `.Include(c => c.Recipes)` from `LoadCookbooks` and added a pure in-memory `SamplePhotoUrls(Cookbook)` projection that orders by RecipeId asc, filters non-null PhotoUrls, takes 6.
- **AiChat POLISH-01 audit:** The `doc.PhotoUrl` read inside `RenderRecipeDocument` is a single direct property access on the `RecipeDocument doc` parameter (which is the cast result of `_lastStructuredRecipe.Value`). There is NO extractor, NO helper, NO projection from rendered text. The only `ExtractRecipeContent` string in the file is the doc-comment header (line 35) asserting the extractor stays DELETED — that's the invariant guard, not a revival.

## Next Phase Readiness

- All five read surfaces ready for the photo composite editor (09-04) to write into — when a user sets a PhotoUrl via the editor, every read surface will pick it up on the next reload without further work.
- AiChat surface ready for 09-05's AnthropicAiService AI-emitted PhotoUrl plumbing — the canvas already reads `doc.PhotoUrl` directly; once 09-05 ensures the AI returns PhotoUrl in its structured output, it will appear in the canvas with no further UI change.
- The Blazor state-flag pattern is now established across 4 surfaces and is the canonical PITFALL H4 mitigation for any future photo-rendering UI in this codebase.

## Self-Check: PASSED

- File `src/CookBot.Web/Components/Pages/RecipeView.razor`: FOUND (modified, build green)
- File `src/CookBot.Web/Components/Pages/Home.razor`: FOUND (modified, build green)
- File `src/CookBot.Web/Components/Pages/Home.razor.cs`: FOUND (modified, build green)
- File `src/CookBot.Web/Components/Pages/AiChat.razor`: FOUND (modified, build green)
- File `src/CookBot.Web/Components/Pages/CookbookList.razor`: FOUND (modified, build green)
- Commit `76b0a9e` (Task 1 RecipeView): FOUND in git log
- Commit `2bf0ffb` (Task 2 Home + CookbookList): FOUND in git log
- Commit `6d54677` (Task 3 AiChat): FOUND in git log
- `dotnet build src/CookBot.Web/CookBot.Web.csproj` → 0 Warnings, 0 Errors
- `dotnet test` (filtered for non-API tests) → 279 passed, 0 failed
- Every `<img>` grep'd carries `referrerpolicy="no-referrer"` AND `loading="lazy"` → confirmed across all 4 .razor files
- POLISH-01 invariant (no extractor methods or call sites in AiChat) → confirmed via tighter regex check

---
*Phase: 09-photos-prod-ready-infrastructure*
*Completed: 2026-05-16*
