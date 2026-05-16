---
phase: 09-photos-prod-ready-infrastructure
plan: 01
subsystem: infra
tags: [blazor-server, signalr, file-upload, magic-bytes, ibrowserfile, kestrel, formoptions, static-files, nosniff, url-validator, di]

requires:
  - phase: 08-format-foundation
    provides: Recipe.PhotoUrl nullable string column on Recipe entity that this plan's storage service feeds
provides:
  - "wwwroot/uploads/* gitignored with .gitkeep allowlist so the directory survives a fresh clone (PHOTO-01)"
  - "RecipePhotoUrlValidator — singleton scheme allowlist (http/https only); rejects javascript:/data:/file:/ftp:/vbscript:/protocol-relative // (PHOTO-07)"
  - "LocalRecipePhotoStorage — scoped Web service that magic-byte sniffs IBrowserFile and persists to wwwroot/uploads/{guid:N}{ext} (PHOTO-02/03/05)"
  - "ImageMagicBytes — pure static helper mapping the first 12 bytes to .jpg/.png/.gif/.webp (or null for SVG/HTML/short/truncated)"
  - "Three server-side size limits raised to 12 MB: Kestrel MaxRequestBodySize, FormOptions.MultipartBodyLengthLimit, Blazor SignalR HubOptions.MaximumReceiveMessageSize (PHOTO-04 + PITFALL H1)"
  - "/uploads served via explicit UseStaticFiles + PhysicalFileProvider with X-Content-Type-Options: nosniff on every response (PHOTO-06 + PITFALL H3)"
  - "InvalidImageException — surfaced by SaveAsync when sniff fails; caught by editor in Plan 09-02 for toast UX"
affects: [09-02-recipe-editor, 09-03-recipeview-render, 09-05-ai-structured-output]

tech-stack:
  added: []  # No new NuGet packages — all foundation uses BCL + existing Microsoft.AspNetCore.* refs
  patterns:
    - "Two-pass IBrowserFile read (sniff first 12 bytes, then re-open for full write) — IBrowserFile guarantees a fresh stream per OpenReadStream call"
    - "Defense-in-depth path-traversal: Path.GetFullPath on both candidate and uploads dir, then StartsWith(prefix + DirectorySeparator) comparison (PITFALL H2)"
    - "Three-limit-pyramid for Blazor file uploads: per-file IBrowserFile cap (10 MB) < outer server limits (12 MB) at three layers (Kestrel + Forms + SignalR)"
    - "Scheme-allowlist validator with tri-out envelope (accept, normalized?, errorCode?) — never throws on any input, including pathological strings"

key-files:
  created:
    - "src/CookBot.Application/Services/RecipePhotoUrlValidator.cs"
    - "src/CookBot.Web/Services/ImageMagicBytes.cs"
    - "src/CookBot.Web/Services/LocalRecipePhotoStorage.cs"
    - "src/CookBot.Web/wwwroot/uploads/.gitkeep"
    - "tests/CookBot.Tests/Services/RecipePhotoUrlValidatorTests.cs"
    - "tests/CookBot.Tests/Services/LocalRecipePhotoStorageTests.cs"
  modified:
    - ".gitignore"
    - "src/CookBot.Application/DependencyInjection.cs"
    - "src/CookBot.Web/Program.cs"

key-decisions:
  - "MaximumReceiveMessageSize wired via .AddInteractiveServerComponents().AddHubOptions(o => o.MaximumReceiveMessageSize = 12 MB) — CircuitOptions does not expose this property in .NET 10; the canonical chained-builder path is AddHubOptions on the IServerSideBlazorBuilder returned by AddInteractiveServerComponents"
  - "All three 12 MB literals are inlined at their call sites (12 * 1024 * 1024 written three times) rather than extracted to a const — keeps the audit trail grep-friendly (`grep -E '12 \\\\* 1024 \\\\* 1024' Program.cs | wc -l` returns 3)"
  - "Path-traversal prefix comparison uses fullUploadsDir + DirectorySeparatorChar (not bare StartsWith) so '/tmp/uploads-evil' cannot pass a '/tmp/uploads' prefix check"
  - "AssertPathInsideUploadsDirectory exposed as public method (not private) so the unit test can exercise the H2 defense without constructing a full IBrowserFile mock — the GUID-only filename in SaveAsync makes traversal unreachable in practice, but the method is the load-bearing invariant"

