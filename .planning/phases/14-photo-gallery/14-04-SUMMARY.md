---
phase: 14-photo-gallery
plan: "04"
subsystem: ui
tags: [blazor, photo-gallery, file-upload, signalr, ai-helper, css]

# Dependency graph
requires:
  - phase: 14-photo-gallery plan 01
    provides: RecipePhoto entity, LocalRecipePhotoStorage, RecipePhotoUrlValidator
  - phase: 14-photo-gallery plan 02
    provides: PhotoUrlHeadValidator (HEAD-validates pasted URLs)
  - phase: 14-photo-gallery plan 03
    provides: RecipePhotoService (CRUD, one-primary invariant, cap, ownership, file cleanup)

provides:
  - RecipePhotoGalleryManager.razor — multi-photo editor component (sequential upload, reorder, caption, set-hero, delete-with-confirm, paste-URL HEAD-validated, gated AI search-term helper, copyright disclaimer)
  - RecipeView.razor gallery — primary-as-hero 420px + hardened thumbnail strip with client-side-only hero swap + caption display
  - RecipeEditor.razor integration — RecipePhotoComposite replaced by RecipePhotoGalleryManager
  - Two new CSS classes (.recipe-gallery-strip, .photo-manager-grid) in cookbot-design.css

affects: [phase-15-nutrition, phase-16-uat]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Sequential per-file await foreach with GetMultipleFiles(remaining) cap prevents SignalR circuit overload (P14)"
    - "Immediate-persist mutation model — every gallery action calls the service directly; no batch-with-save staging"
    - "Display-layer never mutates canonical — SwapHero sets only _displayedPhotoId; IsPrimary unchanged (P15)"
    - "One-shot @onerror via HashSet<int> _failedPhotoIds prevents infinite error-handler loops on broken images"
    - "AI URL-strip: Uri.TryCreate word-by-word scan strips any http(s) URL from AI output before display (P12)"

key-files:
  created:
    - src/CookBot.Web/Components/Pages/RecipeEditorParts/RecipePhotoGalleryManager.razor
  modified:
    - src/CookBot.Web/Components/Pages/RecipeEditor.razor
    - src/CookBot.Web/Components/Pages/RecipeView.razor
    - src/CookBot.Web/wwwroot/css/cookbot-design.css

key-decisions:
  - "Immediate-persist mutation model chosen (no batch staging) — consistent with RESEARCH §Open Questions resolution; each gallery action (AddPhoto, SetPrimary, ReorderAsync, UpdateCaption, DeleteAsync) calls the service and refreshes _photos immediately"
  - "AI helper output rendered as plain text only — Uri.TryCreate word-by-word strip + '(URL removed — AI suggestions are search terms only)' append enforces P12 at the render boundary"
  - "T3 (human-verify checkpoint) auto-approved under --auto chain; browser UAT deferred to phase HUMAN-UAT.md"

patterns-established:
  - "Photo grid card pattern: hardened img (referrerpolicy=no-referrer, loading=lazy, one-shot @onerror) + caption input + move-up/down + set-hero radio + delete-with-confirm"
  - "role=radiogroup + role=radio + aria-checked wraps set-hero buttons for screen reader semantics"
  - "Gallery strip: thumbnail strip (role=button, tabindex=0, aria-pressed, Enter/Space keyboard) only renders when _photos.Count > 1"

requirements-completed: [GALLERY-02, GALLERY-03, GALLERY-04]

# Metrics
duration: continuation (T1+T2 previously committed; this run: SUMMARY + state only)
completed: "2026-06-07"
---

# Phase 14 Plan 04: Gallery UI Summary

**Full photo gallery UI shipped: multi-upload editor manager with reorder/caption/set-hero/delete/paste-URL HEAD-validation/gated AI search-term helper/copyright disclaimer, plus RecipeView hero+strip with client-side hero swap — closing GALLERY-02, GALLERY-03, GALLERY-04**

## Performance

- **Duration:** T1+T2 committed in prior execution; this continuation: SUMMARY + state
- **Started:** T1 commit d495544, T2 commit 7c5c7cb
- **Completed:** 2026-06-07
- **Tasks:** 2 of 3 code tasks (T3 = human-verify checkpoint, auto-approved — see below)
- **Files modified:** 4

## Accomplishments

- `RecipePhotoGalleryManager.razor` (616 lines): sequential multi-file upload loop with per-file pre-stream 10MB check, per-file try/catch, and GetMultipleFiles(remaining) cap; move-up/down reorder calling ReorderAsync; caption CbInput firing UpdateCaptionAsync; set-hero radio-group (role=radiogroup/radio/aria-checked) calling SetPrimaryAsync; delete-with-confirm (CbConfirmDialog + DeleteAsync); paste-URL flow (scheme check via RecipePhotoUrlValidator then HEAD-validation via PhotoUrlHeadValidator then AddPhotoAsync); gated AI helper (rendered only when host AiFeaturesEnabled && user AiEnabled) with Uri.TryCreate URL-strip before display; always-visible copyright disclaimer (role=note).
- `RecipeView.razor` gallery: loads `_photos` via RecipePhotoService.GetPhotosAsync; renders primary as 420px hardened hero with referrerpolicy=no-referrer/loading=lazy/one-shot @onerror; thumbnail strip (`.recipe-gallery-strip`) only when _photos.Count > 1 with role=button/tabindex=0/aria-pressed/keyboard Enter+Space handling; SwapHero sets only `_displayedPhotoId` (never calls SetPrimaryAsync — display layer never mutates canonical, P15); caption displayed below hero when non-empty; StripedPlaceholder fallback when empty or all errored.
- `RecipeEditor.razor`: replaced `<RecipePhotoComposite>` with `<RecipePhotoGalleryManager RecipeId="@_recipeId" UserId="@_userId" />`; dropped old single-photo `_photoUrl`/`PhotoUrlChanged` binding.
- Two CSS classes added to `cookbot-design.css`: `.recipe-gallery-strip` (thumbnail strip container) and `.photo-manager-grid` (photo card grid in editor).

