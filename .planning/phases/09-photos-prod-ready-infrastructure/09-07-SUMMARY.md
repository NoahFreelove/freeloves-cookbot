---
phase: 09-photos-prod-ready-infrastructure
plan: 07
subsystem: docs
tags: [readme, operator-docs, install, configuration, backup, upgrade, self-hosting]
requirements_closed: [PROD-18, PROD-19, PROD-20, PROD-21, PHOTO-14]
depends_on: ["09-05", "09-06"]
provides:
  - "README.md: Install / Configuration / Backup & restore / Upgrade operator-facing sections"
  - "Documented reverse-proxy TLS pattern (Caddyfile snippet)"
  - "Documented dual-volume backup procedure (cookbot_db + cookbot_uploads)"
  - "Documented IDatabaseBackupService pre-*.bak migration safety nets"
  - "Documented 6-step DatabaseSeeder.SeedAsync boot sequence"
  - "Documented cross-user AI telemetry visibility (PITFALL M9)"
  - "Documented plaintext-vs-encrypted-at-rest AnthropicApiKey distinction"
  - "Documented forward-only migration policy and 365-day AiUsageLog retention"
affects:
  - "self-hoster onboarding flow (docker compose up + ./run.sh paths both documented)"
  - "operator backup discipline (explicit dual-volume procedure prevents PITFALL C6 data loss)"
tech-stack:
  added: []  # documentation-only plan; no NuGet, no code changes
  patterns: []
key-files:
  created:
    - .planning/phases/09-photos-prod-ready-infrastructure/09-07-SUMMARY.md
  modified:
    - README.md (113 -> 268 lines, +155)
decisions: []  # no new decisions; this plan documents existing Phase 9 D-40, D-41, D-42, D-43 + Phase 1 D-15 + Phase 8 D-37 inline-README precedent
metrics:
  duration_minutes: 2
  tasks_completed: 1
  files_modified: 1
  files_created: 1
  completed_date: 2026-05-16
---

# Phase 9 Plan 07: README operator-docs rewrite Summary

Appended four new H2 sections (Install, Configuration, Backup & restore, Upgrade) to README.md below the existing Phase 8 "Recipe Format" section, making the README the operator-facing source of truth for self-hosting FreelovesCookBot v1.3.

## One-liner

Operator-facing README documentation covering containerized self-host (`docker compose up`) + local-dev (`./run.sh`) install paths, env-var configuration matrix, dual-volume backup/restore procedure, and forward-only upgrade flow with `IDatabaseBackupService` snapshots.

## What landed

### README.md (113 → 268 lines, +155)

Four new H2 sections appended in order (no existing line 1–113 modified):

1. **`## Install`** (PROD-18)
   - **Option A — Docker:** `git clone`, `docker compose up -d --build`, `/healthz` check, port override via `COOKBOT_PORT`, first-run no-AI-key graceful degradation note.
   - **Option B — Local dev (./run.sh):** wraps `dotnet run --project src/CookBot.Web`, binds `localhost:7000`, db at `./cookbot.db`, uploads at `src/CookBot.Web/wwwroot/uploads/`.
   - **Reverse proxy for TLS:** Caddyfile snippet (`cookbot.example.lan { reverse_proxy localhost:7000 }`), nginx/Traefik mentioned, trusted-LAN posture clarified.
   - **PDF export:** explicit text-only callout (D-40) with PITFALL H6 link.

2. **`## Configuration`** (PROD-19)
   - Env-var override table: `ConnectionStrings__DefaultConnection`, `ASPNETCORE_URLS`, `CookBot__AuthMode`, `CookBot__AiFeaturesEnabled`, `CookBot__AnthropicApiKey`, `CookBot__DatabaseBackupRetention`.
   - **AI pricing subsection:** exact JSON example from Plan 09-05's `appsettings.json` (Haiku/Sonnet/Opus 4.7 rates) with `AiPricingVerifiedDate: 2026-05-16`; one-row-per-call AiUsageLog write semantics + retry-tagging note.
   - **Cross-user AI usage visibility:** explicit PITFALL M9 trust-model disclosure (key-owner sees who burned their credits via shared keys).
   - Plaintext-vs-encrypted-at-rest distinction: host-wide `CookBot:AnthropicApiKey` is plaintext config; per-user `AiApiKey` is encrypted via ASP.NET Core Data Protection.

3. **`## Backup & restore`** (PROD-20 + PHOTO-14)
   - Two-volume table: `cookbot_db` (cookbot.db + WAL sidecars + DataProtection key ring + `.pre-*.bak` files) + `cookbot_uploads` (uploaded photos).
   - **WAL files callout:** `cookbot.db-wal` and `cookbot.db-shm` MUST be backed up alongside `cookbot.db` for consistency (PITFALL M5).
   - **Data Protection key ring trust model:** the keys live inside `cookbot.db` with no external copy — losing the volume = losing every encrypted AI key (the trust model, intentional).
   - **Backup/restore procedures:** `docker compose stop` → `docker run --rm alpine tar` snapshot/extract pattern → `docker compose start`. Uses `stop`/`start` (not `down -v`) to preserve volumes.
   - `IDatabaseBackupService` `.pre-{MigrationName}.bak` files documented as safety nets, NOT substitutes for proper backups.

