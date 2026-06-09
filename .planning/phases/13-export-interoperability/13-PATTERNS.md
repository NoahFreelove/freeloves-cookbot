# Phase 13: Export & Interoperability - Pattern Map

**Mapped:** 2026-06-06
**Files analyzed:** 6 (2 new projectors, 1 optional formatter, 1 modified Razor page, 3 new test files)
**Analogs found:** 6 / 6 (all have strong in-repo analogs)

> No CONTEXT.md for this phase (discuss-phase not run). File list extracted from `13-RESEARCH.md` + INTEROP-01..04 requirements.
> All excerpts below are VERIFIED against current source (read this session), not paraphrased from research.

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|-------------------|------|-----------|----------------|---------------|
| `src/CookBot.Application/Recipes/JsonLdRecipeProjector.cs` | utility (pure projector) | transform (`RecipeDocument` → string) | `src/CookBot.Application/Recipes/JsonRecipeSerializer.cs` | exact (same dir, same input type, same STJ stack) |
| `src/CookBot.Application/Recipes/CooklangRecipeProjector.cs` | utility (pure projector) | transform (`RecipeDocument` → string) | `src/CookBot.Web/Services/CookbookPdfService.cs` (line-building) + `JsonRecipeSerializer.cs` (static/pure shape) | role-match (PDF is the existing doc→text projector; lives in Web, but its field-walk + line-format logic is the template) |
| `src/CookBot.Application/Recipes/Iso8601DurationFormatter.cs` *(or private static)* | utility (pure formatter) | transform (`int?` → string?) | `src/CookBot.Application/Services/FractionFormatter.cs` | exact (same shape: `public static class`, single `Format`-style entry, omit/normalize edge cases) |
| `src/CookBot.Web/Components/Pages/RecipeView.razor` | component (Blazor page) | request-response + file-I/O download | itself (existing `_topBarActions`, `OnAfterRenderAsync` load, `UserCanAccessRecipeAsync` auth) | exact (modify in place) |
| `tests/CookBot.Tests/Recipes/JsonLdRecipeProjectorTests.cs` + `CooklangRecipeProjectorTests.cs` | test (Verify golden + xUnit unit) | `tests/CookBot.Tests/Prompts/PromptSnapshotTests.cs` + `ModuleInitializer.cs` | exact (Verify is the established snapshot pattern; routes to `Snapshots/`) |
| `tests/CookBot.Tests/Web/RecipeViewJsonLdTests.cs` *(bunit, optional)* | test (component) | request-response | `tests/CookBot.Tests/Web/StepSectionToggleTests.cs` | exact (bunit `TestContext`, loose JSInterop) |

## Shared Domain Reference (RecipeDocument v4 — VERIFIED field shapes)

Both projectors read these exact records. **Get the names right — research flagged ROADMAP's `Provenance.OriginalAuthor` as nonexistent; it is `AuthorName`.**

`RecipeDocument` (`src/CookBot.Domain/Recipes/RecipeDocument.cs`):
```csharp
public sealed record RecipeDocument
{
    public required int Version { get; init; }
    public required string Name { get; init; }
    public int Servings { get; init; } = 1;
    public int? PrepTimeMinutes { get; init; }
    public int? CookTimeMinutes { get; init; }
    public string? PhotoUrl { get; init; }            // → JSON-LD image (only if absolute-https)
    public string? Description { get; init; }
    public IReadOnlyList<string> Tags { get; init; } = [];
    public IReadOnlyList<IngredientEntry> Ingredients { get; init; } = [];
    public IReadOnlyList<StepNode> Steps { get; init; } = [];   // polymorphic: ContentStep | SectionStep
    public IReadOnlyList<string> Equipment { get; init; } = []; // recipe-level string list (NOT inline)
    public RecipeProvenance? Provenance { get; init; }
    public Dictionary<string, JsonElement> Extras { get; init; } = new();
}
```

`IngredientEntry`: `int Id`, `string Name`, `double Amount`, `string Unit = ""`, `string? Note`, `IReadOnlyList<IngredientSubstitution> Substitutions`.

