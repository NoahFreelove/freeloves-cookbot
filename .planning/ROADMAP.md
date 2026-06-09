# Roadmap: FreelovesCookBot

## Milestones

- ✅ **v1.0 (pre-GSD existing app)** — codebase mapped in `.planning/codebase/`
- ⏸ **v1.1 Canonical Format & AI Conformance** — Phases 1–2 shipped 2026-04-25/26; Phase 3 absorbed into v1.2; Phase 4 deferred to v1.3+
- ✅ **v1.2 UI Redesign** — Phases 5–7 shipped 2026-04-27, 16 plans, 75/75 reqs ([archive](milestones/v1.2-ROADMAP.md))
- ✅ **v1.3 Production-Ready & Format Maturity** — Phases 8–11 shipped 2026-06-05, 39 plans ([archive](milestones/v1.3-ROADMAP.md))

## Phases

<details>
<summary>✅ v1.2 UI Redesign (Phases 5–7) — SHIPPED 2026-04-27</summary>

- [x] Phase 5: Foundation — Design tokens, atoms, shell, dialogs (5/5 plans) — completed 2026-04-27
- [x] Phase 6: Marquee surfaces — Home, Cooking Mode, Recipe View, Recipe Editor (4/4 plans, absorbs v1.1 EDITOR-01..07) — completed 2026-04-27
- [x] Phase 7: Remaining surfaces, accessibility, MudBlazor strip (7/7 plans + 2 post-ship slices) — completed 2026-04-27

Full details: [`milestones/v1.2-ROADMAP.md`](milestones/v1.2-ROADMAP.md) · [requirements](milestones/v1.2-REQUIREMENTS.md) · [audit](milestones/v1.2-MILESTONE-AUDIT.md)

</details>

<details>
<summary>⏸ v1.1 Canonical Format & AI Conformance (Phases 1–4) — partial</summary>

- [x] Phase 1: Canonical Format Foundation (4/4 plans) — completed 2026-04-25
- [x] Phase 2: AI Structured Output & Conformance (5/5 plans) — completed 2026-04-26
- [~] Phase 3: Editor UX Without Special Syntax — absorbed into v1.2 Phase 6 (EDITOR-01..07 → ED-03..09)
- [→] Phase 4: Format-Driven New Field & Cleanup — deferred to v1.3+ (FUTURE-V1.1-01..05)

Phase artifacts remain in `.planning/phases/01-canonical-format-foundation/` and `.planning/phases/02-ai-structured-output-conformance/` (load-bearing for v1.2 surfaces).

</details>

<details>
<summary>✅ v1.3 Production-Ready & Format Maturity (Phases 8–11) — SHIPPED 2026-06-05</summary>

- [x] Phase 8: Format Foundation (13/13 plans) — V2→V3 canonical schema bump, LegacyRecipeProjector deletion, TagsJson→RecipeTag, prompt-snapshot test, README format section — completed 2026-05-16
- [x] Phase 9: Photos + Prod-Ready Infrastructure (7/7 plans) — file upload + paste-URL safety, Docker + compose, encrypt-at-rest API key, token-cost telemetry, README deploy docs — completed 2026-05-16
- [x] Phase 10: QOL, Polish & Consumer Surfaces (14/14 plans) — scored pantry-match, AI-Chat raw-edit recovery, accent picker, prompt editor, token-cost widget, 5 polish items — completed 2026-05-17
- [x] Phase 11: v1.3 UAT Cleanup & Automated UAT Harness (5/5 plans) — CLEANUP-01..04 (Edit clip, responsive ≤720px, sidebar, unit-system display conversion) + reusable Playwright UAT harness — completed 2026-06-05

Full details: [`milestones/v1.3-ROADMAP.md`](milestones/v1.3-ROADMAP.md) · [requirements](milestones/v1.3-REQUIREMENTS.md)

</details>

## Progress

| Phase | Milestone | Plans Complete | Status | Completed |
|-------|-----------|----------------|--------|-----------|
| 1. Canonical Format Foundation | v1.1 | 4/4 | Complete | 2026-04-25 |
| 2. AI Structured Output & Conformance | v1.1 | 5/5 | Complete | 2026-04-26 |
| 3. Editor UX Without Special Syntax | v1.1 → v1.2 | 0/8 | Absorbed into v1.2 Phase 6 | — |
| 4. Format-Driven New Field & Cleanup | v1.1 → v1.3+ | 0/TBD | Deferred | — |
| 5. Foundation — Design tokens, atoms, shell, dialogs | v1.2 | 5/5 | Complete | 2026-04-27 |
| 6. Marquee surfaces — Home, CookingMode, RecipeView, RecipeEditor | v1.2 | 4/4 | Complete | 2026-04-27 |
| 7. Remaining surfaces, accessibility, MudBlazor strip | v1.2 | 7/7 | Complete | 2026-04-27 |
| 8. Format Foundation | v1.3 | 13/13 | Complete   | 2026-05-16 |
| 9. Photos + Prod-Ready Infrastructure | v1.3 | 7/7 | Complete   | 2026-05-16 |
| 10. QOL, Polish & Consumer Surfaces | v1.3 | 14/14 | Complete    | 2026-05-17 |
| 11. v1.3 UAT Cleanup & Automated UAT Harness | v1.3 | 5/5 | Complete   | 2026-06-05 |

---

*v1.2 milestone archived 2026-05-15. v1.3 roadmap created 2026-05-15 — 3 phases (8–10), 63 requirements mapped. See `.planning/MILESTONES.md` for the historical record.*

## Backlog

### Phase 999.1: RecipeView Cook button missing — TopBarService navigation race ✅ RESOLVED 2026-05-23

**Goal:** Fix `CbTopBarService` so the TopBar.RightSlot survives a route change to a page that re-sets the slot in `OnInitialized` (RecipeView, RecipeEditor).
**Status:** Resolved 2026-05-23 — see commit history.

**Reproducer (now fixed):** Generate a recipe in AiChat → save → navigate to `/recipes/{id}` (RecipeView). Cook / Edit / Share / Schedule buttons were absent from both `TopBar.RightSlot` (≥721px viewport) and the inline `.recipe-actions-inline-fallback` row (≤720px viewport).

**Actual root cause** (opposite of original hypothesis): Diagnostic traces showed `NavigationManager.LocationChanged` fires ~4ms *AFTER* the new page's `OnInitialized` returns, not before. The original D-57 auto-clear was wiping the slot the new page had just set. Fix: `SetRightSlot` now stamps the URL it was called at, and `HandleLocationChanged` preserves the slot when the destination URL matches the stamp (slot belongs to this page); clears only when URL differs (stale slot from prior page).

> **999.2, 999.3, 999.4, 999.5 promoted to Phase 11** (2026-06-05, `/gsd-progress --next`).
> Full reproducers, suspects, and notes preserved in
> `phases/11-v1.3-uat-cleanup/11-BACKLOG-SOURCE.md` and summarized as Phase 11
> success criteria above (CLEANUP-01..04). Only the resolved 999.1 record is kept here.

