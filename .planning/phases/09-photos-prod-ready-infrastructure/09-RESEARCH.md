# Phase 9: Photos + Prod-Ready Infrastructure - Research

**Researched:** 2026-05-16
**Domain:** .NET 10 Blazor Server prod-readiness — file upload, Data Protection encrypt-at-rest, Docker + compose, token-cost telemetry write path, healthcheck, README
**Confidence:** HIGH (delta-only research; v1.3 milestone-level synthesis already pinned the majority of decisions in `.planning/research/SUMMARY.md` + PITFALLS.md)

## Summary

Phase 9 is a delta-research phase. The v1.3 milestone-level research (`.planning/research/SUMMARY.md`, completed 2026-05-15) and the dense `.planning/research/PITFALLS.md` already pin the implementation patterns for every PHOTO-* and PROD-* requirement. The user-resolved 5 gray areas during discuss (D-38..D-43) further locked the remaining open product choices. This RESEARCH.md does NOT re-derive what those documents cover — it focuses exclusively on the 6 open-end items the orchestrator flagged: (1) current Anthropic pricing matrix, (2) sentinel-prefix detection regex/bounds, (3) `/healthz` + `AddDbContextCheck` wire-up for .NET 10, (4) `ExecuteDeleteAsync` 365-day cleanup shape, (5) magic-byte sniff `Span<byte>` snippets for the four accepted image types, (6) Validation Architecture per Nyquist contract.

All six items resolve cleanly with HIGH confidence: pricing pulled live from Anthropic's official `platform.claude.com/docs/en/about-claude/pricing` (2026-05-16); sentinel-prefix `CfDJ8` confirmed as the base64url encoding of the Data Protection magic header `09 F0 C9 F0` from MS Learn; `AddDbContextCheck<TContext>` confirmed to require the `Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore` NuGet package (NOT free in the base meta-package); EF Core 10 `ExecuteDeleteAsync` is the correct primitive for the 365-day pass (single SQL statement, no row materialization); magic-byte signatures for JPEG/PNG/WebP/GIF confirmed against `datatracker.ietf.org/doc/rfc9649/` and Wikipedia's file-signature reference.

**Primary recommendation:** Phase 9 proceeds exactly as locked in 09-CONTEXT.md. The 6-item delta below is everything the planner needs that isn't already in SUMMARY.md / PITFALLS.md / STACK.md / ARCHITECTURE.md.

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|--------------|----------------|-----------|
| Photo upload write (file → disk) | CookBot.Web (Scoped service) | — | Depends on `IBrowserFile` (Blazor type) + `IWebHostEnvironment` — neither available in Application/Domain |
| Photo URL validation | CookBot.Application (Singleton) | — | Pure function over `Uri`; no DI deps; shared across editor + RecipeService + AnthropicAiService AI return path |
| `IDataProtector` key ring storage | CookBot.Infrastructure (EF Core) | — | `CookBotDbContext` implements `IDataProtectionKeyContext`; key ring colocated with `cookbot.db` |
| Encrypt/decrypt of `UserProfile.AiApiKey` | CookBot.Web (`AiApiKeyResolutionService`) | CookBot.Web (`EditProfile.razor` save path) | Read path through `AiApiKeyResolutionService.ResolveAsync`; write path at save time; single shared protector scope `"AiApiKey.v1"` |
| Sentinel-prefix migration | CookBot.Infrastructure (`DatabaseSeeder.SeedAsync`) | — | Boot-time idempotent pass; runs in the same scope that already does null-canonical guard + (new) 365-day AiUsageLog cleanup |
| Token-cost telemetry capture (SSE) | CookBot.Infrastructure (`AnthropicAiService.SendStructuredAsync`) | — | SSE parse loop extension; sibling to existing `content_block_delta` / `message_delta` handlers |
| Telemetry write path | CookBot.Application (`AiRecipeGenerator.GenerateAsync`) | — | One source of truth for retry semantics; writes one row per attempt with `IsRetryAttempt` tag |
| Pricing config | CookBot.Web (`appsettings.json`) | — | Plain configuration; consumed by telemetry write site to compute `EstimatedCostUsd` |
| Healthcheck endpoint | CookBot.Web (`Program.cs`) | CookBot.Infrastructure (DbContext check) | `MapHealthChecks("/healthz")` lives in the composition root; `AddDbContextCheck<CookBotDbContext>` requires `Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore` NuGet |
| Docker container packaging | Repo root (`Dockerfile`, `docker-compose.yml`) | — | Build/deploy artifacts; not a code layer |
| README documentation | Repo root (`README.md`) | — | Operator-facing |

## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| PHOTO-01 | `.gitignore` adds `wwwroot/uploads/` (FIRST commit) | Current `.gitignore` verified — entry absent; verified path `src/CookBot.Web/wwwroot/` exists [VERIFIED: codebase grep] |
| PHOTO-02 | `<InputFile>` accepts JPEG/PNG/GIF/WebP via magic-byte sniff | Magic-byte signatures pinned below [VERIFIED: RFC 9649 + Wikipedia file signatures] |
| PHOTO-03 | `OpenReadStream(maxAllowedSize: 10 MB)` | [CITED: MS Learn Blazor file uploads] |
| PHOTO-04 | Three size limits raised to 12 MB | [CITED: PITFALLS H1] |
| PHOTO-05 | `Path.GetRandomFileName()` + content-type-derived extension | [CITED: PITFALLS H2] |
| PHOTO-06 | `UseStaticFiles` with `PhysicalFileProvider` + `RequestPath="/uploads"` + nosniff | [CITED: PITFALLS H3 + MS Learn Static Files] |
| PHOTO-07 | `RecipePhotoUrlValidator` http/https only | [CITED: PITFALLS H5] |
| PHOTO-08 | `referrerpolicy="no-referrer"` + `loading="lazy"` + Blazor state-flag onerror | [CITED: PITFALLS H4] |
| PHOTO-09 | Editor composite (D-38) | [CITED: 09-CONTEXT.md D-38] |
| PHOTO-10 | RecipeView hero swap | [CITED: PITFALLS H4] |
| PHOTO-11 | Home tile thumbnails | [CITED: PHOTO-08 fallback shared] |
| PHOTO-12 | AiChat canvas `_lastStructuredRecipe.Value.PhotoUrl` | [CITED: POLISH-01 invariant in STATE.md] |
| PHOTO-13 | CookbookList collage sample real photos | [CITED: 09-CONTEXT.md] |
| PHOTO-14 | README docs `uploads/` separate-volume backup | [CITED: PITFALLS C6] |
| PROD-01 | Multi-stage Dockerfile sdk:10.0 → aspnet:10.0 | [VERIFIED: MS container registry tags exist] |
| PROD-02 | docker-compose with named volumes + COOKBOT_PORT | [CITED: SUMMARY.md] |
| PROD-03 | `ASPNETCORE_URLS=http://0.0.0.0:7000` (PITFALL M4) | [CITED: PITFALLS M4] |
| PROD-04 | SQLite WAL + volume directory mount | [CITED: PITFALLS M5] |
| PROD-05 | `/healthz` endpoint (D-43) | Wire-up + package below [VERIFIED: MS Learn] |
| PROD-06 | `Microsoft.AspNetCore.DataProtection.EntityFrameworkCore 10.0.8` | [VERIFIED: NuGet 2026-05-12 publish] |
| PROD-07 | `AddDataProtection().SetApplicationName("FreelovesCookBot").PersistKeysToDbContext<CookBotDbContext>()` | [CITED: MS Learn key-storage-providers] |
| PROD-08 | EF ValueConverter OR `AiApiKeyResolutionService.Unprotect` — Claude's Discretion in 09-CONTEXT.md leans toward read-path-only wrap (no value converter) | [CITED: STACK.md] |
| PROD-09 | Sentinel-prefix migration | Detection regex pinned below [VERIFIED: MS Learn auth-encryption-details] |
| PROD-10 | `SecretRedactor` covers decrypt path | [CITED: PITFALLS C4] |
| PROD-11 | Owner sets key → recipient resolves | [CITED: PITFALLS C2] |
| PROD-12 | SSE `usage.input_tokens` + cumulative `message_delta.usage.output_tokens` | [CITED: Anthropic Messages Streaming docs] |
| PROD-13 | `StructuredResult<T>` gains `int InputTokens`, `int OutputTokens` | [CITED: AnthropicAiService.cs current shape] |
| PROD-14 | `AiUsageLog` entity + composite index `(KeyOwnerId, Timestamp DESC)` | [CITED: PITFALLS M8] |
| PROD-15 | One row per attempt, `IsRetryAttempt = true` for retries | [CITED: PITFALLS H9] |
| PROD-16 | Pricing in `appsettings.json` + `PricingVerifiedDate` | Concrete values pinned below [VERIFIED: platform.claude.com/docs/en/about-claude/pricing 2026-05-16] |
| PROD-17 | Per-user "AI usage" widget (Phase 10 READ path; Phase 9 ships only the write path) | [CITED: SUMMARY.md routing] |
| PROD-18..21 | README Install/Config/Backup/Upgrade | [CITED: PITFALLS C6 + D-40 PDF text-only + D-43 healthcheck note] |