`StepNode` is **polymorphic** (`[JsonPolymorphic(TypeDiscriminatorPropertyName="kind")]`):
- `ContentStep` : `string Text` (carries `[name](#id)` links), `IReadOnlyList<TimerEntry>? Timers`, `StepTemperature? Temperature`, `string? DonenessCue`.
- `SectionStep` : `string Heading`.
- Walk with `switch`/pattern-match: `foreach (var step in doc.Steps) { if (step is SectionStep s) ... else if (step is ContentStep c) ... }`.

`TimerEntry`: `int Duration`, `string Unit = "min"`, `string? Label`.
`StepTemperature`: `decimal Value`, `TemperatureUnit Unit` (enum `F`/`C`/`Gas`).
`IngredientSubstitution`: `string Note` (required), `string? Name`, `double? Amount`, `string? Unit`.
`RecipeProvenance`: `string? SourceUrl`, `string? AuthorName`, `string? SourceName`. **No `OriginalAuthor`.**

---

## Pattern Assignments

### `src/CookBot.Application/Recipes/JsonLdRecipeProjector.cs` (utility, transform)

**Primary analog:** `src/CookBot.Application/Recipes/JsonRecipeSerializer.cs`
**Reuse (do NOT redefine):** `Iso8601DurationFormatter` (new), `RecipeStepTextFormatter.ToPlainText` for step text.

**File-scoped namespace + STJ-only imports** (mirror JsonRecipeSerializer.cs lines 1-7):
```csharp
using System.Text.Json;
using System.Text.Json.Serialization;
using CookBot.Domain.Recipes;

namespace CookBot.Application.Recipes;
```

**Options block — CRITICAL DIVERGENCE from the analog.** `JsonRecipeSerializer._indented` uses `Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping` (lines 36-37). **DO NOT COPY THAT for JSON-LD** — it leaves `<`/`>`/`&` raw and allows `</script>` breakout (research Pitfall 5 / V5). Use the STJ **default** encoder, which HTML-escapes `<`,`>`,`&`:
```csharp
// Default encoder (NOT UnsafeRelaxedJsonEscaping) — escapes <,>,& so output is safe
// inside a raw <script type="application/ld+json"> (MarkupString) block.
private static readonly JsonSerializerOptions LdOptions = new()
{
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,  // omit absent fields (analog line 28)
    WriteIndented = false,
};
```
The `WhenWritingNull` omission pattern is the established way absent fields drop (verified analog line 28).

**Pure static signature** (research §Code Examples — keep Blazor out; resolved image URL passed in):
```csharp
public static class JsonLdRecipeProjector
{
    // absoluteImageUrl resolved at the Web layer via RecipePhotoUrlValidator + NavigationManager.BaseUri,
    // passed in so this stays a pure, framework-free Application function.
    public static string Project(RecipeDocument doc, string? absoluteImageUrl) { /* build model, serialize with LdOptions */ }
}
```

**Build an ordered model** (`@context`/`@type` first), e.g. a `Dictionary<string, object?>` or anonymous object, serialize with `LdOptions`. Field map (research Pattern 1): `name`←`Name`, `image`←`absoluteImageUrl` (omit when null), `description`←`Description`, `recipeYield`←`Servings`, `prepTime`/`cookTime`/`totalTime`←ISO-8601 of the minute fields, `recipeIngredient[]`←ingredient lines, `recipeInstructions[]`←HowToStep/HowToSection, `author`←`{ "@type":"Person", "name": Provenance.AuthorName }`. **NEVER emit `aggregateRating`/`review`/`datePublished`.**

**Ingredient line format** — mirror the PDF service line exactly (`CookbookPdfService.cs` lines 56-58) for consistency, swapping the DTO field names for `IngredientEntry`:
```csharp
// PDF analog: $"{FractionFormatter.Format(ing.Amount)} {ing.Unit} {ing.Name}".Trim()  + " ({Note})"
var line = $"{FractionFormatter.Format(ing.Amount)} {ing.Unit} {ing.Name}".Trim();
if (!string.IsNullOrEmpty(ing.Note)) line += $" ({ing.Note})";
```

