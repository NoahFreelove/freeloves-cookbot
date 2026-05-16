---
phase: 09-photos-prod-ready-infrastructure
plan: 06
subsystem: docker-delivery
tags: [healthcheck, dockerfile, docker-compose, prod-readiness, deviation:rule-1]
requires:
  - 09-04 (PersistKeysToDbContext + DataProtection registered in Program.cs)
provides:
  - "/healthz endpoint (PROD-05, D-43) backed by AddDbContextCheck<CookBotDbContext>"
  - "Multi-stage Dockerfile (PROD-01, PROD-03) — sdk:10.0 → aspnet:10.0 + curl"
  - "docker-compose.yml (PROD-02, PROD-04, D-43) — restart: on-failure + healthcheck + two named volumes"
  - ".dockerignore — keeps operator's cookbot.db / planning notes out of image (T-09-06-01)"
affects:
  - src/CookBot.Web/Program.cs
  - src/CookBot.Web/CookBot.Web.csproj
  - Dockerfile (new)
  - docker-compose.yml (new)
  - .dockerignore (new)
tech-stack:
  added:
    - "Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore 10.0.* (first-party Microsoft, auto-approved per --auto chain)"
  patterns:
    - "ASP.NET Core AddDbContextCheck<TContext> — Database.CanConnectAsync at request time"
    - "Docker multi-stage build with /p:UseAppHost=false for smaller dll-only artifact"
    - "docker-compose named volumes for stateful data isolated from image"
key-files:
  created:
    - "Dockerfile"
    - "docker-compose.yml"
    - ".dockerignore"
  modified:
    - "src/CookBot.Web/Program.cs"
    - "src/CookBot.Web/CookBot.Web.csproj"
decisions:
  - "Auto-approved NuGet legitimacy gate (Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore 10.0.*) per --auto chain mode — first-party Microsoft, audited Approved in 09-RESEARCH lines 108-115"
  - "Rule 1 fix: aspnet:10.0 base image does NOT ship curl (09-RESEARCH line 97/548 was wrong) — installed curl via apt-get in runtime stage to keep compose healthcheck command verbatim per D-43"
  - "Healthcheck NuGet ships in CookBot.Web (not Infrastructure) — keeps web-host concerns in web project"
  - "/healthz wired with single AddDbContextCheck (no /ready vs /live split) — sufficient for D-43 semantics"
metrics:
  duration: "8m 7s"
  completed: "2026-05-16T18:56:14Z"
  tasks: "3 of 3"
  commits: "3 task commits + 1 summary commit"
---

# Phase 09 Plan 06: HealthChecks + Docker Delivery Summary

**One-liner:** Shipped end-to-end Docker delivery story — /healthz endpoint backed by `AddDbContextCheck<CookBotDbContext>`, multi-stage Dockerfile (.NET 10 SDK build → ASP.NET 10 runtime + curl), and docker-compose.yml with two named volumes, on-failure restart, and a curl-based healthcheck that absorbs first-boot seeder time via `start_period: 30s`.

## What Shipped

### Task 1 — HealthChecks NuGet + Program.cs wiring (commit `55e1789`)

- Added `Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore` (floating `10.0.*`) to `src/CookBot.Web/CookBot.Web.csproj`.
- `Program.cs` registers `AddHealthChecks().AddDbContextCheck<CookBotDbContext>(name: "database")` immediately after the Plan 09-04 `AddDataProtection` block, before `app.Build()`.
- `Program.cs` calls `app.MapHealthChecks("/healthz")` after `MapRazorComponents` and before the `using (var scope = ...)` seeder block — natural mid-file placement per the plan.
- **Manual smoke against `./run.sh`:** `curl -fsS http://localhost:7000/healthz` returned `200 Healthy`.
- Build: green (4 pre-existing EF1002 warnings in `RecipeTagBackfillTests.cs` are out of scope — pre-existing in Phase 8 test code).
- Tests: `285 / 285` passed (`--filter "Category!=RequiresApiKey"`); 6 ANTHROPIC_API_KEY-gated tests skipped per the test's own documented filter.

### Task 2 — Dockerfile + .dockerignore (commit `62a34d3`)

`Dockerfile` (repo root):

```dockerfile
# syntax=docker/dockerfile:1.7
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY *.sln ./
COPY src/CookBot.Domain/*.csproj src/CookBot.Domain/
COPY src/CookBot.Application/*.csproj src/CookBot.Application/
COPY src/CookBot.Infrastructure/*.csproj src/CookBot.Infrastructure/
COPY src/CookBot.Web/*.csproj src/CookBot.Web/
COPY tests/CookBot.Tests/*.csproj tests/CookBot.Tests/
RUN dotnet restore src/CookBot.Web/CookBot.Web.csproj
COPY src/ src/
RUN dotnet publish src/CookBot.Web/CookBot.Web.csproj -c Release -o /app /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
# (curl install — see Task 3 / Rule 1 fix)
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
COPY --from=build /app ./
ENV ASPNETCORE_URLS=http://+:7000   # PITFALL M4 — 0.0.0.0 bind
ENV TZ=UTC                          # PITFALL M7 — stable clock
EXPOSE 7000
ENTRYPOINT ["dotnet", "CookBot.Web.dll"]
```

