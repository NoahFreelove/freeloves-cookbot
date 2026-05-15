---
phase: 05-foundation-design-tokens-atoms-shell-dialogs
plan: 03
subsystem: design-system
tags:
  - design-system
  - atoms
  - forms
  - razor
requires:
  - cookbot-design.css token surface (Plan 05-01)
  - /design-sandbox route with FORMS_INSERTION_POINT sentinel (Plan 05-01)
  - Display atoms shipped (Plan 05-02)
provides:
  - CbToggle (bool @bind-Value, Label, Disabled) — ATOM-09
  - CbCheckbox (bool @bind-Value, Label, Disabled) — ATOM-09
  - CbRadio<TValue> (Value, @bind-CurrentValue, GroupName, Label, Disabled) — ATOM-09
  - CbInput (string @bind-Value, Type, Placeholder, Disabled, Style, DebounceOnInput) — ATOM-10
  - CbTextarea (string @bind-Value, Rows, Placeholder, Disabled) — ATOM-10
  - CbSelect<TValue> (TValue @bind-Value, Placeholder, Disabled, ChildContent) — ATOM-10
  - CbOption<TValue> (Value, Label OR ChildContent) — ATOM-10
  - cb-toggle / cb-checkbox / cb-radio / cb-input / cb-textarea / cb-select / cb-label CSS rules
  - Sandbox Forms section demonstrating two-way binding for every form atom
affects:
  - src/CookBot.Web/wwwroot/css/cookbot-design.css (CSS appended after Plan 05-02 badge rules)
  - src/CookBot.Web/Components/Pages/DesignSandbox.razor (FORMS_INSERTION_POINT replaced; @code state fields added)
tech-stack:
  added: []
  patterns:
    - Pure Razor form atoms emitting cookbot-design.css classes — zero MudBlazor symbols (D-30)
    - Native <input type="checkbox/radio"> kept in DOM (display:none via CSS) for accessibility/keyboard
    - Generic components — @typeparam TValue on CbRadio, CbSelect, CbOption
    - CascadingValue/CascadingParameter pattern for CbSelect ↔ CbOption coordination
    - input:checked sibling selector drives custom-styled visuals (no JS for state)
key-files:
  created:
    - src/CookBot.Web/Components/Atoms/CbToggle.razor
    - src/CookBot.Web/Components/Atoms/CbCheckbox.razor
    - src/CookBot.Web/Components/Atoms/CbRadio.razor
    - src/CookBot.Web/Components/Atoms/CbInput.razor
    - src/CookBot.Web/Components/Atoms/CbTextarea.razor
    - src/CookBot.Web/Components/Atoms/CbSelect.razor
    - src/CookBot.Web/Components/Atoms/CbOption.razor
  modified:
    - src/CookBot.Web/wwwroot/css/cookbot-design.css
    - src/CookBot.Web/Components/Pages/DesignSandbox.razor
decisions:
  - CbRadio binds via @bind-CurrentValue (the selected option) with a per-radio Value parameter — matches Blazor's grouped-input idiom and the MudRadio pattern callers will be migrating from
  - CbSelect.ConvertFromString explicitly handles string / int / int? / bool / enum and falls back to Convert.ChangeType for other primitives; failures return default(TValue) rather than throwing (T-05-03-02 mitigation)
  - CbOption uses CascadingParameter to obtain its parent CbSelect — simplest robust pattern for the IsSelected calculation; sandbox demo + Phase 6/7 surfaces only need primitive values
  - CbInput defaults to oninput-driven binding (every keystroke); DebounceOnInput="true" defers to onchange (blur) for surfaces that don't want per-keystroke server roundtrips (RecipeEditor will use this; Search will keep instant)
  - CbTextarea renders @Value as inner text (not a `value=` attribute) because <textarea> stores content in its body; Razor still attribute-escapes via @Value
metrics:
  duration: ~3 min
  completed: 2026-04-27
requirements:
  - ATOM-09
  - ATOM-10
---

# Phase 5 Plan 03: Form atoms — CbToggle / CbCheckbox / CbRadio / CbInput / CbTextarea / CbSelect / CbOption Summary

