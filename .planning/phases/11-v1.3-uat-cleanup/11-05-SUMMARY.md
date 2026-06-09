---
phase: 11-v1.3-uat-cleanup
plan: "05"
subsystem: test-tooling
tags: [playwright, node, uat, browser-test, e2e, uatauto-01]
dependency_graph:
  requires: []
  provides: [UATAUTO-01 harness, UAT Test 5 automated, UAT Test 7 automated]
  affects: [tests/uat-harness]
tech_stack:
  added: [playwright@1.55.1, Node.js ESM harness]
  patterns: [isolated Node test tree, snap chromium executablePath, trusted-LAN session via sessionStorage]
key_files:
  created:
    - tests/uat-harness/package.json
    - tests/uat-harness/package-lock.json
    - tests/uat-harness/README.md
    - tests/uat-harness/run.mjs
    - tests/uat-harness/lib/app.mjs
    - tests/uat-harness/lib/session.mjs
    - tests/uat-harness/tests/test5-reparenting.mjs
    - tests/uat-harness/tests/test7-responsive.mjs
    - tests/uat-harness/tests/test4-validation-fail.mjs
  modified:
    - .gitignore
decisions:
  - "Assume app already running on :7000 (harness polls /healthz, does not spawn run.sh) — simplest path; README documents startup requirement"
  - "Use /snap/bin/chromium via executablePath (verified working at build time); auto-fallback to Playwright-bundled browser in run.mjs if snap confinement fires"
  - "Pin playwright@1.55.1 in package.json (fixes GHSA-7mvr-c777-76hp SSL cert advisory); lock via package-lock.json (T-11-01 mitigation)"
  - "Test 4: SKIP/deferred — RawRecipeEditorDialog cannot be triggered on happy path; honest skip result, never faked as PASS"
  - "Test 5 uses native <select class='cb-select'> CbSelect interaction; finds cookbook select in aside to avoid UI ambiguity"
  - "Test 7 isSingleTrack() helper interprets browser-resolved grid-template-columns (browser resolves '1fr' to pixel value like '719px', single token = single column)"
metrics:
  duration: "~30 minutes"
  completed: "2026-06-05T20:45:00Z"
  tasks_completed: 2
  files_created: 9
  files_modified: 1
---

# Phase 11 Plan 05: UATAUTO-01 — Playwright/Node UAT Harness Summary

**One-liner:** Playwright/Node harness (playwright@1.55.1, snap chromium) drives Phase 10 UAT Tests 5 (cookbook reparenting) and 7 (responsive collapse) with real assertions; Test 4 honestly recorded as skip/deferred.

## What Was Built

A fully isolated `tests/uat-harness/` tree (own `package.json`, gitignored `node_modules`) that:

1. **Polls `/healthz` for app readiness** — `lib/app.mjs` waits up to 60s before failing
2. **Establishes a trusted-LAN session** — `lib/session.mjs` opens the app root, lets the default "Home Chef" user auto-load (MainLayout.OnInitialized auto-creates it; no password required), waits for the TopBar circuit signal
3. **Drives Test 5 (cookbook reparenting)** — `tests/test5-reparenting.mjs` navigates to `/recipes/{id}/edit`, reads the CbSelect current value (origin cookbook), selects a different cookbook, clicks Save, asserts navigation to `/cookbooks/{destId}`, asserts recipe appears on destination page, asserts recipe absent from origin page
4. **Drives Test 7 (responsive collapse)** — `tests/test7-responsive.mjs` sets 719px viewport, navigates to `/recipes/{id}`, asserts `.topbar-right-slot` hidden, `.recipe-actions-inline-fallback` visible, "Edit" text present in fallback (CLEANUP-01), `.recipe-hero` single-column (CLEANUP-02 grid collapse)
5. **Records Test 4 as SKIP/deferred** — `tests/test4-validation-fail.mjs` returns `status:'skipped'` with a documented reason; never faked as PASS
6. **run.mjs** — top-level orchestrator: readiness → session → Test 5 → Test 7 → Test 4 → summary table → `process.exit(1)` on any FAIL

## Chromium Launch Path

**System snap chromium confirmed working: `/snap/bin/chromium` via `executablePath`.**

Smoke test (2026-06-05): `chromium.launch({ executablePath: '/snap/bin/chromium', headless: true, args: ['--no-sandbox', '--disable-setuid-sandbox', '--disable-dev-shm-usage'] })` → launched, navigated `about:blank`, closed cleanly. Both playwright@1.50.1 and @1.55.1 verified.