4. **`## Upgrade`** (PROD-21)
   - `docker compose pull && docker compose up -d --build` pattern.
   - Migrations auto-apply via `DatabaseSeeder.SeedAsync` → `MigrateAsync()`.
   - `IDatabaseBackupService` writes `cookbot.db.pre-{MigrationName}.bak` before every migration; retention controlled by `CookBot:DatabaseBackupRetention` (default 3, clamped [1, 10]).
   - **Forward-only migrations** explicitly noted; downgrade unsupported (recommend restore-from-snapshot).
   - **6-step DatabaseSeeder.SeedAsync boot sequence** for troubleshooting: Backup → Migrate → Null-canonical guard (Phase 8 invariant) → 365-day AiUsageLog cleanup (D-41) → Sentinel-prefix re-encryption (idempotent) → Seed.
   - Troubleshooting hint: 503 on `/healthz` after restart → `docker logs cookbot`.

## Verification

### Automated grep gate (from PLAN <verify>)

All 13 required content fragments verified present in README.md:

| Fragment | Status |
|---|---|
| `^## Install$` | present |
| `^## Configuration$` | present |
| `^## Backup & restore$` | present |
| `^## Upgrade$` | present |
| `docker compose up -d` | present |
| `./run.sh` | present |
| `reverse_proxy localhost:7000` | present |
| `cookbot_db` | present |
| `cookbot_uploads` | present |
| `cookbot.db-wal` | present |
| `CookBot__AnthropicApiKey` / `CookBot:AiPricing` | present |
| `2026-05-16` | present |
| `text-only` (PDF callout) | present |
| `forward-only` (migrations) | present |
| `cross-user` (telemetry visibility) | present |
| `IDatabaseBackupService` | present |

`grep -c "^## " README.md` returns **6** as expected: Features, Recipe Format, Install, Configuration, Backup & restore, Upgrade.

### Acceptance criteria

| Criterion | Status |
|---|---|
| Four new H2 sections in exact order | ✓ Install → Configuration → Backup & restore → Upgrade |
| Install covers BOTH `docker compose` AND `./run.sh` | ✓ Option A + Option B |
| Install has reverse-proxy Caddy snippet | ✓ `cookbot.example.lan { reverse_proxy localhost:7000 }` |
| Install explicitly notes PDF text-only | ✓ "Cookbook PDF export is **text-only**" |
| Configuration has env-var table (6 rows) | ✓ all 6 required env vars present |
| Configuration has AiPricing JSON example with 2026-05-16 values | ✓ Haiku $1/$5, Sonnet $3/$15, Opus $5/$25; verified date present |
| Configuration notes plaintext vs encrypted-at-rest distinction | ✓ |
| Configuration notes cross-user AI usage visibility (M9) | ✓ "Cross-user AI usage visibility" subsection |
| Backup names BOTH cookbot_db + cookbot_uploads + explains WAL | ✓ two-volume table + WAL paragraph |
| Backup explains Data Protection key ring is inside cookbot.db | ✓ "There is no copy of the Data Protection keys anywhere else" |
| Upgrade has `docker compose pull` command | ✓ |
| Upgrade notes IDatabaseBackupService snapshots | ✓ |
| Upgrade explicitly says migrations are forward-only | ✓ "Migrations are **forward-only**." |
| Upgrade has 6-step boot sequence | ✓ enumerated 1–6 with structured-log troubleshooting hint |
| No existing line (1–113) modified | ✓ `git diff` confirms additions only after line 113 |
| `grep -c "^## " README.md` returns 6 | ✓ |

### Build sanity check

`dotnet build` → **Build succeeded** (0 errors, 4 pre-existing EF1002 warnings in `RecipeTagBackfillTests.cs` — out of scope per executor scope-boundary rule).

### JSON example syntax

Both ```json fenced blocks in README.md parse cleanly via `json.tool`:
- Block 0 (gas-mark example, pre-existing): OK
- Block 1 (new AiPricing example, added by this plan): OK

## Deviations from Plan

**None.** The action block in 09-07-PLAN.md drafted the exact prose; the executor inserted it verbatim with no edits. The plan-level success criteria, must-haves, and acceptance criteria are all met as written.

## Files modified

- `README.md` — appended 155 lines (113 → 268 lines)

## Files created

- `.planning/phases/09-photos-prod-ready-infrastructure/09-07-SUMMARY.md` (this file)

## Commit

- `fe3c7b4` — docs(09-07): add Install + Configuration + Backup & Restore + Upgrade sections to README

## Closes

- **PROD-18** Install documentation (Docker + ./run.sh paths)
- **PROD-19** Configuration documentation (env-var matrix + AiPricing)
- **PROD-20** Backup & restore documentation (dual-volume procedure)
- **PROD-21** Upgrade documentation (forward-only migration policy)
- **PHOTO-14** Uploads-volume backup callout (PITFALL C6 mitigated by docs)

Phase 9's shippable-to-others promise is now backed by real operator documentation. A self-hoster can pull the repo, follow the README end-to-end, and reach a working install without external references.

## Self-Check: PASSED

- README.md exists and contains all 4 new H2 sections: FOUND
- Commit fe3c7b4 present in git log: FOUND
- dotnet build green: PASSED
- JSON examples parse: PASSED
- No existing README line (1–113) modified: VERIFIED via `git diff`
