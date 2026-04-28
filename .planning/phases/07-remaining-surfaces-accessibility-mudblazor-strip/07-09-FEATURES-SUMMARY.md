---
phase: 7
slice: 09
slice_name: "scheduled recipes + persistent cooking state"
milestone: v1.2-followups
status: complete
features: 2
commits: [1da3081, b0ee018, 578ffa4, a90d2d7, 4b82843, c236007]
completed: 2026-04-27
---

# Phase 7 Slice 09: Scheduled Recipes + Persistent Cooking State

Two manual-smoke feature requests landed as six atomic commits. The placeholder "Up next" rows on Home are now backed by real `ScheduledRecipe` rows persisted in SQLite, the homepage surfaces in-progress cooking sessions and active long-running timers across page navigation/refresh, and the cooking-mode Finish button writes a `RecipeMade` log row that closes the v1.2 milestone's `FUTURE-Recently-Cooked` deferred item. `dotnet build` clean (0/0) and `dotnet test --filter "Category!=RequiresApiKey"` 196/196 preserved across every commit. Repo-wide `Mud[A-Z]` grep still returns zero hits.

## Feature 1 — Real "Up next" + long-running timers visible on Home

| Concern | How |
|---------|-----|
| Persistent schedule | New `ScheduledRecipe` entity (`Id`, `RecipeId`, `UserId`, `ScheduledFor`, `Notes`, `CreatedAt`) with index on `(UserId, ScheduledFor)`. EF migration `20260428004334_ScheduledRecipesAndRecipeMades`. |
| Service surface | `IScheduledRecipeService` in `CookBot.Web.Services` (alongside `AiApiKeyShareService` — Application is repository-only and EF `Include` is needed). `GetUpcomingAsync` / `ScheduleAsync` / `UnscheduleAsync`. Authz uses the same predicate as `UserCanAccessRecipeAsync`. |
| Schedule UI | `<ScheduleRecipeDialog>` opened from RecipeView's new Ghost "Schedule" button via `CbDialogService.ShowAsync<ScheduleRecipeDialog>`. `<input type="datetime-local">` + optional notes. Local-time wall-clock converted to UTC at submit; default value is "now + 1 hour" rounded to the nearest 15-minute mark. |
| Home Up-next | `Home.razor.cs` calls `GetUpcomingAsync(userId, 3)`; rows render with click-through to `/recipes/{id}`, friendly time formatting (today / tomorrow / weekday / Mmm d). Empty list keeps the original placeholder rows at `0.55` opacity with a hint to schedule. |
| Long-running timer surfaced on Home | New `cooking-session-state.js` module: `saveActiveTimer` / `readActiveTimer` (computes `remainingSeconds` from `startedAtIso + durationSeconds`; clears expired entries automatically) / `clearActiveTimer`. CookingMode's `StartTimer` writes the entry; `StopTimer` / `OnTimerComplete` / `ExitCooking` clear it. Home reads on first render and renders a fixed-dark cocoa band at the top with `Resume cooking` button. |

## Feature 2 — Mark-as-completed + persistent cooking-session state

