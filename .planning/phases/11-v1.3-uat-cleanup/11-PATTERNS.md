# Phase 11: v1.3 UAT Cleanup & Automated UAT Harness - Pattern Map

**Mapped:** 2026-06-05
**Files analyzed:** 9 modified targets + 1 new converter + 1 new harness tree
**Analogs found:** 9 / 10 (harness has no .NET analog — documented explicitly)

This is a **cleanup + tooling** phase. Three items are edits to existing designed
components (RecipeView, app shell). One is a real display-layer feature (unit
conversion) that — critically — has **most of its plumbing already built**
(`IUnitConverter` / `UnitConversionService` / `UnitParser` / `MeasurementUnit`
/ `UnitSystem` already exist). One is brand-new Node/Playwright test tooling with
no .NET analog.

---

## File Classification

| Target file | Role | Data Flow | Closest Analog | Match Quality |
|-------------|------|-----------|----------------|---------------|
| `src/CookBot.Web/Components/Pages/RecipeView.razor` (CLEANUP-01/02/04) | component (Blazor page) | request-response / render | itself (in-place edit) + `MainLayout.razor` JS-interop | self |
| `src/CookBot.Web/wwwroot/css/cookbot-design.css` (CLEANUP-02/03) | config (stylesheet) | render | existing `@media (max-width:720px)` block at L711-722 | exact (same file) |
| `src/CookBot.Web/Components/Layout/MainLayout.razor` (CLEANUP-03) | component (layout) | render | itself — `.cb-shell` wrapper | self |
| `src/CookBot.Web/Components/Layout/Sidebar.razor` (CLEANUP-03) | component (layout) | render | itself — Profile NavRow | self |
| **NEW** `src/CookBot.Application/Services/RecipeUnitDisplayService.cs` (CLEANUP-04) | service (pure transform) | transform | `UnitConversionService.cs` + `RecipeScalingService.cs` | exact (role + flow) |
| `src/CookBot.Web/Components/Pages/CookingMode.razor` (CLEANUP-04) | component (Blazor page) | render | `RecipeView.FormatQty` | role-match (data shape differs — see note) |
| `src/CookBot.Web/Components/Pages/AiChat.razor` (CLEANUP-04) | component (Blazor page) | render | `RecipeView.FormatQty` | exact (same canonical shape) |
| `src/CookBot.Web/wwwroot/js/cookbot-shell.js` (CLEANUP-04 toggle) | utility (JS) | client-state | accent picker `applyDefaults` L35-56 | exact (localStorage precedent) |
| `src/CookBot.Application/DependencyInjection.cs` (CLEANUP-04) | config (DI) | — | `AddSingleton<IUnitConverter, UnitConversionService>()` L14 | exact |
| **NEW** `tests/uat-harness/**` (UATAUTO-01) | test (E2E harness) | event-driven (browser) | **NONE** — no Node/Playwright analog in repo | no analog |

---

## Pattern Assignments

### CLEANUP-01 — RecipeView inline-fallback missing Edit button

**File:** `src/CookBot.Web/Components/Pages/RecipeView.razor` (component, render)
**Analog:** itself — the fault is in the existing markup.

The four BACKLOG suspects, mapped to the actual code:

**Suspect A — flex clipping (`justify-content:flex-end`).** The inline fallback row,
RecipeView.razor **L43**:
```razor
<div class="recipe-actions-inline-fallback" style="max-width:1080px;margin:0 auto;padding:0 32px;display:flex;justify-content:flex-end;gap:10px;align-items:center;">
    @_topBarActions
</div>
```
At ≤720px, four CbButtons (`Edit / Share / Schedule / Cook this`) sit in a single
non-wrapping flex row with `justify-content:flex-end` and **no `flex-wrap`**. The
**first** child (Edit) is the one pushed off the **left** edge when total intrinsic
width exceeds the container — overflow on a flex-end row clips the leading item, not
the trailing ones. This is the most likely root cause; it matches the BACKLOG note
"clipped left ... if total width exceeds container" and the user screenshot ("rofile"-
style left clipping seen elsewhere). **Fix lever:** add `flex-wrap:wrap` (and/or
`justify-content:flex-start`) so all four wrap instead of the first being clipped — a
CSS-class change pairs naturally with the CLEANUP-02 media-query work below.

