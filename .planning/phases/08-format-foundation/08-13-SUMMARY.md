---
phase: 08-format-foundation
plan: 13
subsystem: docs
tags: [docs, recipe-format, clean-04, d-37, readme]
dependency_graph:
  requires: [08-05, 08-11]
  provides: [CLEAN-04]
  affects: [README.md]
tech_stack:
  added: []
  patterns: [inline-docs]
key_files:
  created: []
  modified:
    - README.md
decisions:
  - "Inline Recipe Format section in README.md (not docs/) per D-37 — PROJECT.md positions README as the single self-hoster touchpoint"
  - "YAML wire example uses frontmatter envelope (--- delimiters) to match actual parse input"
  - "Gas half-stop rendering callout distinguishes canonical wire form ({ value: 4.5, unit: gas }) from human-readable indented JSON (4½)"
metrics:
  duration_minutes: 15
  completed_date: "2026-05-16"
  tasks_completed: 1
  tasks_total: 1
  files_changed: 1
---

# Phase 08 Plan 13: Recipe Format README Documentation Summary

## One-liner

Inline Recipe Format section added to README.md with YAML/JSON worked examples, V1->V2->V3 upcaster lineage, and internally-managed-format note per CLEAN-04 / D-37.

## What Was Built

Added a `## Recipe Format` section to README.md with five subsections per D-37:

1. **Canonical description** — one-paragraph prose explaining `RecipeDocument` as the single canonical format shared by YAML, JSON export, database column, and AI prompt schema.
2. **YAML wire example** — fenced YAML code block with all v3 fields populated (version, name, servings, prepTimeMinutes, cookTimeMinutes, photoUrl, description, tags, ingredients with id/name/amount/unit, steps with section and content kinds, per-step temperature and timers).
3. **JSON export example** — prose explaining `SerializeIndented` rendering and a fenced JSON block demonstrating gas half-stop human-readable rendering (`"4½"`), with clarification of the canonical wire form vs. indented display form.
4. **V1→V2→V3 upcaster lineage** — bullet list with one line per migration: V1→V2 (time-field rename, localId→id, kind discriminator replacement) and V2→V3 (photoUrl, description, per-step temperature all nullable with C7 null-coalescing).
5. **Internally-managed-format note** — explicit statement that users author through the chip composer, not raw YAML/JSON; upcaster chain is forward-only; downgrade unsupported.

The worked example uses canonical field names exclusively (photoUrl, description, temperature) per SCHEMA-10 denylist contract. The example is visually aligned with `RecipeSchemaDocumentationProvider.cs` for parity per PATTERNS.md.

## Tasks Completed

| Task | Name | Commit | Files |
|------|------|--------|-------|
| 1 | Add inline Recipe Format section to README.md (CLEAN-04, D-37) | 53def20 | README.md |

## Verification Results

All acceptance criteria met:

- `grep -c '^## Recipe Format' README.md` → 1
- `grep -c 'photoUrl\|description\|temperature' README.md` → 7
- `grep -c '^---$' README.md` → 2 (YAML frontmatter open + close)
- `grep -c 'version: 3' README.md` → 1
- `grep -c 'Migration_V1_To_V2\|Migration_V2_To_V3' README.md` → 2
- `grep -c 'prepTime\|prepTimeMinutes' README.md` → 2 (V1→V2 description)
- `grep -ic 'forward-only\|upcaster chain' README.md` → 2
- `grep -c '½\|gas' README.md` → 3
- `grep -Ec '"imageUrl"|"picture"|"summary"|"oven"' README.md` → 0 (no alias leakage)
- `dotnet build` → Build succeeded (0 errors)
- `dotnet test` → 247 passed, 6 pre-existing AI integration failures (same count as base commit; no regressions introduced)

## Deviations from Plan

None — plan executed exactly as written. The section was appended after the existing repo intro (matching D-37's intent: below the repo introduction, above the yet-to-be-written Phase 9 install section). The YAML wire example uses `---` frontmatter delimiters as specified; the JSON export example includes both the indented gas half-stop rendering and canonical wire-form clarification.

## Known Stubs

None — this is a pure documentation change; no UI rendering or data flow involved.

## Threat Flags

None — documentation-only change; no new network endpoints, auth paths, file access patterns, or schema changes.

## Requirements Closed

- **CLEAN-04** — README "Recipe Format" section complete (inline in README.md per D-37, five subsections per spec).
- Phase 8 success criterion #5 (README half) fully met.

## Self-Check: PASSED

- README.md exists at expected path: FOUND
- Commit 53def20 exists: FOUND
- All acceptance criteria verified above