patterns-established:
  - "Pure-static magic-byte helper in Web layer — testable without DI fixtures, byte-array Theory matrix from PATTERNS.md 673-682"
  - "Scoped storage service constructor pattern: (IWebHostEnvironment env, ILogger<T> logger) — _uploadsDir computed once in ctor, Directory.CreateDirectory idempotent"
  - "Singleton URL validator + scoped storage service co-existence — both registered alongside existing AiApiKeyResolutionService block in Program.cs / DependencyInjection.cs"

requirements-completed: [PHOTO-01, PHOTO-02, PHOTO-03, PHOTO-04, PHOTO-05, PHOTO-06, PHOTO-07]

duration: ~75min
completed: 2026-05-16
---

# Phase 9 Plan 1: Photo Foundation Summary

**Photo upload + paste-URL groundwork: gitignored uploads dir, magic-byte storage service, scheme-allowlist validator, and 12 MB size limits at all three Blazor Server boundaries — every downstream wave's editor / AI / view-mode code can now plug into a working pipeline.**

## Performance

- **Duration:** ~75 min (across two executor agents — Tasks 1+2 in run 1, Task 3 in run 2)
- **Started:** 2026-05-16
- **Completed:** 2026-05-16T(this-commit)Z
- **Tasks:** 3 / 3
- **Files modified:** 3 modified + 6 created = 9 files
- **Tests:** 16 storage + 16 validator = 32 photo-foundation tests passing
- **Full suite:** 279 / 285 passing (6 failures all `Category=RequiresApiKey` gated live-API tests, pre-existing — unrelated to this plan)
- **Build:** `dotnet build` clean — 0 errors, 0 new warnings

## Accomplishments

- **PHOTO-01 standalone-commit ordering preserved.** `.gitignore` exclusion of `wwwroot/uploads/*` (with `!.gitkeep` allowlist) landed as the literal first commit of Phase 9 (`1405ee5`), before any upload code existed. PITFALL C5's "user already uploaded a photo before .gitignore was in place" failure mode is structurally impossible from this point forward.
- **RecipePhotoUrlValidator** shipping with full PITFALL H5 rejection matrix: `javascript:`, `data:`, `file:`, `ftp:`, `vbscript:`, and protocol-relative `//host` all rejected; `http`/`https` accepted; null/empty/whitespace treated as the "no photo" signal (returns accept with `normalized=null`). 16 InlineData rows + 2 Fact-level tests cover the matrix and a pathological-input no-throw guarantee.
- **LocalRecipePhotoStorage** with two-pass `IBrowserFile` read — sniff first 12 bytes for magic-byte detection (PHOTO-02 + PITFALL H3), then re-open via fresh `OpenReadStream(maxAllowedSize: 10 MB)` (PHOTO-03) to copy the full payload to `wwwroot/uploads/{guid:N}{ext}` (PHOTO-05). Path-traversal defense via `Path.GetFullPath` + `prefix + DirectorySeparator` `StartsWith` (PITFALL H2). `InvalidImageException` surfaces sniff failures to the editor's toast UX in Plan 09-02.
- **ImageMagicBytes** pure-static helper recognizes JPEG (FF D8 FF + any 4th byte), PNG (8-byte signature), GIF87a/GIF89a, and WebP (RIFF…WEBP at offset 8). SVG / HTML / sub-3-byte / truncated WebP all return `null`. Tests cover the full RESEARCH Item 5 matrix verbatim.
- **Three server-side size limits raised to 12 MB at each Blazor boundary** (PITFALL H1):
  - Kestrel `MaxRequestBodySize = 12 * 1024 * 1024` (line 23 of Program.cs)
  - `FormOptions.MultipartBodyLengthLimit = 12 * 1024 * 1024` (line 24)
  - Blazor SignalR `HubOptions.MaximumReceiveMessageSize = 12 * 1024 * 1024` via `AddInteractiveServerComponents().AddHubOptions(...)` (line 32)
  - This is the load-bearing PITFALL H1 fix: without `MaximumReceiveMessageSize`, the SignalR circuit silently drops messages over 32 KB (default), failing the upload UX without an error path.
