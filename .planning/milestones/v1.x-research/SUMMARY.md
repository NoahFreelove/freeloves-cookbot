# Research Summary — v1.3 Production-Ready & Format Maturity

**Project:** FreelovesCookBot
**Domain:** Self-hosted Blazor Server recipe tracker — v1.3 delta over shipped v1.2 codebase
**Researched:** 2026-05-15
**Confidence:** HIGH (all four research dimensions grounded in live codebase inspection + official ASP.NET Core 10 / Anthropic / NuGet docs)

---

## Executive Summary

FreelovesCookBot v1.3 adds five capability buckets to an already-solid v1.2 codebase: a schema v3 canonical bump (photos + description + per-step temperature), format cleanup (projector deletion + relational tags + snapshot tests), QOL (smart pantry-match + AI chat hardening + accent picker + prompt editor), small-stuff polish (cookbook reparenting, pantry quick-add, moon glyph, TopBar slot, live timer tick), and a prod-ready track that makes the app shippable for other self-hosters (Dockerfile, encrypt-at-rest API keys, token-cost telemetry, deploy docs). The stack delta is intentionally small: two new NuGet packages (`Microsoft.AspNetCore.DataProtection.EntityFrameworkCore 10.0.8` for EF-backed key ring persistence, `Verify.Xunit 31.12.5` for prompt snapshot regression tests) cover everything; all other new capabilities are code-only changes against existing services. The existing Clean/Onion architecture (Domain → Application → Infrastructure → Web) comfortably absorbs all five buckets without introducing new layers or abstractions.

The single highest-risk decision in v1.3 is the combination of `IDataProtector` encrypt-at-rest and Docker containerization. These two features co-depend in a way that is not obvious at development time: the Data Protection key ring must be on a named Docker volume, or every container restart silently destroys all users' encrypted API keys. This risk is well-mitigated by the research (sentinel-prefix pattern for backward-compatible key migration, `PersistKeysToDbContext` via the new NuGet package, three explicit volume mounts in docker-compose), but the mitigation must be built into the foundation phases, not retrofitted. The second load-bearing decision is ImageSharp rejection: the GPL-3.0 license incompatibility (confirmed via Six Labors Split License text) means photo validation uses magic-byte BCL sniffing instead — approximately 30 lines of pure .NET that carries no license risk.

The four researchers converge on all key technical facts. One divergence required reconciliation: STACK.md leaned toward `ContentRootPath/uploads/` (Microsoft's general recommendation for runtime-uploaded files), while ARCHITECTURE.md landed on `wwwroot/uploads/` + `UseStaticFiles(PhysicalFileProvider, RequestPath="/uploads")`. The ARCHITECTURE decision stands for v1.3: `wwwroot/uploads/` keeps the upload directory co-located with the web root, which simplifies the Docker single-volume story (the existing web root path is already the container's `ContentRootPath/wwwroot`), and the `UseStaticFiles` middleware approach correctly handles runtime-written files that `MapStaticAssets()` (build-time fingerprinting only) cannot serve. The key fact is that `MapStaticAssets()` and `UseStaticFiles()` must coexist — they serve different content.

---

## Key Findings

### Recommended Stack

