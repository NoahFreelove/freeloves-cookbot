# FreelovesCookBot

Self-hosted Blazor Server cooking & baking tracker (.NET 10 + SQLite + MudBlazor). AI-assisted recipe generation via Anthropic Claude. Multi-user, trusted-LAN posture.

## Active milestone

**v1.1 — Canonical Format & AI Conformance.** Standardize the recipe format, make the AI use it reliably, remove special-syntax burden from authoring, ship per-step temperature as the first format-driven field.

Read first when picking up work:
- `.planning/PROJECT.md` — project context, validated/active requirements, key decisions
- `.planning/REQUIREMENTS.md` — 46 requirements across 6 categories with REQ-ID → phase traceability
- `.planning/ROADMAP.md` — 4 phases, dependency invariants, success criteria
- `.planning/STATE.md` — current position
- `.planning/research/SUMMARY.md` — research synthesis (start here before reading the four detail files)
- `.planning/codebase/` — 7-doc codebase map (ARCHITECTURE, STACK, STRUCTURE, CONVENTIONS, TESTING, INTEGRATIONS, CONCERNS)

## Codebase orientation

Clean/Onion architecture with 4 + 1 projects:

- `src/CookBot.Domain/` — POCO entities, enums, interfaces. No framework refs.
- `src/CookBot.Application/` — business logic, parsers (`RecipeFormatParser.cs`), AI orchestration (`PromptBuilderService.cs`), DTOs.
- `src/CookBot.Infrastructure/` — EF Core 10 + SQLite (`CookBotDbContext.cs`, migrations, seeder), Anthropic HTTP client (`AnthropicAiService.cs`).
- `src/CookBot.Web/` — Blazor Server host, 28 Razor pages under `Components/Pages/`, `Program.cs`, web-only services (`CurrentUserService`, `AiApiKeyResolutionService`, `CookbookTransferService`, `CookbookPdfService`).
- `tests/CookBot.Tests/` — xUnit 2.9.2.

Run locally: `./run.sh` (wraps `dotnet run --project src/CookBot.Web`). Server binds `http://localhost:7000`.

## GSD workflow

This project uses [GSD](https://github.com/freelove/get-shit-done) for planning. Core commands:

- `/gsd-progress` — check current position, route to next action
- `/gsd-discuss-phase N` — gather context before planning a phase
- `/gsd-plan-phase N` — create the phase plan
- `/gsd-execute-phase N` — execute approved plans atomically
- `/gsd-verify-work` — confirm phase deliverables match goals

Always work the workflow — don't write code outside a planned phase without an explicit deviation. Phase artifacts live under `.planning/phases/NN-name/`.

## Conventions worth knowing

See `.planning/codebase/CONVENTIONS.md` for the full audit. Highlights:

- **Nullable reference types and implicit usings are enabled** in every project.
- **Repositories** — generic `IRepository<T>` exists but services freely bypass it for `Include(...)` / `AsNoTracking()` queries.
- **Authorization** — enforced inside application/data services (e.g. `RecipeService`, `CookbookService`, `db.UserCanAccessRecipeAsync`), NOT by middleware. The `CookBotSettings.AuthMode` flag is reserved for future use; trust-LAN posture today.
- **Persistence** — `DatabaseSeeder.SeedAsync` runs `MigrateAsync()` at startup, then back-fills required data. Migrations are forward-only and live in `src/CookBot.Infrastructure/Migrations/`.
- **AI** — host-wide kill switch (`CookBotSettings.AiFeaturesEnabled`) + per-user toggle (`UserProfile.AiEnabled`); per-user API key with sharing model that hides the key from recipients.
- **Recipe format** — currently *three* divergent serializations (YAML wire / JSON export / DB owned-entity). Phase 1 of v1.1 collapses them into one canonical `RecipeDocument` record. See `.planning/codebase/CONCERNS.md §1–4`.

## Things to avoid

- Don't introduce a second AI provider abstraction or pull in `Microsoft.Extensions.AI` / official `Anthropic` NuGet — the existing `HttpClient` in `AnthropicAiService` is sufficient and structured-output is a body-shape change, not a client change.
- Don't add `Newtonsoft.Json` / `NJsonSchema` — the project is 100% System.Text.Json. Phase 1 adds `JsonSchema.Net` for runtime validation; that's the only new package.
- Don't add a `CookBot.Schemas` project — `RecipeDocument` is a pure POCO and belongs in `CookBot.Domain/Recipes/`.
- Don't auto-scale temperatures, prep times, or cook times — only `RecipeIngredient.Amount` scales. Doubling servings does not produce a 700°F oven.
- Don't reintroduce a "free-form / numbered-list fallback" escape hatch in the AI prompt — that's the opt-out clause the milestone is removing.