`.dockerignore` (repo root) — excludes `**/bin/`, `**/obj/`, `**/publish/`, `.git/`, `.planning/`, `.claude/`, `.superpowers/`, `**/*.md`, all `*.db` / `*.db-wal` / `*.db-shm` / `*.db.pre-*.bak`, `appsettings.{Development,Production}.json`, and `src/CookBot.Web/wwwroot/uploads/*` (but keeps `.gitkeep`).

`docker build --check .` — no warnings.

### Task 3 — docker-compose.yml + smoke (commit `e1a5371`)

`docker-compose.yml` (repo root):

```yaml
services:
  cookbot:
    build: .
    image: freelovescookbot:latest
    container_name: cookbot
    ports:
      - "${COOKBOT_PORT:-7000}:7000"
    environment:
      ASPNETCORE_URLS: http://+:7000
      ConnectionStrings__DefaultConnection: "Data Source=/data/cookbot.db"
      COOKBOT_PORT: "7000"
    volumes:
      - cookbot_db:/data
      - cookbot_uploads:/app/wwwroot/uploads
    restart: on-failure            # D-43 override of PROD-02 unless-stopped
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:7000/healthz"]
      interval: 30s
      timeout: 5s
      start_period: 30s
      retries: 3

volumes:
  cookbot_db: {}
  cookbot_uploads: {}
```

Key invariants:
- **Two named volumes only** — `cookbot_db` at `/data` (cookbot.db + WAL sidecars), `cookbot_uploads` at `/app/wwwroot/uploads`. The DataProtection key ring colocates in `cookbot.db` via Plan 09-04's `PersistKeysToDbContext`, so no third volume — PITFALL C1 eliminated.
- **`restart: on-failure`** (NOT `unless-stopped`) per D-43 + PITFALL M6 first variant — surfaces startup failures via `docker ps` instead of looping silently.
- **`start_period: 30s`** absorbs first-boot seeder time (migrations + sentinel-prefix re-encryption + 365-day AiUsageLog cleanup all run before `app.Run()`). Failures during start_period don't count toward `retries: 3`.
- **`ConnectionStrings__DefaultConnection=Data Source=/data/cookbot.db`** — double-underscore is .NET configuration's section delimiter; overrides the `appsettings.json` relative path at runtime so the DB lives on the named volume.

### Full Docker Smoke (executor host had Docker 29.5.0 available)

```text
docker compose config         → exits 0 (compose syntax validated)
docker compose up --build -d  → builds in ~30s + boots
                              → status: (health: starting) for ~5s
                              → status: (healthy) at +6s after first boot
curl http://localhost:7000/healthz   → 200 "Healthy"
curl http://localhost:7000/          → 200 (root page)
docker compose stop                  → clean stop
docker compose start                 → (health: starting) → (healthy) in ~10s
curl http://localhost:7000/healthz   → 200 "Healthy" (after restart)
/data inside container:               cookbot.db (217 KB) + cookbot.db-shm (32 KB) + cookbot.db-wal (8 KB)
                                      → PITFALL M5 confirmed: WAL sidecars colocate in mounted volume
docker compose down (no -v)          → volumes preserved
Image size:                           540 MB (aspnet:10.0 base ~200 MB + app + curl)
```

The `/healthz` response shape is the ASP.NET Core HealthChecks default: HTTP 200 with body `Healthy` (plaintext, no JSON). The `AddDbContextCheck<CookBotDbContext>` named `"database"` runs `CookBotDbContext.Database.CanConnectAsync()` on every probe.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 — Bug] `mcr.microsoft.com/dotnet/aspnet:10.0` does NOT ship `curl` by default**

- **Found during:** Task 3 manual smoke. Container reported `(unhealthy)` for the full `start_period` + 3 retries window. `docker inspect cookbot --format='{{json .State.Health}}'` showed: `"Output": "OCI runtime exec failed: ... exec: \"curl\": executable file not found in $PATH"`.
- **Root cause:** `.planning/phases/09-photos-prod-ready-infrastructure/09-RESEARCH.md` lines 97 and 548 claimed the `aspnet:10.0` base image is Debian-based and "includes curl by default" — empirically false. The base image is minimal: no `curl`, no `wget`, no `nc`. Meanwhile the host can hit `/healthz` and get `200 Healthy` instantly, confirming the app itself is fine.
- **Fix:** Add `RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*` to the runtime stage of the Dockerfile, before `COPY --from=build`. Adds ~4 MB (image is 540 MB total). Keeps the compose healthcheck command verbatim per D-43 ("curl -f http://localhost:7000/healthz") — preferred over rewriting the test to use a netcat-style probe or a sidecar.
- **Verification:** Rebuilt + restarted; container reached `(healthy)` in ~10s on both first boot and after `docker compose stop/start`. Both `/healthz` and `/` return HTTP 200 from the host.
- **Files modified:** `Dockerfile`
- **Commit:** `e1a5371` (folded into Task 3 commit since the fix lives in the same execution wave)
- **Note for future planners:** The Phase 9 RESEARCH.md "Environment Availability table line 548" claim about curl-in-aspnet-base should be flagged as out-of-date — the .NET team has been progressively slimming the official runtime images. If a future plan wants to keep the image lean and skip the apt-get install, the alternative is a dotnet-native HTTP probe (e.g. `dotnet /app/HealthProbe.dll` shipped alongside the app dll) or netcat-based stanza using a different base image variant.

