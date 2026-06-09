---
phase: 13-export-interoperability
reviewed: 2026-06-06T22:20:02Z
depth: standard
files_reviewed: 10
files_reviewed_list:
  - src/CookBot.Application/Recipes/CooklangRecipeProjector.cs
  - src/CookBot.Application/Recipes/Iso8601DurationFormatter.cs
  - src/CookBot.Application/Recipes/JsonLdRecipeProjector.cs
  - src/CookBot.Web/Components/Atoms/CbButton.razor
  - src/CookBot.Web/Components/Pages/RecipeView.razor
  - tests/CookBot.Tests/Recipes/CooklangRecipeProjectorTests.cs
  - tests/CookBot.Tests/Recipes/Iso8601DurationFormatterTests.cs
  - tests/CookBot.Tests/Recipes/JsonLdRecipeProjectorTests.cs
  - tests/uat-harness/run.mjs
  - tests/uat-harness/tests/test-jsonld-prerender.mjs
findings:
  critical: 0
  blocker: 0
  warning: 6
  info: 5
  total: 11
status: issues_found
---

# Phase 13: Code Review Report

**Reviewed:** 2026-06-06T22:20:02Z
**Depth:** standard
**Files Reviewed:** 10
**Status:** issues_found

## Summary

Phase 13 adds two read-only export projectors (Schema.org JSON-LD + Cooklang `.cook`), a
prerender refactor in `RecipeView.razor`, a `CaptureUnmatchedValues` splat on `CbButton`, and a
UAT prerender assertion. The JSON-LD projector is the strongest piece of the slice: the
HTML-safe default encoder is correctly used (verified via the `</script>` breakout test and the
`°`/`<` snapshot output), `aggregateRating`/`review`/`datePublished` are reliably
never emitted, `image` is correctly gated to absolute-HTTPS, and the ISO-8601 edge cases
(null/zero/negative/>24h) are covered. Both projectors are genuinely pure (no DI, no IO, no
`RecipeService`/`UpdateAsync`/`CanonicalDocumentJson`), satisfying SC4. All 30 projector tests pass.

The headline concern is **INTEROP-04 sanitization is materially incomplete**. The Cooklang
projector strips only `@ # ~` from *step prose*, but the `.cook` grammar is also driven by
line-leading `--`, `>>`, `==` and by the `{ } %` token-delimiters. None of those are stripped,
and — critically — sanitization is applied to *prose only*, never to ingredient names, units,
timer labels, section headings, or equipment items. As a result hostile or merely unusual
recipe content (a `}` in an ingredient name, a newline in a step, a `==` inside prose) can
corrupt the emitted grammar. The phase's own threat model (T-13-03/T-13-04) claims "special
chars cannot leak" / "names cannot truncate", but the implementation only defends the narrow
`@#~`-in-prose lane. Because export is explicitly one-way (no re-import), these are quality/
correctness defects rather than security vulnerabilities, hence WARNING not BLOCKER — but they
directly contradict the stated INTEROP-04 success criterion and are entirely untested.

The trusted-LAN no-per-user-gate on prerendered JSON-LD is documented and user-approved
(`TODO(AuthMode)`); per the review brief it is NOT flagged as a vulnerability.

## Warnings

### WR-01: Cooklang structural tokens (`--`, `>>`, `==`) and newlines leak from step prose