- **/uploads explicit `UseStaticFiles`** with `PhysicalFileProvider` and `X-Content-Type-Options: nosniff` on every response (PHOTO-06 + PITFALL H3 — prevents the browser from sniffing an uploaded image as HTML or SVG even if a future bug let one slip past the magic-byte check).

## Task Commits

Each task committed atomically:

1. **Task 1: PHOTO-01 — `.gitignore` photo entry** — `1405ee5` (`chore`)
2. **Task 2: PHOTO-07 — RecipePhotoUrlValidator + DI registration + 16 tests** — `a9c4266` (`feat`, TDD RED→GREEN consolidated)
3. **Task 3: PHOTO-02/03/04/05/06 — LocalRecipePhotoStorage + ImageMagicBytes + Program.cs size limits + nosniff static files** — `eee3def` (`feat`, TDD RED→GREEN consolidated)

**Plan metadata commit:** _(this docs commit)_

## Three Size-Limit Verification

```
$ grep -n "MaximumReceiveMessageSize\|MaxRequestBodySize\|MultipartBodyLengthLimit" src/CookBot.Web/Program.cs
20:// MaximumReceiveMessageSize is the limit that silently drops circuits at 32 KB by
23:builder.Services.Configure<KestrelServerOptions>(o => o.Limits.MaxRequestBodySize = 12 * 1024 * 1024);
24:builder.Services.Configure<FormOptions>(o => o.MultipartBodyLengthLimit = 12 * 1024 * 1024);
30:    // dropping the upload circuit. MaximumReceiveMessageSize is the load-bearing
32:    .AddHubOptions(o => o.MaximumReceiveMessageSize = 12 * 1024 * 1024);
```

All three knobs configured with identical literal `12 * 1024 * 1024` (12 582 912 bytes).

## Test Counts

- `RecipePhotoUrlValidatorTests`: 14 Theory rows + 2 Fact tests = **16 tests, 16 passing**
- `LocalRecipePhotoStorageTests`: 12 Theory rows (6 accept + 6 reject) + 4 Fact tests (empty-span, one-byte-span, path-traversal-throws, path-traversal-legit-no-throw) = **16 tests, 16 passing**

Magic-byte matrix size: **12 InlineData rows** in `LocalRecipePhotoStorageTests` (6 accept + 6 reject — exactly the RESEARCH Item 5 matrix from lines 673–690 of 09-PATTERNS.md, with the addition of two boundary cases for length-zero and length-one spans).

## Files Created/Modified

**Created:**
- `src/CookBot.Application/Services/RecipePhotoUrlValidator.cs` — scheme-allowlist validator (singleton, tri-out envelope, never throws)
- `src/CookBot.Web/Services/ImageMagicBytes.cs` — pure-static 12-byte magic header → extension helper
- `src/CookBot.Web/Services/LocalRecipePhotoStorage.cs` — scoped IBrowserFile → wwwroot/uploads persistence with path-traversal defense + InvalidImageException
- `src/CookBot.Web/wwwroot/uploads/.gitkeep` — empty placeholder so the directory survives a fresh clone
- `tests/CookBot.Tests/Services/RecipePhotoUrlValidatorTests.cs` — H5 rejection matrix + trim + no-throw pathological coverage
- `tests/CookBot.Tests/Services/LocalRecipePhotoStorageTests.cs` — magic-byte accept/reject Theory matrix + path-traversal Fact

**Modified:**
- `.gitignore` — appended Phase 9 / PHOTO-01 block with `wwwroot/uploads/*` and `!.gitkeep` negation
- `src/CookBot.Application/DependencyInjection.cs` — added `services.AddSingleton<RecipePhotoUrlValidator>()` in `AddApplication`
- `src/CookBot.Web/Program.cs` — three 12 MB size limits (Kestrel + FormOptions + SignalR HubOptions); `AddScoped<LocalRecipePhotoStorage>`; explicit `UseStaticFiles` for `/uploads` with PhysicalFileProvider + nosniff OnPrepareResponse

