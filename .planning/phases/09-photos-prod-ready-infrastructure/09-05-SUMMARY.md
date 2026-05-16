---
phase: 09-photos-prod-ready-infrastructure
plan: 05
subsystem: ai-telemetry
tags: [ai, telemetry, sse, anthropic, ef-core, sqlite, data-protection, validators, prompts, decimal-currency-math]

# Dependency graph
requires:
  - phase: 09 (Plan 09-01)
    provides: RecipePhotoUrlValidator (PHOTO-07 first half — Singleton scheme allowlist)
  - phase: 09 (Plan 09-02)
    provides: Recipe.PhotoUrl + Recipe.Description columns on DB (consumed via doc.PhotoUrl on AI return path)
  - phase: 09 (Plan 09-04)
    provides: |
      DataProtection key ring + sentinel-prefix re-encryption marker comment in DatabaseSeeder
      (`// 365-day AiUsageLog cleanup will be inserted here by Plan 09-05`), CookBotDbContext
      DbSet<DataProtectionKey>, AiApiKeyResolutionService DecryptIfNeeded shape.
  - phase: 08 (Plan 08-13 / CLEAN-03)
    provides: Verify-based prompt snapshot (`PromptSnapshotTests.BuildSystemPrompt.verified.txt`) — regenerated atomically in this plan to absorb D-42 prose.
provides:
  - "AiUsageLog domain entity (Id, UserId, KeyOwnerId, ModelName, InputTokens, OutputTokens, EstimatedCostUsd, IsRetryAttempt, Timestamp) — append-only telemetry row"
  - "Composite index IX_AiUsageLogs_KeyOwnerId_Timestamp + decimal(18,6) cost column + Cascade FK on UserId / Restrict FK on KeyOwnerId"
  - "EF migration AddAiUsageLog (20260516185336) — backup hook fires via IDatabaseBackupService on first boot"
  - "StructuredResult<T> InputTokens/OutputTokens fields (`=0` defaults — backwards-compatible)"
  - "AnthropicAiService SSE parse loop captures message_start.message.usage.input_tokens + cumulative message_delta.usage.output_tokens (OVERWRITE — never sums)"
  - "AnthropicAiService AI-return-path PhotoUrl scrubbing via RecipePhotoUrlValidator.TryValidate (PHOTO-07 second half)"
  - "IAiUsageLogWriter (Application) + AiUsageLogWriter (Infrastructure) — telemetry sink interface so the Application orchestrator never directly depends on CookBotDbContext"
  - "AiRecipeGenerator.GenerateAsync per-attempt telemetry write at function END (single WriteTelemetryAsync helper at every return site; PITFALL H9 prevention by structure)"
  - "AiRecipeGenerator userId/keyOwnerId optional params; AiChat.razor plumbs CurrentUserId + (SharedFromUserId ?? CallerId)"
  - "appsettings.json CookBot:AiPricing matrix (Haiku 4.5 $1/$5, Sonnet 4.6 $3/$15, Opus 4.7 $5/$25 per million) + AiPricingVerifiedDate 2026-05-16"
  - "CookBotSettings.AiPricing dictionary + AiPricingVerifiedDate DateOnly binding"
  - "PromptBuilderService D-42 prose distinguishing `description` from `steps[0]` (via RecipeSchemaDocumentationProvider.FormatPrompt)"
  - "DatabaseSeeder 365-day AiUsageLog rolling cleanup via ExecuteDeleteAsync inserted at the Plan 09-04 marker (D-41) — runs BEFORE sentinel-prefix re-encryption"
affects:
  - "Phase 10 PROD-17 — per-user AI-usage widget reads from AiUsageLogs filtered by KeyOwnerId + Timestamp window; aggregation MUST filter WHERE IsRetryAttempt=false to avoid double-counting repair calls"
  - "Any future AI orchestrator changes — telemetry write site lives at GenerateAsync END (one place), not inside the retry loop"

