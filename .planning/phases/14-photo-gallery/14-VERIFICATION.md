---
phase: 14-photo-gallery
verified: 2026-06-07T13:37:14Z
status: human_needed
score: 12/12 must-haves verified
overrides_applied: 0
human_verification:
  - test: "Multi-upload circuit stability (P14)"
    expected: "Select 3-4 image files at once; each card appears in sequence; 'Uploading {name}...' status shows per file; SignalR circuit does not disconnect or blank out"
    why_human: "Cannot verify SignalR circuit stays connected during real multi-file browser upload via grep/build checks"
  - test: "Reorder and set-hero persistence"
    expected: "Use move-up/down to reorder; click 'Set hero' on a non-primary photo — the hero badge moves and exactly one photo is marked hero in the editor"
    why_human: "UI state rendering and immediate-persist wiring must be exercised via a real browser"
  - test: "Caption persistence across reload"
    expected: "Type a caption on one photo, reload the editor, confirm the caption is still present"
    why_human: "Round-trip persistence requires a live DB write and a fresh page load to verify"
  - test: "Delete with confirm dialog"
    expected: "Deleting a photo opens CbConfirmDialog; after confirm, the photo and its local file are removed"
    why_human: "Dialog flow and file removal require a running app with real uploads"
  - test: "Paste-URL accept and reject flows"
    expected: "Valid image URL: shows 'Validating...' then adds the photo. Non-image page URL: rejected with inline error message"
    why_human: "Requires live outbound HEAD request to an external URL; cannot mock in a grep check"
  - test: "AI helper text-only output safety (requires AiFeaturesEnabled + profile AiEnabled)"
    expected: "Output is plain guidance text (dish description + search phrases + site names), not a clickable link, containing no usable image URL"
    why_human: "Requires a live Anthropic API call and visual inspection of the rendered output"
  - test: "Copyright disclaimer always visible"
    expected: "The disclaimer 'Only add photos you have the right to use. AI suggestions are search terms only — verify the license at the source.' is visible at all times in the photo section, not conditional, not a tooltip"
    why_human: "Visual presence check; markup is correct but visual render requires a browser"
  - test: "RecipeView gallery display and client-side hero swap"
    expected: "Primary shows as 420px hero; with >1 photo a thumbnail strip appears; clicking/Tab+Enter on a thumbnail swaps the displayed hero WITHOUT changing the saved hero (view-only, P15)"
    why_human: "Visual layout and keyboard navigation require a real browser; P15 non-mutation guarantee requires observing no IsPrimary DB change on client-side swap"
  - test: "Photo count cap UX"
    expected: "After adding 10 photos, the add affordance disables and a 'Max N photos' chip appears"
    why_human: "Requires creating 10 photos in-browser; cap UX is a visual/interaction check"
  - test: "WR-04 paste-URL input clears after successful add"
    expected: "After a successful paste-URL add the URL input field is visually empty (not showing the just-added URL)"
    why_human: "Skipped fix from code review — Blazor child-component re-render behavior requires browser verification to confirm the input actually clears"
---

# Phase 14: Photo Gallery Verification Report

**Phase Goal:** A recipe can have a curated gallery of multiple photos with a user-chosen hero, and an AI helper accelerates finding a free-licensed photo without introducing hallucination or copyright risk.
**Verified:** 2026-06-07T13:37:14Z
**Status:** human_needed
**Re-verification:** No — initial verification

## Goal Achievement

### Observable Truths

