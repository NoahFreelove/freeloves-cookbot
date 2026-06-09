---
phase: 11-v1.3-uat-cleanup
plan: "03"
subsystem: web-shell
tags: [css, layout, sidebar, height-fix]
dependency_graph:
  requires: ["11-02"]
  provides: ["CLEANUP-03"]
  affects: ["cookbot-design.css", "MainLayout.razor"]
tech_stack:
  added: []
  patterns: ["CSS Grid explicit row sizing", "height:100vh on grid container"]
key_files:
  created: []
  modified:
    - src/CookBot.Web/wwwroot/css/cookbot-design.css
    - src/CookBot.Web/Components/Layout/MainLayout.razor
decisions:
  - "Added grid-template-rows:1fr to .cb-shell — the correct fix for the collapsing grid row, not an overflow mask"
  - "Changed CSS height from 100% to 100vh to own the viewport height in the CSS rule"
  - "Removed redundant inline style=height:100vh from MainLayout now that CSS rule owns it"
metrics:
  duration: "~5 minutes"
  completed: "2026-06-05"
requirements: [CLEANUP-03]
---

# Phase 11 Plan 03: CLEANUP-03 Sidebar Height/Grid Fix Summary

**One-liner:** Added `grid-template-rows:1fr` and `height:100vh` to `.cb-shell` so the grid row fills the full viewport, ending sidebar Profile-row clip and cream-bg gap.

## Tasks Completed

| Task | Name | Commit | Files |
|------|------|--------|-------|
| 1 | Fix .cb-shell height/grid inheritance | ddad633 | cookbot-design.css, MainLayout.razor |

## What Was Done

### Root Cause

`.cb-shell` had `display:grid; height:100%` (CSS) with an inline `style="height:100vh"` on the wrapper div. The inline style made the grid container `100vh` tall, but because no `grid-template-rows` was defined, the single implicit grid row auto-sized to its content height. The grid items (sidebar `<aside>` and `<main>`) were only as tall as their content, not the full viewport. The `flex:1` spacer in the sidebar couldn't push the Profile NavRow to the bottom of `100vh` — it only pushed to the content height.

### Fix Applied

1. **cookbot-design.css `.cb-shell`**: Changed `height: 100%` to `height: 100vh` and added `grid-template-rows: 1fr`. The `1fr` row template causes the single grid row to expand to fill the entire container height, so `.side` and the main column both reach the viewport bottom.

2. **MainLayout.razor `.cb-shell` wrapper**: Removed the redundant `style="height:100vh"` inline attribute — the CSS rule now owns the height and there is only one source of truth.

No `overflow:hidden`, `overflow:clip`, or other masking hacks were used. The `--cream` background is still applied to `.cb-shell` (line 227 in cookbot-design.css). The 11-02 responsive `@media (max-width:720px)` rules were not touched.

## Deviations from Plan

None — plan executed exactly as written.

## Automated Verifications (Self-Check)

- HEIGHT_OK: `grep -A8 ".cb-shell {"` confirms `height: 100vh` — PASSED
- NO_OVERFLOW_HACK: no `overflow: hidden|clip` in the `.cb-shell` rule — PASSED
- `--cream` still applied to `.cb-shell` (line 227) — PASSED
- 11-02 media query rules intact (12 rule matches) — PASSED
- `dotnet build src/CookBot.Web/CookBot.Web.csproj` succeeded (0 errors, 0 warnings)

## Pending Verification (Checkpoint — Orchestrator Must Confirm)

The following visual checks require a browser at http://localhost:7000/. The orchestrator should run `./run.sh` and verify:

1. **Profile row not clipped**: At the bottom of the left sidebar, the Profile row (User icon + "Profile" text) is fully visible — the icon is not clipped, the text reads "Profile" not "rofile".
2. **Cream background reaches sidebar bottom**: The `--cream` background of the main column extends all the way to the bottom of the sidebar — no strip of body background showing below the cream column.
3. **Collapsed state**: Toggle the sidebar (drawer/hamburger button) and confirm the same — no clipped row, no background gap in either state.

## Known Stubs

None.

## Threat Flags

None — CSS-only layout fix, no new network endpoints or auth paths.

## Self-Check: PASSED

- ddad633 exists in git log: confirmed
- cookbot-design.css modified with `height: 100vh` + `grid-template-rows: 1fr`
- MainLayout.razor inline height removed
- 11-02 responsive rules intact
- Build: 0 errors