### Authentication Gates

None.

### NuGet Legitimacy Gate (Task 0)

Per `--auto chain` mode, the user pre-authorized addition of `Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore` (10.0.*). The package is first-party Microsoft (Microsoft.* namespace, github.com/dotnet/aspnetcore source repo, audited Approved in 09-RESEARCH lines 108-115). Auto-approved per chain flag; recorded here per the orchestrator brief.

## Validation against Success Criteria

| Criterion | Status |
|-----------|--------|
| HealthChecks.EntityFrameworkCore 10.0.* added to CookBot.Web.csproj | PASS (line 12-15 of csproj) |
| Program.cs registers AddHealthChecks().AddDbContextCheck<CookBotDbContext>("database") | PASS (after AddDataProtection, before app.Build()) |
| Program.cs maps /healthz via MapHealthChecks BEFORE app.Run() | PASS (after MapRazorComponents, before seeder scope) |
| Multi-stage Dockerfile uses sdk:10.0 → aspnet:10.0 | PASS |
| Dockerfile sets ASPNETCORE_URLS=http://+:7000, TZ=UTC, EXPOSE 7000, ENTRYPOINT [\"dotnet\", \"CookBot.Web.dll\"] | PASS |
| docker-compose.yml: two named volumes (cookbot_db:/data, cookbot_uploads:/app/wwwroot/uploads) | PASS |
| docker-compose.yml: restart: on-failure (NOT unless-stopped) per D-43 | PASS |
| docker-compose.yml: healthcheck curl -f http://localhost:7000/healthz, interval 30s, timeout 5s, start_period 30s, retries 3 | PASS |
| ConnectionStrings__DefaultConnection=Data Source=/data/cookbot.db | PASS |
| .dockerignore excludes bin/obj/.git/.planning/.claude/*.db etc | PASS |
| dotnet build green | PASS (4 pre-existing warnings out of scope) |
| dotnet test full suite green | PASS (285/285 non-API-gated, 6 RequiresApiKey skipped per test self-documentation) |
| Docker smoke: /healthz returns 200; stop/start cycle preserves data + key ring | PASS — full smoke executed, both cycles healthy in ~10s, WAL sidecars colocate in /data |
| Key ring colocates in cookbot.db via PROD-07 (Plan 09-04) — no third named volume | PASS |
| SUMMARY.md committed | (committed in next step) |

## Self-Check

```bash
# Files
[ -f Dockerfile ]               → FOUND
[ -f docker-compose.yml ]       → FOUND
[ -f .dockerignore ]            → FOUND
[ -f src/CookBot.Web/Program.cs ] → FOUND
[ -f src/CookBot.Web/CookBot.Web.csproj ] → FOUND

# Commits
git log --oneline | grep 55e1789 → FOUND (Task 1)
git log --oneline | grep 62a34d3 → FOUND (Task 2)
git log --oneline | grep e1a5371 → FOUND (Task 3 + Rule 1 fix)
```

## Self-Check: PASSED

## Known Stubs

None. The /healthz endpoint is fully wired (not a stub); Dockerfile + compose + .dockerignore are end-to-end functional and smoke-tested.

## Threat Flags

None. All new surface in this plan is enumerated in the plan's `<threat_model>` block (T-09-06-01 through T-09-06-SC2). No new endpoints, auth paths, or trust boundaries beyond what the threat register already covers.

## What This Unblocks

- **Phase 9 wave 5** (token telemetry — Plans 09-05+): no longer blocked on healthcheck observability; `docker ps` shows `(healthy)` so operators can spot AI-key migration failures.
- **Plan 09-07 (README rewrite)**: can reference the Dockerfile + docker-compose.yml as concrete artifacts. The Install section will document `docker compose up -d` as the canonical first-run path; the Backup section will list `cookbot_db` + `cookbot_uploads` as the two volumes operators must back up.
- **Phase 9 verification gate**: `/healthz` is the operator-visible signal that the seeder completed (migrations, sentinel-prefix re-encryption, 365-day cleanup all ran successfully).

## Commits

| Task | Commit  | Message |
|------|---------|---------|
| 1    | 55e1789 | feat(09-06): add HealthChecks NuGet + /healthz endpoint (PROD-05, D-43) |
| 2    | 62a34d3 | feat(09-06): add Dockerfile + .dockerignore (PROD-01, PROD-03, M4/M7) |
| 3    | e1a5371 | feat(09-06): add docker-compose.yml + curl install for healthcheck (PROD-02, PROD-04, D-43) |