`run.mjs` auto-falls back to Playwright-bundled chromium (no `executablePath`) if the snap launch throws.

## Pending Verification

**The checkpoint (Task 3 in the plan) cannot be self-approved — the orchestrator must run the harness against the live app after all CLEANUP fixes have landed.**

### How to run (for the orchestrator)

1. Ensure Plan 11-02 (CLEANUP-01/02) has been executed (Test 7 fully asserts those fixes).
2. Start the app if not already running:
   ```sh
   # From project root:
   ./run.sh
   ```
3. In a second terminal:
   ```sh
   cd tests/uat-harness
   npm install   # if not already done (installs playwright 1.55.1)
   npm test      # or: node run.mjs
   ```
4. Confirm output:
   - `UAT Test 5: PASS`
   - `UAT Test 7: PASS`
   - `UAT Test 4: SKIP` (with the deferred note — this is expected and correct)
5. Confirm exit code 0: `echo $?`
6. Optionally break a selector to confirm FAIL yields non-zero exit.

### What a passing run looks like

```
UAT HARNESS RESULTS
────────────────────────────────────────────────────────────
UAT Test 5: PASS
UAT Test 7: PASS
UAT Test 4: SKIP
  -> UAT Test 4 (validation-fail): SKIP — manual/deferred. ...
────────────────────────────────────────────────────────────
RESULT: PASS — 2 passed, 1 skipped, 0 failed.
```

### If Test 5 skips (not fails)

Test 5 returns `skipped` (not `failed`) if the session user has fewer than 2 cookbooks or if no recipe exists. **Seed the database before running:** create Home Chef, create 2 cookbooks, create 1 recipe in one of them. The harness auto-discovers the first recipe via a `/recipes/{id}` link on the home page.

### If Test 7 fails on CLEANUP-02 assertion

Test 7 asserts `.recipe-hero` is single-column at 719px — this requires Plan 11-02 (CLEANUP-02) to be applied. If 11-02 has not landed yet, `.recipe-hero` will still be `1fr 1fr` (two tokens) and Test 7 will FAIL on that assertion. Run 11-02 first.

## Deviations from Plan

### Auto-fixed Issues

None — plan executed as written.

### Package version bump (Rule 2 — security)

- **Found during:** Task 1 install
- **Issue:** `npm audit` reported `playwright < 1.55.1` vulnerable to GHSA-7mvr-c777-76hp (SSL cert not verified during browser download). The plan specified 1.50.1.
- **Fix:** Bumped `package.json` to `playwright@1.55.1` (first non-vulnerable release). This also closes T-11-01 (threat model: "pin a version in package.json"). Smoke test confirmed 1.55.1 launches snap chromium identically.
- **Files modified:** `tests/uat-harness/package.json`, `tests/uat-harness/package-lock.json`

## Isolation Confirmed

- `grep -c "uat-harness" FreelovesCookBot.sln` = 0 (not in .NET solution)
- `grep -i "playwright|selenium" tests/CookBot.Tests/CookBot.Tests.csproj` = no match (CookBot.Tests stays bUnit-only)
- `tests/uat-harness/node_modules/` gitignored via `.gitignore` append

## Known Stubs

None — the harness does not render UI or wire data to a display surface.

## Threat Flags

| Flag | File | Description |
|------|------|-------------|
| threat_flag: supply-chain | tests/uat-harness/package.json | playwright@1.55.1 pulled from npm; verified first-party Microsoft package; version locked in package-lock.json (T-11-01 mitigate disposition applied) |

## Self-Check

### Files exist:
- [x] tests/uat-harness/package.json
- [x] tests/uat-harness/README.md
- [x] tests/uat-harness/run.mjs
- [x] tests/uat-harness/lib/app.mjs
- [x] tests/uat-harness/lib/session.mjs
- [x] tests/uat-harness/tests/test5-reparenting.mjs
- [x] tests/uat-harness/tests/test7-responsive.mjs
- [x] tests/uat-harness/tests/test4-validation-fail.mjs
- [x] .gitignore (modified)

### Commits:
- b48924d: feat(11-05): scaffold UAT harness — package.json, .gitignore, app + session libs
- 90a499b: feat(11-05): UAT test modules + runner — Tests 5, 7 automated; Test 4 honest skip
- 3421786: chore(11-05): commit package-lock.json for uat-harness (pins playwright 1.55.1)

## Self-Check: PASSED
