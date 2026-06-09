---
status: partial
phase: 14-photo-gallery
source: [14-VERIFICATION.md]
started: 2026-06-07T13:40:00Z
updated: 2026-06-07T13:40:00Z
---

## Current Test

[awaiting human testing]

## Tests

### 1. Multi-upload circuit stability (P14)
expected: Select 3-4 image files at once in the editor photo section; each card appears in sequence; "Uploading {name}..." status shows per file; the SignalR circuit does not disconnect or blank out.
result: [pending]

### 2. Reorder and set-hero persistence
expected: Use move-up/down buttons to reorder photos, then click "Set hero" on a non-primary photo — the hero badge moves and exactly one photo is marked hero.
result: [pending]

### 3. Caption persistence across reload
expected: Type a caption on one photo, reload the editor — the caption is still present.
result: [pending]

### 4. Delete with confirm dialog
expected: Deleting a photo opens CbConfirmDialog; after confirm, the photo and (for uploaded files) its local file under wwwroot/uploads/ are removed.
result: [pending]

### 5. Paste-URL accept and reject flows
expected: Valid image URL shows "Validating..." then adds the photo; a non-image page URL is rejected with an inline error message.
result: [pending]

### 6. AI helper text-only output safety (requires AiFeaturesEnabled + profile AiEnabled) (P12)
expected: Output is plain guidance text (dish description + search phrases + site names), not a clickable link, containing no usable image URL. Also confirm the AI button is HIDDEN when the user's profile AiEnabled is off even if an API key exists (CR-01 fix).
result: [pending]

### 7. Copyright disclaimer always visible
expected: "Only add photos you have the right to use. AI suggestions are search terms only — verify the license at the source." is visible at all times in the photo section — not conditional, not a tooltip.
result: [pending]

### 8. RecipeView gallery display and client-side hero swap (P15)
expected: Primary shows as the 420px hero; with >1 photo a thumbnail strip appears; clicking/Tab+Enter on a thumbnail swaps the displayed hero WITHOUT changing the saved hero (reloading restores the original primary).
result: [pending]

### 9. Photo count cap UX
expected: After adding 10 photos (MaxPhotosPerRecipe), the add affordance disables and a "Max 10 photos" chip appears.
result: [pending]

### 10. Paste-URL input clears after successful add (WR-04 — skipped fix)
expected: After a successful paste-URL add, the URL input field is visually empty (not showing the just-added URL).
result: [pending]

## Summary

total: 10
passed: 0
issues: 0
pending: 10
skipped: 0
blocked: 0

## Gaps
