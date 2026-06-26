# FreelovesCookBot

## What This Is

Self-hosted Blazor Server cooking and baking tracker that lets a small group of trusted users author, scale, and cook recipes — with first-class AI assistance for generating and refining them via Anthropic Claude (or any external LLM through a copyable prompt). It pairs a structured recipe format with a step-by-step cooking flow (timers, browser notifications, ingredient highlighting), pantry/grocery features, offline nutrition estimates, multi-photo recipe galleries, Schema.org/Cooklang export, and cookbook export/import/sharing in a single SQLite-backed .NET 10 app.

## Core Value

A durable home for the recipes the user actually cooks, captured in **one standardized format** that round-trips cleanly between AI generation, manual editing, cooking mode, and import/export — without the user (or the AI) having to know special syntax.

## Current State

**Shipped milestones:** **v1.4 Recipe Data & Interoperability (2026-06-25, tag `v1.4`)** — Phases 12–16, 18 plans (richer v4 format, Schema.org/Cooklang export, photo galleries, offline CNF nutrition). v1.3 Production-Ready & Format Maturity (2026-06-05, tag `v1.3`) — Phases 8–11, 39 plans. v1.2 UI Redesign (2026-04-27, tag `v1.2`). Earlier: v1.0 (pre-GSD) + v1.1 Phases 1+2 (Canonical Format + AI Structured Output, 2026-04-25/26, no tag — rolled into v1.2 release).

**App today (post-Phase 10):** Self-hosted Blazor Server cooking & baking tracker on .NET 10 + SQLite. Custom Razor component system (no MudBlazor) matching the Claude Design handoff — warm cream / cocoa ink / dialed-back orange accent (terracotta + sage variants now selectable), Inter typography, custom outline icons, striped photo placeholders, tabular numerals across 9 surfaces. AI-assisted recipe generation via Anthropic Claude using token-level constrained-decoding structured output, 2-retry repair loop, secret redaction, prompt-injection defense, optional per-user custom system-prompt template, raw-edit fallback when JSON parsing fails, and a 30-day AI usage widget (tokens + cost) on Profile. Multi-user with optional password (PBKDF2-HMAC-SHA256), session-scoped current user, trusted-LAN posture. Canonical versioned `RecipeDocument` round-trips through AI generation, manual editing, cooking mode, JSON export, and `.cookbook.json` import. Smart pantry-match algorithm (D-44 exp-decay × D-45 dietary filter × D-46 configurable knobs) drives Home dashboard suggestions.

**v1.3 SHIPPED (2026-06-05, tag `v1.3`).** Phases 8 (Format Foundation), 9 (Photos + Prod-Ready Infrastructure), 10 (QOL, Polish & Consumer Surfaces), and 11 (UAT Cleanup + Automated UAT Harness) all complete and verified. Phase 10 UAT is fully green (6 pass; Test 4 honest validation-fail deferral). Phase 11 fixed the four UAT-surfaced backlog items (999.2–999.5) and shipped a reusable Playwright browser-UAT harness at `tests/uat-harness/` (`npm test`).

**v1.4 SHIPPED (2026-06-25, tag `v1.4`).** Phases 12 (Richer Format v3→v4), 13 (Export & Interoperability — Schema.org JSON-LD + Cooklang one-way export), 14 (Photo Gallery), 15 (Nutrition — offline Canadian Nutrient File), 16 (UAT + Integration). 18 plans complete + human UAT (Phase 14 10/10, Phase 15 14/15 with the responsive-grid item fixed); the GALLERY-04 AI photo-search-term helper was retired after UAT. UATAUTO-02 partial (core flows automated; format-fields-visible + gallery-upload deferred — Blazor `<InputFile>` not Playwright-drivable). Next milestone: **v1.5 External Agent Interface (MCP + REST API)** via `/gsd-new-milestone`.

## Current Milestone: v1.5 External Agent Interface (MCP + REST API)

**Goal:** Let external AI agents manage the pantry and create recipes through a secured, headless interface — exposed as both a REST API and an MCP server — without weakening the trusted-LAN posture or the canonical-format guarantees.