**File:** `src/CookBot.Application/Recipes/CooklangRecipeProjector.cs:71,153-157`
**Issue:** `Sanitize()` removes only `@`, `#`, `~`. The Cooklang grammar that this projector
itself emits is *also* defined by line-leading `--` (comments), `>>` (metadata), `==` (section
headings), and by embedded newlines (each `\n` starts a new logical line). `ContentStep.Text`
is free-form and may contain any of these. `RecipeStepTextFormatter.ToPlainText` preserves
`\n` (it only normalizes `\r\n`/`\r` → `\n`), so a multi-line step such as
`"Whisk eggs.\n-- secret: add salt"` is appended verbatim followed by a single `\n`, producing:
```
Whisk eggs.
-- secret: add salt
```
The injected `-- secret: add salt` is now an attacker-/author-controlled comment line, and any
genuine per-step `-- {temp}` / `-- {doneness}` comment that follows attaches to the *wrong*
logical step. Prose containing `== Dessert ==` injects a spurious section heading. This
directly violates the INTEROP-04 SC ("literal special chars never corrupt the `.cook` grammar")
and the T-13-03 threat-model claim. No test exercises newlines or `-- / >> / ==` in prose.
**Fix:** Sanitize line-structural tokens and collapse newlines before emission, e.g.:
```csharp
private static string Sanitize(string prose)
{
    // Collapse embedded newlines first so prose stays a single logical line.
    prose = prose.Replace("\r\n", " ", StringComparison.Ordinal)
                 .Replace('\n', ' ')
                 .Replace('\r', ' ');
    // Strip inline token chars …
    prose = prose.Replace("@", "", StringComparison.Ordinal)
                 .Replace("#", "", StringComparison.Ordinal)
                 .Replace("~", "", StringComparison.Ordinal);
    // Neutralize line-leading structural markers anywhere they appear.
    prose = prose.Replace("--", "-", StringComparison.Ordinal)
                 .Replace(">>", ">", StringComparison.Ordinal)
                 .Replace("==", "=", StringComparison.Ordinal);
    return prose.Trim();
}
```
Add unit tests for newline-in-prose and `-- / >> / ==`-in-prose.

### WR-02: Ingredient name/unit, timer label/unit, section heading, and equipment items are never sanitized — token breakout

**File:** `src/CookBot.Application/Recipes/CooklangRecipeProjector.cs:53,65,80-83,120-127`
**Issue:** Sanitization is applied to step prose only. Every other field is emitted raw:
- Ingredient `Name`/`Unit` → `@{Name}{...%{Unit}}` (line 120-125). A `}` or `%` or newline in
  the name/unit breaks the braces token. E.g. name `cream (8%) cheese` → `@cream (8%) cheese{1%cup}`
  where the literal `%` and `)` corrupt the token; name `foo}bar` → `@foo}bar{1%cup}` closes the
  token at `foo}`. This is exactly the truncation/corruption the always-braces form (T-13-04)
  was supposed to prevent — braces don't help if the *content* contains a brace.
- Timer `Label`/`Unit` → `~{Label}{{Duration}%{Unit}}` (line 80-83). A `}` in the label closes
  the token early; a `%` in unit/label adds a spurious amount delimiter.
- `SectionStep.Heading` → `== {Heading} ==` (line 65). A heading containing `==` or `\n`
  injects extra structure.
- Equipment items → `-- Equipment: {item}` (line 53). A newline in an item injects a new line.
**Fix:** Apply a field-appropriate sanitizer to *all* emitted free-text fields, not just prose.
At minimum strip `{ } % @ # ~` and collapse newlines from ingredient names/units, timer
labels/units, section headings, and equipment items before interpolation. Add tests covering a
`}`/`%`/newline in an ingredient name and timer label.

### WR-03: `Iso8601DurationFormatter` emits invalid `PT` for sub-hour multiples of 60 with zero remainder is fine, but produces no validation for absurd inputs / and the `>24h` path is non-standard-but-accepted — confirm intent

**File:** `src/CookBot.Application/Recipes/Iso8601DurationFormatter.cs:24-30`
**Issue:** For `minutes = 1500` (25h) the formatter emits `PT25H` rather than rolling into days
(`P1DT1H`). ISO-8601 / schema.org consumers (Google Rich Results) accept `PT25H`, so this is
tolerated, but it is undocumented and untested — the doc-comment examples stop at 125. More
importantly there is no guard that the upstream `int` minutes can't be `int.MaxValue`; while not
a crash, a pathological `cookTimeMinutes` would serialize as e.g. `PT35791394H`. Given times
are user/AI-supplied this is a robustness gap. (The `null`/`0`/negative lanes ARE correctly
handled and tested.)
**Fix:** Either document that hours are intentionally not rolled into days (add a `>24h` test
pinning `PT25H`), or roll into days for correctness. Optionally clamp/validate the minute
ceiling at the editor boundary. This is a low-severity robustness/clarity warning, not a crash.

