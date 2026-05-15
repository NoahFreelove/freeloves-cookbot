# FreelovesCookBot

## What This Is

Self-hosted Blazor Server cooking and baking tracker that lets a small group of trusted users author, scale, and cook recipes — with first-class AI assistance for generating and refining them via Anthropic Claude (or any external LLM through a copyable prompt). It pairs a structured recipe format with a step-by-step cooking flow (timers, browser notifications, ingredient highlighting), pantry/grocery features, and cookbook export/import/sharing in a single SQLite-backed .NET 10 app.

## Core Value

A durable home for the recipes the user actually cooks, captured in **one standardized format** that round-trips cleanly between AI generation, manual editing, cooking mode, and import/export — without the user (or the AI) having to know special syntax.

## Current State

**Shipped milestones:** v1.2 UI Redesign (2026-04-27, tag `v1.2`). Earlier: v1.0 (pre-GSD) + v1.1 Phases 1+2 (Canonical Format + AI Structured Output, 2026-04-25/26, no tag — rolled into v1.2 release).

**App today (post-v1.2):** Self-hosted Blazor Server cooking & baking tracker on .NET 10 + SQLite. Custom Razor component system (no MudBlazor) matching the Claude Design handoff — warm cream / cocoa ink / dialed-back orange accent, Inter typography, custom outline icons, striped photo placeholders, tabular numerals across 9 surfaces. AI-assisted recipe generation via Anthropic Claude using token-level constrained-decoding structured output, 2-retry repair loop, secret redaction, prompt-injection defense. Multi-user with optional password (PBKDF2-HMAC-SHA256), session-scoped current user, trusted-LAN posture. Canonical versioned `RecipeDocument` round-trips through AI generation, manual editing, cooking mode, JSON export, and `.cookbook.json` import.

**Current milestone (v1.3) — planning (started 2026-05-15).** Production-Ready & Format Maturity. One v3 canonical schema bump carries photos + description + per-step temperature; a prod-ready track makes the app shippable for other self-hosters (Dockerfile, encrypt-at-rest, telemetry, deploy docs); the v1.2 carry-forward tech-debt list closes.

## Current Milestone: v1.3 Production-Ready & Format Maturity

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

### Active

<!-- v1.3 Production-Ready & Format Maturity. REQ-IDs land when REQUIREMENTS.md is authored (next step in `/gsd-new-milestone`). The buckets below are the planning frame. -->

- **Schema v3 + Photos** — recipe photos (file upload + paste-URL, single hero), `Recipe.Description` column, per-step temperature, V2→V3 upcaster, AI schema update
- **Format cleanup** — `LegacyRecipeProjector` deletion, tags-relational, prompt snapshot test, README format section
- **QOL** — smart pantry-match, AiChat raw-edit hardening, accent variant picker, Profile-side AI prompt editor
- **Small-stuff polish** — cookbook reparenting on edit, pantry per-row quick-add, moon glyph, TopBar RightSlot, Home live JS tick
- **Prod-ready (self-hosters)** — Dockerfile + compose, encrypt-at-rest API key, token-cost telemetry, deploy guide + README rewrite

### Carry-forward (deferred to v1.4+)

<!-- Items not in scope for v1.3; deferred to subsequent milestones. v1.3 absorbs the photos / per-step-temperature / tags-relational / projector-deletion / AI-prompt-snapshot / README-format / FUTURE-01/02/13/14/15 / DEFERRED-PROF-AIPROMPT / D-25/D-26/D-37/D-15/D-16 / live-tick items from the previous carry-forward list. -->

- **Format extensions** — substitutions / equipment / doneness cues / source provenance (`FUTURE-03..06`) — further format fields beyond v1.3's photo + description + per-step-temperature additions
- **Schema.org Recipe / Cooklang one-way export** (`FUTURE-07`, `FUTURE-11`) — export interop for SEO / external tools
- **USDA FoodData Central nutrition computation** (`FUTURE-08`) — auto-derive nutrition from ingredient amounts
- **Tool-use fallback for structured-output regressions** (`FUTURE-09`) — defensive fallback if Anthropic Structured Outputs regresses
- **Per-sharer cookbook-import consent banner** (`FUTURE-12`) — UX-visible consent affordance for shared imports (AI-08-AUDIT Markdig lockdown is the technical mitigation)
- **Backfilling photos for existing recipes** — v1.3 migration leaves `PhotoUrl = null` for existing rows; bulk backfill UI is a separate concern
- **Multiple photos per recipe / photo gallery** — v1.3 ships single hero photo only
- **Reverse-image search ("find a photo for this recipe" AI feature)** — separate AI-feature scope, not a format-track item

### Out of Scope

<!-- Explicit boundaries with reasoning. -->

- Web API / SPA / WebAssembly client — Blazor Server with `InteractiveServer` render mode is the chosen architecture and there is no driver to expose a separate API
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
*Last updated: 2026-05-15 — v1.3 Production-Ready & Format Maturity milestone started (5 buckets: schema v3 + photos, format cleanup, QOL, small-stuff polish, prod-ready self-hosters; phases start at Phase 8; REQUIREMENTS.md + ROADMAP.md authored next via `/gsd-new-milestone`). Earlier: 2026-05-15 — v1.2 UI Redesign milestone shipped and closed (tag `v1.2`); 2026-04-27 — v1.2 milestone started; v1.1 paused after Phase 2.*
