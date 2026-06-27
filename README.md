# Freelove's Cook bot

*This app is completely vibecoded with Claude Opus 4.6,7,8, but it has been useful to me, so I*
 *publish it in hopes its useful to someone else too.*

I love cooking and baking, I use LLMs to generate or clean up a lot of my recipes because online recipe websites
have so much slop and just so many ads. To get to the recipe you have to scroll down two pages
of about the author and dismiss 8 video ads. And if you have a question you're likely going to an LLM anyway!

I have too many recipes I enjoy generated, I'd love to host them somewhere so I can reference
them. But I also would appreciate it if ones I generate could just be saved in some standardized
format too, so I made this app.

This is a cooking and baking tracking app.

This app can be self hosted completely. It uses Blazor and SQLite

## Features

- **AI recipe generation** — uses Claude Sonnet 4.6 to generate recipes in a structured step format; integrates directly via Anthropic API key or via a prompt generator you can paste into any LLM
- **Multi-format recipe input** — paste recipes as YAML, plain text, or free-form and the app will parse them
- **Structured recipe steps with inline timers** — each step can carry a timer duration so you always know how long to wait
- **Step-by-step cooking mode** — walk through a recipe one step at a time with countdown timers and browser notifications when a timer expires
- **Recipe scaling with fraction display** — scale any recipe up or down; ingredient amounts display as clean fractions
- **600+ ingredient seed database with autocomplete** — start typing an ingredient and get suggestions instantly
- **Flexible units** — any unit string is accepted ("cups", "handful", "splash", etc.) so you're never fighting the form
- **Pantry tracking** — track your ingredients and see at a glance whether you have enough to make a recipe
- **Shopping lists** — generate a shopping list from any recipe
- **Shareable cookbooks** — group recipes into cookbooks and share them with others
- **Multi-user support** — password-optional accounts, designed for self-hosting on a trusted network
- **User authorization hardening** — recipes and cookbooks are protected so users can only edit their own content
- **AI toggle** — if you hate AI being integrated into everything, flip the toggle in your profile and you won't see "AI" anywhere else in the app

If you absolutely hate AI being integrated into everything, there is a toggle in the profile page to disable it.
You won't even see "AI" anywhere else in the app after that.

It has AI chatbot integration if you wish to use an Anthropic API key, but it also has a
prompt generator which you can just put into whichever AI agent you like best so you don't
have to pay extra.

I'd like to implement some more features like smarter food expiration tracking, recipe
substitutions, and export features.

## Recipe Format

FreelovesCookBot stores every recipe in a single canonical format: the `RecipeDocument` C# record (in `CookBot.Domain.Recipes`). YAML, JSON export, and the database column all serialize to the same shape via `JsonRecipeSerializer`; the AI prompt's structured-output schema is generated automatically from the same C# type.

### YAML wire format (v3)

The app reads and writes recipes in a YAML envelope with all v3 fields:

```yaml
---
version: 3
name: Brown Butter Cookies
servings: 24
prepTimeMinutes: 20
cookTimeMinutes: 12
photoUrl: https://example.com/cookies.jpg
description: Crisp-edged, chewy-centered butter cookies.
tags: [baking, dessert]
ingredients:
  - id: 1
    name: butter
    amount: 226
    unit: g
  - id: 2
    name: flour
    amount: 250
    unit: g
steps:
  - kind: section
    heading: Brown the butter
  - kind: content
    text: Melt the [butter](#1) over medium heat until amber and nutty.
  - kind: content
    text: Bake until edges are set.
    temperature:
      value: 375
      unit: F
    timers:
      - duration: 12
        unit: min
        label: bake
---
```

Steps come in two kinds: `section` (a heading with no other fields) and `content` (instruction text with optional timers and an optional per-step temperature). Ingredient references use `[ingredient name](#id)` markdown link syntax to bind step text to the ingredient list by its per-recipe `id`.

### JSON export format

