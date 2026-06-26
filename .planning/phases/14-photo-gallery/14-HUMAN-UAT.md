---
status: complete
phase: 14-photo-gallery
source: [14-VERIFICATION.md]
started: 2026-06-07T13:40:00Z
updated: 2026-06-25T00:00:00Z
automated_harness: tests/uat-harness/tests/test14-photo-gallery.mjs
---

## Current Test

[UAT complete 2026-06-25 — all 10 items PASS; 4 gaps + 1 change request diagnosed AND FIXED 2026-06-25 (see Resolution)]

## Automation note (2026-06-07)

An automated Playwright module was added to the UAT harness for this phase:
`tests/uat-harness/tests/test14-photo-gallery.mjs` (runs under `npm test` in
`tests/uat-harness/`). It creates a dedicated throwaway recipe, exercises the gallery,
then deletes the recipe and unlinks its upload files (idempotent; never touches seeded
recipes).

**Critical environment finding:** the dev server running on :7000 at the start of this
session was a **stale build from before the Phase 14 merge** — it served the old
single-hero photo composite and had no `RecipePhotos` table (the
`20260607124611_AddRecipePhotosTable` migration had never been applied). The server was
restarted, which built the current code and applied the migration. Only then did the
gallery render at all. **If you redeploy, restart the server so Phase 14 is actually live.**

**Harness coverage limitation:** Blazor Server's `<InputFile>` streams file bytes over the
SignalR circuit via JS interop. Under Playwright (headless *and* headed) that streaming is
unreliable — uploads almost always deliver a short/empty read or a canceled stream. So the
harness **cannot dependably drive photo uploads**, and every item that needs a photo to
exist is recorded SKIP rather than guessed. This matches 14-VERIFICATION.md's
"why_human: requires a real browser" rationale for these items. A single upload was
observed succeeding once, confirming the upload pipeline itself is functional.

## Tests

### 1. Multi-upload circuit stability (P14)
expected: Select 3-4 image files at once in the editor photo section; each card appears in sequence; "Uploading {name}..." status shows per file; the SignalR circuit does not disconnect or blank out.
result: PASS — human-verified 2026-06-25. Selected 3-4 files at once; cards appeared and the SignalR circuit stayed live (no blank-out/disconnect). The per-file "Uploading {name}..." status was too brief to observe because uploads completed near-instantly (same too-fast-to-assert nature as the harness timing limitation) — not a failure.

### 2. Reorder and set-hero persistence
expected: Use move-up/down buttons to reorder photos, then click "Set hero" on a non-primary photo — the hero badge moves and exactly one photo is marked hero.
result: PASS — human-verified 2026-06-25. Move-up/down reordered photos; "Set hero" moved the hero badge to the chosen non-primary photo (exactly one hero). NOTE: surfaced a separate integration issue — the cookbook listing page shows no hero thumbnail (see Gaps: cookbook-listing-hero).

### 3. Caption persistence across reload
expected: Type a caption on one photo, reload the editor — the caption is still present.
result: PASS — human-verified 2026-06-25. Caption typed on a photo persisted across an editor reload.

### 4. Delete with confirm dialog
expected: Deleting a photo opens CbConfirmDialog; after confirm, the photo and (for uploaded files) its local file under wwwroot/uploads/ are removed.
result: PASS — human-verified 2026-06-25. Clicking delete opens the confirm dialog (no instant delete); confirming removes the photo. On-disk: wwwroot/uploads/ shows only the remaining photos + .gitkeep, no obvious orphan (the harness already verified the file-unlink path). NOTE: surfaced a separate layout issue — the trash buttons are sometimes covered by neighboring cards and hard/impossible to click (see Gaps: gallery-trash-overlap).

### 5. Paste-URL accept and reject flows
expected: Valid image URL shows "Validating..." then adds the photo; a non-image page URL is rejected with an inline error message.
result: PASS (both lanes) — reject lane automated 2026-06-07; accept lane human-verified 2026-06-25. Pasting https://www.gstatic.com/webp/gallery/1.jpg showed "Validating…" then added the photo via server HEAD validation. Reject lane: non-http(s)/invalid-scheme URL gives the inline "Only http and https URLs are allowed." error with no persistence.