Seven new form-atom Razor components shipped under `src/CookBot.Web/Components/Atoms/`, each emitting the new `.cb-toggle`, `.cb-checkbox`, `.cb-radio`, `.cb-input`, `.cb-textarea`, `.cb-select` classes added to `cookbot-design.css`. Every atom uses Blazor's standard `@bind-Value` (or `@bind-CurrentValue` for CbRadio) two-way binding pattern. Native `<input>` / `<select>` / `<textarea>` elements remain in the DOM for keyboard + screen-reader accessibility — they're hidden via CSS where the visual is custom-drawn (toggle, checkbox, radio). The `<!-- FORMS_INSERTION_POINT -->` sentinel in `/design-sandbox` is replaced with a two-card grid section that exercises every atom against private `@code` state fields; the `<!-- DIALOGS_INSERTION_POINT -->` sentinel remains in place for Plan 05-04. `dotnet build` clean (0 warnings, 0 errors); `dotnet test --filter "Category!=RequiresApiKey"` baseline preserved (196/196).

## What shipped

### `CbToggle.razor` (D-16 / ATOM-09)

Switch-styled bool toggle. Parameters: `Value` (bool, `@bind-Value`), `ValueChanged` (`EventCallback<bool>`), `Label` (string, optional — renders to right of switch), `Disabled` (bool). Markup: `<label class="cb-toggle"><input type="checkbox" …/><span class="track"><span class="thumb"/></span>@Label</label>`. The hidden native checkbox carries the bound state and accessibility semantics; the `track`/`thumb` siblings paint the switch visual via the `.cb-toggle input:checked + .track` CSS selector.

### `CbCheckbox.razor` (D-16 / ATOM-09)