| #  | Truth | Status | Evidence |
|----|-------|--------|---------|
| 1  | Existing recipes with a hero photo are not disrupted — EF migration backfills `Recipe.PhotoUrl` into a primary `RecipePhoto` row with no data loss | ✓ VERIFIED | `20260607124611_AddRecipePhotosTable.cs` Up() runs `INSERT INTO RecipePhotos ... WHERE PhotoUrl IS NOT NULL AND PhotoUrl != ''`; `RecipePhotoBackfillTests` (2 tests, green) proves losslessness and cascade |
| 2  | `RecipePhoto` entity exists with all required fields | ✓ VERIFIED | `src/CookBot.Domain/Entities/RecipePhoto.cs` contains `Id`, `RecipeId`, `Url`, `Caption`, `SortOrder`, `IsPrimary`, `Recipe Recipe = null!` |
| 3  | FK cascade configured — deleting Recipe removes RecipePhoto rows | ✓ VERIFIED | `RecipePhotoConfiguration.cs` line 37-40: `.OnDelete(DeleteBehavior.Cascade)`; proven by backfill test |
| 4  | `MaxPhotosPerRecipe` setting exists with default 10 | ✓ VERIFIED | `CookBotSettings.cs` line 32: `public int MaxPhotosPerRecipe { get; set; } = 10;` |
| 5  | Multi-photo upload (sequential), reorder (move buttons), captions, set-primary persist via `RecipePhotoService` | ✓ VERIFIED | `RecipePhotoService.cs` has all 6 methods (`GetPhotosAsync`, `AddPhotoAsync`, `SetPrimaryAsync`, `ReorderAsync`, `DeleteAsync`, `UpdateCaptionAsync`); 14 service tests green; sequential `foreach` with `await` per file in `RecipePhotoGalleryManager.razor:321` |
| 6  | One-primary invariant enforced — `SetPrimaryAsync` clears all then sets one atomically | ✓ VERIFIED | `RecipePhotoService.cs:141-165`: `BeginTransactionAsync` wraps `ExecuteUpdateAsync` clear + `SaveChangesAsync` set; WR-03 fix applied at commit a957efd |
| 7  | Server-side cap enforced — `AddPhotoAsync` throws when at `MaxPhotosPerRecipe` | ✓ VERIFIED | `RecipePhotoService.cs:98-101`: `Math.Clamp(_settings.MaxPhotosPerRecipe, 1, 20)` + `throw InvalidOperationException` |
| 8  | Cross-user mutation rejected — ownership check gates every mutation | ✓ VERIFIED | `AssertOwnershipAsync` private helper called at top of every public mutation method; `UnauthorizedAccessException` on `cookbook.UserId != userId` |
| 9  | Deleting a photo or recipe removes the local `/uploads/` file; external URLs leave no file | ✓ VERIFIED | `LocalRecipePhotoStorage.DeletePhysicalFile()` at line 118 guarded by `AssertPathInsideUploadsDirectory`; `RecipeService.DeleteAsync` enumerates photos with `/uploads/` prefix check BEFORE `_recipeRepo.DeleteAsync`; `RecipePhotoService.DeleteAsync` calls `DeletePhysicalFile` before row removal |
| 10 | `Recipe.PhotoUrl` and `CanonicalDocumentJson` are re-synced after every gallery mutation (only `RecipeService.SyncPrimaryPhotoUrlAsync` writes canonical) | ✓ VERIFIED | `SyncPrimaryPhotoUrlAsync` at `RecipeService.cs:317-337` uses `doc with { PhotoUrl = recipe.PhotoUrl }` and `_recipeRepo.UpdateAsync`; gallery rows are NOT written into canonical JSON; `RecipeDocument` does not reference `RecipePhoto` |
| 11 | AI helper is text-only, gated by `AiFeaturesEnabled && UserProfile.AiEnabled` — CR-01 fixed | ✓ VERIFIED | `RecipePhotoGalleryManager.razor:282-286` (commit 17d2851): `userOn = user?.Profile?.AiEnabled ?? false; _aiOn = hostOn && userOn && creds is not null`; `StripUrls` at line 590-623 splits on all whitespace (WR-05 fix, commit 2de1aaf); AI output rendered as plain text inside a `<div>`, not `<a>` tags |
| 12 | Pasted URL is HEAD-validated before persist; scheme allowlist gates step 1 | ✓ VERIFIED | `OnUrlPasted` (line 365-420): `RecipePhotoUrlValidator.TryValidate` (step 1) then `PhotoUrlHeadValidator.ValidateAsync` (step 2) before `AddPhotoAsync`; `DebounceOnInput="true"` prevents per-keystroke HEAD (WR-01 fix, commit a49bcbc); 6 `PhotoUrlHeadValidatorTests` green; server-side re-validation in `AddPhotoAsync` for non-`/uploads/` URLs (WR-02 fix, commit 818eeed) |

