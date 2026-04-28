# FreelovesCookBot

## What This Is

Self-hosted Blazor Server cooking and baking tracker that lets a small group of trusted users author, scale, and cook recipes — with first-class AI assistance for generating and refining them via Anthropic Claude (or any external LLM through a copyable prompt). It pairs a structured recipe format with a step-by-step cooking flow (timers, browser notifications, ingredient highlighting), pantry/grocery features, and cookbook export/import/sharing in a single SQLite-backed .NET 10 app.

## Core Value

A durable home for the recipes the user actually cooks, captured in **one standardized format** that round-trips cleanly between AI generation, manual editing, cooking mode, and import/export — without the user (or the AI) having to know special syntax.

## Current Milestone: v1.2 UI Redesign

**Goal:** Replace MudBlazor entirely with custom Razor components matching the Claude Design handoff bundle (`.planning/design-handoff/`) — warm cream / cocoa ink / dialed-back orange accent, Inter only, custom outline icons, striped photo placeholders, tabular numerals — across all 9 surfaces.

**Status of v1.1 (Canonical Format & AI Conformance):** **Paused.** Phases 1 and 2 shipped. Phase 3 (Editor UX chip composer) and Phase 4 (per-step temperature + cleanup) are paused; the editor work is **absorbed** into v1.2's `RECIPE-EDITOR` requirements (we'll author the chip composer correctly in the new component system rather than build it twice in MudBlazor and then rewrite). FEATURE-V2-* and POLISH-03/04/05/07 carry forward to a future milestone (likely v1.3).

**Target features:**
- Foundation — strip MudBlazor; CSS design tokens; custom shell (`MainLayout` + `Sidebar` + `TopBar`); shared atoms (`CbButton`, `CbChip`, `CbCard`, `CbStat`, `CbEyebrow`, `StripedPlaceholder`, custom outline `Icons`); dialog/select/snackbar primitives
- Marquee surfaces — Home dashboard (pantry-aware hero), Cooking Mode (tablet, adaptive timer/step hero, always-on ingredient rail), Recipe View (editorial), Recipe Editor (absorbs v1.1 Phase 3 chip composer)
- Remaining surfaces — Cookbook list/detail, Pantry, Grocery list (mobile-first), AI Chat (streaming canvas), Prompt Builder, Profile + ~14 dialogs

**Carry-forward constraints from v1.1:** AI-off toggle hides all AI surfaces (existing per-user `UserProfile.AiEnabled`); recipe-related screens round-trip the canonical `RecipeDocument` from v1.1 Phases 1/2; no new top-level deps beyond what's needed (no React, no Tailwind — pure Razor + CSS).

## Requirements

### Validated

<!-- Shipped and confirmed valuable. Inferred from existing codebase (.planning/codebase/). -->