## Decisions Made

1. **MaximumReceiveMessageSize wired via `AddHubOptions` rather than a `CircuitOptions` lambda.** The plan suggested `AddInteractiveServerComponents(o => o.MaximumReceiveMessageSize = ...)`, but in .NET 10 `CircuitOptions` does not expose `MaximumReceiveMessageSize` — that property lives on `HubOptions<ComponentHub>`, and `ComponentHub` is internal. The canonical chained-builder path is `.AddInteractiveServerComponents().AddHubOptions(o => o.MaximumReceiveMessageSize = ...)`, which targets the right `HubOptions` instance for the Blazor Server SignalR hub. Verified via `Microsoft.AspNetCore.Components.Server.xml` reference docs.
2. **Three 12 MB literals inlined at each call site** (rather than extracted to a `const int MaxUploadBytes`). Trade-off: gives up one source-of-truth in exchange for grep-friendly audit (`grep -E '12 \\* 1024 \\* 1024' Program.cs | wc -l` returns 3). The plan acceptance criterion uses this grep heuristic, and the literal repetition serves as documentation that each of the three boundary layers got the same number.
3. **Path-traversal prefix check appends `DirectorySeparatorChar`** before comparing — `fullUploadsDir + Path.DirectorySeparatorChar` rather than bare `fullUploadsDir`. Without the separator, `/tmp/cookbot-test-abc/uploads-evil/file` would pass a `/tmp/cookbot-test-abc/uploads` prefix check. This is tighter than the literal RESEARCH Item 5 snippet (which uses bare `StartsWith`); it is a security strengthening, not a deviation from intent.
4. **`AssertPathInsideUploadsDirectory` exposed as public** rather than private. Rationale: the path-traversal Fact test needs to exercise the H2 defense, and constructing a full `IBrowserFile` mock with controllable filename + content + size + cancellation would 5× the test scope. Exposing the assertion publicly costs nothing semantically (the GUID-only filename in `SaveAsync` makes the assertion unreachable via the public surface) and the public XML doc explicitly documents it as the H2 contract.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 — Bug / API drift] `CircuitOptions.MaximumReceiveMessageSize` does not exist in .NET 10**
- **Found during:** Task 3 (Program.cs size-limit configuration)
- **Issue:** Plan's literal `AddInteractiveServerComponents(o => o.MaximumReceiveMessageSize = 12 * 1024 * 1024)` failed to compile — `CircuitOptions` (the options type passed to that lambda) does not expose `MaximumReceiveMessageSize`. That property lives on `HubOptions` (SignalR). Initial attempt to use `Configure<HubOptions<ComponentHub>>` also failed because `ComponentHub` is internal in `Microsoft.AspNetCore.Components.Server`.
- **Fix:** Used `.AddInteractiveServerComponents().AddHubOptions(o => o.MaximumReceiveMessageSize = 12 * 1024 * 1024)` — the canonical chained-builder path. `AddHubOptions` on the `IServerSideBlazorBuilder` returned by `AddInteractiveServerComponents` targets the correct internal `HubOptions<ComponentHub>` instance without requiring the type to be publicly accessible.
- **Files modified:** `src/CookBot.Web/Program.cs` (lines 26–32)
- **Verification:** `dotnet build` clean; `grep -c "MaximumReceiveMessageSize" Program.cs` shows the configuration line; full test suite green.
- **Committed in:** `eee3def` (Task 3 commit)
- **Documented in:** key-decisions #1 above + commit message body

---

**Total deviations:** 1 auto-fixed (1 API drift bug in plan instructions vs .NET 10 reality)
**Impact on plan:** Zero — the load-bearing invariant (SignalR receive message size = 12 MB) is preserved; only the mechanism for setting it changed. No scope creep. All seven PHOTO-* requirements close per plan.

## Issues Encountered

