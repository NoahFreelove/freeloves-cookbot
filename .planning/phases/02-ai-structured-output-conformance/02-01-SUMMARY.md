---
phase: 02-ai-structured-output-conformance
plan: 01
subsystem: ai
tags: [ai, security, redaction, prompt-injection, dotnet, blazor, regex, static-helpers]

# Dependency graph
requires:
  - phase: 01-canonical-recipe-format
    provides: "RecipeDocument canonical type, RecipeJsonSchemaProvider, RecipeValidator (no direct call yet — Wave 1 helpers are independent; later Phase-2 waves consume both Phase-1 outputs and these helpers)"
provides:
  - "SecretRedactor.Redact(string, string?) — AI-07 chokepoint stripping sk-ant-* tokens, x-api-key/authorization header values, and verbatim resolved-key matches from any error/log/response string"
  - "PromptInjectionGuard.WrapRecipe(string) — AI-08 mitigation that wraps recipe content in <recipe>...</recipe> and strips embedded </recipe> closures so injected payloads cannot escape the fence"
affects: [02-02-anthropic-http-transport, 02-03-recipe-cooking-ai-context, 02-04-orchestrator, 02-05-system-prompt]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Pure-static helper class (no DI, no I/O) — established in Application/AI and Infrastructure/AI"
    - "Compiled regex with `RegexOptions.Compiled | RegexOptions.IgnoreCase` initialized at file scope"
    - "Defensive `string.IsNullOrEmpty` guards on public string-processing helpers"

key-files:
  created:
    - "src/CookBot.Infrastructure/AI/SecretRedactor.cs"
    - "src/CookBot.Application/AI/PromptInjectionGuard.cs"
    - "tests/CookBot.Tests/AI/SecretRedactorTests.cs"
    - "tests/CookBot.Tests/AI/PromptInjectionGuardTests.cs"
  modified: []

key-decisions:
  - "SecretRedactor lives in Infrastructure/AI (caller is AnthropicAiService, also Infrastructure) — keeps Application layer free of HTTP-related types"
  - "PromptInjectionGuard lives in Application/AI (callers are RecipeCookingAiContext + AiRecipeGenerator, both Application) — no Infrastructure leakage"
  - "WrapRecipe strip is case-sensitive per D-12 — Anthropic's XML-tag matching is case-sensitive at the model level; uppercase variants intentionally pass through"
  - "Verbatim resolvedKey replacement runs BEFORE regex passes in SecretRedactor — more precise than regex when caller has the exact key"
  - "ReDoS risk explicitly accepted (T-02P01-04): no `Timeout` parameter on either compiled regex; inputs bounded by Anthropic ≤256 KB response size"

patterns-established:
  - "Static-helper namespace layout: src/CookBot.{Layer}/AI/{Name}.cs — Wave 2+ adds AiRecipeGenerator, JsonRepairAttempt to Application/AI/"
  - "TDD red-then-green commits: `test(02-01): add failing tests` followed by `feat(02-01): implement` keeps the gate sequence visible in `git log`"
  - "xUnit Fact tests with global `using Xunit;` (csproj-level <Using>) — no per-file using directives needed for xUnit primitives"

requirements-completed: [AI-07, AI-08]

# Metrics
duration: 23min
completed: 2026-04-26
---

# Phase 02 Plan 01: AI Security Helpers Summary

**Two pure-static helpers — SecretRedactor strips API keys / header values from error strings (AI-07), PromptInjectionGuard wraps recipe content in `<recipe>` tags and strips embedded closures to prevent injection escape (AI-08).**

## Performance

- **Duration:** 23 min
- **Started:** 2026-04-26T04:57:29Z
- **Completed:** 2026-04-26T05:20:51Z
- **Tasks:** 2 (both TDD)
- **Files created:** 4

## Accomplishments

