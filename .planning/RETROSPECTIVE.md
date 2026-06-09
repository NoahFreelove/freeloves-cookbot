# Retrospective — FreelovesCookBot

A living record of what worked, what didn't, and what we learned per milestone.

## Milestone: v1.3 — Production-Ready & Format Maturity

**Shipped:** 2026-06-05 (tag `v1.3`)
**Phases:** 4 (8–11) | **Plans:** 39 | **Tasks:** 59

### What Was Built
Canonical `RecipeDocument` advanced to v3 (photos, description, per-step temperature) with the v1.1 format-cleanup carry-forwards closed; hero photos (upload + paste-URL); production-ready infra (Docker, encrypt-at-rest keys, token telemetry, deploy docs); QOL/polish consumer surfaces; and a Phase 11 cleanup that fixed four UAT-surfaced backlog items and shipped a **reusable Playwright browser-UAT harness** (`tests/uat-harness/`).

### What Worked
- **Promoting backlog → a real phase** (999.2–999.5 → Phase 11 CLEANUP-01..04) kept fixes inside the GSD workflow with full plan/verify rigor instead of ad-hoc patches.
- **Pattern-mapper before planning** paid off twice: it pinned CLEANUP-01's root cause (no-wrap `flex-end` clipping the leading child) and discovered CLEANUP-04's converter engine was ~70% already built — shrinking the largest item from "feature" to "wiring."
- **Plan-checker caught two real correctness risks** (scale-then-format order; CookingMode step-temp ordinal desync) before any code was written.
- **Automated UAT harness closed the loop** the user cares about most — Tests 5 & 7 went from manual/blocked to hands-free PASS, and the verifier independently re-ran it.

### What Was Inefficient
- The UAT harness was first built against **assumptions that didn't match the live app** (anchor links + a "Home Chef" default user that didn't exist) — it took an iterate-against-the-running-app pass to reconcile selectors (`@onclick` tiles via `/cookbooks/{id}`, real users Noah/Bob). Lesson: a harness must be validated against the running app, not just authored from a pattern map.
- A stale Phase-10 UAT spec sentence ("navigate to destination cookbook") disagreed with the implemented+planned behavior (recipe view per plan 10-10) — surfaced only when the harness asserted it. Keep UAT expected-text synced with plan decisions.

### Patterns Established
- **Reusable automated UAT harness** under `tests/uat-harness/` (Playwright/chromium, isolated from the .NET build) — reuse and extend it for future milestone UAT instead of hand-testing.
- **Honest deferral over fake pass** — UAT Test 4 (AI validation-fail path) records SKIP because it can't be triggered while the happy path succeeds; never hard-coded green.

### Key Lessons
- Don't trust literal `--next` routing when reality (open UAT, live backlog) contradicts it — surface the tension and drive the real work.
- Display-only feature layers (unit conversion) must grep-assert non-mutation of the canonical doc; the plan-checker + verifier both enforced it.

### Cost Observations
- Model mix: planning/orchestration on Opus; executors + researcher + verifier on Sonnet.
- Notable: pattern-mapping up front avoided rebuilding an existing converter — the single biggest time saver this milestone.

## Cross-Milestone Trends

| Milestone | Phases | Plans | Shipped | Notable |
|-----------|--------|-------|---------|---------|
| v1.3 Production-Ready & Format Maturity | 4 | 39 | 2026-06-05 | First automated UAT harness; backlog→phase promotion |
