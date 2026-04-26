# Phase 3: Editor UX Without Special Syntax — Pattern Map

**Mapped:** 2026-04-26
**Files analyzed:** 19 (7 modified + 12 created)
**Analogs found:** 18 / 19 (one "no analog — use RESEARCH.md scaffold")

This document is the planner's reference for *what existing code each new/modified file in Phase 3 should copy from*. Every new file has a closest analog in this codebase, except per-token `MudPopover` anchored UI (no usage today — RESEARCH.md prescribes the pattern). bUnit is being added; xUnit cousins are surfaced for each new bUnit test.

---

## File Classification

| File | New / Modified | Role | Data Flow | Closest Analog | Match |
|---|---|---|---|---|---|
| `RecipeEditor.razor` | modified | razor page (interactive) | `ParsedRecipe` ↔ form state ↔ `RecipeService` | itself + extracted sub-components | self-rewrite |
| `CookingMode.razor` | modified | razor page (interactive) | read `Recipe` → render | itself; pulls in `RecipeChipComposer` (Interactive=false) | self-rewrite |
| `PasteRawTextDialog.razor` | modified | dialog (deletion-only) | text → `IRecipeFormatParser.TryParse` → `DialogResult.Ok(parsed)` | itself (lines 1-49 stay; 51-64 deleted) | self-shrink |
| `RecipeStepTextFormatter.cs` | modified | static formatter | `string text` → HTML | itself; one new pass added | self-extension |
| `TimerDetectionService.cs` | modified | static service | `string text` → `List<StepTimer>` | itself; regex broadens | self-extension |
| `RecipeService.cs` | modified | application service | `ParsedRecipe` → DB | itself; auto-write deletion at lines 65-79 + 131-147 | self-deletion |
| `AiChat.razor` | verification only | razor page | (no edits) | n/a | n/a |
| `RecipeEditor/RecipeStepEditor.razor` | created | razor sub-component | `ParsedStep` ↔ chip composer | inline body of `RecipeEditor.razor:127-173` | extract |
| `RecipeEditor/RecipeChipComposer.razor` | created | shared razor sub-component | `string text` ↔ tokenized chip rendering | `RecipeStepTextFormatter.ToHtml` callsites in `RecipeEditor.razor:144-167` + `CookingMode.razor:53,58` | role-match |
| `RecipeEditor/IngredientChip.razor` | created | razor sub-component | `Ingredient` (display) + click-callback | `MudChip<string>` usage in `RecipeEditor.razor:161-165` | partial |
| `RecipeEditor/TimerChip.razor` | created | razor sub-component | `StepTimer` (display+edit) + click-callback | `MudChip<string>` usage in `RecipeEditor.razor:154-157` | partial |
| `RecipeEditor/InlineTimerSuggestion.razor` | created | razor sub-component | detected substring → click-popover | **no exact analog** — see Shared Patterns §Anchored Popover; copy `MudMenu` shape from `CookbookDetail.razor:30-38` | role-only |
| `RecipeEditor/SectionDropConfirmationDialog.razor` | created | dialog | `(timerCount, refCount)` → `DialogResult.Ok(true)` | `PasteRawTextDialog.razor` (full file) | exact |
| `wwwroot/js/recipe-chip-composer.js` | created | JS interop module | DOM ↔ `IJSRuntime.InvokeAsync` | `wwwroot/js/cooking-timers.js` | exact |
| `tests/Web/RecipeChipComposerTests.cs` | created | bUnit component test | render component → assert markup | `RecipeStepTextFormatterTests.cs` (xUnit cousin) + bUnit scaffold from RESEARCH.md Item 4 | xUnit-cousin |
| `tests/Web/StepSectionToggleTests.cs` | created | bUnit component test | toggle event → assert state | `RecipeStepTextFormatterTests.cs` (cousin) | xUnit-cousin |
| `tests/Web/TimerSuggestionTests.cs` | created | bUnit component test | render + click → assert chip strip | `TimerDetectionServiceTests.cs` (cousin) | xUnit-cousin |
| `tests/Web/PasteFlowTests.cs` | created | bUnit component test | render dialog → submit → assert handoff | `RecipeFormatParserTests.cs` (cousin, parse asserts) | xUnit-cousin |
| `tests/Application/TimerDetectionServiceRegexTests.cs` | created | xUnit test | regex → assert detected timers | `TimerDetectionServiceTests.cs` (existing — extend pattern) | exact |

---

## Pattern Assignments

### `src/CookBot.Web/Components/Pages/RecipeEditor.razor` (modified — major rewrite of steps section)

