---
phase: 14-photo-gallery
plan: "02"
subsystem: application
tags: [dotnet, blazor-server, http-validation, unit-tests, ssrf, gallery]

requires:
  - phase: 14-photo-gallery/14-01
    provides: RecipePhoto entity, migration, MaxPhotosPerRecipe setting (wave-1 foundation)

provides:
  - PhotoUrlHeadValidator (HEAD + 405→ranged-GET fallback, AllowAutoRedirect=false, 5s timeout)
  - PhotoUrlValidationResult record with Valid/Timeout/NetworkError/NotAnImage/HttpError factories
  - AddSingleton<PhotoUrlHeadValidator> registration in DependencyInjection.cs
  - 6 unit tests covering all accept/reject/fallback/timeout/network lanes (no live network)

affects:
  - 14-03 (RecipePhotoService and RecipeEditor paste-URL path will call PhotoUrlHeadValidator)
  - 14-04 (UI RecipePhotoGalleryManager will inject and call ValidateAsync on paste events)

tech-stack:
  added: []  # zero new NuGet packages — hard invariant maintained
  patterns:
    - "CreateClient() virtual seam for test-injectable HttpMessageHandler (mirrors AnthropicAiService.CreateHttpClient)"
    - "RecordingFakeHandler captures requests for per-request assertion in tests"
    - "StubPhotoUrlHeadValidator subclass overrides CreateClient() — no live network in tests"
    - "Never-throw envelope: all exception lanes caught and mapped to PhotoUrlValidationResult factories"

key-files:
  created:
    - src/CookBot.Application/Services/PhotoUrlHeadValidator.cs
    - tests/CookBot.Tests/Services/PhotoUrlHeadValidatorTests.cs
  modified:
    - src/CookBot.Application/DependencyInjection.cs

key-decisions:
  - "PhotoUrlValidationResult.NotAnImage is a static property (not a factory), matching the plan's <interfaces> contract — no content-type string in the error message to avoid information leakage"
  - "AllowAutoRedirect=false on HttpClientHandler (not configurable via HttpClient property alone) — SSRF posture D-14-10"
  - "Range header set to bytes=0-511 (512 bytes) — enough for Content-Type header inspection without downloading the full body"
  - "Test double uses RecordingFakeHandler to capture request sequence, enabling assertion that Range header was present on the fallback GET"

patterns-established:
  - "Plan 14-02 gate: HEAD → 405 → ranged GET (Range: bytes=0-511) → EvaluateResponse(contentType starts image/) — copy this pattern for any image URL validation in this codebase"
  - "Never-throw result envelope for network validators — all exception lanes (TaskCanceledException, HttpRequestException) mapped to PhotoUrlValidationResult factory members"

requirements-completed: [GALLERY-04]

duration: 8min
completed: 2026-06-07
---

# Phase 14 Plan 02: PhotoUrlHeadValidator Summary

**HTTP HEAD-with-405→ranged-GET image URL validator — blocks non-image paste-URLs at the Application layer before persist, with AllowAutoRedirect=false SSRF posture and 6 unit tests covering all accept/reject/fallback/timeout/network lanes**

## Performance

- **Duration:** ~8 min
- **Started:** 2026-06-07T12:46:00Z
- **Completed:** 2026-06-07T12:54:42Z
- **Tasks:** 2
- **Files modified:** 3

## Accomplishments

- `PhotoUrlHeadValidator` implemented with full HEAD-validation pipeline: step 1 (scheme allowlist via `RecipePhotoUrlValidator`, prerequisite contract) → step 2 (HEAD → 405 CDN fallback → ranged GET first 512 bytes → EvaluateResponse content-type check)
- `PhotoUrlValidationResult` sealed record with five factory members: `Valid`, `Timeout`, `NetworkError`, `NotAnImage`, `HttpError(HttpStatusCode)` — never-throw envelope per T-14-04
- SSRF posture enforced: `AllowAutoRedirect = false` on `HttpClientHandler`, 5-second timeout (T-14-03, T-14-05)
- Singleton registered in `DependencyInjection.cs` next to `RecipePhotoUrlValidator` — plans 14-03 and 14-04 can inject immediately
- 6 unit tests (all green): valid-image, non-image, 405-fallback with Range-header assertion, HTTP-error, timeout (no-throw), network-error (no-throw)

## Task Commits

1. **Task 1: PhotoUrlHeadValidator + PhotoUrlValidationResult + DI registration** - `8511c6c` (feat)
2. **Task 2: PhotoUrlHeadValidator unit tests** - `75975a5` (test)

## Files Created/Modified

- `src/CookBot.Application/Services/PhotoUrlHeadValidator.cs` — `PhotoUrlHeadValidator` class + `PhotoUrlValidationResult` sealed record; `CreateClient()` virtual seam for test injection
- `src/CookBot.Application/DependencyInjection.cs` — added `services.AddSingleton<PhotoUrlHeadValidator>()`
- `tests/CookBot.Tests/Services/PhotoUrlHeadValidatorTests.cs` — 6 tests; `RecordingFakeHandler`, `TimeoutHandler`, `NetworkErrorHandler`, `StubPhotoUrlHeadValidator` test doubles

## Decisions Made

- `PhotoUrlValidationResult.NotAnImage` is a static property (not a factory with content-type in the message) — matches the plan's `<interfaces>` contract exactly; avoids leaking server-discovered content-type strings to the UI
- `AllowAutoRedirect = false` is set on `HttpClientHandler` (the handler constructor argument), not on the `HttpClient` itself — the `HttpClient` property only controls redirect behavior at the client level but the handler-level setting is authoritative for `HttpClientHandler`
- `RecordingFakeHandler` (captures full request list) chosen over stateless `FakeHttpMessageHandler` from `tests/CookBot.Tests/AI/` — enables the 405-fallback test to assert both (a) two requests were made and (b) the second had a correct `Range` header

## Deviations from Plan

None — plan executed exactly as written.

## Issues Encountered

None.

## Threat Surface Scan

No new network endpoints, auth paths, or schema changes introduced. The `PhotoUrlHeadValidator` itself is a client-side HTTP issuer (outbound only), not a new endpoint. Threats T-14-03 (SSRF), T-14-04 (exception leak), T-14-05 (DoS via slow host) all mitigated per plan's threat register.

## User Setup Required

None — no external service configuration required.

## Next Phase Readiness

- `PhotoUrlHeadValidator` is Singleton-registered and fully tested; plans 14-03 (RecipePhotoService) and 14-04 (RecipePhotoGalleryManager UI) can inject and call `ValidateAsync` immediately
- `PhotoUrlValidationResult` factories provide user-facing error strings ready to surface in the editor toast/alert pattern

---
*Phase: 14-photo-gallery*
*Completed: 2026-06-07*
