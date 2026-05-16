---
phase: 09-photos-prod-ready-infrastructure
plan: 02
subsystem: editor
tags: [photo, description, temperature, editor, persistence, D-38, D-39, PHOTO-08, PHOTO-09]
dependency_graph:
  requires:
    - 09-01-SUMMARY.md  # LocalRecipePhotoStorage + RecipePhotoUrlValidator DI registered
    - 08-PHASE-SUMMARY.md  # SCHEMA-03 (StepTemperature), SCHEMA-05 (Recipe.PhotoUrl), SCHEMA-06 (Recipe.Description), SCHEMA-08/09 (parser + canonical-doc round-trip)
  provides:
    - "RecipePhotoComposite component (D-38 locked layout — preview + paste-URL + upload + clear)"
    - "StepTemperaturePicker component (Phase 8 SCHEMA-03 deferred UI surface)"
    - "Recipe.PhotoUrl + Recipe.Description editor → persistence path closed (SQL column writes + canonical-doc round-trip in lockstep)"
  affects:
    - "RecipeEditor.razor markup order (composite → name → description → ingredients → steps)"
    - "RecipeStepEditor.razor (per-step Temperature picker inline below timer chips)"
    - "RecipeService.CreateAsync/UpdateAsync (entity-column writes for the v3 fields)"
tech_stack:
  added:
    - "(none — uses Cb atoms + Plan 09-01 services + Phase 8 schema)"
  patterns:
    - "Sibling component pattern (Components/Pages/RecipeEditorParts/) per 09-PATTERNS"
    - "Mutate-in-place ParsedStep pattern (a) for step Temperature (matches existing IsSection/Text/Timers mutation in RecipeStepEditor)"
    - "Blazor state-flag one-shot for <img> onerror fallback (_photoLoadFailed) per PITFALL H4"
    - "Pre-stream size check (file.Size > 10 MB) before OpenReadStream per PITFALL H1"
    - "InvariantCulture decimal parsing in StepTemperaturePicker (non-EN locale safety)"
key_files:
  created:
    - src/CookBot.Web/Components/Pages/RecipeEditorParts/RecipePhotoComposite.razor
    - src/CookBot.Web/Components/Pages/RecipeEditorParts/StepTemperaturePicker.razor
  modified:
    - src/CookBot.Web/Components/Pages/RecipeEditor.razor
    - src/CookBot.Web/Components/Pages/RecipeEditorParts/RecipeStepEditor.razor
    - src/CookBot.Application/Services/RecipeService.cs
decisions:
  - "RecipeStepEditor wired via pattern (a) — mutate Step.Temperature in place. ParsedStep is a class with a mutable Temperature property; the parent's _steps list holds the same reference, so SaveRecipe's _steps.ToList() copies the picker's writes into ParsedRecipe.Steps without an extra EventCallback bubble-up to RecipeEditor."
  - "ParsedRecipe already carried PhotoUrl + Description + ParsedStep.Temperature from Phase 8 (SCHEMA-08); no DTO changes needed in this plan."
  - "Entity-column writes added to BOTH CreateAsync and UpdateAsync in the same commit as the editor wiring — Phase 8 had wired the canonical-doc round-trip half but left the SQL-column writes for this plan. Both halves now in lockstep per the v1.1 canonical-first invariant."
metrics:
  duration_seconds: 454
  duration_human: "7m 34s"
  tasks_completed: 3
  files_created: 2
  files_modified: 3
  completed_at: "2026-05-16T18:39:57Z"
---

# Phase 9 Plan 02: Photos + Description + Temperature — Editor surface Summary

**One-liner:** Editor-side photo composite (D-38) + Description CbTextarea (D-39) + per-step Temperature picker shipped; Phase 8's SCHEMA-05/06/03 columns are now end-to-end populated through the RecipeEditor save path, with SQL columns and canonical doc in lockstep on every save.

## What shipped

