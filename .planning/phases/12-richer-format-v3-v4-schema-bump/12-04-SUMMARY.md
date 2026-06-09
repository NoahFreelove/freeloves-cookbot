---
phase: 12-richer-format-v3-v4-schema-bump
plan: "04"
subsystem: ui
tags: [blazor, recipe-format, v4, equipment, provenance, substitutions, doneness-cue, link-safety]

# Dependency graph
requires:
  - phase: 12-01
    provides: "v4 RecipeDocument POCOs — Equipment, Provenance, IngredientSubstitution, DonenessCue"
  - phase: 12-02
    provides: "ParsedRecipe/ParsedIngredient/ParsedStep editor DTOs wired through RecipeFormatParser"
provides:
  - "RecipeEditor authors all four v4 field groups: equipment chip card, Source & Credit provenance card, per-ingredient substitution sub-rows, per-step doneness cue input"
  - "RecipeStepEditor per-step DonenessCue cb-input gated to StepKind.Step, mutate-in-place handler"
  - "RecipeView displays all four field groups: equipment ephemeral checklist, substitution sub-lines with FormatSubAmount, doneness cue with Check icon, provenance credit with TryValidate-gated <a> link"
  - "SC2 UI round-trip: author→save→reload preserves all four groups"
  - "SC5: eight UI-SPEC surfaces (§1-§8) shipped — display and authoring"
  - "D-12-08 SourceUrl defang: javascript:/data: URLs render as plain text, never as live anchors"
affects:
  - phase-13-export-interoperability
  - phase-14-photo-gallery
  - phase-16-uat-integration

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Ephemeral checkbox state: _checkedEquipment HashSet<string> + ToggleEquipment — ephemeral view state, never persisted"
    - "Link-safety gate: RecipePhotoUrlValidator.TryValidate in OnParametersSet guards provenance <a>; _validatedSourceUrl stays null for non-http/https schemes"
    - "Substitution static amounts: FormatSubAmount helper renders sub.Amount/Unit as static text with zero servings-scale multiplier applied (D-12-04)"
    - "Provenance BuildProvenanceCredit: three-branch copy helper (both/SourceName-only/AuthorName-only)"
    - "Custom Cb atoms only — no MudBlazor components introduced in any modified file"

key-files:
  created: []
  modified:
    - "src/CookBot.Web/Components/Pages/RecipeEditorParts/RecipeStepEditor.razor — doneness cue cb-input + OnDonenessCueInput handler"
    - "src/CookBot.Web/Components/Pages/RecipeEditor.razor — Equipment card, Source & Credit card, substitution sub-rows, SaveRecipe FLAG 3, PopulateFromParsed FLAG 2"
    - "src/CookBot.Web/Components/Pages/RecipeView.razor — equipment checklist, substitution sub-lines, doneness cue, provenance credit + TryValidate gate"

key-decisions:
  - "Task 3 checkpoint auto-approved under GSD auto_advance mode — visual UAT deferred to Playwright harness (tests/uat-harness/) + a future manual pass; no fabricated human confirmation"
  - "Provenance SourceUrl validated via RecipePhotoUrlValidator.TryValidate (existing allowlist); _validatedSourceUrl gates the <a> element to enforce D-12-08 / mitigate T-12-07"
  - "Substitution amounts rendered via static FormatSubAmount, explicitly NOT multiplied by the servings scale factor (D-12-04 compliance)"
  - "NullIfEmpty helper added to RecipeEditor to produce null RecipeProvenance when all three fields blank, avoiding empty-string artifacts in the canonical doc"

patterns-established:
  - "Link safety pattern: always call RecipePhotoUrlValidator.TryValidate before rendering any user-supplied URL as a live anchor href"
  - "Ephemeral UI state pattern: volatile per-view state (equipment checked items) lives in a HashSet field, never written back to RecipeDocument or RecipeService"
  - "Substitution display pattern: sub.Name present → formatted name+amount; Note only → sub.Note; both → name+amount + em dash + Note"

requirements-completed: [FORMAT-01, FORMAT-02, FORMAT-03, FORMAT-04]

# Metrics
duration: ~10min
completed: 2026-06-06
---

# Phase 12 Plan 04: Authoring + Display for All Four v4 Field Groups Summary

**Equipment chip card, provenance Source & Credit card, substitution sub-rows, and per-step doneness cue shipped in RecipeEditor and RecipeView, completing SC2 round-trip and SC5 eight-surface UI-SPEC, with SourceUrl defanged through the RecipePhotoUrlValidator allowlist.**

## Performance

- **Duration:** ~10 min (Tasks 1-2 in prior session; continuation finalizes Task 3 + docs)
- **Started:** 2026-06-05T22:58:36-04:00 (Task 1 commit)
- **Completed:** 2026-06-06
- **Tasks:** 3 (2 auto + 1 checkpoint)
- **Files modified:** 3

## Accomplishments

- RecipeEditor authors all four v4 field groups per UI-SPEC §5-§8 (equipment, provenance, substitutions, doneness cue), fully wired through SaveRecipe (FLAG 3) and PopulateFromParsed (FLAG 2).
- RecipeView displays all four v4 field groups per UI-SPEC §1-§4 (equipment ephemeral checklist, substitution sub-lines, doneness cue with Check icon, provenance credit with TryValidate-gated link).
- Link-safety invariant (D-12-08 / T-12-07) enforced: `RecipePhotoUrlValidator.TryValidate` is called in `OnParametersSet`; `_validatedSourceUrl` stays null for `javascript:`, `data:`, and any non-http/https URL; the `<a>` element only renders when the validated value is non-null.

