# Phase 9: Photos + Prod-Ready Infrastructure - Context

**Gathered:** 2026-05-16
**Status:** Ready for planning
**Mode:** discuss (4 selected gray areas + 1 bonus area, all resolved by user)

<domain>
## Phase Boundary

Ship the five vertically-integrated workstreams that turn v1.3's schema foundation (Phase 8) into a self-hostable product: (1) the photo pipeline — file upload AND paste-URL surfaced behind the Phase 8 `Recipe.PhotoUrl` column with magic-byte validation, GUID filenames, and `RecipePhotoUrlValidator` scheme allowlist; (2) Docker + compose with named volumes for `cookbot.db`, `wwwroot/uploads/`, and the Data Protection key ring; (3) encrypt-at-rest for `UserProfile.AiApiKey` via `IDataProtector` (EF value converter, sentinel-prefix idempotent re-encryption, single shared protector scope); (4) token-cost telemetry write path (`AiUsageLog` entity, SSE-parsed `usage` fields from `AnthropicAiService`, per-attempt rows with `IsRetryAttempt`, pricing from `appsettings.json`); (5) README install/config/backup/upgrade sections. Phase 9 also lands the **UI surfacing** of two Phase 8 schema additions that Phase 8 explicitly deferred: `Recipe.PhotoUrl` (editor composite + RecipeView hero + Home tiles + AiChat canvas + CookbookList collage) and `Recipe.Description` (editor field, RecipeView lede).

**This phase delivers the self-hoster story end-to-end.** A fresh `docker compose up` on a clean LAN machine results in a working app with photo upload, encrypted AI keys (idempotent migration), token-cost telemetry, and complete operator docs. The user-visible signals: "I can upload a photo and see it on RecipeView," "I can `docker stop && start` and my AI keys still decrypt," "I can see what I'm spending on Anthropic per month."

**In scope:** PHOTO-01..14, PROD-01..21 (35 requirements). Phase 8's deferred UI surfacing of Description + Temperature + PhotoUrl. Per-step Temperature display in cooking mode lands here alongside the photo work because they're sibling v3 additions in the editor.

**Not in scope** (do not pull forward):
- Smart pantry-match algorithm + dietary filter + Profile telemetry **read** widget → Phase 10 (QOL-01..03, QOL-06..07)
- AI Chat "Edit anyway" hardening with `RawRecipeEditorDialog` → Phase 10 (QOL-04)
- Accent variant picker + Profile AI prompt editor → Phase 10 (QOL-05, QOL-06)
- All five polish items (cookbook reparenting, pantry quick-add, moon glyph, TopBar slot, live timer tick) → Phase 10 (POLISH-01..05)
- Multiple photos / gallery → v1.4+ (single hero only per REQUIREMENTS Out of Scope)
- Image resizing / EXIF stripping / thumbnail generation → v1.4+ (no `ImageSharp` per GPL incompatibility; browser handles via CSS `object-fit`)
- TLS termination inside the container → README points at reverse proxy (nginx/Caddy)
- Cross-currency cost display, per-key-owner billing quotas → v1.4+
- AI key rotation UX → `IDataProtector` auto-rotates per .NET 10 defaults; no user-facing rotation in v1.3

</domain>

<decisions>
## Implementation Decisions

### Photo editor composite UX (Area 1)

- **D-38 (Area 1):** Photo composite sits at the **top of RecipeEditor**, above the recipe name field, as an inline card. Layout: live preview thumbnail on the **left** (4:3 cropped, ~240×180px, replaces existing `<StripedPlaceholder>` when `PhotoUrl` is non-null and validates); right column stacks **paste-URL CbInput** + **"Or upload file" button** (hidden `<InputFile>` triggered by the visible button) + **Clear button**. Both inputs are **always visible simultaneously** — no tab pattern, no accordion. Rationale: user explicitly chose "Both inputs visible, top of form" over a tabbed switcher and over a drag-zone variant; matches the v1.2 design language (warm-cream card, custom Cb atoms, no MudBlazor accordions) and reading order signals that the photo is part of the recipe's identity, not a footnote.
- **D-39 (Description placement):** `Recipe.Description` editor field sits **directly below the recipe name, above the ingredients section** — `<CbTextarea>` 2–3 rows, max 4096 chars (matches the EF column length set in Phase 8). Editor reading order: photo composite → name → description → ingredients → steps. Description is treated as the recipe's "lede" / subtitle, NOT inside the photo card (option 2) and NOT in a collapsible Details accordion (option 3). Rationale: user picked "Below name, above ingredients"; keeps Description visible by default rather than hidden behind a chevron, and visually pairs name + lede the way RecipeView already renders them post-v1.2.

