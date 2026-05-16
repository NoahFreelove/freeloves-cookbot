# Phase 9: Photos + Prod-Ready Infrastructure - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-05-16
**Phase:** 09-photos-prod-ready-infrastructure
**Areas discussed:** Photo editor composite layout, Description placement, PDF/photo intersection, AiUsageLog retention policy, Description vs step[0] AI prompting, Healthcheck (bonus)

---

## Area 1 — Photo editor composite layout (PHOTO-09)

| Option | Description | Selected |
|--------|-------------|----------|
| Both inputs visible, top of form | Above name field. Preview thumbnail left, URL+upload+clear stack right. Always usable. | ✓ |
| Tab pattern (Upload \| Paste URL) | Single area, tabs let user pick path; preview below. Adds a click. | |
| Drop-zone + URL field stacked | Dashed drop-zone for drag-and-drop OR click-to-upload, paste-URL field beneath. | |
| Let Claude decide | Defer to planner. | |

**User's choice:** Both inputs visible, top of form
**Notes:** D-38. Friction-minimal, immediate. Matches v1.2 design language (custom Cb atoms, warm-cream cards, no accordions). Reading order: photo → name → description → ingredients → steps signals the photo is part of the recipe's identity.

---

## Area 1b — Description field placement (Phase 8 deferred surfacing)

| Option | Description | Selected |
|--------|-------------|----------|
| Below name, above ingredients | CbTextarea 2-3 rows under recipe name. Reading order: photo → name → description → ingredients → steps. | ✓ |
| Inside the photo composite card | Group photo + description as 'visual header' block; both fields in same card. | |
| Collapsible 'Details' accordion | New collapsible section (closed by default) holding Description + future format fields. | |
| Let Claude decide | Defer to planner. | |

**User's choice:** Below name, above ingredients
**Notes:** D-39. Description is the recipe's lede, visible by default; editor reading order mirrors RecipeView's post-v1.2 layout (title → lede → body).

---

## Area 2 — PDF/photo intersection (PITFALL H6)

| Option | Description | Selected |
|--------|-------------|----------|
| Omit photos in PDF (recommended) | PDF stays text-only for v1.3. No HttpClient in QuestPDF builder. README notes "text-only." | ✓ |
| Pre-fetch bytes async in download helper | CookbookDownloadHelper resolves PhotoUrl bytes before invoking sync PDF builder. Real feature, more work. | |
| Local-uploads only; external URLs omitted | Pre-fetch only when PhotoUrl starts with /uploads/. Middle ground. | |
| Defer photo-in-PDF to v1.4+ | Same as option 1 outcome; explicit carry-forward. | |

**User's choice:** Omit photos in PDF (recommended)
**Notes:** D-40. PDF stays text-only. README PROD-18..21 must explicitly note "PDF export is text-only — photos remain in-app only." Photo-in-PDF can revisit in v1.4+ if user demand surfaces.

---

## Area 3 — AiUsageLog retention policy (PROD-14 gap)

| Option | Description | Selected |
|--------|-------------|----------|
| Unbounded (trust SQLite) | No cleanup. 10k-20k rows over years negligible. Simplest, no admin surface. | |
| Admin-configurable in appsettings | `CookBot:AiUsageLog:RetentionDays` setting (default null = unbounded). DatabaseSeeder runs startup DELETE when set. | |
| Hardcoded 365-day rolling cleanup | Startup pass deletes rows older than 365 days. Bounded automatically. Loses long-term history. | ✓ |
| Defer to v1.4+ | Ship unbounded; revisit if a user complains. | |

**User's choice:** Hardcoded 365-day rolling cleanup
**Notes:** D-41. Cleanup runs in `DatabaseSeeder.SeedAsync` after Phase 8's null-canonical guard and before the sentinel-prefix re-encryption pass. Trade-off acknowledged: long-term cost archaeology beyond 365 days lost — acceptable for a personal-cooking app.

---

