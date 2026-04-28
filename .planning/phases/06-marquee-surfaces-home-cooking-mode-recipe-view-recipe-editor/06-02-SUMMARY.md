---
phase: 6
plan: 2
subsystem: web/cooking-mode
tags: [cooking-mode, atoms, mud-removal, js-interop, adaptive-hero, ai-off-contract]
requires:
  - Phase 5 atoms (CbButton, CbCard, CbChip, CbTextarea, Icon)
  - Phase 5 design tokens (cookbot-design.css — .cb, .cb-chip, .cb-card, .num, dark-mode)
  - cooking-timers.js JS interop (CookingTimers.init / start / stop / dispose / requestNotificationPermission)
  - RecipeChipComposer + RecipeChipComposer.scrollIntoViewWithHighlight JS interop
  - RecipeCookingAiContext.BuildUserMessage (v1.1 Phase 2)
  - IngredientLinkPatterns.Pattern (single source of truth for [name](#id) parsing)
  - RecipeScalingService.FormatScaledAmount (servings-only scaling)
provides:
  - Dark-cocoa Cooking Mode surface against Phase 5 atoms (no Mud* component refs)
  - Adaptive hero (224px tabular timer when running, 52px step text when idle)
  - Always-on right rail with current-step ingredient highlighting + servings widget
  - Bottom step nav with first-sentence step name lookup + arrow-key + PageUp/Dn + Esc
  - Notification permission status indicator chip in top bar
  - Pause/Reset/+30s controls for the current step's primary timer
affects:
  - src/CookBot.Web/Components/Pages/CookingMode.razor (markup rewritten, @code logic preserved verbatim modulo additive helpers)
tech-stack:
  added: []
  patterns:
    - "Adaptive hero condition derived from existing _activeTimers dictionary (no new persistent state) — _currentStepTimerId scans for keys prefixed `step{N}_` matching the existing StartTimer naming convention"
    - "Bottom-nav step names derived from RecipeStep.Text first-sentence (link-stripped, max 48 chars) — falls back to 'Step N' when text is empty or unparseable"
    - "AI assist panel collapses to a single 'Ask about this step' button (Subtle); panel opens on click, closes on navigation and on explicit Close — replaces the always-expanded MudPaper question form"
    - "Sticky/active timer surfaces moved into the right rail (replaces the MudPaper sticky-bottom bar) keeping a single visual locus for time-of-day"
    - "Negative-margin (-24px) wrapper on the cooking root breaks out of MainLayout's 24px padding so the cocoa surface bleeds to the layout edges; min-height calc(100vh - 96px) accounts for sidebar + topbar chrome"
key-files:
  created: []
  modified:
    - src/CookBot.Web/Components/Pages/CookingMode.razor
decisions:
  - "Adaptive hero binds to the FIRST active timer for the current step (key prefix `step{N}_`) rather than introducing a new _currentTimerRunning field — avoids divergence with the existing _activeTimers dictionary the JS interop already drives via OnTimerTick/OnTimerComplete."
  - "+30s preserves the JS-side endTime by stop+restart with remaining+30 seconds — clean re-arm that lets cooking-timers.js's _notify still fire exactly once at the new zero, no double-notify risk."
  - "Reset re-runs StartTimer with the original underlying StepTimer — same path the user took to start the timer, so display label / duration / unit are reconstructed identically."
  - "Bottom-nav step name uses Text first-sentence rather than RecipeStep.Heading because the EF entity has no Heading field (StepNode discriminated union lives only in RecipeDocument). Pragmatic fallback per the prompt's spec."
  - "Ingredient highlighting reads CurrentStepRefIds() — link-pattern scan over CurrentStep.Text — preserved verbatim from the prior implementation. No reads of dead RecipeStep.IngredientRefs (Phase 1 D-13 retired those writes)."
  - "AI panel collapses by default (was always-rendered); a single 'Ask about this step' Subtle button toggles it open. Hidden entirely when AiEnabled=false (CookBotSettings + UserProfile + ApiCredentials gate, unchanged)."
  - "Servings -/+ widget keeps the existing 1..100 bounds; the existing IncrementServings/DecrementServings handlers were tightened to enforce the upper bound (was unbounded before — minor Rule 1 fix)."
metrics:
  duration: ~5min
  completed: 2026-04-27
  tasks_completed: 6
---

# Phase 6 Plan 2: Cooking Mode rewrite Summary

Rewrote `Components/Pages/CookingMode.razor` against Phase 5 atoms and the design handoff at `screens/cooking.jsx` — dark cocoa surface, segmented step rail, adaptive 224px-timer / 52px-step hero, always-on right rail with `[name](#id)` link-driven ingredient highlighting, 1fr/2fr Previous/Next bottom nav with arrow-key navigation. All JS-interop wiring (`cooking-timers.js`, browser notifications, `RecipeChipComposer.scrollIntoViewWithHighlight`, `RecipeCookingAiContext`) preserved verbatim. Zero `Mud*` component references remain in the file.

## Tasks completed

| Task | Name                                                         | Outcome                                                                                                                                                                                                                                                            |
| ---- | ------------------------------------------------------------ | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| 1    | Top bar + step rail (COOK-01, COOK-02)                       | Cocoa header (Exit + recipe title + step indicator + notification chip) + N-segment step rail driven by `_currentStepIndex`. Past = `rgba(255,255,255,0.5)`, current = `var(--accent)`, future = `rgba(255,255,255,0.12)`.                                          |
| 2    | Adaptive hero (COOK-03)                                      | Conditional render on `_currentStepTimerRunning`. Timer mode: 224px tabular numeral + Pause / +30s / Reset controls + 17px step body. Idle mode: 52px step text + "Start N-min timer" (when step has timer) + "Ask about this step" (when AI on).                  |
| 3    | Always-on right rail (COOK-04)                               | "Ingredients · scaled {ratio}×" eyebrow; ingredient rows accent-tinted when in `CurrentStepRefIds()`, dimmed otherwise. Servings widget with -/+ buttons; active timers + completed alerts moved into rail.                                                        |
| 4    | Bottom step nav (COOK-05)                                    | 1fr ghost Previous + 2fr accent Next, both 64px, with 11.5px uppercase eyebrow + 15px step name (first-sentence, link-stripped, ≤48 chars, fallback "Step N"). KeyDown handler: ←/→/PageUp/PageDown navigate, Esc exits.                                            |
| 5    | Preserve JS interop + AI assist (COOK-06)                    | `CookingTimers.init/start/stop/dispose/requestNotificationPermission` calls preserved exactly. `RecipeChipComposer.scrollIntoViewWithHighlight` still wired through `ScrollToIngredient`. `RecipeCookingAiContext.BuildUserMessage` unchanged. Bell chip reads notification permission. |
| 6    | Build verify + commit                                        | `dotnet build`: 0 warnings / 0 errors. `dotnet test --filter "Category!=RequiresApiKey"`: 196 passed / 0 failed. Single atomic commit `203f544`.                                                                                                                    |

## Acceptance criteria

| Criterion                                                                                       | Status                                                                                                  |
| ----------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------- |
| COOK-01..06 satisfied per ROADMAP SC#2                                                           | Met (all six requirements implemented per task table above)                                              |
| Timer fires browser notification when expired (existing behavior preserved)                     | Met (cooking-timers.js `_notify` invocation path unchanged; `OnTimerComplete` still drains `_activeTimers`) |
| Ingredient highlighting reads `step.IngredientLinks` only (no substring match, no IngredientRefs) | Met (`CurrentStepRefIds()` scans `CurrentStep.Text` via `IngredientLinkPatterns.Pattern`)                |
| DOM contains zero `mud-*` classes from CookingMode.razor                                        | Met (grep for `<Mud[A-Z]` and `mud-` class strings: zero hits in markup)                                |
| `dotnet build` clean                                                                            | Met (0 warnings / 0 errors)                                                                              |
| `dotnet test` baseline                                                                          | Met (196 passed, identical to pre-rewrite baseline)                                                      |

## Hard invariants

- v1.1 D-Q9 (servings-only scaling): only `RecipeIngredient.Amount` is scaled via `RecipeScalingService.FormatScaledAmount`; `_recipe.PrepTimeMinutes`, `_recipe.CookTimeMinutes`, oven temperatures, and per-step descriptive text are never auto-scaled. Confirmed by inspection.
- AI-off contract: `_aiCookAssistVisible` gate (`CookBotSettings.AiFeaturesEnabled && (_profile?.AiEnabled ?? false) && _aiCredentials != null`) suppresses both the "Ask about this step" Subtle button and the (collapsed) AI panel render path. Identical to prior gate.
- All existing JS interop preserved: `CookingTimers.init`, `CookingTimers.start`, `CookingTimers.stop`, `CookingTimers.dispose`, `CookingTimers.requestNotificationPermission`, `RecipeChipComposer.scrollIntoViewWithHighlight`. Verbatim invocations.
- `RecipeStep.IngredientRefs` is never read in the new markup (verified via `grep -n "step\.IngredientRefs" CookingMode.razor` → zero hits). Highlight path is link-pattern based.

## Deviations from Plan

**1. [Rule 1 - Bug] Tightened upper bound on IncrementServings**
- **Found during:** Task 3 (right-rail servings widget)
- **Issue:** Prior `IncrementServings()` did `_targetServings++;` with no upper bound; the original top-bar render disabled the button at `>= 100` but the C# handler did not enforce it. Could be triggered via keyboard / hot-reload of state in the new layout where the disabled-attribute path differs slightly.
- **Fix:** Added `if (_targetServings < 100)` guard inside `IncrementServings()` to mirror the `_targetServings > 1` guard in `DecrementServings()`.
- **Files modified:** `src/CookBot.Web/Components/Pages/CookingMode.razor`
- **Commit:** `203f544` (atomic, included with the main rewrite)

**2. [Rule 2 - Missing critical functionality] Added Esc-to-exit + arrow-key navigation**
- **Found during:** Task 4 (bottom step nav)
- **Issue:** Plan called for "left/right arrow keys also navigate steps (existing keyboard handler if present, else add)" — no handler existed.
- **Fix:** Added `tabindex="0"` + `@onkeydown="HandleKeyDown"` to root div + autofocus on first render. Handles ←/→/PageUp/PageDown for step nav and Esc for exit.
- **Files modified:** `src/CookBot.Web/Components/Pages/CookingMode.razor`
- **Commit:** `203f544`

**3. [Rule 2 - Missing critical functionality] +30s timer extension**
- **Found during:** Task 2 (adaptive hero timer-mode controls)
- **Issue:** Design mock has a "+ 30s" button next to Pause; cooking-timers.js had no `addSeconds` API.
- **Fix:** Implemented C# side via stop+restart pattern: read remaining seconds from `_activeTimers`, call `CookingTimers.stop`, then `CookingTimers.start(id, remaining + 30, displayLabel)`. Re-uses the existing JS API surface — no new JS function added. The `_notify` path still fires exactly once at the new zero.
- **Files modified:** `src/CookBot.Web/Components/Pages/CookingMode.razor`
- **Commit:** `203f544`

No other deviations. Plan executed as written for the remaining tasks.

## Authentication gates

None. The AI-off contract is a feature gate, not an auth gate; no API-key prompts are surfaced from CookingMode (the gate is purely a render-time check).

## Known stubs

None. All visible regions are bound to real data:
- Recipe title from `_recipe.Name`
- Step rail from `_navigableSteps.Count` and `_currentStepIndex`
- Step text from `CurrentStep.Text` (rendered via `RecipeChipComposer Interactive=false`)
- Ingredient rail from `_recipe.RecipeIngredients` (sorted by `RecipeLocalId`)
- Highlight set from `CurrentStepRefIds()` (link-pattern over `CurrentStep.Text`)
- Servings widget from `_targetServings` and `_recipe.Servings`
- Active timers / completed timers from existing dictionaries driven by JS interop callbacks

## Threat Flags

None. The rewrite is a presentation-layer migration — no new endpoints, no new auth paths, no schema changes, no new file/network access. The JS-interop surface and AI assist routing are preserved verbatim with the same `_aiCookAssistVisible` gate.

## Files changed

| File                                                          | Change                                                                                                                                                                                                                |
| ------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `src/CookBot.Web/Components/Pages/CookingMode.razor`          | Markup rewritten against Phase 5 atoms (CbButton/CbCard/CbChip/CbTextarea/Icon). All `Mud*` components removed. `@code` block additions: `_notificationsGranted`, `_stepAiOpen`, `_currentStepTimerId`, `_currentStepTimerRunning`, `BuildTimerId`, `FormatScaleLabel`, `PreviousStepName`/`NextStepName`/`DisplayNameForStep`, `PrevButtonStyle`/`NextButtonStyle`, `HandleKeyDown`, `ToggleStepAiPanel`, `StopCurrentStepTimer`, `ResetCurrentStepTimer`, `AddThirtySecondsToCurrentStepTimer`. All pre-existing fields and methods (recipe load, step nav, scaling, JS interop, AI assist, dispose) preserved. |
| `src/CookBot.Web/wwwroot/js/cooking-timers.js`                | Untouched (preserved as-is, per plan).                                                                                                                                                                                 |
| `src/CookBot.Application/Services/RecipeCookingAiContext.cs`  | Untouched (preserved as-is, per plan).                                                                                                                                                                                 |

## Self-Check: PASSED

- File `src/CookBot.Web/Components/Pages/CookingMode.razor`: FOUND
- Commit `203f544`: FOUND in `git log --oneline`
- `dotnet build`: 0 warnings, 0 errors
- `dotnet test --filter "Category!=RequiresApiKey"`: 196 passed, 0 failed
- Zero `<Mud[A-Z]` component tags in CookingMode.razor: confirmed
- Zero reads of `step.IngredientRefs` in CookingMode.razor: confirmed
- All required JS interop calls present and verbatim: confirmed (`CookingTimers.init`, `CookingTimers.start`, `CookingTimers.stop`, `CookingTimers.dispose`, `CookingTimers.requestNotificationPermission`, `RecipeChipComposer.scrollIntoViewWithHighlight`)