## Task Commits

1. **Task 1: RecipePhotoGalleryManager + editor integration + CSS classes** - `d495544` (feat)
2. **Task 2: RecipeView gallery strip with hero swap + caption** - `7c5c7cb` (feat)
3. **Task 3: Human-verify checkpoint** — auto-approved under --auto chain; browser UAT deferred (see below)

## Files Created/Modified

- `src/CookBot.Web/Components/Pages/RecipeEditorParts/RecipePhotoGalleryManager.razor` — new multi-photo editor manager (616 lines)
- `src/CookBot.Web/Components/Pages/RecipeEditor.razor` — replaced RecipePhotoComposite with RecipePhotoGalleryManager
- `src/CookBot.Web/Components/Pages/RecipeView.razor` — added _photos load, hero from _displayedPhotoId, gallery strip, caption display
- `src/CookBot.Web/wwwroot/css/cookbot-design.css` — two new classes: .recipe-gallery-strip, .photo-manager-grid

## Decisions Made

- Immediate-persist model: every mutation (upload, reorder, caption, set-hero, delete) calls the service and refreshes `_photos` in-place. No batch-with-save staging (RESEARCH §Open Questions #3 resolution).
- AI URL-strip at render boundary: Uri.TryCreate word-by-word scan strips any http(s) URL from AI output, appending "(URL removed — AI suggestions are search terms only)". Enforces P12 without relying on the prompt alone.
- T3 (human-verify checkpoint) auto-approved under --auto chain; browser UAT is deferred to the phase-level HUMAN-UAT.md step (see "Human UAT Pending" section below).

## Deviations from Plan

None — plan executed exactly as written. T3 checkpoint auto-approved per --auto mode.

## Human UAT Pending

Task 3 was a `checkpoint:human-verify` gate that was auto-approved under the `--auto` chain. The following browser verification steps were NOT manually exercised in this execution and MUST be confirmed by the phase-level HUMAN-UAT verifier:

1. **Multi-upload circuit stability (P14):** Select 3-4 image files at once in the editor photo section. Each card should appear in sequence; "Uploading {name}..." status should show per file; the SignalR circuit must not disconnect or blank out.
2. **Reorder + hero:** Use move-up/down to reorder; click "Set hero" on a non-primary photo. Confirm the "Hero" badge moves and exactly one photo is marked hero.
3. **Caption persistence:** Type a caption on one photo; reload the editor; confirm it persisted.
4. **Delete with confirm:** Delete a photo — CbConfirmDialog should appear; after confirm, the photo and (for uploaded files) its local file should be removed.
5. **Paste-URL flow:** Paste a valid image URL (e.g. Unsplash direct image link) — confirm "Validating..." then it adds. Paste a non-image page URL — confirm inline rejection with error message.
6. **AI helper text-only output (P12, requires AiFeaturesEnabled + profile AiEnabled):** Click "Suggest photo search terms" — output should be plain guidance text (dish description + search phrases + site names), not a clickable link, containing no usable image URL.
7. **Copyright disclaimer always visible:** Confirm the disclaimer is visible at all times in the photo section (not conditional, not a tooltip).
8. **RecipeView gallery display:** Open a recipe page — primary shows as the 420px hero; with >1 photo, a thumbnail strip appears; clicking/Tab+Enter on a thumbnail swaps the displayed hero WITHOUT changing the saved hero (view-only, P15).
9. **Photo count cap:** Keep adding until the cap (10) — the add affordance should disable and a "Max N photos" chip should appear.

## Issues Encountered

None — `dotnet build src/CookBot.Web` exits 0 with 0 warnings and 0 errors.

## Known Stubs

None — all gallery data is wired to live `RecipePhotoService` calls. No placeholder or mock data flows to the UI.

## Threat Flags

None — all STRIDE mitigations from the plan threat model (T-14-11 through T-14-SC) are implemented:
- T-14-11 (DoS / circuit): sequential foreach + GetMultipleFiles(remaining) + per-file 10MB pre-stream check
- T-14-12 (AI URL): Uri.TryCreate URL-strip + non-clickable plain-text render
- T-14-13 (malicious upload): LocalRecipePhotoStorage magic-byte sniff reused verbatim per file
- T-14-14 (paste-URL SSRF): scheme allowlist (RecipePhotoUrlValidator) + 2xx+image/* HEAD gate (PhotoUrlHeadValidator)
- T-14-15 (referrer leak): referrerpolicy="no-referrer" on every gallery img

## Next Phase Readiness

- All four GALLERY requirements (GALLERY-01 through GALLERY-04) are covered across plans 14-01 through 14-04.
- Phase 14 is complete pending phase-level HUMAN-UAT.md browser verification (items listed above).
- Phase 15 (Nutrition) can read `Recipe.PhotoUrl` for hero photo wiring in JSON-LD nutrition via Phase 14's `SyncPrimaryPhotoUrlAsync` (established in plan 14-03).

---
*Phase: 14-photo-gallery*
*Completed: 2026-06-07*
