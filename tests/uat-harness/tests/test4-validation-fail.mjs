/**
 * tests/test4-validation-fail.mjs — UAT Test 4: RawRecipeEditorDialog validation-fail path
 *
 * Based on Phase 10 / QOL-04 UAT spec (10-HUMAN-UAT.md Test 4):
 *   "AI Chat → trigger validation-fail fallback → 'Edit anyway' opens
 *    RawRecipeEditorDialog with pretty-printed JSON."
 *
 * HONESTY DISPOSITION: MANUAL/DEFERRED — cannot be triggered on the happy path.
 *
 * WHY THIS TEST IS SKIPPED:
 *   The RawRecipeEditorDialog only opens when the AI returns malformed output
 *   that fails CookBot's schema validation (AiChat.razor L312 gates the canvas
 *   on `_lastStructuredRecipe.Ok == true`). While the AI happy-path succeeds,
 *   there is no way to trigger this dialog from a browser session without
 *   either:
 *     (a) Injecting a synthetic schema-mismatch AI response via a server-side
 *         fault-injection endpoint (none currently exists in the app), OR
 *     (b) Deliberately feeding a broken system prompt that causes the model
 *         to produce invalid output (unreliable, model-dependent, breaks other tests).
 *
 *   Source of truth: 10-HUMAN-UAT.md Gaps section:
 *     "validation_fail_fallback: deferred — cannot be exercised while the happy
 *     path succeeds; would need a synthetic schema-mismatch trigger to test the
 *     RawRecipeEditorDialog opening"
 *
 * WHAT A REAL TEST WOULD DO (fault-injection path, for future implementation):
 *   1. Hit a harness-only route/query-param that instructs AnthropicAiService or
 *      AiRecipeGenerator to return a canned invalid JSON body.
 *   2. Navigate to AI Chat, submit a recipe request.
 *   3. Assert the "Edit anyway" button appears (RawRecipeEditorDialog trigger).
 *   4. Click "Edit anyway" — assert the dialog opens with pretty-printed JSON.
 *   5. Type invalid JSON → assert red validation indicator within 500ms.
 *   6. Fix the JSON → assert "Parse and save" becomes enabled.
 *   7. Click "Parse and save" → assert dialog closes, SaveRecipeDialog opens.
 *
 * EXIT BEHAVIOR: This module returns `skipped` — NOT `failed` AND NOT `passed`.
 *   The harness runner treats SKIP as non-failing; it prints a distinct SKIP line
 *   and does NOT count it toward the exit-code failure check.
 *
 * @returns {Promise<{status: 'skipped', message: string}>}
 */

export async function runTest4() {
  console.log('\n[test4] UAT Test 4 (validation-fail): SKIP — manual/deferred');
  console.log('[test4] Reason: RawRecipeEditorDialog cannot be triggered while the AI happy-path succeeds.');
  console.log('[test4] To exercise this path, add a server-side fault-injection seam that forces a');
  console.log('[test4] schema-mismatch AI response, then update this test to drive it.');

  return {
    status: 'skipped',
    message:
      'UAT Test 4 (validation-fail): SKIP — manual/deferred. ' +
      'RawRecipeEditorDialog only opens on a malformed AI response. ' +
      'Fault-injection seam not yet implemented. ' +
      'See 10-HUMAN-UAT.md §Gaps for the deferred note.'
  };
}
