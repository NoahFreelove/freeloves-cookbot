---
phase: 03-editor-ux-without-special-syntax
plan: 01
subsystem: editor-ux
tags: [blazor, mudblazor, chip-composer, bunit, jsinterop, editor]
requires:
  - Phase 1 D-13 (link-resolution-only highlighting)
  - Phase 1 D-12 (text-backed canonical record)
  - bUnit 1.40.0
provides:
  - shared RecipeChipComposer.razor (interactive + read-only + JS-fail fallback)
  - IngredientChip.razor with replace-popover (D-A2) and × remove (D-A3 name-only)
  - recipe-chip-composer.js JS-interop module (ping / getCaretCoords / scrollIntoViewWithHighlight)
  - IngredientLinkPatterns single-source-of-truth regex
  - bUnit test infrastructure for the test project
affects:
  - src/CookBot.Application/Services/RecipeStepTextFormatter.cs (consumes shared regex)
  - src/CookBot.Application/Recipes/RecipeValidator.cs (consumes shared regex)
  - src/CookBot.Web/Components/App.razor (script registration)
  - src/CookBot.Web/wwwroot/app.css (chip-flow / chip-highlight-pulse / timer-suggestion rules)
tech-stack:
  added:
    - bunit 1.40.0 (component-render testing)
  patterns:
    - InternalsVisibleTo bridge (CookBot.Application → CookBot.Web; CookBot.Web → CookBot.Tests)
    - Per-token segmented chip-flow layout (RESEARCH Pattern 2)
    - JS-interop fail-soft probe via OnAfterRenderAsync(firstRender) (D-D4)
key-files:
  created:
    - src/CookBot.Application/Recipes/IngredientLinkPatterns.cs
    - src/CookBot.Application/AssemblyAttributes.cs
    - src/CookBot.Web/AssemblyAttributes.cs
    - src/CookBot.Web/wwwroot/js/recipe-chip-composer.js
    - src/CookBot.Web/Components/Pages/RecipeEditorParts/IngredientChip.razor
    - src/CookBot.Web/Components/Pages/RecipeEditorParts/RecipeChipComposer.razor
    - tests/CookBot.Tests/Web/RecipeChipComposerTests.cs
  modified:
    - tests/CookBot.Tests/CookBot.Tests.csproj
    - src/CookBot.Application/Services/RecipeStepTextFormatter.cs
    - src/CookBot.Application/Recipes/RecipeValidator.cs
    - src/CookBot.Web/Components/App.razor
    - src/CookBot.Web/wwwroot/app.css
decisions:
  - bUnit 1.40.0 used (no fallback to 1.36 LTS or 1.41 prerelease needed; restored cleanly on net10)
  - Components placed in `Pages/RecipeEditorParts/` instead of the planned `Pages/RecipeEditor/`
    because Razor's source generator collapses folder names into namespaces and a folder named
    `RecipeEditor` collides with the existing `RecipeEditor.razor` page class.
  - InternalsVisibleTo bridge added in two places: CookBot.Application → CookBot.Web (for the
    chip composer to consume the internal regex class) and CookBot.Web → CookBot.Tests (for the
    bUnit tests to call internal Simulate* helpers).
  - RecipeValidator regex broadened from `\[([^\]]+)\]\(#(\d+)\)` to `\[([^\]]*)\]\(#(\d+)\)`
    via the shared IngredientLinkPatterns. Empty-name links `[](#id)` will now be flagged as
    DANGLING_REF if the id is missing — strictly more accurate, no semantic regression.
metrics:
  duration: ~25 minutes
  completed: 2026-04-26
---

# Phase 3 Plan 01: Foundation - RecipeChipComposer + IngredientChip + bUnit Summary

**One-liner:** Shipped the shared chip-composer foundation (RecipeChipComposer + IngredientChip + recipe-chip-composer.js + bUnit tests) and consolidated the duplicated `[name](#id)` regex into a single shared `IngredientLinkPatterns` class consumed by formatter, validator, and chip composer.

## Tasks Completed

| Task | Name | Commit | Files |
|------|------|--------|-------|
| 1 | Add bUnit + lift IngredientLinkPattern to shared file | `1042096` | tests csproj, IngredientLinkPatterns.cs, AssemblyAttributes.cs, RecipeStepTextFormatter.cs, RecipeValidator.cs |
| 2 | Create recipe-chip-composer.js + register in App.razor + chip CSS | `c5b5a78` | recipe-chip-composer.js, App.razor, app.css |
| 3 | Build IngredientChip.razor + RecipeChipComposer.razor + bUnit smoke tests | `8992b8b` | IngredientChip.razor, RecipeChipComposer.razor, RecipeChipComposerTests.cs, CookBot.Web AssemblyAttributes.cs |

## Plan Output Questions Answered

