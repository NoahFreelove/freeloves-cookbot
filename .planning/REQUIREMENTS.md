---
milestone: v1.3
milestone_name: Production-Ready & Format Maturity
authored: 2026-05-15
buckets: 6
categories: 6
requirements: 63
status: drafted
---

# v1.3 Requirements — Production-Ready & Format Maturity

**Milestone goal:** Make CookBot shippable for other self-hosters while landing the deferred format/QOL/polish work — one v3 schema bump carries photos + description + per-step temperature, a new prod-ready track ships Docker + encryption + telemetry + deploy docs, and the v1.2 carry-forward tech-debt list closes.

**Phase numbering:** Phases continue from v1.2 — v1.3 starts at **Phase 8**.

**Sources informing these requirements:**

- User's 5-bucket framing + 3 AskUserQuestion scoping rounds (file upload + bundle in v3 + all QOL/polish/prod-ready items)
- Gap answers: per-step temperature **F + C + gas**; backup is **volumes + README only** (no UI button)
- `.planning/research/SUMMARY.md` (synthesizer) + STACK.md + FEATURES.md + ARCHITECTURE.md + PITFALLS.md
- `.planning/v1.3-PHASE-CANDIDATE-recipe-photos.md` (seed doc — paste-URL bright line lifted; IMG-* re-mapped)
- v1.2 audit `tech_debt` + v1.1 carry-forwards (FUTURE-V1.1-* / FUTURE-01..15 / D-* / DEFERRED-PROF-AIPROMPT)
- CLAUDE.md hard invariants (canonical-first reads; AI-off contract; POLISH-01 no extractor revival; no MudBlazor/Newtonsoft/MEAI/NJsonSchema/Identity middleware)

---

## v1.3 Requirements

### Schema v3 + Upcaster (`SCHEMA-*`)

Canonical `RecipeDocument` bumps from v2 → v3. Three nullable additions are bundled into a single upcaster step. The schema bump is the foundation for everything downstream (Photos surface, AI prompt change, Format-cleanup tests).

- [ ] **SCHEMA-01**: `RecipeDocument.PhotoUrl` (string?, max 2048 chars) added as a v3 field on the canonical record. Null is the legal default — existing v2 docs upcast with `PhotoUrl = null`.
- [ ] **SCHEMA-02**: `RecipeDocument.Description` (string?) added as a v3 field. Null is the legal default — existing v2 docs upcast with `Description = null`. Distinct from the first step's text; the editor surfaces it as a subtitle/lede.
- [ ] **SCHEMA-03**: `ContentStep.Temperature` (nullable structured record `{ Value: int, Unit: "F" | "C" | "gas" }`) added as a v3 field. Null is the legal default — existing v2 steps upcast with `Temperature = null`. Gas-mark unit supports UK home-cook convention (1–9 + half-stops).
- [ ] **SCHEMA-04**: `RecipeDocument.Version` bumps from `2` to `3`. `RecipeUpcasterChain.CurrentVersion` updated; new `Migration_V2_To_V3` step added — null-coalescing per field (NOT a single bundle-throw, per PITFALLS C7).
- [ ] **SCHEMA-05**: `Recipe.PhotoUrl` entity column added (string?, max 2048 chars). EF migration `AddRecipePhotoUrl` runs forward-only; `IDatabaseBackupService` fires `cookbot.db.pre-AddRecipePhotoUrl.bak` per existing pattern.
- [ ] **SCHEMA-06**: `Recipe.Description` entity column added (string?, max 4096 chars). EF migration `AddRecipeDescription` runs forward-only with backup.
- [ ] **SCHEMA-07**: `RecipeJsonSchemaProvider` regenerates JSON schema sent to Anthropic — auto-reflects the 3 new fields via `JsonSchemaExporter` against the C# type (no manual schema editing).
- [ ] **SCHEMA-08**: `RecipeFormatParser` reads and writes the 3 new fields in YAML wire format (`photoUrl:`, `description:`, `temperature: {value, unit}` per step) and in JSON export (`"photoUrl"`, `"description"`, `"temperature"`).
- [ ] **SCHEMA-09**: `JsonRecipeSerializer` includes the 3 new fields in `Recipe.CanonicalDocumentJson` round-trip. Backwards-compat test: existing v2 JSON deserializes via upcaster without loss.
- [ ] **SCHEMA-10**: AI lint denylist extended for the 3 new fields' aliases — `image`, `imageUrl`, `picture` (for PhotoUrl); `summary`, `desc` (for Description); `temp`, `oven` (for Temperature). Prevents AI from emitting alternate field names that bypass the canonical schema.
- [ ] **SCHEMA-11**: Schema provider assertion test added in `CookBot.Tests` — asserts the AI-shipped JSON schema includes `photoUrl`, `description`, and step-level `temperature`. Must be the FIRST test written before any other schema code (per PITFALLS C8).
- [ ] **SCHEMA-12**: `RecipeFormatParserTests` audited and updated for v3 fields. Round-trip fixture tests cover `null`, valid value, all three temperature units (`F`/`C`/`gas`), and broken/invalid value handling.

