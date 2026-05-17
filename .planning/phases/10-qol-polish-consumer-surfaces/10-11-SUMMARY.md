---
phase: 10-qol-polish-consumer-surfaces
plan: 11
subsystem: grocery-list
tags: [grocery-list-service, pantry-view, quick-add, tdd, polish-02]
requirements: [POLISH-02]

dependency_graph:
  requires:
    - "GroceryList entity (Domain)"
    - "GroceryListItem entity with double Amount + IsPurchased (Domain)"
    - "IRepository<GroceryList> (Infrastructure)"
    - "PantryView.razor existing @inject chain (Web)"
  provides:
    - "GroceryListService.EnsurePrimaryListAsync(int userId)"
    - "GroceryListService.AddItemAsync(int groceryListId, int ingredientId, double amount, string unit)"
    - "PantryView cart button click handler wired to quick-add flow"
  affects:
    - "PantryView UX — cart button is now active (was disabled)"
    - "GroceryList creation path — EnsurePrimaryListAsync creates 'Pantry quick-add' list if none exists"

tech_stack:
  added: []
  patterns:
    - "TDD RED/GREEN: test file committed before implementation"
    - "In-memory SQLite bootstrap for service unit tests (mirrors OwnershipTests pattern)"
    - "EF FK seeding: User entity must be seeded before GroceryList rows"
    - "@inject directive for service injection in Razor pages"

key_files:
  created:
    - path: "tests/CookBot.Tests/Services/GroceryListServiceTests.cs"
      role: "Three Fact tests covering EnsurePrimaryListAsync (existing/empty) + AddItemAsync (double amount, IsPurchased=false)"
  modified:
    - path: "src/CookBot.Application/Services/GroceryListService.cs"
      role: "Added EnsurePrimaryListAsync + AddItemAsync methods"
    - path: "src/CookBot.Web/Components/Pages/PantryView.razor"
      role: "Enabled cart button, added @inject for GroceryListService, added AddToGroceryList handler"

decisions:
  - "B-02 enforced: AddItemAsync amount parameter is double (not decimal) — matches GroceryListItem.Amount column type exactly"
  - "PATTERNS.md #3 correction honored: IsPurchased used throughout (not IsCompleted which does not exist)"
  - "Test fix (Rule 1): FK constraint required seeding User entity before GroceryList rows — added to all three tests"
  - "PantryService constructor stub: PantryService injected with in-memory Repository<T> instances since tested methods don't call through it"
  - "No authz guard on AddItemAsync: mirrors GenerateFromRecipeAsync convention; PantryView gates pantry access before reaching this call"

metrics:
  duration_seconds: 233
  completed_date: "2026-05-17"
  tasks_completed: 3
  tasks_total: 3
  files_modified: 2
  files_created: 1
---

# Phase 10 Plan 11: POLISH-02 Pantry Quick-Add to Grocery List Summary

Closed POLISH-02 by adding `EnsurePrimaryListAsync` and `AddItemAsync` to `GroceryListService` and wiring the PantryView per-row cart button to a functional click handler that quick-adds pantry items to the user's primary grocery list with a success toast.

## Tasks Completed

| Task | Name | Commit | Files |
|------|------|--------|-------|
| 1 | Write GroceryListServiceTests (RED) | 6c08918 | tests/CookBot.Tests/Services/GroceryListServiceTests.cs |
| 2 | Add EnsurePrimaryListAsync + AddItemAsync (GREEN) | 70826a0 | src/CookBot.Application/Services/GroceryListService.cs, tests/CookBot.Tests/Services/GroceryListServiceTests.cs |
| 3 | Wire PantryView cart button | 3e175b0 | src/CookBot.Web/Components/Pages/PantryView.razor |

## TDD Gate Compliance

RED gate commit: `6c08918` — test file with 3 failing Fact tests (CS1061 build errors; methods not yet defined).
GREEN gate commit: `70826a0` — service methods added; all 3 tests pass.
No REFACTOR gate needed — implementation was clean on first pass.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Fixed FK constraint failure in test seed data**

- **Found during:** Task 2 (GREEN phase) — tests threw `SQLite Error 19: FOREIGN KEY constraint failed`
- **Issue:** Tests seeded `GroceryList` rows with hardcoded `UserId = 1` without first inserting a `User` entity. SQLite enforced the FK and rejected the save.
- **Fix:** Added `User { DisplayName = "TestUser" }` seed at the start of each of the three test methods; derived `userId` from the saved entity's `Id` rather than hardcoding 1.
- **Files modified:** `tests/CookBot.Tests/Services/GroceryListServiceTests.cs`
- **Commit:** `70826a0`

## Verification Results

All plan verification criteria passed:

- `dotnet test --filter GroceryListServiceTests` exits 0 — 3/3 passed
- `dotnet build` exits 0 — 0 errors, 4 pre-existing warnings (EF1002 in RecipeTagBackfillTests, out-of-scope)
- `grep IsCompleted GroceryListService.cs GroceryListServiceTests.cs PantryView.razor` returns 0 hits
- `grep "double amount = 0" GroceryListService.cs` returns 1 hit (B-02 enforced)
- `grep "decimal amount" GroceryListService.cs` returns 0 hits

## Known Stubs

None. The cart button is fully wired; `EnsurePrimaryListAsync` creates a real `GroceryList` row; `AddItemAsync` creates a real `GroceryListItem` row. No placeholder data paths.

## Threat Flags

No new threat surface beyond what the plan's threat model documents. The `EnsurePrimaryListAsync` + `AddItemAsync` call chain in PantryView always uses `UserService.CurrentUserId.Value` as the userId — cross-user grocery list write (T-10-11-01) is not exploitable from this call site.

## Self-Check: PASSED

- `tests/CookBot.Tests/Services/GroceryListServiceTests.cs` exists: FOUND
- `src/CookBot.Application/Services/GroceryListService.cs` has `EnsurePrimaryListAsync`: FOUND
- `src/CookBot.Application/Services/GroceryListService.cs` has `AddItemAsync`: FOUND
- `src/CookBot.Web/Components/Pages/PantryView.razor` has `AddToGroceryList`: FOUND
- Commits `6c08918`, `70826a0`, `3e175b0` exist in git log: VERIFIED