**Target features:**
- **Agent-operations facade** — one internal Application-layer surface over the existing `PantryService` + `RecipeService`, so the REST API and MCP server share identical logic and authorization (no duplicated business rules).
- **Pantry management** — list the acting user's accessible pantries, list items, add/update an item, deduct an item, and resolve an ingredient name → id; all scoped by the existing in-service ownership checks.
- **Structured recipe creation** — an agent submits a canonical `RecipeDocument`; it is schema-validated (`JsonSchema.Net`), converted to `ParsedRecipe`, and persisted via `RecipeService.CreateAsync`. No inbound Anthropic/AI call on creation.
- **Per-agent token auth** — a bearer token mapped to a real user (token-hash table + resolution middleware that establishes the acting user before any service call); deliberately NOT ASP.NET Identity / OAuth / SSO.
- **MCP server** — an in-process MCP server (HTTP/SSE transport, likely the `ModelContextProtocol` C# SDK) exposing the facade as MCP tools.
- **REST minimal-API** — the first HTTP endpoints in the app (today it is 100% Blazor Server / SignalR with zero controllers), wired into the existing Kestrel host in `Program.cs`.

**Key context:**
- **Scoped exception to the no-auth invariant:** v1.5 adds per-agent token auth. The trusted-LAN posture is preserved (no Identity/OAuth, no *required* public exposure), but headless write access is a genuinely new threat surface — authz, SSRF, secret handling, and abuse posture need a dedicated security pass.
- **New HTTP surface:** the app has no controllers/minimal-API endpoints today; v1.5 introduces minimal-API routes plus an MCP transport on the existing host. The SPA/WASM client stays out of scope — only a headless agent-facing surface is added.
- **Likely the first new NuGet since v1.2** (`ModelContextProtocol`): to be justified during research/planning given the dependency-averse posture. The REST side stays on ASP.NET minimal APIs + System.Text.Json.
- **Canonical-format guarantees hold:** structured-submit reuses the existing `RecipeDocument` → schema-validate → `ParsedRecipe` → `RecipeService.CreateAsync` path; if the agent wire shape extends the canonical doc, the POCO + parser-DTO + `RecipeService` boundaries must all be updated together or fields silently drop.
- Numbering continues — v1.5 phases start after **Phase 16**.

## Shipped Milestone: v1.4 Recipe Data & Interoperability ✅ (2026-06-25, tag `v1.4`; archived → `milestones/v1.4-ROADMAP.md`)

**Goal:** Make recipes richer and portable — extend the canonical v3 schema with the deferred format fields, compute nutrition automatically, export to external standards, and grow photos beyond a single hero — without breaking the v3 round-trip or the trusted-LAN posture.

**Target features (4 themes):**
- **Richer recipe format** — ingredient substitutions, equipment list, per-step doneness cues, source/provenance fields (FUTURE-03..06); a schema bump on top of v3 (likely v4) with a per-field upcaster, AI-prompt schema update, parser + validator coverage.
- **Export & interoperability** — Schema.org Recipe (JSON-LD) markup for SEO/rich results + Cooklang one-way export (FUTURE-07, FUTURE-11); export-only, no import round-trip required.
- **Nutrition** — auto-compute calories + macros from ingredient amounts via the **Canadian Nutrient File (CNF)**, Health Canada's offline food-composition DB (FUTURE-08); per-recipe and per-serving panels; handle unmatched/ambiguous ingredients gracefully. (Data source switched from USDA FDC → CNF on 2026-06-07 — user is Canadian; see Key Decisions.)
- **Photo enhancements** — multiple photos / gallery per recipe + backfill for existing recipes; optional reverse-image AI "find a photo for this recipe" (builds on v1.3's single hero photo + upload/paste pipeline).

**Key context:**
- Additive milestone — **no breaking changes**; a richer-format schema bump rides the existing upcaster chain (v3→v4) the same way v2→v3 did. Canonical-first reads and display-only layering invariants from v1.3 carry forward.
- Two themes lean on external specs/APIs to get right: **Canadian Nutrient File / CNF** (data source, matching strategy, OGL-Canada licensing + attribution), **Schema.org Recipe** (JSON-LD shape, Google rich-results requirements), **Cooklang** (export grammar). Research-first was chosen for these.
- Trusted-LAN posture preserved; nutrition/export are local computations or static markup, not new public endpoints. Numbering continues — v1.4 phases start at **Phase 12**.
- The reusable Playwright UAT harness (`tests/uat-harness/`) shipped in v1.3 — reuse it for v1.4 UAT.

## Shipped Milestone: v1.3 Production-Ready & Format Maturity ✅ (archived → `milestones/v1.3-ROADMAP.md`)

**Goal:** Make CookBot shippable for other self-hosters while landing the deferred format/QOL/polish work — one v3 schema bump carries photos + description + per-step temperature, a new prod-ready track ships Docker + encryption + telemetry + deploy docs, and the v1.2 carry-forward tech-debt list closes.

**Target features (5 buckets):**

- **Schema v3 + Photos** — `Recipe.PhotoUrl` with file upload AND paste-URL (single hero photo, `wwwroot/uploads/`), `Recipe.Description` (closes D-25), per-step temperature (closes FUTURE-V1.1-01), V2→V3 upcaster bundles all three, AI prompt schema update, scheme-allowlist + onerror fallback
- **Format cleanup** — `LegacyRecipeProjector` deletion (FUTURE-V1.1-03), `TagsJson` → relational `RecipeTag` (FUTURE-V1.1-02), prompt-snapshot regression test (FUTURE-V1.1-04), README "Recipe Format" section (FUTURE-V1.1-05)
- **QOL** — smart pantry-match algorithm (FUTURE-13, replaces deterministic stub), AiChat "Edit anyway" hardening (FUTURE-15), accent variant picker (FUTURE-14 — terracotta/sage user-facing), Profile-side AI prompt editor (DEFERRED-PROF-AIPROMPT)
- **Small-stuff polish** — Cookbook reparenting on edit (D-26), Pantry per-row quick-add (D-37), moon glyph for dark-mode toggle (D-15), TopBar RightSlot passthrough (D-16), Home active-timer live JS tick
- **Prod-ready (self-hosters)** — Dockerfile + docker-compose with persistent volumes for `cookbot.db` + `uploads/`, encrypt-at-rest for `UserProfile.AiApiKey` (FUTURE-01), per-key-owner token-cost telemetry (FUTURE-02), README install/config/backup/upgrade sections, no-AI-key first-run flow

**Key context:**
- "Self-hostable" means *runnable by others* (Docker, deploy docs, sane defaults), NOT *internet-exposed* — trusted-LAN auth posture is preserved. No Identity middleware, no OAuth, no rate-limit hardening for public access.
- Schema bumps: photos + description + per-step temperature bundle into a single V2→V3 upcaster step. One AI-prompt regression pass amortizes across all three additions.
- File upload pipeline is in: `wwwroot/uploads/` with size cap + content-type validation. Paste-URL coexists, with scheme allowlist (`http`/`https` only — defangs `javascript:`/`data:`).
- The candidate doc `.planning/v1.3-PHASE-CANDIDATE-recipe-photos.md` is starting material; IMG-01..13 will be refined and the "paste-URL only" bright line flipped during requirements drafting.
- Numbering continues from v1.2 — v1.3 phases start at **Phase 8**.

## Requirements

### Validated

<!-- Shipped and confirmed valuable. v1.0 (pre-GSD existing features) + v1.1 Phases 1+2 + v1.2. -->

**v1.0 (pre-GSD existing app):**
- ✓ Recipe authoring — manual editor with ingredient autocomplete and step composer (`src/CookBot.Web/Components/Pages/RecipeEditor.razor`)
- ✓ Multi-format paste-in — YAML frontmatter, numbered lines, or free-form, parsed by `IRecipeFormatParser` (`src/CookBot.Application/Services/RecipeFormatParser.cs`)
- ✓ Step-by-step cooking mode with countdown timers, browser notifications, and ingredient highlighting (`src/CookBot.Web/Components/Pages/CookingMode.razor`, `src/CookBot.Web/wwwroot/js/cooking-timers.js`)
- ✓ Recipe scaling with fraction display (`src/CookBot.Application/Services/RecipeScalingService.cs`, `FractionFormatter.cs`)
- ✓ Cookbook organization — group recipes into cookbooks, view/edit
- ✓ Cookbook export/import as JSON (`src/CookBot.Web/Services/CookbookTransferService.cs`)
- ✓ Cookbook PDF export (`src/CookBot.Web/Services/CookbookPdfService.cs`, QuestPDF)
- ✓ Cookbook sharing between users (`CookbookShare` entity)
- ✓ Pantry tracking with AI-assisted population (`PantryAiPopulationService`, `PantryView.razor`)
- ✓ Grocery / shopping lists (`GroceryListService`, `GroceryListView.razor`)
- ✓ AI chat (Anthropic) for recipe generation, streaming SSE (`AnthropicAiService`, `AiChat.razor`)
- ✓ Per-step "Ask about this step" assist in cooking mode (`RecipeCookingAiContext`)
- ✓ Prompt builder — copyable system prompt for external LLM use (`PromptBuilder.razor`)
- ✓ Per-user API key storage + share table (recipient never sees the key) (`AiApiKeyResolutionService`, `AiApiKeyShareService`)
- ✓ Multi-user with optional password (PBKDF2-HMAC-SHA256), session-scoped current user (`CurrentUserService`)
- ✓ Authorization hardening — ownership and share checks on every recipe/cookbook mutation (`RecipeAccessExtensions`, `RecipeService`, `CookbookService`)
- ✓ AI kill switches — host-wide (`CookBotSettings.AiFeaturesEnabled`) and per-user (`UserProfile.AiEnabled`)
- ✓ 600+ ingredient seed database with autocomplete (`seeds/ingredients.json`, `DatabaseSeeder.cs`)
- ✓ Dark mode toggle persisted to `localStorage`
- ✓ Auto-applied EF Core migrations on startup (`DatabaseSeeder.SeedAsync` → `MigrateAsync`)

**v1.1 (partial — Phases 1+2 shipped 2026-04-25/26):**
- ✓ Single canonical, versioned `RecipeDocument` — one schema is the source of truth across YAML wire, JSON export, DB column, and AI prompt; `version` field supports forward-compatible evolution (`CookBot.Domain/Recipes/RecipeDocument.cs`) — v1.1 Phase 1
- ✓ AI chat reliably emits the canonical format — Anthropic structured-output transport (`SendStructuredAsync<T>`) via `output_config.format`; 2-retry repair loop; `SecretRedactor` strips API keys from error surfaces; `PromptInjectionGuard` wraps recipe content in `<recipe>` tags; `Markdig DisableHtml` lockdown (AI-08-AUDIT) — v1.1 Phase 2
- ✓ Auto-migrated existing data — `IDatabaseBackupService` (last-3-backup retention) + `LegacyRecipeProjector` + EF migration backfilling `Recipe.CanonicalDocumentJson` for every existing row — v1.1 Phase 1

**v1.2 UI Redesign (shipped 2026-04-27, all 75 reqs):**
- ✓ UI redesign — replace MudBlazor wholesale with custom Razor component system matching the Claude Design handoff (`wwwroot/css/cookbot-design.css` + 17 atom components + 36 outline icons + Cb dialog/toast/dropdown primitives) — v1.2 Phase 5 (DS-01..06, ATOM-01..10, SHELL-01..04, DIALOG-01..04)
- ✓ Pantry-aware Home dashboard — "Tonight from your pantry" deterministic-stub matcher + 4-tile glance strip + recently cooked + up next cards — v1.2 Phase 6 (HOME-01..04)
- ✓ Adaptive Cooking Mode — tablet-optimized; 224px tabular timer hero when running / 52px step hero when idle; always-on right rail with link-only ingredient highlighting; bottom step nav with arrow-key navigation — v1.2 Phase 6 (COOK-01..06)
- ✓ Editorial Recipe View — 64px display title with hanging accent numerals; 300px sticky scaled-ingredients sidebar; "Notes from your last cook" callout sourced from `IRecipeMadeService` — v1.2 Phase 6 (RV-01..05)
- ✓ Editor chip composer in custom Razor — keyboard-navigable explicit ingredient picker (no special syntax for users); inline non-modal timer-suggestion banner (no auto-rewrite); step/section toggle with confirmation; immutable-id reorder; paste-raw-text routing through canonical schema parser — v1.2 Phase 6 (ED-01..09, absorbs v1.1 EDITOR-01..07)
- ✓ Cookbooks (list + detail) — collage thumbnails, detail hero with share/PDF/export — v1.2 Phase 7 (CB-01, CB-02)
- ✓ Pantry — 4-tile summary strip + categorized stock cards + status badges + AI populate/standardize buttons — v1.2 Phase 7 (PA-01..04)
- ✓ Grocery — mobile-first aisle-categorized sections with 24px circle checkboxes + sticky add-item button — v1.2 Phase 7 (GR-01..04)
- ✓ AI Chat as live recipe canvas — 380px chat rail + flex canvas; recipe canvas binds directly to canonical `RecipeDocument` from `IAiRecipeGenerator` (no extractor revival — POLISH-01 preserved); streaming caret + drafting pulse — v1.2 Phase 7 (AIC-01..05)
- ✓ Prompt Builder rewrite — 320px config rail + dark mono preview sourced from `RecipeSchemaDocumentationProvider` — v1.2 Phase 7 (PB-01..03)
- ✓ Profile rewrite — density toggle, equipment + dietary multi-select chip rows, AI key card — v1.2 Phase 7 (PROF-01, PROF-02)
- ✓ Accessibility audit — 2px accent focus rings, keyboard-only nav across 9 surfaces, WCAG AA contrast on warm-cream + cocoa-dark, ARIA roles on atoms, dark-mode smoke pass — v1.2 Phase 7 (A11Y-01..04)
- ✓ MudBlazor wholesale removed — repo-wide `grep "Mud[A-Z]"` returns zero hits; package + `AddMudServices()` + `@using MudBlazor` + 4 providers + static assets + `DesignSandbox` all deleted — v1.2 Phase 7 (MIG-01..03)
- ✓ RecipeMade log entity + `IRecipeMadeService` — wires RV-04 last-cook callout, Recipe View made-count, and Home recently-cooked grid — v1.2 Phase 7 post-ship slice 09

**v1.3 Production-Ready & Format Maturity (shipped 2026-06-05, tag `v1.3`):**
- ✓ Canonical `RecipeDocument` v3 — `PhotoUrl` + `Description` + per-step `Temperature` across type system, V2→V3 upcaster, EF columns, AI schema, YAML/JSON wire, and parser (SCHEMA-01..12) — v1.3 Phase 8
- ✓ Format cleanup — `LegacyRecipeProjector` deleted behind null-canonical boot guard, `TagsJson`→relational `RecipeTag`, byte-stable prompt-snapshot test (Verify.Xunit), README "Recipe Format" section (CLEAN-01..04) — v1.3 Phase 8
- ✓ Recipe hero photos — file upload + paste-URL, magic-byte validation, scheme allowlist, 12 MB limits at all three Blazor boundaries, one-shot `onerror` fallback (PHOTO-01..14) — v1.3 Phase 9
- ✓ Production-ready infra — Docker + compose with persistent volumes, encrypt-at-rest AI keys (Data Protection, idempotent re-encryption), per-call token-cost telemetry, `/healthz`, README install/config/backup/upgrade (PROD-01..21) — v1.3 Phase 9
- ✓ QOL + polish — scored pantry-match (deterministic, indexed), AiChat raw-edit recovery dialog, accent picker, Profile prompt editor + 30-day token-cost widget, cookbook reparenting, pantry quick-add, moon glyph, TopBar RightSlot, live timer tick (QOL-01..07, POLISH-01..05) — v1.3 Phase 10
- ✓ UAT cleanup + automated UAT harness — RecipeView inline-Edit clip, responsive ≤720px, sidebar Profile clip/bg, unit-system display conversion (CLEANUP-01..04) + reusable Playwright/chromium browser-UAT harness at `tests/uat-harness/` (UATAUTO-01) — v1.3 Phase 11

**v1.4 Recipe Data & Interoperability (shipped 2026-06-25, tag `v1.4`):**
- ✓ Richer recipe format — ingredient substitutions, recipe-level equipment list, per-step doneness cues, source/provenance; v3→v4 canonical schema bump + per-field upcaster + AI-prompt/snapshot update + parser/validator coverage (FORMAT-01..07) — v1.4 Phase 12
- ✓ Export & interoperability — server-rendered Schema.org `Recipe` JSON-LD (ISO-8601 durations, HTTPS-only image, tag-derived category/cuisine, no fabricated rating) + one-way Cooklang `.cook` export; both pure display-only Application projectors over `RecipeDocument` (INTEROP-01..04) — v1.4 Phase 13
- ✓ Photo gallery — `RecipePhoto` entity (ordered, captioned, one primary) + multi-upload/reorder/set-hero + RecipeView hero+strip + disk-file cleanup on delete; EF backfill from `Recipe.PhotoUrl` (GALLERY-01..03; GALLERY-04 AI helper retired after UAT) — v1.4 Phase 14
- ✓ Offline nutrition — bundled Canadian Nutrient File SQLite seed + NutritionService (CNF conversion-factor/density compute, per-recipe cache) + 5-state per-serving/total panel with coverage indicators + non-dismissable Health Canada attribution + JSON-LD nutrition wiring (NUTR-01..06) — v1.4 Phase 15
- ◐ UAT automation extended for v1.4 — `test16-integration.mjs` runs JSON-LD + Cooklang + nutrition-panel checks hands-free (UATAUTO-02, partial — format-fields-visible + gallery-upload deferred: Blazor `<InputFile>` not Playwright-drivable) — v1.4 Phase 16

### Active

<!-- v1.5 External Agent Interface — REQ-IDs land when /gsd-new-milestone authors REQUIREMENTS.md. -->

- _Planning **v1.5 — External Agent Interface (MCP + REST API)**: per-agent API-token auth (token→user, no Identity/OAuth), structured-submit recipe creation (canonical `RecipeDocument` → schema-validate → `RecipeService.CreateAsync`, no inbound AI), and a shared agent-operations facade over `PantryService`/`RecipeService` exposed as BOTH a REST minimal-API and an in-process MCP server. Requirements defined next by `/gsd-new-milestone`._

### Carry-forward (deferred to v1.5+)

<!-- v1.4 promoted FUTURE-03..08 / FUTURE-11 + photo backfill / multiple-photos / reverse-image into the Active milestone (richer format + export + nutrition + photos). Items below remain deferred. -->

- **Tool-use fallback for structured-output regressions** (`FUTURE-09`) — defensive fallback if Anthropic Structured Outputs regresses
- **Per-sharer cookbook-import consent banner** (`FUTURE-12`) — UX-visible consent affordance for shared imports (AI-08-AUDIT Markdig lockdown is the technical mitigation)

### Out of Scope

<!-- Explicit boundaries with reasoning. -->

- ~~Web API / SPA / WebAssembly client~~ — **Partially reversed for v1.5:** a minimal headless **REST API + in-process MCP server** (read/write pantry + structured recipe submit, per-agent token auth) is the v1.5 External Agent Interface milestone — there is now a driver (external AI agents managing the pantry / creating recipes). The **SPA / WebAssembly client** remains out of scope: Blazor Server `InteractiveServer` stays the UI architecture; v1.5 adds only a headless agent-facing API/MCP surface, not a browser SPA.
- Multi-tenant SaaS hosting — designed for self-hosting on a trusted LAN (`README.md`); auth is intentionally minimal
- AI providers other than Anthropic — `IAiService` is implemented only by `AnthropicAiService`; adding OpenAI/Gemini is a separate scope item the user has not asked for
- ~~Containerization assets (Dockerfile, compose)~~ — **In scope as of v1.3** (Prod-ready bucket). The previous "not requested" rationale no longer holds: v1.3's self-hostable goal requires a reproducible deploy surface beyond `run.sh`.
- CI/CD — no `.github/` workflows today; out of scope unless it appears in requirements
- Postgres / non-SQLite databases — current scale is single-host, single-user-group; SQLite is sufficient
- Identity middleware / OAuth / SSO — `CookBotSettings.AuthMode` is reserved for future hardening; not in this milestone
- Rewriting the cookbook export JSON DTO into a different exchange format — the *recipe* schema is in scope, but `CookbookTransferDocument`'s outer envelope (cookbook metadata + recipes array + `SourceApp`) stays; only the recipe shape inside it changes to match the canonical format

## Context

**Origin:** The author built this for personal use because online recipe sites are ad-laden and LLM-generated recipes had nowhere to live. The `README.md` notes the app is "completely vibecoded with Claude Opus 4.6" and is shared in case it's useful to others.

**Current footprint (post-v1.2):**
- 4-project Clean/Onion architecture (`Domain`, `Application`, `Infrastructure`, `Web`) plus a Tests project (xUnit 2.9.2 + bUnit)
- 9 routable Blazor Server surfaces on the custom Razor component system + 17 Cb atom components + 36 outline icons + Cb dialog/toast/dropdown primitives
- **Zero MudBlazor dependency** — stripped wholesale in v1.2 Phase 7 / Plan 07-07
- 196 / 196 tests passing under `dotnet test --filter "Category!=RequiresApiKey"`; live-AI theory tests gated under `RequiresApiKey`
- EF Core 10 migrations forward-only through the v1.1 + v1.2 work, applied by `DatabaseSeeder.SeedAsync` at startup with `IDatabaseBackupService` retention
- License: `GPL-3.0-only`

**Format situation (post-v1.1 Phase 1):** The canonical `RecipeDocument` (`CookBot.Domain/Recipes/RecipeDocument.cs`) is the single source of truth across YAML wire format, JSON export, the DB JSON column (`Recipe.CanonicalDocumentJson`), and the AI prompt. `RecipeUpcasterChain` reconciles v1 → v2; `RecipeJsonSchemaProvider` advertises the schema to Anthropic; `LegacyRecipeProjector` is retained as a deletion-target for v1.3+ (FUTURE-V1.1-03).

**AI conformance situation (post-v1.1 Phase 2):**
- Anthropic structured-output via `output_config.format` — token-level constrained decoding — replaces the old free-form prompt-and-parse pattern
- 2-retry repair loop bounds validation failures
- `SecretRedactor` (AI-07) strips API keys from error surfaces; `PromptInjectionGuard` (AI-08) wraps user/shared content in `<recipe>` tags; `Markdig DisableHtml` lockdown (AI-08-AUDIT) blocks HTML injection in rendered AI output
- The legacy three-tier `AiChat.ExtractRecipeContent` extractor is permanently deleted (POLISH-01 invariant — preserved through v1.2 AI Chat rewrite)
- The opt-out clause in `PromptBuilderService` is gone; a lint denylist prevents regression

**Outstanding concerns:**
- AI key encrypt-at-rest (FUTURE-01) — keys are stored as plaintext today; trusted-LAN posture tolerates this for now
- Token-cost telemetry per key owner (FUTURE-02)
- Per-sharer cookbook-import consent banner (FUTURE-12) — AI-08-AUDIT Markdig lockdown is the technical replacement for AI-09 but a UX-visible consent affordance remains deferred

## Constraints

- **Tech stack:** .NET 10 / Blazor Server (`InteractiveServer`) / SQLite via EF Core 10 / custom Razor component system (no MudBlazor as of v1.2) — Changing the platform is not a milestone goal.
- **AI provider:** Anthropic Claude only (`AnthropicAiService`, models Haiku 4.5 / Sonnet 4.6 / Opus 4.7) — `IAiService` is the abstraction if a second provider ever lands.
- **Persistence:** SQLite single-file (`cookbot.db`) — Self-host friendly; migrations applied at startup by `DatabaseSeeder.SeedAsync`.
- **Auth posture:** Trusted-LAN self-hosting; no Identity middleware — `CookBotSettings.AuthMode` exists but is "Reserved for future use; not enforced".
- **License:** GPL-3.0-only — All deps must be license-compatible.
- **Backward compatibility:** Existing `.cookbook.json` exports out in the wild must remain importable; the `version` field on the canonical `RecipeDocument` is the migration path. `RecipeUpcasterChain` reconciles V1 → V2.
- **Canonical format invariants:** UI surfaces consume `RecipeDocument` directly via `Recipe.CanonicalDocumentJson` + `JsonRecipeSerializer`. **Never** read `Recipe.IngredientsJson` / `StepsJson` / `IngredientRefs` / `TagsJson` from new code (deletion-targets retained for upcaster). **Never** auto-rewrite step text on save — explicit chips are the only persisted source of timers/ingredient links.
- **AI invariants:** The structured-output orchestrator (`IAiRecipeGenerator`) + `SecretRedactor` + `PromptInjectionGuard` are preserved verbatim — UI consumes them; do not bypass. The three-tier `AiChat.ExtractRecipeContent` extractor stays deleted (POLISH-01).
- **No second AI provider abstraction; no `Microsoft.Extensions.AI` migration; no `Newtonsoft.Json`; no `NJsonSchema` — these are tracked as explicit out-of-scope.**

## Key Decisions

| Decision | Rationale | Outcome |
|----------|-----------|---------|
| Blazor Server + MudBlazor (not SPA / WASM) | Single-process self-host story; SignalR circuit is sufficient at this scale | ✓ Good |
| SQLite + EF Core 10 | Self-hostable, no external services needed; auto-migrate at startup | ✓ Good |
| Recipe YAML format with `[name](#id)` ingredient links | Lets the AI emit recipes in a parseable shape and lets users paste them in | ✓ Resolved (v1.1 Phase 1) — canonical `RecipeDocument` v2 is the source of truth; `[name](#id)` is encoded in `ContentStep.Text` and rendered as keyboard-navigable chips in the editor (v1.2 Phase 6) so users never type the special syntax |
| Three independent recipe representations (YAML, JSON export, DB owned-entity) | Each grew with its own use case; no single source of truth was defined | ✓ Resolved (v1.1 Phase 1) — collapsed into a single versioned canonical record |
| Anthropic-only AI integration | Author uses Claude; no need for provider abstraction yet | ✓ Good — `IAiService` remains the only abstraction; structured-output transport added in v1.1 Phase 2 without disturbing the interface |
| Per-user API key + share table; recipient never sees the key | Self-host friendly without each user needing their own paid account | ✓ Good |
| AI opt-out clause in system prompt | Tolerated free-form recipes from older models | ✓ Resolved (v1.1 Phase 1 Plan 04) — opt-out clause deleted; lint denylist prevents regression |
| QuestPDF community license for cookbook PDF export | Free, GPL-compatible, server-side render | ✓ Good |
| `CookbookTransferDocument.SchemaVersion = 1` | Acknowledged versioning need on the JSON export | ✓ Good — extended in v1.1 Phase 2 (Plan 02-04) to route through `RecipeUpcasterChain` on deserialize |
| Identity middleware deferred | Designed for trusted LAN; complexity not justified yet | — Pending (still deferred post-v1.2) |
| **v1.2: Replace MudBlazor entirely** | Visual fidelity to the design handoff requires shapes (999px pill buttons, 64px display titles, 224px tabular timer, hanging accent numerals, custom outline icons) that MudBlazor would only approximate; once you skin enough of MudBlazor you've fought it more than you've used it. | ✓ Good (v1.2 Phase 7 / Plan 07-07) — package deleted, repo-wide `Mud[A-Z]` grep zero hits, dotnet build 0/0, tests 196/196 preserved |
| **v1.2: Pause v1.1 mid-flight; absorb Phase 3 into v1.2** | The chip composer (v1.1 Phase 3) was being built in MudBlazor — replacing MudBlazor wholesale would require rewriting it. Cheaper to author it once in the new component system. | ✓ Good (v1.2 Phase 6 / Plan 06-04) — chip composer shipped in custom Razor; EDITOR-01..07 absorbed as ED-03..09; round-trip canonical doc integrity preserved through save path |
| **v1.2: Skip the milestone research step** | The Claude Design handoff bundle already encoded stack/features/architecture/pitfalls. Spawning 4 parallel researchers would duplicate work. | ✓ Good — milestone shipped on time; design handoff was sufficient research |
| **v1.2: D-30 coexistence** (Plan 05-05) | Phase 5 MainLayout removed Mud layout chrome but RETAINED the four MudBlazor providers (Theme/Popover/Dialog/Snackbar) through Phase 6 to support unmigrated dialogs | ✓ Good — let Phases 5/6/7 ship serially without forcing a flag-day cutover; clean atomic strip in Plan 07-07 |
| **v1.2: D-39 AiChat canvas binds canonical RecipeDocument** (Plan 07-04) | AI Chat right canvas reads directly from `_lastStructuredRecipe.Value` (the canonical `RecipeDocument` produced by `IAiRecipeGenerator`), not from rendered chat text. POLISH-01 invariant preserved | ✓ Good — three-tier extractor stays deleted; AI Chat is now a "live recipe canvas" in the design-handoff sense |
| **v1.2: D-43 density toggle in localStorage** (Plan 07-05) | UserProfile has no Density column; adding one solely for a UI-pref toggle would require a migration | ✓ Good — matches existing `cookbot_dark_mode` localStorage pattern; `data-density` set on `<html>` before first paint |
| **v1.2 close: FUTURE-15 — AiChat "Edit anyway" path deferred** (audit 2026-05-01) | Validation-failed path routes RawResponse through `IRecipeFormatParser.TryParse`; if raw JSON also fails parse, degraded fallback (toast, no navigation) is non-crashing but fragile. Audit-flagged as known edge | — Deferred to v1.3+ |
| **v1.4: Phase 15 nutrition data source = Canadian Nutrient File (CNF), not USDA FDC** (decided 2026-06-07, pre-Phase-15) | User is Canadian (`UnitSystem=Canadian`). CNF (Health Canada) is the Canadian analog to USDA FoodData Central: offline relational-CSV bulk download (~5,993 foods, per-100 g, bilingual) → fits the bundled-SQLite-seed plan 1:1, and ships household-measure→gram **Conversion Factors** that remove most of the external "density table" need. License is **Open Government Licence – Canada** (redistribution allowed; *requires* attributing "Health Canada, Canadian Nutrient File"; nutrient values must not be modified, per-serving re-expression is allowed) — vs USDA CC0 which needs no attribution. | — Decided; to be locked in `/gsd-discuss-phase 15`. Implications: nutrition panel must credit Health Canada; seed stores CNF values verbatim; `CookBotSettings.FdcApiKey` repurposed to CNF (USDA optional fallback); disclaimer → "Data: Health Canada, Canadian Nutrient File (2015)". | ✓ Good — shipped v1.4 Phase 15 (flour ≈455 kcal/cup verified live; CNF description + FoodId surfaced; non-dismissable Health Canada attribution). |
| **v1.4: Export projectors are pure display-only Application functions** | `JsonLdRecipeProjector` + `CooklangRecipeProjector` receive `RecipeDocument` and return a string — never mutate canonical or touch `CanonicalDocumentJson`. Preserves the v1.3 display-only-layering invariant. | ✓ Good (v1.4 Phase 13) |
| **v1.4: `RecipePhoto` entity table, not a canonical-doc array** | Photo paths are host-specific operational state — kept out of `CanonicalDocumentJson`, stripped from `.cookbook.json` exports, never fed to/emitted by the AI. EF migration backfills `Recipe.PhotoUrl` → primary row. | ✓ Good (v1.4 Phase 14) |
| **v1.4: GALLERY-04 AI photo-search-term helper built, then retired** | Shipped in Phase 14, removed 2026-06-25 after human UAT ("not useful"); a harness regression guard keeps it gone. Paste-URL (HEAD-validated, scheme-allowlisted) flow retained. | ⚠️ Built then reverted — surface feature-value doubts in discuss/UAT earlier to avoid build-then-delete |
| **v1.4: Nutrition cached in `RecipeNutritionCache`, never in canonical doc** | `NutritionService` computes offline from CNF; AI never emits nutrition; cache invalidated on canonical-doc change; compute is an explicit post-save CTA that never blocks recipe save. | ✓ Good (v1.4 Phase 15) |
| **v1.5: Per-agent API-token auth — scoped exception to the no-auth posture** | External agents need an identity to write pantry/recipe data. A bearer token mapped to a real user (token-hash table + middleware resolves token→user) is added — but deliberately NOT ASP.NET Identity / OAuth / SSO, preserving the trusted-LAN posture. Needs its own threat pass. | — Pending (v1.5 Phase TBD) |
| **v1.5: Recipe creation via agents = structured-submit only** | Agents POST a canonical `RecipeDocument` (schema-validated, then `→ ParsedRecipe → RecipeService.CreateAsync`); no inbound Anthropic/AI call on creation. Deterministic, no API-key resolution, cheaper — but exposes the full canonical shape over the wire. | — Pending (v1.5 Phase TBD) |

## Evolution

This document evolves at phase transitions and milestone boundaries.

**After each phase transition** (via `/gsd-transition`):
1. Requirements invalidated? → Move to Out of Scope with reason
2. Requirements validated? → Move to Validated with phase reference
3. New requirements emerged? → Add to Active
4. Decisions to log? → Add to Key Decisions
5. "What This Is" still accurate? → Update if drifted

**After each milestone** (via `/gsd-complete-milestone`):
1. Full review of all sections
2. Core Value check — still the right priority?
3. Audit Out of Scope — reasons still valid?
4. Update Context with current state (users, feedback, metrics)

---
*Last updated: 2026-06-25 after v1.4 milestone close. **v1.4 Recipe Data & Interoperability SHIPPED (tag `v1.4`)** — Phases 12–16, 18 plans: richer v4 format (FORMAT-01..07), Schema.org JSON-LD + Cooklang one-way export (INTEROP-01..04), photo gallery (GALLERY-01..03; GALLERY-04 retired), offline Canadian Nutrient File nutrition (NUTR-01..06), UAT automation extended (UATAUTO-02 partial — core flows automated, format-fields-visible + gallery-upload deferred). Human UAT: Phase 14 10/10, Phase 15 14/15 (responsive-grid item fixed). Deferred items recorded in STATE.md. Next: **v1.5 External Agent Interface (MCP + REST API)** via `/gsd-new-milestone`. Earlier: 2026-06-05 — v1.3 SHIPPED (tag `v1.3`), Phases 8–11, 39 plans; 2026-05-15 — v1.2 shipped (tag `v1.2`); v1.1 paused after Phase 2.*