| Concern | How |
|---------|-----|
| RecipeMade log entity | New `RecipeMade` entity (`Id`, `RecipeId`, `UserId`, `CompletedAt`, `Notes`) with two indexes — `(UserId, CompletedAt)` for the home recently-cooked feed and `(RecipeId, CompletedAt)` for the per-recipe last-cook callout / made-count. Same migration as Feature 1. |
| Service surface | `IRecipeMadeService` (`LogMadeAsync` / `GetMadeCountAsync` / `GetLastCookAsync` / `GetRecentForUserAsync`) in `CookBot.Web.Services`. |
| Finish button | Last step of CookingMode replaces the Next button with a Finish button (Check icon, "Mark as completed" label). Clicking it calls `RecipeMadeService.LogMadeAsync`, clears the localStorage entries, toasts "Recipe completed!", navigates to `/recipes/{id}`. `_finishInFlight` guard prevents double-submit. |
| Made-count on RecipeView | The hero stat row's "Made N×" is now backed by `GetMadeCountAsync(recipeId, userId)`; the existing `_lastCookNote` / `_lastCookDate` callout (RV-04) is wired through `GetLastCookAsync` and lights up the moment a cook is logged. |
| Recently cooked on Home | `Home.razor.cs` uses `GetRecentForUserAsync(userId, 4)` — falls back to most-recently-updated recipes only when the user has no cooks logged yet, so the tile still renders something on a fresh install. |
| Persistent cooking session | `cooking-session-state.js` `saveInProgress` / `readInProgressForRecipe` (clears stale mismatched entries by recipeId) / `clearInProgress`. CookingMode hydrates `_currentStepIndex` + `_targetServings` on first render; persists on every Next/Previous/IncrementServings/DecrementServings; Home renders a separate "In progress" band when localStorage has an entry (and the recipe is still accessible). |
| Defensive cleanup | When Home loads either band, recipe ids are validated against the user's accessible recipes — entries pointing at deleted/un-shared recipes are cleared from localStorage and the band is suppressed. |

## Commits

| # | Hash | Title |
|---|------|-------|
| 1 | 1da3081 | feat(07-09): add ScheduledRecipe + RecipeMade entities, migration, services |
| 2 | b0ee018 | feat(07-09): add ScheduleRecipeDialog + RecipeView Schedule button |
| 3 | 578ffa4 | feat(07-09): wire Home Up-Next + Recently-cooked to real persistence |
| 4 | a90d2d7 | feat(07-09): add persistent cooking-session state via localStorage |
| 5 | 4b82843 | feat(07-09): show in-progress + active-timer bands on Home |
| 6 | c236007 | feat(07-09): add Mark-as-completed Finish button to CookingMode |

## Files touched

| File | Change |
|------|--------|
| `src/CookBot.Domain/Entities/ScheduledRecipe.cs` | New entity |
| `src/CookBot.Domain/Entities/RecipeMade.cs` | New entity |
| `src/CookBot.Infrastructure/Data/CookBotDbContext.cs` | + DbSets for ScheduledRecipes / RecipeMades |
| `src/CookBot.Infrastructure/Data/Configurations/ScheduledRecipeConfiguration.cs` | New EF config (index `(UserId, ScheduledFor)`) |
| `src/CookBot.Infrastructure/Data/Configurations/RecipeMadeConfiguration.cs` | New EF config (indexes `(UserId, CompletedAt)`, `(RecipeId, CompletedAt)`) |
| `src/CookBot.Infrastructure/Migrations/20260428004334_ScheduledRecipesAndRecipeMades.cs` | New migration (forward-only; backups via existing IDatabaseBackupService at startup) |
| `src/CookBot.Web/Services/ScheduledRecipeService.cs` | New service + interface |
| `src/CookBot.Web/Services/RecipeMadeService.cs` | New service + interface |
| `src/CookBot.Web/Components/Dialogs/ScheduleRecipeDialog.razor` | New dialog (datetime-local + notes) |
| `src/CookBot.Web/Components/Pages/RecipeView.razor` | Schedule button; real made-count + last-cook |
| `src/CookBot.Web/Components/Pages/Home.razor` | Resume-cooking band; real Up-next rows; placeholder fallback |
| `src/CookBot.Web/Components/Pages/Home.razor.cs` | IJSRuntime injection; `LoadActiveSessionAsync`; recipe-name cache for the bands |
| `src/CookBot.Web/Components/Pages/CookingMode.razor` | Hydrate from localStorage; persist on step/serving changes; StartTimer→saveActiveTimer; OnTimerComplete/StopTimer→clearActiveTimer; Finish button writes RecipeMade |
| `src/CookBot.Web/wwwroot/js/cooking-session-state.js` | New JS module — single-recipe + single-timer scope |
| `src/CookBot.Web/Components/App.razor` | Script include for cooking-session-state.js |
| `src/CookBot.Web/Program.cs` | DI registrations for IScheduledRecipeService + IRecipeMadeService |