**Analog:** itself (the file's own established patterns; extraction-style rewrite).

**Page header pattern to preserve** (lines 1-12):
```razor
@page "/cookbooks/{CookbookId:int}/recipes/new"
@page "/recipes/{RecipeId:int}/edit"
@inject CookBot.Infrastructure.Data.CookBotDbContext DbContext
@inject CookBot.Domain.Interfaces.IRecipeFormatParser Parser
@inject CookBot.Application.Services.RecipeService RecipeService
@inject CurrentUserService UserService
@inject NavigationManager Navigation
@inject ISnackbar Snackbar
@inject IDialogService DialogService
@rendermode InteractiveServer
```

**Steps section to rewrite** (lines 108-182): collapse two buttons + inline body. Today's body (the analog being replaced):
```razor
@* lines 122-174 — replace with @for { <RecipeStepEditor Step="step" Index="index" Ingredients="_ingredients" ... /> } *@
@for (int i = 0; i < _steps.Count; i++)
{
    var index = i;
    var step = _steps[index];
    <MudPaper Class="pa-3 mb-3" Elevation="0" Style="border: 1px solid #e0e0e0; border-radius: 8px;">
        <MudStack Row="true" AlignItems="AlignItems.Start">
            <MudStack Spacing="0" Style="min-width: 36px;" AlignItems="AlignItems.Center">
                <MudIconButton Icon="@Icons.Material.Filled.KeyboardArrowUp" Size="Size.Small"
                               Disabled="@(index == 0)" OnClick="@(() => MoveStepUp(index))" />
                <MudText Typo="Typo.caption" Color="Color.Secondary">@(index + 1)</MudText>
                ...
```

**`PopulateFromParsed` pattern to extend** (lines 273-305) — add a parse-warnings field for D-D1/D-D2 banner:
```csharp
private void PopulateFromParsed(CookBot.Domain.Interfaces.ParsedRecipe parsed)
{
    if (!string.IsNullOrWhiteSpace(parsed.Name))
        _name = parsed.Name;
    if (parsed.Servings > 0)
        _servings = parsed.Servings;
    _prepTimeMinutes = parsed.PrepTimeMinutes;
    _cookTimeMinutes = parsed.CookTimeMinutes;
    // ... (extend: also populate _warningBanner from parsed validation result)
}
```

**`AddStep` / `AddSectionHeader` to collapse** (lines 343-351) — delete `AddSectionHeader`; per-step `MudToggleGroup` lives in `RecipeStepEditor`:
```csharp
// BEFORE — keep AddStep, DELETE AddSectionHeader:
private void AddStep() => _steps.Add(new ParsedStep { Text = string.Empty, IsSection = false });
private void AddSectionHeader() => _steps.Add(new ParsedStep { Text = string.Empty, IsSection = true });
```

**`DetectIngredientRefsInStep` to delete entirely** (lines 371-381) — Phase 1 D-13 retired substring detection; this is the last vestige.

**`PasteRawText` to keep unchanged** (lines 385-396):
```csharp
private async Task PasteRawText()
{
    var options = new DialogOptions { MaxWidth = MaxWidth.Medium, FullWidth = true, CloseOnEscapeKey = true };
    var dialog = await DialogService.ShowAsync<PasteRawTextDialog>("Paste Raw Text", options);
    var result = await dialog.Result;
    if (result != null && !result.Canceled && result.Data is ParsedRecipe parsed)
    {
        PopulateFromParsed(parsed);
        StateHasChanged();
    }
}
```

**`SaveRecipe` to keep unchanged in body** (lines 400-459) — `_steps` is now populated by chip composer state; no signature change.

**What to copy / adapt:**
- Copy: header pattern (line 1-12), `OnAfterRenderAsync(firstRender)` data-load (lines 203-239), `PopulateFromParsed` shape (lines 273-305), `SaveRecipe` body (lines 400-459).
- Adapt: replace lines 122-174 step body with `<RecipeStepEditor>` invocation; delete `AddSectionHeader` and `DetectIngredientRefsInStep`; add `_warningBanner` field + top-of-editor `<MudAlert>` reading parser/validator warnings (D-D1/D-D2).
- Drop budget: 468 lines → ~250 lines.

---

### `src/CookBot.Web/Components/Pages/CookingMode.razor` (modified — read-only chip rendering + scroll-on-click)

**Analog:** itself.

**Existing chip-equivalent rendering call site to wrap** (lines 50-59):
```razor
@if (CurrentSectionHeader != null)
{
    <MudText Typo="Typo.subtitle1" Color="Color.Primary" Class="mb-2 recipe-body">
        @((MarkupString)RecipeStepTextFormatter.ToHtml(CurrentSectionHeader))
    </MudText>
}

<MudText Typo="Typo.h4" Style="font-weight: 500; line-height: 1.6;" Class="mb-4 recipe-body">
    @((MarkupString)RecipeStepTextFormatter.ToHtml(CurrentStep.Text))
</MudText>
```
After Phase 3 — replace inner content with shared `RecipeChipComposer` in read-only mode:
```razor
<RecipeChipComposer Interactive="false"
                    Text="@CurrentStep.Text"
                    Ingredients="@_recipe.RecipeIngredients"
                    OnIngredientChipClick="ScrollToIngredient" />
```

**Existing JS-interop disposal pattern to copy for new chip-composer JS calls** (lines 230-237, 485-496):
```csharp
try
{
    await JS.InvokeVoidAsync("CookingTimers.dispose");
}
catch (JSDisconnectedException) { }
```
This is **the** load-bearing pattern for D-D4 (Pitfall 4). Apply identically when calling `RecipeChipComposer.scrollIntoViewWithHighlight`.

**Existing `[JSInvokable]` callback pattern** (lines 418-434):
```csharp
[JSInvokable]
public void OnTimerTick(string timerId, int remainingSeconds)
{
    if (_activeTimers.ContainsKey(timerId))
    {
        _activeTimers[timerId] = remainingSeconds;
        InvokeAsync(StateHasChanged);
    }
}
```
The chip-composer JS module does not need `[JSInvokable]` callbacks — interop is one-way (C# → JS for scroll). No `DotNetObjectReference` needed for D-D3.

**Ingredient sidebar to add `id` attribute** (lines 138-156):
```razor
@foreach (var ri in _recipe.RecipeIngredients.OrderBy(i => i.RecipeLocalId))
{
    var isReferenced = CurrentStep.IngredientRefs.Contains(ri.RecipeLocalId);
    <MudStack Row="true" AlignItems="AlignItems.Center"
              id="@($"ingredient-{ri.RecipeLocalId}")"  @* NEW *@
              Style="@(isReferenced ? "background: var(--mud-palette-primary-lighten); ... " : "...")">
        ...
```

**What to copy / adapt:**
- Copy: `JSDisconnectedException` swallow pattern (lines 234-237) for every new JS interop call.
- Adapt: replace the two `MarkupString RecipeStepTextFormatter.ToHtml` call sites (lines 53, 58) with `<RecipeChipComposer Interactive="false" .../>`; add `id="ingredient-{N}"` to sidebar row; add `ScrollToIngredient(int recipeLocalId)` method.
- Keep unchanged: timer rendering (lines 61-81) — D-D3 says timer chips retain today's start-timer button treatment.

---

### `src/CookBot.Web/Components/Pages/PasteRawTextDialog.razor` (modified — delete fallback)

**Analog:** itself.

**Pattern to keep** (lines 1-49):
```razor
@inject CookBot.Domain.Interfaces.IRecipeFormatParser Parser

<MudDialog>
    <TitleContent>
        <MudText Typo="Typo.h6">Paste Raw Recipe Text</MudText>
    </TitleContent>
    <DialogContent>
        <MudTextField @bind-Value="_rawText" T="string" Variant="Variant.Outlined"
                      Lines="15" Label="Paste your recipe text here" ... />
        @if (_errors.Any())
        {
            <MudAlert Severity="Severity.Warning" Class="mt-2" Dense="true">
                @foreach (var error in _errors) { <div>@error</div> }
            </MudAlert>
        }
    </DialogContent>
    <DialogActions>
        <MudButton OnClick="Cancel">Cancel</MudButton>
        <MudButton Color="Color.Primary" Variant="Variant.Filled" OnClick="Submit"
                   Disabled="@string.IsNullOrWhiteSpace(_rawText)">Import</MudButton>
    </DialogActions>
</MudDialog>
```

**Hand-rolled fallback to delete** (lines 50-64):
```csharp
// DELETE — Phase 1's RecipeFormatParser.TryParse already handles coercion-with-warnings:
var lines = _rawText.Split('\n')
    .Select(l => System.Text.RegularExpressions.Regex.Replace(l.Trim(), @"^\d+\.\s*", ""))
    .Where(l => !string.IsNullOrWhiteSpace(l))
    .ToList();
if (lines.Count > 0)
{
    var partial = new ParsedRecipe { Steps = lines.Select(l => new ParsedStep { Text = l }).ToList() };
    MudDialog.Close(DialogResult.Ok(partial));
    return;
}
```

**`Submit` after deletion** (canonical, per RESEARCH.md line 474-487):
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

**What to copy / adapt:** Pure deletion — 14 lines deleted, 0 added. No new patterns introduced.

---

### `src/CookBot.Application/Services/RecipeStepTextFormatter.cs` (modified — extend with timer-suggestion wraps)

**Analog:** itself.

**Existing two-method shape** (lines 10-65):
```csharp
public static class RecipeStepTextFormatter
{
    private static readonly Regex IngredientLinkPattern = new(
        @"\[([^\]]*)\]\(#(\d+)\)",
        RegexOptions.Compiled);

    public static string ToHtml(string? text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        var normalized = text.Replace("\r\n", "\n", ...).Replace("\r", "\n", ...);
        var sb = new StringBuilder();
        var last = 0;
        foreach (Match m in IngredientLinkPattern.Matches(normalized))
        {
            if (m.Index > last)
                sb.Append(EncodeWithLineBreaks(normalized.AsSpan(last, m.Index - last)));
            var display = WebUtility.HtmlEncode(m.Groups[1].Value);
            var id = WebUtility.HtmlEncode(m.Groups[2].Value);
            sb.Append("<span class=\"ingredient-ref\" data-ingredient-id=\"")
                .Append(id).Append("\">").Append(display).Append("</span>");
            last = m.Index + m.Length;
        }
        ...
```

**New method to add** (per RESEARCH.md Item 5):
```csharp
public static string ToHtmlWithTimerSuggestions(string? text, IReadOnlySet<int> alreadyConvertedDurationsSeconds)
{
    var html = ToHtml(text);
    return TimerSubstringPattern.Replace(html, m =>
    {
        var seconds = ParseDurationToSeconds(m.Groups[1].Value, m.Groups[2].Value);
        if (alreadyConvertedDurationsSeconds.Contains(seconds))
            return m.Value;
        return $"<span class=\"timer-suggestion\" data-duration-seconds=\"{seconds}\">{m.Value}</span>";
    });
}
```

**Idempotency consideration** — must NOT double-wrap inside an existing `<span class="ingredient-ref">`. RESEARCH.md Item 5 calls this an Open Question; the planner picks single-pass-with-skip vs. pre-pass.

**What to copy / adapt:**
- Copy: `IngredientLinkPattern` declaration shape (line 12-14), `WebUtility.HtmlEncode` for safety, `\r\n` normalization.
- Adapt: lift `IngredientLinkPattern` to `internal static readonly` OR move to a new shared `internal static class IngredientLinkPatterns` in `CookBot.Application/Recipes/` (RESEARCH.md Open Question 3 recommends the latter — also benefits `RecipeValidator.cs:15-17` deduplication).
- Add: second-pass `ToHtmlWithTimerSuggestions` overload + private `ParseDurationToSeconds` helper.

---

### `src/CookBot.Application/Services/TimerDetectionService.cs` (modified — broaden regex)

**Analog:** itself.

**Today's full file** (29 lines):
```csharp
public static class TimerDetectionService
{
    private static readonly Regex TimerPattern = new(
        @"(\d+)\s*(minutes?|mins?|hours?|hrs?|seconds?|secs?)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static List<StepTimer> DetectTimers(string text)
    {
        var timers = new List<StepTimer>();
        foreach (Match match in TimerPattern.Matches(text))
        {
            var duration = int.Parse(match.Groups[1].Value);
            var unitStr = match.Groups[2].Value.ToLowerInvariant();
            var unit = unitStr switch
            {
                var u when u.StartsWith("sec") => "sec",
                var u when u.StartsWith("hr") || u.StartsWith("hour") => "hr",
                _ => "min"
            };
            timers.Add(new StepTimer { Duration = duration, Unit = unit });
        }
        return timers;
    }
}
```

**Phase 3 extension** (per RESEARCH.md Item 6, ~70 lines): four ordered patterns (multi-segment → range → fractional → simple), with range-value persistence as **lowest** value. Verbatim regex bodies in RESEARCH.md lines 819-839.

**What to copy / adapt:**
- Copy: `RegexOptions.IgnoreCase | RegexOptions.Compiled` posture, unit-mapping switch idiom (lines 19-24), `List<StepTimer>` return shape.
- Adapt: split into 4 named regex fields (`MultiSegmentPattern`, `RangePattern`, `FractionalPattern`, `SimplePattern`), apply in longest-match order, add `ParseFractionalToSeconds` helper.
- Public-surface change: lift `TimerPattern` to `public static readonly` so `RecipeStepTextFormatter` can reuse it for the timer-suggestion second pass (per RESEARCH.md Item 5 line 788). Already noted as the cleanest option.

---

### `src/CookBot.Application/Services/RecipeService.cs` (modified — auto-write deletion)

**Analog:** itself.

**Today's auto-write site (CreateAsync, lines 65-79):**
```csharp
int order = 0;
foreach (var ps in parsed.Steps)
{
    var step = new RecipeStep
    {
        Order = order++,
        Text = ps.Text,
        IsSection = ps.IsSection,
        Timers = ps.IsSection ? new() :
            (ps.Timers?.Any() == true
                ? ps.Timers.Select(t => new StepTimer { Duration = t.Duration, Unit = t.Unit, Label = t.Label }).ToList()
                : TimerDetectionService.DetectTimers(ps.Text)),  // <-- DELETE this fallback
    };
    recipe.Steps.Add(step);
}
```

**After Phase 3** (per RESEARCH.md Item 11):
```csharp
Timers = ps.IsSection
    ? new()
    : (ps.Timers ?? new()).Select(t => new StepTimer { Duration = t.Duration, Unit = t.Unit, Label = t.Label }).ToList(),
```

**Same change at UpdateAsync, lines 131-147.**

**Auth + canonical-doc patterns to leave alone** (lines 35-49, 85-86, 102-106, 150-151):
```csharp
var cookbook = await _cookbookRepo.GetByIdAsync(cookbookId)
    ?? throw new InvalidOperationException("Cookbook not found.");
if (cookbook.UserId != userId)
    throw new UnauthorizedAccessException("You do not own this cookbook.");
// ...
var canonicalDoc = _projector.Project(recipe);
recipe.CanonicalDocumentJson = _canonicalSerializer.Serialize(canonicalDoc);
```
Phase 1 D-12 hybrid persistence pattern is **untouched** by Phase 3.

**What to copy / adapt:**
- Pure deletion of `: TimerDetectionService.DetectTimers(ps.Text)` ternary fallback at two sites.
- The `using` for `CookBot.Application.Services` stays — `TimerDetectionService` is still called by the new `RecipeStepEditor` for inline suggestions.

---

### `src/CookBot.Web/Components/Pages/AiChat.razor` (verification only)

**Analog:** n/a — no edits.

**Verification check:** the "Save Recipe to Cookbook" handoff path lands cleanly in the new chip composer with the inline-banner UI on validation warnings (D-D2). No code changes expected; the planner adds a verification task in `03-VERIFICATION.md`.

---

### `src/CookBot.Web/Components/Pages/RecipeEditor/RecipeStepEditor.razor` (CREATED — per-step row component)

**Analog:** the inline `<MudPaper>` step-row body in `RecipeEditor.razor:127-173`.

**Pattern to copy** (the analog body, modified to wrap chip composer + toggle):
```razor
@* analog: src/CookBot.Web/Components/Pages/RecipeEditor.razor:127-173 *@
<MudPaper Class="pa-3 mb-3" Elevation="0" Style="border: 1px solid #e0e0e0; border-radius: 8px;">
    <MudStack Row="true" AlignItems="AlignItems.Start">
        <MudStack Spacing="0" Style="min-width: 36px;" AlignItems="AlignItems.Center">
            <MudIconButton Icon="@Icons.Material.Filled.KeyboardArrowUp" Size="Size.Small"
                           Disabled="@(Index == 0)" OnClick="OnMoveUp" />
            <MudText Typo="Typo.caption" Color="Color.Secondary">@(Index + 1)</MudText>
            <MudIconButton Icon="@Icons.Material.Filled.KeyboardArrowDown" Size="Size.Small"
                           Disabled="@IsLast" OnClick="OnMoveDown" />
        </MudStack>
        <MudStack Style="flex: 1;" Spacing="1">
            @* Step/Section toggle (D-B1) *@
            <MudToggleGroup T="StepKind" SelectedValue="_kind" SelectedValueChanged="OnKindRequested" ... />
            @if (_kind == StepKind.Section)
            {
                <MudTextField @bind-Value="Step.Text" T="string" Label="Section Header" ... />
            }
            else
            {
                <RecipeChipComposer Interactive="true" @bind-Text="Step.Text" Ingredients="Ingredients" ... />
                @* Timer chip strip (D-C3) below composer *@
            }
        </MudStack>
        <MudIconButton Icon="@Icons.Material.Filled.Delete" Color="Color.Error" Size="Size.Small" OnClick="OnRemove" />
    </MudStack>
</MudPaper>
```

**Step/Section toggle binding pattern** (per Pitfall 5 — one-way bind, async confirm before flip):
```csharp
// One-way bind, NOT @bind-SelectedValue, so Cancel can revert:
private async Task OnKindRequested(StepKind requested)
{
    if (requested == StepKind.Section && (Step.Timers?.Any() == true || HasIngredientRefs(Step.Text)))
    {
        var dialog = await DialogService.ShowAsync<SectionDropConfirmationDialog>("Convert?", new DialogParameters
        {
            ["TimerCount"] = Step.Timers?.Count ?? 0,
            ["RefCount"] = CountIngredientRefs(Step.Text),
        });
        var result = await dialog.Result;
        if (result?.Canceled ?? true) return;
    }
    _kind = requested;
    if (_kind == StepKind.Section)
    {
        // D-B2: reuse step text as heading
        Step.IsSection = true;
        Step.Timers?.Clear();
    }
    StateHasChanged();
}
```

**Parameter pattern** (copy from MudBlazor convention + existing dialog parameters in `ShareCookbookDialog.razor:70-72`):
```csharp
[Parameter] public ParsedStep Step { get; set; } = null!;
[Parameter] public int Index { get; set; }
[Parameter] public bool IsLast { get; set; }
[Parameter] public List<ParsedIngredient> Ingredients { get; set; } = new();
[Parameter] public EventCallback OnRemove { get; set; }
[Parameter] public EventCallback OnMoveUp { get; set; }
[Parameter] public EventCallback OnMoveDown { get; set; }
```

**What to copy / adapt:**
- Copy: the entire visual shell from `RecipeEditor.razor:127-173` (move-up/down stack, delete button, MudPaper border styling).
- Adapt: replace the inline `MudTextField` step body with `<RecipeChipComposer>`; replace `step.IsSection` branch with `MudToggleGroup`-driven `_kind` state and async confirmation flow (Pitfall 5).
- Inject: `IDialogService` for the confirmation dialog (matches `RecipeEditor.razor:9` pattern).

---

### `src/CookBot.Web/Components/Pages/RecipeEditor/RecipeChipComposer.razor` (CREATED — shared composer)

**Analog:** the two `RecipeStepTextFormatter.ToHtml` call sites — `CookingMode.razor:53,58` (read-only) and `RecipeEditor.razor:144-167` (interactive).

**Read-only call-site pattern to absorb** (`CookingMode.razor:57-59`):
```razor
<MudText Typo="Typo.h4" Style="font-weight: 500; line-height: 1.6;" Class="mb-4 recipe-body">
    @((MarkupString)RecipeStepTextFormatter.ToHtml(CurrentStep.Text))
</MudText>
```
After Phase 3, the `MarkupString` rendering moves *inside* `RecipeChipComposer` when `Interactive="false"`.

**Interactive call-site pattern to absorb** (`RecipeEditor.razor:144-167`):
```razor
var timers = TimerDetectionService.DetectTimers(step.Text ?? string.Empty);
var ingredientRefs = DetectIngredientRefsInStep(step.Text ?? string.Empty);
<MudTextField @bind-Value="step.Text" T="string" Label="Step Instructions" Variant="Variant.Outlined"
              Lines="3" Placeholder="Describe this step..."
              Immediate="true" DebounceInterval="500" />
@if (timers.Any() || ingredientRefs.Any())
{
    <MudStack Row="true" Spacing="1" Wrap="Wrap.Wrap" Class="mt-1">
        @foreach (var timer in timers)
        {
            <MudChip T="string" Size="Size.Small" Color="Color.Warning" Variant="Variant.Filled"
                     Icon="@Icons.Material.Filled.Timer">
                @timer.Duration @timer.Unit
            </MudChip>
        }
        @foreach (var refName in ingredientRefs)
        {
            <MudChip T="string" Size="Size.Small" Color="Color.Info" Variant="Variant.Text"
                     Icon="@Icons.Material.Filled.Egg">
                @refName
            </MudChip>
        }
    </MudStack>
}
```
**Key conventions to preserve:**
- `Immediate="true" DebounceInterval="500"` — D-C1 says this is the right cadence for inline-suggestion detection.
- `Color.Warning` for timers; `Color.Info` for ingredients; `Icons.Material.Filled.Timer` and `.Egg` icons stay.

**Tokenizer pattern to embed** (per RESEARCH.md Code Examples §Tokenizer, lines 593-619):
```csharp
public sealed record StepToken(int Index, bool IsChip, string Text, int IngredientId = 0);

private IEnumerable<StepToken> TokenizeText(string text)
{
    if (string.IsNullOrEmpty(text)) { yield return new StepToken(0, IsChip: false, Text: ""); yield break; }
    var last = 0; var tokenIndex = 0;
    foreach (Match m in IngredientLinkPatterns.Pattern.Matches(text))  // shared pattern
    {
        if (m.Index > last)
            yield return new StepToken(tokenIndex++, IsChip: false, Text: text.Substring(last, m.Index - last));
        var id = int.Parse(m.Groups[2].Value);
        yield return new StepToken(tokenIndex++, IsChip: true, Text: m.Groups[1].Value, IngredientId: id);
        last = m.Index + m.Length;
    }
    if (last < text.Length)
        yield return new StepToken(tokenIndex++, IsChip: false, Text: text.Substring(last));
}
```

**JS-interop probe pattern** (per RESEARCH.md Code Examples §JS-interop fail detection, lines 657-674):
```csharp
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
**This is the load-bearing fallback contract for D-D4.** Same shape as the existing `CookingMode.razor:234-236` pattern, generalized to a probe-once-render-once flag.

**Cooking-mode `OnIngredientChipClick` pattern** (per RESEARCH.md Item 9):
```csharp
[Parameter] public EventCallback<int> OnIngredientChipClick { get; set; }

private async Task ChipClicked(int ingredientId)
{
    if (!Interactive && OnIngredientChipClick.HasDelegate)
    {
        try
        {
            await JS.InvokeAsync<bool>("RecipeChipComposer.scrollIntoViewWithHighlight", $"ingredient-{ingredientId}");
        }
        catch (JSDisconnectedException) { /* graceful per Pitfall 4 */ }
        await OnIngredientChipClick.InvokeAsync(ingredientId);
    }
}
```

**Parameter shape:**
```csharp
[Parameter] public bool Interactive { get; set; } = true;
[Parameter] public string Text { get; set; } = string.Empty;
[Parameter] public EventCallback<string> TextChanged { get; set; }  // for @bind-Text two-way
[Parameter] public IReadOnlyList<ParsedIngredient> Ingredients { get; set; } = Array.Empty<ParsedIngredient>();
[Parameter] public EventCallback<int> OnIngredientChipClick { get; set; }
```

**What to copy / adapt:**
- Copy: `Color.Warning`/`Color.Info` chip palette, `Icons.Material.Filled.Timer`/`.Egg` icons, `Immediate="true" DebounceInterval="500"` cadence.
- Copy: `JSDisconnectedException` swallow pattern from `CookingMode.razor:234-236`.
- Adapt (novel — no codebase analog): per-token segmented `<span contenteditable="plaintext-only">` + `<IngredientChip>` flow inside a `display: flex; flex-wrap: wrap;` container (Pattern 2 in RESEARCH.md). **No existing component does this in the codebase**; the chip-flow CSS plus `@key` token isolation are de-novo to Phase 3.
- Use shared `IngredientLinkPatterns.Pattern` (new — see Open Question 3) instead of redefining the regex.

---

### `src/CookBot.Web/Components/Pages/RecipeEditor/IngredientChip.razor` (CREATED)

**Analog:** the `MudChip<string>` ingredient-ref chip in `RecipeEditor.razor:161-165`:
```razor
<MudChip T="string" Size="Size.Small" Color="Color.Info" Variant="Variant.Text"
         Icon="@Icons.Material.Filled.Egg">
    @refName
</MudChip>
```

**MudMenu anchor pattern to copy** (`CookbookDetail.razor:30-38`):
```razor
<MudMenu Icon="@Icons.Material.Filled.MoreVert" AnchorOrigin="Origin.BottomRight" TransformOrigin="Origin.TopRight"
         Variant="Variant.Outlined" Size="Size.Small">
    <MudMenuItem Icon="@Icons.Material.Filled.PictureAsPdf" OnClick="DownloadPdfAsync" Disabled="_exportBusy">
        Download PDF
    </MudMenuItem>
    <MudMenuItem Icon="@Icons.Material.Filled.DataObject" OnClick="DownloadJsonAsync" Disabled="_exportBusy">
        Download JSON
    </MudMenuItem>
</MudMenu>
```
This is the **only** existing `MudMenu` usage in the codebase. The IngredientChip's replace-popover follows this exact shape:
- `AnchorOrigin="Origin.BottomLeft"` (chip is left-aligned in flow)
- `TransformOrigin="Origin.TopLeft"`
- two `MudMenuItem` entries: "Replace…" (re-runs autocomplete) and "Remove" (deletes the chip)

**Parameter pattern:**
```csharp
[Parameter] public string DisplayName { get; set; } = string.Empty;
[Parameter] public int IngredientId { get; set; }
[Parameter] public bool IsResolved { get; set; } = true;  // false → red error chip per D-A6
[Parameter] public bool Interactive { get; set; } = true;
[Parameter] public EventCallback OnRemove { get; set; }
[Parameter] public EventCallback<int> OnReplace { get; set; }
```

**What to copy / adapt:**
- Copy: `MudChip` color/icon/variant from `RecipeEditor.razor:161-165` (`Color.Info` + `.Egg` icon for resolved; switch to `Color.Error` + `.Error` icon when `IsResolved == false`).
- Copy: `MudMenu` anchor + `MudMenuItem` shape from `CookbookDetail.razor:30-38`.
- Adapt: chip body becomes a clickable `MudChip` (not a `MudIconButton`) that opens the `MudMenu`.

---

### `src/CookBot.Web/Components/Pages/RecipeEditor/TimerChip.razor` (CREATED)

**Analog:** the `MudChip<string>` timer chip in `RecipeEditor.razor:154-157`:
```razor
<MudChip T="string" Size="Size.Small" Color="Color.Warning" Variant="Variant.Filled"
         Icon="@Icons.Material.Filled.Timer">
    @timer.Duration @timer.Unit
</MudChip>
```

**MudDialog or MudPopover for edit popover** — closer analog is `MudMenu` from `CookbookDetail.razor:30-38`. Edit fields:
```razor
<MudNumericField T="int" @bind-Value="Timer.Duration" Label="Duration" Variant="Variant.Outlined" Min="1" />
<MudSelect T="string" @bind-Value="Timer.Unit" Label="Unit" Variant="Variant.Outlined">
    <MudSelectItem Value="@("min")">min</MudSelectItem>
    <MudSelectItem Value="@("sec")">sec</MudSelectItem>
    <MudSelectItem Value="@("hr")">hr</MudSelectItem>
</MudSelect>
<MudTextField T="string" @bind-Value="Timer.Label" Label="Label (optional)" Variant="Variant.Outlined" />
```
Numeric/Select/Text field patterns are well-established in `RecipeEditor.razor:79-87` (numeric/text) and `ShareCookbookDialog.razor:32-38` (select).

**`StepTimer` data flow** — write through to the parent `ParsedStep.Timers` list. Use `EventCallback<StepTimer>` for changes; the parent owns the list.

**What to copy / adapt:**
- Copy: `Color.Warning` + `Icons.Material.Filled.Timer` chip styling.
- Copy: `MudNumericField`/`MudSelect`/`MudTextField` patterns from `RecipeEditor.razor:79-87` and `ShareCookbookDialog.razor:32-38`.
- Adapt: bundle into a `MudMenu`-anchored popover (same as IngredientChip).

---

### `src/CookBot.Web/Components/Pages/RecipeEditor/InlineTimerSuggestion.razor` (CREATED)

**Analog:** **NO direct analog in the codebase** for "click-to-convert" anchored popover on a non-button substring. Closest cousins:
1. `MudMenu` in `CookbookDetail.razor:30-38` — anchor + popover shape.
2. `MudChip` with `OnClick` callback in `RecipeEditor.razor:154-165` — click-handler shape.

**Pattern to follow** (per RESEARCH.md Item 5 + Pattern 2): event delegation at the `chip-flow` container level on `<span class="timer-suggestion" data-duration-seconds="…">…</span>` rendered by the formatter:
```razor
<div @onclick="OnChipFlowClick" class="chip-flow">
    @((MarkupString)html)
</div>
@if (_popoverVisible)
{
    <MudMenu @bind-Open="_popoverVisible" AnchorOrigin="Origin.BottomLeft" ...>
        <MudMenuItem OnClick="ConvertToTimer">Yes, add timer</MudMenuItem>
        <MudMenuItem OnClick="DismissSuggestion">No</MudMenuItem>
    </MudMenu>
}
```
The `OnChipFlowClick` handler reads `e.Target` for `class="timer-suggestion"`, extracts `data-duration-seconds`, opens the popover. **This requires JS interop** (Blazor Server doesn't surface `e.Target.dataset` natively) — see RESEARCH.md Item 5.

**CSS to add** (per RESEARCH.md Item 9 / Item 5):
```css
.timer-suggestion {
    text-decoration: underline dotted;
    text-decoration-color: var(--mud-palette-warning);
    cursor: pointer;
}
```

**What to copy / adapt:**
- Copy: `MudMenu` shape from `CookbookDetail.razor:30-38`.
- Adapt (novel): pre-rendered span wrapping in formatter + click-target detection via `data-*` attribute. **Mark in plan as "novel pattern — see RESEARCH.md Item 5 line 781-803."**

---

### `src/CookBot.Web/Components/Pages/RecipeEditor/SectionDropConfirmationDialog.razor` (CREATED)

**Analog:** `PasteRawTextDialog.razor` (full file) — same `MudDialog` shape, same `[CascadingParameter] IMudDialogInstance MudDialog` pattern, same `DialogResult.Ok(...)` / `MudDialog.Cancel()` flow.

**Code template** (verbatim from RESEARCH.md Code Examples lines 625-653):
```razor
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

**Cascading parameter + close pattern from analog** (`PasteRawTextDialog.razor:30-46`):
```csharp
[CascadingParameter] private IMudDialogInstance MudDialog { get; set; } = null!;
private void Cancel() => MudDialog.Cancel();
// On confirm:
MudDialog.Close(DialogResult.Ok(parsed));
```

**Caller pattern from analog** (`RecipeEditor.razor:386-391`):
```csharp
var options = new DialogOptions { MaxWidth = MaxWidth.Medium, FullWidth = true, CloseOnEscapeKey = true };
var dialog = await DialogService.ShowAsync<SectionDropConfirmationDialog>("Convert?", options);
var result = await dialog.Result;
if (result != null && !result.Canceled) { /* convert */ }
```

**What to copy / adapt:** Direct copy from `PasteRawTextDialog.razor` (file structure, parameter pattern, dialog close idiom).

---

### `src/CookBot.Web/wwwroot/js/recipe-chip-composer.js` (CREATED)

**Analog:** `src/CookBot.Web/wwwroot/js/cooking-timers.js` — the codebase's **canonical JS-interop module shape**.

**Module shape to copy** (`cooking-timers.js:1-77`, full file):
```javascript
// src/CookBot.Web/wwwroot/js/cooking-timers.js
window.CookingTimers = {
    _timers: {},
    _dotNetRef: null,

    init(dotNetRef) {
        this._dotNetRef = dotNetRef;
    },

    start(timerId, durationSeconds, displayLabel) { /* ... */ },
    stop(timerId) { /* ... */ },
    getRemaining(timerId) { /* ... */ },
    async requestNotificationPermission() { /* ... */ },
    _notify(timerId, displayLabel) { /* ... */ },
    dispose() { /* ... */ }
};
```

**App.razor registration pattern to extend** (`App.razor:19-22`):
```razor
<script src="_content/MudBlazor/MudBlazor.min.js"></script>
<script src="_framework/blazor.web.js"></script>
<script src="js/cooking-timers.js"></script>
<script src="js/download.js"></script>
@* Phase 3 add: *@
<script src="js/recipe-chip-composer.js"></script>
```

**C#-side `IJSRuntime.InvokeVoidAsync` pattern** (`CookingMode.razor:307-309, 409, 414`):
```csharp
await JS.InvokeVoidAsync("CookingTimers.init", _dotNetRef);
await JS.InvokeVoidAsync("CookingTimers.start", timerId, durationSeconds, displayLabel);
await JS.InvokeVoidAsync("CookingTimers.stop", timerId);
```
For chip composer, calls become:
```csharp
await JS.InvokeAsync<string>("RecipeChipComposer.ping");
await JS.InvokeAsync<object>("RecipeChipComposer.getCaretCoords", elementId);
await JS.InvokeAsync<bool>("RecipeChipComposer.scrollIntoViewWithHighlight", elementId);
```

**Required exports** (per RESEARCH.md Items 5, 7, 9):
```javascript
window.RecipeChipComposer = {
    ping() { return "ok"; },                                      // D-D4 fail-soft probe
    getCaretCoords(elementId) { /* returns {x, y} */ },           // D-A1 @-trigger anchor
    scrollIntoViewWithHighlight(elementId, cls = 'chip-highlight-pulse', durationMs = 1500) {
        const el = document.getElementById(elementId);
        if (!el) return false;
        el.scrollIntoView({ behavior: 'smooth', block: 'center' });
        el.classList.add(cls);
        setTimeout(() => el.classList.remove(cls), durationMs);
        return true;
    }
};
```

**What to copy / adapt:**
- Copy: `window.<ModuleName> = { ... }` IIFE-style module shape (no ES modules, no `import`/`export`).
- Copy: `_dotNetRef` field if any callbacks are needed (not currently — chip composer interop is one-way C#→JS for D-D3).
- Adapt: methods are pure DOM helpers (caret coords, scroll, ping); no setInterval loop like `cooking-timers.js`.
- Add `<script src>` registration line in `App.razor` after line 22.

---

### `tests/CookBot.Tests/Web/RecipeChipComposerTests.cs` (CREATED — bUnit)

**xUnit cousin (closest non-bUnit analog):** `tests/CookBot.Tests/Services/RecipeStepTextFormatterTests.cs` — same domain (chip rendering input/output), same `[Fact]` shape, same string assertion approach.

**Cousin pattern to mirror** (`RecipeStepTextFormatterTests.cs:7-16`):
```csharp
[Fact]
public void ToHtml_ReplacesIngredientLinksWithSpans()
{
    var input = "Whisk [flour](#1) and [salt](#3).";
    var html = RecipeStepTextFormatter.ToHtml(input);
    Assert.Equal(
        "Whisk <span class=\"ingredient-ref\" data-ingredient-id=\"1\">flour</span> and " +
        "<span class=\"ingredient-ref\" data-ingredient-id=\"3\">salt</span>.",
        html);
}
```

**bUnit-with-MudBlazor scaffold** (per RESEARCH.md Item 4 line 767, paste-ready):
```csharp
using Bunit;
using CookBot.Web.Components.Pages.RecipeEditor;
using CookBot.Domain.Interfaces;
using MudBlazor.Services;

namespace CookBot.Tests.Web;

public class RecipeChipComposerTests
{
    [Fact]
    public void ChipFromAtPath_AndChipFromButtonPath_ProduceSameUnderlyingString()
    {
        using var ctx = new TestContext();
        ctx.Services.AddMudServices();
        // ctx.Services.AddScoped<IJSRuntime>(sp => new FakeJsRuntime());  // returns "ok" on ping
        // ctx.Services.AddScoped<CookBot.Infrastructure.Data.CookBotDbContext>(sp => InMemoryDb());

        var ingredients = new List<ParsedIngredient>
        {
            new() { LocalId = 1, Name = "Salt", Amount = 1, Unit = "tsp" }
        };
        var component = ctx.RenderComponent<RecipeChipComposer>(p => p
            .Add(c => c.Interactive, true)
            .Add(c => c.Text, "")
            .Add(c => c.Ingredients, ingredients));

        // Simulate @-trigger insertion → assert text == "[Salt](#1)"
        // Simulate button-click insertion of same ingredient → assert text == "[Salt](#1)"
        // Test invariant per CONTEXT.md D-A1.
    }
}
```

**csproj package addition** (per RESEARCH.md line 511):
```xml
<PackageReference Include="bunit" Version="1.40.0" />
```
Add inside the existing first `<ItemGroup>` (`tests/CookBot.Tests/CookBot.Tests.csproj:10-16`), alongside `coverlet`, `EFCore.Sqlite`, `Microsoft.NET.Test.Sdk`, `xunit`, `xunit.runner.visualstudio`. Verify net10 compatibility before locking; fallback options in Pitfall 6.

**What to copy / adapt:**
- Copy: file/namespace shape (`namespace CookBot.Tests.Web; public class XxxTests { [Fact] ... }`) from xUnit cousins.
- Copy: `Assert.Equal` / `Assert.Contains` / `Assert.Single` / `Assert.Empty` style from `RecipeStepTextFormatterTests.cs` and `TimerDetectionServiceTests.cs`.
- Adapt: add `using Bunit;`, `using var ctx = new TestContext();`, `ctx.Services.AddMudServices();`, `ctx.RenderComponent<T>(...)` per bUnit docs.
- Adapt: register fakes for `IJSRuntime`, `CookBotDbContext`, `IDialogService`, `ISnackbar` — bUnit's `Services` collection accepts these like the production DI.

---

### `tests/CookBot.Tests/Web/StepSectionToggleTests.cs` (CREATED — bUnit)

**xUnit cousin:** `tests/CookBot.Tests/Services/RecipeStepTextFormatterTests.cs` — same shape, same `[Fact]` idiom.

**Test invariants to encode:**
- `Step → Section` reuses text as heading (D-B2).
- `Section → Step` toggle restores text (no data loss across the round-trip).
- Non-empty `Step → Section` shows confirmation dialog and respects Cancel (Pitfall 5).

**bUnit dialog interaction pattern** — bUnit supports `MudDialogProvider` injection; mock `IDialogService.ShowAsync` to return a configurable `IDialogReference`. Reference shape from `PasteRawTextDialog.razor`-caller pattern in `RecipeEditor.razor:386-391`.

**What to copy / adapt:**
- Copy: bUnit scaffold from `RecipeChipComposerTests.cs` above.
- Adapt: render `<RecipeStepEditor>` (not `<RecipeChipComposer>`); inject a fake `IDialogService` whose `ShowAsync` returns a stubbed `IDialogReference` whose `.Result` task resolves to `DialogResult.Cancel()` or `DialogResult.Ok(true)` per test case.

---

### `tests/CookBot.Tests/Web/TimerSuggestionTests.cs` (CREATED — bUnit)

**xUnit cousin:** `tests/CookBot.Tests/Services/TimerDetectionServiceTests.cs` (full file, lines 1-50) — same input-output assertion idiom on detected durations.

**Cousin pattern to mirror** (`TimerDetectionServiceTests.cs:8-15`):
```csharp
[Fact]
public void DetectTimers_SimpleMinutes()
{
    var timers = TimerDetectionService.DetectTimers("Bake for 25 minutes until golden.");
    Assert.Single(timers);
    Assert.Equal(25, timers[0].Duration);
    Assert.Equal("min", timers[0].Unit);
}
```

**Test invariants to encode** (per CONTEXT.md D-C1, D-C3):
- Step text "Bake 25 minutes" detects `25 min`; rendered HTML contains `<span class="timer-suggestion">`.
- Click-to-convert adds a chip to the timer strip; the inline span no longer offers conversion (already-converted set excludes it).
- Per-occurrence flow: 4 detected durations all get independent Yes/No (no bulk affordance).

**What to copy / adapt:**
- Copy: assertion idioms from `TimerDetectionServiceTests.cs`.
- Adapt: bUnit `RenderComponent<RecipeStepEditor>(...)` + `cut.Find("span.timer-suggestion").Click()` to drive the popover.

---

### `tests/CookBot.Tests/Web/PasteFlowTests.cs` (CREATED — bUnit)

**xUnit cousin:** `tests/CookBot.Tests/Services/RecipeFormatParserTests.cs` (parse-asserts cousin); the **flow** cousin is the existing dialog test approach used elsewhere — there is none today, so the bUnit scaffold from RESEARCH.md Item 4 applies.

**Test invariants to encode** (per CONTEXT.md D-D1, D-D2):
- `PasteRawTextDialog.Submit` calls `Parser.TryParse`; on success, fires `MudDialog.Close(DialogResult.Ok(parsed))` (no fallback path).
- Validation warnings from parser populate the receiving `RecipeEditor.razor`'s warning banner.
- Phase 2 "Edit and save anyway" handoff uses the same banner UI.

**What to copy / adapt:**
- Copy: bUnit scaffold; fake `IRecipeFormatParser` to return `ParsedRecipe` with predefined warnings.
- Adapt: assert dialog Result via `cut.Instance` after invoking Submit programmatically.

---

### `tests/CookBot.Tests/Application/TimerDetectionServiceRegexTests.cs` (CREATED — pure xUnit)

**Analog (exact match):** `tests/CookBot.Tests/Services/TimerDetectionServiceTests.cs` (full file).

**Pattern to copy verbatim** (whole file):
```csharp
using CookBot.Application.Services;
using CookBot.Domain.Entities;

namespace CookBot.Tests.Services;

public class TimerDetectionServiceTests
{
    [Fact]
    public void DetectTimers_SimpleMinutes()
    {
        var timers = TimerDetectionService.DetectTimers("Bake for 25 minutes until golden.");
        Assert.Single(timers);
        Assert.Equal(25, timers[0].Duration);
        Assert.Equal("min", timers[0].Unit);
    }
    // ... more cases
}
```

**Phase 3 extension cases** (per RESEARCH.md Item 6 lines 863-879):
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

**What to copy / adapt:**
- Copy: file structure verbatim from `TimerDetectionServiceTests.cs`; same namespace pattern (`CookBot.Tests.Services` per directory) — but per the test-file-list this lives under `Application/`; verify final namespace (likely `CookBot.Tests.Application`).
- Adapt: `[InlineData]` + `[Theory]` for the new fractional/range/multi-segment cases; assert duration in seconds via the new conversion helper.

---

## Shared Patterns

These cross-cutting concerns apply to multiple Phase 3 files. Copy from the listed source.

### Pattern S1 — `@rendermode InteractiveServer` and page header

**Source:** `src/CookBot.Web/Components/Pages/RecipeEditor.razor:1-12`
**Apply to:** Modified Razor pages (`RecipeEditor.razor`, `CookingMode.razor`); inherited automatically by sub-components in `RecipeEditor/`.
```razor
@page "..."
@inject CookBot.Infrastructure.Data.CookBotDbContext DbContext
@inject CurrentUserService UserService
@inject NavigationManager Navigation
@inject ISnackbar Snackbar
@inject IDialogService DialogService
@rendermode InteractiveServer
```

### Pattern S2 — `JSDisconnectedException` swallow on JS interop

**Source:** `src/CookBot.Web/Components/Pages/CookingMode.razor:230-237, 487-496`
**Apply to:** Every JS-interop call site introduced by Phase 3 (`RecipeChipComposer.scrollIntoViewWithHighlight`, `getCaretCoords`, `ping`).
```csharp
try
{
    await JS.InvokeVoidAsync("Module.method", args);
}
catch (JSDisconnectedException) { /* graceful degrade */ }
```
**Pitfall 4 enforces this** — never let `JSDisconnectedException` reach `ISnackbar`.

### Pattern S3 — `[CascadingParameter] IMudDialogInstance` dialog wiring

**Source:** `src/CookBot.Web/Components/Pages/PasteRawTextDialog.razor:30-46`, `ShareCookbookDialog.razor:69-103`
**Apply to:** `SectionDropConfirmationDialog.razor`, plus any new dialog Phase 3 introduces.
```csharp
[CascadingParameter] private IMudDialogInstance MudDialog { get; set; } = null!;
private void Cancel() => MudDialog.Cancel();
private void Confirm() => MudDialog.Close(DialogResult.Ok(true));
```

### Pattern S4 — `IDialogService.ShowAsync` caller

**Source:** `src/CookBot.Web/Components/Pages/RecipeEditor.razor:385-396`
**Apply to:** `RecipeStepEditor.razor` (Step→Section confirmation), any other Phase 3 caller.
```csharp
var options = new DialogOptions { MaxWidth = MaxWidth.Medium, FullWidth = true, CloseOnEscapeKey = true };
var dialog = await DialogService.ShowAsync<SectionDropConfirmationDialog>("Convert?", options);
var result = await dialog.Result;
if (result != null && !result.Canceled && result.Data is bool ok && ok)
{
    // proceed
}
```

### Pattern S5 — `MudAlert Severity.Warning` for parse-time / non-blocking issues

**Source:** `src/CookBot.Web/Components/Pages/PasteRawTextDialog.razor:14-19`, `AiChat.razor:126-128`
**Apply to:** Top-of-`RecipeEditor.razor` warning banner for D-D1/D-D2 (parser warnings, validator warnings).
```razor
<MudAlert Severity="Severity.Warning" Class="mt-2" Dense="true">
    @foreach (var warning in _warnings)
    {
        <div>@warning</div>
    }
</MudAlert>
```
**Severity convention:** `Warning` for non-blocking issues (orphan ingredient, paste warnings); `Error` only when `RecipeValidator` returns errors (Pitfall 2 — Save still gates on errors).

### Pattern S6 — `MudChip` color + icon palette

**Source:** `src/CookBot.Web/Components/Pages/RecipeEditor.razor:154,161`
**Apply to:** `IngredientChip.razor`, `TimerChip.razor`, the chip strip in `RecipeStepEditor.razor`, the read-only chips in `RecipeChipComposer.razor` (Interactive=false).
```razor
@* Timer (persisted, explicit) — Color.Warning + Filled *@
<MudChip T="..." Size="Size.Small" Color="Color.Warning" Variant="Variant.Filled"
         Icon="@Icons.Material.Filled.Timer">...</MudChip>

@* Ingredient — Color.Info + Text *@
<MudChip T="..." Size="Size.Small" Color="Color.Info" Variant="Variant.Text"
         Icon="@Icons.Material.Filled.Egg">...</MudChip>

@* Error chip (D-A6 unresolved ref) — Color.Error + Filled *@
<MudChip T="..." Size="Size.Small" Color="Color.Error" Variant="Variant.Filled"
         Icon="@Icons.Material.Filled.Error">...</MudChip>
```

### Pattern S7 — `MudAutocomplete<T>` `SearchFunc`

**Source:** `src/CookBot.Web/Components/Pages/RecipeEditor.razor:72-77, 327-339`
**Apply to:** `RecipeChipComposer.razor` `@`-trigger autocomplete; `IngredientChip.razor` replace-popover. **Translate from `MudAutocomplete<string>` to `MudAutocomplete<Ingredient>` (typed)** — current usage is string-typed.
```razor
@* CURRENT (string-typed) — RecipeEditor.razor:72-77 *@
<MudAutocomplete T="string" Label="Ingredient" Variant="Variant.Outlined"
                 Value="@ing.Name"
                 ValueChanged="@(v => ing.Name = v ?? string.Empty)"
                 SearchFunc="SearchIngredients"
                 CoerceText="true" CoerceValue="false" />
```
```csharp
@* SearchFunc shape — copy verbatim *@
private async Task<IEnumerable<string>> SearchIngredients(string value, CancellationToken ct)
{
    if (string.IsNullOrWhiteSpace(value) || value.Length < 2) return Array.Empty<string>();
    var normalized = value.ToLowerInvariant();
    return await DbContext.Ingredients
        .Where(i => i.NormalizedName.Contains(normalized))
        .OrderBy(i => i.Name)
        .Take(10)
        .Select(i => i.Name)
        .ToListAsync(ct);
}
```
**Adapt for Phase 3:** change `T="string"` → `T="Ingredient"`; change `Select(i => i.Name)` → `Select(i => new ParsedIngredient {...})` or `Select(i => i)`; the `MudAutocomplete<Ingredient>` `ValueChanged` callback then has a `ParsedIngredient` (or `Ingredient`) instance, not a string. Custom `<ItemTemplate>` renders `@context.Name` (RESEARCH.md Item 1 confirms 8.15 supports this).

### Pattern S8 — Constructor injection + `private readonly` field naming

**Source:** `src/CookBot.Application/Services/RecipeService.cs:8-31`
**Apply to:** Any new application/web-layer service in Phase 3 (none expected — all new files are Razor/JS).
```csharp
public class RecipeService
{
    private readonly IRecipeFormatParser _parser;
    private readonly IRepository<Recipe> _recipeRepo;
    // ...

    public RecipeService(IRecipeFormatParser parser, IRepository<Recipe> recipeRepo, ...)
    {
        _parser = parser;
        _recipeRepo = recipeRepo;
    }
}
```

### Pattern S9 — `internal static class IngredientLinkPatterns` (NEW — RESEARCH.md Open Question 3)

**Source:** none yet — Phase 3 lifts the regex from `RecipeStepTextFormatter.cs:12-14` and `RecipeValidator.cs:15-17` (per RESEARCH.md, those are duplicate definitions).
**Apply to:** `RecipeChipComposer.razor` tokenizer, both old call sites.
**Recommended location:** `src/CookBot.Application/Recipes/IngredientLinkPatterns.cs`
```csharp
namespace CookBot.Application.Recipes;

internal static class IngredientLinkPatterns
{
    public static readonly Regex Pattern = new(
        @"\[([^\]]*)\]\(#(\d+)\)",
        RegexOptions.Compiled);
}
```
Then update `RecipeStepTextFormatter.cs` and `RecipeValidator.cs` to consume from there. Small refactor (~20 LOC change in 2 files).
**Open Question 3 is the planner's call.** RESEARCH.md recommends in-scope for Phase 3.

---

## No Analog Found

| File / Concern | Reason | Planner Action |
|---|---|---|
| Per-token segmented inline chip flow inside text (Pattern 2) | No existing component does inline chips embedded in editable text in this codebase. MudBlazor itself has no primitive for this (RESEARCH.md Item 1 line 702-704). | Use Pattern 2 from RESEARCH.md verbatim (lines 715-739). Mark in plan as "novel — no codebase analog." |
| `MudPopover` anchored UI for chip replace + inline timer suggestion | Codebase has only `MudMenu` (one usage in `CookbookDetail.razor:30-38`) — no `MudPopover` direct usage. | Use `MudMenu` shape from `CookbookDetail.razor:30-38` as the closest cousin. RESEARCH.md Item 1 line 708 confirms `MudMenu` is the higher-level API. |
| `MudAutocomplete` programmatic `OpenAsync` (caret-anchored) | No existing usage in codebase. RESEARCH.md Item 3 calls this "novel for this codebase" (line 756). | Follow RESEARCH.md Item 3 contract (lines 744-753). Verify `OpenAsync` API surface against MudBlazor 8.15 during execution per Assumption A2. |
| `contenteditable="plaintext-only"` per-segment text input | No existing usage. AI-08-AUDIT (Phase 2) only locked down assistant markdown rendering; user-typed editor content has no precedent. | Follow Pitfall 7 mitigation: set the attribute, add a `paste` event handler that strips formatting (~5 lines JS) for older Firefox. |
| bUnit scaffold (`using Bunit; using var ctx = new TestContext();`) | bUnit is being added (Pitfall 6 verifies net10 compatibility); no precedent in `tests/`. | Use the scaffold from RESEARCH.md Item 4 line 767 across all four new bUnit test files. Closest non-bUnit cousin: `RecipeStepTextFormatterTests.cs`. |

---

## Metadata

**Analog search scope:**
- `src/CookBot.Web/Components/Pages/` (28 .razor files)
- `src/CookBot.Web/Components/Shared/` (UserGuard.razor)
- `src/CookBot.Web/Components/Layout/`
- `src/CookBot.Web/wwwroot/js/` (cooking-timers.js, download.js)
- `src/CookBot.Application/Services/` (16 services)
- `src/CookBot.Domain/Entities/`, `Domain/Interfaces/`
- `tests/CookBot.Tests/Services/` (existing xUnit cousins)
- Phase 1 / Phase 2 context for cross-phase invariants

**Files scanned:** 12 source files Read in full; 4 grep'd for cross-cutting patterns (`MudPopover`/`MudMenu`/`MudAlert`).
**Pattern extraction date:** 2026-04-26

**Cross-references for the planner:**
- RESEARCH.md §Surface files & line ranges to modify (line 434-528) — pinned line ranges for every modified file.
- RESEARCH.md §Code Examples (line 576-688) — paste-ready snippets for tokenizer, dialog, JS-interop probe.
- CONTEXT.md §Decisions D-A1 through D-D4 — interaction model authority.
- CONVENTIONS.md §Razor / Blazor Conventions — page-header pattern, snackbar usage, `<UserGuard>` wrapping.

---

*PATTERNS.md complete. Planner: each plan in this phase should pin its `read_first` to the relevant analog file + this PATTERNS.md, and reference the section header (e.g. "Pattern Assignments → `RecipeChipComposer.razor`") in plan action items.*
