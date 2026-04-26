---
phase: 02-ai-structured-output-conformance
plan: 02
subsystem: ai
tags: [ai, anthropic, structured-output, sse, dotnet, http, redaction, validation]

# Dependency graph
requires:
  - phase: 02-ai-structured-output-conformance
    plan: 01
    provides: "SecretRedactor.Redact (called from every error path); PromptInjectionGuard (NOT called from this plan — used by Wave 3 orchestrator)"
  - phase: 01-canonical-recipe-format
    provides: "RecipeJsonSchemaProvider.GetSchema() (test fixtures), RecipeValidator.Validate (constructor-injected), RecipeDocument (deserialization target)"
provides:
  - "IStructuredAiService.SendStructuredAsync<T> — AI-01 transport surface; never throws (except OperationCanceledException); returns StructuredResult<T> envelope"
  - "StructuredResult<T> — D-02 5-tuple envelope (Ok, Value, RawResponse, Validation, SanitizedError)"
  - "AnthropicAiService now implements both IAiService and IStructuredAiService — single instance per scope, two interface entries"
  - "FakeHttpMessageHandler — first HTTP-layer test double in this codebase; reusable pattern for Wave 3+ tests"
affects: [02-03-recipe-cooking-ai-context, 02-04-orchestrator]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Application-layer interface alongside Domain-layer interface, both implemented by the same Infrastructure class (resolution to JsonNode/ValidationResult layering tension)"
    - "DI factory registration that aliases a second interface to the same scoped instance: AddScoped<I2>(sp => (I2)sp.GetRequiredService<I1>())"
    - "protected virtual seam for HttpClient creation as a testability hook (avoids IHttpClientFactory ceremony for a single-callsite service)"
    - "FakeHttpMessageHandler driven by Func<HttpRequestMessage, HttpResponseMessage> — first HTTP-layer test fake in the codebase"
    - "Never-throws result envelope (StructuredResult<T>) — every error path returns a populated record; OperationCanceledException is the only allowed throw"

key-files:
  created:
    - "src/CookBot.Application/AI/StructuredResult.cs"
    - "src/CookBot.Application/AI/IStructuredAiService.cs"
    - "tests/CookBot.Tests/AI/FakeHttpMessageHandler.cs"
    - "tests/CookBot.Tests/AI/AnthropicStructuredOutputTests.cs"
  modified:
    - "src/CookBot.Infrastructure/AI/AnthropicAiService.cs (now implements IStructuredAiService; CreateHttpClient is protected virtual; constructor adds RecipeValidator; new SendStructuredAsync<T> method)"
    - "src/CookBot.Infrastructure/DependencyInjection.cs (factory registration of IStructuredAiService aliased to the same AnthropicAiService instance as IAiService)"

key-decisions:
  - "Layering deviation honored: IStructuredAiService lives in Application/AI (NOT Domain) because StructuredResult<T> references JsonNode and ValidationResult — Domain is kept clean. The IAiService Domain interface is unchanged."
  - "Constructor signature deviation from plan text: kept the existing IOptions<CookBotSettings> parameter (the plan's IConfiguration + ICurrentUserService description was based on stale codebase assumptions). RecipeValidator was added alongside the existing IOptions param — functional behavior matches plan."
  - "CA2024 warning fix (Rule 1): replaced `while (!reader.EndOfStream)` with `while (true) { line = await ReadLineAsync(); if (line is null) break; }` — sync-EndOfStream is disallowed in async methods and breaks streaming HTTP semantics anyway."
  - "Test fixture id type fix (Rule 1): IngredientEntry.Id is int (not string); tests use integer ids matching the canonical schema."
  - "DI factory alias pattern: `AddScoped<IStructuredAiService>(sp => (IStructuredAiService)sp.GetRequiredService<IAiService>())` ensures one AnthropicAiService instance per scope serves both interface registrations — avoids two parallel scoped instances and keeps RecipeValidator dependency tracked once."

requirements-completed: []  # AI-01 is partially landed (transport in place); marked complete only after Wave 3 orchestrator wires it end-to-end

# Metrics
duration: 5min
completed: 2026-04-26
---

# Phase 02 Plan 02: Anthropic Structured-Output Transport Summary