### PDF photo handling (Area 2)

- **D-40 (Area 2):** `CookbookPdfService` **omits photos in v1.3**. PDF export stays text-only. No `HttpClient` dependency added to `CookbookPdfService`; no async pre-fetch in `CookbookDownloadHelper`; no QuestPDF `Image(...)` calls. Rationale: avoids PITFALL H6 entirely (synchronous QuestPDF builder cannot safely fetch URLs); ships immediately; ~30 lines of work saved. **README PROD-18..21 must explicitly note** "PDF export is text-only — photos remain in-app only." Photo-in-PDF can revisit in v1.4+ via pre-fetched-bytes pattern if user demand surfaces.

### AiUsageLog retention (Area 3)

- **D-41 (Area 3):** `AiUsageLog` rows are **cleaned up on a hardcoded 365-day rolling window** at startup. `DatabaseSeeder.SeedAsync` runs `db.AiUsageLogs.Where(r => r.Timestamp < DateTime.UtcNow.AddDays(-365)).ExecuteDeleteAsync()` after migrations and the null-canonical guard but before AI gate validation. No appsettings flag, no admin UI. Rationale: user picked the hardcoded option over admin-configurable; matches the 30-day rolling Profile widget plus a year of buffer for "what did I spend last summer?" lookups; eliminates a config surface the average self-hoster wouldn't tune. Trade-off acknowledged: long-term cost history beyond 365 days is lost — acceptable for a personal-cooking app.

### Description vs step[0] AI prompting (Area 4)

- **D-42 (Area 4):** Distinguish `Description` from `steps[0]` via **prompt prose only** — no `RecipeValidator` warning, no `[Description(...)]` attribute on `RecipeDocument` properties, no schema-level field-description propagation. `PromptBuilderService.BuildSystemPrompt` adds two clauses to the v3 schema section: (a) `description`: "1–2 sentence summary of what the dish is — no history, no cooking advice." (b) For `steps[]`: "Steps begin with the first cooking action — do not write an introductory paragraph as step 1." Rationale: user picked the simplest of three options; the constrained-decoding structured-output path already steers field shape, so this is a prose-level nudge, not a structural guard. Phase 8's CLEAN-03 byte-stable prompt snapshot test must be re-`verified` to absorb the prose change atomically with the rest of Phase 9's prompt-touching work.

### Healthcheck `/healthz` (Area 5 — bonus)

- **D-43 (Area 5):** `/healthz` returns **200 only after both** (a) `DatabaseSeeder.SeedAsync` completes successfully (migrations applied + null-canonical guard passes + 365-day cleanup runs) **and** (b) a `SELECT 1` against `CookBotDbContext` succeeds at request time. Returns 503 otherwise. Wired as ASP.NET Core's standard `app.MapHealthChecks("/healthz")` with a single `AddDbContextCheck<CookBotDbContext>()` registration. `docker-compose.yml` includes a `healthcheck:` stanza calling `curl -f http://localhost:7000/healthz` every 30s with `start_period: 30s` to absorb seeder time on first boot. **Overrides PROD-02's `restart: unless-stopped` → `restart: on-failure` with `max_retries: 3`** — rationale: PITFALL M6 explicitly warns that `unless-stopped` masks rapid-restart loops on startup failures; the user picked the safer variant; `on-failure` + `max_retries: 3` lets the container exit visibly after 3 failed seeder attempts, surfacing problems to `docker logs` rather than hiding them in a restart spiral. Both options are M6-approved; we pick the more surfaceable one.

### Claude's Discretion

These were not gray areas the user weighed in on; the planner can make the calls during planning.