## Area 4 — Description vs step[0] AI prompting (PITFALL M10)

| Option | Description | Selected |
|--------|-------------|----------|
| Prompt prose only | Extend system prompt to define `description` shape and ban intro paragraphs in step 1. No validator change. | ✓ |
| Prompt prose + ValidationWarning | Same prompt updates + RecipeValidator non-blocking warning when steps[0] > 100 chars not starting with a verb. | |
| Prompt prose + structured-output schema description | Add `[Description(...)]` attributes so JsonSchemaExporter propagates field guidance into the AI schema. | |
| Let Claude decide | Defer to planner. | |

**User's choice:** Prompt prose only
**Notes:** D-42. Constrained decoding does most of the work; prose is a minimal nudge. Phase 8's Verify-based prompt snapshot regenerates `.verified` in the same commit as the prose change.

---

## Area 5 — Healthcheck `/healthz` (bonus, PROD-05 gap)

| Option | Description | Selected |
|--------|-------------|----------|
| App-alive + DB ping; compose `healthcheck` | `/healthz` returns 200 only after seeder completes + `SELECT 1` succeeds. Compose `healthcheck:` consumes it. `restart: on-failure` max 3 (overrides PROD-02). | ✓ |
| App-alive only | `/healthz` just returns 200 once Kestrel binds. No DB ping. Simpler but doesn't detect migration failures. | |
| App-alive + DB ping; no compose healthcheck | Route for external monitoring only (Caddy, uptime-kuma); skip compose `healthcheck:`. Keep `restart: unless-stopped`. | |
| Let Claude decide | Defer to planner. | |

**User's choice:** App-alive + DB ping; compose `healthcheck`
**Notes:** D-43. Overrides PROD-02's `restart: unless-stopped` → `restart: on-failure` with `max_retries: 3`. PITFALL M6's first option preferred — container exits visibly after 3 failed retries rather than masking startup failures in a restart spiral.

---

## Claude's Discretion

These were not gray areas the user weighed in on; the planner can make the calls during planning.

- `IRecipePhotoStorage` interface vs concrete service — defaults to concrete service in CookBot.Web/Services/ per Phase 8 D-29 precedent
- Plan/wave structure (35 reqs split across 5–6 waves) — suggested split in CONTEXT.md but planner may merge or split
- Sentinel-prefix detection regex (CfDJ8... vs sk-ant-) — pinned in PLAN.md
- Token pricing values + PricingVerifiedDate — verified at plan time against Anthropic's current pricing page
- Reverse-proxy README example (Caddy snippet or generic) — planner's call
- UseStaticFiles header details for /uploads (X-Content-Type-Options: nosniff per PITFALL H3)
- EF migration sequence/timestamp ordering for the three Phase 9 migrations
- `_lastStructuredRecipe.Value.PhotoUrl` plumbing in AiChat canvas (POLISH-01 preserved)
- First-run UX without AI key — existing v1.2 gate-with-CTA empty states sufficient per PROD-18

## Deferred Ideas

- Photo-in-PDF rendering — v1.4+
- Multiple photos / gallery / carousel — v1.4+ (single PhotoUrl in v3)
- Image resizing / thumbnail generation / EXIF stripping — v1.4+
- Reverse-image search AI feature — v1.4+
- CDN / image proxying — out of scope for trusted-LAN
- AiUsageLog retention beyond 365 days — revisit if requested
- Admin total-cost-by-user view — v1.4+
- `CookBotSettings.TelemetryEnabled` killswitch — v1.4+ if requested
- TLS/HTTPS inside container — v1.4+; v1.3 points at reverse proxy
- First-run setup wizard / onboarding — existing gates sufficient
- AI key rotation UX — v1.4+
- EXIF metadata stripping — v1.4+
- `.env.example` shape — planner's discretion
- Smart pantry-match dietary filter (Phase 10 QOL-02)
- Profile telemetry read widget (Phase 10 PROD-17 read surface)