### WR-04: JSON-LD `recipeIngredient` produces a double space when `Unit` is empty

**File:** `src/CookBot.Application/Recipes/JsonLdRecipeProjector.cs:148-153`
**Issue:** `BuildIngredientLine` interpolates `$"{amount} {unit} {name}".Trim()`. When `Unit`
is empty (a valid and common case — e.g. `eggs` with `Amount=4, Unit=""`), the result is
`"4  eggs"` with a doubled interior space (`.Trim()` only strips the ends). This is exported as
the literal `recipeIngredient` value consumers index/display. The Cooklang full-doc fixture has
a unit-less `eggs`, but the JSON-LD fixture does not, so this path is untested.
**Fix:** Build the parts and join on a single space, skipping empties:
```csharp
private static string BuildIngredientLine(IngredientEntry ing)
{
    var parts = new List<string>(3);
    var amt = FractionFormatter.Format(ing.Amount);
    if (ing.Amount > 0) parts.Add(amt);
    if (!string.IsNullOrEmpty(ing.Unit)) parts.Add(ing.Unit);
    parts.Add(ing.Name);
    var line = string.Join(" ", parts);
    if (!string.IsNullOrEmpty(ing.Note)) line += $" ({ing.Note})";
    return line;
}
```
Add a JSON-LD test with a unit-less ingredient asserting no double space.

### WR-05: `CbButton` splat lets a caller silently override `disabled`/`type` — and `class`/`style`/`onclick` collisions are not prevented

**File:** `src/CookBot.Web/Components/Atoms/CbButton.razor:8-13,43`
**Issue:** The `@attributes="AdditionalAttributes"` splat is placed *after* the explicit
`type`, `class`, `style`, and `disabled` attributes but *before* `@onclick`. In Blazor, an
explicit attribute and a splatted attribute of the same name resolve to **whichever appears
last in source order**. Because the splat is rendered after `class`/`style`/`disabled`/`type`,
a caller passing `class="..."`, `style="..."`, `disabled="..."`, or `type="..."` through the
unmatched-values dictionary will **override** the component's computed values (e.g. wiping the
`cb-btn` class, or forcing `disabled` off). The doc-comment claims callers "pass no extra
attributes," but `CaptureUnmatchedValues` is precisely the surface that invites them to. The
review-brief concern ("can the splat let a caller override class/style/onclick unexpectedly?")
is confirmed for `class`/`style`/`disabled`/`type`. `@onclick` is safe (it follows the splat).
**Fix:** Move `@attributes` to the *first* attribute position so explicit component attributes
win on collision:
```razor
<button @attributes="AdditionalAttributes"
        type="@Type"
        class="@ClassAttr"
        style="@StyleAttr"
        disabled="@Disabled"
        @onclick="OnClick">
```
This preserves the intended additive use (title/aria-/data-) while preventing a caller from
clobbering the design-system class or the disabled state. Current callers only pass `title`, so
this is a latent-defect/robustness warning rather than an active bug.

### WR-06: Empty `SectionStep` emits a zero-step `HowToSection`; consecutive sections flush empty sections