## Hard-invariant checks

| Invariant | Status |
|-----------|--------|
| `dotnet build` clean (0 warnings, 0 errors) | PASS — verified after every commit |
| `dotnet test --filter "Category!=RequiresApiKey"` 196/196 | PASS — baseline preserved |
| `grep -rn "Mud[A-Z]" src/ tests/` returns zero hits | PASS — no MudBlazor reintroduced |
| AI-off contract (UserProfile.AiEnabled hides AI surfaces) | PASS — no AI surfaces touched |
| Canonical RecipeDocument round-trip | PASS — no schema changes to RecipeDocument; ScheduleRecipe and RecipeMade are sibling entities, not extensions |
| Forward-only migrations | PASS — single new migration; backup runs via existing IDatabaseBackupService at startup if pending list is non-empty |
| No `Mud*` symbols | PASS |
| No new top-level deps | PASS — no NuGet additions |

## Architectural choices

- **Services live in `CookBot.Web.Services`, not `CookBot.Application.Services`.** The Application project doesn't reference Infrastructure (Onion architecture); the existing `IRepository<T>` interface doesn't expose `Include()`/`AsNoTracking()` which both services need. The pattern matches `AiApiKeyShareService` already in Web.Services.
- **Single-recipe / single-timer scope on localStorage.** v1 punts on multi-timer tracking and on multi-recipe parallel cooks. The JS module's `readInProgressForRecipe` clears mismatched entries automatically, so a stale session can never bleed into a different recipe.
- **No reminders or notifications for ScheduledRecipe.** The CONTEXT explicitly punts these — this slice is persistence + visibility only.
- **Active-timer card uses fixed-dark cocoa surface** (`#1A1410` / `#FBF6E7` literal hexes) matching CookingMode's design language. The in-progress card uses the standard light Cb-card with an accent border — the two states are visually distinct, which matches how the spec described them ("a recipe can be 'in progress' without an active countdown timer").

## Punts and deferred items

| Item | Why |
|------|-----|
| Reminders / push notifications for scheduled recipes | Out of scope per CONTEXT — persistence + Home visibility only |
| Multi-timer tracking on Home | Single-timer scope per CONTEXT for v1; `_activeTimers` dict in CookingMode still supports multiple in-page timers, only the Home band is single |
| Multi-recipe parallel cooking sessions | Single-recipe scope per CONTEXT — the JS `readInProgressForRecipe` clears mismatched entries |
| Editing / deleting a scheduled recipe from Home | `IScheduledRecipeService.UnscheduleAsync` is implemented but no UI surface yet — clicking a row navigates to the recipe; deletion path is FUTURE |
| Live-ticking countdown on the Home active-timer card | The card renders the snapshot remaining-seconds and re-computes on next render; a JS-driven 1Hz tick would polish but isn't load-bearing for v1 |
| RecipeMade.Notes input on Finish | The RecipeMade entity supports a Notes field; the Finish button currently passes null. A future "Add notes" inline textarea on the Finish action would surface this — out of scope for this slice |

## Notes for future work

- The `RecipeMade` log already powers RecipeView's last-cook callout and made-count. A natural next step is to wire the existing `/recipes/{id}/made` page (the pantry-deduction flow) to the same log so every "I made this" lands a RecipeMade row.
- `CookingTimers.start` and `CookbotSession.saveActiveTimer` are called in sequence from the same `StartTimer` C# method; merging them into a single JS call would shave one interop round-trip but the current shape is auditable.
- The `formatStartedAgo` helper exists in both JS and C#. If the UI ever needs to live-tick the in-progress card without a server round-trip, the JS version is ready; the C# version provides the initial render.