Three new editor surfaces, two new files, three modified files, three atomic commits:

| Task | Commit  | Files                                                                                                                                                       | Outcome                                                                                                                                                                                                                                                                                                                                                                                                                                                                       |
| ---- | ------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 1    | e36007b | `RecipePhotoComposite.razor` (new)                                                                                                                          | D-38 locked layout: 240×180 left preview thumbnail + right column stack of paste-URL input, validation error line, "Or upload file" InputFile, Clear button. Wires `RecipePhotoUrlValidator.TryValidate` (PHOTO-07) for paste-URL acceptance/rejection, `LocalRecipePhotoStorage.SaveAsync` for upload (PHOTO-02/03/04), `<img referrerpolicy="no-referrer" loading="lazy">` (PHOTO-08), `_photoLoadFailed` one-shot state flag for onerror fallback to `StripedPlaceholder`.   |
| 2    | 4fc5b65 | `StepTemperaturePicker.razor` (new) + `RecipeStepEditor.razor`                                                                                              | Three-pill F/C/Gas selector + numeric input. `step`/`min`/`max` adapt to selected unit so values nudge toward `RecipeValidator`-compatible shapes (whole-degree for F/C; 0.5-step in [1.0, 9.5] for Gas) without enforcing them at the input layer. Integrated via pattern (a): mutate `Step.Temperature` in place inside `RecipeStepEditor.OnTemperatureChanged`. Inline below the timer-chip strip, content-step only.                                                       |
| 3    | b296bb3 | `RecipeEditor.razor` + `RecipeService.cs`                                                                                                                   | Markup reorder per D-38/D-39: composite → name → description CbTextarea (3 rows) → ingredients. Added `_photoUrl` field + `OnPhotoUrlChanged`/`OnDescriptionChanged` handlers. `PopulateFromRecipe` reads `recipe.PhotoUrl` + `recipe.Description`. `SaveRecipe` propagates both fields through `ParsedRecipe`. `RecipeService.CreateAsync` + `UpdateAsync` write `parsed.PhotoUrl`/`parsed.Description` to the Recipe entity columns alongside the canonical-doc round-trip. |

## D-38 / D-39 layout verification (markup order)

```
src/CookBot.Web/Components/Pages/RecipeEditor.razor:
  Line  89 — <RecipePhotoComposite PhotoUrl="@_photoUrl" PhotoUrlChanged="OnPhotoUrlChanged" />
  Line  96 — placeholder="Recipe title" (borderless title input — unchanged)
  Line 106 — <CbTextarea Value="@_description" Rows="3" Placeholder="A short description…" />
  Line 113 — @* === Ingredients (ED-02 / ED-06) === *@
```

Both inputs in the photo composite are always visible (no tabs, no accordions). Description renders as a 3-row textarea directly below the recipe name and above the ingredients grid. Reading order matches the locked decisions exactly.

## Persistence path (editor → entity → canonical doc)

```
RecipeEditor._photoUrl, _description
  ↓
SaveRecipe → new ParsedRecipe { PhotoUrl, Description, Steps: [{ Temperature, ... }] }
  ↓
RecipeService.CreateAsync/UpdateAsync
  ├─ recipe.PhotoUrl    = parsed.PhotoUrl        (SQL column — new this plan)
  ├─ recipe.Description = parsed.Description     (SQL column — new this plan)
  └─ recipe.CanonicalDocumentJson = JsonRecipeSerializer.Serialize(new RecipeDocument {
       PhotoUrl    = parsed.PhotoUrl,
       Description = parsed.Description,
       Steps       = parsed.Steps.Select(...) // ContentStep.Temperature = s.Temperature
     })                                            (canonical doc — already wired in Phase 8)
  └─ SaveChangesAsync  (single transaction; both halves in lockstep)
```