# Tech tracking
tech-stack:
  added: []   # Microsoft.Extensions.Options 10.0.3 promoted from transitive to direct ref in CookBot.Application (no new package install — version already in graph)
  patterns:
    - "Interface-in-Application + impl-in-Infrastructure (IAiUsageLogWriter / AiUsageLogWriter) — sibling to existing IStructuredAiService pattern; preserves Clean architecture invariant (Application cannot reference Infrastructure)"
    - "Decimal currency math: (tokens * $/1M) / 1_000_000m — never float/double; column decimal(18,6) preserves sub-cent precision for Haiku-tier calls"
    - "Per-attempt telemetry accumulation: `List<(StructuredResult, IsRetryAttempt)>` accumulated through orchestrator loop, single WriteTelemetryAsync helper called at every return site (H9 prevention by structure)"
    - "SSE cumulative-counter pattern: message_delta.usage.output_tokens is overwritten on each event, never +=, because Anthropic's stream emits cumulative snapshots"
    - "Defensive optional FKs: when telemetry userId/keyOwnerId is null, the entire write skips (preserves v1.2 AI-off contract; defense-in-depth alongside caller-side gates)"

key-files:
  created:
    - "src/CookBot.Domain/Entities/AiUsageLog.cs"
    - "src/CookBot.Infrastructure/Data/Configurations/AiUsageLogConfiguration.cs"
    - "src/CookBot.Application/AI/IAiUsageLogWriter.cs"
    - "src/CookBot.Infrastructure/AI/AiUsageLogWriter.cs"
    - "src/CookBot.Application/DTOs/AiPricingEntry.cs"
    - "src/CookBot.Infrastructure/Migrations/20260516185336_AddAiUsageLog.cs (+ .Designer)"
    - "tests/CookBot.Tests/Configuration/AiPricingTests.cs"
    - "tests/CookBot.Tests/AI/AnthropicAiServiceTokenTests.cs"
    - "tests/CookBot.Tests/AI/TokenTelemetryTests.cs"
  modified:
    - "src/CookBot.Application/AI/StructuredResult.cs (+ InputTokens, + OutputTokens with `=0`)"
    - "src/CookBot.Application/AI/IAiRecipeGenerator.cs (+ userId/keyOwnerId optional)"
    - "src/CookBot.Application/AI/AiRecipeGenerator.cs (telemetry accumulator + WriteTelemetryAsync helper + pricing TryGetValue)"
    - "src/CookBot.Application/Recipes/RecipeSchemaDocumentationProvider.cs (D-42 prose)"
    - "src/CookBot.Application/DTOs/CookBotSettings.cs (+ AiPricing dict + AiPricingVerifiedDate)"
    - "src/CookBot.Application/CookBot.Application.csproj (+ Microsoft.Extensions.Options 10.0.3 promoted from transitive)"
    - "src/CookBot.Infrastructure/AI/AnthropicAiService.cs (RecipePhotoUrlValidator ctor injection; SSE token capture; PhotoUrl scrub on return path; all 6 return sites carry InputTokens/OutputTokens)"
    - "src/CookBot.Infrastructure/Data/DatabaseSeeder.cs (D-41 365-day cleanup at marker)"
    - "src/CookBot.Infrastructure/Data/CookBotDbContext.cs (+ DbSet<AiUsageLog>)"
    - "src/CookBot.Infrastructure/DependencyInjection.cs (+ IAiUsageLogWriter scoped reg)"
    - "src/CookBot.Web/appsettings.json (+ CookBot:AiPricing block + AiPricingVerifiedDate)"
    - "src/CookBot.Web/Components/Pages/AiChat.razor (passes CurrentUserId + SharedFromUserId)"
    - "tests/CookBot.Tests/Snapshots/PromptSnapshotTests.BuildSystemPrompt.verified.txt (regenerated atomically with D-42 prose)"
    - "tests/CookBot.Tests/AI/AiRecipeGeneratorTests.cs (extended MakeOrchestrator with NoOpAiUsageLogWriter + IOptions<CookBotSettings>)"
    - "tests/CookBot.Tests/AI/AnthropicStructuredOutputTests.cs (TestableAnthropicAiService ctor takes RecipePhotoUrlValidator)"
    - "tests/CookBot.Tests/AI/AiRecipeFixtureTests.cs / PromptInjectionResistanceTests.cs (live-API tests updated for new ctor/sig)"