**Step text → plain** (reuse, do NOT redefine the link regex):
```csharp
var plain = RecipeStepTextFormatter.ToPlainText(contentStep.Text);  // strips [name](#id) to label
```

---

### `src/CookBot.Application/Recipes/CooklangRecipeProjector.cs` (utility, transform)

**Primary analog:** `src/CookBot.Web/Services/CookbookPdfService.cs` (the existing `RecipeDocument`-style field-walk + per-line emission) — but emit into a `StringBuilder` in the Application layer instead of QuestPDF.
**Shape analog:** `JsonRecipeSerializer.cs` (file-scoped ns, pure class).
**Reuse:** `RecipeStepTextFormatter.ToPlainText`, `FractionFormatter.Format`.

**Pure static signature:**
```csharp
namespace CookBot.Application.Recipes;

public static class CooklangRecipeProjector
{
    public static string Project(RecipeDocument doc) { /* StringBuilder line building */ }
}
```

**Step-walk pattern** (the PDF service is the template — note it uses the OLD DTO `step.IsSection`/`step.Text`; the canonical polymorphic version uses pattern-matching). PDF analog (`CookbookPdfService.cs` lines 67-87):
```csharp
foreach (var step in recipe.Steps)
{
    if (step.IsSection) { /* heading */ continue; }
    var body = RecipeStepTextFormatter.ToPlainText(step.Text);
    // timers appended inline...
}
```
For the canonical `RecipeDocument` the equivalent is:
```csharp
foreach (var step in doc.Steps)
{
    switch (step)
    {
        case SectionStep s:                              // → "== Heading =="
            sb.Append("== ").Append(s.Heading).Append(" ==").Append('\n');
            break;
        case ContentStep c:                              // step prose + comments
            var body = Sanitize(RecipeStepTextFormatter.ToPlainText(c.Text)); // strip @ # ~ (Pitfall 2)
            // c.Timers → "~{n%unit}", c.Temperature/c.DonenessCue → "-- comment"
            break;
    }
}
```

**Cooklang grammar rules to implement** (research Pattern 2 / Pitfalls 2-3):
- Ingredients: ALWAYS braces — `@name{amount%unit}` (never bare; names contain spaces/punctuation). Use `FractionFormatter.Format(ing.Amount)` for the amount.
- Equipment (recipe-level string list): emit as `-- Equipment: ...` or `>> equipment: ...` (Open Q4), not inline `#`.
- Timers: `~{duration%unit}` (or `~label{...}` when `Label` present).
- Doneness/subs/per-step temp: `-- comment` lines.
- Metadata (name/servings/prep/cook): `>> key: value` (Open Q5, recommended).
- **Sanitize** literal `@`/`#`/`~` out of `ToPlainText` prose before emission (Cooklang has no escape char).

---

### `src/CookBot.Application/Recipes/Iso8601DurationFormatter.cs` (utility, transform)

**Exact analog:** `src/CookBot.Application/Services/FractionFormatter.cs` — same `public static class` + single static entry-point that normalizes edge cases and returns a string. Place this projector's formatter in `Recipes/` (research structure) or inline as a `private static` on `JsonLdRecipeProjector`.

FractionFormatter shape to mirror (lines 1-4, 19-27):
```csharp
namespace CookBot.Application.Services;   // (use CookBot.Application.Recipes for the new one)

public static class FractionFormatter
{
    public static string Format(double value)
    {
        // ... edge-case short-circuits, then build/return ...
    }
}
```

Target implementation (research §Code Examples — hand-rolled, BCL `XmlConvert` rejected for noisy precision):
```csharp
public static string? ToIso8601Duration(int? minutes)
{
    if (minutes is null or <= 0) return null;   // omit the property entirely (SC1)
    int h = minutes.Value / 60, m = minutes.Value % 60;
    var sb = new System.Text.StringBuilder("PT");
    if (h > 0) sb.Append(h).Append('H');
    if (m > 0) sb.Append(m).Append('M');
    return sb.ToString();                        // 30→"PT30M", 90→"PT1H30M", 60→"PT1H"
}
```

