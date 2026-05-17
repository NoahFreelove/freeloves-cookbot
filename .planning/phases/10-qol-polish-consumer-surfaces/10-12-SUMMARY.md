---
phase: 10-qol-polish-consumer-surfaces
plan: 12
subsystem: web-ui
tags: [icon, moon-glyph, accent-picker, localstorage, edit-profile, topbar]
requirements: [POLISH-03, QOL-05]

dependency_graph:
  requires:
    - v1.2 DS-02 accent CSS variants (terracotta, sage already in cookbot-design.css)
    - cookbot-shell.js setAccent function (pre-existing)
    - CbRadio atom (pre-existing)
    - IJSRuntime injection in EditProfile (pre-existing at line 17)
  provides:
    - Icon.Names.Moon constant + crescent SVG path
    - Dark-mode toggle renders Moon when dark, Sun when light
    - cookbot-shell.js applyDefaults reads localStorage.cookbot_accent before first paint
    - EditProfile Accent color card at W-05 ordinal position #3
  affects:
    - All pages that use TopBar (dark-mode toggle icon appearance)
    - EditProfile page layout (new card at position #3)
    - Page load accent initialization (cookbot-shell.js)

tech_stack:
  added: []
  patterns:
    - localStorage UI preference without EF migration (same as density/dark-mode pattern)
    - Icon.razor name→path switch dispatch (existing pattern)
    - CbRadio grouped radio with CurrentValue/CurrentValueChanged pattern

key_files:
  created: []
  modified:
    - src/CookBot.Web/Components/Atoms/Icon.razor
    - src/CookBot.Web/Components/Layout/TopBar.razor
    - src/CookBot.Web/wwwroot/js/cookbot-shell.js
    - src/CookBot.Web/Components/Pages/EditProfile.razor

decisions:
  - "Moon SVG path: classic crescent `M21 12.79A9 9 0 1 1 11.21 3 7 7 0 0 0 21 12.79z` matching Sun stroke-1.6 weight"
  - "Accent bootstrap replaces hardcoded orange= with full localStorage.getItem with allow-list validation"
  - "Accent card inserted at W-05 ordinal position #3 per card-order contract (after Account password, before AI features)"
  - "No save button on accent picker — instant persistence on radio change matches dark-mode toggle pattern"

metrics:
  duration: "4 minutes"
  completed_date: "2026-05-17"
  tasks_completed: 3
  tasks_total: 3
  files_modified: 4
  files_created: 0
---

# Phase 10 Plan 12: Moon Glyph + Accent Picker Summary

**One-liner:** Moon icon constant + crescent SVG added to Icon.razor; dark-mode toggle swaps Sun/Moon; accent picker (Default/Terracotta/Sage) added to EditProfile at W-05 ordinal #3 with localStorage persistence before first paint.

## Tasks Completed

| Task | Name | Commit | Files |
|------|------|--------|-------|
| 1 | Add Moon constant + SVG path; swap TopBar dark-mode icon | 9dfb04f | Icon.razor, TopBar.razor |
| 2 | Extend applyDefaults to read cookbot_accent from localStorage | e15323a | cookbot-shell.js |
| 3 | Add Accent color card to EditProfile at ordinal position #3 | 494e0fa | EditProfile.razor |

## What Was Built

### Task 1 — Moon glyph + TopBar icon swap (POLISH-03)

`Icon.razor` gains `public const string Moon = "moon"` constant (inserted after `Sun` in the `Names` static class) and a new switch arm `"moon" => "<path d=\"M21 12.79A9 9 0 1 1 11.21 3 7 7 0 0 0 21 12.79z\"/>"` (inserted after the `sun` arm). The classic crescent path matches Sun's stroke-1.6 outline weight.

`TopBar.razor` line 71 now renders `<Icon Name="@(_isDarkMode ? Icon.Names.Moon : Icon.Names.Sun)" Size="16" />` — the dark-mode button shows the Moon when dark, Sun when light.

### Task 2 — accent bootstrap in cookbot-shell.js (QOL-05)

`applyDefaults` previously hardcoded `data-accent="orange"`. Now it reads `localStorage.getItem("cookbot_accent")` with an allow-list `orange|terracotta|sage`, defaulting to `orange` on invalid/missing values. The try/catch handles privacy mode and prerender. The existing `setAccent` function is reused for live updates from EditProfile.

### Task 3 — Accent color card in EditProfile (QOL-05)

New `<CbCard>` inserted at W-05 ordinal position #3 (immediately after "Account password" `</CbCard>`, immediately before "AI features" `<CbCard>`). Contains three `<CbRadio TValue="string" GroupName="accent">` instances: Default / Terracotta / Sage.

`_accent = "orange"` field added to `@code`. In `OnAfterRenderAsync(firstRender)`, the stored accent is read from `localStorage` and validated. `OnAccentChanged` persists via `localStorage.setItem` and applies immediately via `cookbot.setAccent`. No EF migration — localStorage-only per QOL-05 + D-46-precedent.

## Deviations from Plan

None — plan executed exactly as written.

## Threat Surface Scan

No new network endpoints, auth paths, or trust boundary changes introduced. The accent value is allow-listed in both `applyDefaults` (JS) and `OnAccentChanged` (C#/JS), consistent with threat register T-10-12-01 mitigation. No XSS surface — value flows through `setAttribute("data-accent", v)`, not `innerHTML`.

## Known Stubs

None. All shipped functionality is fully wired.

## Self-Check: PASSED

- `src/CookBot.Web/Components/Atoms/Icon.razor` — Moon constant + SVG path present
- `src/CookBot.Web/Components/Layout/TopBar.razor` — `_isDarkMode ? Icon.Names.Moon : Icon.Names.Sun` present
- `src/CookBot.Web/wwwroot/js/cookbot-shell.js` — `cookbot_accent` + `terracotta` + `sage` allow-list present; density block preserved
- `src/CookBot.Web/Components/Pages/EditProfile.razor` — Accent color eyebrow, 3 CbRadio instances, OnAccentChanged, W-05 ordinal check (Account password=63 < Accent color=95 < AI features=110)
- Migration count: 15 (unchanged)
- Build: 0 errors, 4 warnings (pre-existing EF1002 warning; unrelated to this plan)
- Commits: 9dfb04f, e15323a, 494e0fa — all verified in git log
