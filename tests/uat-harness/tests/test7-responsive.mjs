/**
 * tests/test7-responsive.mjs — UAT Test 7: TopBar Responsive Collapse at 719px
 *
 * Based on Phase 10 / POLISH-04 + CLEANUP-01/02 UAT spec (10-HUMAN-UAT.md Test 7):
 *   "At 719px viewport width on RecipeView, TopBar action buttons (Edit/Share/Schedule/Cook)
 *    hide; inline-above-hero fallback action row becomes visible."
 *   Full pass after CLEANUP-01/02 additionally requires:
 *     - Edit button present in the fallback row (CLEANUP-01)
 *     - RecipeView hero grid collapses to single column (CLEANUP-02)
 *
 * CSS hooks asserted (cookbot-design.css L769-805):
 *   @media (max-width: 720px) {
 *     .topbar-right-slot        → display: none !important  (hidden)
 *     .recipe-hero              → grid-template-columns: 1fr (single column)
 *   }
 *   @media (min-width: 721px) {
 *     .recipe-actions-inline-fallback → display: none !important  (hidden at wide)
 *   }
 *   At 719px:
 *     .topbar-right-slot              → hidden
 *     .recipe-actions-inline-fallback → visible (the min-width:721 hide rule does NOT fire)
 *     .recipe-hero                    → single-column track
 *
 * Real DOM verified against the live app (RecipeView.razor): the fallback row's
 * RenderFragment (_topBarActions) contains four CbButtons — Edit / Share /
 * Schedule / Cook this — each a <button class="cb-btn ...">. The Edit button is
 * the first child and was clipped before CLEANUP-01's flex-wrap fix.
 *
 * @param {import('playwright').Page} page
 * @param {object} opts
 * @param {number} opts.recipeId - Recipe to load on /recipes/{id}
 * @returns {Promise<{status: 'passed'|'failed'|'skipped', message: string}>}
 */

import { BASE_URL, screenshot } from '../lib/app.mjs';

const NARROW_WIDTH = 719;
const NARROW_HEIGHT = 900;

