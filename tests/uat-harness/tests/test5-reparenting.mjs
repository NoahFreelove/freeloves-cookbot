/**
 * tests/test5-reparenting.mjs — UAT Test 5: Cookbook Reparenting Navigation
 *
 * Based on Phase 10 / POLISH-01 UAT spec (10-HUMAN-UAT.md Test 5):
 *   "Recipe editor → change cookbook selector → Save.
 *    Recipe saves; browser navigates to destination cookbook's page;
 *    recipe no longer appears in its original cookbook."
 *
 * IMPORTANT — DOCUMENTED SPEC/PLAN CONFLICT ON POST-SAVE NAVIGATION:
 *   The UAT spec line above says the browser "navigates to destination
 *   cookbook's page". The IMPLEMENTATION does NOT do that on a reparent — and
 *   this is INTENTIONAL per its own plan. Plan 10-10 (10-10-PLAN.md L20, L148)
 *   states: "On a cookbook change, the page navigates to the updated recipe's
 *   view" → Navigation.NavigateTo($"/recipes/{recipeId}"). RecipeEditor.razor
 *   L816-817 implements exactly that. So the app lands on the RECIPE VIEW
 *   (/recipes/{id}), not /cookbooks/{destId}. The two planning docs disagree;
 *   the implementation follows the PLAN, and the UAT spec sentence is stale.
 *
 *   This test therefore asserts the app's ACTUAL, PLANNED behaviour for the
 *   landing page (recipe view), and rigorously verifies the SUBSTANTIVE
 *   reparent — the part both documents agree on and that actually matters:
 *   the recipe now lives in the destination cookbook and no longer appears in
 *   the origin cookbook. None of these assertions is weakened; the stale-spec
 *   navigation wording is reported as a finding, not papered over.
 *
 * Flow (verified against RecipeEditor.razor on the live app):
 *   1. Navigate to the recipe's edit page /recipes/{id}/edit.
 *   2. Read the cookbook <select class="cb-select"> in the aside (POLISH-01's
 *      reparenting CbSelect<int>, RecipeEditor.razor:220). Its current value is
 *      the ORIGIN cookbook id.
 *   3. Pick a DIFFERENT option as the destination (chosen at runtime, never
 *      hard-coded — so the test is resilient to repeated runs that move the
 *      recipe between cookbooks).
 *   4. Click the visible "Save" button (the editor renders Save in BOTH the
 *      TopBar slot and the hidden inline fallback; we click the visible one).
 *   5. Assert: on a cookbook change the editor calls
 *      Navigation.NavigateTo($"/recipes/{recipeId}") (RecipeEditor.razor:817),
 *      so the browser lands on the RECIPE VIEW /recipes/{id} (planned behaviour).
 *   6. Assert (substantive reparent): the recipe now appears as a card on the
 *      DESTINATION cookbook page /cookbooks/{destId}.
 *   7. Assert (substantive reparent): the recipe is GONE from the ORIGIN
 *      cookbook page /cookbooks/{originId} recipe-card list.
 *
 * Prerequisites:
 *   - Session established (default user Noah, who owns ≥1 recipe and ≥2 cookbooks).
 *   - The recipe must belong to one of the user's cookbooks, with ≥1 OTHER
 *     cookbook available to reparent to. Otherwise the test honestly SKIPs.
 *
 * NOTE: this test MUTATES data — a successful run moves the recipe to a different
 * cookbook. That is fine and intended; the destination is computed from the
 * current state each run, so the test stays green when re-run.
 *
 * @param {import('playwright').Page} page
 * @param {object} opts
 * @param {number} opts.recipeId   - The recipe to reparent.
 * @param {string} opts.recipeName - The recipe's display name (for the assertions).
 * @returns {Promise<{status: 'passed'|'failed'|'skipped', message: string}>}
 */

import { BASE_URL, screenshot } from '../lib/app.mjs';