**AnthropicAiService now implements `IStructuredAiService.SendStructuredAsync<T>` — wires Anthropic's `output_config.format` with `strict: true`, accumulates SSE deltas, deserializes into RecipeDocument, runs RecipeValidator, and returns a never-throwing StructuredResult<T> envelope. First HTTP-layer test fake (FakeHttpMessageHandler) covers the SSE path without burning real Anthropic tokens.**

## Performance

- **Duration:** ~5 min
- **Started:** 2026-04-26T05:23:55Z
- **Completed:** 2026-04-26T05:28:45Z
- **Tasks:** 3 (Task 1 contract types; Task 2 implementation + DI; Task 3 tests)
- **Files created:** 4
- **Files modified:** 2

## Accomplishments

- **AI-01 transport landed.** `SendStructuredAsync<T>` builds the D-10 request body (`output_config.format` with `type: "json_schema"`, the cached schema node, `strict: true`), accumulates SSE `content_block_delta.delta.text` chunks into a `StringBuilder`, captures `stop_reason` from `message_delta`, and short-circuits on `refusal` per critical-constraint #2.
- **Never-throws envelope.** Every error path — transport failures, non-success HTTP, deserialization errors, refusal stop-reason, empty content — returns a populated `StructuredResult<T>` record. `OperationCanceledException` is the only escape; tested explicitly.
- **Secret redaction at every error site.** Four `SecretRedactor.Redact` call sites cover client init, transport, non-success HTTP body, and JsonException paths. `sk-ant-leaked-secret-abc123` proven absent from sanitized output via `Assert.DoesNotContain("sk-ant-", result.SanitizedError)` in the 401 test.
- **Layering preserved (deviation honored).** `IStructuredAiService` lives in Application/AI; the Domain `IAiService` is unchanged. Verified: `! grep -q "SendStructuredAsync" src/CookBot.Domain/Interfaces/IAiService.cs` passes.
- **First HTTP-layer fake in the codebase.** `FakeHttpMessageHandler : HttpMessageHandler` driven by `Func<HttpRequestMessage, HttpResponseMessage>`. Reusable pattern documented for Wave 3+ tests.
- **DI factory alias.** Single `AnthropicAiService` instance per scope serves both `IAiService` and `IStructuredAiService` — no double-scope footgun, no parallel state.
- **Build clean, full suite green.** 0 warnings, 0 errors. 139/139 tests pass (133 prior + 6 new).

## Task Commits

1. **Task 1: contract types** — `a9c5c47` (feat) — `StructuredResult<T>` and `IStructuredAiService` in Application/AI; build clean.
2. **Task 2: SendStructuredAsync + DI** — `50a64d8` (feat) — AnthropicAiService implements both interfaces; CreateHttpClient is protected virtual; SecretRedactor at every catch site; refusal short-circuit; DI factory alias; CA2024 warning fix included; build clean; 133/133 prior tests still green.
3. **Task 3: HTTP-layer tests** — `384b6c9` (test) — FakeHttpMessageHandler + 6 AnthropicStructuredOutputTests covering valid path, validation failure, HTTP 401 with key-leak guard, refusal short-circuit, truncated JSON, and pre-cancelled token; full suite 139/139.

**Plan metadata commit:** added by `/gsd-execute-phase` after this SUMMARY.

## Files Created/Modified

- **NEW** `src/CookBot.Application/AI/StructuredResult.cs` — D-02 5-tuple sealed record with `where T : class` constraint
- **NEW** `src/CookBot.Application/AI/IStructuredAiService.cs` — Application-layer interface; `SendStructuredAsync<T>` signature with `JsonNode schema` + `CancellationToken ct = default`
- **NEW** `tests/CookBot.Tests/AI/FakeHttpMessageHandler.cs` — internal sealed test double; first HTTP-layer fake in this codebase
- **NEW** `tests/CookBot.Tests/AI/AnthropicStructuredOutputTests.cs` — 6 xUnit Facts; `TestableAnthropicAiService` subclass overrides protected virtual `CreateHttpClient`
- **MOD** `src/CookBot.Infrastructure/AI/AnthropicAiService.cs` — class declaration adds `IStructuredAiService`; `CreateHttpClient` becomes `protected virtual`; constructor adds `RecipeValidator`; new `SendStructuredAsync<T>` method (~140 lines); using directives add `JsonNode`, `Application.AI`, `Application.Recipes`, `Domain.Recipes`
- **MOD** `src/CookBot.Infrastructure/DependencyInjection.cs` — adds `using CookBot.Application.AI;` and the `AddScoped<IStructuredAiService>(sp => (IStructuredAiService)sp.GetRequiredService<IAiService>())` factory line directly below the existing `IAiService` registration