- ✓ Recipe authoring — manual editor with ingredient autocomplete and step composer (`src/CookBot.Web/Components/Pages/RecipeEditor.razor`) — existing
- ✓ Multi-format paste-in — YAML frontmatter, numbered lines, or free-form, parsed by `IRecipeFormatParser` (`src/CookBot.Application/Services/RecipeFormatParser.cs`) — existing
- ✓ Step-by-step cooking mode with countdown timers, browser notifications, and ingredient highlighting (`src/CookBot.Web/Components/Pages/CookingMode.razor`, `src/CookBot.Web/wwwroot/js/cooking-timers.js`) — existing
- ✓ Recipe scaling with fraction display (`src/CookBot.Application/Services/RecipeScalingService.cs`, `FractionFormatter.cs`) — existing
- ✓ Cookbook organization — group recipes into cookbooks, view/edit (`src/CookBot.Web/Components/Pages/CookbookList.razor`, `CookbookDetail.razor`) — existing
- ✓ Cookbook export/import as JSON (`src/CookBot.Web/Services/CookbookTransferService.cs`, `src/CookBot.Application/DTOs/CookbookTransferDtos.cs`) — existing
- ✓ Cookbook PDF export (`src/CookBot.Web/Services/CookbookPdfService.cs`, QuestPDF) — existing
- ✓ Cookbook sharing between users (`CookbookShare` entity, `ShareCookbookDialog.razor`) — existing
- ✓ Pantry tracking with AI-assisted population (`src/CookBot.Application/Services/PantryAiPopulationService.cs`, `PantryView.razor`) — existing
- ✓ Grocery / shopping lists (`src/CookBot.Application/Services/GroceryListService.cs`, `GroceryListView.razor`) — existing
- ✓ AI chat (Anthropic) for recipe generation, streaming SSE (`src/CookBot.Infrastructure/AI/AnthropicAiService.cs`, `src/CookBot.Web/Components/Pages/AiChat.razor`) — existing
- ✓ Per-step "Ask about this step" assist in cooking mode (`src/CookBot.Application/Services/RecipeCookingAiContext.cs`) — existing
- ✓ Prompt builder — copyable system prompt for use in any external LLM (`src/CookBot.Web/Components/Pages/PromptBuilder.razor`) — existing
- ✓ API key storage and sharing (per-user key, sharer/recipient model that hides the key from recipients) (`src/CookBot.Web/Services/AiApiKeyResolutionService.cs`, `AiApiKeyShareService.cs`) — existing
- ✓ Multi-user with optional password (PBKDF2-HMAC-SHA256), session-scoped current user (`src/CookBot.Web/Services/CurrentUserService.cs`) — existing
- ✓ Authorization hardening — ownership and share checks on every recipe/cookbook mutation (`src/CookBot.Infrastructure/Data/RecipeAccessExtensions.cs`, `RecipeService`, `CookbookService`) — existing
- ✓ AI kill switches — host-wide (`CookBotSettings.AiFeaturesEnabled`) and per-user (`UserProfile.AiEnabled`) — existing
- ✓ 600+ ingredient seed database with autocomplete (`seeds/ingredients.json`, `DatabaseSeeder.cs`) — existing
- ✓ Dark mode toggle persisted to `localStorage` (`MainLayout.razor`) — existing
- ✓ Auto-applied EF Core migrations on startup (`DatabaseSeeder.SeedAsync` → `MigrateAsync`) — existing

### Active

<!-- Current scope. Building toward these in this milestone. -->

- [ ] **Recipe-mode UX without special syntax** — users author and edit recipes (including ingredient references and timers) without ever typing `[name](#id)`, picking between `text:` vs `section:`, or formatting YAML by hand
- [ ] **Single canonical, versioned recipe format** — one schema is the source of truth across the AI prompt, the YAML wire format, the JSON export, and the DB representation; the format carries an explicit `version` and supports forward-compatible evolution
- [x] **AI chat reliably emits the canonical format** — Validated in Phase 2: AI Structured Output & Conformance. Anthropic's structured-output transport (`SendStructuredAsync<T>`) emits canonical recipes via `output_config.format`; a 2-retry repair loop bounds validation failures; SecretRedactor strips API keys from error surfaces; PromptInjectionGuard wraps recipe content in `<recipe>` tags. AI-09 (consent banner) deferred to FUTURE-12; AI-08-AUDIT (Markdig DisableHtml lockdown) shipped as the technical replacement.
- [→] **Format-driven new features** — paused with v1.1; per-step temperature (FEATURE-V2-*) deferred to v1.3+ — see "Future Requirements" in REQUIREMENTS.md
- [→] **General usability improvements** — paused with v1.1
- [ ] **UI redesign — replace MudBlazor wholesale** (v1.2) — strip MudBlazor, build custom Razor components against the design handoff at `.planning/design-handoff/`; warm-cream identity tightened with Inter typography, custom outline icons, striped photo placeholders, tabular numerals
- [ ] **Adaptive Cooking Mode** (v1.2) — tablet-optimized; timer-as-hero when running, step-as-hero when idle; always-on right rail with this-step ingredient highlighting
- [ ] **Editorial Recipe View** (v1.2) — display-weight title, hanging accent numerals on steps, sticky scaled-ingredient sidebar, "Notes from your last cook" callout
- [ ] **AI Chat as live recipe canvas** (v1.2) — left-rail conversation + right canvas where streaming text builds a recipe card live; "save to cookbook" affordance is the canvas itself, not a button buried in chat
- [ ] **Pantry-aware Home dashboard** (v1.2) — leads with "Tonight from your pantry" matching surface; counters demoted to a glance strip with delta sub-text; "Recently cooked" + "Up next" cards
- [ ] **Editor chip composer in new component system** (v1.2; absorbs v1.1 EDITOR-01..07) — author the chip composer correctly the first time in custom Razor, not in MudBlazor and then again

