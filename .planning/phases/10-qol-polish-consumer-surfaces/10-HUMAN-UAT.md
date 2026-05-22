---
status: partial
phase: 10-qol-polish-consumer-surfaces
source: [10-VERIFICATION.md]
started: 2026-05-17T00:00:00Z
updated: 2026-05-18T00:00:00Z
---

## Current Test

number: 5
name: Cookbook reparenting navigation (POLISH-01)
expected: |
  Recipe editor → change cookbook selector → Save. Recipe saves; browser navigates to destination cookbook's page; recipe no longer appears in its original cookbook.
awaiting: user response

## Tests

### 1. Live Timer Tick (POLISH-05 — behavioral confirmation)
expected: Timer band shows countdown that updates every second without any page interaction when an active cooking session is running on Home.
result: pass

### 2. Whitespace-only custom prompt template behavior (WR-04)
expected: Profile → save `   ` (three spaces) as AI assistant instructions, then AI Chat → generated system prompt should use DefaultTemplate (begins "You are CookBot…"), not the whitespace string.
result: pass

### 3. Accent picker before-first-paint (QOL-05)
expected: Selecting Terracotta accent, closing tab, and reopening shows terracotta before any content is visible — no flash of default orange.
result: pass

### 4. RawRecipeEditorDialog end-to-end flow (QOL-04)
expected: AI Chat → trigger validation-fail fallback → "Edit anyway" opens RawRecipeEditorDialog with pretty-printed JSON. Invalid JSON shows red validation within 500ms; valid JSON enables "Parse and save", which closes the dialog and opens SaveRecipeDialog.
result: [pending-retest]
reported: "AI error: Anthropic API error 400: output_config.format.schema: For 'anyOf', 'additionalProperties, required, type' is not supported"
severity: blocker
note: |
  Root cause: STJ's JsonSchemaExporter emitted the polymorphic StepNode (ContentStep | SectionStep)
  as steps.items.anyOf with each branch carrying inline type/properties/required/additionalProperties.
  Anthropic strict structured-outputs forbids those keywords inside anyOf branches and requires
  branches to be $ref wrappers into $defs.
fix: |
  RecipeJsonSchemaProvider.ExternalizeAnyOfBranches added — walks the schema post-export, lifts
  each anyOf branch carrying forbidden keywords into $defs/<discriminator-named> entry and
  replaces the branch with { "$ref": "#/$defs/..." }. SchemaAssertionTests now has a regression
  guard (GetSchema_AnyOfBranches_ContainOnlyRefs) and an idempotency guard. The fallback
  validation-fail UX path itself was not modified — re-run this UAT to confirm it now reaches.

### 5. Cookbook reparenting navigation (POLISH-01)
expected: Recipe editor → change cookbook selector → Save. Recipe saves; browser navigates to destination cookbook's page; recipe no longer appears in its original cookbook.
result: [pending]

### 6. Pantry quick-add with no existing grocery list (POLISH-02)
expected: With all grocery lists deleted, clicking the pantry cart icon shows "Added [ingredient] to grocery list" toast and creates a new "Pantry quick-add" grocery list containing the ingredient.
result: [pending]

### 7. TopBar responsive collapse at narrow viewport (POLISH-04)
expected: At 719px viewport width on RecipeView, TopBar action buttons (Edit/Share/Schedule/Cook) hide; inline-above-hero fallback action row becomes visible.
result: [pending]

## Summary

total: 7
passed: 3
issues: 1
pending: 3
skipped: 0
blocked: 0

## Gaps

- truth: "AI Chat returns a parsed/validated RecipeDocument that the user can review (and on failure, opens RawRecipeEditorDialog with the raw output)"
  status: code-fix-applied-pending-retest
  reason: "User reported: Anthropic API error 400 — output_config.format.schema: For 'anyOf', 'additionalProperties, required, type' is not supported. The structured-outputs request was rejected before any model output was generated, so the validation-fail fallback path could not be exercised."
  fix_commit: pending
  fix_summary: "RecipeJsonSchemaProvider.ExternalizeAnyOfBranches lifts polymorphic anyOf branches into $defs and replaces them with $ref wrappers, removing the forbidden inline keywords from anyOf. Regression guard added."
  severity: blocker
  test: 4
  artifacts:
    - src/CookBot.Application/Recipes/RecipeJsonSchemaProvider.cs
    - tests/CookBot.Tests/Recipes/SchemaAssertionTests.cs
  missing: []
