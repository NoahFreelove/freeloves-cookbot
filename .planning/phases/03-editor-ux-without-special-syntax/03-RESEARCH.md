# Phase 3: Editor UX Without Special Syntax — Research

**Researched:** 2026-04-26
**Domain:** Blazor Server / MudBlazor 8.15 chip-aware composer; JS interop for caret-anchored autocomplete and scroll-highlight; regex broadening; component extraction
**Confidence:** HIGH on the codebase facts and on Step 1's tier mapping; MEDIUM on the inline-chip pattern recommendation (no MudBlazor-native primitive — recommendation is justified by trade-off, not by official precedent); HIGH on the regex/JS-interop/auto-write deletion specifics.

---

## Summary

Phase 3 is a **UX surface change layered on a stable canonical schema**. The hard work in Phase 1 (D-13: `[name](#id)` resolution is the only highlight path; canonical record is text-backed) means the chip composer is a pure view-layer tokenization over the same `[name](#id)` markdown the parser already understands — every chip serializes back to the same string the parser sees on save. There is no new schema, no migration, no AI conformance work.

The two technically interesting questions are (1) **how to render chips inline with editable step text in MudBlazor 8.15**, which has no native primitive for that, and (2) **how to anchor `MudAutocomplete<Ingredient>` to the caret on `@`-trigger**. Recommended pattern: **per-token segmented input** (chip Razor components interleaved with `MudTextField` text segments inside a single flex-wrap container) for the inline rendering, plus a **single shared `MudAutocomplete<Ingredient>` host** that opens via a small JS-interop helper that returns the textarea's caret coordinates. This keeps the Blazor diff model intact, reuses MudBlazor primitives, and degrades cleanly to the `MudTextField Lines=3` fallback (D-D4) when JS interop fails.

Everything else — Step/Section toggle, timer suggestion popover, regex broadening, paste flow trim, auto-write removal, cooking-mode chip click → scroll, EDITOR-01 docs amendment — is straightforward; the planner can map each directly to the line ranges in the "Surface files & line ranges" section below.

**Primary recommendation:** Extract `RecipeChipComposer.razor` (read-only mode via `Interactive` parameter, used by both editor and cooking mode) and `RecipeStepEditor.razor` (per-step row owning the chip composer + Step/Section toggle + timer chip strip) under `src/CookBot.Web/Components/Pages/RecipeEditor/`. Render inline chips via interleaved Razor components inside a flex-wrap div; use a single shared `MudAutocomplete<Ingredient>` invoked via a small `recipe-chip-composer.js` interop module. Catch `JSDisconnectedException` / module-import failure in `OnAfterRenderAsync` and swap the row to a `MudTextField Lines=3` fallback. **Add bUnit 1.40+ to `tests/CookBot.Tests/CookBot.Tests.csproj`** so the chip composer can be unit-tested at component level (currently absent — confirmed by reading the csproj).

---

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

> Verbatim from `03-CONTEXT.md` `<decisions>`. The planner MUST honor every D-Xn item — these are NOT alternatives to research.

**A. Chip Composer Interaction Model (EDITOR-01)**
- **D-A1:** Two insertion paths (`@`-trigger and "Insert ingredient" button), single chip output. Both produce identical `[name](#id)` strings given the same selected ingredient (test invariant).
- **D-A2:** Click body of an existing chip → small replace-popover (re-run autocomplete to swap, or remove). Right-edge `×` icon for one-click remove. Backspace from position immediately after a chip removes it.
- **D-A3:** Chip displays name only — no `#id` index on the chip body. Index lives in the Ingredients section table at top of editor.
- **D-A4:** `[name](#id)` markdown hidden by default; **per-step "View as text / View as chips" toggle** is the escape hatch. Toggle is **ephemeral / UI-only** — not persisted, not in `Extras`. Resets to chip view on save and reload.
- **D-A5 (REQUIREMENTS edit needed — P0):** EDITOR-01 wording must be amended during plan-phase. Drop the "user-facing index" clause. New wording (proposal):
  > **EDITOR-01:** `RecipeEditor.razor`'s step textarea is replaced with a chip-aware composer built on `MudAutocomplete<Ingredient>` + `MudChipSet<T>`. Typing `@` or clicking an "Insert ingredient" affordance opens autocomplete over recipe ingredients; selecting one inserts a chip showing the ingredient name. The underlying string keeps `[name](#id)` markdown invisibly; the immutable `id` is what serializes.
- **D-A6:** Text-view → chip-view flip with unresolved `[name](#id)` → red error chip with replace-popover. Save is allowed; validator surfaces `OrphanIngredient`/`DANGLING_REF` warnings; editor displays save-time banner listing affected steps.

**B. Step / Section Toggle (EDITOR-02)**
- **D-B1:** `MudToggleGroup<StepKind>` per step row (`[Step | Section]`). Replaces today's two-button "Add Step / Add Section Header" pattern with single "Add Step" button + per-row toggle.
- **D-B2:** `Step → Section` toggle reuses current step text as the section heading.
- **D-B3:** Section steps clear timers and ingredient refs. If non-empty: `MudDialog` confirmation *"Convert to a section header? This will discard {N} timer(s) and {M} ingredient reference(s) on this step."* with `[Cancel] [Convert]`. Empty-step toggle is silent.

**C. Timer-Detection Suggestion UX (EDITOR-03)**
- **D-C1:** Inline highlight (dotted underline, Color.Warning hue) + click-to-convert popover, per-occurrence. Detection runs on debounced edit (`Immediate="true" DebounceInterval="500"`).
- **D-C2:** Per-occurrence Yes/No only — no bulk affordance.
- **D-C3:** Accepted timer chip renders in a chip strip below the step textarea. Click chip → popover with Duration / Unit / Label fields. `×` icon to remove. The original detected substring stays as plain text in the step body. Re-detection on edit does NOT re-suggest a duration already accepted into an explicit timer chip on the same step.

**D. Edge Flows (EDITOR-05/06/07)**
- **D-D1 (Paste):** Pass-through dialog. `IRecipeFormatParser.TryParse` → close → editor populates with parsed fields + inline `MudAlert` warning banner. Hand-rolled numbered-list fallback in `PasteRawTextDialog.razor:51-64` is **deleted**.
- **D-D2 (Phase 2 "Edit and save anyway"):** Same code path as D-D1. Single mental model.
- **D-D3 (Cooking mode):** Read-only chip rendering via shared `RecipeChipComposer` (`Interactive=false`). Ingredient chips clickable → JS interop scroll-into-view + transient highlight class on ingredients sidebar. Timer chips retain today's start-timer button treatment.
- **D-D4 (JS-interop fallback):** Plain `MudTextField Lines=3` fallback for step text; Save always works. In cooking mode: chips render visual-only (clicks no-op). No error banner — silent degrade.

### Claude's Discretion

> The planner makes these calls during plan-phase (no user input needed). Recommendations are surfaced in research items 5–13 below.

- Ingredient reorder mechanism on the Ingredients table (drag handles vs. arrow buttons vs. both). Recommend: keep arrow buttons + add drag handles (additive).
- Timer regex broadening per CONCERNS §7 (fractional + ranges + multi-segment; word-form numbers may be deferred). Specific patterns + parsing logic in research item 6.
- Specific Tab/Shift+Tab/Backspace/Arrow keyboard semantics inside the chip composer — refer to MudBlazor `MudChipSet<T>` defaults; planner verifies against EDITOR-07.
- axe-core / accessibility test mechanism — manual smoke checklist documented in verification artifacts (no Playwright/Selenium dep in project today). Specific 8-item checklist in research item 8.
- Replace-popover internals (`MudPopover` vs. `MudMenu` vs. custom). Recommend: `MudMenu` with `ActivationEvent="MouseEvent.Click"` + `PositionAtCursor="false"` anchored to chip ElementReference.
- Confirmation-dialog framework for Step→Section drop — `MudDialog` with reusable `SectionDropConfirmationDialog.razor`.
- Inline-highlight CSS approach — `RecipeStepTextFormatter` extension recommended (already owns rendered HTML; consistent with current cooking-mode pipeline). See research item 5.
- Component extraction (`RecipeStepEditor.razor`, `RecipeChipComposer.razor`, `IngredientChip.razor`, `TimerChip.razor`) — recommend extract.
- File layout — `src/CookBot.Web/Components/Pages/RecipeEditor/` (new folder); namespace `CookBot.Web.Components.Pages.RecipeEditor`. Confirmed in research item 12.

### Deferred Ideas (OUT OF SCOPE)

> Verbatim from CONTEXT.md `<deferred>`. Research will not investigate these.

- Timer regex word-form numbers ("ten minutes") — backlog if planner judges out of scope.
- Per-step temperature field (`OvenTempFahrenheit`) — Phase 4 (FEATURE-V2-01..05).
- `Recipe.TagsJson` → relational `RecipeTag` table — Phase 4 (POLISH-04).
- `LegacyRecipeProjector` deletion + `Recipe.IngredientRefs` column drop — Phase 4 (POLISH-03).
- `README.md` "Recipe Format" section — Phase 4 (POLISH-05/07).
- MudBlazor 9.x upgrade — FUTURE-10.
- Drag-and-drop for step reordering — discretion / not core.
- Encrypt-at-rest for `UserProfile.AiApiKey` — FUTURE-01.
- Per-sharer cookbook-import consent banner — FUTURE-12.
- Partial-JSON streaming UX — Phase 2 D-01 already chose compose-then-reveal.
- axe-core / Playwright UI test infra as a CI gate — separate infrastructure milestone.

</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|---|---|---|
| EDITOR-01 | Chip-aware composer replaces step textarea; `@`-trigger or button inserts chip; underlying `[name](#id)` markdown invisible | Items 1, 2, 3, 11 (auto-write removal), 14 (D-A5 docs amendment) |
| EDITOR-02 | Per-step `[Step | Section]` toggle; Section disables timer/ingredient-chip controls | Items 1 (MudToggleGroup), 12 (component layout); Step→Section drop confirmation in Surface files §`SectionDropConfirmationDialog.razor` |
| EDITOR-03 | Detected timer durations surfaced as suggestion-only; auto-write of timers on save removed; explicit chips are the only persisted source | Items 5 (inline timer suggestion rendering), 6 (regex broadening), 11 (auto-write deletion line ranges) |
| EDITOR-04 | Reordering ingredients preserves `id` of each ingredient | Item 1 (chip is name-lookup not index-lookup; Phase 1 D-06 already locks immutable id); no new code path needed beyond confirming chip rendering reads ingredient by id, not list-index |
| EDITOR-05 | Paste-raw-text routes through schema stack; surfaces unresolved fields in chip editor for confirmation; never persists non-conforming recipe | Item 1 (banner UI consumes ValidationResult.Warnings); Surface files §`PasteRawTextDialog.razor` (delete numbered-list fallback) |
| EDITOR-06 | Cooking-mode step view uses same chip rendering; `[name](#id)` link resolution exclusively (no substring matching — already locked by Phase 1 D-13) | Item 9 (cooking-mode chip click → scroll-and-highlight JS interop); Surface files §`CookingMode.razor` (lines 53,58 already pass through `RecipeStepTextFormatter.ToHtml`) |
| EDITOR-07 | Keyboard navigable; passes axe-core/screen-reader smoke pass; degrades gracefully if JS interop fails | Item 7 (JS-interop-fail fallback contract), Item 8 (manual smoke checklist), Item 1 (`MudChipSet<T>` keyboard defaults) |

</phase_requirements>

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|---|---|---|---|
| Chip rendering & inline-text composition | Browser / Blazor Razor (InteractiveServer) | — | Pure UI state; no domain logic. Backed by `_steps[i].Text` (in-memory list) on the Blazor circuit. |
| `@`-trigger detection & caret coordinate read | Browser (JS interop) | Blazor circuit (consumes coords) | Only the browser knows caret pixel position in a textarea; trivial JS, returns to C# via `IJSRuntime`. |
| `MudAutocomplete.SearchFunc` over ingredients | Blazor circuit (`InteractiveServer`) | EF Core / SQLite | Same as today's `RecipeEditor.razor:327-339` (`SearchIngredients`) — `DbContext.Ingredients.Where(...).Take(10)`. No change to the data tier. |
| Step/Section toggle state | Blazor circuit (per-row) | — | Ephemeral; not persisted on the entity. Maps to `RecipeDocument.StepNode` polymorphism (`ContentStep` ↔ `SectionStep`) at save time only. |
| Timer detection regex | Application layer (`TimerDetectionService`) | — | Pure C# function, called from the editor row component on debounced edit. Not crossed by Phase 3 — except the regex broadens. |
| Timer chip persistence | Blazor circuit → Application (`RecipeService`) → DB | EF Core (`RecipeStep.Timers` JSON-owned column) | Already wired today via `ParsedStep.Timers` → `RecipeService.CreateAsync/UpdateAsync` step.Timers projection. Phase 3 deletes the regex auto-write fallback at the same call site (lines 75 / 142). |
| Cooking-mode scroll-into-view | Browser (JS interop) | — | Native `Element.scrollIntoView({block, behavior})` + CSS class toggle. Belongs entirely in `recipe-chip-composer.js`. |
| Paste flow parsing | Application (`IRecipeFormatParser.TryParse`) | — | No tier change — Phase 1 already centralized this. Phase 3 thins the dialog (deletes numbered-list fallback). |
| Validator warning banner | Blazor circuit (reads `ValidationResult.Warnings`) | Application (`RecipeValidator` already returns warnings) | Phase 2 Plan 5 shipped `OrphanIngredient` + `EmptySection` warnings. Phase 3 surfaces them in UI; no Application-tier change. |