**Score:** 12/12 truths verified

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `src/CookBot.Domain/Entities/RecipePhoto.cs` | RecipePhoto POCO with `IsPrimary`, `Recipe` nav | ✓ VERIFIED | Present; all fields including `public bool IsPrimary` |
| `src/CookBot.Infrastructure/Data/Configurations/RecipePhotoConfiguration.cs` | EF config: cascade + composite index | ✓ VERIFIED | `OnDelete(DeleteBehavior.Cascade)` + `HasIndex(p => new { p.RecipeId, p.SortOrder })` |
| `src/CookBot.Infrastructure/Migrations/20260607124611_AddRecipePhotosTable.cs` | Migration with GALLERY-01 backfill | ✓ VERIFIED | `INSERT INTO RecipePhotos ... WHERE PhotoUrl IS NOT NULL AND PhotoUrl != ''` present in `Up()` |
| `src/CookBot.Application/DTOs/CookBotSettings.cs` | `MaxPhotosPerRecipe` with default 10 | ✓ VERIFIED | `public int MaxPhotosPerRecipe { get; set; } = 10;` |
| `src/CookBot.Application/Services/PhotoUrlHeadValidator.cs` | HEAD+405→ranged-GET validator; never throws | ✓ VERIFIED | Present; `AllowAutoRedirect = false`; `TaskCanceledException` → `Timeout`, `HttpRequestException` → `NetworkError`; `CreateClient()` seam for tests |
| `src/CookBot.Infrastructure/Services/RecipePhotoService.cs` | Gallery CRUD with one-primary invariant, cap, ownership | ✓ VERIFIED | All 6 methods; `Math.Clamp`; `BeginTransactionAsync` on clear+set; `AssertOwnershipAsync` |
| `src/CookBot.Web/Services/LocalRecipePhotoStorage.cs` | `DeletePhysicalFile` guarded by path check | ✓ VERIFIED | `DeletePhysicalFile` at line 118 calls `AssertPathInsideUploadsDirectory` before `File.Delete` |
| `src/CookBot.Web/Components/Pages/RecipeEditorParts/RecipePhotoGalleryManager.razor` | Multi-photo manager: sequential upload, reorder, caption, hero, delete, paste-URL HEAD, AI helper, disclaimer | ✓ VERIFIED | Present (616 lines); `OnMultipleFilesPicked` with sequential `foreach`; `role="radiogroup"`; AI gate with `userOn && hostOn`; disclaimer `role="note"` always rendered |
| `src/CookBot.Web/Components/Pages/RecipeView.razor` | Gallery hero + thumbnail strip with hardened img + client-side hero swap | ✓ VERIFIED | `recipe-gallery-strip` class; `GetPhotosAsync` call; `SwapHero` sets only `_displayedPhotoId` |
| `tests/CookBot.Tests/Migration/RecipePhotoBackfillTests.cs` | Backfill losslessness + cascade tests | ✓ VERIFIED | 2 tests, green |
| `tests/CookBot.Tests/Services/PhotoUrlHeadValidatorTests.cs` | 6 unit tests covering all lanes | ✓ VERIFIED | 6 tests, green |
| `tests/CookBot.Tests/Services/RecipePhotoServiceTests.cs` | 14 service behavior tests | ✓ VERIFIED | 14 tests, green (7 original + 7 WR-02 additions) |

### Key Link Verification

| From | To | Via | Status | Details |
|------|----|-----|--------|---------|
| `src/CookBot.Domain/Entities/Recipe.cs` | `RecipePhoto` | `ICollection<RecipePhoto> Photos` navigation | ✓ WIRED | Line 27: `public ICollection<RecipePhoto> Photos { get; set; } = new List<RecipePhoto>();` |
| `src/CookBot.Infrastructure/Data/CookBotDbContext.cs` | `RecipePhoto` | `DbSet<RecipePhoto> RecipePhotos` | ✓ WIRED | Line 34: `public DbSet<RecipePhoto> RecipePhotos => Set<RecipePhoto>();` |
| `src/CookBot.Application/DependencyInjection.cs` | `PhotoUrlHeadValidator` | `AddSingleton<PhotoUrlHeadValidator>()` | ✓ WIRED | Line 30: `services.AddSingleton<PhotoUrlHeadValidator>();` |
| `src/CookBot.Infrastructure/DependencyInjection.cs` | `RecipePhotoService` | `AddScoped<RecipePhotoService>()` | ✓ WIRED | Line 38: `services.AddScoped<RecipePhotoService>();` |
| `src/CookBot.Application/Services/RecipePhotoService.cs` | `RecipeService.SyncPrimaryPhotoUrlAsync` | Called after every gallery mutation | ✓ WIRED | Confirmed in `AddPhotoAsync` (line 120), `SetPrimaryAsync` (line 167), `ReorderAsync` (line 198), `DeleteAsync` (line 279) — not called in `UpdateCaptionAsync` (correct per spec) |
| `src/CookBot.Application/Services/RecipeService.cs` | `LocalRecipePhotoStorage.DeletePhysicalFile` | `DeleteAsync` enumerates local photos before cascade | ✓ WIRED | Line 298: `_photoStorage.DeletePhysicalFile(photo.Url)` inside `/uploads/` guard; file delete BEFORE `_recipeRepo.DeleteAsync` (line 308) |
| `src/CookBot.Web/Components/Pages/RecipeEditorParts/RecipePhotoGalleryManager.razor` | `RecipePhotoService / PhotoUrlHeadValidator / IAiService` | Immediate-persist mutations + HEAD validation + text AI helper | ✓ WIRED | All three injected and called; `OnMultipleFilesPicked`, `OnUrlPasted`, `SuggestSearchTermsAsync` all wired |
| `src/CookBot.Web/Components/Pages/RecipeView.razor` | `RecipePhotoService.GetPhotosAsync` | Loads gallery for display | ✓ WIRED | Line 640: `var photos = await RecipePhotoSvc.GetPhotosAsync(RecipeId, userId)` |
| `src/CookBot.Web/Components/Pages/RecipeEditor.razor` | `RecipePhotoGalleryManager` | Replaces single-photo composite for edit mode | ✓ WIRED | Line 85-86: `<RecipePhotoGalleryManager RecipeId="@_existingRecipe.Id" UserId="@UserService.CurrentUserId.Value" />` inside `_isEdit` guard |