**Suspect B — owner/auth conditional on Edit.** RULED OUT by reading the fragment.
The four buttons are built unconditionally in `OnInitialized`, RecipeView.razor **L244-249**:
```razor
_topBarActions = @<text>
    <CbButton Variant="CbButton.CbButtonVariant.Ghost" StartIcon="@Icon.Names.Pencil" OnClick="EditRecipe">Edit</CbButton>
    <CbButton Variant="CbButton.CbButtonVariant.Ghost" StartIcon="@Icon.Names.Share" OnClick="OpenShareDialog">Share</CbButton>
    <CbButton Variant="CbButton.CbButtonVariant.Ghost" StartIcon="@Icon.Names.Clock" OnClick="OpenScheduleDialog">Schedule</CbButton>
    <CbButton Variant="CbButton.CbButtonVariant.Accent" StartIcon="@Icon.Names.Flame" OnClick="StartCooking">Cook this</CbButton>
</text>;
```
No `@if` owner/auth guard wraps the Edit button. Same fragment renders identically in
TopBar.RightSlot (where Edit IS visible per UAT). The fragment is not the cause.

**Suspect C — `@<text>` first-child drop.** RULED OUT for the same reason: the
identical `_topBarActions` fragment renders all four buttons in the TopBar slot
(`TopBar.razor` L48 `<div class="topbar-right-slot">@RightSlot</div>`). If `@<text>`
dropped the first child it would be missing in BOTH locations. It is only missing in
the inline row → the difference is the **container CSS**, confirming Suspect A.

**Suspect D — `display:none` on Pencil pattern.** Search the only responsive block in
`cookbot-design.css` (L717-721): it hides `.topbar-right-slot` below 720px and
`.recipe-actions-inline-fallback` above 720px. No rule targets a Pencil icon or the
Edit button. RULED OUT.

**Conclusion for planner:** root cause is **Suspect A** (flex overflow on a
no-wrap `justify-content:flex-end` row clips the leading Edit button). Fix in the
shared CSS class created for CLEANUP-02, not in the RenderFragment.

---

### CLEANUP-02 — RecipeView responsive layout ≤720px

**File:** `src/CookBot.Web/Components/Pages/RecipeView.razor` + `cookbot-design.css`
**Analog:** the existing 720px media-query block (same file).

CONTEXT mandates: prefer a CSS **class** in `cookbot-design.css` over inline `style=`
so the media query can target it. The inline styles that need to become targetable
classes:

Article wrapper — RecipeView.razor **L47**:
```razor
<article style="max-width:1080px;margin:0 auto;padding:24px 32px 80px;">
```
Hero grid (the `1fr 1fr` that must stack) — RecipeView.razor **L50**:
```razor
<div style="display:grid;grid-template-columns:1fr 1fr;gap:40px;margin-bottom:48px;align-items:end;">
```
Two-column body (`300px 1fr` → single column) — RecipeView.razor **L113**:
```razor
<div style="display:grid;grid-template-columns:300px 1fr;gap:56px;">
```
Step number grid (the `40px 1fr` that per-word-wraps) — RecipeView.razor **L174**:
```razor
<div style="display:grid;grid-template-columns:40px 1fr;gap:16px;padding:20px 0;border-bottom:1px solid var(--line);">
```
The 64px display cap that overflows narrow viewports — `cookbot-design.css` **L266-273**:
```css
.cb-recipe-cap {
  font-family: var(--f-display);
  font-size: 64px;
  line-height: 1.02;
  letter-spacing: -0.03em;
  font-weight: 600;
  text-wrap: balance;
}
```

**Pattern to copy** — the existing 720px block, `cookbot-design.css` **L711-722** (extend it,
do NOT add a second breakpoint per CONTEXT/POLISH-04):
```css
@media (max-width: 720px) {
    .topbar-right-slot { display: none !important; }
}
@media (min-width: 721px) {
    .recipe-actions-inline-fallback { display: none !important; }
}
```
Add `grid-template-columns:1fr` overrides for the hero/body/step grids and a smaller
`.cb-recipe-cap` font inside the existing `@media (max-width:720px)` rule. Give each
inline-styled element a class hook (e.g. `recipe-hero`, `recipe-body-grid`,
`recipe-article`) so the rule can target it.

---

### CLEANUP-03 — Sidebar Profile-row clip + body-bg gap

**Files:** `cookbot-design.css`, `MainLayout.razor`, `Sidebar.razor`
**Analog:** itself — the height-inheritance chain.