### 6. AI helper text-only output safety (requires AiFeaturesEnabled + profile AiEnabled) (P12)
expected: Output is plain guidance text (dish description + search phrases + site names), not a clickable link, containing no usable image URL. Also confirm the AI button is HIDDEN when the user's profile AiEnabled is off even if an API key exists (CR-01 fix).
result: PASS — automated 2026-06-07. AI was enabled in this env; clicking "Suggest photo search terms" returned plain text containing no http(s) URL (StripUrls working). NOTE: the *button-hidden-when-AiEnabled-off* half of CR-01 was not exercised (AI was on); the gate logic (`hostOn && userOn && creds != null`, button only rendered when `_aiOn`) is correct by code, but flip AiEnabled off in a real session to confirm the button disappears.

### 7. Copyright disclaimer always visible
expected: "Only add photos you have the right to use. AI suggestions are search terms only — verify the license at the source." is visible at all times in the photo section — not conditional, not a tooltip.
result: PASS — automated 2026-06-07. The `[role="note"]` disclaimer is present and visible unconditionally in the gallery section.

### 8. RecipeView gallery display and client-side hero swap (P15)
expected: Primary shows as the 420px hero; with >1 photo a thumbnail strip appears; clicking/Tab+Enter on a thumbnail swaps the displayed hero WITHOUT changing the saved hero (reloading restores the original primary).
result: PASS — human-verified 2026-06-25. Hero renders large with a thumbnail strip; clicking a thumbnail swaps the displayed hero, and reload restores the saved primary (display-only swap confirmed).

### 9. Photo count cap UX
expected: After adding 10 photos (MaxPhotosPerRecipe), the add affordance disables and a "Max 10 photos" chip appears.
result: PASS — human-verified 2026-06-25. At 10 photos the add affordance disables and the "Max 10 photos" chip shows. NOTE: surfaced an over-cap edge case — picking a batch that exceeds the remaining slots (e.g. 8 photos + select 4) silently no-ops with no feedback (see Gaps: gallery-overcap-batch-noop).

### 10. Paste-URL input clears after successful add (WR-04 — skipped fix)
expected: After a successful paste-URL add, the URL input field is visually empty (not showing the just-added URL).
result: PASS — human-verified 2026-06-25. After the successful gstatic URL add, the URL input cleared (empty, not showing the pasted URL). NOTE: surfaced a stale-error issue on the FAILURE path — a "Maximum 10 photos" error left over from an at-cap paste is not cleared after deleting a photo (see Gaps: gallery-stale-urlerror-after-delete).

## Summary

total: 10
passed: 10       # all enumerated items pass (1-10); item 5 both lanes, item 6 (AI text-only) — see change request below
issues: 4        # cookbook-listing-hero, gallery-trash-overlap, gallery-overcap-batch-noop, gallery-stale-urlerror-after-delete
change_requests: 1  # remove the "Suggest photo search terms" AI helper (user: not useful)
pending: 0
skipped: 0
blocked: 0

## Gaps

- truth: "A recipe with a set hero photo shows that hero as its thumbnail on the cookbook listing (/cookbooks/{id})"
  status: failed
  reason: "User reported: on /cookbooks/{id} the hero doesn't show up — no images show, just striped placeholders, even after setting a gallery hero."
  severity: minor
  test: cross-cutting (discovered during item 2; not one of the 10 enumerated items)
  root_cause: "CookbookDetail.razor:108 renders `<StripedPlaceholder Width=80 Height=80 .../>` unconditionally for every recipe row. It never reads any hero source — neither the legacy `Recipe.PhotoUrl` nor the new Phase-14 `RecipePhoto` hero. Pre-existing hardcoded placeholder (predates Phase 14), so not a Phase 14 regression — but the gallery hero is not surfaced on the cookbook listing."
  artifacts:
    - path: "src/CookBot.Web/Components/Pages/CookbookDetail.razor"
      issue: "line ~108 hardcodes StripedPlaceholder instead of rendering the recipe hero photo when one exists"
  missing:
    - "Render the recipe hero (RecipePhoto hero → fallback Recipe.PhotoUrl) as the 80×80 thumbnail when present; fall back to StripedPlaceholder only when the recipe has no photo"
  note: "Likely out of Phase 14's original GALLERY-01..04 scope (gallery lived on editor + RecipeView). Candidate polish item; same hardcoded-placeholder pattern probably exists on other recipe-list surfaces (Home.razor, CookbookList.razor) — check those if fixing."