### Out of Scope

<!-- Explicit boundaries with reasoning. -->

- Web API / SPA / WebAssembly client — Blazor Server with `InteractiveServer` render mode is the chosen architecture and there is no driver to expose a separate API
- Multi-tenant SaaS hosting — designed for self-hosting on a trusted LAN (`README.md`); auth is intentionally minimal
- AI providers other than Anthropic — `IAiService` is implemented only by `AnthropicAiService`; adding OpenAI/Gemini is a separate scope item the user has not asked for
- Containerization assets (Dockerfile, compose) — not requested for this milestone; `run.sh` + `dotnet run` is the deploy story
- CI/CD — no `.github/` workflows today; out of scope unless it appears in requirements
- Postgres / non-SQLite databases — current scale is single-host, single-user-group; SQLite is sufficient
- Identity middleware / OAuth / SSO — `CookBotSettings.AuthMode` is reserved for future hardening; not in this milestone
- Rewriting the cookbook export JSON DTO into a different exchange format — the *recipe* schema is in scope, but `CookbookTransferDocument`'s outer envelope (cookbook metadata + recipes array + `SourceApp`) stays; only the recipe shape inside it changes to match the canonical format

## Context

**Origin:** The author built this for personal use because online recipe sites are ad-laden and LLM-generated recipes had nowhere to live. The `README.md` notes the app is "completely vibecoded with Claude Opus 4.6" and is shared in case it's useful to others.

**Current footprint:**
- 4-project Clean/Onion architecture (`Domain`, `Application`, `Infrastructure`, `Web`) plus a Tests project (xUnit 2.9.2)
- 28 routable Razor pages on Blazor Server with MudBlazor 8.15
- 5 EF Core migrations through `20260416175214_AiApiKeyShares`
- ~55 source files in `src/`, ~1.5k lines of codebase docs at `.planning/codebase/`
- License: `GPL-3.0-only`

**Format situation today** (from `.planning/codebase/CONCERNS.md` §1–4):
The app currently has **three competing serializations** that all describe the same recipe concept and have drifted apart:
1. **YAML frontmatter** (`prepTime`, `cookTime`, `[name](#id)` step links, `text` vs `section` step keys) — what users paste and what the AI is told about
2. **JSON cookbook export** (`prepTimeMinutes`, `cookTimeMinutes`, `IsSection: bool` on every step, `localId` instead of `id`) — what `.cookbook.json` files contain
3. **DB-owned JSON columns** (additional `IngredientRefs: List<int>` derived on save, `TagsJson: string` deserialized at every read site) — what SQLite stores

The AI is taught only the YAML form. Round-tripping export → import → AI prompt forces three shapes. Standardizing this is the prerequisite for the milestone's other goals.

**AI-format conformance situation** (from `.planning/codebase/CONCERNS.md` §9–13):
- The system prompt currently has an explicit **opt-out clause** ("If you can't follow this exact format, plain numbered steps are fine — the app will parse them") — `PromptBuilderService.cs:201`
- The chat extractor `AiChat.ExtractRecipeContent` falls back through three increasingly loose heuristics — the loosest can swallow surrounding prose into the YAML body
- There is no retry/repair pass when the model emits an unparseable recipe
- The format spec is duplicated in two literal strings in `PromptBuilderService` (in-app vs copyable prompt) and will drift