The v1.3 NuGet additions are exactly two packages. `Microsoft.AspNetCore.DataProtection.EntityFrameworkCore 10.0.8` persists the Data Protection key ring into the existing SQLite DB via `CookBotDbContext`, which eliminates the documented Docker-volume failure mode of `PersistKeysToFileSystem` on Linux overlay/NFS filesystems (confirmed via dotnet/aspnetcore#2941). `Verify.Xunit 31.12.5` is compatible with the project's `xunit 2.9.2` and enables prompt-snapshot regression tests; `Verify.XunitV3` would require migrating all 196 tests to xUnit v3 — out of scope. All other v1.3 capabilities (image validation, URL scheme allowlist, token telemetry, tag migration, projector deletion, Dockerfile) are code-only.

**Core technologies (NEW for v1.3):**
- `Microsoft.AspNetCore.DataProtection.EntityFrameworkCore 10.0.8`: DB-backed key ring persistence — avoids Linux Docker volume reliability issues with `PersistKeysToFileSystem`; colocates key ring with `cookbot.db` for a clean one-volume backup story
- `Verify.Xunit 31.12.5`: prompt-snapshot regression tests — compatible with xUnit 2.9.2; `Verify.XunitV3` would force a full xUnit v3 migration
- BCL magic-byte sniffing (no NuGet): JPEG `FF D8 FF`, PNG `89 50 4E 47`, WebP at offset 8, GIF `47 49 46` — replaces `SixLabors.ImageSharp` (REJECTED: GPL-3.0 incompatible with the project's GPL-3.0-only license due to Apache 2.0 patent-termination clause conflict per FSF guidance)
- Anthropic SSE body `usage.input_tokens`/`usage.output_tokens` (no NuGet): token-cost telemetry extracted from `message_start.message.usage` and `message_delta.usage` in the existing SSE parse loop — the Anthropic Admin API (`sk-ant-admin...`) requires an organization-level key unavailable to individual self-hosters

**Confirmed anti-patterns (do not use):**
- `SixLabors.ImageSharp` — GPL-3.0 incompatible (hard block)
- `Magick.NET` — license complexity + 50 MB native binaries; overkill for "is this an image" validation
- `Microsoft.Extensions.AI` / official `Anthropic` NuGet — conflicts with `AnthropicAiService` direct-HttpClient design and structured-output transport
- `Newtonsoft.Json`, `NJsonSchema` — enforced anti-patterns since v1.1
- `Verify.XunitV3` — incompatible with xUnit 2.9.2; migration is a separate milestone concern
- Per-user `IDataProtector` scope for API key encryption — breaks the key-sharing flow where a recipient reads the owner's row

### Expected Features

The feature prioritization matrix from FEATURES.md distinguishes P1 (must-close carry-forwards or core milestone goals) from P2/P3 (meaningful additions and nice-to-haves). The roadmapper should use this to decide which features enter each phase versus which ride as stretch goals.

**Must have (P1 — closes named items or is core to the milestone):**
- Single hero photo: file upload (`<InputFile>` -> `wwwroot/uploads/`) + paste-URL coexist; onerror fallback to `<StripedPlaceholder>`; scheme allowlist (`http`/`https` only)
- `Recipe.Description` — closes D-25 (editor field persisted nowhere in v1.2, a trust-breaking bug)
- Per-step temperature as structured `int? Temperature` on `ContentStep` — first-class field, ahead of Cooklang/Mealie/Tandoor which all use plain text only
- V2->V3 upcaster bundling all three additions (one AI-prompt regression pass amortizes across all three)
- `LegacyRecipeProjector` deletion — closes FUTURE-V1.1-03
- `TagsJson` -> relational `RecipeTag` — closes FUTURE-V1.1-02; unlocks dietary filtering in pantry-match
- Dockerfile + docker-compose with three named volumes (`cookbot_db`, `cookbot_uploads`, `cookbot_keys`)
- README install/config/backup/upgrade sections
- Smart pantry-match: ingredient-coverage % scoring + recency debounce via `IRecipeMadeService` — closes FUTURE-13 (replaces deterministic stub)
- AiChat "Edit anyway" hardening via `RawRecipeEditorDialog` — closes FUTURE-15/WARN-AICHAT-RAW-EDIT-EDGE
- Encrypt-at-rest for `UserProfile.AiApiKey` via `IDataProtector` EF value converter — closes FUTURE-01
- Cookbook reparenting on edit — closes D-26
- Pantry per-row grocery quick-add — closes D-37
- Moon glyph for dark-mode toggle — closes D-15
- Home active-timer live JS tick (closes punch-list item from v1.2 audit)

**Should have (P2 — adds meaningful value within scope):**
- Accent variant picker (terracotta/sage) in `localStorage` — closes FUTURE-14; DS-02 token structure already wired
- Profile-side AI prompt editor for `UserProfile.AiSystemPromptTemplate` — closes DEFERRED-PROF-AIPROMPT
- Token-cost telemetry: `AiUsageLog` table + Profile 30-day rolling widget + key-owner breakdown — closes FUTURE-02
- Prompt-snapshot regression test for `PromptBuilderService` — closes FUTURE-V1.1-04
- README "Recipe Format" section — closes FUTURE-V1.1-05
- Recency debounce in pantry-match (7-day penalty via `IRecipeMadeService.GetLastCookAsync`)
- Dietary pre-filter in pantry-match (depends on `RecipeTag` relational migration)
- TopBar RightSlot passthrough via `ICbTopBarService` — closes D-16

**Defer to v1.4+ (anti-features for v1.3):**
- Multiple photos / photo gallery (requires carousel, lightbox, ordering controls)
- Thumbnail generation / server-side image resizing (requires ImageSharp or SkiaSharp, both GPL-problematic)
- Expiration-weighted pantry-match scoring (requires `PantryItem.ExpiresOn` + per-item date picker UI; no mainstream recipe app implements this; unlikely to see consistent manual entry on a trusted-LAN cooking app)
- AI call per pantry refresh for suggestions (expensive, slow, non-deterministic)
- CDN / image proxy integration (public CDN is cloud, not trusted-LAN)
- UI backup download button (README backup docs + volume guidance is sufficient for v1.3)
- SQLCipher full-DB encryption (disproportionate complexity; breaks EF migrations and DB inspection tools)
- CI/CD pipelines (not in PROJECT.md scope)

### Architecture Approach

All five buckets integrate cleanly into the existing Clean/Onion layer boundaries. The ARCHITECTURE researcher verified each integration point against the live codebase and found no new layers or abstractions are needed. The key structural decisions are: (1) `IRecipePhotoStorage` interface belongs in `CookBot.Domain/Interfaces/` with `LocalRecipePhotoStorage` implementation in `CookBot.Web/Services/` (file write is a Web-layer concern; `IBrowserFile` is a Blazor type unavailable in Application/Domain); (2) `IPantryMatchService` is a new Application-layer service replacing the 40-line inline `BuildPantryMatchesAsync` in `Home.razor.cs`; (3) schema v3 adds three nullable properties to the existing `RecipeDocument` record (not a new versioned type) because the upcaster chain operates at the JSON-node level before typed deserialization; (4) token telemetry surfaces via `StructuredResult<T>` field extensions bubbled from `AnthropicAiService` through `AiRecipeGenerator` to `AiChat.razor`, which writes the `AiUsageLog` row.

**Major new components:**
1. `Migration_V2_To_V3` (Application) — trivial stamp-version upcaster; null-fills all three new fields; registered in `DependencyInjection.cs`
2. `IRecipePhotoStorage` / `LocalRecipePhotoStorage` (Domain interface, Web implementation) — writes to `wwwroot/uploads/`, enforces magic-byte validation + size cap, returns `/uploads/{guid}.{ext}` URL
3. `RecipePhotoUrlValidator` (Application, static) — `Uri.TryCreate` + scheme allowlist; called in editor, `RecipeService`, and AI orchestrator post-process
4. `IPantryMatchService` / `PantryMatchService` (Application) — coverage-ratio scoring + dietary pre-filter + recency debounce; replaces inline stub
5. `AiUsageLog` entity (Domain) + `AiUsageLogConfiguration` (Infrastructure) — keyed `(UserId, KeyOwnerUserId, ModelName, InputTokens, OutputTokens, EstimatedCostUsd, Timestamp)`
6. `CbTopBarService` / `ICbTopBarService` (Web) — Scoped service pattern matching `ICbToastService`; enables page -> layout slot injection
7. `RawRecipeEditorDialog` (Web) — textarea pre-filled with raw JSON; "Try to parse" button re-runs `Parser.TryParse`; replaces the D-09 degraded-toast fallback in `AiChat.razor`
8. `docker/Dockerfile` + `docker/docker-compose.yml` + `docker/.env.example` — multi-stage build; three named volumes; `ASPNETCORE_URLS=http://+:7000`

**Files deleted (format cleanup):**
- `src/CookBot.Infrastructure/Data/Migrations/Helpers/LegacyRecipeProjector.cs`
- `src/CookBot.Application/Recipes/IRecipeProjector.cs`

### Critical Pitfalls

All 8 critical + 11 high pitfalls in PITFALLS.md are grounded in live codebase inspection. The top 5 that must ride into ROADMAP success criteria:

1. **Data Protection key ring not on a named Docker volume (C1)** — container restart silently destroys all encrypted API keys with no user-visible error (app behaves as if no key is set). Prevention: `docker-compose.yml` must have an explicit named volume for the key ring directory on day one; retrofitting requires all users to re-enter keys. The `PersistKeysToDbContext` approach (new NuGet) eliminates this by colocating the key ring with `cookbot.db` in the same volume.

2. **Existing plaintext `AiApiKey` rows not migrated before EF value converter activates (C3)** — `IDataProtector.Unprotect` throws `CryptographicException` on non-ciphertext input; all existing users lose AI access immediately on upgrade. Prevention: sentinel-prefix pattern (`enc:v1:<base64>`) allows the read path to detect and pass through legacy plaintext while scheduling re-encryption; `DatabaseSeeder.SeedAsync` re-encrypts all un-prefixed rows on first boot.

3. **File uploads silently drop the Blazor circuit instead of showing an error (H1)** — three independent size limits must all be raised: Kestrel `MaxRequestBodySize`, `FormOptions.MultipartBodyLengthLimit`, and Blazor Server `AddServerSideBlazor(opts => opts.MaximumReceiveMessageSize = ...)`. Failing to raise the SignalR limit causes a silent circuit disconnect with no error toast.

4. **V2->V3 upcaster null-fills per-step temperature with `{ value: 0, unit: "F" }` instead of `null` (M2 / C7)** — cooking mode shows "Bake at 0 degrees F" on every step of every legacy recipe. Prevention: `ContentStep.Temperature` must be declared `int?` (nullable); the upcaster must leave the field absent.

5. **Smart pantry-match materializes all recipes before filtering — O(recipes x ingredients) on every Home load (H7)** — Home page load time grows linearly with recipe count. Prevention: push ingredient-intersection filter to EF Core before `ToListAsync`; add composite index on `RecipeIngredient(IngredientId, RecipeId)`.

**Additional pitfalls that must appear in success criteria:**
- `wwwroot/uploads/` not in `.gitignore` before any upload code ships (C5) — add entry as the first task of the photos plan
- Single-purpose `IDataProtector` scope for all API key encryption (C2) — never use per-user scope; key-sharing reads owner's row with the same protector
- `onerror` fallback loop without `this.onerror=null` (H4) — Blazor state-flag approach preferred over inline JS
- Path-traversal via `IBrowserFile.Name` (H2) — always generate server-side GUID filename; never use client-supplied name
- `MapStaticAssets()` does not serve runtime-uploaded files (Architecture anti-pattern) — must add `UseStaticFiles` with `PhysicalFileProvider` separately

---

## Implications for Roadmap

### Two Competing Phase Framings

The user confirmed five feature buckets. The ARCHITECTURE researcher proposed a 3-phase shape (foundation / prod-ready / QOL+polish). Both framings are valid; the roadmapper should choose. This section presents both and then gives a clear recommendation.

**Framing A — User's 5 Buckets (as-stated):**
1. Schema v3 + Photos
2. Format cleanup
3. QOL
4. Small-stuff polish
5. Prod-ready (self-hosters)

**Framing B — ARCHITECTURE's 3-Phase Build Order:**
1. Foundation (schema v3 + all EF migrations + format cleanup + file storage + URL safety)
2. Prod-ready infra (encrypt-at-rest + Docker + token telemetry)
3. Consumer features (smart pantry-match + AiChat hardening + QOL + polish)

**Recommendation: a modified 3-phase shape (Phases 8, 9, 10) aligned with the ARCHITECTURE build-order rationale.** The bucket framing groups by feature type; the phase framing groups by dependency order. Phase dependencies in v1.3 run strictly foundation -> infra -> consumers: the V2->V3 upcaster must exist before consuming surfaces; `RecipeTag` must exist before dietary pantry-match filtering; the `AiUsageLog` migration must exist before the telemetry widget; Docker + key ring volumes must be co-designed with encrypt-at-rest. Shipping in bucket order (photos first, then format, then QOL, then prod-ready) would require re-touching schema-related files across phases and risk shipping a partially-complete Docker story.

The ARCHITECTURE researcher's proposed phase numbering (Phase 8 = foundation, Phase 9 = prod-ready infra, Phase 10 = consumer/QOL/polish) maps cleanly onto v1.2's phase numbering continuation (v1.2 ended at Phase 7). The roadmapper may choose to split Phase 9 or Phase 10 further if the work volume justifies it.

---

### Phase 8: Format Foundation

**Rationale:** Everything else depends on the V2->V3 schema bump. The upcaster, the three new nullable fields on `RecipeDocument`/`ContentStep`, the `RecipeTag` relational migration, and the `LegacyRecipeProjector` deletion must all be in place before any consuming surface can be built. Doing this first also means the AI-prompt regression test (prompt snapshot) can be written once against a stable schema and serve as a guard rail for all subsequent phases.

**Delivers:**
- `RecipeDocument` v3 with `PhotoUrl string?`, `Description string?`, `ContentStep.Temperature int?`
- `Migration_V2_To_V3` upcaster (pure stamp — null-fills all three new fields)
- `RecipePhotoUrlValidator` static helper with full unit-test coverage of rejected URL schemes
- `RecipeJsonSchemaProvider` updated to v3 shape with schema-output assertion test
- EF migrations: `AddRecipePhotoUrl`, `AddRecipeDescription` (or combined), `AddRecipeTagTable`, `AddAiUsageLog` (entity shell only)
- `RecipeTag` entity + configuration + data migration from `TagsJson`
- `LegacyRecipeProjector` deleted; `RecipeService.CreateAsync`/`UpdateAsync` rebuilt without it
- `PromptBuilderService` lint denylist updated: add `image`, `imageUrl`, `picture`, `thumbnail`
- Prompt-snapshot regression test for `PromptBuilderService.BuildSystemPrompt`
- `RecipeFormatParserTests` audited and converted to structural assertions before any schema changes merge (H11 prevention)

**Features from FEATURES.md:** Bucket 1 (schema layer), Bucket 2 (full format cleanup)
**Pitfalls to avoid:** C7, C8, M1, M2, M3, M10, H11

**Research flag:** SKIP — all patterns directly observed in live codebase; ARCHITECTURE.md covers every file touch.

---

### Phase 9: Photos + Prod-Ready Infrastructure

**Rationale:** File upload, encrypt-at-rest, and Docker must ship together because they share three co-dependent design decisions: (1) `wwwroot/uploads/` is a Docker volume mount; (2) the Data Protection key ring must be on a named volume beside `cookbot.db`; (3) the `DatabaseSeeder` one-time key-re-encryption pass must run after the `DataProtectionKeys` table migration is applied. Shipping Docker without the key ring volume would be a latent data-loss bug.

**Delivers:**
- `IRecipePhotoStorage` / `LocalRecipePhotoStorage`: magic-byte validation, GUID filename generation, size cap, `X-Content-Type-Options: nosniff`
- `UseStaticFiles(PhysicalFileProvider, RequestPath="/uploads")` in `Program.cs`
- `wwwroot/uploads/` in `.gitignore` + `.gitkeep`
- Kestrel + `FormOptions` + SignalR `MaximumReceiveMessageSize` all raised (H1 prevention)
- `RecipeEditor.razor`: photo input with live preview, URL validation, clear button
- `RecipeView.razor` + Home cards + AiChat canvas: photo rendering with Blazor-state-flag onerror fallback
- `IDataProtector` encrypt-at-rest: sentinel-prefix migration in `DatabaseSeeder`, single shared protector scope (`"CookBot.AiApiKey"`), `Unprotect` in `AiApiKeyResolutionService`, `Protect` in `EditProfile.razor` save path
- `Program.cs` `AddDataProtection().PersistKeysToDbContext<CookBotDbContext>()`
- `CookBotDbContext` implements `IDataProtectionKeyContext`; migration `AddDataProtectionKeysTable`
- Owner-sets-key + recipient-uses-key integration test (C2 prevention)
- `docker/Dockerfile`, `docker/docker-compose.yml` (three named volumes), `docker/.env.example`
- `ASPNETCORE_URLS=http://+:7000` in Dockerfile ENV (M4 prevention)
- SQLite directory mount not file mount (M5 prevention); `healthcheck` + `restart: on-failure` (M6); `TZ=UTC` (M7)
- README install/config/backup/upgrade sections
- `AiUsageLog` write path: SSE parsing extended in `AnthropicAiService`; `StructuredResult<T>` extended; `AiChat.razor` writes one row per `GenerateAsync` call (H9 prevention)
- Pricing in `appsettings.json` under `CookBot.AiPricing` (H10 prevention)
- Composite index `IX_AiUsageLogs_UserId_CreatedAt` in migration (M8 prevention)
- `CookBotSettings.TelemetryEnabled` killswitch + UI disclosure note (M9 prevention)
- `QuestPDF` PDF export: photo omitted or pre-fetched bytes passed asynchronously (H6 prevention)

**Features from FEATURES.md:** Bucket 1 (editor + consuming surfaces), Bucket 5 (Dockerfile, encrypt-at-rest, telemetry, README)
**Pitfalls to avoid:** C1, C2, C3, C4, C5, C6, H1, H2, H3, H4, H5, H6, M4, M5, M6, M7, M8, M9, H9, H10

**Research flag:** SKIP — all implementation decisions resolved; open questions reconciled in this SUMMARY.

---

### Phase 10: QOL, Polish, and Consumer Features

**Rationale:** All consumer features depend on Phase 8 foundations: smart pantry-match dietary filtering requires `RecipeTag`; Profile telemetry widget requires `AiUsageLog` rows from Phase 9. Polish items (moon glyph, TopBar slot, timer tick) have no dependencies but benefit from a stable base. This phase can be executed as one wave or split if scope is tight.

**Delivers:**
- `IPantryMatchService` / `PantryMatchService`: coverage % scoring, dietary pre-filter (from `RecipeTag`), recency debounce, EF-side pre-filter before `ToListAsync`, composite index on `RecipeIngredient(IngredientId, RecipeId)` (H7, H8 prevention)
- `RawRecipeEditorDialog.razor`: textarea + "Try to parse" + "Copy" buttons; replaces D-09 toast
- Accent variant picker in `EditProfile.razor`: three named accents, `localStorage` persistence (no `UserProfile` column)
- Profile AI prompt editor: `<CbTextarea>` bound to `UserProfile.AiSystemPromptTemplate`, variable reference panel, "Reset to default"
- Profile telemetry widget: 30-day rolling totals + key-owner recipient breakdown (reads `AiUsageLog`)
- Cookbook reparenting: `RecipeService.UpdateAsync` extended with optional `newCookbookId` + ownership check; `RecipeEditor.razor` cookbook picker
- Pantry per-row grocery quick-add: `GroceryListService.AddItemAsync` + cart icon in `PantryView.razor`
- Moon glyph: `Icon.Names.Moon` constant + crescent SVG in `Icon.razor`; `TopBar.razor` conditional
- `ICbTopBarService` / `CbTopBarService`: Scoped service; `MainLayout.razor` subscribes; passes `RightSlot` to `TopBar`
- Home active-timer live JS tick: `CookingTimers.startHomeTick` in `cooking-timers.js`
- README "Recipe Format" section

**Features from FEATURES.md:** Bucket 3 (QOL), Bucket 4 (small-stuff polish), P2 items from Buckets 2 and 5
**Pitfalls to avoid:** H7, H8

**Research flag:** SKIP for polish items. Consider `/gsd-discuss-phase` before Phase 10 if the roadmapper wants to validate pantry-match scoring weights against actual user data before committing to specific values.

---

### Phase Ordering Rationale

The foundation-before-consumers dependency chain is strict in three places:
1. `RecipeDocument` v3 properties must exist before any photo input, temperature display, or description field can be wired in any Razor component.
2. `RecipeTag` table must exist before the pantry-match dietary pre-filter can use a JOIN instead of JSON extraction in SQL.
3. The Data Protection key ring volume and `PersistKeysToDbContext` migration must exist before the `DatabaseSeeder` re-encryption pass runs — and both must be live in the same Docker phase to avoid shipping a half-complete self-hoster story.

Within Phase 9, token telemetry write path can be parallelized with encrypt-at-rest and Docker work. The telemetry read path (Profile widget) rides into Phase 10 as part of `EditProfile.razor` additions. Within Phase 10, all items are independent and can be parallelized at the plan level.

### Research Flags

All three phases: SKIP research-phase. Technical patterns are either directly observed in the live codebase or verified via official ASP.NET Core 10 docs and Anthropic streaming docs. The four researchers agree on all key implementation decisions. Open questions that remained after individual research files were resolved in this synthesis.

---

## Confidence Assessment

| Area | Confidence | Notes |
|------|------------|-------|
| Stack | HIGH | Two new NuGets verified against NuGet.org + official MS docs; ImageSharp GPL incompatibility confirmed from Six Labors license text; Anthropic SSE token fields confirmed from official streaming docs |
| Features | HIGH (photo patterns, Anthropic API constraints); MEDIUM (pantry-match algorithm) | No recipe app publicly documents their pantry-match scoring algorithm; coverage % is the standard but exact weighting is inferred from SuperCook/Mealie behavior |
| Architecture | HIGH | All integration points verified against live source files at cited paths; `MapStaticAssets` vs `UseStaticFiles` behavior for runtime-uploaded files confirmed from official docs |
| Pitfalls | HIGH | All 8 critical + 11 high pitfalls grounded in actual source code at cited paths; Docker/Data-Protection pitfalls cross-referenced with official MS docs and dotnet/aspnetcore GitHub issues |

**Overall confidence: HIGH**

### Reconciled Divergences

1. **File upload storage path (STACK vs. ARCHITECTURE):** STACK.md cited Microsoft's general recommendation for `ContentRootPath/uploads/`. ARCHITECTURE.md landed on `wwwroot/uploads/` + `UseStaticFiles(PhysicalFileProvider)`. ARCHITECTURE wins for v1.3: `wwwroot/uploads/` simplifies the Docker volume story (one directory subtree), and `UseStaticFiles` middleware is the correct way to serve runtime-written files that `MapStaticAssets()` (build-time only) cannot see.

2. **Expiration weighting in pantry-match (FEATURES vs. general expectations):** FEATURES.md explicitly flags expiration-weighted scoring as an anti-feature at v1.3 scope. Anti-feature designation stands. Coverage % + recency debounce is the algorithm for v1.3.

### Gaps to Address During Planning

- **Pantry-match scoring weights:** The specific debounce weights and coverage threshold (proposed: `coverageScore - 0.3 * recentlyMadePenalty`, threshold >= 60%) are engineering estimates. They should be surfaced in Phase 10 plan and made configurable in `appsettings.json` rather than hardcoded.
- **`AiApiKey` sentinel-prefix detection:** The exact heuristic (detect `CfDJ8...` Data Protection ciphertext prefix vs. plaintext `sk-ant-` key) should be pinned in the Phase 9 plan. ARCHITECTURE.md notes Data Protection ciphertext starts with `CfDJ8` — document this explicitly.
- **Token pricing table values:** Actual per-million-token prices for Haiku 4.5, Sonnet 4.6, and Opus 4.7 should be verified at Phase 9 plan time against Anthropic's current pricing page and embedded in `appsettings.json` defaults with the verification date.
- **`RecipeTag` migration drop timing:** The `TagsJson` column drop (after both columns coexist) should be specified in Phase 8 plan as a separate migration within the same phase, not deferred to Phase 10.

---

## Sources

### Primary (HIGH confidence — official docs and live codebase)
- ASP.NET Core 10 Data Protection docs (key storage providers, key encryption at rest, configuration)
- ASP.NET Core 10 Blazor file uploads docs — three size limits, `IBrowserFile.ContentType` untrusted warning
- ASP.NET Core 10 Static Files docs — `MapStaticAssets` vs `UseStaticFiles` for runtime-uploaded files; `PhysicalFileProvider`
- Anthropic Messages Streaming docs — SSE event shapes for `message_start.message.usage` and `message_delta.usage`
- Anthropic Usage and Cost API docs — Admin API key requirement; per-request `usage` fields available to individual accounts
- Six Labors ImageSharp LICENSE on GitHub — Split License GPL-3.0 incompatibility confirmed
- NuGet: `Verify.Xunit 31.12.5` dependency spec; `Microsoft.AspNetCore.DataProtection.EntityFrameworkCore 10.0.8`
- Live codebase source inspection — `AiApiKeyResolutionService.cs`, `SecretRedactor.cs`, `RecipeUpcasterChain.cs`, `RecipeJsonSchemaProvider.cs`, `Home.razor.cs`, `CookbookPdfService.cs`, `.gitignore`
- dotnet/aspnetcore#2941 + dotnet/dotnet-docker#4252 — `PersistKeysToFileSystem` Linux Docker volume failure modes

### Secondary (HIGH confidence — live docs via WebFetch)
- Paprika iOS User Guide — file upload patterns, hero photo UX
- Mealie documentation — backup UI, docker-compose patterns, AI key handling, default credentials flow
- Tandoor Docker setup docs — `mediafiles` volume, nginx static file serving
- Cooklang Specification — no structured temperature field (plain text only)
- OWASP File Upload Cheat Sheet — magic-byte validation, path-traversal prevention

### Secondary (MEDIUM confidence — community consensus)
- Cooklang Greedy Coverage Blog — ingredient-coverage scoring as the standard pantry-match axis
- SuperCook product behavior — ingredient-unlock scoring, partial match display (algorithm not publicly documented)
- Mealie GitHub pantry discussion threads — basic ingredient intersection; no documented weighting

---
*Research completed: 2026-05-15*
*Ready for roadmap: yes*
*Phases start at: Phase 8 (continuation from v1.2 Phase 7)*
