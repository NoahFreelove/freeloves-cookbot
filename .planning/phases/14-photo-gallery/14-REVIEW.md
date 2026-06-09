---
phase: 14-photo-gallery
reviewed: 2026-06-07T00:00:00Z
depth: standard
files_reviewed: 5
files_reviewed_list:
  - src/CookBot.Infrastructure/Services/RecipePhotoService.cs
  - src/CookBot.Application/Services/PhotoUrlHeadValidator.cs
  - src/CookBot.Application/Services/RecipeService.cs
  - src/CookBot.Web/Services/LocalRecipePhotoStorage.cs
  - src/CookBot.Web/Components/Pages/RecipeEditorParts/RecipePhotoGalleryManager.razor
findings:
  critical: 1
  warning: 5
  info: 4
  total: 10
status: issues_found
---

# Phase 14: Code Review Report

**Reviewed:** 2026-06-07T00:00:00Z
**Depth:** standard
**Files Reviewed:** 5
**Status:** issues_found

## Summary

Phase 14 adds a multi-photo gallery: a CRUD service (`RecipePhotoService`), an SSRF-aware HEAD validator (`PhotoUrlHeadValidator`), local file storage with path-traversal guards (`LocalRecipePhotoStorage`), a canonical-sync hook (`RecipeService.SyncPrimaryPhotoUrlAsync`), and an immediate-persist Razor manager (`RecipePhotoGalleryManager`).

Most phase invariants hold up well: the SSRF posture is correctly configured (`AllowAutoRedirect = false`, 5s timeout, scheme allowlist enforced by the caller, never-throws contract proven by tests), path-traversal guards funnel every physical delete through `AssertPathInsideUploadsDirectory`, recipe-delete enumerates photos before the EF cascade, the one-primary invariant + cap are enforced server-side, ownership checks gate every `RecipePhotoService` mutation, and the change-tracker detach+refetch dance for the two-primary-drift fix is correct (verified against the shared scoped `DbContext`).

However, the review surfaces one BLOCKER and several warnings. The most serious is an **AI-gate authorization gap**: the gallery's AI helper does NOT honor the per-user `UserProfile.AiEnabled` toggle, contradicting the documented invariant ("AI helper gated by `AiFeaturesEnabled && AiEnabled`") and the component's own comment claiming it "mirrors RecipeEditor.razor verbatim." Secondary concerns include per-keystroke outbound HTTP HEAD requests (SSRF amplification + UX), and a missing server-side URL validation boundary in `AddPhotoAsync`.

## Critical Issues

### CR-01: Gallery AI helper bypasses the per-user `AiEnabled` toggle

**File:** `src/CookBot.Web/Components/Pages/RecipeEditorParts/RecipePhotoGalleryManager.razor:273-284`
**Issue:**
The component's AI gate resolves to `_aiOn = hostOn && userKeyAvailable`, where `userKeyAvailable = creds is not null`:

```csharp
var hostOn = Settings.Value.AiFeaturesEnabled;
var creds = await AiKeyResolver.ResolveAsync(UserId);
var userKeyAvailable = creds is not null;
_aiOn = hostOn && userKeyAvailable;
```

`AiApiKeyResolutionService.ResolveAsync` (verified at `src/CookBot.Web/Services/AiApiKeyResolutionService.cs:39-81`) only inspects the stored/shared API key — it **never reads `UserProfile.AiEnabled`**. So a user who has explicitly disabled AI in their profile (`AiEnabled = false`) but still has a key configured will see and be able to invoke the "Suggest photo search terms" button, firing a live Anthropic API call against their key.

This:
1. Violates the phase invariant stated in the task: "AI helper gated by `AiFeaturesEnabled && AiEnabled`."
2. Contradicts the component's own header comment (line 16-17) and the `OnAfterRenderAsync` comment (line 277): "mirrors RecipeEditor.razor:519-521 verbatim." It does NOT — `RecipeEditor.razor:528-530` reads `user?.Profile?.AiEnabled`:
   ```csharp
   var hostOn = CookBotSettingsOptions.Value.AiFeaturesEnabled;
   var userOn = user?.Profile?.AiEnabled ?? false;
   _aiOn = hostOn && userOn;
   ```
   The gallery dropped the `userOn` term and substituted "a key is resolvable," which is a strictly weaker gate.