**File:** `src/CookBot.Application/Recipes/JsonLdRecipeProjector.cs:165-170,197-214`
**Issue:** `BuildInstructions` opens a new `currentSection` on every `SectionStep` and flushes
the previous one unconditionally. If two `SectionStep`s appear back-to-back (or a `SectionStep`
is the final node with no following `ContentStep`), `FlushSection` still emits a
`HowToSection` whose `itemListElement` is an empty array `[]`. Schema.org tolerates this, but an
empty `HowToSection` is meaningless structured data and some validators warn on it. The
Cooklang projector has the analogous shape (a `== Heading ==` with no body) but there it is
harmless prose. No test covers an empty/trailing section in JSON-LD.
**Fix:** In `FlushSection`, skip emission when `currentSection.Count == 0`:
```csharp
if (currentSection is null) return;
if (currentSection.Count == 0) { currentSection = null; currentSectionName = null; return; }
```
Add a test with a trailing empty section asserting it is omitted.

## Info

### IN-01: JSON-LD `recipeYield` is emitted as a bare integer, not Text/QuantitativeValue

**File:** `src/CookBot.Application/Recipes/JsonLdRecipeProjector.cs:133`
**Issue:** `model["recipeYield"] = doc.Servings;` serializes as `"recipeYield":8` (JSON number).
Schema.org defines `recipeYield` as Text or QuantitativeValue; Google's parser accepts a bare
number, but stricter validators prefer a string like `"8 servings"`. Cosmetic/interop polish.
**Fix:** Emit `doc.Servings.ToString()` or `$"{doc.Servings} servings"`.

### IN-02: `recipeYield` is always emitted even when `Servings <= 0`

**File:** `src/CookBot.Application/Recipes/JsonLdRecipeProjector.cs:133`
**Issue:** Unlike every other field, `recipeYield` is added unconditionally. `RecipeDocument`
defaults `Servings = 1`, but a deserialized doc could carry `0` or a negative, producing
`"recipeYield":0`. Minor — all other absent-when-invalid fields are gated.
**Fix:** `if (doc.Servings > 0) model["recipeYield"] = doc.Servings;` (mirroring the other gates).

### IN-03: `ExportCooklang` produces no user feedback on the no-doc path

**File:** `src/CookBot.Web/Components/Pages/RecipeView.razor:711-719`
**Issue:** `if (_doc is null) return;` silently no-ops. The "Export as .cook" button is only
rendered inside the `_doc != null` branch, so this is currently unreachable in practice, but the
guard is a silent dead-end if the button ever moves. Other download helpers
(`CookbookDownloadHelper`) surface a toast on the failure lane.
**Fix:** Either drop the now-unreachable guard or surface a toast for symmetry with the JSON/PDF
download helpers.

### IN-04: Magic string literals `"converted"` / `"original"` and `"https://"` repeated across RecipeView

**File:** `src/CookBot.Web/Components/Pages/RecipeView.razor:393,438,495,604,632,647`
**Issue:** The unit-mode sentinel (`"converted"`/`"original"`) and the HTTPS scheme prefix are
string literals duplicated at multiple sites; a typo in one branch would silently break the
toggle. Pre-existing pattern (Phase 11/12), surfaced here because the prerender refactor touches
these lines.
**Fix:** Promote to `private const string UnitModeConverted = "converted";` etc., or an enum.

### IN-05: UAT prerender test extracts the script block with naive `indexOf("</script>")` — brittle against future content

**File:** `tests/uat-harness/tests/test-jsonld-prerender.mjs:63-68`
**Issue:** The test locates the JSON-LD payload end with `html.indexOf('</script>', scriptStart)`.
This is correct *today* only because the projector HTML-escapes `</script>` inside the payload
(`<`), so the first literal `</script>` after the opening tag is the real closing tag. The
test thus implicitly depends on the very escaping behavior it is meant to be independent of; if
the encoder ever regressed to `UnsafeRelaxedJsonEscaping`, the extraction would truncate at the
injected `</script>` and could *still pass* assertion (3) (because `"@type"`/`"Recipe"` precede
the breakout), masking the regression. Acceptable for a smoke test, but worth a comment or an
explicit "no raw `</script>` in payload" assertion to harden the seam.
**Fix:** Add `assert(!scriptContent.includes('</script>'))` after extraction so an encoder
regression fails the harness loudly.

---

_Reviewed: 2026-06-06T22:20:02Z_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
