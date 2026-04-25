# Technology Stack

**Analysis Date:** 2026-04-25

## Languages

**Primary:**
- C# (latest, implied by .NET 10) — All backend, application logic, domain models, and Blazor Server components
- Razor (`.razor`) — Blazor server-rendered UI components in `src/CookBot.Web/Components/`
- HTML/CSS — `src/CookBot.Web/wwwroot/app.css`, `src/CookBot.Web/Components/App.razor`

**Secondary:**
- JavaScript (vanilla, no framework) — `src/CookBot.Web/wwwroot/js/cooking-timers.js` (countdown timers, browser notifications, Web Audio beep), `src/CookBot.Web/wwwroot/js/download.js` (base64 → Blob client-side download helper)
- YAML — Frontmatter format used by recipe parser (`src/CookBot.Application/Services/RecipeFormatParser.cs`)
- JSON — Cookbook export/import documents and ingredient seed data (`seeds/ingredients.json`)

## Runtime

**Environment:**
- .NET 10 (`net10.0`) — All five projects target `net10.0` (see all `*.csproj` files)
- ASP.NET Core (Blazor Server with Interactive Server render mode) — `src/CookBot.Web/Program.cs`
- Framework reference: `Microsoft.AspNetCore.App` declared in `src/CookBot.Infrastructure/CookBot.Infrastructure.csproj`

**Package Manager:**
- NuGet (declared in `*.csproj` `<PackageReference>` items)
- Lockfile: Not committed (no `packages.lock.json` present)
- No `global.json` pinning the SDK version

## Frameworks

**Core:**
- ASP.NET Core / Blazor Server (`Microsoft.NET.Sdk.Web`) — `src/CookBot.Web/CookBot.Web.csproj` uses Razor Components with `AddRazorComponents().AddInteractiveServerComponents()`
- Entity Framework Core 10 (Sqlite provider) — `src/CookBot.Infrastructure/Data/CookBotDbContext.cs`, configured in `src/CookBot.Infrastructure/DependencyInjection.cs`
- MudBlazor 8.15.0 — Component library; CSS/JS loaded via `_content/MudBlazor/...` in `src/CookBot.Web/Components/App.razor`; services registered with `AddMudServices()` in `Program.cs`

**Testing:**
- xUnit 2.9.2 — `tests/CookBot.Tests/CookBot.Tests.csproj`
- Microsoft.NET.Test.Sdk 17.12.0
- xunit.runner.visualstudio 2.8.2
- coverlet.collector 6.0.2 (coverage)

**Build/Dev:**
- `dotnet` CLI — Entry point script `run.sh` runs `dotnet run --project src/CookBot.Web`
- EF Core Design 10.* (`Microsoft.EntityFrameworkCore.Design`) — Provides `dotnet ef` migrations support; referenced in both `CookBot.Web` and `CookBot.Infrastructure`

## Key Dependencies

**Critical:**
- `Microsoft.EntityFrameworkCore.Sqlite` 10.* — Persistence layer (`src/CookBot.Infrastructure/CookBot.Infrastructure.csproj`)
- `Microsoft.AspNetCore.Identity.EntityFrameworkCore` 10.* — Referenced in `src/CookBot.Infrastructure/CookBot.Infrastructure.csproj` but NOT wired up in `Program.cs`; password hashing is implemented manually with `Microsoft.AspNetCore.Cryptography.KeyDerivation` PBKDF2 in `src/CookBot.Web/Services/CurrentUserService.cs`
- `MudBlazor` 8.15.0 — UI component framework
- `QuestPDF` 2025.1.0 — PDF generation for cookbook exports (`src/CookBot.Web/Services/CookbookPdfService.cs`); license set to `LicenseType.Community` in `Program.cs`
- `YamlDotNet` 16.3.0 — YAML frontmatter parsing for recipe formats (`src/CookBot.Application/Services/RecipeFormatParser.cs`)
- `Markdig` 0.45.0 — Markdown rendering (`src/CookBot.Web/Components/Pages/AiChat.razor`, `src/CookBot.Web/Components/Pages/CookingMode.razor`)
- `Microsoft.Extensions.DependencyInjection.Abstractions` 10.0.3 — Used by `CookBot.Application/DependencyInjection.cs`

**Infrastructure:**
- `System.Net.Http` (BCL) — Used directly to call Anthropic API in `src/CookBot.Infrastructure/AI/AnthropicAiService.cs`
- `System.Text.Json` (BCL) — JSON (de)serialization throughout (cookbook transfer, AI payloads, ingredient seeds)

## Configuration

**Environment:**
- `appsettings.json` (`src/CookBot.Web/appsettings.json`):
  - `ConnectionStrings:DefaultConnection` defaults to `Data Source=cookbot.db` (SQLite file relative to working directory)
  - `CookBot:AuthMode` (default `Disabled`)
  - `CookBot:AppName` (default `CookBot`)
  - `CookBot:AiFeaturesEnabled` (default `true`) — Host-wide AI kill switch
  - `CookBot:AnthropicApiKey` (default empty) — Optional global Anthropic key fallback when no per-user key is set
- `appsettings.Development.json` overrides logging only
- `Properties/launchSettings.json` binds dev server to `http://localhost:7000` with `ASPNETCORE_ENVIRONMENT=Development`
- `.gitignore` excludes `appsettings.*.json` except for `appsettings.json` and `appsettings.Development.json`, isolating any secret-bearing override files

**Strongly typed config:**
- `CookBot.Application.DTOs.CookBotSettings` (`src/CookBot.Application/DTOs/CookBotSettings.cs`) bound to the `CookBot` configuration section in `Program.cs`

**Build:**
- Solution file: `FreelovesCookBot.sln` (5 projects: Domain, Application, Infrastructure, Web, Tests)
- Per-project `.csproj` files use SDK-style projects with `<Nullable>enable</Nullable>` and `<ImplicitUsings>enable</ImplicitUsings>` consistently
- License declared in `CookBot.Web.csproj` as `GPL-3.0-only` (matches root `LICENSE` file)

## Platform Requirements

**Development:**
- .NET 10 SDK installed
- Linux/macOS/Windows compatible (project uses cross-platform .NET; SQLite has no extra requirements)
- `run.sh` is a Bash launch helper (`dotnet run --project src/CookBot.Web`)

**Production:**
- Self-hosted; designed by README to run on a "trusted network"
- Single binary plus a SQLite file (`cookbot.db`) and the `seeds/` directory accessed at startup via `DatabaseSeeder.SeedAsync` in `Program.cs`
- No containerization assets present (no Dockerfile, no docker-compose)
- No CI workflow files present (no `.github/`)

---

*Stack analysis: 2026-04-25*