---

### `src/CookBot.Web/Components/Pages/RecipeView.razor` (component — MODIFY)

**This is a modify-in-place; the file IS its own analog.** Three edits.

**Edit 1 — Add the "Export as .cook" button to the existing `_topBarActions` RenderFragment** (current shape, `RecipeView.razor` lines 334-345):
```csharp
protected override void OnInitialized()
{
    _topBarActions = @<text>
        <CbButton Variant="CbButton.CbButtonVariant.Ghost" StartIcon="@Icon.Names.Pencil" OnClick="EditRecipe">Edit</CbButton>
        <CbButton Variant="CbButton.CbButtonVariant.Ghost" StartIcon="@Icon.Names.Share" OnClick="OpenShareDialog">Share</CbButton>
        <CbButton Variant="CbButton.CbButtonVariant.Ghost" StartIcon="@Icon.Names.Clock" OnClick="OpenScheduleDialog">Schedule</CbButton>
        <CbButton Variant="CbButton.CbButtonVariant.Accent" StartIcon="@Icon.Names.Flame" OnClick="StartCooking">Cook this</CbButton>
    </text>;
    TopBarService.SetRightSlot(_topBarActions);
}
```
Add a new `<CbButton ... OnClick="ExportCooklang">Export as .cook</CbButton>` here (label must convey "Export only · one-way" per SC3 — put it in a tooltip/title or the button text).

**Edit 2 — `ExportCooklang` handler.** Reuse the EXISTING download path. `JS` is already injected (`@inject IJSRuntime JS`, line 17) and `_doc` is already populated. Mirror `CookbookDownloadHelper` lines 33-38 and `SafeFileStem` lines 11-16:
```csharp
private async Task ExportCooklang()
{
    if (_doc is null) return;
    var cook = CooklangRecipeProjector.Project(_doc);
    var bytes = System.Text.Encoding.UTF8.GetBytes(cook);
    var stem = CookbookDownloadHelper.SafeFileStem(_doc.Name);   // existing filename sanitizer
    await JS.InvokeVoidAsync("cookBotDownloadFile",              // existing wwwroot/js/download.js helper
        $"{stem}.cook", "text/plain", Convert.ToBase64String(bytes));
}
```
`CookbookDownloadHelper.SafeFileStem` (Web/Services, lines 11-16) — REUSE, do not write a new sanitizer:
```csharp
public static string SafeFileStem(string name)
{
    var s = string.IsNullOrWhiteSpace(name) ? "cookbook" : name.Trim();
    foreach (var c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
    return s;
}
```
`cookBotDownloadFile` JS (REUSE verbatim — Blob + `URL.createObjectURL` + `<a download>`, `wwwroot/js/download.js` lines 1-14). No new JS, no new endpoint.

**Edit 3 — Server-rendered JSON-LD in `<head>` (the INTEROP-01 blocker).** App.razor already wires `<HeadOutlet @rendermode="InteractiveServer" />` (line 14), so `<HeadContent>` lands in `<head>`. Add inside the `@if (_doc != null ...)` markup (research Pattern 3):
```razor
@if (_jsonLd is not null)
{
    <HeadContent>
        <script type="application/ld+json">@((MarkupString)_jsonLd)</script>
    </HeadContent>
}
```

