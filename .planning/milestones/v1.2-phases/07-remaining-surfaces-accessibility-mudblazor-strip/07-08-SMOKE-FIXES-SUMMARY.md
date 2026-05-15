---
phase: 7
slice: 08
slice_name: "v1.2 manual-smoke bug-fix sweep"
milestone: v1.2
status: complete
bugs_total: 12
bugs_fixed: 12
completed: 2026-04-27
commits: [b2df97b, 5258dd5, 98fbf5a, d1755e3, 55c4b15, bfe73fe]
---

# Phase 7 Slice 08: v1.2 Manual-Smoke Bug-Fix Sweep

Six atomic commits fixed the 12 issues that surfaced during the manual smoke pass after the v1.2 milestone shipped. `dotnet build` clean (0/0) and `dotnet test --filter "Category!=RequiresApiKey"` 196/196 preserved across every commit. Repo-wide `Mud[A-Z]` grep still returns zero hits — no regressions on the strip.

## Bug-fix index

| # | Symptom | Root cause | Fix | Commit |
|---|---------|-----------|-----|--------|
| 1 | RecipeView had no Edit affordance | Plan 06-03 only wired Share + Cook this; Edit was missing | Added a `<CbButton Variant="Ghost" StartIcon="Pencil">` next to Share that navigates to `/recipes/{id}/edit` | 98fbf5a |
| 2 | Ingredient sidebar showed only quantities, no names | Defensive — qty column had a fixed 64px and the name span had no `flex:1`/`min-width:0`, so empty `Name` values disappeared silently and long names risked clipping | Switched the row to `flex:0 0 64px` + `flex:1; min-width:0`; empty `Name` now renders "(unnamed)" | 98fbf5a |
| 3 | Scale control labelled just "Scale" | Wording from initial design pass | Changed eyebrow to "Scale recipe" | 98fbf5a |
| 4 | Drag-drop image into editor wiped the recipe | Browser default behavior — dropping a file on the placeholder navigated the page to the file URL, which abandoned the in-progress edit | Wrapped the `StripedPlaceholder` in a div with `@ondragover:preventDefault` + `@ondrop:preventDefault` (also stopPropagation). Updated label to "photo · 4:3 (coming soon)" since the recipe entity has no photo column yet | d1755e3 |
| 5 | Hamburger menu didn't close the sidebar | TopBar rendered a static `<Icon Name="menu" />`; Plan 05-05 D-22 deferred the toggle wiring | Wired `OnToggleDrawer` EventCallback from TopBar → MainLayout `_drawerCollapsed` field; conditionally renders `<Sidebar>`; CSS class `.cb-shell.is-collapsed` collapses the grid template column to 0; preference persisted in `localStorage.cookbot_drawer_collapsed` and restored on first render | 5258dd5 |
| 6a | Dark-mode primary/accent button text invisible | `.cb-btn` used `var(--ink)` bg + `var(--cream)` text; both tokens swap in dark mode, which made the button match the surrounding cooking-mode-style fixed-dark surfaces (text and bg both ended up dark-on-dark) | Use **literal hex colors** for `.cb-btn` text/bg in both themes — `#231A0E` cocoa fill + `#FBF6E7` cream text in light mode; explicit `#2A2018` + `#EFEBE9` overrides in `body.dark-mode` | b2df97b |
| 6b | Cooking Mode "crazy bright" in dark mode | The page used `background: var(--ink)` which flipped to `#EFEBE9` in dark mode | Replaced every `var(--ink)`/`var(--cream)` reference in CookingMode.razor (and the PromptBuilder dark mono panel) with literal cocoa hexes (`#1A1410` bg, `#FBF6E7` text). These surfaces are intentionally fixed-dark in BOTH themes per design intent | b2df97b |
| 7 | CookBot logo in sidebar wasn't clickable | Plan 05-05 / Plan 07-05 never wrapped the logo block in a navlink | Wrapped the `cb` tile + "CookBot" wordmark in `<a href="/">` with `text-decoration:none; color:inherit;` so the visual is identical | 5258dd5 |
| 8 | Cook Mode font contrast too low | Many text elements used `rgba(255,255,255,0.5..0.6)` which falls below WCAG AA on the cocoa background | Bumped step body to `0.92`, step heading to full `#FFFFFF`, ingredient names to `0.85+` (referenced ingredients full white), eyebrow labels (Active timers / Ingredients / step label) bumped from `0.5` to `0.7..0.75` | b2df97b |
| 9 | Pause button reset the timer instead of pausing | `StopCurrentStepTimer` was bound to the Pause icon; there was no dedicated pause/resume state | Implemented Pause/Resume in C# state. New `_pausedTimers: Dict<string,int>` saves remaining seconds at pause; the timer stays in `_activeTimers` (right-rail keeps showing it, frozen) so the user sees what they paused. Pause toggles to a Resume button (Play icon) which re-arms the JS interval from the saved time. Added a separate **Stop** subtle-button. `+ 30s` is disabled while paused | 55c4b15 |
| 10 | "Ask about this step" not visible/usable in cooking mode | The button only rendered in the step-text branch (else-of-timerHero). When a timer was running, the user couldn't ask without first pausing | Extracted the AI panel into `RenderStepAiPanel()` render-fragment; both the timer-hero AND step-text branches now show "Ask about this step" + the inline panel. AI-off contract preserved (button hidden when `_aiCookAssistVisible=false`) | 55c4b15 |
| 11 | User-switcher just refreshed the current page | Order-of-operations bug: in-memory `UserService.CurrentUserId` was set BEFORE sessionStorage persisted, and `Navigation.NavigateTo(Navigation.Uri, forceLoad: true)` with the same URI sometimes no-ops in Blazor Server (router thinks nothing changed) | Three fixes: (a) write sessionStorage **first** so the post-reload boot reads the right value; (b) replace `NavigateTo(uri, forceLoad: true)` with `JS.location.reload()` for a guaranteed circuit rebuild; (c) bail early if the dropdown re-emits the already-selected user. AdminManageUsersDialog post-delete fallback got the same treatment | 5258dd5 |
| 12 | AI Generate Recipe failed with vague error | `MapToSanitizedSnackbarCopy` mapped most non-recoverable failures to the same generic copy ("Something went wrong with the AI…"), masking model-ID typos and provider-side issues | (a) Routing for 401/403/404/429/503/refusal/empty to actionable copy ("API key does not have permission for this model…", "AI rate limit hit…"); (b) **fallback** to raw sanitized error (truncated, prefixed "AI error:") so unknown errors surface verbatim — SecretRedactor at the IAiService boundary already redacted any API key from the input; (c) added a save-bar warn chip + status text recoloring when the canvas is in SanitizedError-only state | bfe73fe |

