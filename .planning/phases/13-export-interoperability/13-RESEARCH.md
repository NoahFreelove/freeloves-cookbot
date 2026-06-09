# Phase 13: Export & Interoperability - Research

**Researched:** 2026-06-06
**Domain:** Schema.org Recipe JSON-LD (server-rendered SEO markup) + Cooklang one-way text export, in .NET 10 Blazor Server / Clean-Onion / System.Text.Json
**Confidence:** HIGH (codebase facts VERIFIED by file reads; external specs CITED to official docs)

## Summary

Phase 13 adds two read-only projectors over the canonical `RecipeDocument` v4: a Schema.org `Recipe` JSON-LD block server-rendered into the `<head>` of RecipeView (INTEROP-01/02), and a Cooklang `.cook` text download (INTEROP-03/04). Both are pure `CookBot.Application` functions that take a `RecipeDocument` and return a `string` — they never touch `RecipeService.UpdateAsync` or `CanonicalDocumentJson` (a hard invariant carried from STATE.md). No new NuGet packages are needed or permitted: ISO-8601 duration formatting is a ~10-line hand-rolled function, JSON-LD is built with `System.Text.Json` (already the project's only JSON stack), and Cooklang emission is plain string building.

The single highest-risk finding is architectural, not library-related: **RecipeView is `@rendermode InteractiveServer` and loads its `RecipeDocument` inside `OnAfterRenderAsync` (post-circuit), so `_doc` is null during the prerender pass.** The JSON-LD MUST appear in the initial server-rendered HTML for crawlers (INTEROP-01 is explicitly a SEO/rich-results requirement). The current data-loading pattern cannot satisfy that — the plan must move recipe loading (or at least a minimal projection load) into a prerender-capable path (`OnInitializedAsync` / `OnParametersSetAsync`, no JS interop), or render the JSON-LD via a server-static path. This is the make-or-break design decision for INTEROP-01 and is detailed in Pitfall 1 + Open Question 1.

Second-highest risk is Cooklang fidelity: single-word Cooklang names terminate at *any* punctuation, so the projector must **always** use the braces form `@name{amount%unit}` (never bare `@name`), and `@`/`#`/`~` in free step text must be stripped/sanitized (Cooklang has no documented escape character).

**Primary recommendation:** Build two pure static/stateless Application-layer projectors (`JsonLdRecipeProjector.Project(RecipeDocument, absoluteBaseUri?) → string` and `CooklangRecipeProjector.Project(RecipeDocument) → string`), unit-test both with golden-file (Verify) snapshots, then wire JsonLd into a prerender-safe RecipeView load path and Cooklang into the existing `cookBotDownloadFile` JS-interop download helper. Solve the InteractiveServer/prerender data-load gap first — it gates INTEROP-01.

## Project Constraints (from CLAUDE.md + STATE.md)

These have the authority of locked decisions. Research must not recommend anything that contradicts them.

- **Zero new NuGet packages.** All v1.4 themes hand-rolled on System.Text.Json / EF Core / HttpClient (research consensus, STATE.md). No `Iso8601Duration`, no `Schema.NET`, no Cooklang library.
- **100% System.Text.Json.** No `Newtonsoft.Json`, no `NJsonSchema`, no `Microsoft.Extensions.AI`. [CITED: CLAUDE.md "Things to avoid"]
- **No new `CookBot.Schemas` project.** Projectors are pure POCO logic; they belong in `CookBot.Application` alongside `RecipeFormatParser.cs` / `PromptBuilderService.cs`. [CITED: CLAUDE.md]
- **No auto-scaling of temps/times.** Only `RecipeIngredient.Amount` scales; projectors emit raw values. [CITED: CLAUDE.md]
- **Display-only layers never mutate canonical.** `JsonLdRecipeProjector` / `CooklangRecipeProjector` receive `RecipeDocument` and return a string — never call `RecipeService.UpdateAsync`, never set `CanonicalDocumentJson`. [CITED: STATE.md Hard Invariants — "Display-only layers never mutate canonical"]
- **`aggregateRating` / reviews never fabricated.** No rating system exists; emitting one violates Schema.org policy and is explicitly Out of Scope. [CITED: REQUIREMENTS.md "Out of Scope"]
- **No new public/internet-exposed endpoints.** Trusted-LAN posture preserved; export is local computation / static markup. This nudges the Cooklang download toward the existing JS-interop blob pattern rather than a new minimal-API route. [CITED: REQUIREMENTS.md "Out of Scope"]
- **Trusted-LAN auth posture stays.** No Identity middleware, no public exposure. Authorization stays inside services (`DbContext.UserCanAccessRecipeAsync`). [CITED: STATE.md]
- **Cooklang is one-way export only.** Import is explicitly deferred to v1.5+ (needs an NLP-level parser). [CITED: REQUIREMENTS.md "Future Requirements"] The download affordance must read "Export only (one-way)" (SC3).

## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| INTEROP-01 | Recipe page emits valid Schema.org `Recipe` JSON-LD in server-rendered `<head>`, passing Google Rich Results structural rules | `<HeadContent>` + `MarkupString` pattern (Architecture Pattern 3); **prerender data-load gap is the blocker** (Pitfall 1); `name`+`image` required (Google docs, CITED) |
| INTEROP-02 | JSON-LD has `name`+`image` (required), recommended fields from canonical doc, ISO-8601 durations, `image` omitted when not absolute HTTPS, category/cuisine from tags, no fabricated `aggregateRating` | Field map (Architecture Pattern 1); ISO-8601 formatter (Don't Hand-Roll caveat / Code Example); absolute-URL via `NavigationManager.BaseUri` (Architecture Pattern 4) |
| INTEROP-03 | Single-recipe Cooklang `.cook` export: ingredient refs → `@name{amount%unit}`, cookware → `#items`, timers → `~{n%unit}`, sections → `== Section ==`, doneness/subs/temp as `--` comments | Cooklang grammar (Architecture Pattern 2); always-braces rule (Pitfall 2); `cookBotDownloadFile` JS helper (Don't Hand-Roll / Code Example) |
| INTEROP-04 | Export labeled export-only; `@`/`#`/`~` in step text escaped/sanitized before emission | Cooklang has no escape char → strip/replace (Pitfall 2); label copy "Export only (one-way)" (SC3) |

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| JSON-LD string construction | API/Application (`CookBot.Application`) | — | Pure function over `RecipeDocument`; no I/O, no framework refs. Mirrors `RecipeFormatParser`. |
| Cooklang string construction | API/Application (`CookBot.Application`) | — | Pure function over `RecipeDocument`; no I/O. |
| Absolute base URL resolution | Frontend Server (Blazor host) | — | `NavigationManager.BaseUri` is a per-request host concern; the projector receives the resolved base URI as a parameter so it stays pure. |
| Emitting `<script type=ld+json>` into `<head>` | Frontend Server (SSR) | — | `<HeadContent>` + `<HeadOutlet>` are server-render concerns; must land in initial HTML. |
| Triggering `.cook` file download | Browser/Client (JS interop) | Frontend Server | Reuses existing `window.cookBotDownloadFile` blob+`<a download>` path; server hands base64 bytes over SignalR. |
| Authorization (can user view/export this recipe?) | API/Application (data service) | — | `DbContext.UserCanAccessRecipeAsync` — already enforced in RecipeView load. Projectors assume an authorized doc. |

## Standard Stack

**No new packages.** Everything is already referenced.

### Core (already present)
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| System.Text.Json | net10.0 BCL | Build the JSON-LD string (`JsonSerializer` over an anonymous/dictionary model, or `Utf8JsonWriter`) | Project's only JSON stack; `JsonIgnoreCondition.WhenWritingNull` already used in `JsonRecipeSerializer` to omit absent fields cleanly [VERIFIED: src/CookBot.Application/Recipes/JsonRecipeSerializer.cs] |
| System.Xml (`XmlConvert`) | net10.0 BCL | *Optional* ISO-8601 helper — but see caveat; a hand-rolled formatter is cleaner here | BCL, no package [CITED: learn.microsoft.com XmlConvert] |
| Microsoft.JSInterop (`IJSRuntime`) | net10.0 | Trigger `.cook` download via `cookBotDownloadFile` | Already the project's download mechanism [VERIFIED: src/CookBot.Web/Services/CookbookDownloadHelper.cs] |
| Microsoft.AspNetCore.Components (`HeadContent`, `MarkupString`, `NavigationManager`) | net10.0 | Emit JSON-LD into `<head>`; resolve absolute base URI | Blazor BCL; `<HeadOutlet @rendermode="InteractiveServer">` already wired in App.razor [VERIFIED: src/CookBot.Web/Components/App.razor] |

### Supporting (test-only, already present)
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| Verify.Xunit | 31.12.5 | Golden-file snapshot tests for projector output (the existing `Snapshots/` pattern) | Lock the exact JSON-LD and `.cook` text shape against regression [VERIFIED: tests/CookBot.Tests/CookBot.Tests.csproj] |
| bunit | 1.40.0 | Render RecipeView and assert the `<script type=ld+json>` block is present in markup | Component-level INTEROP-01 structural assertion [VERIFIED: csproj + tests/CookBot.Tests/Web/*] |
| xunit | 2.9.3 | Unit tests for projectors, ISO-8601 formatter edge cases | Per-field assertions [VERIFIED: csproj] |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Hand-rolled `Utf8JsonWriter`/dictionary for JSON-LD | `Schema.NET` NuGet | VIOLATES zero-new-package rule; overkill for one type. Rejected. |
| Hand-rolled ISO-8601 formatter | `Iso8601Duration` / `Iso8601kit` NuGet | VIOLATES zero-new-package rule; the format we need (PT#H#M from minutes) is trivial. Rejected. |
| JS-interop blob download | New minimal-API `GET /recipes/{id}.cook` endpoint | Cleaner URL, but adds a new endpoint (Out-of-Scope nudge) and a second download mechanism. Reuse the existing `cookBotDownloadFile` path. |
| `XmlConvert.ToString(TimeSpan)` for ISO-8601 | Hand-rolled | `XmlConvert.ToString(TimeSpan)` emits seconds/sub-second precision (e.g. `PT30M` is fine but `PT0S` for zero and `PT1H30M0S`-style noise can appear); a hand-rolled formatter gives exact `PT30M`/`PT1H30M` and clean null handling. Hand-roll. |

**Installation:** None. `dotnet add package` is forbidden this phase.

## Package Legitimacy Audit

> Phase 13 installs **zero** external packages. All capabilities use BCL + already-referenced project packages. Slopcheck/registry verification is therefore N/A for new dependencies.

| Package | Registry | Disposition |
|---------|----------|-------------|
| (none) | — | No new packages — zero-new-NuGet invariant (STATE.md). |

**Packages removed due to slopcheck [SLOP] verdict:** none (none proposed)
**Packages flagged as suspicious [SUS]:** none

## Architecture Patterns

### System Architecture Diagram

```
INTEROP-01/02  (JSON-LD, server-rendered)
───────────────────────────────────────────────────────────────
  HTTP GET /recipes/{id}
        │
        ▼
  RecipeView (Blazor, prerender pass)         ◄── PROBLEM: today _doc loads in
        │   load RecipeDocument (must be          OnAfterRenderAsync (post-circuit),
        │   prerender-safe — see Pitfall 1)       so _doc is null at prerender.
        ▼                                         Plan must fix this.
  JsonLdRecipeProjector.Project(doc, baseUri)  ── pure Application fn
        │   (RecipeDocument + absolute base URI from NavigationManager.BaseUri)
        ▼
  string  →  <HeadContent><script type="application/ld+json">
                @((MarkupString)json)
             </script></HeadContent>
        │
        ▼
  <HeadOutlet> renders it into <head> of the INITIAL server HTML  →  crawler reads it

INTEROP-03/04  (Cooklang download, client-triggered)
───────────────────────────────────────────────────────────────
  user clicks "Export as .cook  (Export only · one-way)"
        │
        ▼
  RecipeView handler: already has _doc (RecipeDocument)
        │
        ▼
  CooklangRecipeProjector.Project(doc)  ── pure Application fn
        │   - sections  → "== Heading =="
        │   - ingredients → "@name{amount%unit}" (ALWAYS braces)
        │   - timers    → "~{n%unit}"
        │   - step text → strip [name](#id) to label; sanitize @ # ~
        │   - doneness/subs/temp → "-- comment"
        ▼
  string → UTF-8 bytes → Convert.ToBase64String
        │
        ▼
  JS.InvokeVoidAsync("cookBotDownloadFile", "<stem>.cook", "text/plain", base64)
        │
        ▼
  download.js: Blob + URL.createObjectURL + <a download>.click()  →  file saved
```

### Recommended Project Structure
```
src/CookBot.Application/
└── Recipes/
    ├── JsonLdRecipeProjector.cs        # NEW — pure fn: RecipeDocument(+baseUri) → JSON-LD string
    ├── CooklangRecipeProjector.cs      # NEW — pure fn: RecipeDocument → .cook string
    ├── Iso8601DurationFormatter.cs     # NEW (or private static) — int? minutes → "PT#H#M" | null
    ├── JsonRecipeSerializer.cs         # existing — STJ conventions to mirror
    ├── RecipeUpcasterChain.cs          # existing
    └── IngredientLinkPatterns.cs       # existing — REUSE the [name](#id) regex; do NOT redefine

src/CookBot.Web/
├── Components/Pages/RecipeView.razor   # MODIFY — add <HeadContent> JSON-LD + Cooklang export action
└── Services/CookbookDownloadHelper.cs  # existing pattern to mirror for a TryDownloadCookAsync helper (optional)

tests/CookBot.Tests/
├── Recipes/JsonLdRecipeProjectorTests.cs    # NEW — Verify golden file + per-field unit tests
├── Recipes/CooklangRecipeProjectorTests.cs  # NEW — Verify golden file + sanitization tests
├── Recipes/Iso8601DurationFormatterTests.cs # NEW — edge cases (0, null, 30, 90, 60)
└── Snapshots/                               # Verify .verified.txt baselines land here
```

### Pattern 1: JSON-LD field map (RecipeDocument v4 → Schema.org Recipe)

**What:** Project each canonical field to its Schema.org property, omitting absent fields.
**When to use:** `JsonLdRecipeProjector`.

The **exact** v4 `RecipeDocument` shape (VERIFIED by file read — get these names right):

| Canonical field (`RecipeDocument`) | Schema.org property | Notes |
|---|---|---|
| `Name` (required string) | `name` | Required by Google. |
| `PhotoUrl` (string?) | `image` | **Omit unless it is an absolute HTTPS URL.** Relative/local → omit entirely (SC1, P8). |
| `Description` (string?) | `description` | Omit when null. |
| `Servings` (int, default 1) | `recipeYield` | Emit as string e.g. `"4 servings"` or integer. Required if nutrition emitted (Phase 15). |
| `PrepTimeMinutes` (int?) | `prepTime` | ISO-8601; omit when null/0. |
| `CookTimeMinutes` (int?) | `cookTime` | ISO-8601; omit when null/0. |
| `PrepTimeMinutes + CookTimeMinutes` | `totalTime` | ISO-8601; omit when both null/0. |
| `Tags` (IReadOnlyList<string>) | `recipeCategory` / `recipeCuisine` / `keywords` | **Derived from tags** (STATE.md decision — no new schema fields). Needs a tag-partition heuristic (Open Question 2). |
| `Ingredients[].{Amount, Unit, Name, Note}` | `recipeIngredient` (Text[]) | One human-readable string per ingredient, e.g. `"2 cups flour (sifted)"`. Mirror PDF service line format. |
| `Steps` (ContentStep / SectionStep) | `recipeInstructions` (HowToStep[] / HowToSection[]) | `SectionStep.Heading` → `HowToSection{name, itemListElement:[HowToStep...]}`; `ContentStep.Text` → `HowToStep{text}` with `[name](#id)` links stripped to label via `RecipeStepTextFormatter.ToPlainText`. |
| `Provenance.AuthorName` (string?) | `author` | `{ "@type":"Person", "name": authorName }`. Omit when null. |
| — | `aggregateRating` / `review` | **NEVER emit.** No rating system exists (Out of Scope). |
| — | `datePublished` | Optional; `Recipe` entity has timestamps but `RecipeDocument` does not carry one. Leave out or source from the entity at the call site (discretion). |
| (Phase 15) | `nutrition.calories` | NOT this phase — NUTR-06 wires it in Phase 15. Leave the projector extensible. |

**Heads-up — ROADMAP inaccuracy:** ROADMAP Phase 13 "Depends on" says `Provenance.OriginalAuthor`. **That field does not exist.** `RecipeProvenance` has `SourceUrl`, `AuthorName`, `SourceName` only [VERIFIED: src/CookBot.Domain/Recipes/RecipeProvenance.cs]. Map `author` from `AuthorName`; there is no "original author" field to consume. The RecipeView credit logic already composes "Adapted from {SourceName} by {AuthorName}" from these three fields.

**Required-property guard:** Google requires `name` AND `image`. `name` is always present (required on the record). `image` is conditionally present. Decide explicitly: a Recipe JSON-LD block without `image` is technically *invalid* for rich-results eligibility, but emitting a relative/local `image` is worse (P8 forbids it). SC1 says "`name` and `image` ... are present; ... omitted when relative/local". The reconcilable reading: emit the full block; omit `image` when not absolute-HTTPS; accept that such pages are not rich-results-eligible (correct behavior — you can't claim an image you don't have a public URL for). Confirm with user if they'd rather suppress the entire block when no absolute image exists (Open Question 3).

### Pattern 2: Cooklang emission grammar

**What:** Map canonical structures to Cooklang tokens.
**When to use:** `CooklangRecipeProjector`.

Cooklang grammar (CITED: cooklang.org/docs/spec + spec EBNF):

| Canonical | Cooklang token | Rule |
|---|---|---|
| Section heading (`SectionStep.Heading`) | `== Heading ==` | `==Name==` form; the `=` and trailing `==` are technically optional but `== X ==` is the conventional, readable form requested by SC2. |
| Ingredient reference | `@name{amount%unit}` | **ALWAYS use braces** (Pitfall 2). `@name{amount}` if no unit; `@name{}` if neither. |
| Cookware (`Equipment[]`) | `#item{}` | Recipe-level equipment list → `#item{}` (braces force multi-word safety). Cooklang cookware is inline-in-step; equipment is recipe-level here, so emit as a leading line or `--` note. See Open Question 4. |
| Timer (`TimerEntry`) | `~{n%unit}` | `~{duration%unit}`. `~name{n%unit}` if `Label` present. |
| Doneness cue / substitutions / per-step temperature | `-- comment` | Line comment after the step. `[- ... -]` block form also valid. |
| Recipe metadata (name, servings, prep/cook) | `>> key: value` OR YAML front matter | `>> servings: 4` etc. Front matter (`---` fenced) is the newer spec form; `>>` is widely supported. Pick one; `>>` is simpler to emit (Open Question 5). |

**Multi-word names:** A single-word Cooklang name (no braces) terminates at the first whitespace OR punctuation [CITED: spec EBNF — `word = { text item - white space - punctuation character }-`]. Because ingredient/cookware names routinely contain spaces and punctuation, the projector must ALWAYS brace them. Do not try to detect "single word, safe to bare."

**Step text:** `ContentStep.Text` contains `[name](#id)` ingredient links and free prose. For Cooklang:
1. Strip `[name](#id)` to its label via `RecipeStepTextFormatter.ToPlainText` (REUSE — already strips to `Groups[1]`).
2. The chips themselves are NOT in step text (they're in `Ingredients[]`); SC2 says "ingredient chip refs become `@name{amount%unit}`". The chip→token mapping comes from the `Ingredients[]` collection, not from inline parsing.
3. **Sanitize** any literal `@`, `#`, `~` remaining in the prose (Pitfall 2).

### Pattern 3: Server-rendered JSON-LD via `<HeadContent>` + `MarkupString`

**What:** Emit a raw `<script type="application/ld+json">` into `<head>` without HTML-encoding the JSON.
**When to use:** RecipeView, INTEROP-01.

```razor
@* in RecipeView.razor, inside the @if (_doc != null) block *@
@if (_jsonLd != null)
{
    <HeadContent>
        <script type="application/ld+json">
            @((MarkupString)_jsonLd)
        </script>
    </HeadContent>
}
```

- `<HeadContent>` routes its children to `<HeadOutlet>`, which App.razor already renders in `<head>` [VERIFIED: src/CookBot.Web/Components/App.razor `<HeadOutlet @rendermode="InteractiveServer" />`].
- `(MarkupString)` prevents Razor from HTML-encoding the JSON braces/quotes. **Because the JSON is rendered raw, the projector MUST produce JSON that is safe inside a `<script>` element** — escape `<`, `>`, and `&` (and `</` sequences) per the JSON-LD-in-HTML rule. `System.Text.Json` with the default (HTML-safe) encoder already escapes `<`, `>`, `&` to `<` etc. — so use the **default** encoder, NOT `UnsafeRelaxedJsonEscaping` (which `JsonRecipeSerializer._indented` uses — do not copy that setting here). [CITED: learn.microsoft.com STJ character-encoding]

### Pattern 4: Absolute base URL for the `image` field

**What:** Turn `RecipeDocument.PhotoUrl` into an absolute HTTPS URL, or omit.
**When to use:** `JsonLdRecipeProjector` (passed in) — keep the projector pure.

- `NavigationManager.BaseUri` returns the absolute base (e.g. `https://host/`) [CITED: learn.microsoft.com NavigationManager]. The codebase injects `NavigationManager` into RecipeView already [VERIFIED: RecipeView.razor line 12].
- The projector should receive a `Uri? absoluteBase` (or the already-resolved absolute image URL) as a parameter, so it stays a pure function with no Blazor dependency.
- Decision logic (SC1, P8): if `PhotoUrl` is already an absolute `https://` URL → use it. If it's relative (`/uploads/...`) → combine with `BaseUri` ONLY IF `BaseUri` is itself https (trusted-LAN deployments are often plain http) → otherwise **omit** `image`. If unresolvable to absolute-HTTPS → omit.
- **Trusted-LAN reality:** the server binds `http://localhost:7000` [VERIFIED: CLAUDE.md run note]. Most self-host deployments will NOT have an https base URI, so `image` will frequently be omitted. That is correct per SC1 ("omitted when relative/local"). The JSON-LD is still emitted; it's just not rich-results-eligible without a public https image. This is the honest, spec-correct behavior.

### Anti-Patterns to Avoid
- **Parsing step text for ingredients in the Cooklang projector.** Ingredients come from `Ingredients[]`; step text only needs label-stripping + sanitization.
- **Using `UnsafeRelaxedJsonEscaping` for the JSON-LD.** It leaves `<`/`>` unescaped → `</script>` injection / broken markup. Use the default HTML-safe encoder.
- **Bare `@name` Cooklang tokens.** Names with spaces/punctuation break parsing. Always brace.
- **Loading the recipe in `OnAfterRenderAsync` and expecting JSON-LD in prerendered HTML.** It won't be there (Pitfall 1).
- **Fabricating `aggregateRating`, `review`, or `datePublished` you don't have.** Policy violation + Out of Scope.
- **Mutating `_doc` or calling RecipeService from a projector.** Hard invariant.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| `[name](#id)` link stripping | A new regex | `RecipeStepTextFormatter.ToPlainText` + `IngredientLinkPatterns.Pattern` | Single source of truth; CLAUDE.md/Phase-1 D-13 forbids redefining the regex [VERIFIED: src/CookBot.Application/Recipes/IngredientLinkPatterns.cs] |
| Triggering a browser download | A new JS function or minimal-API route | `window.cookBotDownloadFile(name, mime, base64)` | Already implemented and used by PDF/JSON export [VERIFIED: wwwroot/js/download.js + CookbookDownloadHelper.cs] |
| Filename sanitization for `.cook` | Custom char filter | `CookbookDownloadHelper.SafeFileStem(name)` | Already strips invalid filename chars [VERIFIED: CookbookDownloadHelper.cs] |
| Emitting markup into `<head>` | Custom JS DOM injection | `<HeadContent>` → `<HeadOutlet>` | Built-in Blazor; already wired; works in server render [VERIFIED: App.razor] |
| Building JSON safely | String concatenation | `System.Text.Json` (`JsonSerializer` over a model, default encoder) | Correct escaping for `<script>` context; project's only JSON stack |
| Absolute URL composition | Manual string concat | `new Uri(baseUri, relative)` / `NavigationManager.ToAbsoluteUri` | Handles edge cases; but keep the Uri resolution at the Web layer, pass result into the pure projector |

**Key insight:** ISO-8601 duration formatting (PT#H#M from minutes) IS worth hand-rolling — it's ~10 lines, the BCL `XmlConvert.ToString(TimeSpan)` emits noisy precision (`PT1H30M0S`, `PT0S`), and adding a NuGet package violates the zero-package invariant. This is the one place where "hand-roll" is the right call.

## Common Pitfalls

### Pitfall 1: JSON-LD absent from prerendered HTML (INTEROP-01 BLOCKER)
**What goes wrong:** RecipeView is `@rendermode InteractiveServer` and loads `_doc` inside `OnAfterRenderAsync` (post-circuit, requires JS interop for localStorage) [VERIFIED: RecipeView.razor lines 20, 418-504]. During the prerender HTTP response, `_doc == null`, so a `@if (_doc != null)` JSON-LD block renders nothing. Crawlers see the prerendered HTML (and many do not execute the SignalR-driven interactive render at all), so they get NO structured data. INTEROP-01 fails silently — the page "works" in a browser but rich-results validation against the raw HTML finds nothing.
**Why it happens:** The data load was designed for the interactive phase (it does DB reads + JS-interop localStorage reads + per-user authorization in `OnAfterRenderAsync`). Prerendering runs before the circuit exists, so JS interop throws and the original design deferred everything to post-render.
**How to avoid:** Move the **minimal** recipe-document load needed for JSON-LD into a prerender-safe lifecycle method (`OnParametersSetAsync` / `OnInitializedAsync`) that does ONLY DB work and NO JS interop. The DB read (`DbContext.Recipes...CanonicalDocumentJson` → `RecipeSerializer.Deserialize`) is prerender-safe; only the localStorage unit-mode read must stay in `OnAfterRenderAsync`. Then render `<HeadContent>` from that prerender-loaded doc. Validate by inspecting the raw HTML (`curl http://localhost:7000/recipes/{id}` → grep for `application/ld+json`), NOT the browser-rendered DOM.
**Warning signs:** JSON-LD visible in browser DevTools but absent from `view-source:` / `curl` output. The UAT harness must assert via DOM-from-initial-response, not post-hydration DOM.

### Pitfall 2: Cooklang special-character corruption (INTEROP-04)
**What goes wrong:** A step like "Cook @ 350°F #1 priority, ~5 min" contains literal `@`, `#`, `~`. Emitted verbatim into `.cook`, these become spurious ingredient/cookware/timer tokens, producing a malformed file (P11). Cooklang has **no documented escape character** [CITED: spec EBNF — "No escaping rules documented for @, #, ~"].
**Why it happens:** Free-typed step prose legitimately uses these characters; there's no Cooklang-native way to escape them.
**How to avoid:** Sanitize prose before emission — replace/strip literal `@`/`#`/`~` in step text (e.g. `@`→`at`, `#`→`No.`/remove, `~`→`approx`/remove, or simply drop them). Define the exact replacement table at plan time (SC2 says "sanitized"). Ingredient/cookware/timer tokens are emitted intentionally from structured data, never from prose. Cover with a unit test that round-trips a hostile step string.
**Warning signs:** A `.cook` file with `@350` or `#1` tokens that were never ingredients.

### Pitfall 3: Always-braces ingredient names (INTEROP-03)
**What goes wrong:** Emitting `@all-purpose flour 2 cups` (bare) — Cooklang reads the name as `all-purpose` (terminates at `-` punctuation / space) and treats the rest as prose [CITED: spec EBNF — name terminates at whitespace/punctuation].
**Why it happens:** Tempting to only brace multi-word names; but punctuation also terminates.
**How to avoid:** ALWAYS emit `@name{amount%unit}` braces form, even for single tokens. Same for `#cookware{}` and named timers.
**Warning signs:** Parsed Cooklang has truncated ingredient names.

### Pitfall 4: ISO-8601 format noise / wrong zero handling (INTEROP-02, P9)
**What goes wrong:** `XmlConvert.ToString(TimeSpan.FromMinutes(90))` → `"PT1H30M"` (OK) but `TimeSpan.Zero` → `"PT0S"` and 30 min can serialize with trailing `0S` depending on path. SC1 wants exactly `PT30M` / `PT1H30M`.
**Why it happens:** `XmlConvert` is built for full TimeSpan precision, not minute-granularity recipe durations.
**How to avoid:** Hand-roll: `null`/`<=0` → omit the property entirely; else `h = min/60, m = min%60`; build `"PT" + (h>0? h+"H":"") + (m>0? m+"M":"")`. Test 0, null, 30, 60, 90, 125.
**Warning signs:** `PT0S`, `PT1H0M`, or `PT90M`-style output (90 should be `PT1H30M`).

### Pitfall 5: Wrong JSON encoder leaks `</script>` (INTEROP-01 security/validity)
**What goes wrong:** Using `JavaScriptEncoder.UnsafeRelaxedJsonEscaping` (as `JsonRecipeSerializer._indented` does) leaves `<`/`>` raw. A recipe name like `"Mom's <best> cake"` or any `</script>` in description breaks out of the `<script>` block (markup corruption + potential XSS).
**Why it happens:** Copy-pasting the existing indented serializer options.
**How to avoid:** Use the DEFAULT STJ encoder for JSON-LD (escapes `<`,`>`,`&` to `<` etc.). Test with a recipe name containing `<`, `>`, `&`, `"`.
**Warning signs:** `<` appearing raw in the JSON-LD; Rich Results test parse error.

## Code Examples

### ISO-8601 duration formatter (hand-rolled, omit-when-empty)
```csharp
// Source: derived from ISO-8601 duration spec; BCL XmlConvert avoided for clean PT#H#M output
// CITED: developers.google.com/search/docs/appearance/structured-data/recipe (durations ISO 8601)
public static string? ToIso8601Duration(int? minutes)
{
    if (minutes is null or <= 0) return null;   // omit the property entirely (SC1)
    int h = minutes.Value / 60;
    int m = minutes.Value % 60;
    var sb = new System.Text.StringBuilder("PT");
    if (h > 0) sb.Append(h).Append('H');
    if (m > 0) sb.Append(m).Append('M');
    return sb.ToString();                        // 30 -> "PT30M", 90 -> "PT1H30M", 60 -> "PT1H"
}
```

### JSON-LD with HTML-safe encoder + null omission
```csharp
// Source: System.Text.Json defaults — HTML-safe encoder escapes <,>,& for <script> context
// CITED: learn.microsoft.com/dotnet/standard/serialization/system-text-json/character-encoding
private static readonly JsonSerializerOptions LdOptions = new()
{
    // DEFAULT encoder (NOT UnsafeRelaxedJsonEscaping) — escapes <,>,& to < etc.
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = false,
};
// Build an ordered model (dictionary or anonymous object) with "@context"/"@type" first,
// then serialize. Omit image/author/etc. by leaving them null.
```

### Server-rendered JSON-LD (RecipeView)
```razor
@* Source: VERIFIED App.razor wires <HeadOutlet @rendermode="InteractiveServer"> *@
@if (_jsonLd is not null)
{
    <HeadContent>
        <script type="application/ld+json">@((MarkupString)_jsonLd)</script>
    </HeadContent>
}
```

### Cooklang download (reuse existing JS helper)
```csharp
// Source: VERIFIED CookbookDownloadHelper.cs pattern
var cook = CooklangRecipeProjector.Project(_doc);              // pure Application fn
var bytes = System.Text.Encoding.UTF8.GetBytes(cook);
var stem = CookbookDownloadHelper.SafeFileStem(_doc.Name);
await JS.InvokeVoidAsync("cookBotDownloadFile",
    $"{stem}.cook", "text/plain", Convert.ToBase64String(bytes));
```

### Pure projector signatures (no framework refs, no mutation)
```csharp
namespace CookBot.Application.Recipes;

public static class CooklangRecipeProjector
{
    public static string Project(RecipeDocument doc) { /* ... */ }
}

public static class JsonLdRecipeProjector
{
    // absoluteImageUrl resolved at the Web layer (NavigationManager) and passed in to keep this pure.
    public static string Project(RecipeDocument doc, string? absoluteImageUrl) { /* ... */ }
}
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Microdata / RDFa for recipes | JSON-LD in `<script>` | Google's stated preference for years | Use JSON-LD (CITED: Google Search Central) |
| Cooklang `>> key: value` metadata | YAML front matter (`---` fenced) also supported | Recent spec addition | Either works; `>>` is simpler to emit. Pick one. |
| `recipeInstructions` as plain text | `HowToStep` / `HowToSection` objects preferred | Long-standing | Use HowToStep/HowToSection for section fidelity (matches canonical SectionStep/ContentStep split) |

**Deprecated/outdated:**
- The Cooklang `spec/EBNF.md` is self-labeled "WIP, outdated" — treat the prose spec at cooklang.org/docs/spec as primary; EBNF for structure only.
- `aggregateRating` without a real rating source: actively penalized by Google's structured-data spam policies (manual actions). Never emit.

## Validation Architecture

> Note: `.planning/config.json` has `workflow.nyquist_validation: false`, so the full Nyquist sampling framework is OFF. This section documents the testable seams the user explicitly requested, because both projectors are pure functions with excellent golden-file affordances.

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit 2.9.3 + Verify.Xunit 31.12.5 (golden file) + bunit 1.40.0 (component) [VERIFIED: csproj] |
| Snapshot dir | `tests/CookBot.Tests/Snapshots/` (ModuleInitializer routes Verify there) [VERIFIED: ModuleInitializer.cs] |
| Quick run | `dotnet test tests/CookBot.Tests --filter "FullyQualifiedName~Projector"` |
| Full suite | `dotnet test` (377 tests today; keep all green) |

### Testable Seams (pure functions = ideal)
| Seam | Test type | What to assert |
|------|-----------|----------------|
| `JsonLdRecipeProjector.Project` | Verify golden file | Exact JSON-LD for a fully-populated v4 doc (locks `@context`, `@type`, field order, HowToSection nesting) |
| `JsonLdRecipeProjector.Project` | xUnit unit | `image` omitted when relative/local; present when absolute https (P8); `aggregateRating` NEVER present; durations are `PT30M`/`PT1H30M` (P9); category/cuisine/keywords from tags |
| `JsonLdRecipeProjector.Project` | xUnit unit | Recipe name with `<`,`>`,`&` is `<`-escaped, no raw `</script>` (P-Pitfall 5) |
| `Iso8601DurationFormatter` | xUnit unit | 0→null, null→null, 30→PT30M, 60→PT1H, 90→PT1H30M, 125→PT2H5M |
| `CooklangRecipeProjector.Project` | Verify golden file | Exact `.cook` text for a full v4 doc (sections, braced ingredients, timers, `--` comments) |
| `CooklangRecipeProjector.Project` | xUnit unit | Step text "@350 #1 ~5" sanitized; always-braces names; `== Section ==` for SectionStep; doneness/subs/temp as `--` comments |
| RecipeView JSON-LD presence | bunit | `<script type="application/ld+json">` rendered in component markup for a doc with name+image |
| **JSON-LD in PRERENDER html** | integration / UAT (Playwright `tests/uat-harness/`) | Fetch raw HTTP response (not post-hydration DOM) and assert the script block exists — guards Pitfall 1 |

### Structural validation of JSON-LD
- Parse the emitted string with `JsonDocument.Parse` in tests (proves it's valid JSON).
- Assert required keys (`@context`=="https://schema.org", `@type`=="Recipe", `name`, and `image` when applicable).
- Manual/CI: run against Google Rich Results Test once per phase (external, not automatable in-repo).
- UATAUTO-02 (Phase 16) will add the hands-free Playwright check for "JSON-LD present + structurally valid" — Phase 13 should leave a clean DOM seam (e.g. the script block is queryable) for that harness.

## Runtime State Inventory

> Phase 13 is **additive code only** — two new read-only projectors + RecipeView markup. No rename/refactor/migration. Inventory included for completeness.

| Category | Items Found | Action Required |
|----------|-------------|------------------|
| Stored data | None — projectors are stateless; nothing persisted. Verified: no new EF entity, no migration. | None |
| Live service config | None — no external service, no new endpoint (trusted-LAN, Out-of-Scope guard). | None |
| OS-registered state | None. | None |
| Secrets/env vars | None — no API key, no external call. | None |
| Build artifacts | New JS? No — reuses existing `wwwroot/js/download.js`. New `.cook` golden files land under `tests/.../Snapshots/` (Verify `.verified.txt`) and must be committed. | Commit new snapshot baselines |

## Environment Availability

> Phase 13 has no external runtime dependencies (no network, no new tool, no service). All capabilities are in-process .NET 10 + existing JS. Step skipped per the no-external-dependency condition, with one confirmation:

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| .NET 10 SDK | build/test | ✓ | net10.0 [VERIFIED: csproj TargetFramework] | — |
| Browser JS (Blob/URL.createObjectURL) | Cooklang download | ✓ (existing PDF/JSON export uses it) | — | — |
| Google Rich Results Test | manual JSON-LD validation | external web tool | — | `JsonDocument.Parse` + structural asserts in tests cover structure offline |

**Missing dependencies with no fallback:** none.

## Security Domain

> Trusted-LAN posture; `/gsd:secure-phase` has never run on this project (no SECURITY.md). The relevant security items for Phase 13:

### Applicable ASVS Categories
| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V5 Input/Output Encoding | YES | JSON-LD rendered as raw `MarkupString` → MUST use HTML-safe STJ encoder so recipe content (name/description/steps from AI or paste) cannot break out of `<script>` (Pitfall 5). Cooklang is a downloaded text file, not rendered → lower risk, but sanitize `@#~`. |
| V4 Access Control | YES | Both projectors operate on an already-authorized `_doc`. RecipeView already gates via `DbContext.UserCanAccessRecipeAsync` [VERIFIED: RecipeView.razor]. The prerender-load refactor (Pitfall 1) MUST preserve this authorization check in the new load path. |
| V2 Authentication | no | No new auth surface. |
| V6 Cryptography | no | No crypto. |

### Known Threat Patterns
| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| `</script>` / `<` injection via recipe name/description into JSON-LD block | Tampering / XSS | Default HTML-safe STJ encoder (escapes `<`,`>`,`&`); test with hostile strings |
| Provenance `SourceUrl` reflected as JSON-LD `author.url` / `url` | Tampering | If emitting any URL into JSON-LD, reuse `RecipePhotoUrlValidator`/`UrlValidator` http/https allowlist (already used for the RecipeView provenance link, D-12-08) [VERIFIED: RecipeView.razor uses `UrlValidator.TryValidate`] |
| Authorization bypass via prerender load path | Info disclosure | New prerender-safe load MUST retain `UserCanAccessRecipeAsync` |

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | Prerendering is ON (default for `.AddInteractiveServerRenderMode()` with no `prerender:false`), so the server renders RecipeView once in the initial HTTP response — making `<HeadContent>` JSON-LD reachable by crawlers IF data is loaded prerender-safely. | Pitfall 1 / Pattern 3 | If prerender were disabled, JSON-LD could never be in initial HTML without a different mechanism (a static-SSR sub-component or a minimal-API route). VERIFIED no `prerender:false` in Program.cs/App.razor, so this holds — but the data-load timing still needs the fix. |
| A2 | `>> key: value` Cooklang metadata is acceptable to the user vs YAML front matter. | Pattern 2 | Cosmetic only; both parse. Pick at plan/discuss time. |
| A3 | Recipe-level `Equipment[]` maps acceptably to Cooklang `#item{}` cookware OR `--`/metadata lines. Cooklang cookware is normally inline-in-step; recipe-level equipment has no exact 1:1. | Pattern 2 / Open Q4 | Could produce slightly non-idiomatic `.cook`. Low risk; one-way export. |
| A4 | Tag-partition heuristic for category/cuisine/keywords is acceptable to derive from a flat `Tags[]` (no per-tag type marker exists). | Pattern 1 / Open Q2 | If too lossy, all tags fall to `keywords`. Safe default. |
| A5 | Emitting the JSON-LD block WITHOUT `image` (when no absolute-https URL) is the desired behavior vs suppressing the whole block. | Pattern 1 / Open Q3 | Wrong guess = either invalid-for-rich-results blocks or missing structured data. SC1 wording supports "omit image", so this is the leading reading. |

## Open Questions (RESOLVED)

All five questions were resolved during phase planning + the plan-checker revision (iteration 1). Resolutions are authoritative for the Phase 13 plans.

1. **Prerender data-load refactor scope (was BLOCKER for INTEROP-01).** — RESOLVED via D1 + the "Trusted-LAN: prerender all" decision (this revision). User identity is resolved CLIENT-SIDE (sessionStorage, restored in `MainLayout.OnAfterRenderAsync`); at prerender `CurrentUserId` is the default user, so a per-user `UserCanAccessRecipeAsync` check at prerender is meaningless. The prerender JSON-LD load does a plain EF read by id in a prerender-safe lifecycle method (no JS interop) and emits the block for EVERY recipe — NOT per-user gated. This is an accepted risk under the documented trusted-LAN posture (CLAUDE.md: AuthMode flag reserved for future use), marked in RecipeView with `// TODO(AuthMode): gate prerendered JSON-LD per-user once server-side auth lands`. localStorage/unit-mode stays in `OnAfterRenderAsync`. See Plan 03 threat T-13-05 (accept).

2. **Tag → category/cuisine/keywords partitioning.** — RESOLVED via the Warning-1 allow-list derivation. ALL tags always go into `keywords` (comma-joined). `recipeCuisine` = the first tag case-insensitively matching a curated CUISINE allow-list (Italian, Mexican, Thai, French, Chinese, Indian, Japanese, Greek, Spanish, Korean, Mediterranean, American); `recipeCategory` = the first tag matching a curated COURSE/CATEGORY allow-list (Breakfast, Lunch, Dinner, Dessert, Appetizer, Snack, Side Dish, Main Course, Salad, Soup, Beverage, Bread). Each is omitted when no tag matches — never fabricated. This is deterministic classification-by-lookup, not invention. See Plan 01 Task 2.

3. **Image-absent behavior.** — RESOLVED: emit the JSON-LD block WITHOUT an `image` property when no absolute-HTTPS URL is available (relative/local/http → omitted), per SC1. The block is still emitted; it is simply not rich-results-eligible without a public https image. See Plan 01 (image omitted when `absoluteImageUrl` null) + Plan 03 (Web-layer absolute-HTTPS resolution).

4. **Recipe-level Equipment in Cooklang.** — RESOLVED: emit recipe-level `Equipment[]` as `>>` metadata (`>> equipment: a, b, c`) or `-- Equipment: {item}` comment lines, NOT as inline `#cookware` (inline `#` is reserved for step-scoped cookware). See Plan 02 Task 1 + must-have truth.

5. **Cooklang metadata form.** — RESOLVED: use `>> key: value` (simpler to emit, widely supported) rather than YAML front matter. See Plan 02 Task 1 (`>> servings:` / `>> prep time:` / `>> cook time:` / `>> source:`).

## Sources

### Primary (HIGH confidence)
- **Codebase (file reads, this session)** — `RecipeDocument.cs`, `IngredientEntry.cs`, `StepNode.cs`, `StepTemperature.cs`, `TimerEntry.cs`, `IngredientSubstitution.cs`, `RecipeProvenance.cs`, `RecipeView.razor`, `App.razor`, `Program.cs`, `JsonRecipeSerializer.cs`, `RecipeStepTextFormatter.cs`, `IngredientLinkPatterns.cs`, `CookbookDownloadHelper.cs`, `download.js`, `CookbookTransferService.cs`, `CookbookPdfService.cs`, `CookBot.Tests.csproj`, `ModuleInitializer.cs`, `PromptSnapshotTests.cs`, `.planning/config.json`, STATE.md, ROADMAP.md, REQUIREMENTS.md, CONCERNS.md, CLAUDE.md.
- **Google Search Central — Recipe structured data** — https://developers.google.com/search/docs/appearance/structured-data/recipe (required: name, image; recommended fields; ISO-8601 durations; HowToStep/HowToSection; image must be crawlable/absolute).
- **Cooklang Specification** — https://cooklang.org/docs/spec/ (ingredient/cookware/timer/section/comment/metadata syntax; braces rules; fractions).
- **schema.org/Recipe** — https://schema.org/Recipe (canonical property definitions).

### Secondary (MEDIUM confidence)
- **Cooklang spec EBNF** — https://github.com/cooklang/spec/blob/main/EBNF.md (grammar productions; self-labeled WIP/outdated — used for structure/word-termination only).
- **Google Search Central — General structured data guidelines** — https://developers.google.com/search/docs/appearance/structured-data/sd-policies (no fabricated/misleading markup).

### Tertiary (LOW confidence — verified against primary before use)
- WebSearch results on C# ISO-8601 formatting (confirmed `XmlConvert` lacks clean format-direction support → hand-roll). Third-party libs (`Iso8601Duration`, `Iso8601kit`) noted but rejected per zero-package rule.

## Metadata

**Confidence breakdown:**
- Standard stack / no-new-packages: HIGH — VERIFIED against csproj + CLAUDE.md + STATE.md invariants.
- RecipeDocument v4 field map: HIGH — VERIFIED by direct file reads (note: ROADMAP's `Provenance.OriginalAuthor` is wrong; real fields are AuthorName/SourceName/SourceUrl).
- Schema.org / Google Rich Results requirements: HIGH — CITED to official Google + schema.org docs.
- Cooklang grammar: HIGH for documented tokens (CITED official spec); MEDIUM on escaping (spec confirms NO escape char → sanitize, not escape).
- Prerender/InteractiveServer JSON-LD blocker: HIGH on the problem (VERIFIED render mode + load timing); MEDIUM on the recommended fix scope (Open Question 1).
- ISO-8601 hand-roll decision: HIGH.

**Research date:** 2026-06-06
**Valid until:** 2026-07-06 (stable — BCL + project conventions; external specs evolve slowly. Re-check Google Recipe doc requirements if rich-results validation fails.)