**THE GATING FIX — current load is NOT prerender-safe.** `_doc` loads in `OnAfterRenderAsync` (post-circuit), so it is null during prerender and JSON-LD would be absent from initial HTML (research Pitfall 1). The plan must move the **DB-only** portion of the load into a prerender-safe method (`OnParametersSetAsync`/`OnInitializedAsync`, no JS interop). Current auth + load to preserve verbatim (`RecipeView.razor` lines 427-446):
```csharp
if (!await DbContext.UserCanAccessRecipeAsync(RecipeId, userId))   // MUST stay in the new path (V4)
{
    _recipe = null; _doc = null;
}
else
{
    _recipe = await DbContext.Recipes.Include(r => r.Cookbook)
        .FirstOrDefaultAsync(r => r.Id == RecipeId);
    if (_recipe is { CanonicalDocumentJson: { Length: > 0 } json })
    {
        try { _doc = RecipeSerializer.Deserialize(json); ... }
        catch (JsonException) { _doc = null; }
    }
}
```
Keep the localStorage unit-mode read (lines 494-500, wrapped in `catch (InvalidOperationException) { /* prerender */ }`) in `OnAfterRenderAsync` — only that needs JS interop. The DB read + `UserCanAccessRecipeAsync` + `RecipeSerializer.Deserialize` are prerender-safe and should move so `_doc` (and `_jsonLd`) exist at prerender.

**Image URL safety for JSON-LD** — RecipeView already injects `RecipePhotoUrlValidator` as `UrlValidator` (line 18) and `NavigationManager` as `Navigation` (line 12). Resolve `_doc.PhotoUrl` to an absolute-https URL at the Web layer (validate via `UrlValidator.TryValidate`, combine relative with `Navigation.BaseUri` only when base is https, else null) and pass the result into `JsonLdRecipeProjector.Project(doc, absoluteImageUrl)`. Existing provenance validation already uses this exact call (`RecipeView.razor` lines 455-461):
```csharp
UrlValidator.TryValidate(p12.SourceUrl, out var normalized, out _) && normalized != null
```

---

### Test files (`tests/CookBot.Tests/Recipes/*ProjectorTests.cs`)

**Exact analog:** `tests/CookBot.Tests/Prompts/PromptSnapshotTests.cs` + `ModuleInitializer.cs`.

Verify is already configured project-wide. `ModuleInitializer.cs` routes all snapshots to `tests/CookBot.Tests/Snapshots/` (lines 11-15) and `[UseVerify]` is injected at assembly level — **no class attribute needed** (PromptSnapshotTests.cs comment, lines 6-7). Golden-file test shape (lines 10-18):
```csharp
namespace CookBot.Tests.Recipes;

public class JsonLdRecipeProjectorTests
{
    [Fact]
    public Task FullDocument_ProducesExpectedJsonLd()
    {
        var doc = /* fully-populated v4 RecipeDocument */;
        var actual = JsonLdRecipeProjector.Project(doc, absoluteImageUrl: "https://host/img.jpg");
        return Verifier.Verify(actual);   // baseline lands in Snapshots/*.verified.txt — COMMIT it
    }
}
```
Per-field xUnit unit tests (research Validation table): `image` omitted when null/relative; never `aggregateRating`; durations `PT30M`/`PT1H30M`; name with `<`/`>`/`&` is `<`-escaped (parse with `JsonDocument.Parse` to prove validity); Cooklang sanitizes `@ # ~`, always-braces, `== Section ==`.

**bunit component test (optional)** — analog `tests/CookBot.Tests/Web/StepSectionToggleTests.cs` lines 39-49:
```csharp
var ctx = new Bunit.TestContext();
ctx.JSInterop.Mode = JSRuntimeMode.Loose;   // tolerate the localStorage / download JS calls
// register fakes for injected services (DbContext, CurrentUserService, etc.), render, assert
// the <script type="application/ld+json"> block is present in cut.Markup
```
> NOTE: a bunit render asserts the COMPONENT markup, not the raw prerender HTTP response. The Pitfall-1 guard (JSON-LD present in initial HTML) belongs in the Playwright UAT harness (`tests/uat-harness/`), fetching the raw response — not in bunit.

---

## Shared Patterns

