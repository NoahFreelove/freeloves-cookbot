# FreelovesCookBot

## What This Is

Self-hosted Blazor Server cooking and baking tracker that lets a small group of trusted users author, scale, and cook recipes — with first-class AI assistance for generating and refining them via Anthropic Claude (or any external LLM through a copyable prompt). It pairs a structured recipe format with a step-by-step cooking flow (timers, browser notifications, ingredient highlighting), pantry/grocery features, and cookbook export/import/sharing in a single SQLite-backed .NET 10 app.

## Core Value

A durable home for the recipes the user actually cooks, captured in **one standardized format** that round-trips cleanly between AI generation, manual editing, cooking mode, and import/export — without the user (or the AI) having to know special syntax.

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
- [ ] **AI chat reliably emits the canonical format** — system prompt requires the format (no opt-out), output is validated, and the app self-repairs (re-prompts the model) when the response doesn't parse
- [ ] **Format-driven new features** — once the format is canonical, add at least one new field/capability that exercises versioning (candidates: per-step temperature, ingredient substitutions, expiration dates, nutrition, equipment requirements) — exact list locked during requirements step
- [ ] **General usability improvements** — friction items surfaced in the codebase concerns audit and from the user's own use, scoped during requirements step (candidates: better paste-raw-text affordances, smarter timer detection, scaling-aware timing notes, recipe-from-AI save flow polish)

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
*Last updated: 2026-04-25 after initialization (brownfield import — codebase mapped first)*