- truth: "Every photo card's action buttons (move up/down, set hero, delete) are fully clickable"
  status: failed
  reason: "User reported: the trash icons are sometimes impossible to press because they are covered by neighboring image cards."
  severity: minor
  test: 4 (discovered while verifying delete)
  root_cause: "RecipePhotoGalleryManager.razor: each photo card is fixed `width:180px` (≈148px inner after 16px padding), but the action-button row (`<div style=display:flex;gap:8px>`, no flex-wrap) holds Move-up(36) + Move-down(36) + Set-hero(~75) + Delete(36) + gaps(24) ≈ 207px. The row overflows the card's right edge; with `.photo-manager-grid { display:flex; flex-wrap:wrap; gap:16px }` the overflowing trash button lands in the inter-card gap and the next card paints over it → unclickable. Delete itself works when reachable."
  artifacts:
    - path: "src/CookBot.Web/Components/Pages/RecipeEditorParts/RecipePhotoGalleryManager.razor"
      issue: "action-button row (~line 94) is a non-wrapping flex whose buttons exceed the 180px card inner width and overflow into the neighboring card"
  missing:
    - "Add `flex-wrap:wrap` to the action-button row so controls stay inside the card; and/or widen the card / shrink the 'Set hero' button footprint so the row fits within 148px"

- truth: "Selecting more files than remaining cap slots adds the ones that fit and tells the user the rest were skipped"
  status: failed
  reason: "User reported: if you have 8 and try to add 4 it just nops, doesn't say anything."
  severity: minor
  test: 9 (discovered while verifying the cap)
  root_cause: "RecipePhotoGalleryManager.razor OnMultipleFilesPicked (line ~321): `foreach (var file in e.GetMultipleFiles(remaining))` calls GetMultipleFiles in the loop header, OUTSIDE the per-file try/catch (which only wraps the body, lines ~333-352). Blazor's InputFileChangeEventArgs.GetMultipleFiles(max) THROWS InvalidOperationException when the selection count exceeds `max`. With 8 photos remaining=2 and 4 files picked, it throws before the body runs; the exception escapes uncaught → the entire batch is dropped (even the 2 that would fit) with no toast → silent no-op."
  artifacts:
    - path: "src/CookBot.Web/Components/Pages/RecipeEditorParts/RecipePhotoGalleryManager.razor"
      issue: "GetMultipleFiles(remaining) at ~line 321 throws-and-escapes when selection > remaining; no over-cap user feedback"
  missing:
    - "Read with a generous max (e.g. GetMultipleFiles(cap)) then Take(remaining), OR wrap GetMultipleFiles in try/catch; and show a toast like 'Only N more photos can be added — M were skipped' so the over-cap batch is not silently lost"

- truth: "After deleting a photo while at the cap, the paste-URL field no longer shows a 'Maximum N photos' error and a URL can be added"
  status: failed
  reason: "User reported: at 10/10 the paste-URL add properly refuses; after deleting one (now 9/10) it STILL says you have max photos."
  severity: minor
  test: 10 (discovered while verifying the paste-URL accept lane)
  root_cause: "Stale UI error, NOT a real cap block. The server path is correct — RecipePhotoService.DeleteAsync (line ~230) commits via SaveChangesAsync, and AddPhotoAsync re-counts fresh (CountAsync, line ~99), so adding at 9/10 would succeed. But in RecipePhotoGalleryManager.razor, the at-cap paste failure sets `_urlError = 'Maximum N photos per recipe.'` (OnUrlPasted catch, line ~416) and leaves `_urlInput` populated (failure path does not clear it). DeletePhotoAsync (and the other photo-count mutations) never reset `_urlError`, so the stale message persists at 9/10. The user can't easily retry either: CbInput won't re-fire ValueChanged for the unchanged URL still in the field."
  artifacts:
    - path: "src/CookBot.Web/Components/Pages/RecipeEditorParts/RecipePhotoGalleryManager.razor"
      issue: "`_urlError` is not cleared when the photo count changes (DeletePhotoAsync / LoadPhotosAsync); failure path also leaves `_urlInput` populated, blocking a same-URL retry"
  missing:
    - "Clear `_urlError` (at minimum the cap error) whenever the photo set changes — e.g. reset it in DeletePhotoAsync / LoadPhotosAsync — so a delete that frees a slot dismisses the stale 'Maximum N photos' message"