This is an authorization-gap class defect (host kill-switch + per-user opt-in is the documented AI control surface per CLAUDE.md), so it is classified Critical.

**Fix:** Resolve the AI gate the same way `RecipeEditor.razor` does — read the per-user `AiEnabled` flag and AND it in. For example, fetch the current user/profile and combine both terms:
```csharp
var user = await UserService.GetCurrentUserAsync();   // same source RecipeEditor uses
var hostOn = Settings.Value.AiFeaturesEnabled;
var userOn = user?.Profile?.AiEnabled ?? false;
var creds = await AiKeyResolver.ResolveAsync(UserId);
_aiOn = hostOn && userOn && creds is not null;
```
(Keeping the `creds is not null` term is fine as an additional precondition, but `userOn` must be part of the conjunction.)

## Warnings

### WR-01: Paste-URL fires an outbound HTTP HEAD on every keystroke (SSRF amplification + UX)

**File:** `src/CookBot.Web/Components/Pages/RecipeEditorParts/RecipePhotoGalleryManager.razor:180-184, 361-416`
**Issue:**
The paste-URL field binds `ValueChanged="OnUrlPasted"` on `<CbInput>` without `DebounceOnInput="true"`. `CbInput` (verified at `src/CookBot.Web/Components/Atoms/CbInput.razor:7, 27-36`) fires `ValueChanged` on `oninput` — i.e. **every keystroke** — unless `DebounceOnInput` is set.

`OnUrlPasted` runs the full two-step validation: as soon as the partial input parses as an absolute http/https URL (e.g. the user has typed `https://example.com`), step 2 issues a real outbound `HttpClient.SendAsync` HEAD request via `PhotoUrlHeadValidator`. Typing or pasting a URL therefore generates a burst of server-originated outbound requests to whatever prefix the user has typed so far. Under the trusted-LAN posture this is not a full SSRF (private-IP deny-listing is intentionally out of scope), but it is a request-amplification vector and a clear UX/perf defect (a fresh `HttpClient` + 5s-timeout request per keystroke). It also races: multiple in-flight validations can resolve out of order and clobber `_urlError` / `_urlValidating`.

**Fix:** Set `DebounceOnInput="true"` on the `<CbInput>` so validation runs only on blur/change, or otherwise debounce before invoking `HeadValidator.ValidateAsync`. A paste-then-blur flow is the intended interaction; per-keystroke HEADs are not.

### WR-02: `AddPhotoAsync` performs no server-side URL validation (security boundary gap)

