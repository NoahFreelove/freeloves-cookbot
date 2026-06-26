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

## Milestone: v1.4 — Recipe Data & Interoperability

**Shipped:** 2026-06-25 (tag `v1.4`)
**Phases:** 5 (12–16) | **Plans:** 18 | **Tasks:** 19

### What Was Built
Canonical `RecipeDocument` advanced to v4 (ingredient substitutions, equipment list, per-step doneness cues, source/provenance) on a per-field upcaster; two pure display-only export projectors (Schema.org `Recipe` JSON-LD + one-way Cooklang `.cook`); a multi-photo gallery (`RecipePhoto` entity, upload/reorder/set-hero, disk cleanup); fully-offline nutrition from a bundled Canadian Nutrient File seed (`NutritionService` + 5-state per-serving panel + Health Canada attribution + JSON-LD wiring); and an extension of the Playwright UAT harness (`test16-integration.mjs`).

### What Worked
- **Schema bump as a standalone first phase (12)** before any consumer — every downstream theme (projectors, gallery migration, nutrition) read `RecipeDocument` v4 from day one, avoiding build-against-v3-then-repatch rework. The build-order dependency chain held.
- **Pure display-only projector pattern** (receive `RecipeDocument`, return a string, grep-assert non-mutation) made JSON-LD and Cooklang trivially golden-testable and kept the canonical-mutation invariant clean across two new output formats.
- **A pre-phase data-source decision (CNF over USDA FDC)** fit the bundled-SQLite-seed plan 1:1 and — via CNF household-measure→gram Conversion Factors — removed most of the external density-table burden before Phase 15 planning even started.
- **Reusing the v1.3 UAT harness** paid off again: `test16` automated JSON-LD validity, Cooklang export, and 8/15 nutrition items hands-free.

### What Was Inefficient
- **Build-then-revert on GALLERY-04** — the AI photo-search-term helper was fully built in Phase 14, then deleted after human UAT ("not useful"). Feature value should have been pressure-tested in discuss/UAT before implementation; a harness regression guard now keeps it gone.
- **Blazor `<InputFile>` SignalR uploads aren't Playwright-drivable** — discovered during Phase 14 UAT, it stranded 6+ gallery items as manual-only and left UATAUTO-02 partial at milestone close. Driving upload coverage needs a deliberate test-only direct-upload seam (a production-code decision deferred).
- **A stale dev server served pre-migration code** during Phase 14 UAT (a build predating the `RecipePhotos` migration), masking the working gallery until restarted. Reinforced: restart the server after any redeploy before UAT.
- **Phase 16 never received formal PLAN.md files** — its UAT/integration work ran directly through the harness, so the milestone-close CLI flagged it as "unstarted" and required `--force`. Phase-as-harness-work doesn't map cleanly onto the plan/summary model.

### Patterns Established
- **Standalone schema-bump-first phase** ahead of any format consumer.
- **Display-only projector** convention (`RecipeDocument` → string, never mutates canonical) — now used by JSON-LD, Cooklang, and the nutrition panel.
- **Offline-bundled reference data with a mandatory attribution disclaimer** (OGL-Canada: values stored verbatim, per-serving re-expression at display, non-dismissable credit).

### Key Lessons
- Restart the dev server after any redeploy before UAT — stale circuits / unapplied migrations silently serve old behavior.
- Pressure-test "nice to have" AI affordances in discuss/UAT before building them (GALLERY-04 was built then deleted).
- The Playwright harness cannot drive Blazor file uploads; plan a test-only seam up front if upload flows must be automated, or accept them as honest manual residue.

### Cost Observations
- Model mix: planning/orchestration on Opus; executors + researcher + verifier on Sonnet (per `model_profile: balanced`).
- Notable: the per-field upcaster + golden-test projector patterns made the largest themes (format + export) low-risk and verifiable without live AI calls.

## Cross-Milestone Trends

| Milestone | Phases | Plans | Shipped | Notable |
|-----------|--------|-------|---------|---------|
| v1.3 Production-Ready & Format Maturity | 4 | 39 | 2026-06-05 | First automated UAT harness; backlog→phase promotion |
| v1.4 Recipe Data & Interoperability | 5 | 18 | 2026-06-25 | Schema-bump-first + pure display-only projectors; harness can't drive Blazor uploads (UATAUTO-02 partial) |
