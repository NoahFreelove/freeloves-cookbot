---
status: partial
phase: 10-qol-polish-consumer-surfaces
source: [10-VERIFICATION.md]
started: 2026-05-17T00:00:00Z
updated: 2026-05-22T00:00:00Z
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
result: blocked
blocked_by: 999.1
note: Cannot reach Recipe Editor because the Edit button is missing from RecipeView (TopBar.RightSlot navigation race). See backlog 999.1 (RecipeView Cook button missing).

### 6. Pantry quick-add with no existing grocery list (POLISH-02)
expected: With all grocery lists deleted, clicking the pantry cart icon shows "Added [ingredient] to grocery list" toast and creates a new "Pantry quick-add" grocery list containing the ingredient.
result: pass

### 7. TopBar responsive collapse at narrow viewport (POLISH-04)
expected: At 719px viewport width on RecipeView, TopBar action buttons (Edit/Share/Schedule/Cook) hide; inline-above-hero fallback action row becomes visible.
result: partial
note: |
  POLISH-04's narrow criterion DOES work — the inline-above-hero action row appears at ≤720px and the TopBar slot
  is hidden as intended. But (a) the rest of RecipeView's layout doesn't responsively collapse (hero stays
  2-column, title overflows, hero photo squishes); see backlog 999.4. And (b) Edit is missing from the inline
  fallback row though the RenderFragment lists it first; see backlog 999.5.
follow_ups:
  - 999.4 (responsive layout)
  - 999.5 (missing Edit button)

## Summary

total: 7
passed: 4
partial: 1
issues: 0
pending: 0
skipped: 0
blocked: 1
follow_ups_captured: [999.1, 999.2, 999.3, 999.4, 999.5]
session_2_date: 2026-05-22
session_2_notes: |
  Test 4 schema rejection resolved through tool-use migration (commits c76037a, 1e014c7, 4346c44).
  Test 5 blocked by 999.1 (Cook/Edit button missing from RecipeView — TopBar.RightSlot navigation race).
  Test 6 passed.
  Test 7 partial — POLISH-04 inline-fallback toggle works, but broader responsive layout (999.4) and
  missing Edit button (999.5) surfaced during the test.

## Gaps

- truth: "AI Chat returns a parsed/validated RecipeDocument that the user can review (and on failure, opens RawRecipeEditorDialog with the raw output)"
  status: live-verified-2026-05-22
  reason: "Original Anthropic 400 — output_config.format.schema anyOf restrictions. Resolution required four layered fixes in one session: (1) externalize anyOf branches to $defs, (2) hoist parent type/required/additionalProperties into $defs and strip from anyOf siblings, (3) drop strict field from output_config.format (not a valid field there), (4) disable JsonSerializerDefaults.Web's AllowReadingFromString in schema export, and finally (5) migrate from output_config.format to the tools API entirely because the structured-outputs grammar compiler timed out on the polymorphic StepNode shape."
  fix_commits: [c76037a, 1e014c7, 4346c44]
  happy_path: verified
  validation_fail_fallback: deferred — cannot be exercised while the happy path succeeds; would need a synthetic schema-mismatch trigger to test the RawRecipeEditorDialog opening
  severity: resolved
  test: 4
  artifacts:
    - src/CookBot.Application/Recipes/RecipeJsonSchemaProvider.cs
    - src/CookBot.Application/AI/AiRecipeGenerator.cs
    - src/CookBot.Infrastructure/AI/AnthropicAiService.cs
    - src/CookBot.Web/Components/Pages/AiChat.razor
    - tests/CookBot.Tests/Recipes/SchemaAssertionTests.cs
  missing: []