## Build & Test Output

```
$ dotnet build FreelovesCookBot.sln -c Debug
Build succeeded.
    0 Warning(s)
    0 Error(s)

$ dotnet test FreelovesCookBot.sln --no-build -c Debug
Passed!  - Failed:     0, Passed:   139, Skipped:     0, Total:   139, Duration: 1 s

$ dotnet test --filter "FullyQualifiedName~AnthropicStructuredOutputTests"
Passed!  - Failed:     0, Passed:     6, Skipped:     0, Total:     6, Duration: 100 ms
```

## Threat Model Mitigations Honored

| Threat ID | Category | Status | Verification |
|-----------|----------|--------|--------------|
| T-02P02-01 | Information Disclosure (4xx body echoed to UI) | mitigated | `grep -q "SecretRedactor.Redact.*errorBody" src/CookBot.Infrastructure/AI/AnthropicAiService.cs` passes; `SendStructuredAsync_Http401_ReturnsSanitizedErrorWithoutLeakingKey` asserts `sk-ant-` absent from `SanitizedError` |
| T-02P02-02 | Information Disclosure (deserialization message containing key) | mitigated | `JsonException` catch routes `ex.Message` through `SecretRedactor.Redact`; `SendStructuredAsync_TruncatedJson_ReturnsSanitizedDeserializationError` asserts `sk-ant-` absent |
| T-02P02-03 | Information Disclosure (transport exception message) | mitigated | Two `catch (Exception ex) when (ex is not OperationCanceledException)` blocks (client-init and SendAsync) both wrap message in `SecretRedactor.Redact(...)`. Total redaction call count: 4. |
| T-02P02-04 | Denial of Service (refusal exhausts repair-loop budget) | mitigated | `grep -q 'stopReason == "refusal"' src/CookBot.Infrastructure/AI/AnthropicAiService.cs` passes; `SendStructuredAsync_RefusalStopReason_ShortCircuits` asserts the refusal message returns without deserialization attempt |
| T-02P02-05 | Tampering (malformed SSE event) | mitigated | The SSE loop wraps `JsonDocument.Parse(data)` in `try/catch (JsonException) { /* skip */ }`; only `text_delta` chunks grow the `accumulated` buffer; non-content events cannot inject content |
| T-02P02-06 | Spoofing (TestableAnthropicAiService reachable from production) | accepted | `TestableAnthropicAiService` lives in `tests/CookBot.Tests/`; not referenced by any `src/` project. The `protected virtual` change to `CreateHttpClient` is the production-side hook. Risk accepted; documented. |

## Layering Verification

- `grep -q "SendStructuredAsync" src/CookBot.Domain/Interfaces/IAiService.cs` returns no match (Domain unchanged)
- `grep -rn "CookBot.Infrastructure" src/CookBot.Application/AI/` returns only XML doc-comment `<see cref>` references — no code-level imports or usings (layering invariant preserved)
- `grep -rE "Microsoft\.Extensions\.AI|<PackageReference[^>]*Anthropic|Newtonsoft" --include="*.csproj" src/ tests/` returns zero matches (no forbidden packages introduced)
- `git diff` of `*.csproj` shows zero `PackageReference` additions

## Decisions Made