### Photos surface (`PHOTO-*`)

File upload AND paste-URL both ship. Photo is part of the canonical doc (SCHEMA-01) and round-trips through AI/JSON/cookbook export/import. PITFALLS C5 is the first task: `.gitignore` BEFORE any upload code lands.

- [ ] **PHOTO-01**: `wwwroot/uploads/` added to `.gitignore` as the FIRST commit of the photos phase, before any upload code is written (per PITFALLS C5).
- [ ] **PHOTO-02**: Blazor `<InputFile OnChange="..."/>` accepts JPEG/PNG/GIF/WebP only. Server-side magic-byte sniffing (BCL `Span<byte>`) validates first 12 bytes — NOT trusting `IBrowserFile.ContentType`. Server-side rejection produces a user-facing toast with the rejected file type and the allowed list.
- [ ] **PHOTO-03**: `OpenReadStream(maxAllowedSize: 10 * 1024 * 1024)` — 10 MB per-file cap. Larger uploads rejected with a clear error toast (NOT a silent SignalR drop).
- [ ] **PHOTO-04**: Three independent size limits all raised in `Program.cs` to support PHOTO-03 (per PITFALLS H1): Kestrel `MaxRequestBodySize`, `FormOptions.MultipartBodyLengthLimit`, AND `AddServerSideBlazor MaximumReceiveMessageSize` set to 12 MB (10 MB payload + envelope headroom).
- [ ] **PHOTO-05**: Uploaded files renamed with `Path.GetRandomFileName()` + original extension — NEVER use the client-supplied filename (per PITFALLS H2 path traversal).
- [ ] **PHOTO-06**: `wwwroot/uploads/` served via explicit `UseStaticFiles(new StaticFileOptions { FileProvider = new PhysicalFileProvider(Path.Combine(env.WebRootPath, "uploads")), RequestPath = "/uploads" })` in `Program.cs` (`MapStaticAssets` is build-time-only and cannot serve runtime uploads).
- [ ] **PHOTO-07**: Paste-URL `RecipePhotoUrlValidator` shared service — rejects non-`http`/`https` schemes (`javascript:`, `data:`, `file:`, `vbscript:`, etc.). Same validator runs on AI-emitted `PhotoUrl` in `AnthropicAiService` structured-output return path (per PITFALLS H5).
- [ ] **PHOTO-08**: `<img>` tags rendered from `PhotoUrl` set `referrerpolicy="no-referrer"` (don't leak the recipe URL to the photo host) and `loading="lazy"` (off-viewport tiles don't block initial render). `onerror` handler falls back to `<StripedPlaceholder>` with a one-shot debounce — no infinite loop (per PITFALLS H4).
- [ ] **PHOTO-09**: Recipe Editor `<StripedPlaceholder>` block replaced with a composite — paste-URL input + file-upload chooser + live preview + clear button. When `_photoUrl` is non-null and validates, the preview swaps in `<img>` 4:3 cropped; when null/invalid, the StripedPlaceholder stays. Whitespace-only paste-URL treats as `null` (don't persist empty strings).
- [ ] **PHOTO-10**: `RecipeView.razor` hero swaps `<StripedPlaceholder>` → `<img src="@_photoUrl">` when set, with `onerror` fallback to placeholder (broken-link recovery).
- [ ] **PHOTO-11**: Home "Recently cooked" tile + "Tonight from your pantry" hero card render thumbnails from `Recipe.PhotoUrl` when available. Same `onerror` fallback.
- [ ] **PHOTO-12**: AI Chat canvas streaming card surfaces `_lastStructuredRecipe.Value.PhotoUrl` when present. POLISH-01 invariant preserved — no extractor revival; direct canonical-doc binding.
- [ ] **PHOTO-13**: CookbookList collage thumbnails sample from real recipe `PhotoUrl`s when available; fall back to existing accent-tinted striped tiles. Keep current 3×2 collage shape.
- [ ] **PHOTO-14**: `IDatabaseBackupService` is the existing DB-only backup pattern; PHOTO-14 documents in README that `uploads/` is a separate volume that the self-hoster MUST back up in tandem with `cookbot.db` (per PITFALLS C6). No code change here — README-only requirement (also reinforced in PROD-13).

### Format cleanup (`CLEAN-*`)

The four v1.1 Phase 4 carry-forwards (`FUTURE-V1.1-02..05`) ship together. The `LegacyRecipeProjector` deletion has 6 touch points and must follow a specific sequence.

- [ ] **CLEAN-01**: `LegacyRecipeProjector` deletion — sequence: (a) add startup null-canonical guard in `DatabaseSeeder.SeedAsync` (fail-loud if any row has null `CanonicalDocumentJson`), (b) replace `_projector.Project(recipe)` in `RecipeService` with direct `RecipeDocument` construction from `ParsedRecipe`, (c) remove `IRecipeProjector` from `RecipeService` constructor, (d) remove `IRecipeProjector` DI registration, (e) delete `LegacyRecipeProjector.cs` and `IRecipeProjector.cs`. Closes `FUTURE-V1.1-03`.
- [ ] **CLEAN-02**: `Recipe.TagsJson` migrated to relational `RecipeTag` table — entity `RecipeTag(Id, RecipeId, Name)` with composite index on `(RecipeId, Name)`. EF migration `AddRecipeTagTable` populates from existing `TagsJson` data. `RecipeService` reads/writes through the relational table; serialization to canonical doc projects tags from the table at serialize time. `TagsJson` column retained through this phase; drops in a follow-up migration. Closes `FUTURE-V1.1-02`.
- [ ] **CLEAN-03**: Prompt snapshot regression test — `Verify.Xunit 31.12.5` added to `CookBot.Tests`. Test class decorated `[UsesVerify]`; `Verifier.DerivePathInfo` configures `tests/CookBot.Tests/Snapshots/`. Asserts `PromptBuilderService.BuildSystemPrompt(...)` output is byte-stable across runs. Closes `FUTURE-V1.1-04`.
- [ ] **CLEAN-04**: README "Recipe Format" section — documents the canonical `RecipeDocument` v3 structure, YAML wire format, JSON export format, and the V1→V2→V3 upcaster lineage. Includes a worked example with all three v3 fields populated. Closes `FUTURE-V1.1-05`.

### QOL (`QOL-*`)

Four QOL items the user picked across all multi-selects. Smart pantry-match uses ingredient-coverage % baseline + recency debounce via existing `IRecipeMadeService` (per FEATURES research); expiration-weighting is explicitly out (anti-feature at this scope).

- [x] **QOL-01**: Smart pantry-match service `IPantryMatchService` in `CookBot.Application`. Scoring formula: `pantryMatches / totalIngredients` (ingredient-coverage %) — Cooklang-blog/SuperCook baseline. Tie-break by recency-debounce (recipes cooked in last 7 days score lower) sourced from `IRecipeMadeService`. Stable sort by `(score desc, recipeId asc)` to prevent volatility on reload (per PITFALLS H8). Replaces Home's deterministic stub. Closes `FUTURE-13`.
- [x] **QOL-02**: Smart pantry-match dietary filter — `UserProfile.DietaryPreferences` (existing) filters out recipes containing excluded ingredients before scoring. Requires `RecipeTag` relational table (depends on CLEAN-02; informs sequencing).
- [x] **QOL-03**: Smart pantry-match composite DB indexes — index on `RecipeIngredient(RecipeId, IngredientId)` + `PantryItem(UserId, IngredientId)` to keep Home load O(n log n), not O(n²) (per PITFALLS H7). Migration `AddPantryMatchIndexes`.
- [x] **QOL-04**: AI Chat "Edit anyway" hardening — `RawRecipeEditorDialog` Cb component replaces the silent `IRecipeFormatParser.TryParse` fallback. Dialog shows the raw AI response in a textarea with a "Parse and save" action that re-runs the parser, and a "Save raw to clipboard" action for manual recovery. Closes `FUTURE-15`.
- [x] **QOL-05**: User-facing accent variant picker — terracotta / sage (in addition to default orange). CSS tokens already wired in v1.2 DS-02. Persistence via `localStorage.setItem("cookbot_accent", v)` (matches density-toggle pattern, NOT a new `UserProfile` column). Profile UI: radio group with live in-page preview. Sets `data-accent` on `<html>` before first paint. Closes `FUTURE-14`.
- [x] **QOL-06**: Profile-side AI prompt editor — surfaces `UserProfile.AiSystemPromptTemplate` (already loaded by `PromptBuilderService.BuildSystemPrompt` but with no UI today). Profile page adds an "AI assistant instructions" card with a `<CbTextarea>` for the template + reset-to-default button + variable insertion hints (`{{recipe-name}}`, `{{user-name}}`). Save persists to `UserProfile`. Closes `DEFERRED-PROF-AIPROMPT`.
- [x] **QOL-07**: AI prompt editor — prompt-injection warning UI. A small `<CbCard>` note adjacent to the editor explains that custom templates ARE injected verbatim into the system prompt and recommends avoiding instructions that override safety (PromptInjectionGuard wraps user content but not the system-prompt template itself).

### Small-stuff polish (`POLISH-*`)

The five tech-debt items from the v1.2 audit + 6-design-handoff list. Each is small (≤1 plan).

- [x] **POLISH-01**: Cookbook reparenting on edit — `RecipeService.UpdateAsync` accepts a new optional `cookbookId` parameter. Validates the user has access to the destination cookbook via `db.UserCanAccessCookbookAsync`. Closes v1.2 D-26.
- [x] **POLISH-02**: Pantry per-row quick-add — `PantryView` per-row "Add to grocery" cart icon (currently disabled affordance) wires to `GroceryListService.AddItem` for the current user's primary grocery list. Toast on success; closes v1.2 D-37.
- [x] **POLISH-03**: Moon glyph added — 37th outline icon in `Icon.razor` (current set has Sun but no Moon, so the dark-mode toggle uses Sun for both states). Dark-mode toggle now shows Sun when light and Moon when dark. Closes v1.2 D-15.
- [x] **POLISH-04**: TopBar `RightSlot` per-page passthrough — `MainLayout` exposes a `[CascadingParameter]`-style mechanism (or a `RenderFragment` page parameter) so pages can inject content into `TopBar.RightSlot`. `RecipeView.razor` migrates RV-05 actions from the inline-above-hero PRAGMATIC fallback to the TopBar slot. Closes v1.2 D-16.
- [x] **POLISH-05**: Home active-timer live JS tick — `cooking-session-state.js` adds a `setInterval(updateTick, 1000)` that updates `data-remaining-seconds` on the DOM band. Tear down on page unload. Closes the v1.2 slice-09 punch-list "live JS tick" item.

### Prod-ready for self-hosters (`PROD-*`)

The new track. Docker + encrypt-at-rest + token telemetry + README rewrite + first-run UX. Highest pitfall density of any v1.3 bucket — PITFALLS C1/C2/C3/C4/C6 + H9/H10 + M4/M5/M6 are all addressed below.

#### Dockerfile + compose

- [ ] **PROD-01**: Multi-stage `Dockerfile` at repo root — `mcr.microsoft.com/dotnet/sdk:10.0` build stage → `mcr.microsoft.com/dotnet/aspnet:10.0` runtime stage. Builds `CookBot.Web` into `/app`. `ENTRYPOINT ["dotnet", "CookBot.Web.dll"]`.
- [ ] **PROD-02**: `docker-compose.yml` at repo root — exposes port 7000 (or `${COOKBOT_PORT:-7000}`); named volumes for `/data` (covers `cookbot.db` + key ring via `PersistKeysToDbContext`) and `/uploads` (mounts `wwwroot/uploads/`). `restart: unless-stopped`; explicit env vars set in compose file for `ASPNETCORE_URLS`, `ConnectionStrings__DefaultConnection`, etc.
- [ ] **PROD-03**: Container listens on `0.0.0.0:7000` via `ASPNETCORE_URLS=http://0.0.0.0:7000` in `Dockerfile` ENV (per PITFALLS M4 — default localhost binding is unreachable from outside the container).
- [ ] **PROD-04**: SQLite WAL mode safe for volume-mount — `cookbot.db-wal` and `cookbot.db-shm` live in the same `/data` volume; `DatabaseSeeder` verifies write access at startup (per PITFALLS M5).
- [ ] **PROD-05**: Forward-only migration runs at first container start via existing `DatabaseSeeder.SeedAsync` → `MigrateAsync()` — container start must NOT mask migration failures. PROD-05 adds an explicit health-check route `/healthz` that fails fast if migrations error, surfacing the failure instead of being masked by `restart: unless-stopped` (per PITFALLS M6).

#### Encrypt-at-rest for `UserProfile.AiApiKey`

- [ ] **PROD-06**: `Microsoft.AspNetCore.DataProtection.EntityFrameworkCore 10.0.8` added to `CookBot.Infrastructure`. `CookBotDbContext` implements `IDataProtectionKeyContext` with `DbSet<DataProtectionKey>`. EF migration `AddDataProtectionKeys` creates the table.
- [ ] **PROD-07**: `Program.cs` registration: `builder.Services.AddDataProtection().SetApplicationName("FreelovesCookBot").PersistKeysToDbContext<CookBotDbContext>()`. Key ring lives in `cookbot.db`, colocated with the data it protects — one persistent volume backs up the whole story.
- [ ] **PROD-08**: AI key encrypt path — EF Core `ValueConverter<string, string>` on `UserProfile.AiApiKey` calls `IDataProtector.Protect`/`Unprotect` via a single shared scope (`CreateProtector("AiApiKey.v1")`), NOT per-user (per PITFALLS C2 — sharing breaks under per-user scoping). The column stays `string?`; the encrypted blob replaces plaintext.
- [ ] **PROD-09**: One-time plaintext-to-encrypted upgrade pass in `DatabaseSeeder.SeedAsync` — detects plaintext rows by sentinel prefix (`CfDJ8...` = Data Protection ciphertext per ARCHITECTURE finding; plaintext lacks that prefix) and re-protects them. Idempotent: re-running the seeder on already-encrypted rows is a no-op (per PITFALLS C3).
- [ ] **PROD-10**: `SecretRedactor` (AI-07) extended to cover the decrypt path — exception messages from `Unprotect` failures must NOT leak any portion of the ciphertext or plaintext (per PITFALLS C4).
- [ ] **PROD-11**: AI key sharing still works under encryption — `AiApiKeyShareService` rows remain owner-ID references; `AiApiKeyResolutionService` decrypts the owner's key at resolution time using the shared protector scope. Recipient still never sees the plaintext key. Integration test covers the share-then-resolve round-trip.

#### Token-cost telemetry

- [ ] **PROD-12**: `AnthropicAiService` SSE parsing captures `message_start.message.usage.input_tokens` + cumulative `message_delta.usage.output_tokens` per the official Anthropic streaming docs. (Anthropic Usage Admin API NOT used — requires org-level `sk-ant-admin...` key unavailable to individual self-hosters.)
- [ ] **PROD-13**: `StructuredResult<T>` record gains `int InputTokens`, `int OutputTokens` fields with `= 0` defaults (backward-compatible interface). Surfaced through `IAiRecipeGenerator.GenerateAsync` return.
- [ ] **PROD-14**: New `AiUsageLog` entity: `(Id, UserId, KeyOwnerId, ModelName, InputTokens, OutputTokens, EstimatedCostUsd, IsRetryAttempt, Timestamp)`. EF migration `AddAiUsageLog` adds composite index on `(KeyOwnerId, Timestamp DESC)` for Profile-widget queries (per PITFALLS M8).
- [ ] **PROD-15**: Each AI call writes one `AiUsageLog` row from `AiChat.razor` / `AiRecipeGenerator` after generation completes. The 2-retry repair loop writes a row per attempt with `IsRetryAttempt = true` for retries — aggregation queries exclude or surface retries separately so the repair loop doesn't silently double-count (per PITFALLS H9).
- [ ] **PROD-16**: Per-model pricing table in `appsettings.json` (NOT hardcoded). Schema: `{ "AiPricing": { "claude-haiku-4-5-20251001": { "InputTokensPerMillionUsd": 0.80, "OutputTokensPerMillionUsd": 4.00 }, ... } }`. Profile widget displays a "Pricing as of 2026-05" footnote (per PITFALLS H10 — stale pricing). Self-hosters update the table when Anthropic raises prices.
- [ ] **PROD-17**: Profile widget — per-user "AI usage" card showing rolling 30-day input/output tokens + estimated cost in USD, sourced from `AiUsageLog` aggregated by `KeyOwnerId`. Cross-user privacy note: in trusted-LAN mode, the key-owner can see who burned their credits (`UserId` != `KeyOwnerId` rows); documented in README (per PITFALLS M9).

#### README + deploy docs

- [ ] **PROD-18**: README rewrite — new "Install" section covering both `docker compose up` quickstart and the existing `./run.sh` local-dev path. Notes the `7000` default port, `cookbot.db` + `uploads/` volume locations, first-run UX (no AI key required to start — AI features gracefully degrade per the `AiFeaturesEnabled` + `AiEnabled` gates).
- [ ] **PROD-19**: README "Configuration" section — documents env-var override pattern for all `CookBotSettings`, `ConnectionStrings__DefaultConnection`, `ASPNETCORE_URLS`, `AiPricing` table.
- [ ] **PROD-20**: README "Backup & restore" section — explicit instructions for self-hosters: stop the container, snapshot/copy BOTH volumes (`cookbot.db` data + `uploads/`), restart. Notes the WAL files. Notes that the Data Protection key ring is inside `cookbot.db`; losing the DB also loses the ability to decrypt the AI keys (which is the correct trust model — there's no second copy of the keys anywhere).
- [ ] **PROD-21**: README "Upgrade" section — `docker compose pull && docker compose up -d` for container upgrades. Migrations auto-apply at startup; `IDatabaseBackupService` writes `cookbot.db.pre-{MigrationName}.bak` before each EF migration runs. Downgrading is unsupported (migrations are forward-only).

---

## Future Requirements (deferred to v1.4+)

These items are explicitly deferred — included here so the carry-forward picture is auditable at v1.3 close.

- **Format extensions** — substitutions / equipment / doneness cues / source provenance (`FUTURE-03..06`). Further format fields beyond v3's photo+description+temperature.
- **Schema.org Recipe / Cooklang one-way export** (`FUTURE-07`, `FUTURE-11`). Export interop.
- **USDA FoodData Central nutrition computation** (`FUTURE-08`). Auto-derive nutrition.
- **Tool-use fallback for structured-output regressions** (`FUTURE-09`). Defensive fallback if Anthropic Structured Outputs ever regresses.
- **Per-sharer cookbook-import consent banner** (`FUTURE-12`). UX-visible consent affordance.
- **Backfilling photos for existing recipes** — v3 migration leaves `PhotoUrl = null` for all existing rows.
- **Multiple photos per recipe / photo gallery** — v3 ships single hero photo only.
- **Reverse-image search ("find a photo for this recipe" AI feature)** — separate AI-feature scope.
- **UI backup button** — FEATURES research rated P3; deferred. v1.3 ships `volumes + README` only (per user gap-answer 2026-05-15).
- **Expiration-aware pantry-match scoring** — FEATURES research deemed anti-feature at this scope (users won't maintain expiration dates). Deferred until a use-by-date capture workflow exists.
- **TLS / HTTPS cert hardening** — v1.3 deploy guide recommends reverse proxy (nginx/Caddy). Internal HTTPS / cert rotation is OOS for trusted-LAN posture.

---

## Out of Scope

Explicit boundaries with reasoning. Some are absolute project-level OOS (carried from PROJECT.md); some are v1.3-specific OOS to keep scope contained.

**Architecture (project-level):**

- Web API / SPA / WebAssembly client — Blazor Server stays.
- Multi-tenant SaaS hosting — trusted-LAN self-host only; CookBotSettings.AuthMode is reserved-unused.
- AI providers other than Anthropic — `IAiService` is the abstraction; new provider is a separate milestone.
- Postgres / non-SQLite databases — SQLite is sufficient at single-host scale.
- Identity middleware / OAuth / SSO — trusted-LAN posture deferred.
- `Microsoft.Extensions.AI` migration — enforced anti-pattern.
- Official Anthropic NuGet (`Anthropic` package) — enforced anti-pattern.
- `Newtonsoft.Json` — System.Text.Json everywhere.
- `NJsonSchema` — `JsonSchema.Net` for runtime validation only.
- MudBlazor reintroduction — stripped wholesale in v1.2; staying out.

**v1.3-specific:**

- **Multiple photos per recipe / gallery** — v3 schema reserves a single `PhotoUrl` field; multi-photo is a v1.4+ schema decision.
- **EXIF / metadata stripping on uploaded images** — files are stored as uploaded; v1.3 doesn't process pixel data.
- **CDN integration / image proxying for paste-URL** — browser fetches direct from photo host (the `referrerpolicy="no-referrer"` is the only privacy mitigation).
- **Image resizing / thumbnail generation** — browser handles via CSS `object-fit: cover`. No `ImageSharp` (GPL-3.0 incompatible per STACK research), no `Magick.NET`.
- **File upload to S3 / R2 / Azure Blob** — local-disk only for v1.3; cloud-storage is a future concern.
- **Per-key-owner billing / quotas** — telemetry is read-only display; no enforcement of spending caps.
- **Cross-currency cost display** — `EstimatedCostUsd` is USD only.
- **TLS/HTTPS termination inside the container** — defer to reverse proxy (nginx/Caddy) in the deploy guide.
- **Identity middleware activation via `CookBotSettings.AuthMode`** — `AuthMode` flag remains reserved-unused in v1.3.
- **AI key rotation UX** — `IDataProtector` key ring auto-rotates per .NET 10 defaults; user-facing rotation UI is a future concern.
- **UI backup/restore button** — deferred to v1.4+ per user gap-answer 2026-05-15; v1.3 ships docs-only.
- **Cookbook cover photo** — distinct from recipe photo; separate field on `Cookbook` if pursued.

---

## Traceability

Filled by the roadmapper after `/gsd-new-milestone` completes — updated 2026-05-15 by roadmapper.

| REQ-ID | Phase | Plan | Status |
|--------|-------|------|--------|
| SCHEMA-01 | Phase 8 | TBD | Pending |
| SCHEMA-02 | Phase 8 | TBD | Pending |
| SCHEMA-03 | Phase 8 | TBD | Pending |
| SCHEMA-04 | Phase 8 | TBD | Pending |
| SCHEMA-05 | Phase 8 | TBD | Pending |
| SCHEMA-06 | Phase 8 | TBD | Pending |
| SCHEMA-07 | Phase 8 | TBD | Pending |
| SCHEMA-08 | Phase 8 | TBD | Pending |
| SCHEMA-09 | Phase 8 | TBD | Pending |
| SCHEMA-10 | Phase 8 | TBD | Pending |
| SCHEMA-11 | Phase 8 | TBD | Pending |
| SCHEMA-12 | Phase 8 | TBD | Pending |
| CLEAN-01 | Phase 8 | TBD | Pending |
| CLEAN-02 | Phase 8 | TBD | Pending |
| CLEAN-03 | Phase 8 | TBD | Pending |
| CLEAN-04 | Phase 8 | TBD | Pending |
| PHOTO-01 | Phase 9 | TBD | Pending |
| PHOTO-02 | Phase 9 | TBD | Pending |
| PHOTO-03 | Phase 9 | TBD | Pending |
| PHOTO-04 | Phase 9 | TBD | Pending |
| PHOTO-05 | Phase 9 | TBD | Pending |
| PHOTO-06 | Phase 9 | TBD | Pending |
| PHOTO-07 | Phase 9 | TBD | Pending |
| PHOTO-08 | Phase 9 | TBD | Pending |
| PHOTO-09 | Phase 9 | TBD | Pending |
| PHOTO-10 | Phase 9 | TBD | Pending |
| PHOTO-11 | Phase 9 | TBD | Pending |
| PHOTO-12 | Phase 9 | TBD | Pending |
| PHOTO-13 | Phase 9 | TBD | Pending |
| PHOTO-14 | Phase 9 | TBD | Pending |
| PROD-01 | Phase 9 | TBD | Pending |
| PROD-02 | Phase 9 | TBD | Pending |
| PROD-03 | Phase 9 | TBD | Pending |
| PROD-04 | Phase 9 | TBD | Pending |
| PROD-05 | Phase 9 | TBD | Pending |
| PROD-06 | Phase 9 | TBD | Pending |
| PROD-07 | Phase 9 | TBD | Pending |
| PROD-08 | Phase 9 | TBD | Pending |
| PROD-09 | Phase 9 | TBD | Pending |
| PROD-10 | Phase 9 | TBD | Pending |
| PROD-11 | Phase 9 | TBD | Pending |
| PROD-12 | Phase 9 | TBD | Pending |
| PROD-13 | Phase 9 | TBD | Pending |
| PROD-14 | Phase 9 | TBD | Pending |
| PROD-15 | Phase 9 | TBD | Pending |
| PROD-16 | Phase 9 | TBD | Pending |
| PROD-17 | Phase 9 | TBD | Pending |
| PROD-18 | Phase 9 | TBD | Pending |
| PROD-19 | Phase 9 | TBD | Pending |
| PROD-20 | Phase 9 | TBD | Pending |
| PROD-21 | Phase 9 | TBD | Pending |
| QOL-01 | Phase 10 | TBD | Pending |
| QOL-02 | Phase 10 | TBD | Pending |
| QOL-03 | Phase 10 | TBD | Pending |
| QOL-04 | Phase 10 | TBD | Pending |
| QOL-05 | Phase 10 | TBD | Pending |
| QOL-06 | Phase 10 | TBD | Pending |
| QOL-07 | Phase 10 | TBD | Pending |
| POLISH-01 | Phase 10 | TBD | Pending |
| POLISH-02 | Phase 10 | TBD | Pending |
| POLISH-03 | Phase 10 | TBD | Pending |
| POLISH-04 | Phase 10 | TBD | Pending |
| POLISH-05 | Phase 10 | TBD | Pending |

---

## Hard invariants carried forward (re-stated for v1.3 reviewers)

These existed in v1.1 + v1.2 and must NOT regress in v1.3:

1. **Canonical-first reads** — UI surfaces consume `RecipeDocument` directly via `Recipe.CanonicalDocumentJson` + `JsonRecipeSerializer`. Never read `Recipe.IngredientsJson` / `StepsJson` / `IngredientRefs` / `TagsJson` from new code. (`TagsJson` is retained through CLEAN-02 as a deletion target only.)
2. **No auto-rewrite on save** — Step text is never modified by the save path. Explicit chips are the only persisted source of timers and ingredient links.
3. **AI structured-output orchestrator** — `IAiRecipeGenerator` + `SecretRedactor` + `PromptInjectionGuard` preserved verbatim. UI consumes them; do not bypass.
4. **Three-tier extractor stays deleted** — POLISH-01 invariant. `AiChat.ExtractRecipeContent` is permanently gone. PHOTO-12 surfaces `_lastStructuredRecipe.Value.PhotoUrl` directly — no extractor revival.
5. **AI-off contract** — host kill switch `CookBotSettings.AiFeaturesEnabled` AND per-user `UserProfile.AiEnabled` must both be true; gating enforced inside application/data services, not by middleware. PROD-12..17 telemetry writes ONLY when both gates are open.
6. **Trusted-LAN auth posture** — `CookBotSettings.AuthMode` reserved for future use; no Identity middleware in v1.3.
7. **MudBlazor stays out** — repo-wide `grep "Mud[A-Z]"` returns zero hits; v1.3 introduces nothing that would change that.
8. **System.Text.Json only** — no Newtonsoft.Json; no NJsonSchema.

---

*Authored 2026-05-15 by `/gsd-new-milestone v1.3`. Sources: PROJECT.md, `.planning/research/SUMMARY.md` + 4 detail files (STACK/FEATURES/ARCHITECTURE/PITFALLS, commit 9350301), `.planning/v1.3-PHASE-CANDIDATE-recipe-photos.md`, v1.2 audit `tech_debt`, v1.1 carry-forwards. User-confirmed scoping rounds 2026-05-15.*