The wrapper, `MainLayout.razor` **L26** — note `height:100vh` on the wrapper:
```razor
<div class="cb cb-shell @(_drawerCollapsed ? "is-collapsed" : "")" style="height:100vh;">
```
The grid container, `cookbot-design.css` **L218-223** — `height:100%` (NOT `100vh`):
```css
.cb-shell {
  display: grid;
  grid-template-columns: 232px 1fr;
  height: 100%;
  background: var(--cream);
}
```
The sidebar grid cell, `cookbot-design.css` **L230-237**:
```css
.cb-shell .side {
  background: var(--paper-2);
  border-right: 1px solid var(--line);
  padding: 18px 14px;
  display: flex;
  flex-direction: column;
  gap: 4px;
}
```
The Sidebar `<aside>` carries its OWN explicit `width:232px` but NO height,
`Sidebar.razor` **L15**, and the Profile NavRow that gets clipped is the
flex-spacer-pushed bottom row, `Sidebar.razor` **L33-35**:
```razor
    <div style="flex:1;"></div>

    <NavRow IconName="@Icon.Names.User"   Label="Profile"        Href="/profile"        MatchMode="NavLinkMatch.All" />
```

**Root-cause chain for the planner:** the wrapper is `height:100vh`, but
`.cb-shell { height:100% }` only resolves against a sized parent. The `.side`
flex column stretches to the grid row height; if the grid row does not fill the
viewport (because `height:100%` collapses when the ancestor chain isn't fully
sized), the `--paper-2` sidebar and the `--cream` body both stop short, exposing
the body background and leaving the bottom Profile NavRow clipped/cut. **Fix lever
(per CONTEXT):** correct the height/grid inheritance — make `.cb-shell` resolve to a
real viewport height (`height:100vh` on the rule, or `min-height:100%` with a sized
chain) so the grid row, `.side`, and the main `--cream` column all reach the full
viewport bottom. Do NOT mask with `overflow` hacks. The clip is left-side because
`.side` padding is `18px 14px` and the row is not overflowing horizontally — confirm
the clip is vertical (row pushed below the cut grid row) before touching horizontal
padding.

---

### CLEANUP-04 — Unit-system display conversion (largest item)

**KEY DISCOVERY: the conversion engine already exists.** Do NOT build a new factor
table from scratch — extend/wrap the existing services.

**New file:** `src/CookBot.Application/Services/RecipeUnitDisplayService.cs`
(service, pure transform)
**Analogs:** `UnitConversionService.cs` (weight/volume engine) + `RecipeScalingService.cs`
(static display-formatter shape).

Existing weight/volume converter — `UnitConversionService.cs` **L57-81** (already
handles g↔oz/lb and ml↔fl oz/cup; returns `null` for non-convertible pairs):
```csharp
public double? Convert(double amount, string fromUnit, string toUnit)
{
    var from = UnitParser.TryParse(fromUnit);
    var to = UnitParser.TryParse(toUnit);
    if (!from.HasValue || !to.HasValue) return null;
    if (from.Value == to.Value) return amount;
    if (VolumeToMl.ContainsKey(from.Value) && VolumeToMl.ContainsKey(to.Value)) {
        var ml = amount * VolumeToMl[from.Value];
        return ml / VolumeToMl[to.Value];
    }
    if (WeightToGrams.ContainsKey(from.Value) && WeightToGrams.ContainsKey(to.Value)) {
        var grams = amount * WeightToGrams[from.Value];
        return grams / WeightToGrams[to.Value];
    }
    return null;
}
```
The interface to depend on (Domain), `IUnitConverter.cs`:
```csharp
public interface IUnitConverter
{
    bool CanConvert(string fromUnit, string toUnit);
    double? Convert(double amount, string fromUnit, string toUnit);
    bool IsVolume(string unit);
    bool IsWeight(string unit);
}
```
Unit string parsing + display-name table is ALSO already built — `UnitParser.cs`
**L113-140** (`TryParse` returns `null` for unknown units like "clove"/"to taste";
`ToDisplayString` passes unknowns through unchanged) — exactly the "non-convertible
passthrough" behaviour CONTEXT requires:
```csharp
public static MeasurementUnit? TryParse(string unit) { /* null if unrecognised */ }
public static string ToDisplayString(string unit) { /* canonical name, else input as-is */ }
```
Target unit system source of truth — `UserProfile.cs` **L10** (`UnitSystem` enum
`Imperial | Metric | Canadian`, default `Imperial`):
```csharp
public UnitSystem UnitSystem { get; set; } = UnitSystem.Imperial;
```
How `UnitSystem` is read today (precedent for choosing target units per system) —
`PromptBuilderService.cs` **L125-145** switches on `profile.UnitSystem` (note Canadian
is mixed: cups/tbsp + grams). Mirror this mapping when picking the destination unit.