**User-flagged concerns:**
- "Users shouldn't have to know our special syntax" (recipe mode UX)
- "Standardizing our file format"
- "Adding new features to the format"
- "Getting AI chat to actually respond in the format and use it"

## Constraints

- **Tech stack:** .NET 10 / Blazor Server (`InteractiveServer`) / SQLite via EF Core 10 / MudBlazor 8.15 — Established in current code; changing the platform is not a milestone goal.
- **AI provider:** Anthropic Claude only (`AnthropicAiService`, models Haiku 4.5 / Sonnet 4.6 / Opus 4.7) — `IAiService` is the abstraction if a second provider ever lands, but not in this milestone.
- **Persistence:** SQLite single-file (`cookbot.db`) — Self-host friendly; migrations applied at startup by `DatabaseSeeder.SeedAsync`.
- **Auth posture:** Trusted-LAN self-hosting; no Identity middleware — `CookBotSettings.AuthMode` exists but is "Reserved for future use; not enforced" (`CookBotSettings.cs`).
- **License:** GPL-3.0-only — All deps must be license-compatible.
- **Backward compatibility:** Existing `.cookbook.json` exports out in the wild must remain importable; a `version` field on the canonical format is the migration path.

## Key Decisions

| Decision | Rationale | Outcome |
|----------|-----------|---------|
| Blazor Server + MudBlazor (not SPA / WASM) | Single-process self-host story; SignalR circuit is sufficient at this scale | ✓ Good |
| SQLite + EF Core 10 | Self-hostable, no external services needed; auto-migrate at startup | ✓ Good |
| Recipe YAML format with `[name](#id)` ingredient links | Lets the AI emit recipes in a parseable shape and lets users paste them in | ⚠️ Revisit — see CONCERNS §1, §5: the syntax is leaky to users and inconsistent with other serializations |
| Three independent recipe representations (YAML, JSON export, DB owned-entity) | Each grew with its own use case; no single source of truth was defined | ⚠️ Revisit — milestone goal explicitly addresses this |
| Anthropic-only AI integration | Author uses Claude; no need for provider abstraction yet | — Pending |
| Per-user API key + share table; recipient never sees the key | Self-host friendly without each user needing their own paid account | ✓ Good |
| AI opt-out clause in system prompt | Tolerated free-form recipes from older models | ⚠️ Revisit — milestone goal: AI must use the format; remove or rework |
| QuestPDF community license for cookbook PDF export | Free, GPL-compatible, server-side render | ✓ Good |
| `CookbookTransferDocument.SchemaVersion = 1` | Acknowledged versioning need on the JSON export | ⚠️ Revisit — YAML format has no version field at all |
| Identity middleware deferred | Designed for trusted LAN; complexity not justified yet | — Pending |
| **v1.2: Replace MudBlazor entirely** | Visual fidelity to the design handoff requires shapes (999px pill buttons, 64px display titles, 224px tabular timer, hanging accent numerals, custom outline icons) that MudBlazor would only approximate; once you skin enough of MudBlazor you've fought it more than you've used it. Pure Razor + CSS is simpler than CSS overrides on every Mud component. | — Pending (v1.2) |
| **v1.2: Pause v1.1 mid-flight; absorb Phase 3 into v1.2** | The chip composer (v1.1 Phase 3) is being built in MudBlazor right now — replacing MudBlazor wholesale would require rewriting it. Cheaper to author it once in the new component system. Phase 4 (per-step temperature) is real domain work and carries forward to v1.3+. | — Pending (v1.2) |
| **v1.2: Skip the milestone research step** | The Claude Design handoff bundle (chats + 9 fully-specified screens + design system tokens in `styles.css`) already encodes stack/features/architecture/pitfalls. Spawning 4 parallel researchers would duplicate work. | — Logged (v1.2 milestone start) |

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
*Last updated: 2026-04-27 — v1.2 UI Redesign milestone started; v1.1 paused after Phase 2*