## Hard-invariant checks

| Invariant | Status |
|-----------|--------|
| `dotnet build` clean (0 warnings, 0 errors) | PASS — verified after every commit |
| `dotnet test --filter "Category!=RequiresApiKey"` 196/196 | PASS — baseline preserved |
| Dark-mode toggle wiring (`cookbot_dark_mode` localStorage + `body.dark-mode` class) | PASS — untouched in MainLayout; only the underlying CSS rules changed |
| `grep -rn "Mud[A-Z]" src/ tests/` returns zero hits | PASS — no MudBlazor reintroduced |
| Canonical `RecipeDocument` round-trip | PASS — no schema changes; defensive name fallback in RecipeView is render-only |
| AI-off contract (UserProfile.AiEnabled hides AI surfaces) | PASS — `_aiCookAssistVisible` gating preserved on the new RenderStepAiPanel; no new AI surfaces added |
| SecretRedactor (v1.1 AI-07) redacts API keys from user-visible errors | PASS — redaction happens inside AnthropicAiService before the error reaches AiChat; the new "AI error: {raw}" fallback consumes the already-redacted string and does not bypass any sanitization layer |

## Files touched

| File | Change |
|------|--------|
| `src/CookBot.Web/wwwroot/css/cookbot-design.css` | `.cb-btn` literal hex colors + dark-mode overrides + `.cb-shell.is-collapsed` rule |
| `src/CookBot.Web/Components/Layout/MainLayout.razor` | `_drawerCollapsed` state + restore-from-localStorage + conditional Sidebar render + `OnToggleDrawer` |
| `src/CookBot.Web/Components/Layout/TopBar.razor` | Hamburger button (was static Icon) + `OnToggleDrawer` callback + user-switcher reorder + location.reload |
| `src/CookBot.Web/Components/Layout/Sidebar.razor` | Logo block wrapped in `<a href="/">` |
| `src/CookBot.Web/Components/Pages/RecipeView.razor` | Edit button + ingredient row flex layout + scale label + (unnamed) fallback |
| `src/CookBot.Web/Components/Pages/RecipeEditor.razor` | Drag-drop event prevention on the photo placeholder |
| `src/CookBot.Web/Components/Pages/CookingMode.razor` | Literal cocoa hexes for fixed-dark surface + Pause/Resume state machine + RenderStepAiPanel render-fragment + contrast bumps |
| `src/CookBot.Web/Components/Pages/PromptBuilder.razor` | Literal cocoa hexes for the dark mono prompt panel |
| `src/CookBot.Web/Components/Pages/AiChat.razor` | MapToSanitizedSnackbarCopy expanded routing + raw-error fallback + save-bar warn chip on `Validation: null, SanitizedError: not null` |

## Carried forward (none)

All 12 reported bugs landed. No deferred items. No new architectural changes — every fix was inline (Rules 1–3) within the existing component shapes. The shell drawer-collapse mechanism is the closest thing to a structural change but it composes on top of existing tokens and conditional rendering; no new atom was added.

## Notes for future work

- **Bug 6a** root cause is a general pattern: `var(--ink)` and `var(--cream)` swap between themes, so any UI element that's intentionally "fixed-dark" (cooking mode hero, prompt builder mono panel, recipe-share screenshots) needs to use literal hex colors instead. The dark-mode block at the bottom of `cookbot-design.css` could grow a `--ink-fixed` / `--cream-fixed` pair as a v1.3 cleanup if more fixed-dark surfaces appear.
- **Bug 12** would benefit from a real "test connection" probe in Profile that surfaces the same `MapToSanitizedSnackbarCopy` output before the user hits the AI Chat surface — but that's UX scope for a later slice.
- **Bug 9** uses an in-process `_pausedTimers` dict; if a user switches steps while a timer is paused on the prior step, the pause state is currently lost (the dict is keyed by timerId so it persists across step nav, but the UI's `isPaused` check only runs against `_currentStepTimerId`). For v1.2 this matches the existing single-active-timer-per-step UX; multi-step pause-tracking is FUTURE.