1. **Final bUnit version chosen:** `bunit 1.40.0` — no fallback ladder needed. Restored cleanly on net10.0 (the package metadata lists netstandard2.1 / net8.0 / net9.0; the netstandard2.1 build runs fine on net10).
2. **Was `[InternalsVisibleTo("CookBot.Web")]` needed?** Yes — added via `src/CookBot.Application/AssemblyAttributes.cs` so the chip composer in `CookBot.Web` can consume the internal `IngredientLinkPatterns.Pattern`. A second `[InternalsVisibleTo("CookBot.Tests")]` was added via `src/CookBot.Web/AssemblyAttributes.cs` so the bUnit tests can call the internal `Simulate*` helpers on `RecipeChipComposer`. This second attribute was not in the plan but was required for the D-A1 invariant test to compile.
3. **Deviations from planned interface signatures on RecipeChipComposer:** Minor only.
   - `RemoveChip(int start, int length)` is invoked via `EventCallback.Factory.Create` instead of inline lambda to satisfy Blazor's parameter-list inference for `EventCallback`.
   - Read-only branch's `OnRemove` uses `EventCallback.Empty` (the static field on the `EventCallback` struct) for the no-op chip removal; this is just an explicit no-op handler.
   - `RequestReplace` does not take the `currentId` argument from the plan — it is unused inside the method (the picker uses the `_pendingReplace` range to substitute regardless of the previous chip's id).
   - `SearchIngredients` returns `Task<IEnumerable<ParsedIngredient>>` (matching `MudAutocomplete<T>.SearchFunc` exactly).
4. **bUnit test counts and pass status:** 4 `[Fact]` tests, all passing.
   - `TokenizesIngredientLinksAsChips`
   - `AtTriggerInsertion_AndButtonInsertion_ProduceIdenticalUnderlyingText_DA1Invariant` (D-A1 anchor)
   - `JsInteropFails_FallsBackToMudTextField_DD4` (D-D4 anchor)
   - `UnresolvedChipRendersAsErrorChip_DA6` (D-A6 anchor; asserts `mud-chip-color-error` CSS class)
   - Full project test count: 160 / 160 passing (155 previous + 4 new + 1 from bUnit setup discovery; numbers match a no-regression run against Phase 2's baseline).

## Deviations from Plan

### Auto-fixed Issues (Rule 3 — Blocking)

**1. [Rule 3 — Blocker] Folder name `RecipeEditor/` collides with existing `RecipeEditor.razor` page class**
- **Found during:** Task 3, first build attempt.
- **Issue:** Razor's source generator emits `CookBot.Web.Components.Pages.RecipeEditor` as the namespace for any file under `Components/Pages/RecipeEditor/`. The sibling `Components/Pages/RecipeEditor.razor` already declares a class named `RecipeEditor` in `CookBot.Web.Components.Pages`. Result: `error CS0101: The namespace 'CookBot.Web.Components.Pages' already contains a definition for 'RecipeEditor'`.
- **Fix:** Renamed the new folder from `Components/Pages/RecipeEditor/` to `Components/Pages/RecipeEditorParts/`. Updated the test project's `using` directive accordingly.
- **Files modified:** Two new components moved; one test using-statement updated.
- **Commit:** `8992b8b` (Task 3).
- **Plan-text impact:** PLAN.md `files_created` list mentioned `Components/Pages/RecipeEditor/...`. The corresponding paths in the codebase are now `Components/Pages/RecipeEditorParts/...`. PATTERNS.md and CONTEXT.md should be updated by future plans (Wave 2/3) to reference the corrected folder.

**2. [Rule 3 — Blocker] `internal` Simulate* helpers not visible to test project**
- **Found during:** Task 3, second build attempt.
- **Issue:** `RecipeChipComposer.SimulateAtTriggerSelectionAsync` / `SimulateButtonInsertionAsync` were declared `internal` per the plan, but the test project (CookBot.Tests) is a separate assembly and could not access them.
- **Fix:** Added `[assembly: InternalsVisibleTo("CookBot.Tests")]` via a new `src/CookBot.Web/AssemblyAttributes.cs` file (mirrors the same pattern used in `CookBot.Application`).
- **Files modified:** `src/CookBot.Web/AssemblyAttributes.cs` (created).
- **Commit:** `8992b8b`.

### Auto-fixed Issues (Rule 1 — Lint)

**3. [Rule 1 — MUD0002 lint] `Title` is not a valid MudIconButton attribute in MudBlazor 8.15**
- **Found during:** Task 3 build.
- **Issue:** `<MudIconButton Title="Insert ingredient" />` raised `warning MUD0002: Illegal Attribute 'Title' on 'MudIconButton'`. MudBlazor 8.15's analyzer disallows `Title` (no such parameter on `MudIconButton`).
- **Fix:** Replaced `Title="Insert ingredient"` with `aria-label="Insert ingredient"` — proper accessibility attribute that screen readers announce, and a valid HTML attribute that flows through MudBlazor's splatted `UserAttributes`.
- **Files modified:** `RecipeChipComposer.razor`.
- **Commit:** `8992b8b`.

### Auto-fixed Issues (Rule 1 — Test correctness)

**4. [Rule 1 — Test bug] bUnit fallback test triggered MudBlazor JS calls (`mudElementRef.addOnBlurEvent`) under Strict mode**
- **Found during:** Task 3, first test run.
- **Issue:** `JsInteropFails_FallsBackToMudTextField_DD4` was set to `JSRuntimeMode.Strict`. The fallback path renders a `MudTextField`, and MudBlazor's `MudInput` calls `mudElementRef.addOnBlurEvent` from its own `OnAfterRenderAsync`. Strict mode rejects unregistered calls, throwing inside the test render.
- **Fix:** Switched the fallback test to `JSRuntimeMode.Loose` (still mocks `RecipeChipComposer.ping` to throw — the only call we care about). Loose mode lets unrelated MudBlazor JS invocations be no-ops.
- **Files modified:** `RecipeChipComposerTests.cs`.
- **Commit:** `8992b8b`.

**5. [Rule 1 — Test bug] Wrong CSS class assertion for Color.Error chip**
- **Found during:** Task 3, first test run.
- **Issue:** Test asserted `Assert.Contains("mud-error", cut.Markup)`. MudBlazor 8.15's actual class for color-themed chips is `mud-chip-color-error` (see MudBlazor.min.css class catalog).
- **Fix:** Updated assertion to `mud-chip-color-error`.
- **Files modified:** `RecipeChipComposerTests.cs`.
- **Commit:** `8992b8b`.

**6. [Rule 1 — Lint] xUnit1031 (blocking task ops) on `Wait()` calls in tests**
- **Found during:** Task 3 build.
- **Issue:** Test method called `.Wait()` on `Task` returned by `InvokeAsync`, raising `xUnit1031: Test methods should not use blocking task operations`.
- **Fix:** Made the test method `async Task` and switched `.Wait()` to `await`.
- **Files modified:** `RecipeChipComposerTests.cs`.
- **Commit:** `8992b8b`.

### Auto-fixed Issues (Rule 1 — Spec drift)

**7. [Rule 1 — Spec consistency] RecipeValidator's regex was `\[([^\]]+)\]\(#(\d+)\)` (1+ chars) but the formatter's was `\[([^\]]*)\]\(#(\d+)\)` (0+ chars)**
- **Found during:** Task 1.
- **Issue:** The two regex sources didn't agree on whether `[](#id)` (empty display name) matches. The plan specifies the shared regex use the `*` form (formatter's). After consolidation, the validator now matches empty-name links too.
- **Fix:** Validator now uses `IngredientLinkPatterns.Pattern` (`*` form). Empty-name dangling refs will be flagged as `DANGLING_REF` errors — strictly more accurate, no false positives possible.
- **Verification:** All 11 existing `RecipeValidatorTests` continue to pass.
- **Commit:** `1042096`.

## Authentication Gates

None.

## Threat-Surface Notes

The plan's `<threat_model>` listed:
- **T-03P01-01 mitigate** — `<span contenteditable="plaintext-only">` is in `RecipeChipComposer.razor` line ~31. Verified.
- **T-03P01-02 accept** — All `elementId` strings are C#-constructed (`composer-{Guid:N}`, `ingredient-{int}`). Verified — no user-controlled string flows into `getElementById`.
- **T-03P01-03 / T-03P01-04 accept** — No new mitigation work.

No new surface introduced beyond the threat register.

## Self-Check: PASSED

**Files created (7) — all present:**
- src/CookBot.Application/Recipes/IngredientLinkPatterns.cs — FOUND
- src/CookBot.Application/AssemblyAttributes.cs — FOUND
- src/CookBot.Web/AssemblyAttributes.cs — FOUND
- src/CookBot.Web/wwwroot/js/recipe-chip-composer.js — FOUND
- src/CookBot.Web/Components/Pages/RecipeEditorParts/IngredientChip.razor — FOUND
- src/CookBot.Web/Components/Pages/RecipeEditorParts/RecipeChipComposer.razor — FOUND
- tests/CookBot.Tests/Web/RecipeChipComposerTests.cs — FOUND

**Commits — all present in `git log`:**
- `1042096` chore(03-01): add bUnit + lift IngredientLinkPattern to shared file — FOUND
- `c5b5a78` feat(03-01): add recipe-chip-composer JS module + chip CSS — FOUND
- `8992b8b` feat(03-01): add RecipeChipComposer + IngredientChip + bUnit tests — FOUND

**Build:** `dotnet build FreelovesCookBot.sln` — 0 warnings, 0 errors.

**Tests:** `dotnet test --filter "Category!=RequiresApiKey"` — 160 / 160 passing.

**Regex consolidation:** `grep -rn "private static readonly Regex IngredientLinkPattern\|private static readonly Regex IngredientLink " src/` — empty (no duplicate regex declarations remain).
