---
phase: 11-v1.3-uat-cleanup
plan: "02"
subsystem: RecipeView CSS / responsive layout
tags: [cleanup, css, responsive, blazor]
dependency_graph:
  requires: []
  provides: [CLEANUP-01, CLEANUP-02]
  affects: [11-03, 11-04]
tech_stack:
  added: []
  patterns:
    - CSS class extraction from inline styles (targetable by media query)
    - flex-wrap:wrap on action row to prevent leading-child clip
    - Extending existing @media(max-width:720px) block (no second breakpoint)
key_files:
  modified:
    - src/CookBot.Web/wwwroot/css/cookbot-design.css
    - src/CookBot.Web/Components/Pages/RecipeView.razor
  created: []
decisions:
  - Moved all inline styles from .recipe-actions-inline-fallback div and the four
    grid containers to named CSS classes, keeping desktop layout byte-equivalent
  - Used flex-wrap:wrap + justify-content:flex-start (inside the 720px block) so
    all four action buttons wrap to a second line instead of flex-end clipping Edit
  - Reused the single existing @media(max-width:720px) breakpoint throughout —
    no second breakpoint introduced
metrics:
  duration: "~15 minutes"
  completed: "2026-06-05"
  tasks_completed: 2
  tasks_total: 3
  files_changed: 2
---

# Phase 11 Plan 02: CLEANUP-01 + CLEANUP-02 Summary

**One-liner:** flex-wrap + class-based grid collapse fix — Edit button no longer clips and RecipeView stacks to single column at <=720px.

## Tasks Completed

| Task | Name | Commit | Files |
|------|------|--------|-------|
| 1 | CLEANUP-01: stop inline fallback row clipping Edit | 2f8b675 | cookbot-design.css, RecipeView.razor |
| 2 | CLEANUP-02: responsive single-column collapse at <=720px | 2f8b675 | cookbot-design.css, RecipeView.razor |
| 3 | Checkpoint: human visual verify | — (deferred) | — |

Tasks 1 and 2 are a single CSS unit of work and were committed together.

## What Was Built

### CLEANUP-01 — Edit button clip fix

Root cause confirmed (Suspect A from PATTERNS): the `.recipe-actions-inline-fallback` div had inline `style="... justify-content:flex-end; ..."` with no `flex-wrap`. On a no-wrap `flex-end` flex row, when the total intrinsic width of the four buttons exceeds the container, the flex algorithm pushes the leading child (Edit) off the left edge — clipping it rather than the trailing items.

Fix: extracted the inline styles to a `.recipe-actions-inline-fallback { }` base rule in `cookbot-design.css` with `flex-wrap: wrap` added. Inside the `@media (max-width: 720px)` block the rule also switches to `justify-content: flex-start` so wrapping buttons stack from left. The `_topBarActions` RenderFragment (RecipeView.razor L244-249, four `<CbButton>` declarations) is byte-unchanged.

### CLEANUP-02 — Responsive single-column collapse

Added class hooks to RecipeView's four previously-inline-styled grid containers:
- `recipe-article` — article wrapper (was `style="max-width:1080px;margin:0 auto;padding:24px 32px 80px;"`)
- `recipe-hero` — 1fr 1fr hero grid (was `style="display:grid;grid-template-columns:1fr 1fr;..."`)
- `recipe-body-grid` — 300px 1fr body grid (was `style="display:grid;grid-template-columns:300px 1fr;..."`)
- `recipe-step-grid` — 40px 1fr step grid (was `style="display:grid;grid-template-columns:40px 1fr;..."`)

Base rules in CSS preserve the desktop layout exactly. Inside the **existing** `@media (max-width: 720px)` block (no new breakpoint):
- `.recipe-hero { grid-template-columns: 1fr; }` — hero stacks to single column
- `.recipe-body-grid { grid-template-columns: 1fr; gap: 32px; }` — ingredients + method full-width
- `.recipe-step-grid { grid-template-columns: 28px 1fr; gap: 12px; }` — step number column narrowed to stop per-word wrap
- `.recipe-article { padding: 16px 16px 64px; }` — tighter padding at narrow viewport
- `.cb-recipe-cap { font-size: 40px; }` — display cap reduced from 64px so title fits

## Automated Self-Checks

```
CSS_OK (Task 2 verify): all four class hooks found in RecipeView.razor; single @media(max-width:720px) block; no 600/640/768 breakpoints
flex-wrap: present at cookbot-design.css L727 in .recipe-actions-inline-fallback base rule
dotnet build: succeeded (0 warnings, 0 errors)
_topBarActions RenderFragment: byte-unchanged (git diff confirms no change to L244-249)
```

## Pending Verification (Checkpoint Task 3)

**Checkpoint type:** human-verify (visual/functional)

The orchestrator or a human reviewer must:

1. Start the app: `./run.sh` (binds `http://localhost:7000`)
2. Open any recipe page: `http://localhost:7000/recipes/1`
3. Resize the browser to **719px wide** (or use DevTools device toolbar)
4. **CLEANUP-01 check:** confirm the inline action row above the hero shows **four buttons — Edit, Share, Schedule, Cook this** — with Edit present and not clipped on the left edge (wrapping to a second line is acceptable)
5. **CLEANUP-02 check:** confirm the hero is a single column (title above the photo, not side-by-side), ingredients and method are full-width single column, and the recipe title does not overflow the right edge
6. Resize back to >=900px and confirm the desktop layout is unchanged: two-column hero, TopBar actions visible, inline action row hidden

**Resume signal:** Type "approved" or describe what is still clipped/squished.

## Deviations from Plan

None — plan executed exactly as written. Tasks 1 and 2 were committed as one atomic commit (they were one CSS unit of work on two files with no independent intermediate state).

## Threat Flags

None — changes are pure CSS/Razor render-layer. No new network endpoints, auth paths, file access patterns, or schema changes.

## Self-Check: PASSED

- `src/CookBot.Web/wwwroot/css/cookbot-design.css` — modified, committed 2f8b675
- `src/CookBot.Web/Components/Pages/RecipeView.razor` — modified, committed 2f8b675
- Commit 2f8b675 confirmed in `git log --oneline -3`
- `dotnet build` succeeded (0 errors, 0 warnings)
- `.recipe-actions-inline-fallback` base rule contains `flex-wrap: wrap` at L727
- `@media (max-width: 720px)` is the only max-width breakpoint in cookbot-design.css
- All four class hooks (`recipe-article`, `recipe-hero`, `recipe-body-grid`, `recipe-step-grid`) present in RecipeView.razor