key-decisions:
  - "Adopted Interface-in-Application + impl-in-Infrastructure for telemetry writes (IAiUsageLogWriter / AiUsageLogWriter) — Application project cannot reference CookBotDbContext per Clean architecture; matches existing IStructuredAiService / AnthropicAiService split."
  - "Telemetry write at GenerateAsync END (PITFALL H9 prevention by structure) — accumulate tuples, single WriteTelemetryAsync helper called at every return site. The write site appears exactly once in the orchestrator file."
  - "Cumulative output_tokens semantic: OVERWRITE (not sum). Per Anthropic streaming spec — last value wins. Grep gate `! grep -q 'outputTokens +=' AnthropicAiService.cs` is the regression guard."
  - "Decimal currency math throughout: (InputTokens * Input$/1M + OutputTokens * Output$/1M) / 1_000_000m. Column decimal(18,6). A Haiku 100/50-token call ($0.00035) tests as exactly 0.00035m — no float-rounding regression."
  - "Defensive optional telemetry: when AiRecipeGenerator.GenerateAsync is called with null userId or null keyOwnerId, the entire WriteTelemetryAsync helper short-circuits. This preserves the v1.2 AI-off contract (caller-side gate authoritative) and protects against future call sites that don't plumb user context."
  - "AI-return-path PhotoUrl scrub: AnthropicAiService runs RecipePhotoUrlValidator.TryValidate on doc.PhotoUrl after deserialization. Reject lane nulls the field via `with`; accept-and-normalize lane updates to the validator's canonical AbsoluteUri."

patterns-established:
  - "IAiUsageLogWriter — write-only Application-layer interface for any future append-only event-log sink that needs to live in the Application orchestrator but write via Infrastructure."
  - "Single-helper-at-every-return-site for accumulated state (PITFALL H9 prevention): better than try/finally because finally would force the helper to handle partially-built state; the helper is called explicitly with the final accumulator at each return."

requirements-completed: [PROD-12, PROD-13, PROD-14, PROD-15, PROD-16, PROD-17, PHOTO-07]
# Note: PROD-17 write-path is closed here; Phase 10 owns the read-path widget.

# Metrics
duration: ~75min
completed: 2026-05-16
---

# Phase 09 Plan 05: AI Token-Cost Telemetry Write Path Summary

**End-to-end AI usage logging: AnthropicAiService SSE token capture (cumulative-semantic-safe) → StructuredResult plumbing → AiRecipeGenerator per-attempt write at function END (PITFALL H9 prevention) → AiUsageLog rows priced from appsettings.json, plus D-42 prompt prose, D-41 365-day cleanup, and PHOTO-07 AI-return-path PhotoUrl scrub.**

## Performance

- **Duration:** ~75 min
- **Started:** 2026-05-16T19:30:00Z (approx)
- **Completed:** 2026-05-16T20:45:00Z (approx)
- **Tasks:** 2 (TDD: RED + GREEN for both)
- **Files modified:** 22 (10 created, 12 modified)
- **Tests:** 294 passing (filter Category!=RequiresApiKey) — +9 over baseline (3 AiPricingTests, 4 TokenTelemetryTests, 2 AnthropicAiServiceTokenTests)

## Accomplishments

