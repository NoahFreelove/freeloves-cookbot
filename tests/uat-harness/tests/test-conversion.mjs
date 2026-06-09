/**
 * tests/test-conversion.mjs — UAT Conversion smoke check (CLEANUP-04)
 *
 * Validates the per-recipe unit-display toggle wired by plan 11-04 / CLEANUP-04
 * on RecipeView (RecipeView.razor:138-147, ToggleUnitMode + FormatQty):
 *   - Default mode = "converted" (the user's UnitSystem).
 *   - A per-recipe Ghost CbButton flips to "original" (the AI-emitted canonical
 *     units) for THIS recipe only; state persists in localStorage under
 *     cookbot_units_<recipeId>.
 *   - Display conversion is presentation-only — it must NOT mutate the canonical
 *     RecipeDocument.
 *
 * Target: /recipes/1 ("Apple Blueberry Crumble"), whose canonical document
 * contains a 900 g ingredient (id=1) plus volume ingredients in tbsp/tsp/cup.
 *
 * Assertions (unit-system-agnostic — they prove the convert↔original flip is
 * wired, regardless of which UnitSystem the default user has; per the plan note
 * this is the load-bearing behaviour):
 *   (a) the toggle control exists (a <button> labelled "...units").
 *   (b) toggling CHANGES at least one displayed ingredient amount.
 *   (c) ORIGINAL mode surfaces the canonical "900 g" literal verbatim (proving
 *       the toggle shows AI-emitted units), AND CONVERTED mode differs from
 *       ORIGINAL on ≥1 ingredient (proving display conversion is actually
 *       running). Together these prove conversion is wired without mutating the
 *       canonical document.
 *
 * Hygiene: the test restores the recipe's pre-existing localStorage value (and
 * never touches the user's UnitSystem), so it leaves no residue.
 *
 * @param {import('playwright').Page} page
 * @param {object} opts
 * @param {number} [opts.recipeId=1] - Recipe to load (defaults to the 900 g recipe).
 * @returns {Promise<{status: 'passed'|'failed'|'skipped', message: string}>}
 */

import { BASE_URL, screenshot } from '../lib/app.mjs';

const CANONICAL_LITERAL = '900 g'; // recipe 1 ingredient #1, verbatim AI-emitted units