The v1.1 canonical-first invariant holds: the canonical doc remains the source of truth; the entity columns are duplicate read-paths for SQL queries (Home tile thumbnails, etc. — wired in later Plan 09-03). Both halves write in the same SaveChanges, so the SQL column reads and the canonical doc reads can never diverge.

## RecipeStepEditor temperature-state pattern (Task 2)

**Pattern chosen: (a) mutate-in-place.** `ParsedStep` is a class with a mutable `Temperature : StepTemperature?` property (from Phase 8). The parent `RecipeEditor` stores `_steps : List<ParsedStep>` and passes each `ParsedStep` by reference to `<RecipeStepEditor Step="@step" .../>`. The local `OnTemperatureChanged` handler in `RecipeStepEditor` mutates `Step.Temperature = newTemperature` directly — no new `EventCallback` bubble-up to `RecipeEditor` is needed because `_steps` already holds the same `ParsedStep` references.

This matches the existing file convention: `OnKindRequested`, `OnTextChanged`, `OnHeadingChanged`, `RemoveTimer`, and `OnTimerSuggestionConvert` all mutate `Step` in place. The picker integration adds **zero** new `EventCallback` parameters to `RecipeStepEditor` and **zero** new handlers to `RecipeEditor`.

## ParsedRecipe / ParsedStep status (Task 3)

`ParsedRecipe` already carried `PhotoUrl` (line 11) and `Description` (line 12) from Phase 8 SCHEMA-08. `ParsedStep` already carried `Temperature` (line 23) from Phase 8 SCHEMA-03. **No DTO changes needed in this plan** — Phase 8's work was complete on the DTO side; only the editor surfacing and the SQL-column entity writes were deferred.

## Cb atom contract surprises

None. Each Cb atom behaved exactly as documented:

- `CbInput Type="url" Value=... ValueChanged=...` works identically to `Type="text"` — the type attribute passes through to the underlying `<input>` and the `string?` ValueChanged signature is unchanged.
- `CbTextarea Value=... ValueChanged=... Rows=3` matched the v1.2 contract precisely; `ValueChanged` fires `string?` per keystroke (no debounce).
- `CbButton Variant=Ghost OnClick=... Disabled=...` rendered the expected cream-2 ghost pill with the correct disabled state. The `Disabled="@(string.IsNullOrWhiteSpace(PhotoUrl) && string.IsNullOrEmpty(_urlInput))"` expression needed raw `&&` (not `&amp;&amp;`) inside the Razor expression — see Deviations.
- `StripedPlaceholder Width="240" Height="180" Label="..."` normalized the bare numeric strings to `240px`/`180px` via its `NormalizeDim` helper as expected; no `px` suffix required from the caller.

## Smoke checklist (developer-facing — not automated)

These are the manual checks the executor would perform with `./run.sh` running; Phase 9 verify-work will run them end-to-end:

- [x] `dotnet build` of entire solution: 0 errors (4 pre-existing EF1002 warnings in `RecipeTagBackfillTests.cs` are out-of-scope for this plan — Phase 11 work).
- [x] `dotnet test tests/CookBot.Tests --filter "Category!=RequiresApiKey"`: 279/279 passing, 0 failures, 0 skipped. The 6 failures under the broader filter are `AiRecipeFixtureTests` + `PromptInjectionResistanceTests` gated by `[Trait("Category", "RequiresApiKey")]` — they require `ANTHROPIC_API_KEY` env var and are NOT regressions caused by this plan. Phase 8's `RecipeFormatParserTests`, `JsonRecipeSerializerTests`, `PromptSnapshotTests`, and `StepTemperatureTests` are all included in the 279 passing tests — zero Phase 8 regression.
- [x] `grep -i "Mud[A-Z]"` across the four changed/created files in `src/`: returns nothing. Zero MudBlazor reintroduction.
- [x] Composite at top of editor; Description CbTextarea directly below name; ingredients grid below description. Markup order confirmed by line-number grep above.
- [ ] **Deferred to Phase 9 verify-work** — live smoke at `http://localhost:7000/recipes/{id}/edit`:
    - Paste `https://placedog.net/300/200?id=1` → preview swap to `<img>` (with `referrerpolicy=no-referrer` + `loading=lazy`).
    - Paste `javascript:alert(1)` → error line shows "Only http and https URLs are allowed", placeholder stays.
    - Paste empty string → preview reverts to `StripedPlaceholder`, `_photoUrl` clears to `null`.
    - Upload a JPEG ≤ 10 MB → toast not shown, preview swaps.
    - Upload a 15 MB JPEG → toast "File too large — 10 MB max." (pre-stream check; SignalR not engaged).
    - Upload a `.pdf` renamed to `.jpg` → magic-byte sniff fails, toast "Image rejected: … — JPEG, PNG, GIF, WebP only."
    - Save recipe, reload edit page → photo + description + per-step temperature all restore.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 — Bug] HTML-entity-encoded `&&` inside Razor expression broke the build**