- **AI-07 chokepoint shipped.** `SecretRedactor.Redact` strips `sk-ant-[A-Za-z0-9_\-]+` (case-insensitive), `x-api-key` / `authorization` header values, and verbatim resolved-key matches. The D-18 canonical fixture (`"error: x-api-key: sk-ant-foo123 with body {api_key: sk-ant-bar456}"`) is now provably free of `sk-ant-` substrings after redaction.
- **AI-08 wrap shipped.** `PromptInjectionGuard.WrapRecipe` returns `<recipe>\n{stripped}\n</recipe>` where embedded `</recipe>` closures are stripped before the wrap. The closing-tag-injection test verifies exactly one `</recipe>` remains in output, so an injected payload cannot escape the fence and append new directives.
- **Layering invariant preserved.** `CookBot.Application.AI` references nothing from `CookBot.Infrastructure`; `SecretRedactor` lives in Infrastructure because its caller (`AnthropicAiService`) is also Infrastructure.
- **Zero new dependencies.** No NuGet packages added. No forbidden packages (`Microsoft.Extensions.AI`, `Newtonsoft`, `NJsonSchema`) introduced.
- **Full suite green.** 133/133 tests pass (122 from Phase 1 + 11 new in this plan: 6 SecretRedactor + 5 PromptInjectionGuard).

## Task Commits

Each task followed TDD with explicit RED → GREEN gates:

1. **Task 1 RED: SecretRedactor failing tests** — `0f81d91` (test) — 6 tests added; build fails with `CS0103: SecretRedactor does not exist`
2. **Task 1 GREEN: SecretRedactor implementation** — `e772f2b` (feat) — D-16 regex patterns verbatim; all 6 tests pass
3. **Task 2 RED: PromptInjectionGuard failing tests** — `3736d32` (test) — 5 tests added; build fails with `CS0234: namespace 'AI' does not exist in 'CookBot.Application'`
4. **Task 2 GREEN: PromptInjectionGuard implementation** — `54e9c68` (feat) — D-12 expression body verbatim; all 5 tests pass; full suite 133/133

No refactor commits — both implementations match locked spec language (D-12 / D-16) and required no cleanup.

**Plan metadata commit:** added by `/gsd-execute-phase` after this SUMMARY (includes STATE.md, ROADMAP.md, REQUIREMENTS.md updates).

## Files Created/Modified

- `src/CookBot.Infrastructure/AI/SecretRedactor.cs` — public static class with two compiled regexes (`ApiKeyPattern`, `HeaderPattern`) and `Redact(raw, resolvedKey?)` method
- `src/CookBot.Application/AI/PromptInjectionGuard.cs` — public static class with single expression-bodied `WrapRecipe(raw)` method
- `tests/CookBot.Tests/AI/SecretRedactorTests.cs` — 6 xUnit Facts covering D-18 canonical fixture, authorization header, verbatim key, no-secret pass-through, null/empty guard, case-insensitive matching
- `tests/CookBot.Tests/AI/PromptInjectionGuardTests.cs` — 5 xUnit Facts covering wrap shape, closing-tag strip (exactly-one-`</recipe>` assertion), plain-content newline format, empty wrap, case-sensitive D-12 strip

## Build & Test Output

```
$ dotnet build FreelovesCookBot.sln -c Debug
Build succeeded.
    0 Warning(s)
    0 Error(s)

$ dotnet test FreelovesCookBot.sln --no-build -c Debug
Passed!  - Failed:     0, Passed:   133, Skipped:     0, Total:   133, Duration: 1 s

$ dotnet test --filter "FullyQualifiedName~SecretRedactorTests|FullyQualifiedName~PromptInjectionGuardTests"
Passed!  - Failed:     0, Passed:    11, Skipped:     0, Total:    11, Duration: 43 ms
```

## Threat Model Mitigations Honored

