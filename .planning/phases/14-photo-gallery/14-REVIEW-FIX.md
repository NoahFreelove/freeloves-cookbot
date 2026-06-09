---
phase: 14-photo-gallery
fixed_at: 2026-06-07T00:00:00Z
review_path: .planning/phases/14-photo-gallery/14-REVIEW.md
iteration: 1
findings_in_scope: 6
fixed: 5
skipped: 1
status: partial
---

# Phase 14: Code Review Fix Report

**Fixed at:** 2026-06-07T00:00:00Z
**Source review:** .planning/phases/14-photo-gallery/14-REVIEW.md
**Iteration:** 1

**Summary:**
- Findings in scope: 6 (CR-01, WR-01, WR-02, WR-03, WR-04, WR-05)
- Fixed: 5 (CR-01, WR-01, WR-02, WR-03, WR-05)
- Skipped: 1 (WR-04)

## Fixed Issues

### CR-01: Gallery AI helper bypasses the per-user AiEnabled toggle

**Files modified:** `src/CookBot.Web/Components/Pages/RecipeEditorParts/RecipePhotoGalleryManager.razor`
**Commit:** 17d2851
**Applied fix:** Injected `CurrentUserService` into the gallery manager and rewrote `OnAfterRenderAsync` to mirror `RecipeEditor.razor:527-530` exactly: `var user = await UserService.GetCurrentUserAsync(); var hostOn = ...; var userOn = user?.Profile?.AiEnabled ?? false; _aiOn = hostOn && userOn && creds is not null`. Previously the gate was `hostOn && creds is not null`, skipping the per-user toggle entirely. A user with `AiEnabled=false` but a configured key can no longer see or invoke the AI button.

---

### WR-02: AddPhotoAsync performs no server-side URL validation

**Files modified:** `src/CookBot.Infrastructure/Services/RecipePhotoService.cs`, `tests/CookBot.Tests/Services/RecipePhotoServiceTests.cs`
**Commit:** 818eeed
**Applied fix:** Added `RecipePhotoUrlValidator` as a new constructor dependency of `RecipePhotoService`. In `AddPhotoAsync`, non-`/uploads/` URLs are now validated server-side: scheme must be http/https (reusing `RecipePhotoUrlValidator.TryValidate`) and length must not exceed 2048 chars (column max). `/uploads/` paths from the upload pipeline are allow-listed by prefix. All three inline test constructors updated; new WR-02 theory tests added for `javascript:`, `data:`, `file:`, `ftp:` schemes, over-length URLs, and `/uploads/` passthrough.

---

### WR-05: AI URL-stripping splits on single space only — tab/newline-delimited URLs survive

**Files modified:** `src/CookBot.Web/Components/Pages/RecipeEditorParts/RecipePhotoGalleryManager.razor`
**Commit:** 2de1aaf
**Applied fix:** Changed `text.Split(' ')` to `text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)` in `StripUrls`. The null `char[]` overload splits on all whitespace (spaces, tabs, newlines), so newline-delimited URLs from the AI's "one per line" output are now isolated as tokens and stripped. Comment updated to document the WR-05 rationale.

---

### WR-01: Paste-URL fires an outbound HTTP HEAD on every keystroke

**Files modified:** `src/CookBot.Web/Components/Pages/RecipeEditorParts/RecipePhotoGalleryManager.razor`
**Commit:** a49bcbc
**Applied fix:** Added `DebounceOnInput="true"` to the paste-URL `<CbInput>`. `CbInput.HandleInput` returns early when `DebounceOnInput` is true, so `ValueChanged` (which triggers `OnUrlPasted` and the two-step HEAD validation) now fires only on blur/change — once per settled input, not once per keystroke. Eliminates the per-keystroke outbound HEAD burst and the associated race where multiple in-flight validations could clobber `_urlError`.

---

### WR-03: SetPrimaryAsync/DeleteAsync clear-then-set is not transactional

**Files modified:** `src/CookBot.Infrastructure/Services/RecipePhotoService.cs`
**Commit:** a957efd
**Applied fix:** Both the `SetPrimaryAsync` clear+set pair and the promotion path in `DeleteAsync` are now wrapped in `BeginTransactionAsync` / `CommitAsync`. The two writes (bulk `ExecuteUpdateAsync` clear + `SaveChangesAsync` set) now commit atomically, preventing zero-primary state if the process is interrupted between them. In the `DeleteAsync` concurrent-delete edge case (winner row is null after re-fetch), the transaction is explicitly rolled back and a warning is logged rather than silently skipping the promotion.

---

## Skipped Issues

### WR-04: Clearing _urlInput does not reset the bound CbInput after a successful paste-add

**File:** `src/CookBot.Web/Components/Pages/RecipeEditorParts/RecipePhotoGalleryManager.razor:402-416`
**Reason:** This is a UI/rendering correctness issue that requires browser testing to verify the fix works correctly. The underlying cause (parent setting `_urlInput = string.Empty` doesn't reliably re-push to the child) could be addressed by switching to `@bind-Value` semantics or adding a component key, but verifying the correct approach without being able to run the browser is high-risk (could introduce a different regression in the input binding). Flagged for human verification and browser-side confirmation.
**Original issue:** On successful paste-URL add, `_urlInput = string.Empty` is set but `CbInput` may not visually clear because Blazor won't re-push a parameter that appears unchanged. The residual URL text in the field can trigger a duplicate-add on the next blur.

---

_Fixed: 2026-06-07T00:00:00Z_
_Fixer: Claude (gsd-code-fixer)_
_Iteration: 1_