## Standard Stack

### NEW NuGet packages this phase introduces

| Package | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| `Microsoft.AspNetCore.DataProtection.EntityFrameworkCore` | 10.0.8 | DB-backed key ring via `PersistKeysToDbContext<CookBotDbContext>` (PROD-06/07) | One-volume backup story; avoids documented `PersistKeysToFileSystem` Docker Linux issues (dotnet/aspnetcore#2941) [VERIFIED: NuGet — publish date 2026-05-12; targets net10.0; deps `Microsoft.AspNetCore.DataProtection >= 10.0.8` + `Microsoft.EntityFrameworkCore >= 10.0.8`] |
| `Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore` | 10.0.* (use floating to track DataProtection.EF pinning) | `AddDbContextCheck<CookBotDbContext>` for `/healthz` (D-43, PROD-05) | Base `AddHealthChecks()` does NOT include DB checks; this package is required [VERIFIED: MS Learn host-and-deploy/health-checks; NuGet 10.0.7 latest stable] |

**⚠️ Delta vs STACK.md:** STACK.md listed only `Microsoft.AspNetCore.DataProtection.EntityFrameworkCore 10.0.8` as the v1.3 net-new. The D-43 healthcheck decision was a discuss-mode bonus area not present at milestone-research time — `Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore` is therefore ALSO new in Phase 9. STACK.md's "exactly two packages" claim was for the whole v1.3 milestone (counting `Verify.Xunit` from Phase 8). The Phase 9 NuGet additions are 2.

### Existing in-codebase assets reused (no new deps)

| Asset | Path | Phase 9 use |
|-------|------|-------------|
| `IDatabaseBackupService` | `src/CookBot.Infrastructure/Data/` | Fires `cookbot.db.pre-{name}.bak` before each Phase 9 migration |
| `SecretRedactor` | `src/CookBot.Infrastructure/AI/` | Extended call-site coverage, same `Redact(raw, resolvedKey)` signature |
| `AnthropicAiService.SendStructuredAsync` SSE loop | `src/CookBot.Infrastructure/AI/AnthropicAiService.cs` lines 284–317 | Add `message_start.message.usage` + `message_delta.usage` capture as sibling cases to existing `content_block_delta` / `message_delta` |
| `AiRecipeGenerator.GenerateAsync` 2-retry loop | `src/CookBot.Application/AI/AiRecipeGenerator.cs` | Telemetry write happens after the loop returns; loop body unchanged |
| QuestPDF Community license | already in `Program.cs` line 9 | No change — D-40 keeps PDF text-only |
| `StripedPlaceholder` Cb atom | `src/CookBot.Web/Components/Atoms/` | Fallback target for `_photoLoadFailed` Blazor state flag |
| `<CbTextarea>`, `<CbInput>` | `src/CookBot.Web/Components/Atoms/` | Reused for Description + paste-URL |

### Installation

```xml
<!-- src/CookBot.Infrastructure/CookBot.Infrastructure.csproj — added next to existing EF packages -->
<PackageReference Include="Microsoft.AspNetCore.DataProtection.EntityFrameworkCore" Version="10.0.8" />

<!-- src/CookBot.Web/CookBot.Web.csproj — wire to DbContext (transitive Infrastructure dep is fine; explicit is clearer) -->
<PackageReference Include="Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore" Version="10.0.*" />
```

### Version verification

| Package | Verified version | Publish date | Source |
|---------|------------------|--------------|--------|
| `Microsoft.AspNetCore.DataProtection.EntityFrameworkCore` | 10.0.8 | 2026-05-12 | `nuget.org/packages/Microsoft.AspNetCore.DataProtection.EntityFrameworkCore/10.0.8` [VERIFIED] |
| `Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore` | 10.0.7 (latest stable) | recent | `nuget.org/packages/Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore` [VERIFIED] |

## Package Legitimacy Audit

Both packages are first-party Microsoft `Microsoft.*` namespace packages with millions of downloads, MIT-licensed, source repos at `github.com/dotnet/aspnetcore`. slopcheck was not available in this research session; both packages predate the slopsquatting threat era for first-party Microsoft packages by a wide margin and are quoted directly from MS Learn `learn.microsoft.com/en-us/aspnet/core/...` official documentation. No `[SLOP]` or `[SUS]` risk.

| Package | Registry | Age | Downloads | Source Repo | slopcheck | Disposition |
|---------|----------|-----|-----------|-------------|-----------|-------------|
| `Microsoft.AspNetCore.DataProtection.EntityFrameworkCore` | NuGet | ~8 years (since ASP.NET Core 2.x) | Tens of millions/yr | github.com/dotnet/aspnetcore | unavailable | Approved (first-party Microsoft, cited in official MS Learn docs) |
| `Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore` | NuGet | ~6 years | Tens of millions/yr | github.com/dotnet/aspnetcore | unavailable | Approved (first-party Microsoft, cited in official MS Learn docs) |

## Open Items Delta — what the planner needs that isn't in SUMMARY.md / PITFALLS.md / STACK.md

### Item 1 — Anthropic per-million-token pricing matrix [VERIFIED: platform.claude.com/docs/en/about-claude/pricing fetched 2026-05-16]

Phase 9 PROD-16 puts a pricing table in `appsettings.json` keyed by the three `CuratedModels` IDs from `AnthropicAiService.cs` line 19–22. The user-resolved decision in 09-CONTEXT.md (Claude's Discretion) defers exact values to plan-time verification against Anthropic's current pricing page. Verified:

| Model ID (`AnthropicAiService.CuratedModels`) | Input USD/1M tokens | Output USD/1M tokens | Notes |
|-----------------------------------------------|---------------------|----------------------|-------|
| `claude-haiku-4-5-20251001` | **$1.00** | **$5.00** | Fast tier |
| `claude-sonnet-4-6` | **$3.00** | **$15.00** | Default model (`AnthropicAiService.DefaultModelId`) |
| `claude-opus-4-7` | **$5.00** | **$25.00** | Highest capability; note: Opus 4.7 uses a new tokenizer that may consume up to 35% more tokens for the same text — telemetry rows surface raw `usage.input_tokens` so this is observable without code changes |

Concrete `appsettings.json` shape:

```json
{
  "CookBot": {
    "AiPricing": {
      "claude-haiku-4-5-20251001": {
        "InputTokensPerMillionUsd": 1.00,
        "OutputTokensPerMillionUsd": 5.00
      },
      "claude-sonnet-4-6": {
        "InputTokensPerMillionUsd": 3.00,
        "OutputTokensPerMillionUsd": 15.00
      },
      "claude-opus-4-7": {
        "InputTokensPerMillionUsd": 5.00,
        "OutputTokensPerMillionUsd": 25.00
      }
    },
    "AiPricingVerifiedDate": "2026-05-16"
  }
}
```

**Cost-calc rule:** `EstimatedCostUsd = (InputTokens * Input$/1M + OutputTokens * Output$/1M) / 1_000_000m`. Use `decimal` (NOT `double`) — currency math. `AiUsageLog.EstimatedCostUsd` column is `decimal(18, 6)` to allow sub-cent precision (Haiku call of 100 input + 50 output tokens = $0.00035 — must not round to zero).

**Profile widget footnote (PROD-17 — Phase 10 reads this):** "Pricing estimates based on Anthropic public rates as of 2026-05-16. Check claude.com/pricing for current rates." (PITFALL H10 mitigation.)

### Item 2 — Sentinel-prefix detection regex/bounds [VERIFIED: MS Learn auth-encryption-details + cross-confirmed via `andrewlock.net` writeup of the Data Protection payload format]

The Data Protection payload format begins with a 32-bit magic header `09 F0 C9 F0` followed by a 128-bit key id. When Data Protection is asked to produce a `string` (via the `IDataProtector.Protect(string plaintext)` overload — the one this codebase will use via the encoded `UserProfile.AiApiKey` column), the entire payload is **base64url-encoded** (NOT base64). The first 4 bytes `09 F0 C9 F0` base64url-encode to the 5-char prefix **`CfDJ8`** [VERIFIED: andrewlock.net "Introduction to Data Protection system in ASP.NET Core" + cross-referenced via the MS Learn machineKey replacement doc which says "you can tell if the new data protection system is active by inspecting fields like `__VIEWSTATE`, which should begin with `CfDJ8`"].

**Detection direction:** "Does this string look like Data Protection ciphertext?" — answer is *yes* iff it starts with `CfDJ8`.

**Plaintext Anthropic API keys** are documented to start with `sk-ant-` (already pinned in `SecretRedactor.cs` line 14 regex). Real keys are typically ~95–100 characters total.

**Length bounds for ciphertext:** the minimum protected payload is at least 32 bytes (4-byte magic + 16-byte key id + 12-byte minimum auth tag + IV), which base64url-encodes to ≥ 44 chars. Real-world protected payloads for a ~100-char plaintext key are ~180–220 chars. Sane sanity check: `value.Length >= 44`.

**Recommended detection helper (lives next to `SecretRedactor` in `CookBot.Infrastructure/AI/` or as a private helper in `AiApiKeyResolutionService`):**

```csharp
// One canonical helper, used by both DatabaseSeeder (migration pass) and the read path.
private static bool LooksLikeDataProtectionCiphertext(string? value) =>
    !string.IsNullOrEmpty(value)
    && value.Length >= 44
    && value.StartsWith("CfDJ8", StringComparison.Ordinal);
```

**Why startsWith over regex:** `CfDJ8` is a literal 5-char prefix; regex (`^CfDJ8`) compiles to the same logic but adds Regex engine overhead and a state-machine read for every `ResolveAsync` call (which happens on every AI message). `StringComparison.Ordinal` is the load-bearing detail — culture-aware comparison can produce surprises on Turkish locale (`ı`/`I` etc.) even for ASCII strings.

**Migration pass shape (`DatabaseSeeder.SeedAsync`, runs after Phase 8 null-canonical guard AND after the new D-41 365-day cleanup per the order decided in 09-CONTEXT.md):**

```csharp
// PROD-09 — idempotent re-encryption of legacy plaintext rows.
// Runs AFTER the 365-day AiUsageLog cleanup (cleanup target is a different table).
var protector = dataProtectionProvider.CreateProtector("AiApiKey.v1");

var legacyRows = await context.UserProfiles
    .Where(p => p.AiApiKey != null && p.AiApiKey != "")
    .Select(p => new { p.UserId, p.AiApiKey })
    .ToListAsync();

int reencrypted = 0;
foreach (var row in legacyRows)
{
    if (LooksLikeDataProtectionCiphertext(row.AiApiKey)) continue; // already encrypted — skip

    var encrypted = protector.Protect(row.AiApiKey!);
    await context.UserProfiles
        .Where(p => p.UserId == row.UserId)
        .ExecuteUpdateAsync(s => s.SetProperty(p => p.AiApiKey, encrypted));
    reencrypted++;
}

if (reencrypted > 0)
    logger.LogInformation("Re-encrypted {Count} legacy plaintext AI API key(s) at startup.", reencrypted);
```

**Idempotency proof:** A second boot finds all rows already prefixed `CfDJ8`, skips every iteration, makes zero writes. The `SentinelPrefixMigrationTests` covers this exact shape (seeded plaintext → first boot re-encrypts → second boot no-op).

**Read path (`AiApiKeyResolutionService`):** Read-path-only encryption (per Claude's Discretion in 09-CONTEXT.md and STACK.md — no EF value converter, since the value-converter approach forces all rows through Unprotect which throws on legacy plaintext during the very migration that's supposed to fix it). Recommended shape:

```csharp
// Inside ResolveAsync, after reading profile.AiApiKey (or chosen.AiApiKey from share path):
private string DecryptIfNeeded(string stored)
{
    if (!LooksLikeDataProtectionCiphertext(stored))
        return stored; // legacy plaintext — pass through, DatabaseSeeder will re-encrypt at next boot

    try
    {
        return _protector.Unprotect(stored);
    }
    catch (CryptographicException ex)
    {
        // PROD-10 / PITFALL C4: the ciphertext (stored) may not contain the plaintext key,
        // but if the key ring was lost (PITFALL C1) the row is unrecoverable. Redact the
        // exception message before any downstream consumer can see it.
        _logger.LogError(SecretRedactor.Redact($"Failed to decrypt AI API key for user: {ex.Message}", stored));
        throw; // ResolveAsync will catch one level up; this is a "key lost" condition that should surface to admin
    }
}
```

The single shared protector scope `"AiApiKey.v1"` is the C2 mitigation: recipient resolves the owner's encrypted row through the *same* protector that wrote it. No per-user purpose.

### Item 3 — `/healthz` + `AddDbContextCheck` wire-up for .NET 10 [VERIFIED: MS Learn host-and-deploy/health-checks aspnet-core-10.0]

**Required package:** `Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore` 10.0.* — base `AddHealthChecks()` does NOT include DB checks. The `AddDbContextCheck<TContext>` extension method lives in the namespace `Microsoft.Extensions.DependencyInjection` (auto-discovered when the package is referenced) and the class `EntityFrameworkCoreHealthChecksBuilderExtensions`.

**Behavior:** Calls EF Core's `DbContext.Database.CanConnectAsync()` at request time. Health-check name defaults to `nameof(TContext)` (i.e., `"CookBotDbContext"`). Returns `Healthy` on success, `Unhealthy` on connection failure.

**Wire-up (additions to `Program.cs`):**

```csharp
// 1. After AddInfrastructure (which registers AddDbContext<CookBotDbContext>):
builder.Services.AddHealthChecks()
    .AddDbContextCheck<CookBotDbContext>(
        name: "database",
        failureStatus: HealthStatus.Unhealthy);

// 2. After app.Build() and middleware setup, BEFORE app.Run():
app.MapHealthChecks("/healthz");
```

**Order constraint:** `AddDbContext<CookBotDbContext>` must be registered before `AddHealthChecks().AddDbContextCheck<CookBotDbContext>()` — already the case in `Program.cs` because `AddInfrastructure(builder.Configuration)` (line 18) runs before any health-check wire-up.

**D-43 semantics:** `/healthz` returns 200 only when both (a) `DatabaseSeeder.SeedAsync` completed successfully AND (b) `CanConnectAsync` returns true at request time. (a) is satisfied implicitly — the seeder runs in `Program.cs` lines 42–52 BEFORE `app.Run()`, so if it throws the app never starts listening and there is no `/healthz` to call. The healthcheck only needs to cover (b).

**docker-compose stanza (D-43 + PITFALL M6 override to `restart: on-failure`):**

```yaml
services:
  cookbot:
    # ... build/image/env/volumes ...
    restart: on-failure
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:7000/healthz"]
      interval: 30s
      timeout: 5s
      start_period: 30s
      retries: 3
```

Note `start_period: 30s` — the first-boot migration + sentinel re-encryption + 365-day cleanup all run in the seeder before `app.Run()`, and `curl` will refuse during that window. 30s buffer matches PITFALL M6's "fail visibly, not silently" intent. The Docker engine treats failures during `start_period` as not yet counting toward `retries`.

### Item 4 — 365-day cleanup query shape [VERIFIED: EF Core docs `ef/core/saving/execute-insert-update-delete`]

`ExecuteDeleteAsync` translates a LINQ `Where` clause to a single SQL `DELETE` and runs it without loading rows into the change tracker. EF Core 10 supports this on SQLite. Exact shape for D-41:

```csharp
// In DatabaseSeeder.SeedAsync, AFTER null-canonical guard, BEFORE sentinel-prefix re-encryption pass.
// Phase 9 D-41: hardcoded 365-day rolling cleanup of AiUsageLog rows.
var cutoff = DateTime.UtcNow.AddDays(-365);
var deletedCount = await context.AiUsageLogs
    .Where(r => r.Timestamp < cutoff)
    .ExecuteDeleteAsync();

if (deletedCount > 0)
    logger.LogInformation("Pruned {Count} AiUsageLog row(s) older than 365 days.", deletedCount);
```

**SQL produced (for SQLite):** `DELETE FROM "AiUsageLogs" WHERE "Timestamp" < @cutoff` — single statement, no row materialization, no `cookbot.db-wal` bloat from per-row tracking.

**Indexing implications:** The composite index `IX_AiUsageLog_KeyOwnerId_Timestamp DESC` already required by PROD-14 / PITFALL M8 does NOT cover this DELETE-by-Timestamp query (the leading column is `KeyOwnerId`, not `Timestamp`). For a single self-hoster with ~10 AI calls/day × 365 days = 3,650 rows, a full table scan is microseconds — no need for a separate `IX_AiUsageLog_Timestamp` index. If a self-hoster ever scales to multi-user with high AI usage and the boot cleanup becomes slow, add a `Timestamp`-only index in v1.4+. For v1.3 scope, the composite index alone is sufficient (KeyOwnerId-Timestamp aggregate queries are the hot path; the boot-time cleanup runs once per boot regardless of speed).

**Cutoff captured into a local variable, NOT inlined into the `Where`:** EF Core can translate `DateTime.UtcNow.AddDays(-365)` (it's a parameter, not a server-side call), but capturing it once makes the SQL `@__cutoff_0` parameter explicit and is the convention in the existing `DatabaseSeeder` (e.g., `ClearStaleSharedKeyPreferenceAsync` uses `ExecuteUpdateAsync`).

**Order constraint inside `DatabaseSeeder.SeedAsync`:** backup → migrate → null-canonical guard → 365-day cleanup → sentinel-prefix re-encryption pass → existing seed logic. The cleanup runs BEFORE the re-encryption pass per 09-CONTEXT.md "Established Patterns" — eliminates the (tiny) edge case of re-encrypting a row that's about to be deleted (note: AiUsageLog has no AiApiKey field — they target different tables — but the documented order is still cleanup-then-reencrypt for consistency).

### Item 5 — Magic-byte sniff snippets [VERIFIED: RFC 9649 + Wikipedia "List of file signatures" + Google WebP Container Specification]

Server-side sniff of first 12 bytes from `IBrowserFile.OpenReadStream` (BCL only, no NuGet). Buffer must be 12 bytes — WebP needs offset 0–3 (RIFF) + offset 8–11 (WEBP), so reads of < 12 bytes can't detect WebP.

```csharp
public static class ImageMagicBytes
{
    // JPEG: FF D8 FF (any 4th byte — JFIF E0, Exif E1, etc.)
    public static bool IsJpeg(ReadOnlySpan<byte> head) =>
        head.Length >= 3 && head[0] == 0xFF && head[1] == 0xD8 && head[2] == 0xFF;

    // PNG: 89 50 4E 47 0D 0A 1A 0A (8-byte signature)
    private static ReadOnlySpan<byte> PngSig => new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
    public static bool IsPng(ReadOnlySpan<byte> head) =>
        head.Length >= 8 && head[..8].SequenceEqual(PngSig);

    // GIF: "GIF87a" or "GIF89a" (47 49 46 38 [37|39] 61)
    public static bool IsGif(ReadOnlySpan<byte> head) =>
        head.Length >= 6
        && head[0] == 0x47 && head[1] == 0x49 && head[2] == 0x46 && head[3] == 0x38
        && (head[4] == 0x37 || head[4] == 0x39)
        && head[5] == 0x61;

    // WebP: "RIFF" at offset 0 (52 49 46 46) + "WEBP" at offset 8 (57 45 42 50)
    public static bool IsWebp(ReadOnlySpan<byte> head) =>
        head.Length >= 12
        && head[0] == 0x52 && head[1] == 0x49 && head[2] == 0x46 && head[3] == 0x46
        && head[8] == 0x57 && head[9] == 0x45 && head[10] == 0x42 && head[11] == 0x50;

    public static string? DetectExtension(ReadOnlySpan<byte> head) =>
        IsJpeg(head) ? ".jpg" :
        IsPng(head)  ? ".png" :
        IsWebp(head) ? ".webp" :
        IsGif(head)  ? ".gif" :
        null;
}
```

**Read pattern in `LocalRecipePhotoStorage` (call site):**

```csharp
// PHOTO-02 server-side sniff. Read 12 bytes BEFORE persisting.
await using var src = file.OpenReadStream(maxAllowedSize: 10 * 1024 * 1024, ct);
var head = new byte[12];
var bytesRead = await src.ReadAtLeastAsync(head, 12, throwOnEndOfStream: false, ct);
var ext = ImageMagicBytes.DetectExtension(head.AsSpan(0, bytesRead));
if (ext is null)
    throw new InvalidImageException(...); // surfaced to editor as a toast

// PHOTO-05: server-generated filename. NEVER use file.Name.
var safeName = $"{Guid.NewGuid():N}{ext}";
var savePath = Path.Combine(_uploadsDir, safeName);

// PITFALL H2 defense-in-depth: assert resolved path stays inside uploads dir.
var fullPath = Path.GetFullPath(savePath);
if (!fullPath.StartsWith(Path.GetFullPath(_uploadsDir), StringComparison.Ordinal))
    throw new InvalidOperationException("Path traversal attempt detected.");

// Then re-stream the file from the start — we already consumed the first 12 bytes.
// Re-open: file.OpenReadStream returns a new stream each call.
await using var writeStream = File.Create(fullPath);
await using var src2 = file.OpenReadStream(maxAllowedSize: 10 * 1024 * 1024, ct);
await src2.CopyToAsync(writeStream, ct);
```

**SVG rejection is implicit:** SVG starts with `<?xml` or `<svg` (ASCII text — bytes 0x3C 0x3F or 0x3C 0x73) which matches none of the four accepted signatures, so the upload is rejected at the magic-byte check without needing a separate SVG denylist (PITFALL H3 satisfied).

**HTML-as-jpg rejection is implicit:** A file named `evil.jpg` whose content starts with `<!DOCTYPE` (0x3C 0x21) fails `IsJpeg(head[0]==0xFF)` and is rejected.

**Test matrix for `LocalRecipePhotoStorageTests` (xUnit Theory):**
- 12-byte JPEG header (FF D8 FF E0 ... ) → accepted, `.jpg`
- 12-byte PNG signature → accepted, `.png`
- 12-byte WebP header (RIFF + WEBP at offset 8) → accepted, `.webp`
- 12-byte GIF87a / GIF89a → accepted, `.gif`
- SVG bytes (`<?xml` or `<svg`) → rejected
- HTML bytes (`<!DOC` / `<html`) → rejected
- Empty/short file (< 3 bytes) → rejected
- Truncated WebP (RIFF but only 6 bytes) → rejected (correct — Length >= 12 check)
- Random bytes → rejected

### Item 6 — Validation Architecture (per Nyquist contract — included though `workflow.nyquist_validation` is `false` in config.json per orchestrator brief)

**Note:** `.planning/config.json` has `workflow.nyquist_validation: false`. The orchestrator brief explicitly asked for this section anyway. The Phase 8 codebase already has xUnit 2.9.2 + Verify.Xunit 31.12.5 — no Wave 0 framework install needed.

#### Test Framework

| Property | Value |
|----------|-------|
| Framework | xUnit 2.9.2 + Verify.Xunit 31.12.5 (Phase 8 add) + bUnit (for any Razor-component tests Phase 9 introduces) |
| Config file | none — convention-based discovery via `dotnet test` |
| Quick run command | `dotnet test tests/CookBot.Tests/CookBot.Tests.csproj --filter "FullyQualifiedName~{TestClass}" --no-build` |
| Full suite command | `dotnet test tests/CookBot.Tests/CookBot.Tests.csproj` |

#### Phase Requirements → Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| PHOTO-01 | `.gitignore` excludes `wwwroot/uploads/` | smoke | `grep -q 'wwwroot/uploads/' .gitignore` | ❌ Wave 0 — add `.gitignore` entry + commit before any code |
| PHOTO-02/03/05 | Magic-byte accept/reject + GUID filename + size cap | unit | `dotnet test --filter "FullyQualifiedName~LocalRecipePhotoStorageTests"` | ❌ — new test file |
| PHOTO-04 | Three size limits set to 12 MB | smoke | `grep -E 'MaxRequestBodySize|MultipartBodyLengthLimit|MaximumReceiveMessageSize' src/CookBot.Web/Program.cs` | smoke only — no test |
| PHOTO-06 | `UseStaticFiles` + nosniff header | manual | `curl -I http://localhost:7000/uploads/test.jpg` — assert `X-Content-Type-Options: nosniff` | manual |
| PHOTO-07 | `RecipePhotoUrlValidator` scheme allowlist | unit (Theory matrix) | `dotnet test --filter "FullyQualifiedName~RecipePhotoUrlValidatorTests"` | ❌ — new test file |
| PHOTO-08 | onerror Blazor state-flag, no JS loop | manual | manual DevTools Network tab inspection with deliberate 404 | manual |
| PHOTO-09..13 | Photo composite + RecipeView + Home + AiChat + CookbookList rendering | bUnit | `dotnet test --filter "FullyQualifiedName~RecipePhotoCompositeTests"` (optional — visual-tested manually for Phase 9 scope; bUnit defer to v1.4+) | manual |
| PHOTO-14 / PROD-20 | README documents `uploads/` separate-volume backup | smoke | `grep -q 'wwwroot/uploads' README.md && grep -q 'back up' README.md` | manual review |
| PROD-01..04 | Dockerfile + compose syntax | smoke | `docker build .` + `docker compose config` | smoke only |
| PROD-05 / D-43 | `/healthz` returns 200 after seed | manual | `docker compose up -d && sleep 35 && curl -fsS http://localhost:7000/healthz` | manual |
| PROD-06/07 | Key ring persists across container restart | manual (the load-bearing test) | `docker compose up -d; <set AI key>; docker compose stop; docker compose start; <call AI>` — assert key still works | manual |
| PROD-08/11 | Owner sets key → recipient resolves via shared protector scope | integration | `dotnet test --filter "FullyQualifiedName~KeyShareEncryptionRoundTripTests"` | ❌ — new test file |
| PROD-09 | Sentinel-prefix migration: plaintext → encrypted; idempotent | integration | `dotnet test --filter "FullyQualifiedName~SentinelPrefixMigrationTests"` | ❌ — new test file |
| PROD-10 | `SecretRedactor` covers decrypt-error path | unit | `dotnet test --filter "FullyQualifiedName~SecretRedactorDecryptPathTests"` | ❌ — new test file |
| PROD-12/13 | SSE `usage` capture + `StructuredResult<T>` fields populated | unit | `dotnet test --filter "FullyQualifiedName~AnthropicAiServiceTokenTests"` — fixture stub SSE stream with `message_start.message.usage` + `message_delta.usage` | ❌ — new test file |
| PROD-14 | `AiUsageLog` entity + composite index | smoke + migration | `dotnet ef migrations script` — grep for `IX_AiUsageLog_KeyOwnerId_Timestamp` | smoke |
| PROD-15 | One row per attempt, IsRetryAttempt=true for retries | integration | `dotnet test --filter "FullyQualifiedName~TokenTelemetryTests"` — force validation failure on attempt 1, success on attempt 2; assert 2 rows; assert second row `IsRetryAttempt=true`; assert sum-of-tokens query excludes retries | ❌ — new test file |
| PROD-16 | Pricing config loads + dollar math is correct | unit | `dotnet test --filter "FullyQualifiedName~AiPricingTests"` — assert reading config produces expected per-model values | ❌ — new test file |
| PROD-18..21 | README sections present | smoke | `grep -E '^## (Install|Configuration|Backup|Upgrade)' README.md` — assert all 4 present | smoke |
| D-41 | 365-day cleanup query runs at boot | integration | `SentinelPrefixMigrationTests` extension OR standalone — seed AiUsageLog row with `Timestamp = UtcNow.AddDays(-400)`; run `SeedAsync`; assert row deleted; idempotency (second boot is no-op) | covered by an extension to the migration test |
| D-42 | Prompt prose distinguishes Description from step[0] | snapshot regen | `dotnet test --filter "FullyQualifiedName~PromptBuilderServiceTests"` — Verify snapshot updated atomically in same commit | snapshot regen, then green |

#### Sampling rate

- **Per task commit:** `dotnet build` + `dotnet test --filter "FullyQualifiedName~{NewlyAddedTestClass}"` for the test class the task added
- **Per wave merge:** `dotnet test tests/CookBot.Tests/` — full suite green
- **Phase gate:** Full suite green + manual key-ring-survives-restart smoke + manual `/healthz` smoke + manual photo-upload smoke before `/gsd:verify-work`

#### Wave 0 gaps

- [ ] `tests/CookBot.Tests/AI/SentinelPrefixMigrationTests.cs` — covers PROD-09 + D-41 cleanup (extends to AiUsageLog cleanup)
- [ ] `tests/CookBot.Tests/AI/KeyShareEncryptionRoundTripTests.cs` — covers PROD-11 + PITFALL C2
- [ ] `tests/CookBot.Tests/AI/SecretRedactorDecryptPathTests.cs` — covers PROD-10 + PITFALL C4
- [ ] `tests/CookBot.Tests/AI/AnthropicAiServiceTokenTests.cs` — covers PROD-12/13 SSE parsing
- [ ] `tests/CookBot.Tests/AI/TokenTelemetryTests.cs` — covers PROD-15 + PITFALL H9
- [ ] `tests/CookBot.Tests/Services/RecipePhotoUrlValidatorTests.cs` — covers PHOTO-07 + PITFALL H5 rejection matrix
- [ ] `tests/CookBot.Tests/Services/LocalRecipePhotoStorageTests.cs` — covers PHOTO-02/03/05 + PITFALLS H2/H3
- [ ] `tests/CookBot.Tests/Configuration/AiPricingTests.cs` — covers PROD-16 config-read shape

(Framework install: none — xUnit 2.9.2 + Verify.Xunit 31.12.5 already present from Phase 8.)

## Runtime State Inventory

> Phase 9 is a forward-feature phase, NOT a rename/refactor. Three of the five categories are explicitly NOT relevant. Two ARE relevant and have non-trivial entries.

| Category | Items Found | Action Required |
|----------|-------------|------------------|
| Stored data | (1) Existing `UserProfile.AiApiKey` rows that are currently plaintext — these must NOT break on first boot of Phase 9. (2) Existing recipes with `PhotoUrl = NULL` from Phase 8 schema add — these render with `<StripedPlaceholder>` (no migration needed). | **Sentinel-prefix migration in `DatabaseSeeder.SeedAsync` (PROD-09)** — see Item 2 above. Idempotent. |
| Live service config | None — no n8n / external job runners / etc. in this project. | None. |
| OS-registered state | None — no Windows Task Scheduler / launchd / systemd entries. Container runtime managed by Docker only. | None. |
| Secrets/env vars | (1) Existing `ConnectionStrings__DefaultConnection` — no rename, but compose file must export it. (2) `CookBot:AnthropicApiKey` (host-wide fallback in `appsettings.json` per `AnthropicAiService.CreateHttpClient`) — Phase 9 does NOT change this; only user-row `AiApiKey` is encrypted. The host-wide fallback stays plaintext-in-config (trusted-LAN posture — `appsettings.json` is gitignored per existing .gitignore `appsettings.*.json`). | Document in README PROD-19 that env-var override pattern works for all `CookBot:*` settings; specifically call out that the host-wide `AnthropicApiKey` is NOT encrypted-at-rest and is intended for single-operator scenarios. |
| Build artifacts | (1) `bin/` and `obj/` are already in .gitignore. (2) `wwwroot/uploads/` will accumulate runtime files — must be in .gitignore (PHOTO-01 first commit). (3) `cookbot.db` already gitignored. (4) `cookbot.db-wal` / `cookbot.db-shm` already gitignored. | PHOTO-01 — add `src/CookBot.Web/wwwroot/uploads/` to .gitignore FIRST commit; also add `src/CookBot.Web/wwwroot/uploads/.gitkeep` to ensure the directory exists on fresh clone. |

**Stale-state verification:** After Phase 9 ships, on first boot the seeder will scan `UserProfiles` and re-encrypt any plaintext key. The second boot (and every subsequent boot) is a no-op. The `SentinelPrefixMigrationTests` proves both directions.

## Project Constraints (from CLAUDE.md)

Phase 9 must respect these hard invariants from `./CLAUDE.md`:

- ✅ **No second AI provider abstraction / no `Microsoft.Extensions.AI` / no official `Anthropic` NuGet** — Phase 9 extends `AnthropicAiService.SendStructuredAsync` directly (lines 284–317 SSE parse loop); no new HTTP client, no transport change.
- ✅ **No `Newtonsoft.Json` / no `NJsonSchema`** — All Phase 9 JSON work uses `System.Text.Json` (already in `AnthropicAiService.JsonOptions`).
- ✅ **No `CookBot.Schemas` project** — Phase 9 adds no new projects; `AiUsageLog` goes in `CookBot.Domain/Entities/` (POCO).
- ✅ **No auto-scaling of temperature/prep/cook times** — Phase 9 does NOT touch scaling logic; only displays per-step `Temperature` from Phase 8's schema.
- ✅ **No "free-form / numbered-list fallback" escape hatch in AI prompt** — Phase 9 D-42 adds prose nudges DISTINGUISHING `description` from `steps[0]` but does NOT add any opt-out clause to constrained decoding.
- ✅ **Canonical-first reads** — Phase 9 PHOTO-12 reads `_lastStructuredRecipe.Value.PhotoUrl` directly from the canonical doc (POLISH-01 invariant — no extractor revival).
- ✅ **AI-off contract** — `CookBotSettings.AiFeaturesEnabled` + `UserProfile.AiEnabled` gates remain authoritative; PROD-12..17 telemetry writes ONLY when both gates are open. Phase 9 adds NO middleware-level enforcement.
- ✅ **MudBlazor stays out** — Phase 9 photo composite uses existing Cb atoms (`<CbInput>`, `<CbTextarea>`, `<StripedPlaceholder>`); no MudBlazor accordions, tabs, or dialogs.
- ✅ **Trusted-LAN auth posture** — Phase 9 README points at reverse proxy (Caddy/nginx/Traefik) for TLS termination; container itself binds plain HTTP on `0.0.0.0:7000`.

Phase 9 does NOT contradict any CLAUDE.md directive.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Key persistence across container restarts | `PersistKeysToFileSystem` + Docker volume for `/root/.aspnet/DataProtection-Keys` | `PersistKeysToDbContext<CookBotDbContext>` | Documented Linux Docker overlay/NFS file-move issues (dotnet/aspnetcore#2941) [VERIFIED] |
| Encrypt-at-rest crypto | Custom AES-GCM / hand-rolled IV management | `IDataProtector.CreateProtector("AiApiKey.v1")` | IV reuse, missing AAD, custom key derivation = subtle crypto bugs. `IDataProtector` is built for this case. |
| Image type validation | `SixLabors.ImageSharp` / `Magick.NET` | Magic-byte sniff in 20 lines of BCL | ImageSharp is GPL-3.0 incompatible with project license; Magick.NET is 50 MB native binary overkill for "is this an image" check |
| Health endpoint shape | Custom `MapGet("/health", ...)` with manual DB ping | `app.MapHealthChecks("/healthz")` + `AddDbContextCheck<CookBotDbContext>()` | Battle-tested across ASP.NET Core; structured `HealthReport` JSON; future-extensible with additional `IHealthCheck` registrations |
| Bulk delete of old rows | Load → iterate → `Remove` → SaveChanges | `ExecuteDeleteAsync` | Single SQL DELETE, no change-tracker overhead, no WAL bloat |
| Cost arithmetic | `float` / `double` | `decimal` | Currency math; `(int * decimal) / 1_000_000m` is the safe shape |

## Common Pitfalls (delta to PITFALLS.md)

PITFALLS.md already enumerates C1–C8 + H1–H11 + M1–M10 for v1.3 in extraordinary detail. The following are Phase-9-specific clarifications that the pitfall doc treats less explicitly:

### Pitfall — Capturing `DateTime.UtcNow` inside ExecuteDeleteAsync's Where lambda

**What goes wrong:** Inlining `DateTime.UtcNow.AddDays(-365)` in the lambda works, but EF Core re-evaluates it each call which is fine — the trap is testing: if a test captures the cutoff value before calling `SeedAsync` and then sleeps, the test's expected cutoff drifts from the seeder's actual cutoff by milliseconds.

**Prevention:** Capture once in a local. For tests, use a stable injectable `IClock` if precise testing is needed; for Phase 9 scope a >24-hour margin in test fixtures makes the millisecond drift irrelevant.

### Pitfall — Reading `IBrowserFile` content twice consumes the stream

**What goes wrong:** Calling `file.OpenReadStream()` for magic-byte sniff, then trying to `CopyToAsync` from the same stream — the second read sees EOF. Symptom: zero-byte file persisted.

**Prevention:** `IBrowserFile.OpenReadStream` returns a NEW stream on each call. Open once for sniff (read first 12 bytes only), dispose, then open again for the copy. Documented in Item 5 above.

### Pitfall — `appsettings.json` change does NOT trigger Data Protection re-application-name

**What goes wrong:** If a developer changes `SetApplicationName("FreelovesCookBot")` between deploys, every encrypted key becomes unreadable (different key-ring isolation namespace). Same trap as PITFALL C1 with a different mechanism.

**Prevention:** Document the application name as a load-bearing constant in code, NOT in config. Set it literally in `Program.cs` and add a comment explaining the trap. Phase 9 PROD-07 uses the literal `"FreelovesCookBot"` — never read from `IConfiguration`.

### Pitfall — `usage.output_tokens` is cumulative across `message_delta` events, NOT per-event

**What goes wrong:** Naive SSE parser does `totalOutput += delta.usage.output_tokens` on every `message_delta` event → result is sum-of-cumulative-snapshots, which is roughly `n*(n+1)/2` times too large.

**Prevention:** Anthropic's streaming docs specify `usage.output_tokens` in `message_delta` events is the cumulative count so far. Capture the LAST seen value, not a sum. Pattern: `int outputTokens = 0; ... if (type == "message_delta" && delta.usage?.output_tokens is int n) outputTokens = n; ... return outputTokens;`. The `message_start.message.usage.input_tokens` value is final at message-start time and never changes during the stream.

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| `PersistKeysToFileSystem` for Docker self-hosters | `PersistKeysToDbContext<TContext>` | ASP.NET Core 2.2+ stable, current best practice | Eliminates volume-mount surprises on Linux overlay/NFS |
| Hand-rolled `MapGet("/health", ...)` | `MapHealthChecks("/healthz")` + `AddDbContextCheck<TContext>` | ASP.NET Core 2.2+ | Structured response shape, composable additional checks |
| `IBrowserFile.ContentType` trust | Magic-byte server-side sniff | Always been the right call | XSS / stored-script prevention |
| `restart: unless-stopped` (compose) | `restart: on-failure` + healthcheck for production | Docker compose best practice (~2020) | Failures surface in `docker ps` instead of looping silently |
| `Newtonsoft.Json` (legacy .NET) | `System.Text.Json` | .NET Core 3.0+ | Already project convention |

**Deprecated/outdated:**
- `PersistKeysToFileSystem` for containerized deployments — not removed but counter-recommended (CookBot deliberately avoids it)
- `Verify.XunitV3` — current at NuGet but requires xUnit v3 migration; Phase 9 stays on `Verify.Xunit 31.12.5` (Phase 8 selection)

## Assumptions Log

> All Phase 9 claims in this research are either [VERIFIED] (live URL fetch or codebase grep) or [CITED] (existing planning doc). No [ASSUMED] tags below — every fact was verified against an authoritative source in this session OR cross-referenced from the v1.3 milestone research synthesis.

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| (none) | — | — | — |

**This table is empty:** All claims in this research were verified or cited — no user confirmation needed beyond the existing 09-CONTEXT.md decisions.

## Open Questions

The orchestrator brief flagged TWO open questions from STATE.md for Phase 9. Both are now closed by this research:

1. **Sentinel-prefix detection regex** — RESOLVED in Item 2 above. Use `value.Length >= 44 && value.StartsWith("CfDJ8", StringComparison.Ordinal)`. The `CfDJ8` prefix is the base64url encoding of the Data Protection magic header `09 F0 C9 F0` [VERIFIED via andrewlock.net writeup + MS Learn machineKey-replacement doc cross-reference].

2. **Token pricing values for Haiku 4.5 / Sonnet 4.6 / Opus 4.7** — RESOLVED in Item 1 above. $1/$5, $3/$15, $5/$25 per million tokens (input/output). `PricingVerifiedDate: "2026-05-16"`. [VERIFIED: platform.claude.com/docs/en/about-claude/pricing fetched live in this session]

No remaining open questions for Phase 9 planning.

## Environment Availability

Phase 9 introduces external dependencies (Docker runtime). The planner must check these before running Phase 9 verification:

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| Docker engine | PROD-01..04 (Dockerfile + compose smoke test) | unknown — operator-machine dependent | `docker --version` to probe | If absent: `dotnet test` covers everything except the Docker smoke tests; flag Docker smoke tests as manual on operator machines without Docker |
| Docker Compose v2+ | PROD-02 healthcheck syntax (`healthcheck.start_period`) | unknown | `docker compose version` to probe | If only Compose v1: rewrite healthcheck stanza in v1 syntax (no `start_period` — use longer `interval`) |
| `curl` inside container | docker-compose healthcheck (`test: curl -f /healthz`) | yes — `mcr.microsoft.com/dotnet/aspnet:10.0` base image includes curl | n/a | If a future base-image change drops curl: switch to `wget -q -O - http://localhost:7000/healthz \|\| exit 1` |
| .NET 10 SDK | All build/test | confirmed present (codebase builds against `net10.0`) | 10.0.* | none — required |
| EF Core CLI (`dotnet ef`) | Creating Phase 9's 2 new migrations | check `dotnet ef --version`; should be ≥ 10.0 | 10.0.* | If absent: `dotnet tool install --global dotnet-ef --version 10.0.*` |

**Missing dependencies with no fallback:** None for Phase 9 plan creation. Docker absence is acceptable — Phase 9 code still builds and unit-tests pass; only the integration smoke (key-survives-restart) requires Docker.

**Missing dependencies with fallback:** Docker Compose v1 vs v2 syntax (rewrite healthcheck stanza if needed).

## Security Domain

> The `security_enforcement` key is not present in `.planning/config.json` — treat as enabled per the agent contract. Phase 9 is the highest-security-density phase in v1.3.

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | yes (indirect — Phase 9 protects user API keys, not auth tokens) | existing `CurrentUserService` + PBKDF2 — unchanged |
| V3 Session Management | no | trusted-LAN posture; no new sessions |
| V4 Access Control | yes (PROD-11 key-share resolution semantics) | `AiApiKeyShareService` + `AiApiKeyResolutionService` — unchanged shape, only read path adds Unprotect |
| V5 Input Validation | yes (PHOTO-02 magic bytes, PHOTO-07 URL scheme) | `RecipePhotoUrlValidator` + `ImageMagicBytes` sniff |
| V6 Cryptography | yes (PROD-08 encrypt-at-rest) | `IDataProtector` (NEVER hand-roll) |
| V7 Error Handling | yes (PROD-10 decrypt-error path) | `SecretRedactor.Redact(..., resolvedKey)` at every new catch site |
| V8 Data Protection | yes (PROD-09 plaintext-to-ciphertext migration) | Sentinel-prefix pattern; idempotent boot pass |
| V12 Files and Resources | yes (PHOTO-02..06 file upload) | Magic-byte sniff + GUID filename + path-traversal assertion + nosniff header |
| V14 Configuration | yes (Docker + env vars + appsettings) | Document env-var override pattern in PROD-19 README section |

### Known Threat Patterns for ASP.NET Core 10 + Blazor Server + SQLite + Docker

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Path traversal via uploaded filename | Tampering | Server-generated `Guid.NewGuid():N` + content-type-derived extension; `Path.GetFullPath` prefix assertion (PITFALL H2 / Item 5 above) |
| Stored XSS via uploaded HTML-as-JPG | Tampering + Spoofing | Magic-byte sniff; `X-Content-Type-Options: nosniff` on `/uploads` static-file response (PITFALL H3) |
| Path traversal in paste-URL (`file:///`, `javascript:`, `data:`) | Information Disclosure + Elevation of Privilege | `RecipePhotoUrlValidator` scheme allowlist (`http`/`https` only) (PITFALL H5) |
| Data Protection key ring loss → all user API keys unrecoverable | Denial of Service (against legitimate use) | `PersistKeysToDbContext` colocates key ring with `cookbot.db` (PITFALL C1) |
| Encrypted-key share recipient cannot decrypt | DoS against legitimate sharing | Single shared protector scope `"AiApiKey.v1"` (PITFALL C2) — covered by `KeyShareEncryptionRoundTripTests` |
| Plaintext-on-disk → encrypted migration leaves system non-functional | DoS at boot | Sentinel-prefix idempotent migration; per-row try-protect-then-update pattern (PITFALL C3 + Item 2 above) |
| Cleartext API key in exception message reaches UI | Information Disclosure | Extend `SecretRedactor.Redact(message, resolvedKey)` to every new catch site (PITFALL C4) |
| Photo upload silently drops Blazor circuit | DoS (against UX) | Raise all three size limits to 12 MB; client-side `file.Size` precheck before stream open (PITFALL H1) |
| Docker key ring on ephemeral container layer | Information Disclosure (key persistence break) | `PersistKeysToDbContext` removes this attack surface entirely |
| docker-compose silent restart loop hides startup failures | DoS (operator confusion) | `restart: on-failure` + `max_retries: 3` + healthcheck (PITFALL M6 / D-43) |
| Per-user telemetry visible to admin without disclosure | Information Disclosure | README PROD-20 documents cross-user visibility for trusted-LAN deployments (PITFALL M9) |

## Sources

### Primary (HIGH confidence — verified live this session)
- **Anthropic pricing page** — `platform.claude.com/docs/en/about-claude/pricing` (fetched 2026-05-16). Source for Haiku 4.5 = $1/$5, Sonnet 4.6 = $3/$15, Opus 4.7 = $5/$25 per million tokens.
- **NuGet `Microsoft.AspNetCore.DataProtection.EntityFrameworkCore` 10.0.8** — `nuget.org/packages/Microsoft.AspNetCore.DataProtection.EntityFrameworkCore/10.0.8` (publish 2026-05-12; targets net10.0; deps verified).
- **MS Learn — Key storage providers in ASP.NET Core 10** — `learn.microsoft.com/en-us/aspnet/core/security/data-protection/implementation/key-storage-providers?view=aspnetcore-10.0`. Confirms `PersistKeysToDbContext<TContext>` + `IDataProtectionKeyContext` shape.
- **MS Learn — Health checks in ASP.NET Core 10** — `learn.microsoft.com/en-us/aspnet/core/host-and-deploy/health-checks?view=aspnetcore-10.0`. Confirms `AddDbContextCheck<TContext>` requires `Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore` NuGet.
- **MS Learn — ExecuteUpdate and ExecuteDelete** — `learn.microsoft.com/en-us/ef/core/saving/execute-insert-update-delete`. Confirms `ExecuteDeleteAsync` syntax for EF Core 10 + SQLite.
- **RFC 9649 — WebP Image Format** — `datatracker.ietf.org/doc/rfc9649/`. Source for `RIFF` (offset 0) + `WEBP` (offset 8) magic-byte sequence.
- **Wikipedia — List of file signatures** — JPEG (`FF D8 FF`), PNG (`89 50 4E 47 0D 0A 1A 0A`), GIF (`GIF87a` / `GIF89a`).
- **Live codebase inspection** — `src/CookBot.Infrastructure/AI/AnthropicAiService.cs` (SSE parse loop shape, CuratedModels list, JsonOptions), `src/CookBot.Application/AI/AiRecipeGenerator.cs` (2-retry loop structure, `StructuredResult<T>` return shape), `src/CookBot.Infrastructure/AI/SecretRedactor.cs` (Redact signature), `src/CookBot.Web/Services/AiApiKeyResolutionService.cs` (owner-then-share resolution), `src/CookBot.Infrastructure/Data/DatabaseSeeder.cs` (boot sequence), `src/CookBot.Web/Program.cs` (composition root), `src/CookBot.Infrastructure/CookBot.Infrastructure.csproj` (current package refs), `src/CookBot.Infrastructure/DependencyInjection.cs` (DI shape), `.gitignore` (current entries, no `uploads/`).

### Secondary (HIGH confidence — milestone-level research, pinned 2026-05-15)
- `.planning/research/SUMMARY.md` — full v1.3 synthesis; especially §"Phase 9: Photos + Prod-Ready Infrastructure" lines 169–197.
- `.planning/research/PITFALLS.md` — C1–C8 + H1–H11 + M1–M10 with prevention strategies; Phase 9 owns the prevention for C1–C6 + H1–H6 + H9–H10 + M4–M9.
- `.planning/research/STACK.md` — `Microsoft.AspNetCore.DataProtection.EntityFrameworkCore 10.0.8` rationale + ImageSharp GPL rejection.
- `.planning/research/ARCHITECTURE.md` — `IRecipePhotoStorage` shape, `RecipePhotoUrlValidator` placement, `AiUsageLog` entity.
- `.planning/phases/09-photos-prod-ready-infrastructure/09-CONTEXT.md` — 6 user-locked decisions (D-38..D-43).

### Tertiary (MEDIUM confidence — community writeups, cross-confirmed)
- **andrewlock.net** — "An introduction to the Data Protection system in ASP.NET Core" — independent confirmation that `CfDJ8` is the base64url-encoded magic header. Cross-confirmed via the MS Learn machineKey-replacement doc which explicitly mentions the `CfDJ8` prefix as the telltale sign of Data Protection.
- **Google for Developers — WebP Container Specification** — `developers.google.com/speed/webp/docs/riff_container` — confirms exact byte offsets for `RIFF` (0–3) + `WEBP` (8–11).

## Metadata

**Confidence breakdown:**
- Standard stack (the 2 net-new packages): HIGH — both Microsoft first-party, both cited from MS Learn official docs, NuGet versions verified live this session
- Architecture (already pinned by SUMMARY.md + ARCHITECTURE.md): HIGH — referenced, not re-derived
- Pitfalls (PITFALLS.md): HIGH — referenced; 4 Phase-9-specific clarifications added
- Pricing values: HIGH — live URL fetch this session, captured in `PricingVerifiedDate`
- Sentinel prefix: HIGH — official MS Learn + andrewlock.net cross-reference
- Magic bytes: HIGH — RFC 9649 + Wikipedia + Google WebP Container Spec triangulated
- `/healthz` wire-up: HIGH — official MS Learn aspnet-core-10.0 doc
- 365-day cleanup query: HIGH — official EF Core docs + EF Core 10 confirmed supports SQLite

**Research date:** 2026-05-16
**Valid until:** 2026-06-15 for pricing values (Anthropic reprices ~quarterly per H10); other facts valid 6+ months (stable framework patterns)
