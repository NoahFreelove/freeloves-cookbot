# Stack Research

**Domain:** Self-hosted Blazor Server cooking/baking tracker — v1.3 NEW capabilities only
**Researched:** 2026-05-15
**Confidence:** HIGH (claims verified against .NET 10 docs, NuGet package pages, GitHub issues, and the Anthropic streaming docs)

## Recommended Stack — v1.3 Delta

The v1.3 stack delta is small. Two new NuGet packages cover all five buckets; everything else is code-only changes against existing services.

### Core Technologies (NEW)

| Technology | Version | Purpose | Why Recommended |
|------------|---------|---------|-----------------|
| `Microsoft.AspNetCore.DataProtection.EntityFrameworkCore` | `10.0.8` | Persist the Data Protection key ring into the existing SQLite DB via `CookBotDbContext` (Bucket 5 — encrypt-at-rest) | `PersistKeysToFileSystem` has documented reliability issues with Docker volumes on Linux (file-move failures on certain NFS/overlay filesystems — [dotnet/aspnetcore#2941](https://github.com/dotnet/aspnetcore/issues/2941), [dotnet/dotnet-docker#4252](https://github.com/dotnet/dotnet-docker/issues/4252)). `PersistKeysToDbContext` colocates the key ring with `cookbot.db` — one persistent volume covers the encrypted AI keys + the data they protect. Cleaner backup story. |
| `Verify.Xunit` | `31.12.5` | Prompt-snapshot regression tests (Bucket 2 — FUTURE-V1.1-04) | Dependency spec `xunit.extensibility.execution >= 2.9.3` is compatible with the project's `xunit 2.9.2`. NuGet flags it as "legacy" because `Verify.XunitV3` is the successor — but `XunitV3` requires xUnit v3, which would force migrating `bUnit` + 196 tests. Out of scope for v1.3. MIT licensed, GPL-3.0 compatible. |

### Supporting Libraries

No additional supporting packages are required. Specifically:

- **Image validation:** Magic-byte sniffing with BCL `Span<byte>` (JPEG `FF D8 FF`, PNG `89 50 4E 47`, GIF `47 49 46`, WebP at offset 8 `57 45 42 50`). ~30 lines of pure .NET, no NuGet.
- **Scheme allowlist for paste-URLs:** `Uri.TryCreate` + `uri.Scheme` check against `["http", "https"]`. BCL only.
- **Token-cost telemetry:** Anthropic SSE already streams `usage.input_tokens` (in `message_start.message.usage`) and `usage.output_tokens` (cumulative on each `message_delta.usage`) per the [Messages Streaming docs](https://platform.claude.com/docs/en/api/messages-streaming). `AnthropicAiService` adds two fields to `StructuredResult<T>` and writes a row to a new `AiUsageLog` table — no NuGet.
- **Tags → relational migration (FUTURE-V1.1-02):** Pure EF Core entity + migration. No NuGet.
- **`LegacyRecipeProjector` deletion (FUTURE-V1.1-03):** Pure code removal. No NuGet.
- **Dockerfile + compose:** No NuGet. Multi-stage build using existing `mcr.microsoft.com/dotnet/sdk:10.0` + `aspnet:10.0` images.
- **README format section (FUTURE-V1.1-05):** Documentation only.

### Development Tools

| Tool | Purpose | Notes |
|------|---------|-------|
| `Verify.DiffPlex` (transitive via `Verify.Xunit`) | Inline diff display for snapshot mismatches in test output | Auto-included; no explicit dependency needed |
| `git` | `.verified.txt` snapshot files check in as plain-text diffs | Already in use; no setup change |

## Installation

```xml
<!-- tests/CookBot.Tests/CookBot.Tests.csproj — Bucket 2 -->
<PackageReference Include="Verify.Xunit" Version="31.12.5" />

<!-- src/CookBot.Infrastructure/CookBot.Infrastructure.csproj — Bucket 5 -->
<PackageReference Include="Microsoft.AspNetCore.DataProtection.EntityFrameworkCore" Version="10.0.8" />
```

That is the complete v1.3 NuGet additions list.

## Alternatives Considered

| Recommended | Alternative | When to Use Alternative |
|-------------|-------------|-------------------------|
| `PersistKeysToDbContext` (EF Core key ring) | `PersistKeysToFileSystem` | Non-containerized self-hosters who already mount a stable local directory and don't want a `DataProtectionKeys` table in the DB. Avoid for Docker due to Linux filesystem issues. |
| `PersistKeysToDbContext` (no at-rest encryption) | DPAPI (Windows) / X.509 cert / Azure KV | Cross-machine deploys where the SQLite file might be exfiltrated separately from the key ring. v1.3's trusted-LAN posture treats DB-file access as already-compromised — no value-add. |
| `Verify.Xunit` 31.12.5 | `Verify.XunitV3` 31.12.5+ | Only after the project migrates `xunit` → v3 (and re-verifies `bUnit` compatibility). Separate milestone concern. |
| Magic-byte sniffing | `SixLabors.ImageSharp` 3.1.12 | **REJECTED** for GPL-3.0 license incompatibility (see What NOT to Use). |
| Magic-byte sniffing | `Magick.NET` | Q16 native binaries are large (50+ MB); GPL-3.0 compat is murky (ImageMagick license has clauses that need legal review). For v1.3's "validate-not-process" need, BCL byte-comparison is sufficient. |
| Anthropic SSE token capture (no NuGet) | Adding a tokenizer library (`Tiktoken`-equivalent for Claude) | Would let us count tokens *before* sending. Anthropic doesn't publish their tokenizer; community libs are approximations. Server-returned `usage.*` is authoritative. |

## What NOT to Use

| Rejected | Reason | Use Instead |
|----------|--------|-------------|
| `SixLabors.ImageSharp` | Six Labors Split License is Apache 2.0 for qualifying open-source projects, but Apache 2.0 ↔ GPL-3.0 has a patent-termination clause conflict per FSF guidance. Using ImageSharp in a GPL-3.0 repo requires a commercial license. Hard blocker. | Magic-byte header check with BCL `Span<byte>` |
| `Newtonsoft.Json` | Project enforces 100% `System.Text.Json` since v1.0; adding Newtonsoft would introduce a second JSON runtime with subtly different default behaviors (date formats, null handling, enum casing) and risk canonical-doc round-trip drift | `System.Text.Json` (BCL, already in use) |
| `NJsonSchema` | Enforced anti-pattern since v1.1 Phase 1. `JsonSchema.Net` already handles runtime schema validation for the canonical `RecipeDocument` | `JsonSchema.Net` 9.2.* (already in the Application project) |
| `Microsoft.Extensions.AI` | Enforced anti-pattern. Adds an abstraction layer over `AnthropicAiService`'s deliberate direct-`HttpClient` design; would conflict with `output_config.format` structured-output transport and the existing `SecretRedactor` + `PromptInjectionGuard` interception points | `AnthropicAiService` (existing direct-HttpClient implementation) |
| Official Anthropic NuGet (`Anthropic` package) | Enforced anti-pattern. Wraps the HTTP client and would conflict with `output_config.format` and the existing redaction/injection guards. The CLAUDE.md note "the existing HttpClient in AnthropicAiService is sufficient and structured-output is a body-shape change, not a client change" is the binding constraint | Existing `AnthropicAiService` |
| `MudBlazor` | Stripped wholesale in v1.2 Phase 7 (repo-wide `Mud[A-Z]` grep returns zero hits). Re-adding would conflict with the custom Razor component system and the design tokens in `cookbot-design.css`. | Custom Cb atoms + `cookbot-design.css` design tokens |
| `Microsoft.AspNetCore.Identity.*` middleware | Trusted-LAN auth posture is preserved for v1.3. `CookBotSettings.AuthMode` is reserved-unused. Identity middleware would force a Blazor pipeline rewrite that's out of scope. | Existing `CurrentUserService` + PBKDF2 password hashing |
| `Azure.Extensions.AspNetCore.DataProtection.Blobs` / `.Keys` | Azure-only. CookBot is self-hosted on local disk/container, not Azure. | `Microsoft.AspNetCore.DataProtection.EntityFrameworkCore` (DB-backed key ring) |
| `Verify.XunitV3` | Requires `xunit.v3.extensibility.core >= 3.2.2` — incompatible with the project's `xunit 2.9.2`. Migrating xUnit is a separate milestone. | `Verify.Xunit` 31.12.5 |
| Custom AES-GCM encryption | Hand-rolled crypto risks: IV reuse, missing AAD, custom key derivation. `IDataProtector` exists for exactly this case. | `IDataProtectionProvider.CreateProtector("AiApiKey.v1")` |
| `Magick.NET` / `ImageMagick` | License complexity for GPL-3.0; large native binary footprint; overkill for "is this even an image" validation. | Magic-byte BCL check |

## Integration Points with Existing Services

| New Capability | Touches | How |
|---------------|---------|-----|
| Data Protection key ring | `CookBotDbContext` (`CookBot.Infrastructure`) | Implement `IDataProtectionKeyContext`; add `DbSet<DataProtectionKey>`; new EF migration adds `DataProtectionKeys` table |
| Data Protection key ring | `Program.cs` (`CookBot.Web`) | `builder.Services.AddDataProtection().SetApplicationName("FreelovesCookBot").PersistKeysToDbContext<CookBotDbContext>()` |
| AI key encrypt-at-rest | `AiApiKeyResolutionService` (`CookBot.Web`) | Inject `IDataProtectionProvider`; `Unprotect()` on read with `CreateProtector("AiApiKey.v1")` |
| AI key encrypt-at-rest | `UserProfile` save path | `Protect()` on write; column stays `string?`, encrypted blob replaces plaintext in the same column |
| AI key encrypt-at-rest | `DatabaseSeeder.SeedAsync` | One-time upgrade pass: detect plaintext rows by sentinel/version prefix, re-protect them |
| Token-cost telemetry | `AnthropicAiService.SendStructuredAsync` / streaming path | Read `message_start.message.usage.input_tokens` + cumulative `message_delta.usage.output_tokens` from existing SSE parsing loop |
| Token-cost telemetry | `StructuredResult<T>` record | Add `int InputTokens`, `int OutputTokens` fields |
| Token-cost telemetry | New `AiUsageLog` entity + migration | `(Id, UserId, KeyOwnerId, ModelName, InputTokens, OutputTokens, EstimatedCostUsd, Timestamp)`; composite index on `(KeyOwnerId, Timestamp)` for Profile-widget queries |
| Prompt snapshot tests | `tests/CookBot.Tests/CookBot.Tests.csproj` | Add `Verify.Xunit 31.12.5`; decorate test classes with `[UsesVerify]`; snapshot files live in `tests/CookBot.Tests/Snapshots/` via `Verifier.DerivePathInfo` |
| Schema v3 (photos + description + per-step temp) | `RecipeUpcasterChain` | New V2→V3 step; pure POCO, no NuGet |
| Schema v3 | `DatabaseSeeder.SeedAsync` | `IDatabaseBackupService` backup fires before the `AddRecipePhotoUrl` / `AddRecipeDescription` migrations per existing pattern; no new service |
| File upload | `wwwroot/uploads/` OR `ContentRootPath/uploads/` directory; `.gitignore` add | Blazor Server `<InputFile OnChange=...>` with `OpenReadStream(maxAllowedSize)`; default 500 KB limit, pass explicit `maxAllowedSize` (recommended 5 MB). Use `Path.GetRandomFileName()` not client filename. Validate via magic-byte sniffing, NEVER trust `IBrowserFile.ContentType`. |

## Trusted-LAN Posture Acknowledgment

v1.3's "self-hostable for others" goal does NOT flip the trusted-LAN posture. Out of scope for the stack:

- TLS/HTTPS cert hardening — defer to reverse-proxy guidance in the deploy doc (`nginx`/`Caddy` in front of `localhost:7000`)
- Identity middleware / OAuth / SSO
- Rate limiting / DoS protection
- `IDataProtector` at-rest encryption of the *key ring itself* (DPAPI / X.509 / Azure KV) — accepted: if attacker has the SQLite file, they have the key ring; document this in the deploy guide rather than pretend otherwise

## Confidence Assessment

| Area | Confidence | Basis |
|------|------------|-------|
| `Verify.Xunit` 31.12.5 ↔ xUnit 2.9.2 compatibility | HIGH | NuGet package dependency spec: `xunit.extensibility.execution >= 2.9.3` — direct match |
| ImageSharp GPL-3.0 incompatibility | HIGH | Six Labors Split License text read from GitHub; Apache 2.0 ↔ GPL-3.0 conflict is established FSF guidance |
| `PersistKeysToDbContext` for Docker | HIGH | Official MS docs for .NET 10; `PersistKeysToFileSystem` Docker volume issues confirmed in dotnet/aspnetcore + dotnet/dotnet-docker GitHub issues |
| Token telemetry — no new NuGet | HIGH | SSE event shapes verified from official Anthropic streaming docs |
| Magic-byte validation without ImageSharp | HIGH | Documented BCL approach in multiple ASP.NET Core security guidance pages |
| Blazor Server `<InputFile>` patterns | HIGH | Official .NET 10 file-uploads docs explicitly cover `OpenReadStream(maxAllowedSize)`, ContentType-untrusted warning, and `ContentRootPath`-over-`wwwroot` storage |

## Open Questions for `/gsd-discuss-phase` (Photos / Encrypt-at-rest phases)

These are scope/architecture questions for the relevant phase plan — NOT stack questions. The stack answer is the same either way.

1. **File upload storage layout.** PROJECT.md (final-decision source) confirms "file upload AND paste-URL." Open: `wwwroot/uploads/{recipe-guid}.{ext}` (browser-fetchable directly, simpler) vs `ContentRootPath/uploads/{recipe-guid}.{ext}` (out-of-wwwroot, needs a Blazor Server route handler). The Microsoft recommendation leans `ContentRootPath` (avoids accidentally serving non-image content). Decide during the photos-phase plan.
2. **Data Protection key upgrade strategy.** Existing plaintext `UserProfile.AiApiKey` rows need a one-time upgrade. Options: (a) sentinel-prefix the encrypted blob (e.g., `enc:v1:<base64>`), or (b) add a `UserProfile.AiApiKeyVersion` column. (a) is simpler. Decide during the encrypt-at-rest phase plan.
3. **`PersistKeysToDbContext` migration ordering.** The new `DataProtectionKeys` table migration must run *before* the AiApiKey re-encryption pass in `DatabaseSeeder.SeedAsync`. Migration order is forward-only; verify migration name sort order at phase-plan time.
4. **AI key sharing semantics under encrypt-at-rest.** `AiApiKeyShareService` currently lets recipient resolve a sharer's key without seeing it. Under `IDataProtector` with `SetApplicationName("FreelovesCookBot")`, the same protector scope covers all keys → recipient can `Unprotect` shared keys via `AiApiKeyResolutionService` (which does the unprotect server-side; recipient still never sees plaintext). Confirm trust model during the encrypt-at-rest phase plan.

## Sources

- [Verify.Xunit 31.12.5 on NuGet](https://www.nuget.org/packages/Verify.Xunit/31.12.5)
- [Verify.XunitV3 on NuGet](https://www.nuget.org/packages/Verify.XunitV3)
- [SixLabors ImageSharp LICENSE on GitHub](https://github.com/sixlabors/ImageSharp/blob/main/LICENSE)
- [SixLabors.ImageSharp on NuGet](https://www.nuget.org/packages/SixLabors.ImageSharp)
- [Microsoft.AspNetCore.DataProtection.EntityFrameworkCore on NuGet](https://www.nuget.org/packages/Microsoft.AspNetCore.DataProtection.EntityFrameworkCore)
- [ASP.NET Core Data Protection: Key storage providers (.NET 10)](https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/implementation/key-storage-providers?view=aspnetcore-10.0)
- [ASP.NET Core Data Protection: Key encryption at rest (.NET 10)](https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/implementation/key-encryption-at-rest?view=aspnetcore-10.0)
- [ASP.NET Core Data Protection: Configuration (.NET 10)](https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/configuration/overview?view=aspnetcore-10.0)
- [ASP.NET Core Blazor file uploads (.NET 10)](https://learn.microsoft.com/en-us/aspnet/core/blazor/file-uploads?view=aspnetcore-10.0)
- [Anthropic Messages Streaming docs](https://platform.claude.com/docs/en/api/messages-streaming)
- [PersistKeysToFileSystem Docker Linux issues — dotnet/aspnetcore#2941](https://github.com/dotnet/aspnetcore/issues/2941)
- [PersistKeysToFileSystem Docker Linux issues — dotnet/dotnet-docker#4252](https://github.com/dotnet/dotnet-docker/issues/4252)