export async function runTest7(page, { recipeId }) {
  const testLabel = 'UAT Test 7 (responsive)';
  console.log(`\n[test7] Starting ${testLabel} at ${NARROW_WIDTH}px viewport...`);

  try {
    // Set viewport to 719px BEFORE navigating — below the 720px media-query
    // threshold — so the responsive CSS is applied on first paint.
    await page.setViewportSize({ width: NARROW_WIDTH, height: NARROW_HEIGHT });

    const recipeUrl = `${BASE_URL}/recipes/${recipeId}`;
    console.log(`[test7] Navigating to ${recipeUrl}`);
    await page.goto(recipeUrl, { waitUntil: 'networkidle', timeout: 30_000 });

    // Wait for the interactive render: the hero (always present) and the unit
    // toggle button confirm the page rendered. We then wait for the fallback row
    // to be VISIBLE — at 719px the min-width:721 hide rule does not apply, so it
    // becomes visible once CSS settles. This doubles as Assertion 2's wait.
    await page.waitForSelector('.recipe-hero', { timeout: 15_000 });
    await page.waitForSelector('.recipe-actions-inline-fallback', {
      state: 'visible',
      timeout: 15_000,
    });
    // Give CSS media queries time to fully apply.
    await page.waitForTimeout(400);

    await screenshot(page, 'test7-719px-recipeview.png');

    // ── Assertion 1: .topbar-right-slot is HIDDEN at 719px ──────────────────
    const topbarSlot = await page.evaluate(() => {
      const el = document.querySelector('.topbar-right-slot');
      if (!el) return { present: false };
      const computed = window.getComputedStyle(el);
      return {
        present: true,
        display: computed.getPropertyValue('display'),
        visibility: computed.getPropertyValue('visibility'),
      };
    });

    if (!topbarSlot.present) {
      return {
        status: 'failed',
        message: `${testLabel}: FAIL — .topbar-right-slot not in DOM at ${NARROW_WIDTH}px (expected present-but-hidden).`,
      };
    }
    if (topbarSlot.display !== 'none') {
      return {
        status: 'failed',
        message: `${testLabel}: FAIL — .topbar-right-slot should be hidden at ${NARROW_WIDTH}px but display="${topbarSlot.display}".`,
      };
    }
    console.log('[test7] .topbar-right-slot is hidden (display:none) — PASS');

    // ── Assertion 2: .recipe-actions-inline-fallback is VISIBLE at 719px ────
    const fallback = await page.evaluate(() => {
      const el = document.querySelector('.recipe-actions-inline-fallback');
      if (!el) return { present: false };
      const computed = window.getComputedStyle(el);
      return {
        present: true,
        display: computed.getPropertyValue('display'),
        visibility: computed.getPropertyValue('visibility'),
      };
    });

    if (!fallback.present) {
      return {
        status: 'failed',
        message: `${testLabel}: FAIL — .recipe-actions-inline-fallback not in DOM at ${NARROW_WIDTH}px.`,
      };
    }
    if (fallback.display === 'none' || fallback.visibility === 'hidden') {
      return {
        status: 'failed',
        message: `${testLabel}: FAIL — .recipe-actions-inline-fallback should be visible at ${NARROW_WIDTH}px but display="${fallback.display}" visibility="${fallback.visibility}".`,
      };
    }
    console.log('[test7] .recipe-actions-inline-fallback is visible — PASS');

    // ── Assertion 3 (CLEANUP-01): an "Edit" <button> is present + visible in the
    // fallback row. We assert on a real <button> (not just text) and that it is
    // not clipped to zero size — this is the exact regression CLEANUP-01 fixed.
    const editBtn = await page.evaluate(() => {
      const fb = document.querySelector('.recipe-actions-inline-fallback');
      if (!fb) return { found: false, reason: 'fallback not in DOM' };
      const buttons = Array.from(fb.querySelectorAll('button'));
      const edit = buttons.find((b) => (b.textContent || '').trim().startsWith('Edit'));
      if (!edit) {
        return {
          found: false,
          reason: 'no Edit <button> in fallback',
          buttonTexts: buttons.map((b) => (b.textContent || '').trim()),
        };
      }
      const rect = edit.getBoundingClientRect();
      const cs = window.getComputedStyle(edit);
      return {
        found: true,
        width: rect.width,
        height: rect.height,
        left: rect.left,
        display: cs.getPropertyValue('display'),
        visibility: cs.getPropertyValue('visibility'),
      };
    });

    if (!editBtn.found) {
      return {
        status: 'failed',
        message: `${testLabel}: FAIL (CLEANUP-01) — no "Edit" button in .recipe-actions-inline-fallback (${editBtn.reason}; buttons=[${(editBtn.buttonTexts || []).join(', ')}]).`,
      };
    }
    // The Edit button must be rendered (non-zero size) and not clipped off the
    // left edge — the precise CLEANUP-01 regression (it used to be cut off the
    // left of a no-wrap flex-end row).
    if (editBtn.width <= 0 || editBtn.height <= 0 || editBtn.display === 'none' || editBtn.visibility === 'hidden') {
      return {
        status: 'failed',
        message: `${testLabel}: FAIL (CLEANUP-01) — "Edit" button present but not rendered (w=${editBtn.width} h=${editBtn.height} display=${editBtn.display} visibility=${editBtn.visibility}).`,
      };
    }
    if (editBtn.left < 0) {
      return {
        status: 'failed',
        message: `${testLabel}: FAIL (CLEANUP-01) — "Edit" button is clipped off the left edge (left=${editBtn.left}px). This is the regression CLEANUP-01 was meant to fix.`,
      };
    }
    console.log(`[test7] "Edit" button present + rendered in fallback (w=${Math.round(editBtn.width)}px left=${Math.round(editBtn.left)}px) — PASS (CLEANUP-01)`);

    // ── Assertion 4 (CLEANUP-02): .recipe-hero collapses to a single column ──
    const heroGrid = await page.evaluate(() => {
      const el = document.querySelector('.recipe-hero');
      if (!el) return { found: false, columns: null };
      const computed = window.getComputedStyle(el);
      return { found: true, columns: computed.getPropertyValue('grid-template-columns') };
    });

    if (!heroGrid.found) {
      return {
        status: 'failed',
        message: `${testLabel}: FAIL (CLEANUP-02) — .recipe-hero element not found on the page. Is plan 11-02 applied?`,
      };
    }

    // A single-column grid resolves to one track value (e.g. "407px" — the
    // browser resolves "1fr" to pixels). A two-column "1fr 1fr" resolves to two
    // space-separated tokens. Single track ⇒ exactly one token.
    if (!isSingleTrack(heroGrid.columns)) {
      return {
        status: 'failed',
        message: `${testLabel}: FAIL (CLEANUP-02) — .recipe-hero grid-template-columns="${heroGrid.columns}" (expected single-track / 1fr at ${NARROW_WIDTH}px).`,
      };
    }
    console.log(`[test7] .recipe-hero single-column at ${NARROW_WIDTH}px (columns="${heroGrid.columns}") — PASS (CLEANUP-02)`);

    return {
      status: 'passed',
      message: `${testLabel}: PASS — TopBar slot hidden, fallback visible, Edit button rendered (not clipped), hero single-column at ${NARROW_WIDTH}px.`,
    };
  } catch (err) {
    await screenshot(page, 'test7-FAIL.png').catch(() => {});
    return {
      status: 'failed',
      message: `${testLabel}: FAIL (unexpected error) — ${err.message}`,
    };
  }
}

/**
 * Determine if a computed grid-template-columns value represents a single track.
 *
 * The browser resolves "1fr" to a pixel value like "407px". A two-column
 * "1fr 1fr" resolves to two space-separated tokens. We treat the value as
 * single-track if it contains exactly one CSS length/fr/auto token.
 *
 * @param {string|null} cols
 * @returns {boolean}
 */
function isSingleTrack(cols) {
  if (!cols) return false;
  if (cols === 'none') return true; // not a grid — treat as single-column fallback
  const tokens = cols.trim().split(/\s+/);
  return tokens.length === 1;
}