- **Constructor signature kept its existing `IOptions<CookBotSettings>` shape** — the plan text described `IConfiguration` + `ICurrentUserService` based on stale codebase assumptions (neither exists in the constructor today). `RecipeValidator` was added alongside the existing `IOptions` parameter, satisfying the spirit of the plan without breaking the established pattern.
- **`CreateHttpClient` is `protected virtual` (not `protected internal virtual` or DI-factoried)** — minimum-blast-radius testability hook per PATTERNS.md "AnthropicStructuredOutputTests" recommendation. The seam is exercised exclusively by `TestableAnthropicAiService` in tests.
- **`while (true) { line = await ReadLineAsync(); if (line is null) break; }` instead of `while (!reader.EndOfStream)`** — auto-fixed CA2024 warning; sync `EndOfStream` is forbidden in async methods because it blocks the I/O thread. Matches the existing `StreamMessageAsync` pattern.
- **`http.Dispose()` in `finally`, not `using var http`** — needed because the `try/catch` for `CreateHttpClient` happens before any usage scope; structured the disposal explicitly to keep the early-return path clean.
- **Validation only runs for `RecipeDocument` Ts; other Ts get `Ok=true` on deserialization success** — the pattern-match `if (doc is RecipeDocument recipeDoc)` keeps the validator-less generic path forward-compatible while honoring the "RecipeValidator runs after deserialize" contract from D-02.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 — Blocking issue] Constructor signature mismatch with plan text**
- **Found during:** Task 2 (read-first of AnthropicAiService.cs)
- **Issue:** Plan text described constructor params as `(IConfiguration config, ICurrentUserService currentUser, RecipeValidator validator)`, but the actual existing constructor takes `(IOptions<CookBotSettings> settings)`. `ICurrentUserService` doesn't exist in the codebase as an injectable service shaped that way; `IConfiguration` is not used by `AnthropicAiService` directly.
- **Fix:** Kept the existing `IOptions<CookBotSettings>` parameter and added `RecipeValidator` alongside it. The functional behavior matches the plan: `SendStructuredAsync<T>` reads `_settings.AnthropicApiKey` (via `CreateHttpClient`'s existing fallback) and uses `_validator.Validate` for `RecipeDocument`. `resolvedKey` for redaction = `apiKey ?? _settings.AnthropicApiKey`.
- **Files modified:** `src/CookBot.Infrastructure/AI/AnthropicAiService.cs`
- **Commit:** 50a64d8

**2. [Rule 1 — Bug] CA2024 warning: `reader.EndOfStream` in async method**
- **Found during:** Task 2 verification (`dotnet build`)
- **Issue:** `while (!reader.EndOfStream)` triggers CA2024 because `EndOfStream` is synchronous and blocks the I/O thread when called inside an async method — also subtly broken for streaming HTTP since it can return false while waiting for the next chunk.
- **Fix:** Replaced with the existing `StreamMessageAsync` pattern: `while (true) { var line = await reader.ReadLineAsync(ct); if (line is null) break; ... }`. Matches the codebase's established async SSE-reading style.
- **Files modified:** `src/CookBot.Infrastructure/AI/AnthropicAiService.cs`
- **Commit:** 50a64d8 (folded into Task 2 commit)

**3. [Rule 1 — Bug] Test fixture used string ingredient ids; canonical schema uses int**
- **Found during:** Task 3 (read-first of `IngredientEntry.cs`)
- **Issue:** Plan text used `"id":"ing-1"` in test JSON, but `IngredientEntry.Id` is `int` (required), so JSON deserialization would fail and the "valid recipe" test would have spuriously failed.
- **Fix:** Test fixtures use integer ids (`"id":1`).
- **Files modified:** `tests/CookBot.Tests/AI/AnthropicStructuredOutputTests.cs`
- **Commit:** 384b6c9

**4. [Rule 1 — Bug] Raw-string interpolation triple-brace clash**
- **Found during:** Task 3 first build
- **Issue:** `$$"""..."""` raw strings used `}}}` (close JSON brace + close interpolation hole + close raw string) which the C# parser disambiguates as needing more `$` characters. Three CS9007 errors.
- **Fix:** Replaced the affected fixture-builder helpers with simple string concatenation (e.g. `"{\"type\":\"content_block_delta\",...,\"text\":" + escaped + "}}"`). The intent (escaped JSON in a literal) is preserved; legibility is comparable.
- **Files modified:** `tests/CookBot.Tests/AI/AnthropicStructuredOutputTests.cs`
- **Commit:** 384b6c9

### Architectural Deviation (Carried from Plan's `<deviations>` block)

**Layering: IStructuredAiService in Application, NOT Domain.** CONTEXT.md `<canonical_refs>` named `src/CookBot.Domain/Interfaces/IAiService.cs — adds SendStructuredAsync<T> overload signature` — this plan implements the surface as a NEW Application-layer interface (`src/CookBot.Application/AI/IStructuredAiService.cs`) and leaves the Domain interface untouched. Reason: `StructuredResult<T>` references `JsonNode` (System.Text.Json.Nodes) and `ValidationResult` (CookBot.Application.Recipes) — neither of which Domain may reference per Clean Architecture (CONVENTIONS.md). PATTERNS.md "Key Architectural Decision for Planner" Option 2 applied. AI-01 ("IAiService gains a structured-output overload") is interpreted as "the AI service surface gains the overload"; the SAME `AnthropicAiService` instance now implements two interfaces, so functionally the AI service does gain the overload — through a separate Application-layer interface. CONTEXT.md `<canonical_refs>` should be updated to reflect this in Plan 04 (documentation correction).

---

**Total deviations:** 4 auto-fixes (Rule 1 ×3, Rule 3 ×1) + 1 architectural deviation pre-approved in plan
**Impact on plan:** None functional — all task acceptance criteria met. The constructor-signature deviation is the largest; it changes ~3 lines vs. the plan text but produces identical runtime behavior.

## Issues Encountered

None — build issues were caught and resolved within the same task. No background-process / CLR errors this time. No transient test flakes.

## User Setup Required

None — pure-code change. No new packages, no DB migration, no environment variables. Live integration with Anthropic still runs through the existing API key resolution path (no behavior change for `IAiService` callers).

## Next Plan Readiness

- **Wave 3 (Plan 02-03 — `IAiRecipeGenerator` orchestrator)** can now call `IStructuredAiService.SendStructuredAsync<RecipeDocument>(...)` directly. The orchestrator wraps it with the 2-retry repair loop and the `PromptInjectionGuard.WrapRecipe` calls. Inject `IStructuredAiService` into the orchestrator constructor; the existing `RecipeJsonSchemaProvider` provides the schema parameter.
- **`StructuredResult<T>` interpretation contract for callers:**
  - `Ok=true, Value!=null, Validation=null, SanitizedError=null` → success
  - `Ok=false, Value=null, RawResponse!=null, Validation!=null, SanitizedError=null` → semantic-validation failure (candidate for repair loop)
  - `Ok=false, Value=null, RawResponse!=null, Validation=null, SanitizedError!=null` → deserialization failure (candidate for repair loop)
  - `Ok=false, Value=null, RawResponse=null, Validation=null, SanitizedError!=null` → transport / auth / refusal (NOT a repair candidate; surface to user)
- **Refusal short-circuit**: callers detect by `SanitizedError` containing the literal "declined" — Wave 3 should match this exactly (or expose a discriminator if preferred during Wave 3 execution).
- **AI-01 marked complete only after Wave 3** — this plan delivers the transport; the orchestrator that fulfills the requirement's "wires Anthropic's `output_config.format` into recipe-emitting AI calls" contract is Wave 3.

## Threat Flags

None — no new security-relevant surface beyond the threat-model rows already covered by the plan's `<threat_model>` block.

## Self-Check: PASSED

Verified after writing SUMMARY:
- FOUND: src/CookBot.Application/AI/StructuredResult.cs
- FOUND: src/CookBot.Application/AI/IStructuredAiService.cs
- FOUND: tests/CookBot.Tests/AI/FakeHttpMessageHandler.cs
- FOUND: tests/CookBot.Tests/AI/AnthropicStructuredOutputTests.cs
- FOUND modified: src/CookBot.Infrastructure/AI/AnthropicAiService.cs (now : IAiService, IStructuredAiService)
- FOUND modified: src/CookBot.Infrastructure/DependencyInjection.cs (AddScoped<IStructuredAiService>)
- FOUND commit: a9c5c47 (feat: contract types)
- FOUND commit: 50a64d8 (feat: SendStructuredAsync + DI)
- FOUND commit: 384b6c9 (test: HTTP-layer tests)
- VERIFIED: 139/139 tests pass
- VERIFIED: 0 warnings, 0 errors
- VERIFIED: Domain IAiService unchanged
- VERIFIED: 0 new NuGet packages
- VERIFIED: 4 SecretRedactor.Redact call sites in AnthropicAiService.cs

---
*Phase: 02-ai-structured-output-conformance*
*Plan: 02*
*Completed: 2026-04-26*