**Why this matters:** The chip composer is genuinely a *view-layer* feature. The recurring trap on phases like this is to "improve" the canonical schema along the way (e.g. "what if we stored the chips structurally?") — that would re-open the formatting standardization Phase 1 just closed. Every chip serializes back to the same `[name](#id)` text the parser sees today. Phase 3 holds this line.

---

## Standard Stack

### Core (already in the project — no NuGet additions for these)

| Library | Version | Purpose | Why Standard |
|---|---|---|---|
| MudBlazor | 8.15.0 | Chip composer, autocomplete, toggle group, popover/menu, dialog | Already the only UI toolkit; CONTEXT.md locks 8.15 (no v9 upgrade). [VERIFIED: `src/CookBot.Web/CookBot.Web.csproj` and `.planning/codebase/STACK.md` line 51] |
| Markdig | 0.45.0 | Untouched in this phase (already locked down to `DisableHtml()` in `AiChat.razor`) | Phase 2 AI-08-AUDIT settled this surface. Phase 3 doesn't read assistant markdown. [VERIFIED: STACK.md line 54] |
| `RecipeStepTextFormatter` (BCL) | — | Extends to wrap detected timer-duration substrings in `<span data-timer-suggestion>` for inline highlight (D-C1) | Already owns the rendered HTML pipeline (`[name](#id)` resolution); recommend extending here over JS DOM mutation. [VERIFIED: `src/CookBot.Application/Services/RecipeStepTextFormatter.cs` exists, lines 1–66] |

### Supporting (NEW — one NuGet addition recommended)

| Library | Version | Purpose | When to Use |
|---|---|---|---|
| **bUnit** | **1.40.0** (current as of April 2026) | Razor-component unit tests for chip composer behavior (insertion via `@`-path vs button-path, replace-popover swap, click-to-remove vs Backspace-to-remove, Step/Section toggle drop confirmation, timer suggestion accept/dismiss) | **Add to `tests/CookBot.Tests/CookBot.Tests.csproj`**. CONTEXT.md says "bUnit if available." It is **not** present today (verified — see Surface files §csproj). Without bUnit the chip composer is testable only by manual smoke. With bUnit, all six test files in CONTEXT.md `Source files this phase creates` become real automated tests. [VERIFIED: csproj lines 10-16 — only xunit/coverlet/EFCore.Sqlite/Test.Sdk; no bUnit] |