The `.cookbook.json` export and database column use indented JSON via `JsonRecipeSerializer.SerializeIndented`. The shape is identical to the YAML example above. Gas mark temperatures get a special human-readable rendering in the indented form:

```json
{
  "kind": "content",
  "text": "Roast vegetables.",
  "temperature": "4½"
}
```

In the canonical wire format (database column, AI prompt schema, `.cookbook.json` export from `Serialize`), gas half-stops are stored as `{ "value": 4.5, "unit": "gas" }`. Only the human-readable indented JSON (`SerializeIndented`) renders them as `"4½"` for visual ergonomics.

### V1 → V2 → V3 upcaster lineage

Recipes stored by older app versions are upcast on import through a forward-only chain:

- **V1 → V2** (`Migration_V1_To_V2`): renames `prepTime` → `prepTimeMinutes` and `cookTime` → `cookTimeMinutes` (Pitfall C2 — units in field name); renames per-ingredient `localId` → `id` (D-06); replaces the `IsSection: true` boolean discriminator and the `{ section: "X" }` legacy YAML step shape with the `kind: "section"` / `kind: "content"` polymorphic discriminator.
- **V2 → V3** (`Migration_V2_To_V3`): introduces `photoUrl` (string?, max 2048), `description` (string?, max 4096), and per-step `temperature` (`{ value, unit }` where unit is `"F"`, `"C"`, or `"gas"`). All three fields default to null on existing v2 documents — per-field null-coalescing per PITFALLS C7.

### Internally managed format

The recipe format is managed internally; users do not need to author YAML or JSON directly. The recipe editor produces canonical-format documents through the chip composer. The upcaster chain is forward-only — `.cookbook.json` files exported from older app versions upcast on import; downgrade is unsupported.

## Install

Two supported paths: containerized self-host (recommended for sharing the app with others) or local dev mode (recommended for tinkering and contributing).

### Option A — Docker (recommended for self-hosters)

Requirements: Docker engine 20.10+, Docker Compose v2+. No source checkout or .NET SDK needed — a prebuilt `linux/amd64` image is published to GitHub Container Registry on every release.

```bash
# Grab the latest release's compose file (no git clone needed) and start
curl -fsSLO https://github.com/NoahFreelove/freeloves-cookbot/releases/latest/download/docker-compose.yml
docker compose up -d
```

This pulls `ghcr.io/noahfreelove/freeloves-cookbot:latest` (the newest tagged release) and starts it. First boot takes ~30 seconds while the seeder runs migrations + creates the Data Protection key ring. The healthcheck reports `(healthy)` once the app responds on `/healthz`:

```bash
docker ps                             # status column shows "Up X seconds (healthy)" when ready
curl -fsS http://localhost:7000/healthz
```

Open `http://localhost:7000/` in a browser on the same LAN. First-run UX: no AI key is required to start. Recipe management works offline; AI assistant features gracefully degrade until a user enters a personal Anthropic API key in **Profile → AI**.

Port override: set `COOKBOT_PORT` before `docker compose up` to bind a different host port (compose maps `${COOKBOT_PORT:-7000}:7000`).

Build from source instead of pulling: uncomment `build: .` in `docker-compose.yml` (after a `git clone`) and run `docker compose up -d --build`.

### Option B — Local dev mode (./run.sh)

Requirements: .NET 10 SDK.

```bash
git clone https://github.com/<your-fork>/freeloves-cookbot.git
cd freeloves-cookbot
./run.sh
```

This wraps `dotnet run --project src/CookBot.Web` and binds `http://localhost:7000`. The SQLite database lives at `./cookbot.db` next to the source tree. Uploaded photos land in `src/CookBot.Web/wwwroot/uploads/`. Both paths are gitignored.

### Reverse proxy for TLS (recommended for non-localhost deployments)

Phase 9 does not terminate TLS inside the container — trusted-LAN posture only. Operators who want HTTPS or want to expose the app beyond `127.0.0.1` should put a reverse proxy in front. Caddy is the simplest choice:

