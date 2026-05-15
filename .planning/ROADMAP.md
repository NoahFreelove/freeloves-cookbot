# Roadmap: FreelovesCookBot

## Milestones

- ✅ **v1.0 (pre-GSD existing app)** — codebase mapped in `.planning/codebase/`
- ⏸ **v1.1 Canonical Format & AI Conformance** — Phases 1–2 shipped 2026-04-25/26; Phase 3 absorbed into v1.2; Phase 4 deferred to v1.3+
- ✅ **v1.2 UI Redesign** — Phases 5–7 shipped 2026-04-27, 16 plans, 75/75 reqs ([archive](milestones/v1.2-ROADMAP.md))
- 📋 **v1.3** — not yet planned; phase candidate drafted at `.planning/v1.3-PHASE-CANDIDATE-recipe-photos.md`

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

### 📋 v1.3 (not yet planned)

Next milestone is empty. Seed material:
- `.planning/v1.3-PHASE-CANDIDATE-recipe-photos.md` — paste-URL recipe photos (IMG-01..IMG-09); the first format-driven slice for v1.3 (exercises the V1→V2 upcaster pattern with a nullable field before per-step temperature stress-tests it for nested arrays)
- `FUTURE-V1.1-01..05` — per-step temperature, tags relational, LegacyRecipeProjector cleanup, prompt snapshot test, README format section
- `FUTURE-01..09, FUTURE-11..15` — encrypt-at-rest API key, token-cost telemetry, format extensions, Schema.org / Cooklang export, USDA nutrition, tool-use fallback, per-sharer consent banner, accent variant picker, AiChat raw-edit hardening
- `FUTURE-13` — smart pantry-match algorithm (replaces v1.2 deterministic stub)
- `FUTURE-14` — user-facing accent variant picker (terracotta/sage)
- `DEFERRED-PROF-AIPROMPT` — AiChat assistant-instructions editor on Profile
- 6 Phase 6 + 7 design tech-debt items recorded in v1.2 audit

Run `/gsd-new-milestone v1.3` to scaffold (questioning → research → requirements → roadmap).

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

---

*v1.2 milestone archived 2026-05-15. See `.planning/MILESTONES.md` for the historical record.*