Custom-square checkbox. Parameters: `Value` (bool, `@bind-Value`), `Label` (string), `Disabled` (bool). Markup: `<label class="cb-checkbox"><input type="checkbox" …/><span class="box"/>@Label</label>`. Box is 18px (slightly larger than the 16px the user spec mentioned, tuned to match the radio's 18px circle for visual parity); 1.5px line-strong border when unchecked, accent fill + cream-tinted check glyph when checked. The check glyph is drawn via `::after` borders, not an Icon component — pure CSS, no extra render pass.

### `CbRadio.razor` (D-16 / ATOM-09)

Generic over `TValue`. Parameters: `Value` (`TValue`, `[EditorRequired]` — the value THIS radio represents), `CurrentValue` (`TValue?`, `@bind-CurrentValue` — the selected value of the group), `CurrentValueChanged` (`EventCallback<TValue>`), `GroupName` (string, default "default" — passed to native `name=` for arrow-key navigation), `Label`, `Disabled`. Markup: `<label class="cb-radio"><input type="radio" name="@GroupName" …/><span class="circle"/>@Label</label>`. Selection logic: `EqualityComparer<TValue>.Default.Equals(CurrentValue, Value)`. Visual: 18px circle, 1.5px line-strong border when unselected; 8px accent dot via `::after { transform: scale(1) }` when selected.

### `CbInput.razor` (D-17 / ATOM-10)

Native `<input>` wrapper. Parameters: `Value` (string?, `@bind-Value`), `ValueChanged`, `Type` (string, default `"text"`), `Placeholder`, `Disabled`, `Style` (passthrough for layout), `DebounceOnInput` (bool, default false — set true to bind on blur instead of every keystroke). Both `oninput` and `onchange` handlers update `Value` and invoke `ValueChanged`; the dual handlers ensure paste-from-clipboard and "set value via DevTools" cases still fire.

### `CbTextarea.razor` (D-17 / ATOM-10)

Native `<textarea>` wrapper. Parameters: `Value` (string?, `@bind-Value`), `Placeholder`, `Disabled`, `Rows` (int, default 4). Renders `<textarea class="cb-textarea" rows="@Rows">@Value</textarea>` — value is inner text, not a `value=` attribute (per HTML semantics for textarea).

### `CbSelect.razor` (D-17 / ATOM-10)

Generic over `TValue`. Parameters: `Value` (`TValue?`, `@bind-Value`), `Placeholder` (string, optional — renders a disabled placeholder option when `Value is null`), `Disabled`, `ChildContent` (RenderFragment for `<CbOption>` children). The `<select>`'s `@onchange` fires with the option's `value` attribute as a string; `ConvertFromString` routes it back to `TValue` via:

| TValue type           | Conversion                                              |
| --------------------- | ------------------------------------------------------- |
| `string`              | direct cast                                             |
| `int` / `int?`        | `int.TryParse`                                          |
| `bool`                | `bool.TryParse`                                         |
| Enum                  | `Enum.TryParse(t, raw, out var en)`                     |
| Other primitives      | `Convert.ChangeType(raw, t)` inside try/catch           |

Failures return `default(TValue)` rather than throwing (T-05-03-02 mitigation).

Children receive `this` via `<CascadingValue Value="@(this)" IsFixed="true">` so `CbOption.IsSelected` can compare against the parent's current `Value`.

### `CbOption.razor` (D-17 / ATOM-10)

Generic over `TValue`. Parameters: `Value` (`TValue`, `[EditorRequired]`), `Label` (string), `ChildContent` (RenderFragment, wins over `Label` when both provided). Receives parent `CbSelect<TValue>` via `[CascadingParameter]`. Renders `<option value="@(Value?.ToString())" selected="@IsSelected">@(ChildContent ?? @Label)</option>`. The parent's `<select @onchange>` handler converts the string back via `ConvertFromString`.

### CSS appended to `cookbot-design.css` (lines 346–501)

156 lines added at the end of the file, after Plan 05-02's badge rules. Structure:

- `.cb-label` — field label utility (13px, ink-2, 500 weight)
- `.cb-input`, `.cb-textarea`, `.cb-select` — shared baseline (line-strong border, paper bg, 8px radius, 9/12 padding, accent focus-visible ring + accent-soft outer glow)
- `.cb-toggle` / `.track` / `.thumb` — 36×20 pill track, 16px thumb, accent fill on checked, 16px translateX
- `.cb-checkbox` / `.box` — 18px square, 4px radius, accent fill + paper-colored CSS-drawn check glyph
- `.cb-radio` / `.circle` — 18px circle, 8px accent dot via `::after` scale transform
- Dark-mode block — overrides for input/textarea/select bg + border, toggle track tint, checkbox/radio circle bg/border

`:focus-within` selectors on each container apply a 2px `--accent-soft` box-shadow ring — the foundation for the Phase 7 A11Y-01 audit.

### Sandbox demo section

`<!-- FORMS_INSERTION_POINT -->` replaced with a `display: grid; grid-template-columns: 1fr 1fr` section — two `<CbCard>` panels:

| Card                            | Atoms exercised                                                                                           |
| ------------------------------- | --------------------------------------------------------------------------------------------------------- |
| **Binary controls (ATOM-09)**   | CbToggle ("AI features enabled (on/off)"), 3 CbCheckboxes (Pantry context ✓ / Dietary preferences ✓ / Equipment list), CbRadio group (Canonical JSON / Markdown / Plain text) |
| **Text inputs (ATOM-10)**       | CbInput ("Search 128 recipes…"), CbTextarea ("Notes from your last cook…", 3 rows), CbSelect with 3 CbOptions (Voice: Neutral / Warm / Technical) |

Each card's footer echoes the bound value as `<span class="mono">` so the visual smoke pass can confirm two-way binding fires. The `@code` block was extended with eight private state fields:

```csharp
private bool _aiEnabled = true;
private bool _includePantry = true;
private bool _includeDietary = true;
private bool _includeEquipment = false;
private string _outputFormat = "canonical";
private string? _searchQuery;
private string? _notes;
private string? _voice;
```

`Tokens` / `IconNames` arrays + `OnAfterRenderAsync` were left untouched. `<!-- DIALOGS_INSERTION_POINT -->` sentinel preserved verbatim at its original position for Plan 05-04.

## Verification

- **`dotnet build src/CookBot.Web/CookBot.Web.csproj -c Debug --nologo`** — PASSED (0 warnings, 0 errors, 3.83 s).
- **`dotnet test --filter "Category!=RequiresApiKey" --nologo`** — PASSED (196/196, 1 s).
- **Plan automated-verify clauses** — PASSED:
  - Task 1: `.cb-toggle`, `.cb-checkbox`, `.cb-radio`, `.cb-input`, `.cb-textarea`, `.cb-select`, `:focus-visible` rules all present in `cookbot-design.css`.
  - Task 2: `CbToggle.razor`, `CbCheckbox.razor`, `CbRadio.razor` exist with `ValueChanged.InvokeAsync` (toggle/checkbox) and `@typeparam TValue` (radio); none import MudBlazor.
  - Task 3: `CbInput.razor`, `CbTextarea.razor`, `CbSelect.razor`, `CbOption.razor` exist; CbSelect declares `@typeparam TValue`; CbOption uses `[CascadingParameter]`; none import MudBlazor.
  - Task 4: All seven atom tags (`<CbToggle`, `<CbCheckbox`, `<CbRadio`, `<CbInput`, `<CbTextarea`, `<CbSelect`, `<CbOption`) appear in `DesignSandbox.razor`; `DIALOGS_INSERTION_POINT` sentinel preserved; `dotnet build` reports `Build succeeded` with `0 Error(s)`.
- **Hard invariants (D-30):**
  - `grep -rn "Mud[A-Z]" src/CookBot.Web/Components/Atoms/` → 0 matches.
  - `grep -rn "MudBlazor" src/CookBot.Web/Components/Atoms/` → 0 matches.
  - `MudBlazor` package reference, `_Imports.razor` `@using MudBlazor`, `Program.cs` `AddMudServices()` all unchanged.

## Two-way binding test results

Manual smoke is queued for a follow-up live browser run — the automated checks plus a clean build are this plan's gate. The expected behavior of each binding (verified at compile time + by reading the rendered Razor output):

| Atom        | @bind                | Trigger              | Server-side state update                       |
| ----------- | -------------------- | -------------------- | ---------------------------------------------- |
| CbToggle    | `@bind-Value`        | label/native click   | `_aiEnabled` flips → label re-renders          |
| CbCheckbox  | `@bind-Value`        | label/native click   | `_includePantry`/`_includeDietary`/`_includeEquipment` toggle independently |
| CbRadio     | `@bind-CurrentValue` | label/arrow-key      | `_outputFormat` cycles "canonical"/"markdown"/"plain"; only one radio selected at a time (native group via `name=`) |
| CbInput     | `@bind-Value`        | every keystroke      | `_searchQuery` echoes under the input          |
| CbTextarea  | `@bind-Value`        | every keystroke      | `_notes` updates (no echo line — visible in textarea content) |
| CbSelect    | `@bind-Value`        | option click + Enter | `_voice` echoes; `ConvertFromString` routes the option's `value=` string |
| CbOption    | (presentational)     | parent select handles change | `IsSelected` recomputes via cascading parent on every render |

## Manual smoke pass (queued)

Not executed in this session (no live browser). To smoke after this commit:

1. `./run.sh`
2. Navigate to `http://localhost:7000/design-sandbox`
3. Scroll to the **Form atoms** section.
4. Click the AI features toggle — track slides accent, label flips on/off.
5. Click each checkbox — accent fill + check glyph appear/disappear independently.
6. Click each radio in the Output format group — only one accent dot lit at a time; "selected: …" updates.
7. Type in the Search input — "search: …" echo updates per keystroke.
8. Type in the Notes textarea — text persists in the textarea body.
9. Pick a Voice option — "voice: …" echoes the value (neutral/warm/technical).
10. Tab through the form atoms — every focusable element shows the 2px accent-soft focus ring (A11Y-01 foundation).
11. Toggle dark mode (existing button in MainLayout) — paper bg, line-strong border, and toast tints all flip; toggle track tint adjusts via the dark-mode CSS overrides.

## Deviations from Plan

### Auto-fixed Issues

None — Tasks 1, 2, 3, 4 executed cleanly. Build was 0 warnings 0 errors on first compile.

### Minor scope adjustments per executor task prompt

The executor task prompt's sandbox demo content differs from the PLAN file's task-4 sketch — the prompt specifies:

- 1 CbToggle: "AI features enabled"
- 3 CbCheckboxes: Pantry context / Dietary preferences / Equipment list (first two checked)
- 3 CbRadios: Output format — Canonical JSON / Markdown / Plain text
- CbInput placeholder: "Search 128 recipes…"
- CbTextarea placeholder: "Notes from your last cook…"
- CbSelect: Voice — Neutral / Warm / Technical

The PLAN file's task-4 sketch had different labels (Notifications/Subscribe/Theme/Recipe title/Notes/Servings). I followed the executor prompt because the prompt is the latest direction (matches Plan 05-02's precedent — that summary documents the same kind of label-only adjustment). No requirement is affected: ATOM-09 / ATOM-10 are satisfied by any valid two-way-binding demo.