```caddyfile
cookbot.example.lan {
    reverse_proxy localhost:7000
}
```

nginx and Traefik work the same way. Cert provisioning, HSTS, and Let's Encrypt automation are handled by the proxy, not by FreelovesCookBot.

### PDF export

Cookbook PDF export is **text-only**. Recipe photos are not embedded in PDF output. This is intentional for v1.3 — re-fetching arbitrary photo URLs from inside the synchronous PDF builder is a documented foot-gun (PITFALLS H6) that Phase 9 chose to avoid. Photo-in-PDF may revisit in a future release.

## Configuration

All settings can be overridden via environment variables using .NET's standard double-underscore section delimiter. Compose users set them in `docker-compose.yml`; local-dev users use `appsettings.Development.json` (gitignored).

| Env var | Source key | Default | Purpose |
|---|---|---|---|
| `ConnectionStrings__DefaultConnection` | `ConnectionStrings:DefaultConnection` | `Data Source=cookbot.db` | SQLite file path. Compose sets this to `Data Source=/data/cookbot.db` so the file lands on the named volume. |
| `ASPNETCORE_URLS` | (Kestrel) | `http://localhost:5000` | Bind address. Dockerfile sets `http://+:7000`. |
| `CookBot__AuthMode` | `CookBot:AuthMode` | `Disabled` | Reserved for future use; do not rely on this for security. |
| `CookBot__AiFeaturesEnabled` | `CookBot:AiFeaturesEnabled` | `true` | Host-wide kill switch for AI assistant; per-user toggle (`UserProfile.AiEnabled`) is the second gate. |
| `CookBot__AnthropicApiKey` | `CookBot:AnthropicApiKey` | `""` | Host-wide fallback key. **Plaintext in config** — intended for single-operator scenarios where appsettings.json is read-only via env vars. Per-user keys (set via Profile UI) are encrypted-at-rest with ASP.NET Core Data Protection. |
| `CookBot__DatabaseBackupRetention` | `CookBot:DatabaseBackupRetention` | `3` | Number of `.pre-*.bak` files kept alongside `cookbot.db`. Clamped to [1, 10]. |

### AI pricing

Per-million-token pricing for cost-estimation telemetry lives in `appsettings.json` under `CookBot:AiPricing`. The current pricing was verified against `https://platform.claude.com/docs/en/about-claude/pricing` on `2026-05-16`. When Anthropic raises prices, update this block:

```json
{
  "CookBot": {
    "AiPricing": {
      "claude-haiku-4-5-20251001": { "InputTokensPerMillionUsd": 1.00, "OutputTokensPerMillionUsd": 5.00 },
      "claude-sonnet-4-6":          { "InputTokensPerMillionUsd": 3.00, "OutputTokensPerMillionUsd": 15.00 },
      "claude-opus-4-7":            { "InputTokensPerMillionUsd": 5.00, "OutputTokensPerMillionUsd": 25.00 }
    },
    "AiPricingVerifiedDate": "2026-05-16"
  }
}
```

Each AI call writes one `AiUsageLog` row capturing input/output tokens and the computed `EstimatedCostUsd`. Repair-loop retries (up to 2 per generation) are tagged `IsRetryAttempt = true` so aggregation queries can exclude them.

### Cross-user AI usage visibility

In trusted-LAN mode, when one user shares their AI key with another (Profile → "Share my AI key"), the key-owner can see exactly who has burned their credits via the per-user usage telemetry. This is the documented trust model for self-hosted small-team deployments. If you need stricter isolation, do not enable key-sharing in your deployment.

## Backup & restore

Two volumes hold all persistent state. Backup BOTH together — they are not independently recoverable.