- **AiUsageLog telemetry write path is live end-to-end.** A successful AI generation writes a single row with IsRetryAttempt=false; a 2-attempt repair writes two rows with the second flagged IsRetryAttempt=true; budget-exhausted (3 calls) writes three rows. Aggregation `WHERE IsRetryAttempt=false` returns the primary-attempt cost only — verified by `Aggregation_ExcludesRetryAttempts_ReturnsOnlyPrimaryCost`.
- **PITFALL H9 is closed by structure.** The orchestrator accumulates per-attempt tuples and flushes them via `WriteTelemetryAsync` called at every return site (never inside the loop body). The write site appears exactly once in `AiRecipeGenerator.cs`.
- **PITFALL: cumulative output_tokens overcount is closed.** AnthropicAiService overwrites (never `+=`) on each `message_delta.usage.output_tokens`. Asserted by `Parser_CapturesCumulativeOutputTokens_OverwritesNotSums` with two message_delta events (100 then 250) — final value 250, not 350.
- **Decimal currency math.** Column `decimal(18,6)` preserves sub-cent precision; `CostCalculation_HaikuExample_BelowOneCent` asserts a 100/50-token Haiku call costs exactly `0.00035m` (would round to 0 with float/double).
- **AI-return-path PhotoUrl scrub (PHOTO-07 second half).** AnthropicAiService runs `RecipePhotoUrlValidator.TryValidate` on emitted `doc.PhotoUrl`; reject lane nulls the field via `with`, accept-and-normalize lane updates to canonical `AbsoluteUri`.
- **D-42 prompt prose.** Two field-level clauses distinguishing `description` from `steps[0]` added to `RecipeSchemaDocumentationProvider.FormatPrompt`; Verify snapshot regenerated atomically in the same commit. Phase 8 PromptSnapshotTests stays green against the regenerated `.verified.txt`.
- **D-41 365-day cleanup.** Inserted at the Plan 09-04 marker comment via `context.AiUsageLogs.Where(r => r.Timestamp < cutoff).ExecuteDeleteAsync()`. Runs BEFORE the sentinel-prefix re-encryption pass per documented boot order.

## Task Commits

1. **Task 1: AiUsageLog entity + AiPricing config foundation** — `c443f8e` (feat). TDD RED + GREEN consolidated in one commit (RED phase ran inline; the GREEN diff is what landed). 3 AiPricingTests pass; build + full suite green; no behavior change yet.
2. **Task 2: SSE token capture + telemetry write site + D-42 prose + D-41 cleanup + PHOTO-07 AI return path** — `5222ec6` (feat). TDD RED + GREEN consolidated; 6 new tests pass (TokenTelemetryTests x4, AnthropicAiServiceTokenTests x2); D-42 prompt snapshot regenerated atomically.

_Note: TDD phases collapsed per the plan's practical-scope guidance (single feat commit per task with tests living in the same commit as the code they cover) so the diff is reviewable as a unit. Verify snapshot regen landed atomically with the prompt prose change._

## Files Created/Modified

### Created

| File | Role |
|------|------|
| `src/CookBot.Domain/Entities/AiUsageLog.cs` | POCO telemetry row (11 props + 2 navs) |
| `src/CookBot.Infrastructure/Data/Configurations/AiUsageLogConfiguration.cs` | Composite index, decimal(18,6), Cascade/Restrict FKs |
| `src/CookBot.Application/AI/IAiUsageLogWriter.cs` | Write-only Application interface for telemetry sink |
| `src/CookBot.Infrastructure/AI/AiUsageLogWriter.cs` | EF impl: Add + SaveChanges per call |
| `src/CookBot.Application/DTOs/AiPricingEntry.cs` | POCO `{InputTokensPerMillionUsd, OutputTokensPerMillionUsd}` |
| `src/CookBot.Infrastructure/Migrations/20260516185336_AddAiUsageLog.cs` + .Designer | CreateTable + composite index + 2 FKs |
| `tests/CookBot.Tests/Configuration/AiPricingTests.cs` | 3 facts (load, date, sub-cent math) |
| `tests/CookBot.Tests/AI/AnthropicAiServiceTokenTests.cs` | 2 facts (input_tokens capture, cumulative-not-sum) — via HttpMessageHandler shim with hand-crafted SSE bytes |
| `tests/CookBot.Tests/AI/TokenTelemetryTests.cs` | 4 facts (success/retry-converges/budget-exhausted/aggregation-excludes-retries) — extends RecordingFakeStructuredAi pattern with temp SQLite |

