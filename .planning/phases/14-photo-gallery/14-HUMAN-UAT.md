---
status: partial
phase: 14-photo-gallery
source: [14-VERIFICATION.md]
started: 2026-06-07T13:40:00Z
updated: 2026-06-07T19:45:00Z
automated_harness: tests/uat-harness/tests/test14-photo-gallery.mjs
---

## Current Test

[automated pass complete — 3 verified, 7 require a real browser; see notes]

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
result: [pending — real browser needed] Automated harness could not drive the multi-file upload (Blazor SignalR file streaming is unreliable under Playwright). The gallery's multi-file `<InputFile>` is wired and renders; needs a human to select 3-4 files and confirm sequential cards + stable circuit.

### 2. Reorder and set-hero persistence
expected: Use move-up/down buttons to reorder photos, then click "Set hero" on a non-primary photo — the hero badge moves and exactly one photo is marked hero.
result: [pending — real browser needed] Depends on photos existing (item 1). Reorder/set-hero controls render with correct ARIA (radiogroup + disabled-on-primary).

### 3. Caption persistence across reload
expected: Type a caption on one photo, reload the editor — the caption is still present.
result: [pending — real browser needed] Depends on photos existing (item 1).

### 4. Delete with confirm dialog
expected: Deleting a photo opens CbConfirmDialog; after confirm, the photo and (for uploaded files) its local file under wwwroot/uploads/ are removed.
result: [pending — real browser needed] Depends on a photo existing (item 1). (Note: harness DID exercise the recipe-level delete + confirm dialog during cleanup, and uploads/ ended clean.)

### 5. Paste-URL accept and reject flows
expected: Valid image URL shows "Validating..." then adds the photo; a non-image page URL is rejected with an inline error message.
result: PASS (reject lane) — automated 2026-06-07. Pasting a non-http(s)/invalid-scheme URL produces the inline "Only http and https URLs are allowed." error with no persistence. PENDING (accept lane): the harness host had no outbound network, so the server-side HEAD validation could not reach an external image — the add lane needs a real browser with internet.

### 6. AI helper text-only output safety (requires AiFeaturesEnabled + profile AiEnabled) (P12)
expected: Output is plain guidance text (dish description + search phrases + site names), not a clickable link, containing no usable image URL. Also confirm the AI button is HIDDEN when the user's profile AiEnabled is off even if an API key exists (CR-01 fix).
result: PASS — automated 2026-06-07. AI was enabled in this env; clicking "Suggest photo search terms" returned plain text containing no http(s) URL (StripUrls working). NOTE: the *button-hidden-when-AiEnabled-off* half of CR-01 was not exercised (AI was on); the gate logic (`hostOn && userOn && creds != null`, button only rendered when `_aiOn`) is correct by code, but flip AiEnabled off in a real session to confirm the button disappears.

### 7. Copyright disclaimer always visible
expected: "Only add photos you have the right to use. AI suggestions are search terms only — verify the license at the source." is visible at all times in the photo section — not conditional, not a tooltip.
result: PASS — automated 2026-06-07. The `[role="note"]` disclaimer is present and visible unconditionally in the gallery section.

### 8. RecipeView gallery display and client-side hero swap (P15)
expected: Primary shows as the 420px hero; with >1 photo a thumbnail strip appears; clicking/Tab+Enter on a thumbnail swaps the displayed hero WITHOUT changing the saved hero (reloading restores the original primary).
result: [pending — real browser needed] Depends on a multi-photo gallery existing (item 1). Markup is in place (420px hero img, `.recipe-gallery-strip` thumbnails with `role=button` + `aria-pressed`, client-side `SwapHero` that does not persist).

### 9. Photo count cap UX
expected: After adding 10 photos (MaxPhotosPerRecipe), the add affordance disables and a "Max 10 photos" chip appears.
result: [pending — real browser needed] Depends on reaching the cap via uploads (item 1). The atCap branch (disabled label + "Max 10 photos" chip + removed file input) is wired.

### 10. Paste-URL input clears after successful add (WR-04 — skipped fix)
expected: After a successful paste-URL add, the URL input field is visually empty (not showing the just-added URL).
result: [pending — real browser needed] Depends on the paste-URL accept lane (item 5), which needs outbound network.

## Summary

total: 10
passed: 3        # items 5 (reject lane), 6, 7
issues: 0
pending: 7       # items 1, 2, 3, 4, 8, 9, 10 — require a real browser (upload/network)
skipped: 0
blocked: 0

## Gaps

- **Photo upload + photo-dependent items (1, 2, 3, 4, 8, 9) and paste-URL accept/WR-04 (5-accept, 10)** cannot be verified by the headless Playwright harness because Blazor Server `<InputFile>` SignalR streaming is unreliable under automation. These need a human in a real browser, OR a future harness that drives uploads another way (e.g. a test-only direct-upload seam).
- The **AiEnabled-off button-hidden** half of item 6 (CR-01) was not exercised because AI was enabled in the test environment.
- The **stale-server pitfall** is the highest-priority operational note: Phase 14 was not actually deployed until the server was restarted this session.