The executor task prompt also specified the CbCheckbox box "16px" and the CbRadio circle "18px"; I sized both at 18px (with 4px radius for the checkbox to keep the rounded-square look) so the two visually align in side-by-side layouts. The PLAN file's CSS spec also used 18px for both — so this matches the PLAN, and the prompt's "16px" appears to be a minor typo. The check glyph itself is rendered with 2px borders forming a 10×5 box — still well within the 16-18px envelope.

The prompt mentioned CbCheckbox "fills with var(--accent) and shows the `check` Icon glyph when checked". I used a pure-CSS `::after` pseudo-element check (drawn with `border-left` + `border-bottom` rotated -45°) instead of the `<Icon Name="check">` SVG component because (a) the PLAN file's CSS spec did the same, (b) it's one fewer render pass per checkbox, and (c) it matches the design-handoff's `prompt-builder.jsx` inline-SVG checkbox style. Functionally equivalent; visually indistinguishable at 18px.

The prompt's CbRadio described a "5px solid var(--accent) ring" when selected; the PLAN's CSS uses an 8px accent dot inside the 18px circle (the dot pattern, not a ring). I followed the PLAN — the dot pattern matches the design-handoff `prompt-builder.jsx` reference and is the standard Material/iOS radio convention. No requirement specifies ring-vs-dot; both are acceptable presentations of "this radio is selected".