**Temperature — reuse, do not invent.** `StepTemperature.cs` (record `Value` + enum
`TemperatureUnit { F, C, Gas }`) carries per-step oven temps; lives on
`ContentStep.Temperature` (`StepNode.cs` **L24-25**):
```csharp
[JsonPropertyName("temperature")]
public StepTemperature? Temperature { get; init; }
```
There is NO °C↔°F↔gas converter yet — `UnitConversionService` only does weight/volume.
The new `RecipeUnitDisplayService` (or an extension on it) must add the temperature
conversion table. CONTEXT-specified reference values to unit-test: `200°C = 392°F`
(cook-round to 400°F), `gas mark 6 = 200°C = 400°F`. Do NOT auto-scale temps (CLAUDE.md).

**Static-formatter shape to copy** — `RecipeScalingService.cs` (the existing
display-side static helper RecipeView already calls; same architectural slot):
```csharp
public static class RecipeScalingService
{
    public static string FormatScaledAmount(double amount, int originalServings, int targetServings)
    {
        var scaled = ScaleAmount(amount, originalServings, targetServings);
        return FractionFormatter.Format(scaled);   // <-- reuse FractionFormatter for cooking rounding
    }
}
```
`FractionFormatter.Format` already gives "sensible cooking rounding" — reuse it so the
converter doesn't emit `13.9876 oz`.

**DI registration analog** — `DependencyInjection.cs` **L14**:
```csharp
services.AddSingleton<IUnitConverter, UnitConversionService>();
```
Register the new service the same way (singleton, pure/stateless).

#### Render call sites to thread the converter through (the exact Razor lines)

**RecipeView.razor (canonical `IngredientEntry`) — the analog all three should match.**
Ingredient format helper, **L370-377**:
```csharp
private string FormatQty(IngredientEntry ing)
{
    if (ing.Amount <= 0) return string.IsNullOrWhiteSpace(ing.Unit) ? "—" : ing.Unit;
    var origServings = _doc?.Servings > 0 ? _doc.Servings : 1;
    var formatted = RecipeScalingService.FormatScaledAmount(ing.Amount, origServings, _targetServings);
    var unitDisplay = string.IsNullOrWhiteSpace(ing.Unit) ? "" : $" {UnitParser.ToDisplayString(ing.Unit)}";
    return $"{formatted}{unitDisplay}";
}
```
This is the **single best insertion point**: scaling already runs here; convert the
(amount, unit) pair through the new service BEFORE `FormatScaledAmount`/`ToDisplayString`.
RecipeView renders no step temperature today (steps render via `StripIngredientLinks`
at L177) — adding converted temperature is net-new markup, gated on the toggle.

**AiChat.razor (canonical `IngredientEntry`, same shape) — L1099-1106:**
```csharp
private static string FormatIngredientQuantity(IngredientEntry ing)
{
    if (ing.Amount <= 0 && string.IsNullOrEmpty(ing.Unit)) return "—";
    var amount = ing.Amount % 1 == 0
        ? ((int)ing.Amount).ToString(System.Globalization.CultureInfo.InvariantCulture)
        : ing.Amount.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
    return string.IsNullOrEmpty(ing.Unit) ? amount : $"{amount} {ing.Unit}";
}
```
Same canonical `IngredientEntry` → identical conversion call. Method steps render at
AiChat.razor **L1021-1044** via `OfType<ContentStep>()`, so `ContentStep.Temperature`
is reachable for the temperature display here (it currently is not rendered).