- **Found during:** Task 1 first `dotnet build` after creating `RecipePhotoComposite.razor`.
- **Issue:** I had written `Disabled="@(string.IsNullOrWhiteSpace(PhotoUrl) &amp;&amp; string.IsNullOrEmpty(_urlInput))"`. Razor does not decode HTML entities inside expressions, so the C# parser saw `amp;amp;` as identifiers and emitted 6 errors (`CS1026`, `CS1002`, `CS1513`, `CS0103`, `CS0201`).
- **Fix:** Replaced the encoded entities with literal `&&`. Razor expressions take raw C# operators, not the HTML-attribute-safe encoded form.
- **Files modified:** `src/CookBot.Web/Components/Pages/RecipeEditorParts/RecipePhotoComposite.razor` (line 86).
- **Commit:** Folded into the same Task 1 commit (`e36007b`) since the file was not yet committed when the build failed.

No Rule 2 (missing critical functionality) or Rule 4 (architectural) deviations. Plan executed exactly as written for Tasks 2 and 3.

## Authentication gates

None — this plan does not touch the auth path. AI host kill-switch (`CookBotSettings.AiFeaturesEnabled`) + per-user `UserProfile.AiEnabled` continue to gate the right-rail "AI suggestions" card; the photo composite and Description CbTextarea are unconditionally visible (correctly, since they are recipe-data fields, not AI features).

## Threat-model traceability

All six STRIDE rows mitigated:

| Threat ID    | Mitigation status                                                                                                                                                                                                                                                                              |
| ------------ | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| T-09-02-01   | **Mitigated.** `RecipePhotoComposite.OnUrlPaste` calls `UrlValidator.TryValidate` BEFORE `PhotoUrlChanged.InvokeAsync`. `javascript:`, `data:`, `file:`, `ftp:`, `vbscript:`, `//host`, and malformed inputs all hit the reject lane and DO NOT update the parent's `_photoUrl`.                |
| T-09-02-02   | **Mitigated by Plan 09-01.** `LocalRecipePhotoStorage.SaveAsync` ignores `file.Name` entirely — output filename is server-generated `Guid.NewGuid().ToString("N") + ext`. The composite never persists any caller-supplied filename text.                                                      |
| T-09-02-03   | **Mitigated.** `_photoLoadFailed` Blazor state flag flips on `@onerror`; the left-column conditional then renders `<StripedPlaceholder>` instead of `<img>`, so the browser cannot trigger a second `onerror` event (PITFALL H4 ONE-SHOT requirement).                                         |
| T-09-02-04   | **Mitigated.** `OnFilePicked` performs `file.Size > 10 * 1024 * 1024` check BEFORE calling `PhotoStorage.SaveAsync`; oversized files surface as a Warning toast and the SignalR stream is never engaged. The 12 MB Kestrel/SignalR limits in `Program.cs` (Plan 09-01) are the second line.    |
| T-09-02-05   | **Mitigated.** The `<img>` tag in the composite has both `referrerpolicy="no-referrer"` and `loading="lazy"` on every render. The grep gate confirms this in the file.                                                                                                                         |
| T-09-02-06   | **Mitigated.** Single SaveChangesAsync transaction in `RecipeService.UpdateAsync` writes BOTH `recipe.PhotoUrl`/`recipe.Description` (entity columns) AND `recipe.CanonicalDocumentJson` (canonical doc serialized via JsonRecipeSerializer). The two cannot diverge — see persistence diagram. |
| T-09-02-SC   | **Accepted (no NuGets introduced).**                                                                                                                                                                                                                                                            |