export async function runConversionTest(page, { recipeId = 1 } = {}) {
  const testLabel = 'UAT Conversion (CLEANUP-04)';
  console.log(`\n[conversion] Starting ${testLabel} on /recipes/${recipeId}...`);

  const lsKey = `cookbot_units_${recipeId}`;
  let savedLsValue = null;

  try {
    // Reset viewport to a desktop width so the sticky ingredients aside renders
    // in its normal column (Test 7 may have left a 719px viewport behind).
    await page.setViewportSize({ width: 1280, height: 1000 });

    const recipeUrl = `${BASE_URL}/recipes/${recipeId}`;
    await page.goto(recipeUrl, { waitUntil: 'networkidle', timeout: 30_000 });

    // Wait for the interactive render: the hero is always present, the unit
    // toggle button confirms the ingredients aside has rendered.
    await page.waitForSelector('.recipe-hero', { timeout: 15_000 });

    // Preserve any pre-existing per-recipe unit-mode so we can restore it.
    savedLsValue = await page.evaluate((k) => localStorage.getItem(k), lsKey);

    // ── Assertion (a): the toggle control exists ────────────────────────────
    const toggle = page.locator('button', { hasText: /units/i }).first();
    const toggleExists = await toggle
      .waitFor({ state: 'visible', timeout: 15_000 })
      .then(() => true)
      .catch(() => false);

    if (!toggleExists) {
      return {
        status: 'failed',
        message: `${testLabel}: FAIL (a) — per-recipe unit toggle button (text "...units") not found on /recipes/${recipeId}. Is CLEANUP-04 applied?`,
      };
    }
    console.log('[conversion] Unit toggle button exists — PASS (a)');

    // Helper: read the ingredient amount column (aside span.num) as a list.
    const readAmounts = () =>
      page.evaluate(() => {
        const aside = document.querySelector('.recipe-body-grid aside');
        if (!aside) return null;
        return Array.from(aside.querySelectorAll('span.num')).map((s) => s.textContent.trim());
      });

    // The page default mode is "converted" (RecipeView._unitMode = "converted")
    // unless localStorage pinned it. Normalise to a KNOWN state first by reading
    // the current toggle label, then capture both states explicitly.
    // We capture amounts in the current state, toggle, capture again, then map
    // each capture to converted/original using the toggle's own label.

    const labelBefore = (await toggle.textContent())?.trim() || '';
    const amountsBefore = await readAmounts();
    if (!amountsBefore || amountsBefore.length === 0) {
      return {
        status: 'failed',
        message: `${testLabel}: FAIL — could not read ingredient amounts (aside span.num) on /recipes/${recipeId}.`,
      };
    }

    await screenshot(page, 'test-conversion-state1.png');

    // Toggle once.
    await toggle.click();
    await page.waitForTimeout(500);
    const labelAfter = (await toggle.textContent())?.trim() || '';
    const amountsAfter = await readAmounts();

    await screenshot(page, 'test-conversion-state2.png');

    if (!amountsAfter || amountsAfter.length !== amountsBefore.length) {
      return {
        status: 'failed',
        message: `${testLabel}: FAIL — ingredient list changed length across toggle (before=${amountsBefore?.length}, after=${amountsAfter?.length}); cannot compare.`,
      };
    }

    // ── Assertion (b): toggling CHANGES at least one displayed amount ───────
    const changedIndices = amountsBefore
      .map((v, i) => (v !== amountsAfter[i] ? i : -1))
      .filter((i) => i >= 0);

    if (changedIndices.length === 0) {
      return {
        status: 'failed',
        message: `${testLabel}: FAIL (b) — toggling the unit mode changed NO displayed ingredient amount. before=${JSON.stringify(amountsBefore)} after=${JSON.stringify(amountsAfter)}`,
      };
    }
    const sampleIdx = changedIndices[0];
    console.log(
      `[conversion] Toggle changed ${changedIndices.length} amount(s), e.g. ingredient #${sampleIdx + 1}: "${amountsBefore[sampleIdx]}" ↔ "${amountsAfter[sampleIdx]}" — PASS (b)`
    );

    // ── Map the two captures to {converted, original} via the toggle label. ──
    // The toggle reads "Show original units" while in CONVERTED mode, and
    // "Show converted units" while in ORIGINAL mode (RecipeView.razor:145-146).
    // So labelBefore tells us which mode amountsBefore was captured in.
    const isConverted = (label) => /show original units/i.test(label);
    let convertedAmounts;
    let originalAmounts;
    if (isConverted(labelBefore)) {
      convertedAmounts = amountsBefore;
      originalAmounts = amountsAfter;
    } else {
      // labelBefore said "Show converted units" ⇒ before was ORIGINAL.
      originalAmounts = amountsBefore;
      convertedAmounts = amountsAfter;
    }
    // Sanity: the post-toggle label must be the opposite of the pre-toggle one.
    if (isConverted(labelBefore) === isConverted(labelAfter)) {
      return {
        status: 'failed',
        message: `${testLabel}: FAIL — toggle label did not flip (before="${labelBefore}", after="${labelAfter}").`,
      };
    }

    // ── Assertion (c.1): ORIGINAL mode shows the canonical "900 g" verbatim. ─
    const originalHas900g = originalAmounts.some((a) => a === CANONICAL_LITERAL);
    if (!originalHas900g) {
      return {
        status: 'failed',
        message: `${testLabel}: FAIL (c) — ORIGINAL mode does not surface the canonical "${CANONICAL_LITERAL}" verbatim. original=${JSON.stringify(originalAmounts)}`,
      };
    }
    console.log(`[conversion] ORIGINAL mode shows canonical "${CANONICAL_LITERAL}" verbatim — PASS (c.1)`);

    // ── Assertion (c.2): CONVERTED mode differs from ORIGINAL on ≥1 ingredient,
    // i.e. display conversion is genuinely running (not a no-op relabel). ──────
    const convertedDiffers = convertedAmounts.some((a, i) => a !== originalAmounts[i]);
    if (!convertedDiffers) {
      return {
        status: 'failed',
        message: `${testLabel}: FAIL (c) — CONVERTED mode is identical to ORIGINAL on every ingredient; display conversion is not wired. converted=${JSON.stringify(convertedAmounts)} original=${JSON.stringify(originalAmounts)}`,
      };
    }
    const diffIdx = convertedAmounts.findIndex((a, i) => a !== originalAmounts[i]);
    console.log(
      `[conversion] CONVERTED differs from ORIGINAL (e.g. ingredient #${diffIdx + 1}: original "${originalAmounts[diffIdx]}" vs converted "${convertedAmounts[diffIdx]}") — PASS (c.2)`
    );

    return {
      status: 'passed',
      message:
        `${testLabel}: PASS — toggle exists; flip changes ${changedIndices.length} amount(s); ` +
        `ORIGINAL shows canonical "${CANONICAL_LITERAL}" verbatim and CONVERTED differs from ORIGINAL ` +
        `(display conversion wired, canonical not mutated).`,
    };
  } catch (err) {
    await screenshot(page, 'test-conversion-FAIL.png').catch(() => {});
    return {
      status: 'failed',
      message: `${testLabel}: FAIL (unexpected error) — ${err.message}`,
    };
  } finally {
    // Restore pre-existing localStorage so the harness leaves no residue and the
    // recipe view returns to whatever mode the human last chose.
    try {
      await page.evaluate(
        ({ k, v }) => {
          if (v === null) localStorage.removeItem(k);
          else localStorage.setItem(k, v);
        },
        { k: lsKey, v: savedLsValue }
      );
    } catch {
      /* page may be navigating; best-effort cleanup */
    }
  }
}