**CookingMode.razor — DATA-SHAPE DIVERGENCE (planner must note).** CookingMode does
NOT read the canonical doc for rendering; it reads **EF entities** —
`_recipe.RecipeIngredients` (`RecipeIngredient.Amount`/`.Unit`) and `RecipeStep`
(an EF entity, **not** the canonical `ContentStep`/`StepTemperature` union). Ingredient
line, **L300-303**:
```razor
<div class="num" style="...">
    @RecipeScalingService.FormatScaledAmount(ri.Amount, recipeBaseServings, _targetServings) @ri.Unit
</div>
```
Load site, **L665-668**:
```razor
_recipe = await DbContext.Recipes
    .Include(r => r.RecipeIngredients).ThenInclude(ri => ri.Ingredient)
    ...
```
The EF `RecipeStep` carries **no temperature field** (comment at L501-502: "EF RecipeStep
has no Heading field ... it's an EF entity, not a StepNode union"). **Implication:** to
show converted per-step temperatures in CookingMode, the plan must also deserialize
`Recipe.CanonicalDocumentJson` here (CookingMode already injects `JsonRecipeSerializer`-
adjacent services; `RecipeView` shows the canonical read at L327-336). Flag this as the
one surface where CLEANUP-04 needs an extra canonical read, not just a formatter swap.
Ingredient conversion in CookingMode can stay on the EF `Amount`/`Unit` (same string units).

#### Per-recipe toggle persistence (localStorage, no EF migration)

**Analog:** the QOL-05 accent picker in `cookbot-shell.js` **L35-56** — read-from-
localStorage-before-paint, default when unset:
```javascript
window.cookbot.applyDefaults = function () {
  if (!document.documentElement.hasAttribute("data-accent")) {
    var accent = "orange";
    try {
      var stored = localStorage.getItem("cookbot_accent");
      if (stored === "orange" || stored === "terracotta" || stored === "sage") accent = stored;
    } catch (e) { /* ignore — privacy mode / prerender */ }
    document.documentElement.setAttribute("data-accent", accent);
  }
  ...
};
```
**JS-interop call-site analog (read + write, prerender-safe)** — `MainLayout.razor`
**L84, L91, L99** (read) and **L119, L138-141** (write), each wrapped in
`try { ... } catch (InvalidOperationException) { /* prerender */ }`:
```razor
var darkPref = await JS.InvokeAsync<string?>("localStorage.getItem", "cookbot_dark_mode");
...
await JS.InvokeVoidAsync("localStorage.setItem", "cookbot_drawer_collapsed", _drawerCollapsed.ToString().ToLower());
```
Copy this exact read-in-`OnAfterRenderAsync(firstRender)` / write-on-toggle pattern,
keyed by recipe id (e.g. `cookbot_units_<recipeId>` → `"converted"` | `"original"`,
default `"converted"`). No new `cookbot-shell.js` helper is strictly required — the
inline `localStorage.getItem`/`setItem` interop form (as MainLayout uses) is sufficient;
add a tiny helper to `cookbot-shell.js` only if you want before-paint application.

#### Toggle control styling

**Analog:** `CbButton` (`CbButton.razor` — `Variant` ∈ `{Primary, Accent, Ghost, Subtle}`,
`StartIcon`, `OnClick`). The inline-fallback / TopBar action buttons already use
`CbButtonVariant.Ghost` with `StartIcon` (RecipeView L245-248). Match that for the
units toggle (e.g. a Ghost CbButton labelled "Metric"/"Imperial" with a scale icon).

---

### UATAUTO-01 — Automated browser-UAT harness (NEW tooling, no .NET analog)

**Files:** NEW tree `tests/uat-harness/**` (own `package.json`, gitignored `node_modules`).
**Analog: NONE.** This is the one target with no codebase pattern to copy — the repo is
100% .NET; the only existing browser-test tech is bUnit (component-only, cannot drive a
real circuit, JS interop, localStorage-before-paint, or responsive CSS). Do NOT force a
match. Planner uses RESEARCH.md / Playwright docs for harness structure.

What the harness needs from the app (captured so the planner can script it):

**App launch** — `run.sh`:
```bash
dotnet run --project src/CookBot.Web
```
**Port binding** — `launchSettings.json` → `"applicationUrl": "http://localhost:7000"`;
`Program.cs` L98/L122 confirm `app.Run()` and a `/healthz` endpoint (curl-pollable for
"app ready"). The harness can either spin up `run.sh` itself or expect `:7000` already
running (Claude's discretion per CONTEXT).

**Session establishment (trusted-LAN, no real auth)** — there is NO login form. User
selection is client-side:
- `CurrentUserService.cs` L20 — `public int? CurrentUserId { get; set; }` (circuit-scoped,
  set from sessionStorage).
- `MainLayout.razor` L84-88 restores the user from `sessionStorage["cookbot_current_user"]`;
  if none, auto-creates/defaults to "Home Chef" admin (L57-71).
- The visible user picker is the TopBar `CbDropdown` — `TopBar.razor` L52-58 (each user
  is a `CbDropdownItem ... Label="@u.DisplayName"`); switching writes
  `sessionStorage["cookbot_current_user"]` and `cookbot.hardReloadTo("/")` (TopBar L169, L181).

**Harness auth recipe:** open `http://localhost:7000/`, let the default "Home Chef" user
auto-establish (or set `sessionStorage["cookbot_current_user"]` to a known id and reload
via `cookbot.hardReloadTo`). No password by default (`CurrentUserService.VerifyPasswordAsync`
L88-93 returns `true` when `PasswordHash == null`). Prefer stable `data-` hooks or visible
text (CbDropdownItem labels, button text "Edit"/"Save") over brittle CSS.

**Two open UAT reruns the harness must drive (per CONTEXT/BACKLOG):**
- UAT Test 5 (POLISH-01 reparenting): `/recipes/{id}` → Edit → change cookbook selector →
  Save → assert nav to destination cookbook + recipe gone from origin.
- UAT Test 7 (POLISH-04 responsive): load `/recipes/{id}` at 719px → assert
  `.topbar-right-slot` hidden + `.recipe-actions-inline-fallback` visible + (post-CLEANUP-02)
  layout stacks + (post-CLEANUP-01) Edit present. The CSS hooks to assert on are the exact
  classes in `cookbot-design.css` L717-721.

**UAT Test 4 honesty rule:** the `RawRecipeEditorDialog` only opens on a malformed AI
response (`AiChat.razor` L312 gates the canvas on `_lastStructuredRecipe.Ok == true`).
Cannot be triggered on the happy path → harness exposes a fault-injection seam OR records
Test 4 as manual/deferred. Do NOT fake a pass.

---

## Shared Patterns

### Pure Application-layer service (CLEANUP-04 converter)
**Source:** `src/CookBot.Application/Services/UnitConversionService.cs` (instance, behind
`IUnitConverter`, DI-singleton) and `RecipeScalingService.cs` (static display helper).
**Apply to:** the new `RecipeUnitDisplayService`. Reuse `IUnitConverter.Convert` for
weight/volume; reuse `FractionFormatter.Format` for cooking rounding; reuse
`UnitParser.TryParse`/`ToDisplayString` for unit normalisation + non-convertible
passthrough; add only the °C/°F/gas table (mirroring `StepTemperature` units).

### localStorage client-state (prerender-safe)
**Source:** `MainLayout.razor` L84/L91/L99 (read), L119/L138 (write), each in a
`try/catch (InvalidOperationException)`; `cookbot-shell.js` L35-56 (read-before-paint).
**Apply to:** the per-recipe unit toggle on RecipeView, CookingMode, AiChat.

### 720px responsive breakpoint (single, shared)
**Source:** `cookbot-design.css` L711-722.
**Apply to:** CLEANUP-01 (wrap the fallback row) and CLEANUP-02 (stack the grids).
Do NOT introduce a second breakpoint.

### Canonical-first read (the hard invariant)
**Source:** `RecipeView.razor` L327-336 deserializes `Recipe.CanonicalDocumentJson` via
`JsonRecipeSerializer` and never reads legacy columns.
**Apply to:** CookingMode's CLEANUP-04 temperature read (it currently renders only EF
entities, which lack temperature). Display-only; never mutate canonical.

---

## No Analog Found

| File | Role | Data Flow | Reason |
|------|------|-----------|--------|
| `tests/uat-harness/**` | test (E2E) | event-driven (browser) | Repo is 100% .NET; only existing browser-test tech is bUnit (component-only). No Node/Playwright/Selenium precedent. Planner uses RESEARCH.md + Playwright docs; app-side hooks captured above. |

Partial-only note: a brand-new °C/°F/gas temperature converter has no exact in-repo
analog (`UnitConversionService` covers weight/volume only). Use `StepTemperature`'s unit
enum as the model and the weight/volume converter's `Convert` shape as the API template.

## Metadata

**Analog search scope:** `src/CookBot.Web/Components/{Pages,Layout,Atoms}`,
`src/CookBot.Application/Services`, `src/CookBot.Domain/{Recipes,Entities,Enums,Interfaces}`,
`src/CookBot.Web/wwwroot/{css,js}`, `src/CookBot.Web/{Program.cs,Properties,Services}`,
`run.sh`.
**Files scanned:** ~25 (worktree copies under `.claude/worktrees/` excluded).
**Pattern extraction date:** 2026-06-05