- **`IRecipePhotoStorage` interface vs concrete service** — `LocalRecipePhotoStorage` is the only storage backend in v1.3 scope (cloud storage is explicitly OOS). Phase 8 D-29 set the precedent "duplication beats coupling"; the planner should default to a **concrete service in `CookBot.Web/Services/`** (no interface in Domain) unless test ergonomics require otherwise. `IBrowserFile` is a Blazor type unavailable in Application/Domain anyway, so the storage service is naturally Web-layer.
- **Plan/wave structure** — 35 requirements across 5 workstreams. Suggested split (planner's call): Wave 1 = `.gitignore` photo entry (PHOTO-01 FIRST) + photo storage service + URL validator + three size limits; Wave 2 = photo editor composite + Description field + Temperature picker in editor + RecipeView/Home/AiChat/CookbookList rendering; Wave 3 = `IDataProtector` + sentinel-prefix migration + SecretRedactor extension + share integration test; Wave 4 = Dockerfile + compose + `/healthz` + 365-day cleanup wired; Wave 5 = SSE token parsing + StructuredResult fields + AiUsageLog entity + pricing config + per-attempt write path; Wave 6 = README rewrite. Planner may merge or split.
- **Sentinel-prefix detection** — `CfDJ8...` (Data Protection's standard ciphertext prefix per ARCHITECTURE) is the encrypted sentinel; presence of `sk-ant-` OR absence of `CfDJ8` (length-bounded) is plaintext. Planner pins exact regex in PLAN.md.
- **Token pricing values** — verified at plan time against Anthropic's current pricing page; embedded in `appsettings.json` with a `PricingVerifiedDate` field. Per-model entries for the three CuratedModels (Haiku 4.5, Sonnet 4.6, Opus 4.7). User-facing Profile widget displays "Pricing as of {PricingVerifiedDate}" footnote (PITFALL H10).
- **Reverse-proxy README example** — generic "use your preferred reverse proxy (Caddy, nginx, Traefik) for TLS termination" with one short Caddyfile snippet for the most-friendly common case. Planner's call whether to include the snippet or keep it provider-agnostic.
- **`UseStaticFiles` headers for `/uploads`** — must include `X-Content-Type-Options: nosniff` per PITFALL H3 + reject SVG content-type entirely. Planner wires via `StaticFileOptions.OnPrepareResponse`.
- **EF migration sequence/timestamp ordering** — three Phase 9 migrations: `AddDataProtectionKeysTable`, `AddAiUsageLog` (with composite index `IX_AiUsageLog_KeyOwnerId_Timestamp DESC` per PROD-14), and any value-converter migration metadata. `IDatabaseBackupService` fires `cookbot.db.pre-{name}.bak` per existing pattern.
- **`_lastStructuredRecipe.Value.PhotoUrl` plumbing** — PHOTO-12 wires the AiChat canvas streaming card to surface PhotoUrl from the canonical doc. POLISH-01 invariant preserved (no extractor revival). Direct property read on the existing `_lastStructuredRecipe` accumulator.
- **First-run UX without AI key** — existing v1.2 AiChat gate sequence (`AiFeaturesEnabled` → `profile.AiEnabled` → `ResolveAsync != null`) with empty-state CTAs is sufficient per REQUIREMENTS PROD-18; no new banner or onboarding wizard. README PROD-18 documents "AI features gracefully degrade."

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents (researcher, planner, executor) MUST read these before planning or implementing.**

### Project & Roadmap
- `.planning/PROJECT.md` — project context, validated capabilities, active scope, key decisions, constraints, hard invariants (canonical-first reads, AI-off contract, POLISH-01 no extractor revival, no MudBlazor / Newtonsoft / MEAI / NJsonSchema / Identity middleware)
- `.planning/REQUIREMENTS.md` §"Photos surface (`PHOTO-*`)" (PHOTO-01..14) and §"Prod-ready for self-hosters (`PROD-*`)" (PROD-01..21) — 35 REQ-IDs Phase 9 owns; each row is spelled out in remarkable detail
- `.planning/ROADMAP.md` §"Phase 9: Photos + Prod-Ready Infrastructure" — phase goal, success criteria (5), dependency invariants (depends on Phase 8 PhotoUrl + RecipeTag groundwork)
- `.planning/STATE.md` §"Open questions" — sentinel-prefix detection (resolved here by Claude's Discretion + PROD-09 + ARCHITECTURE finding); token pricing (verified at plan time per Claude's Discretion); pantry-match weights (Phase 10 concern, not Phase 9)

### Research (load-bearing)
- `.planning/research/SUMMARY.md` — synthesis routing layer; especially §"Phase 9: Photos + Prod-Ready Infrastructure" (lines 169–197), §"Stack" (the two new NuGet packages: `Microsoft.AspNetCore.DataProtection.EntityFrameworkCore 10.0.8`, `Verify.Xunit 31.12.5` — Verify already added in Phase 8), §"Critical Pitfalls" #1–4 (the four highest-risk Phase 9 patterns)
- `.planning/research/PITFALLS.md` — **MUST READ END-TO-END before planning Phase 9**. Density is extreme: C1 (key ring volume), C2 (shared protector scope), C3 (sentinel-prefix migration), C4 (SecretRedactor coverage), C5 (`.gitignore` first), C6 (uploads backup docs), H1 (three size limits), H2 (path traversal), H3 (nosniff + magic bytes), H4 (onerror one-shot debounce), H5 (paste-URL scheme allowlist), H6 (QuestPDF sync — resolved by D-40 omit), H9 (token double-count), H10 (pricing in config), M4 (0.0.0.0 bind), M5 (WAL directory mount), M6 (restart policy — resolved by D-43), M7 (TZ=UTC), M8 (telemetry composite index), M9 (cross-user telemetry disclosure), M10 (Description vs step[0] — resolved by D-42)
- `.planning/research/STACK.md` — `Microsoft.AspNetCore.DataProtection.EntityFrameworkCore 10.0.8` package details + DB-backed key ring rationale + ImageSharp GPL-3.0 rejection
- `.planning/research/ARCHITECTURE.md` §"Phase 9" — `IRecipePhotoStorage` shape (Claude's Discretion D-38 supplement: concrete service per Phase 8 D-29 precedent); `RecipePhotoUrlValidator` shared between editor / `RecipeService` / `AnthropicAiService` AI return path; `AiUsageLog` entity shape
- `.planning/research/FEATURES.md` §"Must have (P1)" — file upload, paste-URL, encrypt-at-rest, Dockerfile, README all P1

### Codebase
- `.planning/codebase/ARCHITECTURE.md` §"AI Chat" + §"Recipe authoring (manual editor)" — current `RecipeEditor.razor` structure and `AiApiKeyResolutionService` flow that Phase 9 wraps in encryption
- `.planning/codebase/CONCERNS.md` — file format inconsistencies (mostly closed by Phase 1+8); Phase 9 introduces no new format concerns
- `.planning/codebase/STACK.md` — confirms System.Text.Json everywhere, EF Core 10 + SQLite, .NET 10, QuestPDF community license

### Phase 8 Reference (load-bearing — Phase 9 builds on Phase 8 schema)
- `.planning/phases/08-format-foundation/08-CONTEXT.md` — D-28 (`PhotoUrl: string?` + `Description: string?` shapes; EF column lengths 2048 / 4096), D-29 (single-class upcaster pattern, "duplication beats coupling"), D-32 (null-canonical guard is permanent — Phase 9 piggybacks 365-day cleanup AFTER this guard), D-37 (README inline; Phase 9 adds Install/Config/Backup/Upgrade sections BELOW the existing "Recipe Format" section)
- `.planning/phases/08-format-foundation/08-PHASE-SUMMARY.md` (if shipped) — final shape of v3 schema as it lands in Phase 9

### Phase 1 Reference (selectively load-bearing)
- `.planning/phases/01-canonical-format-foundation/01-CONTEXT.md` — D-15 (`IDatabaseBackupService` pattern Phase 9's three new migrations follow), D-22 (prompt denylist — extended in Phase 8; Phase 9 doesn't extend further but the test must stay green after D-42's prompt prose change)

### Source files this phase modifies (start here)
- `src/CookBot.Web/Program.cs` — register `AddDataProtection().SetApplicationName("FreelovesCookBot").PersistKeysToDbContext<CookBotDbContext>()` (PROD-06/07); register `LocalRecipePhotoStorage` + `RecipePhotoUrlValidator` (PHOTO-02/07); raise three size limits to 12 MB (PHOTO-04); `app.UseStaticFiles(new StaticFileOptions { FileProvider = new PhysicalFileProvider(...), RequestPath = "/uploads", OnPrepareResponse = ctx => ctx.Context.Response.Headers["X-Content-Type-Options"] = "nosniff" })` (PHOTO-06 + PITFALL H3); `app.MapHealthChecks("/healthz")` (PROD-05 / D-43)
- `src/CookBot.Infrastructure/Data/CookBotDbContext.cs` — implement `IDataProtectionKeyContext` with `DbSet<DataProtectionKey>` (PROD-06); add `DbSet<AiUsageLog>` (PROD-14)
- `src/CookBot.Infrastructure/Data/DatabaseSeeder.cs` — sentinel-prefix detection + one-time re-encryption pass (PROD-09); 365-day `AiUsageLog` cleanup pass (D-41) — runs AFTER Phase 8's null-canonical guard
- `src/CookBot.Web/Services/AiApiKeyResolutionService.cs` — switch read path through `IDataProtector.Unprotect` via shared `"AiApiKey.v1"` protector scope (PROD-08/11); preserve owner-vs-recipient resolution semantics under encryption
- `src/CookBot.Web/Components/Pages/RecipeEditor.razor` — photo composite block above name (D-38); Description CbTextarea below name above ingredients (D-39); Temperature picker per ContentStep (PHOTO-09 sibling — Phase 8 deferred)
- `src/CookBot.Web/Components/Pages/RecipeView.razor` — `<StripedPlaceholder>` → `<img>` with onerror state-flag fallback (PHOTO-10 + PITFALL H4); Description rendered as lede under recipe title
- `src/CookBot.Web/Components/Pages/Home.razor` + `Home.razor.cs` — recently-cooked tile + tonight-from-your-pantry hero card photo thumbnails (PHOTO-11)
- `src/CookBot.Web/Components/Pages/AiChat.razor` — streaming card surfaces `_lastStructuredRecipe.Value.PhotoUrl` (PHOTO-12; POLISH-01 invariant preserved)
- `src/CookBot.Web/Components/Pages/CookbookList.razor` — collage thumbnails sample from real `PhotoUrl`s, fallback to accent-tinted striped tiles (PHOTO-13)
- `src/CookBot.Infrastructure/AI/AnthropicAiService.cs` — SSE parse loop captures `message_start.message.usage.input_tokens` + cumulative `message_delta.usage.output_tokens` (PROD-12); apply `RecipePhotoUrlValidator` to AI-emitted `PhotoUrl` in structured-output return path (PHOTO-07)
- `src/CookBot.Application/AI/AiRecipeGenerator.cs` — return `StructuredResult<T>` with `InputTokens` / `OutputTokens` fields (PROD-13); 2-retry loop tags retries with `IsRetryAttempt = true` (PROD-15 + PITFALL H9)
- `src/CookBot.Infrastructure/AI/SecretRedactor.cs` — extend to cover `CryptographicException` decrypt path (PROD-10 + PITFALL C4)
- `src/CookBot.Application/Services/PromptBuilderService.cs` — add prose clauses distinguishing `description` from `steps[0]` (D-42); Phase 8 Verify-based prompt snapshot test will need `.verified` regeneration in the same commit
- `src/CookBot.Web/Components/Pages/EditProfile.razor` — write path through `IDataProtector.Protect` for new/edited AI keys (PROD-08); save-time round-trip validation
- `src/CookBot.Web/appsettings.json` — add `CookBot:AiPricing` table (per-model `InputTokensPerMillionUsd` + `OutputTokensPerMillionUsd`) + `CookBot:AiPricingVerifiedDate` (PROD-16 + PITFALL H10)
- `README.md` — add Install + Configuration + Backup & Restore + Upgrade sections below the existing "Recipe Format" section (PROD-18..21); explicit note that PDF export is text-only (D-40); explicit note about reverse-proxy for TLS (Claude's Discretion); explicit note that uploads/ is a separate volume that must be backed up alongside `cookbot.db` (PHOTO-14 + PITFALL C6); explicit note about cross-user telemetry visibility (PITFALL M9)
- `.gitignore` — add `src/CookBot.Web/wwwroot/uploads/` (PHOTO-01 — FIRST commit of phase, before any upload code)

### Source files this phase creates
- `src/CookBot.Domain/Entities/AiUsageLog.cs` — `(Id, UserId, KeyOwnerId, ModelName, InputTokens, OutputTokens, EstimatedCostUsd, IsRetryAttempt, Timestamp)` POCO (PROD-14)
- `src/CookBot.Infrastructure/Data/Configurations/AiUsageLogConfiguration.cs` — composite index `IX_AiUsageLog_KeyOwnerId_Timestamp` (DESC) (PROD-14 + PITFALL M8)
- `src/CookBot.Application/Services/RecipePhotoUrlValidator.cs` — scheme allowlist (`http`/`https` only); shared between editor + `RecipeService` + `AnthropicAiService` return path (PHOTO-07 + PITFALL H5)
- `src/CookBot.Web/Services/LocalRecipePhotoStorage.cs` — `IBrowserFile` → magic-byte sniff → GUID filename → `wwwroot/uploads/{guid}.{ext}` (PHOTO-02/03/05 + PITFALLS H2/H3); concrete service, no interface (Claude's Discretion per Phase 8 D-29 precedent)
- `src/CookBot.Web/Components/RecipePhotoComposite.razor` — paste-URL + upload + preview + clear composite at top of RecipeEditor (D-38 + PHOTO-09)
- `src/CookBot.Web/wwwroot/uploads/.gitkeep` — empty placeholder so the directory exists on fresh clone
- `src/CookBot.Infrastructure/Migrations/<timestamp>_AddDataProtectionKeysTable.cs` — EF migration for `DataProtectionKey` table (PROD-06)
- `src/CookBot.Infrastructure/Migrations/<timestamp>_AddAiUsageLog.cs` — EF migration with composite index (PROD-14)
- `Dockerfile` (repo root) — multi-stage `mcr.microsoft.com/dotnet/sdk:10.0` → `mcr.microsoft.com/dotnet/aspnet:10.0`; `ASPNETCORE_URLS=http://+:7000`; `ENV TZ=UTC`; `ENTRYPOINT ["dotnet", "CookBot.Web.dll"]` (PROD-01/03 + PITFALLS M4/M7)
- `docker-compose.yml` (repo root) — named volumes `cookbot_db` (mounts `/data` for `cookbot.db` + WAL sidecars per PITFALL M5) and `cookbot_uploads` (mounts `/app/wwwroot/uploads`); key ring colocated in `cookbot.db` via `PersistKeysToDbContext` (PROD-07 — eliminates C1 entirely without needing a third named volume); `restart: on-failure` with `max_retries: 3` (overrides PROD-02's `unless-stopped` per D-43 + PITFALL M6); `healthcheck:` calling `/healthz` with `start_period: 30s` (D-43); env vars `ASPNETCORE_URLS`, `ConnectionStrings__DefaultConnection`, `COOKBOT_PORT` (PROD-02)
- `tests/CookBot.Tests/AI/SentinelPrefixMigrationTests.cs` — seeded plaintext row → first-boot re-encryption → second-boot no-op idempotency (PROD-09 + PITFALL C3)
- `tests/CookBot.Tests/AI/KeyShareEncryptionRoundTripTests.cs` — owner sets key → share row created → recipient resolves and decrypts via shared protector scope (PROD-11 + PITFALL C2)
- `tests/CookBot.Tests/AI/SecretRedactorDecryptPathTests.cs` — `CryptographicException` messages do not leak ciphertext or plaintext (PROD-10 + PITFALL C4)
- `tests/CookBot.Tests/Services/RecipePhotoUrlValidatorTests.cs` — full rejection matrix per PITFALL H5 (`javascript:`, `data:`, `file:`, `ftp:`, `vbscript:`, protocol-relative `//host`, accepted: `http`/`https`)
- `tests/CookBot.Tests/Services/LocalRecipePhotoStorageTests.cs` — magic-byte sniff (JPEG/PNG/WebP/GIF accepted; SVG + HTML-as-jpg rejected); GUID filename generation; path-traversal rejection via prefix check (PITFALLS H2/H3)
- `tests/CookBot.Tests/AI/TokenTelemetryTests.cs` — single `GenerateAsync` writes one `AiUsageLog` row on success; retry attempt writes second row with `IsRetryAttempt=true`; aggregation excludes retries (PROD-15 + PITFALL H9)

### Source files this phase deletes
- None this phase. Phase 8 already deleted the projector files.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets

- **`AiApiKeyResolutionService`** (`src/CookBot.Web/Services/`) — owner-then-share resolution flow ships unchanged; only the field read is wrapped in `Unprotect`. The owner-id is already known at decrypt time (no per-user scope needed — D-08 / PROD-08 shared scope works).
- **`SecretRedactor`** (`src/CookBot.Infrastructure/AI/`) — `Redact(raw, resolvedKey)` signature is already correct for the encrypt-at-rest path; Phase 9 extends call sites, not the helper shape. Existing `AnthropicAiService.SendStructuredAsync` lines 219, 263, 274 are the canonical model to mirror.
- **`AnthropicAiService.SendStructuredAsync`** SSE parse loop already iterates `content_block_delta` events; PROD-12 adds a sibling capture for `message_start.message.usage` and cumulative `message_delta.usage`. No new HTTP client, no new transport.
- **`AiRecipeGenerator`** 2-retry repair loop is already in place from Phase 2; Phase 9 adds telemetry write at the END of `GenerateAsync` (one row per attempt with `IsRetryAttempt`), NOT inside the loop body (PITFALL H9 prevention by structure).
- **`IDatabaseBackupService`** fires `cookbot.db.pre-{name}.bak` before every migration in `DatabaseSeeder.SeedAsync` — Phase 9's three new migrations (`AddDataProtectionKeysTable`, `AddAiUsageLog`, and any value-converter metadata) each get a backup automatically.
- **`StripedPlaceholder` Cb atom** (`src/CookBot.Web/Components/Atoms/`) — already wired in CookbookList collage, Home cards, RecipeView hero; Phase 9 keeps it as the fallback target for the Blazor state-flag `_photoLoadFailed` pattern.
- **`<CbTextarea>`** Cb atom — used by Profile page since v1.2; Phase 9 reuses unchanged for Description editor field.
- **`<CbInput>`** Cb atom — used everywhere; Phase 9 reuses for paste-URL field with `type="url"` semantics.
- **DataProtection's `CfDJ8` ciphertext prefix** — standard ASP.NET Core sentinel; ARCHITECTURE.md confirms; planner pins the exact regex in PLAN.md.
- **`QuestPDF.Settings.License = LicenseType.Community`** already registered in `Program.cs` — no PDF-pipeline changes needed since D-40 omits photos.

### Established Patterns

- **DI registration via per-project extension** — `AddInfrastructure(IConfiguration)` registers `IDataProtector` infra (PROD-07) + `AiUsageLog` configuration; `Program.cs` registers Web-layer services (`LocalRecipePhotoStorage`, `RecipePhotoUrlValidator` if Web-resident).
- **Scoped lifetimes for per-circuit Web services** — `CurrentUserService`, `AiApiKeyResolutionService` are Scoped; Phase 9's `LocalRecipePhotoStorage` is also Scoped (depends on `IWebHostEnvironment`).
- **Singleton lifetimes for pure validators** — `RecipePhotoUrlValidator` is a Singleton (no state, no DI deps).
- **xUnit Theory + MemberData for fixture-driven tests** — used for round-trip and matrix tests; Phase 9's `RecipePhotoUrlValidatorTests` and `LocalRecipePhotoStorageTests` follow this.
- **`#nullable enable` + implicit usings** — every new file.
- **Forward-only EF migrations** — Phase 9's three migrations are forward-only; downgrade unsupported.
- **Sequence-sensitive `DatabaseSeeder` boot order**: backup → migrate → seed → null-canonical guard (Phase 8 D-32) → **365-day AiUsageLog cleanup (D-41)** → **sentinel-prefix re-encryption pass (PROD-09)**. The cleanup runs before the re-encryption to avoid re-encrypting rows that are about to be deleted.

### Integration Points

- **`Program.cs` composition root** — picks up `AddDataProtection().SetApplicationName("FreelovesCookBot").PersistKeysToDbContext<CookBotDbContext>()` (PROD-07); three size limits (PHOTO-04); explicit `UseStaticFiles` middleware for `/uploads` (PHOTO-06 + PITFALL H3 nosniff); `MapHealthChecks("/healthz")` with `AddDbContextCheck` (D-43); env-var-driven config overrides for `CookBotSettings`, connection string, `AiPricing` (PROD-19).
- **`CookBotDbContext`** — gains `DbSet<DataProtectionKey>` (via `IDataProtectionKeyContext`) and `DbSet<AiUsageLog>`. Existing `OnModelCreating` picks up `AiUsageLogConfiguration` automatically (Phase 1 pattern).
- **`AiApiKeyResolutionService`** — Phase 9 makes the read path encryption-aware; the share resolution semantics (owner's row + recipient context) remain unchanged. The single shared protector scope is the load-bearing decision (PROD-08 + PITFALL C2).
- **`AnthropicAiService.SendStructuredAsync` return** — extended `StructuredResult<T>` flows back through `AiRecipeGenerator.GenerateAsync` to `AiChat.razor`, which writes one `AiUsageLog` row at the end of the generation event (NOT inside the retry loop — H9 prevention).
- **`PromptBuilderService.BuildSystemPrompt`** — Phase 9 D-42 adds two prose clauses (`description` definition + steps-no-intro rule); Phase 8's Verify snapshot test regenerates `.verified` in the same commit as the prose change to keep CI green.
- **`RecipeEditor.razor`** — three editor surfaces land in Phase 9: photo composite (D-38 / PHOTO-09), Description textarea (D-39), per-step Temperature picker (Phase 8 deferred). All three are part of one editor PR per the wave-1 → wave-2 split in Claude's Discretion.
- **`AiChat.razor` write path for telemetry** — PROD-15 specifies the write happens "from `AiChat.razor` / `AiRecipeGenerator` after generation completes". The planner should put the write in `AiRecipeGenerator.GenerateAsync` (one source of truth for retry semantics), with `AiChat.razor` just consuming `StructuredResult<T>`.
- **`CookbookPdfService`** — receives NO new dependencies. Phase 9 D-40 explicitly keeps it text-only. The PDF flow continues to consume `CookbookTransferDocument` shape; PhotoUrl field is present on recipes but not rendered.
- **`.env.example`** — optional (not in REQUIREMENTS); planner may add it to `docker/` root as a copy-of-defaults reference for `COOKBOT_PORT`, `ConnectionStrings__DefaultConnection`, `ASPNETCORE_URLS`, key `appsettings.json` overrides.

</code_context>

<specifics>
## Specific Ideas

- **Both inputs visible** beats tabs and drag-zones — user wants the photo composite to be immediate and obvious in the editor; no "where do I click first?" friction. Match Paprika/Mealie's "both paths always usable" pattern over a modal switcher.
- **Description below name** is treated as the recipe's lede, NOT as a clutter-managed accordion item. Visually pairs name + lede the way RecipeView already does post-v1.2 — editor mirrors view mode.
- **Omit photos in PDF** — user chose simplicity over feature completeness for v1.3; PDF stays text-only with an explicit README note. Photo-in-PDF revisit is a v1.4+ stretch goal, not a v1.3 hole.
- **365-day hardcoded cleanup** — user picked the bounded-no-knob option. Implicit acceptance: long-term cost archaeology beyond 365 days is not a real use case for a self-hosted personal cooking app.
- **Prompt prose only for Description disambiguation** — user explicitly rejected the validator-warning and schema-attribute variants. Constrained decoding does most of the work; prose is the minimal nudge.
- **Healthcheck with explicit `on-failure` restart** — user picked the more-surfaceable variant over `unless-stopped`; PITFALL M6's first option preferred over its second. Container fails visibly after 3 retries instead of looping silently.

</specifics>

<deferred>
## Deferred Ideas

Surfaced during analysis but not in scope for this phase:

- **Photo-in-PDF rendering** — Phase 9 D-40 omits photos in PDF; revisit in v1.4+ via pre-fetched-bytes pattern in `CookbookDownloadHelper` if user demand materializes.
- **Multiple photos / photo gallery / carousel** — explicitly v1.4+ per REQUIREMENTS Out of Scope; v3 schema reserves a single `PhotoUrl`.
- **Image resizing / thumbnail generation / EXIF stripping** — explicitly v1.4+ (no `ImageSharp` per GPL incompatibility; browser handles via CSS `object-fit`).
- **Reverse-image search ("find a photo for this recipe" AI feature)** — separate AI-feature scope, v1.4+.
- **CDN integration / image proxying** — out of scope for trusted-LAN posture; `referrerpolicy="no-referrer"` is the only privacy mitigation in v1.3.
- **AiUsageLog retention beyond 365 days** — D-41 hardcodes; revisit if a user reports lost cost history archaeology need.
- **Admin-facing total-cost-by-user view** — REQUIREMENTS PROD-17 ships per-user Profile widget only; cross-user admin view (PITFALL M9 surface) is a v1.4+ admin-page concern with proper privacy disclosure.
- **`CookBotSettings.TelemetryEnabled` killswitch** — PITFALL M9 mentions this as one mitigation but REQUIREMENTS doesn't include it for v1.3; v1.4+ if a self-hoster requests opt-out.
- **TLS / HTTPS inside the container** — explicitly v1.4+ per REQUIREMENTS Out of Scope; v1.3 README points at reverse proxy.
- **First-run setup wizard / onboarding** — existing v1.2 gate-with-CTA empty states are sufficient per PROD-18; no new flow.
- **AI key rotation UX** — `IDataProtector` auto-rotates per .NET 10 defaults; user-facing rotation UI is v1.4+.
- **EXIF / metadata stripping on upload** — explicitly v1.4+; files stored as uploaded.
- **`.env.example` shape** — planner's discretion; not in REQUIREMENTS but acceptable as a `docker/` convenience.
- **Smart pantry-match dietary filter using `RecipeTag` JOIN** — Phase 10 (QOL-02). Phase 8 set the table up; Phase 10 consumes.
- **Profile telemetry read widget** — Phase 10 (PROD-17 surface). Phase 9 writes the rows; Phase 10 renders the per-user "AI usage" card.

### Reviewed Todos (not folded)

(No pending todos in `.planning/STATE.md` or todo system to evaluate.)

</deferred>

---

*Phase: 09-photos-prod-ready-infrastructure*
*Context gathered: 2026-05-16 (discuss mode — 4 user-selected gray areas + 1 bonus healthcheck area, all 5 resolved by user)*