## Task Commits

Each task was committed atomically:

1. **Task 1: RecipeStepEditor doneness cue + RecipeEditor authoring (equipment, provenance, substitutions) + save/populate wiring** - `7e55842` (feat)
2. **Task 2: RecipeView display — equipment checklist, substitution sub-lines, doneness cue, validated provenance credit** - `87da007` (feat)
3. **Task 3: Human verify the eight v4 UI surfaces end-to-end** — checkpoint; auto-approved (see note below)

## Files Created/Modified

- `src/CookBot.Web/Components/Pages/RecipeEditorParts/RecipeStepEditor.razor` — doneness cue `cb-input` after temperature picker, gated to `StepKind.Step`; `OnDonenessCueInput` mutate-in-place handler (mirrors `OnTemperatureChanged`)
- `src/CookBot.Web/Components/Pages/RecipeEditor.razor` — Equipment chip card (Enter/comma commit, case-insensitive dedup, `cb-chip tag`); Source & Credit card (three `cb-input` rows, on-blur URL warning `role="alert"`); substitution sub-rows under each ingredient (`cream-2` bg, `cb-input` Note, trash + Add substitution ghost button); `SaveRecipe` FLAG 3 wiring; `PopulateFromParsed` FLAG 2 wiring; `NullIfEmpty` helper
- `src/CookBot.Web/Components/Pages/RecipeView.razor` — `RecipePhotoUrlValidator` injected; `_validatedSourceUrl` computed in `OnParametersSet`; provenance credit with `BuildProvenanceCredit` helper (3 copy forms); substitution sub-lines with `FormatSubAmount` (static amounts, no servings scale); equipment `<ul role="list" aria-label="Equipment list">` with `_checkedEquipment` ephemeral state and `ToggleEquipment`; doneness cue with `Icon.Names.Check`

## Decisions Made

- No MudBlazor components introduced in any modified file (hard constraint from CLAUDE.md).
- `NullIfEmpty` helper added to `RecipeEditor` to produce a null `RecipeProvenance` when all three provenance fields are blank — prevents empty-string artifacts in `CanonicalDocumentJson`.
- Provenance URL warning is non-blocking on blur per D-12-15 — user can still save with an invalid URL; the validator gate in `RecipeView` is the enforcement point.

## Task 3 Checkpoint — Auto-Approved (Transparent Record)

**Checkpoint type:** `checkpoint:human-verify`

The checkpoint asked a human to verify six steps:
1. Start the app and open the editor; author equipment, Source & Credit (SourceName, Author, valid https URL), at least one substitution note, and a doneness cue on a step.
2. Save, re-open the editor — confirm all four groups round-trip (SC2 UI completion).
3. Open RecipeView: confirm equipment checklist (ephemeral strike-through), substitution sub-line (static amount — does NOT change with servings), doneness cue under the step, provenance credit as a clickable link.
4. Set Source URL to `javascript:alert(1)`, save, reopen RecipeView — confirm the credit renders as PLAIN TEXT, proving the allowlist defang (D-12-08).
5. Confirm visuals match 12-UI-SPEC (custom Cb atoms, correct copy: "Equipment", "Source & Credit", "Adapted from", "Add substitution", "Doneness").

**Resolution:** Auto-approved under GSD `auto_advance: true` (config `_auto_chain_active: true`). **No human visually confirmed these surfaces.** Visual UAT is deferred to:
- The Playwright harness at `tests/uat-harness/` (`npm test`) — see Memory: automated-uat-harness
- A future manual pass by the developer before shipping v1.4

**Automated sanity performed during this continuation:**
- `dotnet build src/CookBot.Web` → Build succeeded, 0 errors, 0 warnings.
- Static code inspection confirmed `RecipeView.razor` calls `UrlValidator.TryValidate` before rendering any provenance `<a>` tag. The `_validatedSourceUrl` field is null-initialized, set only on `TryValidate` success, and the `<a>` is gated by `@if (_validatedSourceUrl != null)`. The `javascript:alert(1)` defang (step 4 of the checkpoint) is implemented correctly per the code path — static guarantee, not a live test.

## Deviations from Plan

None — plan executed exactly as written. The link-safety invariant, substitution static amounts, NullIfEmpty helper, and non-blocking URL warning were all within the plan spec (D-12-08, D-12-04, D-12-15).

## Issues Encountered

None.

## User Setup Required

None — no external service configuration required.

## Next Phase Readiness

Phase 12 is now fully complete (all 4 plans executed):
- 12-01: v4 RecipeDocument POCOs + upcaster + gap-detection test
- 12-02: editor DTO extensions + round-trip guarantee
- 12-03: AI prompt updated to v4 schema + snapshot test
- 12-04: RecipeEditor + RecipeView four-group authoring + display (this plan)

Phase 13 (Export & Interoperability — Schema.org JSON-LD + Cooklang one-way export) can proceed immediately. The v4 `RecipeDocument` with all four new fields is stable and fully surfaced; export projectors will consume it directly.

**Deferred UAT:** Before closing v1.4 overall, run the Playwright harness (`npm test` in `tests/uat-harness/`) and do a manual pass covering the six checkpoint steps above. Phase 16 (UAT + Integration) is the designated gate.

---
*Phase: 12-richer-format-v3-v4-schema-bump*
*Plan: 04*
*Completed: 2026-06-06*