| Volume | Contents | Recovery without it |
|---|---|---|
| `cookbot_db` (`/data` inside container) | `cookbot.db` (recipes, users, profiles, encrypted AI keys, Data Protection key ring), `cookbot.db-wal`, `cookbot.db-shm`, `cookbot.db.pre-*.bak` migration backups | Everything is lost. There is no copy of the Data Protection keys anywhere else — losing this volume means every encrypted AI key in user profiles becomes unrecoverable (the trust model). |
| `cookbot_uploads` (`/app/wwwroot/uploads/` inside container) | User-uploaded recipe photos | Photos referenced via paste-URL still resolve (the URL is in the canonical doc); uploaded photos show as broken images until re-uploaded. |

### Backup procedure (Docker)

```bash
# Stop the container so SQLite isn't mid-write during the copy.
docker compose stop

# Snapshot both volumes. The exact path depends on your Docker engine.
docker run --rm -v cookbot_db:/source -v "$PWD/backups":/dest alpine \
    tar czf /dest/cookbot_db-$(date +%Y%m%d).tar.gz -C /source .
docker run --rm -v cookbot_uploads:/source -v "$PWD/backups":/dest alpine \
    tar czf /dest/cookbot_uploads-$(date +%Y%m%d).tar.gz -C /source .

docker compose start
```

The `cookbot.db-wal` and `cookbot.db-shm` files in the `cookbot_db` volume are SQLite write-ahead log files. They MUST be backed up together with `cookbot.db` (a backup of just `cookbot.db` without its WAL may be inconsistent if SQLite was mid-checkpoint at backup time). The volume snapshot above captures all three by design.

The `cookbot.db.pre-*.bak` files are migration safety nets created automatically by `IDatabaseBackupService` before every EF migration runs (Phase 1 D-15 invariant). They are useful for rolling back a migration but they are NOT a substitute for proper backups.

### Restore procedure

```bash
docker compose stop

# Wipe and restore from a snapshot.
docker run --rm -v cookbot_db:/dest -v "$PWD/backups":/source alpine \
    sh -c "rm -rf /dest/* && tar xzf /source/cookbot_db-YYYYMMDD.tar.gz -C /dest"
docker run --rm -v cookbot_uploads:/dest -v "$PWD/backups":/source alpine \
    sh -c "rm -rf /dest/* && tar xzf /source/cookbot_uploads-YYYYMMDD.tar.gz -C /dest"

docker compose start
```

No UI backup/restore button is shipped in v1.3 — volumes + docs only. A UI backup feature may land in a future release.

## Upgrade

```bash
docker compose pull        # fetch the newest published image (:latest tracks the newest release)
docker compose up -d
```

If you build from source instead, run `docker compose up -d --build` after `git pull`.

Migrations auto-apply at container start via `DatabaseSeeder.SeedAsync` → `MigrateAsync()`. Before each migration runs, `IDatabaseBackupService` writes a `cookbot.db.pre-{MigrationName}.bak` snapshot alongside `cookbot.db` — useful as a rollback point if a migration produces unexpected results. The retention count is controlled by `CookBot:DatabaseBackupRetention` (default 3, clamped [1, 10]).

Migrations are **forward-only**. Downgrading to an earlier version is not supported. If you need to revert to an older release, restore from a backup snapshot taken before that release ran for the first time.

Boot sequence on every container start (FYI for troubleshooting):

1. **Backup** — `IDatabaseBackupService` snapshots `cookbot.db` if any migration is pending.
2. **Migrate** — `dotnet ef` applies all pending EF migrations forward.
3. **Null-canonical guard** — fails loud if any recipe row has `null CanonicalDocumentJson` (Phase 8 invariant).
4. **365-day cleanup** — `AiUsageLog` rows older than 365 days are pruned via a single SQL DELETE (Phase 9 D-41).
5. **Sentinel-prefix re-encryption** — any plaintext `UserProfile.AiApiKey` rows (legacy installs upgrading to Phase 9) are encrypted via ASP.NET Core Data Protection. Idempotent: a second boot is a no-op.
6. **Seed** — admin user / personal pantry / default cookbook setup runs only on a truly empty database.

If `/healthz` returns 503 after a long restart, check `docker logs cookbot` — the seeder logs each step with structured log lines, and the failure point will be visible.
