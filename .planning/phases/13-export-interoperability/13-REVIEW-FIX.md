---
phase: 13-export-interoperability
fixed_at: 2026-06-06T22:45:00Z
review_path: .planning/phases/13-export-interoperability/13-REVIEW.md
iteration: 1
findings_in_scope: 6
fixed: 6
skipped: 0
status: all_fixed
---

# Phase 13: Code Review Fix Report

**Fixed at:** 2026-06-06T22:45:00Z
**Source review:** .planning/phases/13-export-interoperability/13-REVIEW.md
**Iteration:** 1

**Summary:**
- Findings in scope: 6 (WR-01 through WR-06; no Critical/Blocker; Info findings excluded per scope)
- Fixed: 6
- Skipped: 0

## Fixed Issues

### WR-01 + WR-02: Grammar-complete Cooklang sanitization (INTEROP-04)

**Files modified:** `src/CookBot.Application/Recipes/CooklangRecipeProjector.cs`, `tests/CookBot.Tests/Recipes/CooklangRecipeProjectorTests.cs`
**Commit:** `19a8883`
**Applied fix:**

Expanded `Sanitize()` (for step prose) to cover the full Cooklang grammar:
- Collapse embedded newlines (`\r\n`, `\n`, `\r` → space) so prose stays a single logical line
- Strip token sigils `@ # ~` (unchanged from before)
- Strip brace/percent delimiters `{ } %` (new)
- Neutralize line-leading structural markers `--` → `-`, `>>` → `>`, `==` → `=` (new)

Added `SanitizeToken()` (new method) for all other user-derived fields — ingredient names/units, timer labels/units, section headings, equipment items, substitution notes — stripping `{ } % @ # ~`, collapsing newlines, and neutralizing structural markers.

Applied `SanitizeToken()` at every emission site: equipment items, section headings (`SectionStep.Heading`), timer labels and units, ingredient names and units, and substitution notes.

Added 13 new unit tests covering: newline-in-prose, `--`/`>>`/`==` injection in prose, `}`/`%`/newline in ingredient name, `}` in timer label, `%` in timer unit, `==` and newline in section heading, newline in equipment. All pass.

**Snapshot status:** Unchanged — the full-document fixture data contains no special characters that sanitization would alter.

### WR-03: Document and pin >24h ISO-8601 behavior

**Files modified:** `src/CookBot.Application/Recipes/Iso8601DurationFormatter.cs`, `tests/CookBot.Tests/Recipes/Iso8601DurationFormatterTests.cs`
**Commit:** `f657af6`
**Applied fix:** Added a `<remarks>` doc-block documenting that hours are intentionally not rolled into days (1500 min → `PT25H`, not `P1DT1H`) and that schema.org / Google Rich Results accepts the `PT##H` form. Added a pinning test `ToIso8601Duration_Over24Hours_EmitsPTHHForm` asserting 1500 → `PT25H` and 1530 → `PT25H30M`.

### WR-04: JSON-LD double space for unit-less ingredients

**Files modified:** `src/CookBot.Application/Recipes/JsonLdRecipeProjector.cs`, `tests/CookBot.Tests/Recipes/JsonLdRecipeProjectorTests.cs`
**Commit:** `ac0f64d`
**Applied fix:** Replaced the interpolation-with-Trim approach in `BuildIngredientLine` with a parts-list approach: build a `List<string>`, add `amount` only when `Amount > 0`, add `unit` only when non-empty, always add `name`, then `string.Join(" ", parts)`. This eliminates interior double spaces when `Unit` is empty. Added a test `UnitlessIngredient_NoDoubleSpace` asserting `"4 eggs"` (no double space).

**Snapshot status:** Unchanged — the full-document fixture uses non-empty units throughout.

### WR-05: CbButton splat ordering

**Files modified:** `src/CookBot.Web/Components/Atoms/CbButton.razor`
**Commit:** `eb5da7a`
**Applied fix:** Moved `@attributes="AdditionalAttributes"` to the first attribute position on the `<button>` element. In Blazor, the last attribute wins on collision; placing the splat first ensures that the component's explicitly computed `type`, `class`, `style`, `disabled`, and `@onclick` attributes always win, preventing a caller from clobbering the design-system class or disabled state through `CaptureUnmatchedValues`. The intended additive use (`title`, `aria-*`, `data-*`) is unaffected.

### WR-06: Empty HowToSection emission

**Files modified:** `src/CookBot.Application/Recipes/JsonLdRecipeProjector.cs`, `tests/CookBot.Tests/Recipes/JsonLdRecipeProjectorTests.cs`
**Commit:** `69fbaf3`
**Applied fix:** Added an early-return guard in `FlushSection` when `currentSection.Count == 0` (resetting both `currentSection` and `currentSectionName` to null and returning without emitting). This prevents a trailing `SectionStep` with no following `ContentStep`, or two consecutive `SectionStep`s, from emitting a `HowToSection` with an empty `itemListElement []`. Added two tests: `TrailingEmptySection_IsOmitted` and `ConsecutiveSections_EmptyFirstOmitted`.

---

## Test results

**Full test run:** `dotnet test tests/CookBot.Tests --filter "Category!=RequiresApiKey" --no-build`
- Passed: 423 / 423
- Failed: 0
- Skipped: 0

**Net new tests added:** 16 (13 Cooklang sanitization + 1 Iso8601 >24h + 1 JSON-LD unit-less ingredient + 2 JSON-LD empty section)

**Snapshot changes:** None — both `.verified.txt` files are unchanged. The full-document fixtures for both projectors contain no content that the new sanitization logic would alter.

---

_Fixed: 2026-06-06T22:45:00Z_
_Fixer: Claude (gsd-code-fixer)_
_Iteration: 1_