### Modified

| File | Change |
|------|--------|
| `src/CookBot.Application/AI/StructuredResult.cs` | + `int InputTokens = 0, int OutputTokens = 0` positional params |
| `src/CookBot.Application/AI/IAiRecipeGenerator.cs` | + `int? userId = null, int? keyOwnerId = null` optional params |
| `src/CookBot.Application/AI/AiRecipeGenerator.cs` | Inject IAiUsageLogWriter + IOptions<CookBotSettings>; accumulate per-attempt (result, IsRetryAttempt) tuples; WriteTelemetryAsync helper at all 4 return sites; pricing TryGetValue fallback |
| `src/CookBot.Application/Recipes/RecipeSchemaDocumentationProvider.cs` | + D-42 field guidance (description = "1–2 sentences saying what the dish is"; steps[] = "begin with the first cooking action") |
| `src/CookBot.Application/DTOs/CookBotSettings.cs` | + `Dictionary<string, AiPricingEntry>? AiPricing` + `DateOnly? AiPricingVerifiedDate` |
| `src/CookBot.Application/CookBot.Application.csproj` | + Microsoft.Extensions.Options 10.0.3 (promoted from transitive — no new package install) |
| `src/CookBot.Infrastructure/AI/AnthropicAiService.cs` | + RecipePhotoUrlValidator ctor injection; new `message_start` SSE branch; cumulative `output_tokens` capture in `message_delta` (overwrite); PhotoUrl scrub after deserialize via `with`; all 6 return sites propagate InputTokens/OutputTokens |
| `src/CookBot.Infrastructure/Data/DatabaseSeeder.cs` | + D-41 365-day cleanup at the Plan 09-04 marker; runs BEFORE re-encryption |
| `src/CookBot.Infrastructure/Data/CookBotDbContext.cs` | + `DbSet<AiUsageLog> AiUsageLogs` |
| `src/CookBot.Infrastructure/DependencyInjection.cs` | + Scoped `IAiUsageLogWriter` registration |
| `src/CookBot.Web/appsettings.json` | + CookBot:AiPricing block (3 models) + AiPricingVerifiedDate "2026-05-16" |
| `src/CookBot.Web/Components/Pages/AiChat.razor` | Passes `UserService.CurrentUserId + (_effectiveAi?.SharedFromUserId ?? CallerId)` to GenerateAsync |
| `tests/CookBot.Tests/Snapshots/PromptSnapshotTests.BuildSystemPrompt.verified.txt` | Regenerated for D-42 (4-line addition) |
| Various test files | Constructor signature updates for new dep injection |

## Decisions Made