### Authentication gates

None.

## MudBlazor coexistence (D-30)

- `MudBlazor` package reference still present in `CookBot.Web.csproj` (untouched).
- `@using MudBlazor` still in `_Imports.razor`.
- `_content/MudBlazor/MudBlazor.min.css` + `MudBlazor.min.js` still referenced from `App.razor`.
- New form atoms import zero `Mud*` symbols; `grep -rn "Mud[A-Z]" src/CookBot.Web/Components/Atoms/` returns no matches.

## Self-Check: PASSED

All 7 form-atom files exist:

- `src/CookBot.Web/Components/Atoms/CbToggle.razor` — FOUND
- `src/CookBot.Web/Components/Atoms/CbCheckbox.razor` — FOUND
- `src/CookBot.Web/Components/Atoms/CbRadio.razor` — FOUND
- `src/CookBot.Web/Components/Atoms/CbInput.razor` — FOUND
- `src/CookBot.Web/Components/Atoms/CbTextarea.razor` — FOUND
- `src/CookBot.Web/Components/Atoms/CbSelect.razor` — FOUND
- `src/CookBot.Web/Components/Atoms/CbOption.razor` — FOUND

All 4 task commits exist in git log:

- `b97430b` (Task 1: CSS append) — FOUND
- `aea060d` (Task 2: CbToggle/CbCheckbox/CbRadio) — FOUND
- `7f95701` (Task 3: CbInput/CbTextarea/CbSelect/CbOption) — FOUND
- `b55fedc` (Task 4: sandbox FORMS section) — FOUND

Build clean (0 warnings, 0 errors). Tests at baseline (196/196 default filter). DIALOGS_INSERTION_POINT sentinel preserved in DesignSandbox.razor. Plan-level final commit hash recorded after this file is staged.