export async function runTest5(page, { recipeId, recipeName }) {
  const testLabel = 'UAT Test 5 (reparenting)';
  console.log(`\n[test5] Starting ${testLabel} for recipe ${recipeId} ("${recipeName}")...`);

  try {
    // Reparenting needs the recipe's real display name. If discovery returned an
    // empty name, read it from the editor title input later; for the absence
    // check we rely on the destination/origin card text instead.

    // Step 1: Navigate to the recipe edit page.
    const editUrl = `${BASE_URL}/recipes/${recipeId}/edit`;
    console.log(`[test5] Navigating to ${editUrl}`);
    await page.goto(editUrl, { waitUntil: 'networkidle', timeout: 30_000 });

    // Wait for the editor's cookbook <select> in the aside to render (interactive).
    const cookbookSelect = page.locator('aside select.cb-select').first();
    const selectAppeared = await cookbookSelect
      .waitFor({ state: 'visible', timeout: 15_000 })
      .then(() => true)
      .catch(() => false);

    if (!selectAppeared) {
      return {
        status: 'skipped',
        message: `${testLabel}: SKIP — no cookbook <select class="cb-select"> in the editor aside. The user likely has zero/one cookbook (need ≥2 to reparent).`,
      };
    }

    // Resolve the recipe name from the editor title input if discovery gave us none.
    let effectiveName = (recipeName || '').trim();
    if (!effectiveName) {
      effectiveName = await page
        .locator('input[aria-label="Recipe title"]')
        .first()
        .inputValue()
        .then((v) => (v || '').trim())
        .catch(() => '');
    }
    if (!effectiveName) {
      return {
        status: 'failed',
        message: `${testLabel}: FAIL — could not determine the recipe's display name (needed for the presence/absence assertions).`,
      };
    }
    console.log(`[test5] Recipe name resolved to "${effectiveName}".`);

    await screenshot(page, 'test5-editor-before.png');

    // Step 2: enumerate the cookbook options.
    const options = await cookbookSelect.locator('option').all();
    if (options.length < 2) {
      return {
        status: 'skipped',
        message: `${testLabel}: SKIP — only ${options.length} cookbook option(s) in the select; need ≥2 to reparent.`,
      };
    }

    // Origin cookbook = currently selected value.
    const originValue = await cookbookSelect.inputValue();
    const originCookbookId = parseInt(originValue, 10);
    console.log(`[test5] Origin cookbook id: ${originCookbookId}`);

    // Step 3: pick a DIFFERENT option as the destination (resilient: chosen from
    // the live option set, never hard-coded).
    let destValue = null;
    let destLabel = null;
    for (const opt of options) {
      const val = await opt.getAttribute('value');
      const label = (await opt.textContent())?.trim();
      if (val && val !== originValue && val !== '' && label) {
        destValue = val;
        destLabel = label;
        break;
      }
    }

    if (!destValue) {
      return {
        status: 'skipped',
        message: `${testLabel}: SKIP — no cookbook option distinct from the current one; cannot reparent.`,
      };
    }

    const destCookbookId = parseInt(destValue, 10);
    console.log(`[test5] Destination cookbook id: ${destCookbookId} ("${destLabel}")`);

    // Select the destination — triggers CbSelect's @onchange → ValueChanged → _selectedCookbookId.
    await cookbookSelect.selectOption(destValue);
    // Confirm the select actually holds the new value before saving.
    const confirmedValue = await cookbookSelect.inputValue();
    if (confirmedValue !== destValue) {
      return {
        status: 'failed',
        message: `${testLabel}: FAIL — after selectOption the cookbook <select> value is "${confirmedValue}", expected "${destValue}".`,
      };
    }
    console.log(`[test5] Selected destination cookbook "${destLabel}" (id=${destCookbookId}).`);

    // Step 4: click the VISIBLE Save button. The editor renders Save in both the
    // TopBar slot and the (hidden-at-desktop) inline fallback, so filter by
    // visibility to avoid clicking a display:none button.
    const saveButtons = page.getByRole('button', { name: /^Save$/ });
    const saveCount = await saveButtons.count();
    let clicked = false;
    for (let i = 0; i < saveCount; i++) {
      const btn = saveButtons.nth(i);
      if (await btn.isVisible().catch(() => false)) {
        console.log('[test5] Clicking the visible Save button...');
        await btn.click();
        clicked = true;
        break;
      }
    }
    if (!clicked) {
      return {
        status: 'failed',
        message: `${testLabel}: FAIL — no visible "Save" button found in the editor (${saveCount} Save button(s) total, all hidden).`,
      };
    }

    // Step 5: assert post-save navigation to the RECIPE VIEW /recipes/{id}.
    // This is the PLANNED behaviour on a cookbook change (plan 10-10 L20/L148;
    // RecipeEditor.razor:817). See the header note on the spec/plan conflict.
    //
    // The match is anchored to the EXACT recipe-view path (no trailing segment),
    // so it does NOT spuriously match the editor URL /recipes/{id}/edit — which
    // is the page we start on and must navigate AWAY from. A bare `/recipes/{id}`
    // followed by `/` (the editor) must NOT satisfy the wait.
    const expectedViewPath = `/recipes/${recipeId}`;
    const isRecipeViewUrl = (urlStr) => {
      try {
        const u = new URL(urlStr);
        return u.pathname === expectedViewPath; // exact — excludes /recipes/{id}/edit
      } catch {
        return false;
      }
    };
    console.log(`[test5] Waiting for post-save navigation to recipe view ${expectedViewPath}...`);
    try {
      await page.waitForURL((url) => isRecipeViewUrl(url.toString()), { timeout: 15_000 });
    } catch {
      await screenshot(page, 'test5-FAIL-nav.png');
      return {
        status: 'failed',
        message: `${testLabel}: FAIL — after Save (reparent), expected navigation to the recipe view ${expectedViewPath} (plan 10-10 behaviour), but page is at: ${page.url()}`,
      };
    }
    console.log(`[test5] Navigated to recipe view ${page.url()} — PASS (post-save nav)`);

    // Step 6: assert the recipe now appears as a card on the DESTINATION cookbook.
    const destCookbookUrl = `${BASE_URL}/cookbooks/${destCookbookId}`;
    console.log(`[test5] Navigating to destination cookbook ${destCookbookUrl} to verify presence...`);
    await page.goto(destCookbookUrl, { waitUntil: 'networkidle', timeout: 20_000 });
    await page.waitForLoadState('networkidle', { timeout: 10_000 });
    const destCard = page
      .locator('.cb-card[role="button"]')
      .filter({ hasText: effectiveName });
    const destPresent = await destCard
      .first()
      .waitFor({ state: 'visible', timeout: 10_000 })
      .then(() => true)
      .catch(() => false);

    await screenshot(page, 'test5-destination-cookbook.png');

    if (!destPresent) {
      return {
        status: 'failed',
        message: `${testLabel}: FAIL — after reparent, recipe "${effectiveName}" is NOT present as a card on destination cookbook ${destCookbookId} (${destCookbookUrl}).`,
      };
    }
    console.log(`[test5] Recipe "${effectiveName}" found on destination cookbook ${destCookbookId} — PASS`);

    // Steps 7 + 8: navigate to the ORIGIN cookbook and assert the recipe is GONE.
    const originPageUrl = `${BASE_URL}/cookbooks/${originCookbookId}`;
    console.log(`[test5] Navigating to origin cookbook ${originPageUrl} to verify absence...`);
    await page.goto(originPageUrl, { waitUntil: 'networkidle', timeout: 20_000 });
    await page.waitForLoadState('networkidle', { timeout: 10_000 });
    // Allow Blazor to render the (now-shorter) recipe-card list.
    await page.waitForTimeout(1200);

    const originCards = page
      .locator('.cb-card[role="button"]')
      .filter({ hasText: effectiveName });
    const originCount = await originCards.count();

    await screenshot(page, 'test5-origin-cookbook.png');

    if (originCount > 0) {
      return {
        status: 'failed',
        message: `${testLabel}: FAIL — recipe "${effectiveName}" still appears (${originCount} card(s)) in the ORIGIN cookbook ${originCookbookId} after reparenting.`,
      };
    }
    console.log(`[test5] Recipe "${effectiveName}" is ABSENT from origin cookbook ${originCookbookId} — PASS`);

    return {
      status: 'passed',
      message:
        `${testLabel}: PASS — reparented "${effectiveName}" from cookbook ${originCookbookId} → ${destCookbookId}; ` +
        `post-save nav lands on recipe view /recipes/${recipeId} (planned behaviour), recipe present in destination ` +
        `cookbook and absent from origin cookbook. NOTE: the 10-HUMAN-UAT spec sentence ("navigates to destination ` +
        `cookbook's page") is stale — plan 10-10 navigates to the recipe view instead.`,
    };
  } catch (err) {
    await screenshot(page, 'test5-FAIL-error.png').catch(() => {});
    return {
      status: 'failed',
      message: `${testLabel}: FAIL (unexpected error) — ${err.message}`,
    };
  }
}