- **Interface-in-Application split for telemetry write.** The plan's behavior section described injecting `CookBotDbContext` directly into `AiRecipeGenerator`. Application can't reference Infrastructure per Clean architecture; the dependency goes the other way. I introduced `IAiUsageLogWriter` (Application) + `AiUsageLogWriter` (Infrastructure) — a sibling to the existing `IStructuredAiService` / `AnthropicAiService` pattern. This preserves the architectural invariant and is functionally identical; tests still observe rows landing in `db.AiUsageLogs`.
- **AiPricingTests scope.** Used in-memory `IConfiguration` (`AddJsonStream`) with the literal JSON from 09-RESEARCH Item 1; binds through `configuration.GetSection("CookBot").Get<CookBotSettings>()`. Asserts all three models, the verified date, and exact decimal precision on a sub-cent example.
- **AnthropicAiServiceTokenTests scope choice.** Per the plan's "practical-scope note" — I implemented the full SSE-stream test via an `HttpMessageHandler` shim returning a fixed byte stream. The cumulative-not-sum invariant is proven by feeding two `message_delta` events (output_tokens=100 then 250) and asserting final OutputTokens=250 (a naive `+=` would yield 350). This is preferable to grep-only verification because it fails loudly on regression.
- **PromptSnapshotTests regen included a UTF-8 BOM.** Verify writes the regenerated file with a BOM; I left it as-is rather than stripping. The test passes byte-stably either way and the BOM matches what Verify accepts. Phase 8 CLEAN-03 set this byte-stable expectation.
- **D-42 wording rephrased to avoid SCHEMA-10 alias collision.** See deviations.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 — Wording bug] D-42 prose used literal "summary" — collides with PromptDenylistTests `\bsummary\b` regex**
- **Found during:** Task 2 (D-42 GREEN phase, after snapshot regen)
- **Issue:** The plan's exact wording — "1–2 sentence summary of what the dish is" — contains the literal token `summary`. PromptDenylistTests (Phase 8 anti-regression for SCHEMA-10 alias tokens) treats `summary` as a forbidden alias for `description`. The test failed with: `Found opt-out phrases in src/CookBot.Application/Recipes/RecipeSchemaDocumentationProvider.cs: summary`.
- **Fix:** Rephrased to "1–2 sentences saying what the dish is — no history, no cooking advice". Preserves D-42 intent (terse field-level guidance), drops the literal alias token. The D-42 user-visible meaning (description ≠ history/advice; description ≠ first cooking step) is intact.
- **Files modified:** `src/CookBot.Application/Recipes/RecipeSchemaDocumentationProvider.cs`, `tests/CookBot.Tests/Snapshots/PromptSnapshotTests.BuildSystemPrompt.verified.txt`
- **Verification:** PromptDenylistTests + PromptSnapshotTests both green after rephrase.
- **Committed in:** `5222ec6` (Task 2 commit).

**2. [Rule 3 — Blocker] AiRecipeGenerator can't directly reference CookBotDbContext (Clean architecture)**
- **Found during:** Task 2 GREEN — when writing the telemetry helper
- **Issue:** The plan's behavior section describes "Inject CookBotDbContext (db)" into AiRecipeGenerator. Application project cannot reference Infrastructure per the project's Clean architecture invariant (CONVENTIONS.md). Direct injection would either require an upward reference (forbidden) or a project-graph cycle.
- **Fix:** Introduced `IAiUsageLogWriter` (Application) with `AiUsageLogWriter` impl (Infrastructure). Matches the existing `IStructuredAiService` / `AnthropicAiService` split. Functionally identical — the write still hits `db.AiUsageLogs` via the impl. Tests confirm rows land correctly.
- **Files modified:** `src/CookBot.Application/AI/IAiUsageLogWriter.cs` (new), `src/CookBot.Infrastructure/AI/AiUsageLogWriter.cs` (new), `src/CookBot.Infrastructure/DependencyInjection.cs` (+ Scoped registration), `src/CookBot.Application/AI/AiRecipeGenerator.cs` (uses the interface).
- **Verification:** Build green, all 294 tests green, `TokenTelemetryTests` observes rows landing in `db.AiUsageLogs`.
- **Committed in:** `5222ec6` (Task 2 commit).