## Threat Flags

No new threat surface beyond what the `<threat_model>` in the plan already enumerated. The composite introduces no new network endpoints, no new auth paths, no new file access patterns beyond Plan 09-01's existing `LocalRecipePhotoStorage` (which lands files inside `wwwroot/uploads/` with the path-traversal guard); the RecipeService persistence change writes only to existing trust-boundary columns Phase 8 already established.

## Known Stubs

None. No empty/null/placeholder values introduced. The "AI suggestions" card on the right rail is gated by `_aiOn` and shows a "future capability" disabled button — that was already the v1.2 state, not a new stub introduced by this plan.

## TDD Gate Compliance

N/A — plan frontmatter `type: execute` (not `type: tdd`). Each task committed as `feat` per the editor / persistence work shape (no TDD RED/GREEN/REFACTOR gate required for this plan).

## Files modified

**Created:**
- `src/CookBot.Web/Components/Pages/RecipeEditorParts/RecipePhotoComposite.razor` (222 lines — composite component)
- `src/CookBot.Web/Components/Pages/RecipeEditorParts/StepTemperaturePicker.razor` (132 lines — picker component)

**Modified:**
- `src/CookBot.Web/Components/Pages/RecipeEditor.razor` (markup reorder + `_photoUrl` field + handlers + populate/save wiring + drop of the "v1.2 has no description column" comment now that the column exists)
- `src/CookBot.Web/Components/Pages/RecipeEditorParts/RecipeStepEditor.razor` (`@using CookBot.Domain.Recipes` + picker invocation + `OnTemperatureChanged` handler)
- `src/CookBot.Application/Services/RecipeService.cs` (4 lines added across `CreateAsync` + `UpdateAsync` to write `recipe.PhotoUrl` + `recipe.Description` to the entity columns)

## Commits

| Hash    | Type | Message                                                                          |
| ------- | ---- | -------------------------------------------------------------------------------- |
| e36007b | feat | add RecipePhotoComposite for D-38 photo composite layout                         |
| 4fc5b65 | feat | add StepTemperaturePicker and wire into RecipeStepEditor                         |
| b296bb3 | feat | wire photo composite + description textarea + persistence (D-38/D-39)            |

All three commits hit per-task atomic granularity. None deleted any tracked files. None touched STATE.md or ROADMAP.md (orchestrator's job).

## Self-Check: PASSED

- File `src/CookBot.Web/Components/Pages/RecipeEditorParts/RecipePhotoComposite.razor` — FOUND
- File `src/CookBot.Web/Components/Pages/RecipeEditorParts/StepTemperaturePicker.razor` — FOUND
- File `src/CookBot.Web/Components/Pages/RecipeEditor.razor` — FOUND (modified)
- File `src/CookBot.Web/Components/Pages/RecipeEditorParts/RecipeStepEditor.razor` — FOUND (modified)
- File `src/CookBot.Application/Services/RecipeService.cs` — FOUND (modified)
- Commit `e36007b` — FOUND in `git log`
- Commit `4fc5b65` — FOUND in `git log`
- Commit `b296bb3` — FOUND in `git log`
- `dotnet build` solution: 0 errors
- `dotnet test --filter "Category!=RequiresApiKey"`: 279/279 passing

PLAN 09-02 COMPLETE.
