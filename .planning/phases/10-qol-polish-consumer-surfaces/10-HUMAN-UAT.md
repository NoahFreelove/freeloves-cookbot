---
status: partial
phase: 10-qol-polish-consumer-surfaces
source: [10-VERIFICATION.md]
started: 2026-05-17T00:00:00Z
updated: 2026-05-17T00:00:00Z
---

## Current Test

[awaiting human testing]

## Tests

### 1. Live Timer Tick (POLISH-05 — behavioral confirmation)
expected: Timer band shows countdown that updates every second without any page interaction when an active cooking session is running on Home.
result: [pending]

### 2. Whitespace-only custom prompt template behavior (WR-04)
expected: Profile → save `   ` (three spaces) as AI assistant instructions, then AI Chat → generated system prompt should use DefaultTemplate (begins "You are CookBot…"), not the whitespace string.
result: [pending]

### 3. Accent picker before-first-paint (QOL-05)
expected: Selecting Terracotta accent, closing tab, and reopening shows terracotta before any content is visible — no flash of default orange.
result: [pending]

### 4. RawRecipeEditorDialog end-to-end flow (QOL-04)
expected: AI Chat → trigger validation-fail fallback → "Edit anyway" opens RawRecipeEditorDialog with pretty-printed JSON. Invalid JSON shows red validation within 500ms; valid JSON enables "Parse and save", which closes the dialog and opens SaveRecipeDialog.
result: [pending]

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
passed: 0
issues: 0
pending: 7
skipped: 0
blocked: 0

## Gaps