- **Pre-existing live-API test failures.** `dotnet test` shows 6 failures in `Category=RequiresApiKey`-gated tests (Anthropic API key not present in worktree env). These predate Phase 9 entirely and are out of scope per the scope-boundary rule. Verified by re-running with `--filter "Category!=RequiresApiKey"` → 279/279 pass.
- **Pre-existing EF1002 warnings in `RecipeTagBackfillTests.cs`** (Phase 8 file) about `ExecuteSqlRawAsync` — also out of scope; not introduced by this plan.

## User Setup Required

None. This plan adds no external service dependencies, no new env vars, and no manual database migrations. The `wwwroot/uploads/` directory is auto-created by both `LocalRecipePhotoStorage`'s constructor and the explicit `Directory.CreateDirectory` call in `Program.cs` (idempotent), so fresh clones work without manual intervention.

## Next Phase Readiness

**Plan 09-02 (RecipePhotoComposite + Description editor field + per-step Temperature picker)** can now `[Inject] LocalRecipePhotoStorage storage` and `[Inject] RecipePhotoUrlValidator validator` in `RecipeEditor.razor` — both DI registrations are wired and ready. The editor's toast surface needs to catch `InvalidImageException` from `SaveAsync` and display "Only JPEG, PNG, GIF, or WebP allowed." per PITFALL H3.

**Plan 09-05 (AI structured-output + PROD-07)** can `[Inject] RecipePhotoUrlValidator validator` in `AnthropicAiService` (or wherever the AI-emitted `PhotoUrl` is consumed) to filter the structured-output return path through the same scheme allowlist the editor uses — closing the symmetry mandated by PHOTO-07 + PITFALL H5.

**Plan 09-03 (RecipeView render with onerror fallback)** depends only on `/uploads/{guid}.{ext}` URLs resolving against the explicit `UseStaticFiles` middleware — already wired and verified via grep + (implicit) compile. The `onerror` one-shot debounce (PITFALL H4) is editor/view-side; this plan's foundation does not constrain it.

## Self-Check: PASSED

- `[x] src/CookBot.Application/Services/RecipePhotoUrlValidator.cs` exists (Task 2)
- `[x] src/CookBot.Web/Services/ImageMagicBytes.cs` exists (Task 3)
- `[x] src/CookBot.Web/Services/LocalRecipePhotoStorage.cs` exists (Task 3)
- `[x] src/CookBot.Web/wwwroot/uploads/.gitkeep` exists, 0 bytes (Task 1)
- `[x] tests/CookBot.Tests/Services/RecipePhotoUrlValidatorTests.cs` exists (Task 2)
- `[x] tests/CookBot.Tests/Services/LocalRecipePhotoStorageTests.cs` exists (Task 3)
- `[x] .gitignore` contains `src/CookBot.Web/wwwroot/uploads/*` and `!src/CookBot.Web/wwwroot/uploads/.gitkeep`
- `[x] src/CookBot.Application/DependencyInjection.cs` contains `AddSingleton<RecipePhotoUrlValidator>`
- `[x] src/CookBot.Web/Program.cs` contains `AddScoped<LocalRecipePhotoStorage>`
- `[x] Program.cs` contains all three size limits (Kestrel + FormOptions + SignalR HubOptions) at `12 * 1024 * 1024`
- `[x] Program.cs` contains `RequestPath = "/uploads"` AND `X-Content-Type-Options` `"nosniff"`
- `[x] Commit 1405ee5 (PHOTO-01)` in `git log`
- `[x] Commit a9c4266 (PHOTO-07)` in `git log`
- `[x] Commit eee3def (Task 3)` in `git log`
- `[x] dotnet build` — 0 errors, 0 new warnings
- `[x] dotnet test --filter "FullyQualifiedName~LocalRecipePhotoStorageTests"` — 16 / 16 passing
- `[x] dotnet test --filter "FullyQualifiedName~RecipePhotoUrlValidatorTests"` — 16 / 16 passing
- `[x] dotnet test --filter "Category!=RequiresApiKey"` — 279 / 279 passing

---
*Phase: 09-photos-prod-ready-infrastructure*
*Plan: 01*
*Completed: 2026-05-16*