### Data-Flow Trace (Level 4)

| Artifact | Data Variable | Source | Produces Real Data | Status |
|----------|---------------|--------|--------------------|--------|
| `RecipePhotoGalleryManager.razor` | `_photos` | `PhotoService.GetPhotosAsync(RecipeId, UserId)` → `_db.RecipePhotos.Where().OrderBy()` | Yes — EF query against SQLite | ✓ FLOWING |
| `RecipeView.razor` | `_photos`, `_displayedPhotoId` | `RecipePhotoSvc.GetPhotosAsync(RecipeId, userId)` → same EF query | Yes — EF query against SQLite | ✓ FLOWING |
| `RecipeService.SyncPrimaryPhotoUrlAsync` | `recipe.PhotoUrl`, `recipe.CanonicalDocumentJson` | `IsPrimary` photo from `_recipePhotoRepo.FindAsync` | Yes — real DB write via `_recipeRepo.UpdateAsync` | ✓ FLOWING |

### Behavioral Spot-Checks

| Behavior | Command | Result | Status |
|----------|---------|--------|--------|
| Build (0 errors) | `dotnet build src/CookBot.Web` | `0 Warning(s), 0 Error(s)` | ✓ PASS |
| Backfill tests (2 green) | `dotnet test --filter "FullyQualifiedName~RecipePhotoBackfillTests"` | `Passed: 2, Failed: 0` | ✓ PASS |
| HEAD validator tests (6 green) | `dotnet test --filter "FullyQualifiedName~PhotoUrlHeadValidatorTests"` | `Passed: 6, Failed: 0` | ✓ PASS |
| Service behavior tests (14 green) | `dotnet test --filter "FullyQualifiedName~RecipePhotoServiceTests"` | `Passed: 14, Failed: 0` | ✓ PASS |
| Full test suite (445 green, 6 skipped api-key) | `dotnet test --filter "Category!=RequiresApiKey"` | `Passed: 445, Failed: 0` | ✓ PASS |
| Migration name matches SC1 | `ls src/CookBot.Infrastructure/Migrations/ | grep AddRecipePhotosTable` | `20260607124611_AddRecipePhotosTable.cs` | ✓ PASS |

### Requirements Coverage

| Requirement | Source Plan | Description | Status | Evidence |
|-------------|-------------|-------------|--------|---------|
| GALLERY-01 | 14-01-PLAN.md | `RecipePhoto` entity + EF migration backfills `Recipe.PhotoUrl` into primary row; no data loss | ✓ SATISFIED | Migration `20260607124611_AddRecipePhotosTable.cs` backfill SQL; `RecipePhotoBackfillTests` green |
| GALLERY-02 | 14-03-PLAN.md, 14-04-PLAN.md | Multi-upload, reorder, captions, set-primary from editor | ✓ SATISFIED (automated); ? NEEDS HUMAN (browser UX) | Service tests green; `RecipePhotoGalleryManager.razor` wired to all mutations; browser flow requires human check |
| GALLERY-03 | 14-03-PLAN.md, 14-04-PLAN.md | Gallery display in RecipeView; delete removes local file | ✓ SATISFIED (automated); ? NEEDS HUMAN (browser display) | File delete proven by service tests; `RecipeView.razor` gallery wired; visual display requires human |
| GALLERY-04 | 14-02-PLAN.md, 14-04-PLAN.md | AI helper text-only, gated; pasted URL HEAD-validated; copyright disclaimer visible | ✓ SATISFIED (automated); ? NEEDS HUMAN (live AI call, visual disclaimer) | CR-01 fix verified in code; WR-05 strip verified; 6 HEAD validator tests green; disclaimer markup present; live AI call requires human |

### Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
|------|------|---------|----------|--------|
| `src/CookBot.Web/Components/Pages/RecipeView.razor` | 47, 507, 516 | `TODO(AuthMode): gate prerendered JSON-LD per-user...` | ℹ️ Info | Pre-existing from Phase 13 (commit d1bf379); not introduced by Phase 14; tracks a future auth gate, not a correctness gap |
| `src/CookBot.Web/Components/Pages/RecipeEditorParts/RecipePhotoGalleryManager.razor` | 88 | `placeholder="Caption (optional)"` | ℹ️ Info | HTML attribute on input element — not a stub indicator |

No `TBD`, `FIXME`, or `XXX` markers in any Phase 14 files. The `TODO(AuthMode)` markers pre-date this phase and carry a formal scope label (AuthMode); they are not unreferenced debt.

### Hard Invariant Checks

| Invariant | Description | Status | Evidence |
|-----------|-------------|--------|---------|
| P15: Only `RecipeService` writes `CanonicalDocumentJson` | Gallery rows never appear in canonical JSON | ✓ VERIFIED | `RecipeDocument.cs` does not reference `RecipePhoto`; `SyncPrimaryPhotoUrlAsync` writes only `PhotoUrl` mirror; `RecipePhotoService` calls `SyncPrimaryPhotoUrlAsync` but does not touch canonical directly |
| D-14-12: Photos NOT in `.cookbook.json` transfer DTO | `CookbookTransferService` and transfer DTOs untouched | ✓ VERIFIED | No `RecipePhoto` reference in `CookbookTransferService.cs` or `CookbookTransferDto.cs` |
| No new NuGet packages | Zero package additions | ✓ VERIFIED | No `.csproj` file modified in any Phase 14 commit; git log confirms |
| No MudBlazor | Zero MudBlazor references in new files | ✓ VERIFIED | No MudBlazor component references in `RecipePhotoGalleryManager.razor` or Phase 14 files |
| No vision path added to `AnthropicAiService` | AI service unchanged | ✓ VERIFIED | No `vision`, `image_url`, or `Vision` references in `AnthropicAiService.cs`; Phase 14 commits do not modify that file |
| SwapHero never mutates `IsPrimary` | Display-layer never calls `SetPrimaryAsync` or modifies canonical | ✓ VERIFIED | `SwapHero` at `RecipeView.razor:771-775` sets only `_displayedPhotoId`; comment explicitly states "NEVER mutates IsPrimary / canonical — P15" |

### Human Verification Required

**10 items require human/browser verification.** These were explicitly flagged in the Plan 14-04 Task 3 (`checkpoint:human-verify`) and auto-approved during execution; the code paths are implemented correctly but behavioral/visual confirmation is deferred.

#### 1. Multi-upload circuit stability (P14)

**Test:** Select 3-4 image files at once in the editor photo section.
**Expected:** Each card appears in sequence; "Uploading {name}..." status shows per file; the SignalR circuit does not disconnect or blank out.
**Why human:** SignalR circuit stability under real file upload is not verifiable by code analysis.

#### 2. Reorder and set-hero persistence

**Test:** Use move-up/down buttons to reorder photos, then click "Set hero" on a non-primary photo.
**Expected:** Hero badge moves and exactly one photo is marked hero.
**Why human:** UI state rendering and immediate-persist wiring must be exercised via a real browser.

#### 3. Caption persistence across reload

**Test:** Type a caption on one photo, reload the editor.
**Expected:** Caption is still present after reload.
**Why human:** Round-trip persistence requires a live DB write and a fresh page load.

#### 4. Delete with confirm dialog

**Test:** Delete a photo — confirm dialog appears; after confirm, the photo and (for uploaded files) its local file are removed.
**Expected:** `CbConfirmDialog` shown; photo removed from gallery; local file gone from `/uploads/`.
**Why human:** Dialog flow and file removal require a running app with real uploads.

#### 5. Paste-URL accept and reject flows