**3. [Rule 3 — Blocker] CookBot.Application could not see `IOptions<T>` (compile error)**
- **Found during:** Task 2 GREEN — when adding `IOptions<CookBotSettings>` ctor param to AiRecipeGenerator
- **Issue:** `Microsoft.Extensions.Options` was a transitive dependency of CookBot.Application but not a direct reference, so the compiler refused to bind to `IOptions<T>`. Threat model line T-09-05-SC stipulates "No new NuGets in Plan 09-05".
- **Fix:** Promoted the existing transitive reference to a direct `<PackageReference>` in `CookBot.Application.csproj`. This is NOT a new package install — the exact same version (10.0.3) was already in the transitive graph (verified via `dotnet list package --include-transitive`). It's a manifest-only change that promotes visibility without changing the binary surface.
- **Files modified:** `src/CookBot.Application/CookBot.Application.csproj` (+ `<PackageReference Include="Microsoft.Extensions.Options" Version="10.0.3" />`).
- **Verification:** `dotnet list package --include-transitive | grep Options` showed 10.0.3 was already present pre-change. Build green post-change.
- **Committed in:** `5222ec6` (Task 2 commit).

---

**Total deviations:** 3 auto-fixed (1 wording bug, 2 blockers — both architectural)
**Impact on plan:** None on user-visible behavior. The IAiUsageLogWriter interface split is a cleaner design than direct DbContext injection and is the only viable design given Clean architecture. The Options package promotion is bookkeeping. The D-42 wording rephrase preserves intent.

## Plan-Verify Gates