**Version verification:**
```bash
# Confirm latest stable bUnit before locking the version in csproj
dotnet add package bunit --prerelease   # or: dotnet add package bunit
# Targets net6+ / net8+ / net9+; .NET 10 supported via netstandard2.0/2.1 fallback for the snapshot machinery,
# component rendering uses the actual project's TFM. Confirmed via package's published target frameworks.
```
[CITED: https://bunit.dev/docs/getting-started/] [ASSUMED: 1.40.x is the latest stable line as of April 2026 — verify with `dotnet add package bunit --version *` before adding. Fallback: 1.36.x is the long-stable LTS that matches xUnit 2.9.2.]

### Alternatives Considered

| Instead of | Could Use | Tradeoff |
|---|---|---|
| Per-token segmented input (recommendation, item 2 below) | `contenteditable`-based composer | Less Blazor-friendly diff model; keyboard nav harder to maintain; tougher fallback path. Rejected. |
| Per-token segmented input | Pure structured `MudChipSet` per step + a separate `MudTextField` for "free text not in any chip" | Clean but loses the inline-flow visual where chips appear *between* words ("simmer the [Sauce] for 10 min"). Rejects D-A1's "natural typing flow" UX intent. |
| `RecipeStepTextFormatter` extension for timer highlight | JS DOM mutation on the rendered textarea | Conflicts with Blazor's diff model; harder to test; doesn't survive re-render. The formatter already owns the HTML; extending is the smallest change. |
| bUnit | Manual smoke only | CONTEXT.md notes this is acceptable as a fallback. But once bUnit is added, EDITOR-07 Tab/Shift+Tab/Backspace semantics become unit-testable, which is much higher leverage than re-running the smoke checklist on every PR. |
| `MudPopover` for replace-popover | `MudMenu` | `MudMenu` has built-in click-to-open + ESC-close + focus management. `MudPopover` is lower-level. Both work. `MudMenu` recommended. |

**Installation (planner: add to phase plan):**
```xml
<!-- tests/CookBot.Tests/CookBot.Tests.csproj — add inside the existing <ItemGroup> -->
<PackageReference Include="bunit" Version="1.40.0" />
```

---

## Architecture Patterns

### System Architecture Diagram

```
┌──────────────────────────── Browser (InteractiveServer circuit) ────────────────────────────┐
│                                                                                              │
│  RecipeEditor.razor (entry page, ~250 lines after this phase — was 468)                     │
│   │                                                                                          │
│   ├── Metadata MudPaper (lines 28-50, unchanged)                                            │
│   ├── Ingredients MudPaper (lines 52-106, mostly unchanged; reorder affordance per discretion)│
│   └── Steps MudPaper                                                                         │
│         │                                                                                    │
│         └── RecipeStepEditor.razor  (NEW — one per step row)                                │
│               │                                                                              │
│               ├── MudToggleGroup<StepKind>  ◄── D-B1 [Step | Section]                       │
│               ├── (when Step) RecipeChipComposer.razor  ◄── D-A1..A6, D-D4                  │
│               │     │                                                                        │
│               │     ├── flex-wrap div with interleaved children:                            │
│               │     │     ├── IngredientChip.razor  (rendered for each [name](#id) match)    │
│               │     │     │     └── click → MudMenu replace-popover ◄── D-A2                │
│               │     │     └── editable text segment (MudTextField inline OR contenteditable │
│               │     │           span — see item 2 recommendation)                            │
│               │     │                                                                        │
│               │     ├── @-trigger → JS interop (recipe-chip-composer.js)                    │
│               │     │     getCaretCoords(textarea) → x/y                                    │
│               │     │     anchored MudAutocomplete<Ingredient> opens                        │
│               │     │                                                                        │
│               │     ├── "Insert ingredient" button → opens same MudAutocomplete as popover  │
│               │     │                                                                        │
│               │     ├── "View as text / View as chips" toggle (per-step ephemeral) ◄── D-A4 │
│               │     │                                                                        │
│               │     └── InlineTimerSuggestion.razor (overlays dotted-underline + Yes/No popover)│
│               │           ◄── D-C1, D-C2                                                    │
│               │                                                                              │
│               └── TimerChip strip (below text) — chip per step.Timers entry                 │
│                     ├── click chip → edit popover (Duration / Unit / Label) ◄── D-C3        │
│                     └── × icon → remove                                                     │
│                                                                                              │
│  CookingMode.razor (read-only consumer)                                                      │
│   └── RecipeChipComposer.razor (Interactive=false)                                          │
│         ├── ingredient chips clickable → JS interop scroll-into-view + highlight ◄── D-D3   │
│         └── timer chips → existing start-timer button (cooking-timers.js, unchanged)        │
│                                                                                              │
│  PasteRawTextDialog.razor (entry point for raw text)                                        │
│   └── Parser.TryParse → close → caller (RecipeEditor) populates + shows MudAlert warnings   │
│         (numbered-list fallback at lines 51-64 DELETED) ◄── D-D1                            │
│                                                                                              │
└──────────────────────────────────────────────────────────────────────────────────────────────┘
                                            │
                                            ▼ (existing flow, unchanged)
                ┌────────────────────────────────────────────────────────┐
                │  RecipeService.CreateAsync / UpdateAsync               │
                │   ◄── DELETE auto-write at lines 75, 142 (EDITOR-03)   │
                │   step.Timers = ps.Timers.Select(...).ToList();        │
                │   (no fallback to TimerDetectionService)               │
                └────────────────────────────────────────────────────────┘
```

### Recommended Project Structure

```
src/CookBot.Web/Components/Pages/
├── RecipeEditor.razor                         # ~250 lines after extract; thin shell + metadata + ingredients
├── RecipeEditor/                              # NEW folder
│   ├── RecipeStepEditor.razor                 # NEW — per-step row, owns toggle + composer + timer chip strip
│   ├── RecipeChipComposer.razor               # NEW — shared (interactive vs read-only) chip flow surface
│   ├── IngredientChip.razor                   # NEW — chip body + replace-popover MudMenu
│   ├── TimerChip.razor                        # NEW — explicit timer chip + edit popover
│   ├── InlineTimerSuggestion.razor            # NEW — dotted-underline overlay + Yes/No popover
│   └── SectionDropConfirmationDialog.razor    # NEW — D-B3 confirm with N timers / M refs counts
├── CookingMode.razor                          # MODIFIED — replace lines 50-59 with <RecipeChipComposer Interactive="false" .../>
└── PasteRawTextDialog.razor                   # MODIFIED — delete lines 51-64 (numbered-list fallback)

src/CookBot.Web/wwwroot/js/
├── cooking-timers.js                          # UNCHANGED (existing)
├── download.js                                # UNCHANGED (existing)
└── recipe-chip-composer.js                    # NEW — caret coords + scrollIntoViewWithHighlight; module pattern matches cooking-timers.js

tests/CookBot.Tests/
├── CookBot.Tests.csproj                       # MODIFIED — add bUnit 1.40.0 PackageReference
└── Web/                                       # NEW namespace folder
    ├── RecipeChipComposerTests.cs             # bUnit — insertion paths produce identical strings; replace-popover; remove
    ├── StepSectionToggleTests.cs              # bUnit — Step→Section reuse heading; non-empty drop confirmation
    ├── TimerSuggestionTests.cs                # bUnit — debounced detection, per-occurrence accept, no re-suggest
    └── PasteFlowTests.cs                      # bUnit — paste pass-through populates editor + banner; D-D2 reuses path
```

Namespaces follow folder structure (project convention, CONVENTIONS.md §Project Layout): `CookBot.Web.Components.Pages.RecipeEditor` for the new sub-folder. Each Razor component starts file-scoped with no explicit namespace block (CONVENTIONS.md §File-Scoped Namespaces).

### Pattern 1: Two Insertion Paths, One Chip String (D-A1)

**Rationale:** The hard test invariant from CONTEXT.md is "chip from `@`-path == chip from button-path given the same selected ingredient." Centralize the chip-creation logic into a single internal helper.

```csharp
// Inside RecipeChipComposer.razor @code — recommended pattern
private void InsertChipForIngredient(Ingredient ing, int caretIndex)
{
    // Single source of truth — both insertion paths call this method.
    // The serialized form is exactly what the Phase 1 parser sees on save.
    var chipMarkdown = $"[{ing.Name}](#{ing.Id})";

    // Splice into the underlying string at caretIndex, replacing any "@partial" prefix
    // (when called from @-trigger path) or empty range (when called from button path).
    Text = Text.Substring(0, caretIndex - PartialAtTokenLength)
         + chipMarkdown
         + Text.Substring(caretIndex);

    // Re-tokenize for chip rendering on next render pass (see Pattern 2)
    StateHasChanged();
}
```

The bUnit test for D-A1 invariant:
```csharp
[Fact]
public async Task InsertViaAtTrigger_AndInsertViaButton_ProduceIdenticalUnderlyingText()
{
    var ing = new Ingredient { Id = 3, Name = "Salt" };
    using var atCtx = new TestContext();
    var atCmp = atCtx.RenderComponent<RecipeChipComposer>(p => p.Add(c => c.Text, "Add @ to taste"));
    await atCmp.InvokeAsync(() => atCmp.Instance.SimulateAtTriggerSelection(ing, caretIndex: 5));

    using var btnCtx = new TestContext();
    var btnCmp = btnCtx.RenderComponent<RecipeChipComposer>(p => p.Add(c => c.Text, "Add  to taste"));
    await btnCmp.InvokeAsync(() => btnCmp.Instance.SimulateButtonInsertion(ing, caretIndex: 4));

    Assert.Equal(atCmp.Instance.Text, btnCmp.Instance.Text);  // both end up as "Add [Salt](#3) to taste"
}
```

### Pattern 2: Per-Token Segmented Inline Layout (RECOMMENDED — see Item 2 for full trade-off)

**What:** Render chips and editable text segments interleaved inside a single `display: flex; flex-wrap: wrap;` container. On each render, scan the string for `[name](#id)` matches via `RecipeStepTextFormatter.IngredientLinkPattern`, emit alternating Razor children: chip component for each match, plain editable span (or `MudTextField` micro-input) for each gap.

**Why:** It's the only pattern that simultaneously (a) keeps Blazor's diff model clean, (b) doesn't fight `contenteditable`'s legendary keyboard/IME quirks, (c) degrades trivially to a single `MudTextField Lines=3` when JS interop fails (D-D4), and (d) lets each chip be a real Razor component (not a serialized DOM string) which is what makes the click-to-replace popover (D-A2) practical.

**Code sketch:**
```razor
<div class="chip-flow" style="display:flex;flex-wrap:wrap;align-items:center;gap:0.25rem;">
@foreach (var token in TokenizeText(Text))
{
    @if (token.IsChip)
    {
        <IngredientChip Ingredient="@LookupById(token.IngredientId)"
                        OnRemove="@(() => RemoveChipAt(token.Index))"
                        OnReplace="@(ing => ReplaceChipAt(token.Index, ing))" />
    }
    else
    {
        <span contenteditable="true" @key="@token.Index"
              @onfocus="@(() => _activeSegmentIndex = token.Index)"
              @onkeydown="OnSegmentKeyDown"
              @oninput="@(e => UpdateSegment(token.Index, e.Value?.ToString() ?? ""))">
            @token.Text
        </span>
    }
}
</div>
```

**Where:** Inside `RecipeChipComposer.razor`. The `TokenizeText` helper reuses the already-compiled `RecipeStepTextFormatter.IngredientLinkPattern` (line 12-14 of that file) — do **not** re-define the regex.

### Pattern 3: Caret-Anchored MudAutocomplete via JS Interop

**What:** Single `MudAutocomplete<Ingredient>` instance per `RecipeChipComposer`, normally hidden (`Style="display:none"` or behind a `_pickerVisible` bool). On `@`-trigger:
1. JS reads textarea/contenteditable caret coordinates → returns `{x, y}` to C#.
2. C# positions an absolutely-positioned wrapper `<div>` around the `MudAutocomplete` at those coords.
3. `_pickerVisible = true; StateHasChanged()`.
4. The autocomplete's `SearchFunc` filters ingredients by typed prefix (the substring after `@`).
5. On `OnValueChanged` (selection): `InsertChipForIngredient(...)`, hide picker.

**Code:**
```javascript
// src/CookBot.Web/wwwroot/js/recipe-chip-composer.js
window.RecipeChipComposer = {
    getCaretCoords(elementId) {
        const el = document.getElementById(elementId);
        if (!el) return null;
        const rect = el.getBoundingClientRect();
        // For <textarea> use a hidden mirror div technique; for contenteditable use Range.getBoundingClientRect()
        const sel = window.getSelection();
        if (sel && sel.rangeCount) {
            const r = sel.getRangeAt(0).cloneRange();
            r.collapse(true);
            const cr = r.getClientRects()[0];
            if (cr) return { x: cr.left - rect.left, y: cr.bottom - rect.top };
        }
        return { x: 0, y: rect.height };  // fallback: bottom-left of element
    },

    scrollIntoViewWithHighlight(elementId, highlightClass = 'chip-highlight-pulse', durationMs = 1500) {
        const el = document.getElementById(elementId);
        if (!el) return false;
        el.scrollIntoView({ behavior: 'smooth', block: 'center' });
        el.classList.add(highlightClass);
        setTimeout(() => el.classList.remove(highlightClass), durationMs);
        return true;
    },

    ping() { return 'ok'; }
};
```

The `ping()` method is the JS-interop-fail detection probe — see item 7.

### Pattern 4: Step/Section toggle round-trip (D-B1, D-B2, D-B3)

**What:** A `MudToggleGroup<StepKind>` per `RecipeStepEditor` row, where `StepKind` is a local enum:

```csharp
private enum StepKind { Step, Section }
private StepKind _kind;  // bound to the toggle

private async Task OnKindChanged(StepKind newKind)
{
    if (newKind == StepKind.Section && _kind == StepKind.Step
        && (_step.Timers?.Any() == true || HasIngredientRefs(_step.Text)))
    {
        // Non-empty drop — show confirmation per D-B3
        var (timerCount, refCount) = (_step.Timers?.Count ?? 0, CountIngredientRefs(_step.Text));
        var parameters = new DialogParameters<SectionDropConfirmationDialog>
        {
            { d => d.TimerCount, timerCount },
            { d => d.RefCount, refCount }
        };
        var dlg = await DialogService.ShowAsync<SectionDropConfirmationDialog>("Convert to section header?", parameters);
        var result = await dlg.Result;
        if (result is null || result.Canceled) return;  // toggle reverts via @bind state mismatch
    }
    _kind = newKind;
    if (newKind == StepKind.Section)
    {
        // D-B2 — reuse text as heading
        _sectionHeading = _step.Text;
        _step.Timers?.Clear();
        // (Ingredient refs in text vanish at save time when projected to SectionStep)
    }
    StateHasChanged();
}
```

The `MudDialog` flow follows the existing pattern in `PasteRawTextDialog.razor:35` (`MudDialog.Cancel()` and `MudDialog.Close(DialogResult.Ok(...))`).

### Anti-Patterns to Avoid

- **Anti-pattern: Storing chip state as a separate structured field on `RecipeStep`.** D-A4 explicitly says the toggle is ephemeral; the canonical record is text-backed (STATE.md Q2). Adding a `ChipModeOnly: bool` column or stuffing it into `Extras` re-opens schema work and breaks the round-trip invariant. **Don't.**
- **Anti-pattern: Relying on `contenteditable` for the entire step text region without per-token component boundaries.** Loses click-to-replace popover anchoring (each chip needs to be a real Razor component, not a `<span>` with a click handler in JS). It also fights Blazor's diff model on every keystroke.
- **Anti-pattern: Re-defining the `[name](#id)` regex inside the chip composer.** `RecipeStepTextFormatter.IngredientLinkPattern` already exists (line 12-14, compiled, used by Phase 1's substring-fallback removal). The validator (`RecipeValidator.cs:15-17`) and the orphan-detector (`RecipeValidator.cs:105`) also use the same pattern. Reuse, do not re-spell.
- **Anti-pattern: Auto-converting timer suggestions on Save.** EDITOR-03 explicitly removes the auto-write fallback (`RecipeService.cs:75` and `:142`). Per-occurrence Yes/No is the only persistence path. Even an "auto-convert obvious cases" shortcut violates the spec.
- **Anti-pattern: Adding a second AI provider abstraction or pulling in `Microsoft.Extensions.AI` / official `Anthropic` NuGet.** CLAUDE.md `Things to avoid`. (Phase 3 doesn't touch AI — but Phase 3 plans should not introduce these for any reason.)
- **Anti-pattern: Adding `Newtonsoft.Json`.** CLAUDE.md / `.planning/REQUIREMENTS.md` Out of Scope. The chip composer uses only `System.Text.Json` (in fact, Phase 3 uses **no** new JSON serialization).

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---|---|---|---|
| Inline chip rendering with editable text | Custom DOM parser / VDOM | `RecipeStepTextFormatter.IngredientLinkPattern` + per-token Razor child components (Pattern 2) | The regex + tokenization already exists; rolling a parser is duplicative and error-prone. |
| `[name](#id)` validation | Custom string-walking validator in the editor | `RecipeValidator.Validate(RecipeDocument)` | Phase 1 D-08 already returns `ValidationResult.Errors` (DANGLING_REF) and `Warnings` (OrphanIngredient). The editor consumes these — does not re-implement them. |
| Caret coordinate calculation in a textarea | Visible mirror div with character measurement loop | One small `getCaretCoords` JS function with `Range.getBoundingClientRect()` (Pattern 3) | Browser API does this directly; no need to measure. The 30-line mirror-div technique is only needed for plain `<textarea>` (which the chip composer doesn't use — it uses `contenteditable` per-segment per Pattern 2). |
| Step/Section confirmation dialog | Custom modal | `MudDialog` + `IDialogService.ShowAsync<TDialog>(...)` | Existing pattern in `PasteRawTextDialog.razor`, `ShareCookbookDialog.razor`, `SaveRecipeDialog.razor`. |
| Replace-popover anchored to a chip | Custom positioning | `MudMenu ActivationEvent="MouseEvent.Click"` | Built-in: anchors to the parent button/element, has ESC-close, focus trap, aria roles. |
| Step/Section segmented control | Two `MudButton`s with state | `MudToggleGroup<StepKind>` + two `MudToggleItem<StepKind>` | Built-in segmented appearance, accessible (`role="radiogroup"`), keyboard nav (Arrow keys), single-click switch. |
| Drag-and-drop on step rows | Custom `dragstart`/`dragover`/`drop` handlers | (Out of scope — keep arrow buttons; defer DnD to Phase 4 polish or backlog) | EDITOR-04 invariant is "preserve `id` across reorder," already satisfied by today's arrow buttons. DnD is a UX nicety. |
| Manual focus management on chip insert/remove | Custom `await JS.InvokeVoidAsync("focus", ...)` ladders | `ElementReference.FocusAsync()` (built-in) for the active text segment after chip insertion | Native Blazor API, no JS interop call needed. |
| Inline timer-suggestion `<span>` wrapping | Custom HTML string concat | Extend `RecipeStepTextFormatter.ToHtml` to also wrap detected-timer substrings (Item 5) | The formatter already runs `WebUtility.HtmlEncode`; adding a second pattern pass is ~15 lines. |

**Key insight:** This phase has no domain logic. Every "build vs reuse" answer is "reuse" because Phase 1 already built every regex, validator, parser, and resolver this composer needs. The chip composer is wiring; the planner should resist any task that adds a new service/regex/parser.

---

## Surface files & line ranges to modify

> Pin these into each plan's `read_first` and `files_modified` frontmatter.

### `src/CookBot.Web/Components/Pages/RecipeEditor.razor` (468 lines today)

| Line range | Today | After Phase 3 |
|---|---|---|
| 108-121 | Steps section header — two buttons "Add Step" + "Add Section Header" | Single "Add Step" button. New rows default to `StepKind.Step`. |
| 122-174 | Inline `@for` step rendering with `MudTextField`, regex-detected timer/ingredient chips inline | Replaced with `<RecipeStepEditor Step="step" Index="index" Ingredients="_ingredients" OnRemove="@(() => RemoveStep(index))" OnMove="..." />`. The for-loop stays; the body collapses to one component invocation per row. |
| 137-167 | `step.IsSection` branch + raw `MudTextField` + ad-hoc detection chips | Moves entirely into `RecipeStepEditor.razor`. |
| 144-145 | `TimerDetectionService.DetectTimers(...)` + `DetectIngredientRefsInStep(...)` ad-hoc calls | Moves into `RecipeStepEditor` (timers) and `RecipeChipComposer` (ingredient-ref tokenization). |
| 169-181 | "No steps yet" placeholder | Stays as-is (still belongs on the parent page). |
| 185-204 | `@code { ... [Parameter]s and metadata fields }` | Add new state: top-of-editor `MudAlert _warningBanner` for D-D1/D-D2 (`ValidationResult.Warnings` from parser/orchestrator hand-off). |
| 273-305 | `PopulateFromParsed(ParsedRecipe parsed)` | Extend to also populate the warning banner from any parser-returned `errors`/warnings list. |
| 343-351 | `AddStep()` / `AddSectionHeader()` methods | Collapse to single `AddStep()`; the per-row `MudToggleGroup` lives in `RecipeStepEditor`. **Delete `AddSectionHeader()`.** |
| 371-381 | `DetectIngredientRefsInStep` (substring-match-by-name) | **Delete entirely.** Phase 1 D-13 already retired substring detection at the persistence path; this UI helper is the last vestige. The chip composer reads `[name](#id)` matches via the formatter's regex. |
| 385-396 | `PasteRawText()` dialog launch | Stays — but the receiving-side surface (lines 273-305) populates the new warning banner from the parser's error list. |
| 400-459 | `SaveRecipe()` | **Stays unchanged in body**, but the `parsed.Steps` list now reflects the chip-composer state (which writes `[name](#id)` to `step.Text` and writes the persisted timer-chip list to `step.Timers`). |

**Line-count budget:** RecipeEditor.razor should drop from 468 → ~250 after extraction.

### `src/CookBot.Web/Components/Pages/CookingMode.razor` (498 lines today)

| Line range | Today | After Phase 3 |
|---|---|---|
| 50-55 | `@if (CurrentSectionHeader != null)` block rendering section header via `RecipeStepTextFormatter.ToHtml` | Replace inner `MudText` content with `<RecipeChipComposer Interactive="false" Text="@CurrentSectionHeader" Ingredients="@_recipe.RecipeIngredients..." />`. The shared composer renders the same chip visuals. |
| 57-59 | `MudText Typo="Typo.h4"` rendering `CurrentStep.Text` via `RecipeStepTextFormatter.ToHtml` | Replace with `<RecipeChipComposer Interactive="false" Text="@CurrentStep.Text" Ingredients="@_recipe.RecipeIngredients..." OnIngredientChipClick="ScrollToIngredient" />`. |
| 61-81 | Active timers rendered via `MudButton Variant="Outlined"...StartIcon="@Icons.Material.Filled.Timer"` (today's start-timer button mapping) | **Stays as-is.** D-D3 says timer chips retain today's start-timer button treatment. Visual-style alignment with the editor's `Color.Warning` happens via styling (small change, optional). |
| 138-156 | Ingredient sidebar (`@foreach (var ri in _recipe.RecipeIngredients.OrderBy(...))`) | Add `id="ingredient-{ri.RecipeLocalId}"` attribute to each `MudStack Row` so `RecipeChipComposer.OnIngredientChipClick` can call `RecipeChipComposer.scrollIntoViewWithHighlight("ingredient-3", ...)` from JS. |
| 197-310 | `@code { ... }` lifecycle | Add `private async Task ScrollToIngredient(int recipeLocalId)` that calls JS interop with element id `ingredient-{recipeLocalId}`; catches `JSDisconnectedException` for D-D4 graceful degrade. |

### `src/CookBot.Web/Components/Pages/PasteRawTextDialog.razor` (68 lines today)

| Line range | Today | After Phase 3 |
|---|---|---|
| 1-49 | Dialog shell, MudTextField, error banner | **UNCHANGED.** |
| **51-64** | **Hand-rolled numbered-list fallback** (regex `^\d+\.\s*` strip, build partial `ParsedRecipe` with steps only) | **DELETED entirely.** D-D1: Phase 1 D-10 already routes coercion-with-warnings inside `RecipeFormatParser.TryParse`; the dialog's hand-rolled fallback is redundant. |
| 66-67 | Display parser errors inline | UNCHANGED — `_errors = errors` from the parser. |

After deletion, `Submit()` becomes:
```csharp
private void Submit()
{
    _errors.Clear();
    if (string.IsNullOrWhiteSpace(_rawText)) return;
    if (Parser.TryParse(_rawText, out var parsed, out var errors))
    {
        MudDialog.Close(DialogResult.Ok(parsed));
        return;
    }
    _errors = errors;
}
```
Net diff: ~14 lines deleted, 0 added.

### `src/CookBot.Application/Services/RecipeService.cs` (185 lines today)

| Line range | Today | After Phase 3 |
|---|---|---|
| **65-79** (CreateAsync step loop) | `Timers = ps.IsSection ? new() : (ps.Timers?.Any() == true ? ps.Timers.Select(...).ToList() : TimerDetectionService.DetectTimers(ps.Text))` | `Timers = ps.IsSection ? new() : (ps.Timers ?? new()).Select(t => new StepTimer { Duration = t.Duration, Unit = t.Unit, Label = t.Label }).ToList()` — **delete the `: TimerDetectionService.DetectTimers(...)` fallback**. |
| **131-147** (UpdateAsync step loop) | Same pattern as above | Same deletion as above. |

The `using` import for `CookBot.Application.Services` (where `TimerDetectionService` lives) stays — `TimerDetectionService` is still called by the Phase 3 `RecipeStepEditor` for inline suggestions; it's just not auto-written on save anymore.

**Tests to update:** Any test in `tests/CookBot.Tests/Services/RecipeServiceTests.cs` (if it exists — check during plan-phase) or `OwnershipTests.cs` that asserts timer auto-detection-on-save behavior. After change, those assertions invert: "step text containing '25 minutes' but no `Timers` results in zero persisted timers." `TimerDetectionServiceTests.cs` (existing) keeps testing the regex itself in isolation, unchanged behavior.

### `src/CookBot.Application/Services/TimerDetectionService.cs` (29 lines today)

The regex broadens per Item 6 below. New file is ~70 lines including helpers; canonical patterns are listed in Item 6 with copy-paste-ready snippets.

### `src/CookBot.Application/Services/RecipeStepTextFormatter.cs` (66 lines today)

Extension recommended in Item 5: add a second pass that wraps detected-timer substrings in `<span class="timer-suggestion" data-duration-seconds="…">…</span>` (idempotent — does not double-wrap inside an already-wrapped `<span>`). New file size ~115 lines.

### `tests/CookBot.Tests/CookBot.Tests.csproj` (39 lines today)

Add `<PackageReference Include="bunit" Version="1.40.0" />` inside the existing first `<ItemGroup>` (lines 10-16) — alphabetical order between `Microsoft.NET.Test.Sdk` and `coverlet.collector` is fine but conventional placement is right after the test SDK.

### `.planning/REQUIREMENTS.md` (EDITOR-01 amendment, P0)

| Line | Today | After Phase 3 |
|---|---|---|
| **50** | `- [ ] **EDITOR-01**: ... selecting one inserts a chip; the underlying string keeps `[name](#id)` markdown invisibly.` | `- [ ] **EDITOR-01**: ... selecting one inserts a chip showing the ingredient name. The underlying string keeps `[name](#id)` markdown invisibly; the immutable `id` is what serializes.` |

The diff is small but **load-bearing** — D-A5 says the planner MUST include this as an explicit task, not "Claude's discretion." The new wording is verbatim what CONTEXT.md D-A5 proposes. Verified anchor: line 50 of `.planning/REQUIREMENTS.md` has the EDITOR-01 bullet.

### Files NOT modified (sanity check)

- `src/CookBot.Web/Components/Pages/AiChat.razor` — Phase 2 wired the structured-output flow; the "Save Recipe to Cookbook" button (line 179) routes to `SaveRecipeDialog`, which writes via `RecipeService` — picks up the chip composer for free. **No AiChat changes expected.** [VERIFIED: lines 175-182 + the SaveRecipeFromMessageAsync flow at 531-562 — handoff lands cleanly in `RecipeEditor` once that page swaps to the chip composer.]
- `src/CookBot.Web/Components/App.razor` — script registration (lines 19-22 today: MudBlazor.min.js, blazor.web.js, cooking-timers.js, download.js). **Add one line:** `<script src="js/recipe-chip-composer.js"></script>` after line 22. (Confirmed via grep — these four scripts are the only entries.)
- `src/CookBot.Web/wwwroot/app.css` — already has `.recipe-body .ingredient-ref` styling (lines 26-36). Phase 3 adds a `.chip-highlight-pulse` and `.timer-suggestion` block (~15 lines) — decide between extending app.css and a per-component scoped CSS file (`.razor.css`); CONVENTIONS.md suggests app.css is the only stylesheet in use.
- All Phase 1 / Phase 2 schema, validator, parser, AI orchestrator code — **untouched.**

---

## Common Pitfalls

### Pitfall 1: Re-tokenizing the entire step on every keystroke
**What goes wrong:** A 50-step recipe with 5 chips per step = 250 chip components re-rendered on every keystroke. Blazor Server's SignalR diff blows up.
**Why it happens:** Naïve approach: `Text` is a single `@bind-Value`-bound string; on every input, the whole thing re-tokenizes and the chip array is rebuilt.
**How to avoid:** Use `@key` on each token (chip or text segment) tied to a stable token index, not array position. When the user edits one segment, only that segment's component re-renders; chips don't unmount. Debounce the re-tokenization itself (existing `Immediate="true" DebounceInterval="500"` from RecipeEditor.razor:148 is the right cadence).
**Warning signs:** Editor feels "laggy" with > 20 chips; SignalR roundtrip times >100ms per keystroke.

### Pitfall 2: Saving with an unresolved chip silently writes a non-existent ingredient ref
**What goes wrong:** User pastes `[Pomegranate](#9)` but ingredient #9 was deleted. The chip renders as red error chip (D-A6), the user clicks Save, and either (a) the recipe persists with a dangling ref, or (b) the validator throws and Save crashes.
**Why it happens:** D-A6 says "Save is allowed with unresolved chips, but the validator surfaces a warning." But Phase 1 `RecipeValidator` returns `DANGLING_REF` as an **error**, not a warning (`RecipeValidator.cs:64-69`). If `IsValid == false`, Phase 1's invariant "non-conforming recipes never persist" kicks in and Save fails.
**How to avoid:** Either (a) reclassify dangling-ref to a warning in the validator (touches Phase 1 code — risky, breaks Phase 1 contracts), OR (b) the editor's save-time banner explicitly blocks save when `Errors.Any()` and surfaces the offending steps in the banner with "fix or delete" CTAs. **Recommendation: option (b).** D-A6 says "Save is allowed with unresolved chips" but really means "the editor doesn't proactively block; the validator still gates." Plan must clarify this language so the executor doesn't try to weaken the validator.
**Warning signs:** Save click silently does nothing with no error; or recipes persist with `[Foo](#999)` in step text and the cooking-mode renderer crashes on highlight resolution.

### Pitfall 3: Per-step "View as text" toggle leaks into the canonical record
**What goes wrong:** A well-meaning executor adds a `ViewMode: string?` to `Extras` ("just for round-trip safety") so the user's per-step toggle state survives reload. Suddenly every recipe in the wild has phantom UI state in its canonical document.
**Why it happens:** `Extras` is forward-compat infrastructure (Phase 1 D-05) — it's tempting to use as a "small UI state stash."
**How to avoid:** D-A4 is explicit: ephemeral, not persisted, **not in Extras**. Toggle state lives in the `RecipeStepEditor.razor` `_viewMode` private field. Resets to chip view on save and reload. Documented in the plan as "no schema change, no Extras write."
**Warning signs:** Phase 4 round-trip tests start failing because some recipes have a `viewMode` key in their canonical JSON.

### Pitfall 4: Cooking mode chip click → JS interop fails → user-visible error
**What goes wrong:** D-D4 says cooking mode degrades silently when interop fails. But a naive `await JS.InvokeVoidAsync("RecipeChipComposer.scrollIntoViewWithHighlight", id)` without try/catch surfaces `JSDisconnectedException` as a snackbar error.
**Why it happens:** `JSDisconnectedException` is the standard Blazor Server "circuit lost" exception during reconnect or page navigation. Existing `CookingMode.razor:235-237` catches it for `CookingTimers.dispose`; Phase 3 needs the same pattern for the new interop calls.
**How to avoid:** Wrap every JS call site in `try { await JS.InvokeVoidAsync("...", ...); } catch (JSDisconnectedException) { /* graceful degrade */ }`. Don't surface to snackbar.
**Warning signs:** Users see "JS interop disconnected" red snackbars after page transitions or laptop sleep/wake.

### Pitfall 5: Step→Section toggle with non-empty step lands the user in a confirmation, they Cancel, but the toggle visual state has already changed
**What goes wrong:** `MudToggleGroup` is two-way bound with `@bind-SelectedValue`. Clicking "Section" flips the visual state immediately; the dialog opens *after*; user clicks Cancel; visual state stays on "Section" while the underlying `_kind` is still "Step". UI lies.
**Why it happens:** Two-way binding doesn't automatically revert on async dialog cancel.
**How to avoid:** Bind the toggle one-way (`SelectedValue="@_kind"` not `@bind-SelectedValue`); handle `SelectedValueChanged` callback that runs the confirmation; only flip `_kind` and call `StateHasChanged()` after confirmation Convert. Cancel = no state change.
**Warning signs:** UI inconsistency reports — "I cancelled but the toggle still shows Section."

### Pitfall 6: bUnit version mismatch with .NET 10 SDK
**What goes wrong:** bUnit 1.40 lists target frameworks net6.0/net8.0/net9.0; .NET 10 may or may not be in the target list as of April 2026. Compile error or runtime error.
**Why it happens:** bUnit lags major .NET releases by a few months.
**How to avoid:** Verify `dotnet add package bunit --version *` lists a version that supports `net10.0` before locking. If not, fallback options in priority order: (a) bUnit prerelease build that supports net10, (b) bUnit 1.36 LTS (xUnit 2.9 compatible) targeted at netstandard2.1 — usually loads fine on net10, (c) defer bUnit to a follow-up plan and rely on manual smoke for Phase 3.
**Warning signs:** `dotnet test` fails with `The package bunit 1.40.0 is not compatible with net10.0` or runtime `MissingMethodException` from bUnit's renderer.

### Pitfall 7: AI-08-AUDIT regression — chip composer accepts pasted markdown HTML
**What goes wrong:** A user pastes `<img src=https://attacker.com/log onerror=...>` into a chip composer text segment. If the segment uses `contenteditable`, the browser may accept the raw HTML and inject the img into the DOM. AI-08-AUDIT's Markdig lockdown only protects the AI-rendered chat bubble, not arbitrary user-typed content in the editor.
**Why it happens:** Phase 2's lockdown is at the markdown-render boundary. The chip composer renders user-typed text as plain text inside a `contenteditable` span — but pasting HTML into a contenteditable element preserves the HTML by default in most browsers.
**How to avoid:** Set `contenteditable="plaintext-only"` on text segments — this is widely-supported (Chrome, Edge, Safari; Firefox added it in 2023). Alternatively, register a `paste` event handler that intercepts `event.clipboardData.getData('text/plain')` and inserts as plaintext. Either approach is ~3 lines of JS per segment.
**Warning signs:** Pasting formatted text from a webpage retains the formatting in the chip composer; or worse, retains an image / iframe.

---

## Code Examples

### `[name](#id)` pattern (CANONICAL — reuse, do not redefine)

```csharp
// Source: src/CookBot.Application/Services/RecipeStepTextFormatter.cs:12-14
// Already compiled, already used by Phase 1 D-13 substring-fallback removal,
// RecipeValidator.cs:15-17, and RecipeValidator orphan detector.
private static readonly Regex IngredientLinkPattern = new(
    @"\[([^\]]*)\]\(#(\d+)\)",
    RegexOptions.Compiled);
```

The chip composer's tokenizer reuses this same pattern. Do **not** redefine it inside the chip composer.

### Tokenizer for chip rendering (Pattern 2)

```csharp
// Inside RecipeChipComposer.razor @code
public sealed record StepToken(int Index, bool IsChip, string Text, int IngredientId = 0);

private IEnumerable<StepToken> TokenizeText(string text)
{
    if (string.IsNullOrEmpty(text))
    {
        yield return new StepToken(0, IsChip: false, Text: "");
        yield break;
    }

    var last = 0;
    var tokenIndex = 0;
    foreach (Match m in RecipeStepTextFormatter.IngredientLinkPattern.Matches(text))
    {
        if (m.Index > last)
            yield return new StepToken(tokenIndex++, IsChip: false, Text: text.Substring(last, m.Index - last));
        var id = int.Parse(m.Groups[2].Value);
        yield return new StepToken(tokenIndex++, IsChip: true,
            Text: m.Groups[1].Value, IngredientId: id);
        last = m.Index + m.Length;
    }
    if (last < text.Length)
        yield return new StepToken(tokenIndex++, IsChip: false, Text: text.Substring(last));
}
```

Note: `RecipeStepTextFormatter.IngredientLinkPattern` is currently `private static readonly`; the planner needs to either (a) make it `internal static readonly` and add `[InternalsVisibleTo]` to `CookBot.Web` (won't work since Application doesn't reference Web), or (b) lift the pattern into a shared `internal static class IngredientLinkPatterns` in `CookBot.Application/Recipes/` (clean) and update both `RecipeStepTextFormatter` and `RecipeValidator` to consume from there. Recommend (b) — it consolidates the pattern.

### Step→Section drop confirmation dialog (D-B3)

```razor
@* src/CookBot.Web/Components/Pages/RecipeEditor/SectionDropConfirmationDialog.razor *@
<MudDialog>
    <TitleContent>
        <MudText Typo="Typo.h6">Convert to a section header?</MudText>
    </TitleContent>
    <DialogContent>
        <MudText>
            This will discard
            @if (TimerCount > 0) { <text>@TimerCount timer(s)</text> }
            @if (TimerCount > 0 && RefCount > 0) { <text> and </text> }
            @if (RefCount > 0) { <text>@RefCount ingredient reference(s)</text> }
            on this step.
        </MudText>
    </DialogContent>
    <DialogActions>
        <MudButton OnClick="Cancel">Cancel</MudButton>
        <MudButton Color="Color.Warning" Variant="Variant.Filled" OnClick="Confirm">Convert</MudButton>
    </DialogActions>
</MudDialog>

@code {
    [CascadingParameter] private IMudDialogInstance MudDialog { get; set; } = null!;
    [Parameter] public int TimerCount { get; set; }
    [Parameter] public int RefCount { get; set; }
    private void Cancel() => MudDialog.Cancel();
    private void Confirm() => MudDialog.Close(DialogResult.Ok(true));
}
```

### JS-interop fail detection probe (D-D4)

```csharp
// Inside RecipeChipComposer.razor @code — call from OnAfterRenderAsync(firstRender: true)
private bool _jsInteropAvailable = false;

protected override async Task OnAfterRenderAsync(bool firstRender)
{
    if (!firstRender) return;
    try
    {
        var pong = await JS.InvokeAsync<string>("RecipeChipComposer.ping");
        _jsInteropAvailable = pong == "ok";
    }
    catch (JSException) { _jsInteropAvailable = false; }
    catch (JSDisconnectedException) { _jsInteropAvailable = false; }
    catch (TaskCanceledException) { _jsInteropAvailable = false; }
    StateHasChanged();
}
```

Then the Razor body switches on `_jsInteropAvailable`:
```razor
@if (_jsInteropAvailable && Interactive)
{
    <div class="chip-flow">@* ... per-token rendering per Pattern 2 ... *@</div>
}
else
{
    <MudTextField @bind-Value="Text" T="string" Lines="3" Variant="Variant.Outlined" Label="Step Instructions" />
}
```

This is the load-bearing fallback contract for D-D4.

---

## Detailed Research Items

### Item 1 — MudBlazor 8.15 component reality check

**Recommendation:** All MudBlazor primitives needed by Phase 3 exist and are stable in 8.15.

**MudAutocomplete<T>:** `SearchFunc` is `Func<string, CancellationToken, Task<IEnumerable<T>>>`; `CoerceText`/`CoerceValue` exist; custom item template via `<ItemTemplate>` render fragment. Programmatic open: as of MudBlazor 8.x, `MudAutocomplete` exposes an `Open` method (`OpenAsync()`) and `Close` — visible in the MudBlazor 8 API docs and in the source at `MudBlazor/Components/Autocomplete/MudAutocomplete.razor.cs`. [CITED: https://mudblazor.com/components/autocomplete] [VERIFIED: source repo `dev` branch reachable via the search-result link to MudBlazor.razor]

**How to apply:** Single shared `MudAutocomplete<Ingredient>` instance per composer. Bound to `_pickerRef` ElementReference. On `@`-trigger or button-click, set its position via absolute-positioned wrapper, then `await _pickerRef.OpenAsync()`. Selection fires `OnValueChanged`, which calls `InsertChipForIngredient`.

**MudChipSet<T> / MudChip<T>:** `MudChipSet<T>` supports removable chips via `ChipsClosable="true"`, `OnClose` callback per chip, custom render fragments via `<ChildContent>`. **However**, `MudChipSet` is designed for "tag list" scenarios — chips inside a single horizontal/wrapped row. It does **not** natively support "chip embedded inline with editable text" (no MudBlazor primitive does — confirmed by web search). [CITED: https://mudblazor.com/components/chipset]

**How to apply:** Use `MudChip<T>` (the individual chip primitive) directly, not `MudChipSet<T>`, for the inline-flow rendering inside `RecipeChipComposer`. `MudChipSet<T>` is fine for the timer-chip strip below the step text (D-C3).

**MudToggleGroup<T> / MudToggleItem<T>:** Supports `@bind-Value` for two-value enum binding; `SelectionMode="SelectionMode.SingleSelection"` for radio-style; segmented appearance is the default. [CITED: https://mudblazor.com/components/togglegroup]

**MudPopover / MudMenu:** `MudMenu` is the higher-level API — built-in click-to-open, ESC-close, focus management, aria roles. Anchor by placing `MudMenu` as a child of a button-like element (or using `ActivationEvent="MouseEvent.Click"` with custom anchor). [CITED: https://mudblazor.com/components/menu and mentioned in search results from `deepwiki.com/MudBlazor/MudBlazor`]

**Risks / open questions:**
- The exact API of `MudAutocomplete.OpenAsync()` in 8.15 specifically (vs. 8.6 docs) — verify via `npx --yes ctx7@latest docs MudBlazor "MudAutocomplete OpenAsync 8.15"` during execution.
- Programmatic open timing — `MudAutocomplete` may swallow the first programmatic open if `IsOpen` is changed too fast after a render. Discussion in [MudBlazor #7569] notes async open timing quirks. Mitigation: `await Task.Delay(1)` between position-set and open call. [CITED: https://github.com/MudBlazor/MudBlazor/discussions/7569]
- Replace-popover positioning when a chip is at the right edge of a wrapping text flow — `MudMenu` may overflow viewport. Use `AnchorOrigin="Origin.BottomLeft"` and `TransformOrigin="Origin.TopLeft"` defaults; mitigations available via `OffsetX`/`OffsetY`.

### Item 2 — Inline-chips-inside-text-area pattern

**Recommendation: per-token segmented input (option c).** Each token is a Razor child component or contenteditable span, alternating chips and text segments inside a single `display: flex; flex-wrap: wrap;` container.

**Trade-off table:**

| Pattern | Blazor diff fit | Keyboard nav | Replace-popover anchoring | JS-fail fallback |
|---|---|---|---|---|
| (a) `contenteditable` div with chip spans (HTML-native) | Bad — every keystroke re-runs the diff over the whole content; chip identity preserved only via `data-key` attribute heuristics | OK if you don't fight the browser; bad if you try to make Tab skip chips | Each chip would need a click handler on a `<span>`, which can't host a `MudMenu` cleanly | Hard — the same `contenteditable` div degrades; no clean swap to `MudTextField` |
| (b) Token list flow + invisible textarea (Notion-style hybrid) | OK — but requires a custom mirror layer that maintains a virtual cursor | Complex; needs custom selection handling | Doable but indirect | Hard — requires recreating the textarea-mirror state |
| **(c) Per-token segmented input (RECOMMENDED)** | **Excellent** — `@key` on each token isolates re-renders; Blazor diff is happy | **Native** — Tab/Shift+Tab move between segment elements (browser default); Backspace at start of a text segment removes the previous chip; chip click opens replace-popover | **Each chip is a real Razor component** with its own `MudMenu`; click handler is C#, anchoring is automatic | **Trivial** — fallback is a single `MudTextField Lines=3` bound to the same `Text` field |
| (d) Other (Tiptap / Quill / etc.) | Out of scope per `.planning/REQUIREMENTS.md` Out of Scope ("Rich-text editors") | — | — | — |

**How to apply in plans:**
- The tokenizer (`TokenizeText`, code in §Code Examples above) runs in C# on each render of `RecipeChipComposer.razor`.
- Each chip token renders an `<IngredientChip>` Razor component; each text token renders a `<span contenteditable="plaintext-only">` (Pitfall 7).
- The container is `<div class="chip-flow" style="display:flex;flex-wrap:wrap;align-items:center;gap:0.25rem;">`.
- Keyboard handling on text segments: `@onkeydown="OnSegmentKeyDown"` with explicit Backspace/Tab handlers; default arrow-key behavior is the browser's caret nav, which is fine.
- The same component, with `Interactive="false"`, renders chips with no contenteditable text (just `<span>` text segments) — used by cooking mode.

**Risks / open questions:**
- Browser support for `contenteditable="plaintext-only"`: Chrome/Edge/Safari yes; Firefox added in 2023; older browsers fall back to full `contenteditable`. Mitigation: paste event handler that strips formatting (~5 lines JS).
- Caret position across segment boundaries — when the user types past the end of a text segment, the caret may "fall off"; need `@onfocus` tracking of which segment is active and explicit caret restoration after re-tokenization.
- IME composition (Chinese/Japanese/Korean input) on per-segment contenteditable spans: needs `@oncompositionstart` / `@oncompositionend` handlers to suppress re-tokenization mid-composition. **This is a real risk for users in those locales**; recommend the manual smoke checklist (Item 8) include IME-input verification.

### Item 3 — `@`-trigger autocomplete pattern

**Recommendation:** Caret-anchored shared `MudAutocomplete` opened via small JS-interop helper.

**Contract:**
1. On `@onkeydown` in any text segment, if `e.Key == "@"`:
2. Call `JS.InvokeAsync<CaretCoords>("RecipeChipComposer.getCaretCoords", elementId)` → returns `{x, y}`.
3. Set wrapper div absolute position: `style="position:absolute;left:{x}px;top:{y}px"`.
4. Set `_pickerVisible = true`, `_partialAtToken = ""`, `await _autocompleteRef.OpenAsync()`.
5. As user types, `_partialAtToken` accumulates the chars after `@`; the `MudAutocomplete.SearchFunc` filters by `_partialAtToken`.
6. On selection (`OnValueChanged`): call `InsertChipForIngredient(selected, caretIndex - _partialAtToken.Length - 1)` — the `-1` accounts for the `@` itself, which is also replaced.
7. Hide picker (`_pickerVisible = false`).

**How to apply in plans:** This logic lives entirely in `RecipeChipComposer.razor` `@code` block, ~80 lines including the keyboard handler, the SearchFunc (calls into `DbContext.Ingredients` exactly like `RecipeEditor.razor:327-339`), and the insertion helper.

**Risks / open questions:**
- Reference implementation: no clean MudBlazor + Blazor Server example exists for caret-anchored autocomplete on `@`-trigger. Web search returned: third-party libs (BlazorAutocomplete, Radzen, Syncfusion) but none with the inline-chip pattern. [CITED: https://github.com/erossini/BlazorAutocomplete] [CITED: https://blazor.radzen.com/autocomplete] [CITED: https://docs.blazorbootstrap.com/forms/autocomplete] All are general-purpose autocompletes, not the @-mention pattern. **The implementation is novel for this codebase** — hence the bUnit recommendation.
- Confidence on programmatic open timing: MEDIUM — the GitHub discussion thread on programmatic open shows that `MudAutocomplete.OpenAsync()` exists but timing can be quirky; an explicit `await Task.Delay(1)` between position update and open call is a known workaround. [CITED: https://github.com/MudBlazor/MudBlazor/discussions/7569]

### Item 4 — bUnit availability

**Recommendation: ADD bUnit 1.40.0** (or fallback 1.36 LTS) to `tests/CookBot.Tests/CookBot.Tests.csproj`.

**Verification:** Read of `tests/CookBot.Tests/CookBot.Tests.csproj` (lines 10-16): `<PackageReference>` items are coverlet, EFCore.Sqlite, NET.Test.Sdk 17.12.0, xunit 2.9.2, xunit.runner.visualstudio 2.8.2 — **bUnit is not present**. [VERIFIED]

**Compatibility:** bUnit 1.40 supports xUnit 2.9.x and net9.0/net8.0. .NET 10 is brand-new (current month is April 2026); bUnit's published target frameworks should be checked against `dotnet add package bunit` output during plan-phase. If 1.40 doesn't have a net10 build, fallback to 1.36 LTS — it targets netstandard2.1 and runs cleanly on net10. [CITED: https://bunit.dev/docs/getting-started/]

**How to apply in plans:** Single-line csproj edit (one PackageReference). No DI changes, no global-using changes (`<Using Include="Xunit" />` already gives globals). The bUnit pattern matches xUnit naturally — `using Bunit;` at the top of test files, `using var ctx = new TestContext();` per test, `RenderComponent<TComponent>(p => p.Add(c => c.Param, value));` to render. The CookBot.Tests project already references `CookBot.Web` via the projectReference at line 26, so Razor components are visible to the test project.

**Risks / open questions:**
- LOW: bUnit 1.40 may not have shipped with explicit net10 support. Mitigation in Pitfall 6 above.

### Item 5 — Inline timer-suggestion rendering (D-C1)

**Recommendation: Extend `RecipeStepTextFormatter` to wrap detected-timer substrings in `<span class="timer-suggestion">…</span>`.**

**Why over JS DOM mutation:**
- Formatter already owns the rendered HTML pipeline (it's the only path after Phase 1 D-13).
- Cooking mode already uses `RecipeStepTextFormatter.ToHtml` (line 53, 58) — extending the formatter means the inline highlight appears in cooking mode too, no extra wiring. (D-D3 does not surface timer-suggestion popovers in cooking mode — only the highlight visual.)
- JS DOM mutation fights Blazor's diff: every re-render rewrites the textarea HTML, JS runs again, can leak listeners.
- Perf: a step with 4-5 detected durations across 20 steps = 80-100 regex matches × 500ms debounce ≤ 200/sec, well within budget. The regex is already compiled.

**Click-to-popover wiring:** In `RecipeChipComposer.razor`, after rendering the formatted HTML via `MarkupString`, attach a click handler at the `chip-flow` container level using event delegation: `@onclick="OnChipFlowClick"` checks `e.Target` for `class="timer-suggestion"`, reads `data-duration-seconds`, opens a Yes/No popover (`MudMenu`) anchored to the click coordinates.

**Code (formatter extension):**

```csharp
// Source: src/CookBot.Application/Services/RecipeStepTextFormatter.cs (extended)
private static readonly Regex TimerSubstringPattern = TimerDetectionService.CompiledTimerPattern;
// ^^ Phase 3 lifts the regex to a public static readonly on TimerDetectionService for reuse.

public static string ToHtmlWithTimerSuggestions(string? text, IReadOnlySet<int> alreadyConvertedDurationsSeconds)
{
    var html = ToHtml(text);  // existing path — emits chips + line breaks
    // Second pass: wrap timer substrings that are NOT already inside an <span>
    return TimerSubstringPattern.Replace(html, m =>
    {
        var seconds = ParseDurationToSeconds(m.Groups[1].Value, m.Groups[2].Value);
        if (alreadyConvertedDurationsSeconds.Contains(seconds))
            return m.Value;  // skip — already an explicit timer chip
        return $"<span class=\"timer-suggestion\" data-duration-seconds=\"{seconds}\">{m.Value}</span>";
    });
}
```

**Risks / open questions:**
- Idempotency: if a timer substring appears inside an `<span class="ingredient-ref">` (e.g. an ingredient named "5 minute rice"), the second pass would wrap. Mitigation: the second pass runs on `text` BEFORE the first pass, OR the second pass scans for `<span` openings and skips any match inside one. Cleanest is a single-pass formatter that knows about both patterns. **Discretion** — this is a refinement detail.
- D-C3 says "Re-detection on edit does NOT offer to re-convert a duration that's already been accepted into an explicit timer chip on the same step." The `alreadyConvertedDurationsSeconds` set passed by the editor is what makes this work.

### Item 6 — Timer regex broadening (CONCERNS §7)

**Recommendation:** Replace today's regex with an alternation of three sub-patterns. Persist the **lowest** value of any range.

**Today (`TimerDetectionService.cs:8-10`):**
```csharp
@"(\d+)\s*(minutes?|mins?|hours?|hrs?|seconds?|secs?)"
```

**After Phase 3:**
```csharp
// FRACTIONAL: "1 1/2 hours", "1/2 hour", "0.5 hours"
private static readonly Regex FractionalPattern = new(
    @"(?:(\d+)\s+)?(\d+)\s*/\s*(\d+)\s*(minutes?|mins?|hours?|hrs?|seconds?|secs?)",
    RegexOptions.IgnoreCase | RegexOptions.Compiled);

// RANGE: "20-25 minutes", "20 to 25 minutes", "20–25 minutes" (en dash)
private static readonly Regex RangePattern = new(
    @"(\d+(?:\.\d+)?)\s*(?:-|–|—|to)\s*(\d+(?:\.\d+)?)\s*(minutes?|mins?|hours?|hrs?|seconds?|secs?)",
    RegexOptions.IgnoreCase | RegexOptions.Compiled);

// MULTI-SEGMENT: "1 hour 30 minutes", "2h 15m"
private static readonly Regex MultiSegmentPattern = new(
    @"(\d+(?:\.\d+)?)\s*(h|hr|hrs|hour|hours)\s*(\d+(?:\.\d+)?)\s*(m|min|mins|minute|minutes)",
    RegexOptions.IgnoreCase | RegexOptions.Compiled);

// SIMPLE (existing, kept as fallback for plain "25 minutes")
private static readonly Regex SimplePattern = new(
    @"(\d+(?:\.\d+)?)\s*(minutes?|mins?|hours?|hrs?|seconds?|secs?)",
    RegexOptions.IgnoreCase | RegexOptions.Compiled);
```

**Detection order (longest-match first to avoid SimplePattern eating the multi-segment):**
1. `MultiSegmentPattern` first (consumes "1 hour 30 minutes" as one detection)
2. `RangePattern` next (consumes "20-25 minutes" as one detection)
3. `FractionalPattern` (consumes "1 1/2 hours" as one detection)
4. `SimplePattern` last, on remaining unconsumed text spans

**Range value persistence:** RECOMMEND **lowest value** for "20-25 minutes" → 20 minutes. Rationale: cooking timers should fire at the earliest "check it" point, not the latest. Surface the original substring as the chip Label ("20-25 min") so the user sees the range. CONTEXT.md doesn't decide; this is the planner's call to lock during plan-phase.

**Parsing logic for "1 1/2 hours" → seconds:**
```csharp
private static int ParseFractionalToSeconds(int? whole, int numerator, int denominator, string unit)
{
    var totalUnits = (whole ?? 0) + (double)numerator / denominator;
    return unit.ToLowerInvariant() switch
    {
        var u when u.StartsWith("hr") || u.StartsWith("hour") => (int)(totalUnits * 3600),
        var u when u.StartsWith("sec") => (int)totalUnits,
        _ => (int)(totalUnits * 60)
    };
}
```

**Test cases per pattern:**

```csharp
// FRACTIONAL
[InlineData("Bake for 1 1/2 hours", 5400)]  // 1.5 * 3600
[InlineData("Rest 1/2 hour", 1800)]
[InlineData("Simmer 0.5 hours", 1800)]

// RANGE
[InlineData("Cook 20-25 minutes", 1200)]  // lowest = 20 min
[InlineData("Bake 20 to 25 minutes", 1200)]
[InlineData("Roast 30–35 minutes", 1800)]  // en dash

// MULTI-SEGMENT
[InlineData("Slow cook 1 hour 30 minutes", 5400)]
[InlineData("Marinate 2h 15m", 8100)]
```

**How to apply in plans:** New file is ~70 lines. The existing `DetectTimers` method becomes a sequenced caller of the four patterns; existing callers in `RecipeStepEditor` and the inline timer-suggestion overlay use the same method (they get fractional/range/multi-segment detection for free).

**Risks / open questions:**
- "ten minutes" word-form numbers — explicitly deferred per CONTEXT.md `<deferred>`. Add a TODO comment in the new TimerDetectionService body referencing FUTURE-XX or plan backlog.
- Overlapping matches: "1 hour 1 1/2 minutes" — multi-segment expects integer minutes; this falls through to no match. Acceptable; the user types the durations explicitly via timer chips if the regex misses.
- Confidence: HIGH on the patterns themselves (regex is well-formed); MEDIUM on the order-of-application logic — recommend a comprehensive bUnit/xUnit test suite during execution.

### Item 7 — JS-interop-fail fallback contract (D-D4)

**Recommendation:** Try/catch around a `ping()` probe in `OnAfterRenderAsync(firstRender: true)`, store result in `_jsInteropAvailable` field, render chip composer or `MudTextField` based on flag.

**Why this approach (vs. wrap-every-call-and-degrade-on-first-failure):**
- Deterministic: probe-once, render-once. The user sees consistent behavior across the entire editing session.
- No half-states: chips render OR fallback renders; never "chips that are click-broken."
- Easy to test: bUnit can mock `IJSRuntime.InvokeAsync` to throw, assert fallback renders.

**Code:** See §Code Examples "JS-interop fail detection probe" above. The probe catches `JSException` (module not loaded), `JSDisconnectedException` (circuit lost), `TaskCanceledException` (interop timeout — Blazor Server defaults to 60s but circuit lost looks like cancellation in some builds).

**Microsoft docs:** Blazor Server's JS-interop exception model is documented at `learn.microsoft.com/aspnet/core/blazor/javascript-interoperability/call-javascript-from-dotnet#javascript-isolation-in-javascript-modules`. `JSDisconnectedException` is the canonical "circuit lost" exception; the existing codebase already catches it for `CookingTimers.dispose` (`CookingMode.razor:235-237`) — the same pattern transfers to Phase 3. [VERIFIED: file path + line ranges]

**How to apply in plans:** The probe lives in `RecipeChipComposer.OnAfterRenderAsync(firstRender)`. The `_jsInteropAvailable` field gates the entire `chip-flow` rendering. Save still works either way — text-backed canonical record (Phase 1 D-12) means the raw `[name](#id)` text round-trips through the parser unchanged.

**Risks / open questions:**
- Page transition between editor and cooking mode: JS module re-imports cleanly; `_jsInteropAvailable` is per-component, so each composer probes independently.
- LOW: edge case where the JS module loads on initial render but disconnects mid-edit. The probe never re-runs; the user keeps typing chips that don't insert because `getCaretCoords` fails. Mitigation: catch in `getCaretCoords` call too, set `_jsInteropAvailable = false`, force re-render to fallback. Recommended belt-and-suspenders.

### Item 8 — Accessibility test mechanism

**Recommendation:** Manual smoke checklist documented in the phase verification artifacts. **No** axe-core, Playwright, Cypress, or Lighthouse infrastructure exists in the project today (verified via grep — zero matches in `src/` or `tests/`).

**8-item smoke checklist (planner: include in `03-VERIFICATION.md`):**

1. **Tab/Shift+Tab navigation across step rows** — Tab from metadata → ingredients → first step's Step/Section toggle → first text segment → first chip × button → next text segment → ... → last step → Save. Shift+Tab reverses cleanly. No focus traps.
2. **Backspace from position immediately after a chip removes the chip** (D-A2). Arrow-Left from the position immediately after a chip places caret at end of chip body without entering the chip text.
3. **Screen reader announces chips correctly** — VoiceOver / NVDA reads each ingredient chip as `"<ingredient name>, button, ingredient chip"` or similar. The replace-popover opens with focus moved into it; Escape closes and returns focus to the chip.
4. **`@`-trigger autocomplete is keyboard-only operable** — type `@par` opens picker, Arrow-Down/Up navigates results, Enter selects, Escape cancels and removes the `@par` literal.
5. **Step/Section toggle is announced as `"radiogroup"` with two options** (`MudToggleGroup` defaults). Arrow keys cycle between Step/Section.
6. **Inline timer suggestion is announced as a button** ("Detected 25 minutes, button — click to convert"). Click/Enter opens the popover; Yes/No are buttons with full keyboard activation.
7. **JS-interop-fail fallback path is keyboard-complete** — disable JS in browser dev tools, reload the editor, verify each step is a `MudTextField` with raw `[name](#id)` text and Save still works.
8. **Color contrast on chip variants** — ingredient chips (`Color.Info`), timer chips (`Color.Warning`), error chips (red border) all meet WCAG AA contrast against the editor background in both light and dark mode.

**Risks / open questions:**
- IME composition: not in this 8-item checklist — recommend adding "9. Type a step in Japanese (test on macOS Japanese IME or Windows Japanese IME); chips render correctly without breaking mid-composition." Confidence MEDIUM that the per-token contenteditable handles IME correctly without extra handlers. Mitigation in Pitfall 7 / Item 2.
- This checklist is a verification gate, not a CI gate — matches CONTEXT.md's locked decision to defer Playwright/Cypress.

### Item 9 — Cooking-mode chip click → scroll-and-highlight (D-D3)

**Recommendation:** Use `Element.scrollIntoView({behavior: 'smooth', block: 'center'})` plus a transient CSS class added via JS, removed via `setTimeout`.

**Working snippet (already in §Code Examples Pattern 3 — `recipe-chip-composer.js`):**
```javascript
scrollIntoViewWithHighlight(elementId, highlightClass = 'chip-highlight-pulse', durationMs = 1500) {
    const el = document.getElementById(elementId);
    if (!el) return false;
    el.scrollIntoView({ behavior: 'smooth', block: 'center' });
    el.classList.add(highlightClass);
    setTimeout(() => el.classList.remove(highlightClass), durationMs);
    return true;
}
```

**CSS (add to `app.css`):**
```css
.chip-highlight-pulse {
    background: var(--mud-palette-info-lighten);
    box-shadow: 0 0 0 2px var(--mud-palette-info);
    border-radius: 8px;
    transition: background 300ms ease-out, box-shadow 300ms ease-out;
}
```

**How to apply:**
- In `CookingMode.razor`, ingredient sidebar items get `id="ingredient-{ri.RecipeLocalId}"` (line ~141, modified).
- `RecipeChipComposer` (when `Interactive=false` and `OnIngredientChipClick` is set) calls `await JS.InvokeAsync<bool>("RecipeChipComposer.scrollIntoViewWithHighlight", $"ingredient-{ingredientId}")` on chip click.
- Wrap the call in `try { ... } catch (JSDisconnectedException) { /* graceful */ }` per Pitfall 4.

**Risks / open questions:**
- `behavior: 'smooth'` honors the user's `prefers-reduced-motion` media query in modern browsers (Chrome 110+, Safari 14+). LOW risk of jarring scroll for accessibility-conscious users.
- Multiple rapid clicks queue setTimeouts; the highlight may flicker. Mitigation: clear any prior timeout before setting a new one (`if (el._highlightTimer) clearTimeout(el._highlightTimer); el._highlightTimer = setTimeout(...)`).

### Item 10 — EDITOR-01 docs-amendment task (D-A5)

**Recommendation:** P0 task in plan-phase: amend `.planning/REQUIREMENTS.md` line 50.

**Exact diff:**
```diff
-- [ ] **EDITOR-01**: `RecipeEditor.razor`'s step textarea is replaced with a chip-aware composer built on `MudAutocomplete<Ingredient>` + `MudChipSet<T>`. Typing `@` or clicking an "Insert ingredient" affordance opens autocomplete over recipe ingredients; selecting one inserts a chip; the underlying string keeps `[name](#id)` markdown invisibly.
++ [ ] **EDITOR-01**: `RecipeEditor.razor`'s step textarea is replaced with a chip-aware composer built on `MudAutocomplete<Ingredient>` + `MudChipSet<T>`. Typing `@` or clicking an "Insert ingredient" affordance opens autocomplete over recipe ingredients; selecting one inserts a chip showing the ingredient name. The underlying string keeps `[name](#id)` markdown invisibly; the immutable `id` is what serializes.
```

**Anchor confirmed:** `.planning/REQUIREMENTS.md` line 50. Section: `### EDITOR — Recipe authoring without special syntax`. [VERIFIED: read of file lines 46-56]

**Side-effect check:** ROADMAP.md Phase 3 Success Criterion #1 currently reads `chips render with the user-facing index while the underlying string keeps the immutable id`. This phrasing **also conflicts with D-A3** (chip is name only). Either (a) update ROADMAP.md success criterion #1 in the same plan-phase task, or (b) accept the slight wording mismatch as documentation-vs-spec drift and rely on the EDITOR-01 amendment as authoritative. **Recommend (a)** — update both files together. New ROADMAP language: "chips render with the ingredient name while the underlying string keeps the immutable `id`."

### Item 11 — Auto-write deletion (per EDITOR-03)

**Recommendation:** Delete the timer auto-detection fallback at two sites in `RecipeService.cs`.

**Exact line ranges:**

`src/CookBot.Application/Services/RecipeService.cs` lines **65-79** (CreateAsync):
```csharp
// BEFORE (current):
var step = new RecipeStep
{
    Order = order++,
    Text = ps.Text,
    IsSection = ps.IsSection,
    Timers = ps.IsSection ? new() :
        (ps.Timers?.Any() == true
            ? ps.Timers.Select(t => new StepTimer { Duration = t.Duration, Unit = t.Unit, Label = t.Label }).ToList()
            : TimerDetectionService.DetectTimers(ps.Text)),
    // ...
};

// AFTER (Phase 3):
var step = new RecipeStep
{
    Order = order++,
    Text = ps.Text,
    IsSection = ps.IsSection,
    Timers = ps.IsSection
        ? new()
        : (ps.Timers ?? new()).Select(t => new StepTimer { Duration = t.Duration, Unit = t.Unit, Label = t.Label }).ToList(),
};
```

Same change at lines **131-147** (UpdateAsync) — same pattern, same delete.

**Tests to update:** Search `tests/CookBot.Tests/` for any test that exercises the auto-detection-on-save path. Likely candidates (verify during plan-phase): `tests/CookBot.Tests/Services/OwnershipTests.cs`, `tests/CookBot.Tests/Services/RecipeFormatParserTests.cs`. The behavior under test inverts: "saving a step with text='Cook 25 minutes' and Timers=null results in zero persisted timers." Add a new test asserting this. The standalone `TimerDetectionServiceTests.cs` (which tests the regex itself) is unaffected.

**Risks / open questions:**
- Existing recipes in `cookbot.db` that were saved with auto-detected timers persist as-is — the column data isn't touched. Cooking mode renders those existing timers. New saves don't auto-add. Acceptable behavior; no migration needed.
- LOW: the deletion is a single-conditional change in two places; rollback is trivial if needed.

### Item 12 — Component file layout & namespace

**Recommendation:** New folder `src/CookBot.Web/Components/Pages/RecipeEditor/`. Namespace `CookBot.Web.Components.Pages.RecipeEditor` (folder-mirrored, per CONVENTIONS.md §Project Layout).

**Conflict check:** No existing folder at that path; no existing class/component named `RecipeEditor` in any other namespace; the existing `RecipeEditor.razor` file at `src/CookBot.Web/Components/Pages/RecipeEditor.razor` does not have a folder of the same name. The two coexist:
```
src/CookBot.Web/Components/Pages/
├── RecipeEditor.razor         # existing entry page (lives at /cookbooks/X/recipes/new and /recipes/X/edit)
└── RecipeEditor/              # NEW — sub-components
    ├── RecipeStepEditor.razor
    ├── ...
```

**Sibling page namespace pattern:** Pages in `src/CookBot.Web/Components/Pages/` have implicit namespace `CookBot.Web.Components.Pages` (verified via the existing `Components/_Imports.razor`). Adding a sub-folder gives `CookBot.Web.Components.Pages.RecipeEditor` — Razor components in that namespace are reachable from the parent `RecipeEditor.razor` without an explicit `@using` (since they're in a child namespace; alternatively add `@using CookBot.Web.Components.Pages.RecipeEditor` to `_Imports.razor`).

**Risks / open questions:**
- Some Blazor route-discovery quirks treat folder + same-named file ambiguously. The route attribute `@page "/cookbooks/{CookbookId:int}/recipes/new"` is on the file `RecipeEditor.razor`, not on any sub-component, so there's no route conflict. LOW risk.

### Item 13 — Validation Architecture (Nyquist) is DISABLED

CONTEXT.md confirms `nyquist_validation: false` in `.planning/config.json`. [VERIFIED: `.planning/config.json` line 13: `"nyquist_validation": false`]

**Action:** Skip the `## Validation Architecture` section per template instructions. The bUnit + manual smoke checklist (Item 8) is the sufficient validation strategy for Phase 3.

---

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|---|---|---|---|
| Substring-match ingredient detection (`textLower.Contains(name)`) | `[name](#id)` markdown link parsing only (`RecipeStepTextFormatter.IngredientLinkPattern`) | Phase 1 D-13 (Apr 2026) | Phase 3 chip composer reuses this regex; no parallel detection logic |
| `RecipeStep.IngredientRefs: List<int>` derived-on-save column | Column persists for one milestone for safe rollback; writes retired | Phase 1 D-13 (Apr 2026); column drop in Phase 4 (POLISH-03) | Phase 3 chip composer does NOT write to this column. `CookingMode.razor:140` still reads it (deferred cleanup). |
| Three-tier `AiChat.ExtractRecipeContent` ladder | Structured-output via `IAiRecipeGenerator`; legacy ladder DELETED | Phase 2 (Apr 2026) | Phase 3 receives clean `RecipeDocument` from AI flow; no parsing fallback |
| Markdig `Markdown.ToHtml(content)` allowing raw HTML | `MarkdownPipelineBuilder().DisableHtml().Build()` for assistant-rendered content | Phase 2 AI-08-AUDIT (Apr 2026) | Phase 3 doesn't render assistant markdown — but the editor's contenteditable text segments need their own paste-sanitization (Pitfall 7) |
| Two YAML key spellings (`prepTime` vs `prepTimeMinutes`) | Single `prepTimeMinutes` everywhere via Migration_V1_To_V2 upcaster | Phase 1 D-03 / D-09 | Phase 3 metadata fields read/write the canonical form |

**Deprecated/outdated:**
- `IngredientRefDetectionService.DetectRefs` substring fallback — already deleted in Phase 1; the file still exists with link-only detection. Phase 3 doesn't touch.
- `AiChat.ExtractRecipeContent` — already deleted in Phase 2; do not re-introduce.

---

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|---|---|---|
| A1 | bUnit 1.40 supports .NET 10. | Item 4, Pitfall 6 | Plans need to fall back to bUnit 1.36 LTS or defer bUnit altogether — adds ~30 min plan-phase decision work. |
| A2 | `MudAutocomplete.OpenAsync()` exists and is callable in MudBlazor 8.15. | Item 1, Item 3 | If the method name differs (`Open()` vs `OpenAsync()`) or doesn't exist as a public API, the planner needs to use a workaround like `IsOpen`-state binding or a dirty-trick "call SearchFunc with empty string to force-open." All workarounds work; just slower to figure out. |
| A3 | `contenteditable="plaintext-only"` is sufficient for paste-sanitization on Chrome/Edge/Safari/Firefox 2023+. | Pattern 2 / Pitfall 7 | Older Firefox falls back to full contenteditable; users on those browsers could inject HTML. Mitigation is a `paste` event handler, ~5 lines JS — acceptable cost. |
| A4 | Range timers persist as the **lowest** value of the range. | Item 6 | If the user expects upper-bound (e.g. "set timer for 25 min" on a 20-25 range), the convention may surprise. CONTEXT.md doesn't decide; the planner should lock during plan-phase. |
| A5 | Existing tests in `tests/CookBot.Tests/Services/` don't cover the timer auto-detection-on-save path, so deleting it doesn't break tests. | Item 11 | Need to grep during plan-phase. If tests do exist, they need updating — adds a small task but doesn't change the deletion approach. |
| A6 | bUnit's renderer can render Razor components that depend on `IDialogService`, `IJSRuntime`, `ISnackbar`, `CookBotDbContext`, etc. without full DI plumbing. | Item 4 | bUnit supports `Services.AddScoped<IFoo>(sp => fakeFoo)` per `TestContext`; planner needs to register fakes for each dependency. ~10 lines of fake setup per test class. |

**Anything tagged `[ASSUMED]` in body text:**
- Item 4: latest bUnit version is 1.40 — minor; verify with `dotnet add package bunit --version *` during execution.
- Item 1: MudBlazor 8.x `OpenAsync` API surface — high confidence based on web search but not verified against 8.15 specifically.

---

## Open Questions (RESOLVED)

1. **Range-timer persistence (lowest, midpoint, or upper)?**
   - **RESOLVED:** Lowest-bound persistence locked. Implemented in Plan 03 Task 1 (`TimerDetectionService.ParseRangeToSeconds`), tested in `TimerDetectionServiceRegexTests.Range_PersistsLowestBound`.
   - What we know: CONTEXT.md says broaden the regex but doesn't decide which value persists.
   - What's unclear: Cooking convention. Lowest = check at first-doneness; upper = total time budget.
   - Recommendation: Lowest (early "check it" is more useful than late "remove now"). Plan-phase locks this.

2. **Drag handles on ingredient reorder (in addition to existing arrow buttons)?**
   - **RESOLVED:** Deferred to Phase 4 polish / backlog. Phase 3 keeps the existing arrow-button reorder UX in `RecipeStepEditor.razor`; no DnD added. EDITOR-04 is satisfied by `id`-based chip rendering (Phase 1 D-06 immutable id flowing through Wave 1 chip composer), independent of reorder mechanism.
   - What we know: CONTEXT.md Claude's Discretion; Phase 1 D-06 locks `id` immutability.
   - What's unclear: Whether the planner adds DnD or defers.
   - Recommendation: Defer DnD to Phase 4 polish or backlog; arrow buttons are sufficient for EDITOR-04.

3. **Should `RecipeStepTextFormatter.IngredientLinkPattern` be lifted to a shared `internal` location?**
   - **RESOLVED:** Lifted to `CookBot.Application.Recipes.IngredientLinkPatterns` in Plan 01 Task 1; consumed by `RecipeStepTextFormatter`, `RecipeValidator`, `RecipeChipComposer`, and `RecipeStepEditor` (D-A4 / D-B2 reference-strip path). Marked `internal` with `[InternalsVisibleTo("CookBot.Web")]`.
   - What we know: It's `private static readonly` today; reused by `RecipeValidator.cs:15-17` (separately defined identical regex).
   - What's unclear: Whether pulling into a shared `IngredientLinkPatterns` static class is in-scope for Phase 3 or out-of-scope refactor.
   - Recommendation: In-scope for Phase 3 — small refactor, deduplicates the regex, makes the chip composer's tokenizer use the canonical pattern. ~20 LOC change in 2 files.

4. **Where do new CSS rules live — `app.css` or scoped CSS files (`.razor.css`)?**
   - **RESOLVED:** Stays in `wwwroot/app.css` per CONVENTIONS.md. Plan 01 Task 2 appends a `/* === Recipe Chip Composer === */` banner section with the chip / chip-suggestion / chip-highlight-pulse rules. No `.razor.css` files introduced.
   - What we know: CONVENTIONS.md says `app.css` is the only stylesheet; `.razor.css` is not used today.
   - What's unclear: Whether Phase 3 introduces scoped CSS (10+ rules across new components).
   - Recommendation: Stay with `app.css` to honor existing convention. Group Phase 3 rules under a `/* === Recipe Chip Composer === */` banner.

---

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|---|---|---|---|---|
| .NET 10 SDK | All Phase 3 work | ✓ (assumed — Phase 1/2 shipped on net10) | .NET 10 (per `*.csproj` `<TargetFramework>net10.0</TargetFramework>`) | — |
| `dotnet ef` tools | Not used (no schema changes) | N/A | N/A | — |
| MudBlazor 8.15 | Composer primitives | ✓ | 8.15.0 | — |
| Markdig 0.45 | Untouched | ✓ | 0.45.0 | — |
| YamlDotNet 16.3 | Untouched | ✓ | 16.3.0 | — |
| **bUnit** | Component-level tests | **✗** | — | Manual smoke (Item 8); slower regression coverage. **Recommend installing.** |
| axe-core / Playwright / Lighthouse | Accessibility tests | ✗ | — | Manual smoke checklist (Item 8); CONTEXT.md accepts this. |
| Browser dev environment for manual smoke (Chrome/Edge/Safari/Firefox) | Verification | Assumed available on developer workstation | — | — |

**Missing dependencies with no fallback:** None.

**Missing dependencies with fallback:**
- bUnit — fallback is manual smoke. **Recommend adding** because chip composer's interaction model (insertion via `@`-path vs button-path; Backspace vs `×` removal; replace-popover swap) is exactly the surface bUnit excels at testing.

---

## Plan-shaping suggestions

A 4-wave plan structure maps cleanly onto the dependency graph. Each wave is sized to one plan with 3-5 tasks.

| Wave | Scope | Why this slice |
|---|---|---|
| **Wave 1: Shared chip composer foundation + tests** | `RecipeChipComposer.razor` (skeleton + tokenizer + Pattern 2 inline rendering + JS-interop probe + plain-text fallback), `IngredientChip.razor` (chip body + replace-popover MudMenu), `recipe-chip-composer.js` (caret coords + ping), bUnit dependency added, `RecipeChipComposerTests.cs`. Lift `IngredientLinkPattern` to shared `internal static` location. | Foundation for both interactive editor and read-only cooking mode; testable in isolation; enables Wave 2 to compose without re-implementing tokenization. EDITOR-01 partial coverage. |
| **Wave 2: Editor integration + Step/Section toggle + timer chip strip** | `RecipeStepEditor.razor` (step row owning toggle + composer + timer chip strip), `TimerChip.razor` (edit popover), `SectionDropConfirmationDialog.razor`, `RecipeEditor.razor` rewrite (lines 108-181 collapse to component invocations; metadata + ingredients sections unchanged), `StepSectionToggleTests.cs`. | Composes Wave 1 into the actual editor surface; covers EDITOR-01 (full), EDITOR-02, EDITOR-04. Single page rewrite is the riskiest slice; isolating it lets it land cleanly. |
| **Wave 3: Cooking-mode chip rendering + paste flow + timer regex broadening** | `CookingMode.razor` lines 50-59 + 138-156 modifications (chip composer in read-only mode; ingredient sidebar gets element ids; click-to-scroll wired); `PasteRawTextDialog.razor` numbered-list fallback DELETED; `TimerDetectionService.cs` regex broadened with the four sub-patterns; `PasteFlowTests.cs`, `TimerSuggestionTests.cs`. | Wave 1 + Wave 2 are the load-bearing interactive surface; Wave 3 propagates the chip rendering to read-only contexts and tightens up the parsing/regex side. EDITOR-03 (regex), EDITOR-05, EDITOR-06. |
| **Wave 4: Auto-write removal + EDITOR-01 docs amendment + ROADMAP success-criterion update + JS-interop fallback hardening + manual smoke checklist** | `RecipeService.CreateAsync/UpdateAsync` lines 65-79 + 131-147 timer auto-detection deletion; `.planning/REQUIREMENTS.md` EDITOR-01 line 50 amendment; `.planning/ROADMAP.md` Phase 3 SC#1 wording fix; `RecipeChipComposer` and `CookingMode` JS-interop calls wrapped in try/catch for `JSDisconnectedException`; manual smoke checklist authored in `03-VERIFICATION.md`. | EDITOR-03 (auto-write), EDITOR-07 (graceful degradation + accessibility). The docs amendment is P0 per D-A5 — landing it in the same wave as the auto-write deletion ensures REQUIREMENTS.md stays in sync with shipped code at phase-end. |

**Inter-wave dependencies:**
- Wave 1 → Wave 2: Wave 2 imports the chip composer; Wave 1 must land first.
- Wave 1 → Wave 3: Cooking-mode rendering imports the same chip composer.
- Wave 2 ⊥ Wave 3 (parallel-safe): editor and cooking-mode rewrites touch different files. Could ship in either order or in parallel if multiple sessions.
- Wave 4 depends on Wave 2 (auto-write removal would surface as test failures if Wave 2's editor still needs the auto-write fallback to display detected timers — but Wave 2's editor already shows timer suggestions inline via the formatter extension, so independent timing is fine).

**One-task-per-wave anchor:** every wave should declare exactly one xUnit/bUnit test class as its primary verification artifact. Manual smoke is the cross-cutting verification gate (Wave 4 owns the checklist).

---

## Project Constraints (from CLAUDE.md)

> Mandatory directives the planner must verify each plan complies with.

- **No second AI provider abstraction; no `Microsoft.Extensions.AI` / official `Anthropic` NuGet** — Phase 3 doesn't touch AI; trivially compliant. (CLAUDE.md `Things to avoid`)
- **No `Newtonsoft.Json` / `NJsonSchema`** — Phase 3 adds zero JSON serialization; compliant. (CLAUDE.md `Things to avoid`)
- **No `CookBot.Schemas` project** — `RecipeDocument` already lives in `CookBot.Domain.Recipes` per Phase 1 D-01; not relevant to Phase 3 since no new schema.
- **Don't auto-scale temperatures/prep/cook times — only `RecipeIngredient.Amount` scales** — Phase 3 doesn't add scaling; trivially compliant.
- **Don't reintroduce a "free-form / numbered-list fallback" escape hatch** — directly relevant: D-D1 deletes the numbered-list fallback in `PasteRawTextDialog.razor:51-64`. Plans must NOT add any new "if parser fails, salvage what we can with a regex" path. The Phase 1 parser handles best-effort coercion-with-warnings; the dialog stays minimal. (CLAUDE.md `Things to avoid` — last bullet)
- **MudBlazor 8.15 only** — Phase 3 ships on 8.15; v9 upgrade is FUTURE-10. (CLAUDE.md preamble)
- **`@rendermode InteractiveServer` on every interactive page** — `RecipeEditor.razor` already declares this (line 10); new sub-components inherit through the parent's render mode. (CONVENTIONS.md / CLAUDE.md preamble)
- **Authorization in application/data services, not middleware** — `RecipeService.CreateAsync/UpdateAsync` enforce ownership; Phase 3 doesn't add new save paths. (CLAUDE.md / CONVENTIONS.md)
- **No `Console.WriteLine` / `Debug.*`; surface failures via `ISnackbar`** — Phase 3 components use snackbars exclusively for user-facing errors. (CONVENTIONS.md §Logging)

---

## Sources

### Primary (HIGH confidence)
- **`.planning/phases/03-editor-ux-without-special-syntax/03-CONTEXT.md`** — every D-Xn locked decision and the EDITOR-01 amendment requirement.
- **`.planning/phases/01-canonical-format-foundation/01-CONTEXT.md`** + **`01-VERIFICATION.md`** — Phase 1 schema contracts (D-02, D-06, D-08, D-13) that Phase 3 consumes; Phase 1 ship status (122/122 tests passed).
- **`.planning/phases/02-ai-structured-output-conformance/02-VERIFICATION.md`** — confirms AiChat.razor structured-output handoff to RecipeEditor lands cleanly (lines 175-182 + 531-562).
- **`src/CookBot.Web/Components/Pages/RecipeEditor.razor`** lines 1-468 — full read; line ranges in §Surface files are direct citations.
- **`src/CookBot.Web/Components/Pages/CookingMode.razor`** lines 1-498 — full read; lines 53, 58, 138-156 are the integration points.
- **`src/CookBot.Web/Components/Pages/PasteRawTextDialog.razor`** lines 1-68 — full read; lines 51-64 are the deletion target.
- **`src/CookBot.Application/Services/RecipeService.cs`** lines 1-185 — full read; lines 65-79 + 131-147 are the auto-write deletion targets.
- **`src/CookBot.Application/Services/TimerDetectionService.cs`** lines 1-29 — full read; new regex patterns in Item 6 replace this body.
- **`src/CookBot.Application/Services/RecipeStepTextFormatter.cs`** lines 1-66 — full read; the `IngredientLinkPattern` at lines 12-14 is the canonical regex.
- **`src/CookBot.Application/Recipes/RecipeValidator.cs`** lines 1-153 — full read; orphan/empty-section warnings already shipped (Phase 2 Plan 5).
- **`tests/CookBot.Tests/CookBot.Tests.csproj`** lines 1-39 — full read; bUnit absence verified.
- **`.planning/codebase/CONCERNS.md`** §5–7 — special-syntax burden / `text:` vs `section:` / timer regex gaps that Phase 3 closes.
- **`.planning/codebase/STACK.md`** §UI — MudBlazor 8.15 confirmed.
- **`.planning/codebase/CONVENTIONS.md`** — Razor + Blazor Server patterns, file-scoped namespaces, DI lifetimes.
- **`.planning/codebase/STRUCTURE.md`** — directory layout for new component folder.
- **`.planning/codebase/TESTING.md`** — xUnit 2.9.2 + in-memory SQLite + global `using Xunit`; bUnit not present.
- **`./CLAUDE.md`** — project constraints (no Newtonsoft, no second AI provider, MudBlazor 8.15 only, no auto-scale).
- **`.planning/config.json`** — confirms `nyquist_validation: false`, no Brave/Exa/Firecrawl available.
- **`src/CookBot.Web/wwwroot/js/cooking-timers.js`** — JS interop module pattern reference.
- **`src/CookBot.Web/Components/App.razor`** lines 19-22 — script registration pattern (4 scripts; Phase 3 adds one).

### Secondary (MEDIUM confidence)
- **MudBlazor official docs** — autocomplete, chipset, togglegroup, menu component pages.
  - [Autocomplete](https://mudblazor.com/components/autocomplete)
  - [ChipSet](https://mudblazor.com/components/chipset)
  - [ToggleGroup](https://mudblazor.com/components/togglegroup)
  - [Menu / Popover](https://mudblazor.com/components/menu)
- **MudBlazor GitHub discussions / issues** — programmatic open timing quirks ([#7569](https://github.com/MudBlazor/MudBlazor/discussions/7569)), post-selection list behavior ([#11974](https://github.com/MudBlazor/MudBlazor/issues/11974)).
- **bUnit official docs** — [getting started](https://bunit.dev/docs/getting-started/) — confirms general install pattern; specific net10 compatibility flagged as A1 in Assumptions Log.

### Tertiary (LOW confidence — flagged for execution-time verification)
- bUnit 1.40 net10 compatibility (Assumption A1).
- `MudAutocomplete.OpenAsync()` exact API surface in 8.15 (Assumption A2). Verify via Context7/CLI fallback during execution.
- `contenteditable="plaintext-only"` browser-support edge cases (Assumption A3).
- Web search results for "Blazor Server contenteditable chip token input @-trigger autocomplete pattern" — no clean precedent found; the implementation is novel for this codebase.

---

## Metadata

**Confidence breakdown:**
- Standard stack: **HIGH** — every primitive is in MudBlazor 8.15 (verified via official docs); bUnit is a single-line csproj add with documented LTS fallback.
- Architecture (component extraction + per-token segmented inline pattern): **MEDIUM** — recommendation is justified by trade-off analysis (Item 2) but not by official MudBlazor precedent. The pattern is widely used in custom Blazor token inputs; Phase 3 is the first time this codebase implements it.
- Inline timer-suggestion rendering (Item 5): **HIGH** — extending `RecipeStepTextFormatter` is the natural path; no architectural risk.
- Timer regex broadening (Item 6): **HIGH** — patterns are well-formed; logic is straightforward.
- JS-interop fallback contract (Item 7): **HIGH** — pattern matches the existing `JSDisconnectedException` catch in `CookingMode.razor:235-237`; deterministic probe strategy.
- Auto-write deletion (Item 11): **HIGH** — single-conditional change in two places; trivial diff.
- EDITOR-01 docs amendment (Item 10): **HIGH** — line and wording verified.
- Pitfalls: **HIGH** for #1, #3, #4, #5, #11; **MEDIUM** for #2 (validator-vs-warning interpretation needs plan-phase clarification), #6 (bUnit version), #7 (paste-sanitization assumes browser support).

**Research date:** 2026-04-26
**Valid until:** 2026-05-26 (one month — chip composer libraries / MudBlazor minor versions / bUnit minor versions are stable enough for monthly cadence; if Phase 3 execution slips past that, re-verify A1/A2/A3).