## Change Requests

- id: remove-ai-suggest-photo-search-terms
  request: "User: the 'Suggest photo search terms' feature should be deleted — it's not useful."
  scope: "Remove the AI helper UI + wiring from the photo gallery. Touches: RecipePhotoGalleryManager.razor (the `@if (_aiOn)` block ~lines 202-223, `SuggestSearchTermsAsync`, `_aiOn`/`_aiLoading`/`_aiOutput` state, the AI injects), the prompt/service method behind it, the copyright disclaimer's 'AI suggestions are search terms only' clause (lines ~229-231 — reword once AI helper is gone), and any tests asserting it (item 6 / GALLERY-04, harness test14 items 5-6, prompt-snapshot tests). NOTE: this RETIRES requirement GALLERY-04 / Phase 14 item 6 — record as a scope change, not a regression."
  decision_needed: "Confirm before executing — feature removal spanning UI + service + prompt + tests + a requirement retirement. Recommend folding into the end-of-UAT gap-closure plan."
  resolution: "DONE 2026-06-25 (user confirmed 'Remove it'). Deleted the AI helper UI block, _aiOn/_aiLoading/_aiOutput state, OnAfterRender AI gate, SuggestSearchTermsAsync + StripUrls, and the IAiService/AiApiKeyResolutionService/CurrentUserService injects from RecipePhotoGalleryManager.razor. Reworded the copyright note (dropped the 'AI suggestions are search terms only' clause). test14 item 6 repurposed as a regression guard (button must stay absent) — PASS. GALLERY-04 marked Retired in REQUIREMENTS.md."

## Resolution (2026-06-25 — all gaps fixed in this session)

All four gaps and the change request were fixed directly after the UAT walkthrough (user chose "Fix all 6 now"). Build clean (0 errors); harness green (6 pass / 1 skip / 0 fail).

| Gap | Fix | Verified |
|-----|-----|----------|
| cookbook-listing-hero | CookbookDetail.razor renders `recipe.PhotoUrl` as the 80×80 thumbnail (fallback to StripedPlaceholder). Home/CookbookList already did this. | Playwright: hero img present on `/cookbooks/1` — PASS |
| gallery-trash-overlap | `flex-wrap:wrap` on the action-button row so controls stay inside the 180px card | Playwright trial-click: all 10 trash buttons actionable; row flex-wrap=wrap (80px/2 lines) — PASS |
| gallery-overcap-batch-noop | Read `GetMultipleFiles(e.FileCount)` then `Take(remaining)` + warning toast for the skipped count (no more throw-out-of-loop) | Code (compiled); interaction-gated — user spot-check optional |
| gallery-stale-urlerror-after-delete | Clear `_urlError = null` after a delete frees a slot | Code (compiled); interaction-gated — user spot-check optional |
| CR: AI helper removal | See change-request resolution above | Harness item 6 guard — PASS |


- **Photo upload + photo-dependent items (1, 2, 3, 4, 8, 9) and paste-URL accept/WR-04 (5-accept, 10)** cannot be verified by the headless Playwright harness because Blazor Server `<InputFile>` SignalR streaming is unreliable under automation. These need a human in a real browser, OR a future harness that drives uploads another way (e.g. a test-only direct-upload seam).
- The **AiEnabled-off button-hidden** half of item 6 (CR-01) was not exercised because AI was enabled in the test environment.
- The **stale-server pitfall** is the highest-priority operational note: Phase 14 was not actually deployed until the server was restarted this session.