| Threat ID | Category | Status | Verification |
|-----------|----------|--------|--------------|
| T-02P01-01 | Information Disclosure (key leakage) | mitigated | D-16 regex committed verbatim (`grep -q 'sk-ant-\[A-Za-z0-9_\\-\]+' src/CookBot.Infrastructure/AI/SecretRedactor.cs` passes); D-18 canonical-fixture test asserts zero `sk-ant-` substrings post-redaction |
| T-02P01-02 | Tampering (prompt injection) | mitigated | D-12 strip body committed verbatim (`grep -q 'Replace("</recipe>", "")'` passes); injection test asserts exactly one `</recipe>` in output |
| T-02P01-03 | Information Disclosure (NRE on null) | mitigated | `string.IsNullOrEmpty` guard tested: `Redact(null!)` returns `null` without throwing |
| T-02P01-04 | Denial of Service (regex ReDoS) | accepted | No `Timeout` set; inputs bounded by Anthropic ≤256 KB response size; no nested-quantifier-on-overlapping-class constructs in either pattern |

## Layering Verification

- `grep -rn "CookBot.Infrastructure" src/CookBot.Application/AI/` returns zero matches (confirms Application/AI has no Infrastructure dependency)
- `grep -rE "Microsoft\.Extensions\.AI|Newtonsoft|NJsonSchema" --include="*.csproj" src/ tests/` returns zero matches (confirms no forbidden packages)
- `git diff --cached -- '*.csproj'` returns no `PackageReference` additions (confirms zero new NuGet refs)

## Decisions Made

- **No optional resolved-key positional argument shuffle.** Plan specified `Redact(string raw, string? resolvedKey = null)` exactly; kept that signature so all later callers (`AnthropicAiService` catch sites in Plan 02) get a default for the no-resolved-key path.
- **`(?i)` inline flag kept on header pattern despite redundant `RegexOptions.IgnoreCase`.** D-16 spec language is locked verbatim; both flags coexisting is harmless and matches the threat-model wording — refactoring to one form would diverge from the spec.

## Deviations from Plan

None — plan executed exactly as written. Both implementations match the D-12 / D-16 / D-18 locked spec language verbatim. No auto-fixes (Rules 1-3) needed; no architectural decisions (Rule 4) surfaced; no authentication gates encountered.

---

**Total deviations:** 0
**Impact on plan:** N/A — plan was tightly specified with locked code blocks; execution was a direct transcription with TDD gating.

## Issues Encountered

- One transient `Fatal error. Internal CLR error. (0x80131506)` in a backgrounded `dotnet test` task output. Re-running `dotnet test` in the foreground produced clean `Passed!` output (133/133). The error was a benign teardown artifact in the background-process I/O capture, not a test failure — the exit code on the original run was 0.

## User Setup Required

None — no external service configuration required.

## Next Phase Readiness

- **Wave 2 (Plan 02 — Anthropic HTTP transport)** can now wrap `AnthropicAiService` catch sites with `SecretRedactor.Redact(...)` calls. The signature is final; no breaking changes expected.
- **Wave 3 (Plan 03 — RecipeCookingAiContext) and Wave 4 (Plan 04 — orchestrator)** can now call `PromptInjectionGuard.WrapRecipe(...)` at every recipe-body injection site. The expression-bodied method is allocation-cheap (one `Replace` + one interpolated string) and safe to call on every prompt assembly.
- No blockers for Phase 2 Wave 2.

## Self-Check: PASSED

Verified after writing SUMMARY:
- FOUND: src/CookBot.Infrastructure/AI/SecretRedactor.cs
- FOUND: src/CookBot.Application/AI/PromptInjectionGuard.cs
- FOUND: tests/CookBot.Tests/AI/SecretRedactorTests.cs
- FOUND: tests/CookBot.Tests/AI/PromptInjectionGuardTests.cs
- FOUND commit: 0f81d91 (test: SecretRedactor RED)
- FOUND commit: e772f2b (feat: SecretRedactor GREEN)
- FOUND commit: 3736d32 (test: PromptInjectionGuard RED)
- FOUND commit: 54e9c68 (feat: PromptInjectionGuard GREEN)

---
*Phase: 02-ai-structured-output-conformance*
*Plan: 01*
*Completed: 2026-04-26*