**File:** `src/CookBot.Infrastructure/Services/RecipePhotoService.cs:66-95`
**Issue:**
`AddPhotoAsync(int recipeId, string url, ...)` accepts and persists any `url` string verbatim. The scheme allowlist (`RecipePhotoUrlValidator`) and the HEAD image-gate (`PhotoUrlHeadValidator`) are applied ONLY in the Razor UI (`OnUrlPasted`). The service is the authoritative trust boundary, yet it does not re-validate. Any caller that reaches `AddPhotoAsync` by another path (a future endpoint, a test-seeded call, or a UI regression that drops the validation step) can persist a `javascript:`/`data:`/`file:` URL that later renders into an `<img src>` in `RecipeView`/`RecipePhotoGalleryManager`. The same value is then mirrored into `Recipe.PhotoUrl` and the canonical JSON by `SyncPrimaryPhotoUrlAsync`. Defense-in-depth (the documented project posture for the upload path, e.g. `LocalRecipePhotoStorage`'s "belt-and-braces" guards) is absent on the paste-URL path at the service layer.

Additionally there is no length check against the documented 2048-char `Url` column max (`RecipePhotoConfiguration.cs:19-21`); SQLite does not enforce `maxLength`, so an over-length URL persists silently and could exceed the limit other consumers assume.

**Fix:** Re-run `RecipePhotoUrlValidator.TryValidate` inside `AddPhotoAsync` for non-`/uploads/` URLs and reject on failure; reject (or truncate-and-reject) URLs longer than 2048 chars. Local `/uploads/{guid}.ext` paths produced by `LocalRecipePhotoStorage` can be allow-listed by prefix.

### WR-03: `SetPrimaryAsync` / `DeleteAsync` clear-then-set is not transactional — interrupt leaves zero primaries

**File:** `src/CookBot.Infrastructure/Services/RecipePhotoService.cs:113-129, 199-225`
**Issue:**
Both the set-primary and delete-promote paths run two separate writes: (1) a bulk `ExecuteUpdateAsync` that clears `IsPrimary = false` on every row (committed immediately, bypassing the change tracker), then (2) a `SaveChangesAsync` that sets `IsPrimary = true` on the chosen row. There is no surrounding transaction. If the process is interrupted, the request is cancelled, or step (2) throws between the two writes, the recipe is left with **zero** primary photos, violating the one-primary invariant in the persisted store.

In `DeleteAsync` (line 219-224) this is worse: if `FindAsync(nextId)` returns `null` (concurrent delete), promotion is silently skipped, again leaving zero `IsPrimary` rows. `SyncPrimaryPhotoUrlAsync` masks the symptom for `Recipe.PhotoUrl` via its lowest-SortOrder fallback (line 324-325), but the `RecipePhoto.IsPrimary` flags themselves remain all-false, so the next gallery load shows no hero outline and `SetPrimaryAsync`'s own invariant assumption is broken.

**Fix:** Wrap the clear+set pair in a single `await using var tx = await _db.Database.BeginTransactionAsync();` ... `await tx.CommitAsync();` so the two writes commit atomically. Alternatively, fold the clear and set into one logical operation that cannot leave an intermediate zero-primary state (e.g. set the target true and clear others in a single `ExecuteUpdateAsync` using a conditional `SetProperty`). For the delete-promote null-`winner` case, treat it as an error or re-query rather than silently skipping.

### WR-04: Clearing `_urlInput` does not reset the bound `CbInput` after a successful paste-add

**File:** `src/CookBot.Web/Components/Pages/RecipeEditorParts/RecipePhotoGalleryManager.razor:402-416`
**Issue:**
On a successful paste-URL add, the handler sets `_urlInput = string.Empty` (line 406) and calls `StateHasChanged()`. But `CbInput` maintains its own internal `Value` parameter and only renders `value="@Value"`; Blazor will not re-push the parent's reset value into the child unless the child's `Value` parameter is re-bound. Because the field uses `Value="@_urlInput"` + `ValueChanged="OnUrlPasted"` (not `@bind-Value`) and the input updates on `oninput`, the visible text in the box is not reliably cleared after the add — the user sees the just-added URL still sitting in the field, inviting a duplicate add on the next keystroke/blur. (Combined with WR-01's per-keystroke validation, the residual text can immediately re-trigger validation.)

**Fix:** Use `@bind-Value` semantics consistently or ensure the child re-renders with the cleared value (e.g. switch to `@bind-Value="_urlInput"` and clear it, or key the input so it resets). Verify in the browser that the field is empty after a successful add.

### WR-05: AI URL-stripping splits on single space only — tab/newline-delimited URLs survive

**File:** `src/CookBot.Web/Components/Pages/RecipeEditorParts/RecipePhotoGalleryManager.razor:584-615`
**Issue:**
`StripUrls` tokenizes with `text.Split(' ')` — a single literal space. The AI system prompt asks for output "one per line," so tokens are separated by `\n`, and the model may also emit tabs or non-breaking spaces. A URL that is newline- or tab-delimited (e.g. `\nhttps://evil.example/x.jpg\n`) is never isolated as its own token, so `Uri.TryCreate` is run against a multi-line blob that fails to parse as an absolute URI, and the URL passes through to display. The P12 invariant ("strip any http(s) URL from AI helper output before display") is therefore only partially enforced. The output is rendered as plain text (`white-space:pre-wrap`, not a hyperlink), which limits the blast radius, but the documented stripping guarantee is not met.

**Fix:** Split on all whitespace: `text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)` or a regex-based pass that detects `https?://` substrings regardless of surrounding whitespace. Preserve line structure separately if needed for display.

## Info

### IN-01: `RecipePhotoService` is not registered against an interface — testability/consistency

**File:** `src/CookBot.Infrastructure/DependencyInjection.cs:38`
**Issue:** `RecipePhotoService` is registered as a concrete type (`AddScoped<RecipePhotoService>()`) and injected concretely into `RecipePhotoGalleryManager` and `RecipeView`. Every other mutation/CRUD service of comparable weight in this codebase that needs mocking (`IScheduledRecipeService`, `IRecipeMadeService`, `IPantryMatchService`) sits behind an interface. The tests construct `RecipePhotoService` directly so this isn't blocking, but it diverges from the established pattern and makes the Razor components harder to unit-test.
**Fix:** Consider extracting an `IRecipePhotoService` for consistency with sibling services (optional; not a correctness issue).

### IN-02: `ReorderAsync` and `UpdateCaptionAsync` re-sync semantics are inconsistent

**File:** `src/CookBot.Infrastructure/Services/RecipePhotoService.cs:138-163, 235-245`
**Issue:** `ReorderAsync` calls `SyncPrimaryPhotoUrlAsync` at the end (line 162), but reordering does not change which photo is primary — the primary's `Url` is unchanged, so the sync is a no-op write (re-serializes canonical JSON + `UpdateAsync` for nothing). Conversely `UpdateCaptionAsync` deliberately skips the sync (documented, captions aren't mirrored). The reorder sync is harmless but is dead/wasteful work and muddies the "every mutation except UpdateCaption syncs" contract stated in the class doc (line 18-20). Note this is a correctness-adjacent observation, not a performance finding.
**Fix:** Drop the `SyncPrimaryPhotoUrlAsync` call from `ReorderAsync` (reorder never changes the primary URL), or document why it is retained.

### IN-03: `_aiOn` resolved only on `firstRender`; stale across recipe switches

**File:** `src/CookBot.Web/Components/Pages/RecipeEditorParts/RecipePhotoGalleryManager.razor:267-284`
**Issue:** `OnParametersSetAsync` reloads `_photos` when `RecipeId`/`UserId` change, but `_aiOn` is computed only inside `OnAfterRenderAsync(firstRender: true)`. The same component instance is keyed by `RecipeId` in `RecipeEditor.razor:85` so in practice it remounts, but if the component were ever reused across users without remount, the AI gate would not re-evaluate. Low risk under current usage.
**Fix:** Re-resolve the AI gate when `UserId` changes, or confirm the component always remounts on user switch.

### IN-04: `DeleteAsync` log message says "single-photo delete" but is reused by promotion path context

**File:** `src/CookBot.Infrastructure/Services/RecipePhotoService.cs:189`
**Issue:** Minor: the warning log "Could not delete photo file {Url} during single-photo delete" is accurate, but the near-identical message in `RecipeService.DeleteAsync:303` says "during recipe delete." Consistent, but the two file-delete code paths are duplicated (try/catch around `DeletePhysicalFile` with a `/uploads/` prefix check appears in both `RecipePhotoService.DeleteAsync` and `RecipeService.DeleteAsync`). Consider extracting a shared helper to avoid drift.
**Fix:** Optional — extract a `TryDeleteLocalPhotoFile(url, logger)` helper used by both delete paths.

---

_Reviewed: 2026-06-07T00:00:00Z_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