**Test:** Paste a valid image URL (e.g. an Unsplash direct image link). Then paste a non-image page URL.
**Expected:** Valid URL: shows "Validating..." then adds the photo. Non-image URL: rejected with inline error.
**Why human:** Requires a live outbound HEAD request.

#### 6. AI helper text-only output safety (P12)

**Test:** Click "Suggest photo search terms" with `AiFeaturesEnabled` and profile `AiEnabled` set.
**Expected:** Output is plain guidance text — search phrases and site names only, not a clickable link, containing no usable image URL.
**Why human:** Requires a live Anthropic API call and visual inspection of rendered output.

#### 7. Copyright disclaimer always visible

**Test:** Navigate to the photo section of the recipe editor.
**Expected:** The disclaimer "Only add photos you have the right to use. AI suggestions are search terms only — verify the license at the source." is visible at all times, not behind a toggle or tooltip.
**Why human:** Visual presence check; markup is correct (`role="note"` always rendered) but visual confirmation requires a browser.

#### 8. RecipeView gallery display and client-side hero swap

**Test:** Open a recipe page with multiple photos. Click/Tab+Enter on a thumbnail.
**Expected:** Primary shows as 420px hero; thumbnail strip appears with >1 photo; thumbnail click swaps the displayed hero WITHOUT changing the saved hero (IsPrimary unchanged in DB).
**Why human:** Visual layout and keyboard navigation require a real browser; non-mutation guarantee requires observing no DB change.

#### 9. Photo count cap UX

**Test:** Keep adding photos until the cap (10) is reached.
**Expected:** The add affordance disables and a "Max N photos" chip appears.
**Why human:** Requires creating 10 photos in-browser.

#### 10. WR-04 Paste-URL input clears after successful add

**Test:** Paste a valid image URL. After the photo is added, check the URL input field.
**Expected:** The URL input field is visually empty (not showing the just-added URL).
**Why human:** WR-04 was intentionally skipped by the code-fix author due to Blazor child-component re-render complexity. Fix was deferred to browser testing to avoid introducing a binding regression.

---

## Summary

Phase 14 (Photo Gallery) achieves its stated goal. All 12 observable truths are verified in the actual codebase:

- **SC1 (GALLERY-01):** Migration `20260607124611_AddRecipePhotosTable` is present with the correct backfill SQL; `RecipePhotoBackfillTests` (2 tests) prove losslessness and cascade. The migration name matches the roadmap specification exactly.
- **SC2 (GALLERY-02):** `RecipePhotoService` implements all 6 gallery mutations with the one-primary invariant (transactional clear+set), server-side cap (`Math.Clamp`), ownership checks, and promotion-on-delete. `RecipePhotoGalleryManager.razor` wires all mutations via immediate-persist. Sequential `foreach` with per-file `await` is present.
- **SC3 (GALLERY-03):** `LocalRecipePhotoStorage.DeletePhysicalFile` is guarded by `AssertPathInsideUploadsDirectory`. `RecipeService.DeleteAsync` enumerates local photos BEFORE `_recipeRepo.DeleteAsync`. `RecipePhotoService.DeleteAsync` calls `DeletePhysicalFile` before row removal. Service tests include a real temp-file deletion proof.
- **SC4 (GALLERY-04):** The CR-01 AI-gate fix (commit 17d2851) is present — `_aiOn = hostOn && userOn && creds is not null` with `userOn = user?.Profile?.AiEnabled ?? false`. The WR-05 URL-strip fix (commit 2de1aaf) splits on all whitespace. AI output is rendered as plain text in a `<div>`, never `<a>` tags. Copyright disclaimer `role="note"` is unconditionally rendered. `DebounceOnInput="true"` (WR-01 fix) prevents per-keystroke HEAD requests. Server-side URL re-validation in `AddPhotoAsync` (WR-02 fix) closes the service-layer security boundary gap.

**All hard invariants pass:** canonical JSON stays clean (no gallery rows), transfer DTO untouched, zero new NuGet packages, no MudBlazor, no vision path in `AnthropicAiService`, `SwapHero` never mutates `IsPrimary`.

The remaining **human_needed** status reflects 10 browser UAT items (visual gallery behavior, circuit stability, live AI call safety, WR-04 paste-URL clear) that cannot be verified by code analysis. All automated checks — build (0 errors), 445 tests passing — are green.

---

_Verified: 2026-06-07T13:37:14Z_
_Verifier: Claude (gsd-verifier)_
