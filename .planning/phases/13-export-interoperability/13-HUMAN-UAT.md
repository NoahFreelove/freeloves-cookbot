---
status: passed
phase: 13-export-interoperability
source: [13-VERIFICATION.md]
started: 2026-06-06T22:37:33Z
updated: 2026-06-07T00:46:52Z
---

## Current Test

[complete — both items verified 2026-06-07]

## Tests

### 1. Cooklang .cook file download (browser click)
expected: Open a recipe in the browser, click "Export as .cook" in the top bar → a `<RecipeName>.cook` file downloads. Opening it shows valid Cooklang: braced ingredients `@name{amount%unit}`, `== Section ==` headings, `~{n%unit}` timers, `--` comments for temp/doneness, and a trailing `-- Substitution (name): note` block. The action tooltip reads "Export only · one-way (no re-import)".
result: pass — 2026-06-07. Downloaded "Apple Blueberry Crumble.cook" via the top-bar button. Valid Cooklang: `>>` metadata, `== Section ==` headings, always-braced ingredients (incl. a comma/paren-heavy name kept intact), `~bake{45%min}`/`~rest{15%min}` timers, `--` doneness/equipment comments, and a trailing `-- Substitution (...)` block. Note: the `>> source:` line emits the recipe's raw SourceUrl as plain text (a seeded `javascript:` XSS-test value) — harmless in a `.cook` file; the JSON-LD/HTML path does NOT leak it (author is name-only). Optional low-pri hygiene: validate/omit non-http(s) schemes in the Cooklang `>> source:` line.

### 2. Google Rich Results structural validation
expected: Copy the `<script type="application/ld+json">` block from `/recipes/{id}` (or paste the URL on a public HTTPS deployment) into https://search.google.com/test/rich-results → the Recipe entity passes with no structural errors. Note: `image` is correctly absent on a plain-http/localhost deployment (only emitted for absolute-HTTPS URLs) — that is expected, not an error.
result: pass (with caveat) — 2026-06-07. Google Rich Results Test parsed the block as a `Recipe` with all fields well-formed (ISO-8601 durations, recipeIngredient array, HowToSection/HowToStep instructions, no aggregateRating). It reports "1 invalid item" solely because the required `image` field is absent — which is correct-by-design on `http://localhost` (INTEROP-02 omits image without an absolute-HTTPS URL). Structural validity confirmed; full rich-result eligibility requires a public HTTPS deployment of a recipe that has a photo. Verified via the live JSON-LD audit + the user's Rich Results Test run.

## Summary

total: 2
passed: 2
issues: 0
pending: 0
skipped: 0
blocked: 0

## Gaps