### Pure Application-layer projector (file-scoped ns, no framework refs)
**Source:** `src/CookBot.Application/Recipes/JsonRecipeSerializer.cs`, `src/CookBot.Application/Services/FractionFormatter.cs`
**Apply to:** both new projectors + the ISO-8601 formatter
- File-scoped `namespace CookBot.Application.Recipes;`
- `public sealed class` (instance, like JsonRecipeSerializer) OR `public static class` (like FractionFormatter — preferred for stateless projectors).
- Imports limited to `System.Text.Json*` + `CookBot.Domain.Recipes`. No Blazor, no EF, no HttpClient.

### Reuse, never redefine (CLAUDE.md / Phase-1 D-13)
**Apply to:** both projectors
- `[name](#id)` stripping → `RecipeStepTextFormatter.ToPlainText` (`src/CookBot.Application/Services/RecipeStepTextFormatter.cs` lines 48-55), which delegates to the single-source regex `IngredientLinkPatterns.Pattern` (`...Recipes/IngredientLinkPatterns.cs` lines 11-14). Do NOT write a new regex.
- Amount display → `FractionFormatter.Format` (`...Services/FractionFormatter.cs` line 19).
- Ingredient line format → mirror `CookbookPdfService.cs` lines 56-58.

### File download (no new mechanism)
**Source:** `src/CookBot.Web/Services/CookbookDownloadHelper.cs`, `src/CookBot.Web/wwwroot/js/download.js`
**Apply to:** RecipeView Cooklang export
- `SafeFileStem(name)` for filename, `Convert.ToBase64String(bytes)`, `JS.InvokeVoidAsync("cookBotDownloadFile", "{stem}.cook", "text/plain", base64)`.
- Existing call sites for reference: `ShareCookbookDialog.razor` lines 163/178, `CookbookDetail.razor` lines 234/249.

### Authorization stays in the load path (V4 access control)
**Source:** `RecipeView.razor` line 427 — `DbContext.UserCanAccessRecipeAsync(RecipeId, userId)`
**Apply to:** the prerender-safe load refactor — the new path MUST retain this guard before deserializing/projecting (research Security V4 / Pitfall 1).

### Output encoding for raw markup (V5)
**Source:** STJ default encoder (the absence of `UnsafeRelaxedJsonEscaping` that `JsonRecipeSerializer._indented` line 36 sets).
**Apply to:** `JsonLdRecipeProjector` only — JSON-LD renders as raw `MarkupString`, so it MUST use the HTML-safe default encoder to prevent `</script>` breakout.

## No Analog Found

None. Every new file has a strong in-repo analog. Two design seams have NO exact 1:1 (handle in plan/discuss, not via a missing-analog fallback):
- **Cooklang recipe-level `Equipment[]`** — Cooklang cookware is inline-in-step; recipe-level string list has no idiomatic token (research Open Q4 → emit as `--`/`>>` line).
- **Tag → `recipeCategory`/`recipeCuisine`/`keywords` partition** — `Tags[]` is flat with no type marker (research Open Q2 → keyword heuristic or all-to-`keywords`).

## Metadata

**Analog search scope:** `src/CookBot.Domain/Recipes/`, `src/CookBot.Application/Recipes/`, `src/CookBot.Application/Services/`, `src/CookBot.Web/Services/`, `src/CookBot.Web/Components/Pages/`, `src/CookBot.Web/wwwroot/js/`, `tests/CookBot.Tests/{Prompts,Web,Recipes}/`
**Files read & verified:** RecipeDocument.cs, IngredientEntry.cs, StepNode.cs, StepTemperature.cs, TimerEntry.cs, IngredientSubstitution.cs, RecipeProvenance.cs, JsonRecipeSerializer.cs, IngredientLinkPatterns.cs, RecipeStepTextFormatter.cs, FractionFormatter.cs, RecipePhotoUrlValidator.cs, CookbookDownloadHelper.cs, CookbookPdfService.cs, download.js, App.razor (HeadOutlet), RecipeView.razor (topbar/load/auth seams), PromptSnapshotTests.cs, ModuleInitializer.cs, StepSectionToggleTests.cs, CookBot.Tests.csproj
**Skills dirs:** none (`.claude/skills/`, `.agents/skills/` absent)
**Pattern extraction date:** 2026-06-06