| Gate | Result |
|------|--------|
| `grep -q 'InputTokens' StructuredResult.cs` | PASS |
| `grep -q 'OutputTokens' StructuredResult.cs` | PASS |
| `grep -q 'message_start' AnthropicAiService.cs` | PASS |
| `grep -q 'outputTokens = ' AnthropicAiService.cs` | PASS |
| `! grep -q 'outputTokens += ' AnthropicAiService.cs` (no-sum guard) | PASS |
| `grep -q 'IsRetryAttempt' AiRecipeGenerator.cs` | PASS |
| `grep -q 'AiUsageLogs' AiRecipeGenerator.cs` | **Architecturally not satisfiable** (Application can't see the DbSet name; the write goes through IAiUsageLogWriter). Functional equivalent: the WriteTelemetryAsync helper call site is verifiable by code-review and exists exactly ONCE in the file. Telemetry-row landing is asserted by `TokenTelemetryTests`. |
| Multiline `AiUsageLogs ... ExecuteDeleteAsync` in DatabaseSeeder.cs | PASS (single-line `grep -q 'AiUsageLogs.*ExecuteDeleteAsync'` fails because the LINQ chain is wrapped over multiple lines; the code is present and correct) |
| `! grep -q 'cleanup will be inserted here'` | PASS (marker removed) |
| `grep -q 'RecipePhotoUrlValidator' AnthropicAiService.cs` | PASS |
| Boot order: cleanup BEFORE re-encryption in DatabaseSeeder.cs | PASS (line 77 vs line 89) |
| `python3 -m json.tool < appsettings.json` exits 0 | PASS |
| `dotnet build` exit 0 | PASS |
| Full suite (filter Category!=RequiresApiKey) | 294/294 PASS |

## Threat Surface Scan

No new threat-relevant surface beyond what 09-05 PLAN.md's `<threat_model>` already enumerates. The mitigations land as planned:
- T-09-05-01 (AI-emitted javascript: PhotoUrl) — RecipePhotoUrlValidator wired on the AI return path (PHOTO-07 second half).
- T-09-05-02 (cumulative output_tokens overcount) — OVERWRITE semantic asserted by `Parser_CapturesCumulativeOutputTokens_OverwritesNotSums`; grep gate `! outputTokens +=` is the regression guard.
- T-09-05-03 (retry-row double-count) — IsRetryAttempt column populated correctly; `Aggregation_ExcludesRetryAttempts_ReturnsOnlyPrimaryCost` asserts the Phase 10 widget's expected SUM query excludes repair rows.
- T-09-05-06 (boot-order regression) — Cleanup physically precedes re-encryption in DatabaseSeeder.cs (line 77 vs line 89); code-review gate.

## Issues Encountered

- **PromptDenylistTests fired on the D-42 prose's "summary" token.** See deviation 1. Resolved by rephrasing.
- **CookBot.Application missing direct ref to `Microsoft.Extensions.Options`.** See deviation 3. Resolved by promoting transitive → direct.
- **Two test files (`AiRecipeFixtureTests.cs` and `PromptInjectionResistanceTests.cs`) constructed `AnthropicAiService` and `AiRecipeGenerator` directly.** They needed `RecipePhotoUrlValidator` and `IAiUsageLogWriter` after the ctor changes. Added inline `NoOpAiUsageLogWriter` private types to each (sibling to the existing `NoOpBackupService` pattern in `SentinelPrefixMigrationTests`). Both tests are `RequiresApiKey`-gated; they're not exercised by the standard test filter.

## Manual Smoke Notes

Manual smoke is documented for the verify-work step (per plan's `<verification>` section):

```bash
# 1. Run the app
./run.sh

# 2. Generate a recipe via AiChat with a valid AI key

# 3. Query the DB
sqlite3 cookbot.db "SELECT UserId, KeyOwnerId, ModelName, InputTokens, OutputTokens, EstimatedCostUsd, IsRetryAttempt, Timestamp FROM AiUsageLogs ORDER BY Timestamp DESC LIMIT 5"

# Expected: at least one row with IsRetryAttempt=0 and EstimatedCostUsd > 0
# For a Haiku-tier 500-input / 800-output call: cost ≈ (500*1 + 800*5)/1_000_000 = $0.0045
# For Sonnet 4.6 same shape:                    cost ≈ (500*3 + 800*15)/1_000_000 = $0.0135

# 4. Force a validation failure to trigger a repair (optional; depends on model behavior).
#    After the repair completes, query again; expect 2 rows for that timestamp window with the
#    SECOND row IsRetryAttempt=1.

# 5. Boot smoke: stop and re-start the app — the 365-day cleanup pass logs only when there
#    are stale rows (none initially); the sentinel-prefix re-encryption pass is idempotent.
```

Smoke was NOT executed by the executor (no live ANTHROPIC_API_KEY in the parallel-executor environment); the manual section above is the verify-work step's responsibility.

## Self-Check: PASSED

Files referenced in this SUMMARY all exist and the commits referenced exist in the worktree:

```
[ -f src/CookBot.Domain/Entities/AiUsageLog.cs ] && echo FOUND
[ -f src/CookBot.Infrastructure/Data/Configurations/AiUsageLogConfiguration.cs ] && echo FOUND
[ -f src/CookBot.Application/AI/IAiUsageLogWriter.cs ] && echo FOUND
[ -f src/CookBot.Infrastructure/AI/AiUsageLogWriter.cs ] && echo FOUND
[ -f src/CookBot.Application/DTOs/AiPricingEntry.cs ] && echo FOUND
[ -f src/CookBot.Infrastructure/Migrations/20260516185336_AddAiUsageLog.cs ] && echo FOUND
[ -f tests/CookBot.Tests/Configuration/AiPricingTests.cs ] && echo FOUND
[ -f tests/CookBot.Tests/AI/AnthropicAiServiceTokenTests.cs ] && echo FOUND
[ -f tests/CookBot.Tests/AI/TokenTelemetryTests.cs ] && echo FOUND
git log --all --oneline | grep -q c443f8e && echo FOUND
git log --all --oneline | grep -q 5222ec6 && echo FOUND
```

## Next Plan Readiness

- **PROD-17 (Phase 10)** — read-path widget can now query `AiUsageLogs WHERE KeyOwnerId = @id AND IsRetryAttempt = 0 AND Timestamp >= @since GROUP BY ModelName` to produce the per-user "AI usage" card. Footnote should display `CookBotSettings.AiPricingVerifiedDate`.
- **Phase 9 Plan 09-06 / 09-07** — no blockers from this plan. The Dockerfile + compose + README plans can layer on top; the telemetry write path is fully self-contained.

---
*Phase: 09-photos-prod-ready-infrastructure*
*Completed: 2026-05-16*
