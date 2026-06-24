/**
 * tests/test16-integration.mjs — Phase 16 UAT + Integration (v1.4 hands-free harness).
 *
 * Closes the automatable slice of the Phase 15 (Nutrition) and Phase 13 (Export) human-UAT
 * backlogs, plus the Phase 16 cross-theme integration check (nutrition wired into JSON-LD).
 * Runs against the live app on :7000 with a real Playwright/chromium session.
 *
 * WHY A THROWAWAY RECIPE
 * ----------------------
 * Like test14, this creates a dedicated recipe ("UAT v1.4 Integration (auto)") via the
 * editor's Paste-raw seam, runs every assertion on it, then deletes it. Seeded recipes are
 * never mutated. The recipe carries five ingredients — four CNF-matchable staples plus one
 * deliberately-unmatchable item ("edible gold flake") so the "--" unmatched path is exercised.
 *
 * COVERAGE (maps to 15-HUMAN-UAT.md and 13/INTEROP)
 * ------------------------------------------------
 *   A. Nutrition State 1: CTA + "not yet calculated" + disclaimer; JSON-LD has NO nutrition key
 *      (15-UAT items 1, 9, 13-before, 15)
 *   B. CTA → State 2: macro grid (Energy/Protein/Carbs/Fat), "Matched N of M ingredients",
 *      "--" for the unmatched ingredient, Per-serving/Total toggle (15-UAT items 2, 4, 5, 6)
 *   C. JSON-LD after compute carries nutrition.calories (NUTR-06 / SC5; 15-UAT item 13-after)
 *      — this is also the achievable half of Phase 16 SC2 cross-theme integration
 *   D. Cooklang "Export as .cook" downloads a non-empty .cook with @ingredient tokens
 *      (INTEROP-02 / Phase 13)
 *
 * KNOWN GAP (documented, not silently skipped)
 * --------------------------------------------
 * Phase 16 SC2 also wants the gallery hero as a Schema.org `image`. The JSON-LD projector
 * emits `image` ONLY for an absolute https URL; on a localhost http host every recipe photo
 * resolves to a non-https path and is omitted by design (P8). So the `image` half cannot be
 * verified locally — it needs a deployed https host. The nutrition half (above) IS verified.
 *
 * @param {import('playwright').Page} page
 * @param {object} opts
 * @param {number} [opts.cookbookId=1] - A cookbook owned by the session user (Noah owns 1).
 */

import { BASE_URL, screenshot } from '../lib/app.mjs';

const RECIPE_NAME = 'UAT v1.4 Integration (auto)';

// Four CNF-matchable staples + one unmatchable ("edible gold flake") to drive the "--" path.
const YAML_RECIPE = `---
name: "${RECIPE_NAME}"
servings: 4
ingredients:
  - id: 1
    name: "all-purpose flour"
    amount: 2
    unit: "cups"
  - id: 2
    name: "butter"
    amount: 100
    unit: "g"
  - id: 3
    name: "granulated sugar"
    amount: 1
    unit: "cup"
  - id: 4
    name: "egg"
    amount: 2
    unit: ""
  - id: 5
    name: "edible gold flake"
    amount: 1
    unit: "pinch"
steps:
  - text: "Mix [all-purpose flour](#1), [butter](#2), [granulated sugar](#3), and [egg](#4); bake until golden."
---
`;

const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

/** Click the first VISIBLE button matching an accessible name. */
async function clickVisible(page, nameRe) {
  const btns = page.getByRole('button', { name: nameRe });
  const count = await btns.count();
  for (let i = 0; i < count; i++) {
    const b = btns.nth(i);
    if (await b.isVisible().catch(() => false)) {
      await b.click();
      return true;
    }
  }
  return false;
}

/** Delete every recipe card with the given name in a cookbook (handles re-run leftovers). */
async function deleteRecipeByName(page, cookbookId, name) {
  for (let guard = 0; guard < 6; guard++) {
    await page.goto(`${BASE_URL}/cookbooks/${cookbookId}`, { waitUntil: 'networkidle', timeout: 30_000 });
    await sleep(600);
    const del = page.locator(`button[aria-label="Delete ${name}"]`).first();
    if ((await del.count()) === 0) return guard > 0;
    await del.click();
    // Scope the confirm to the dialog: the page also has a cookbook-level "Delete" button,
    // so a page-wide /^Delete$/ + .last() can click the wrong one and never confirm the
    // recipe delete (this is why throwaway recipes used to accumulate). The CbDialog host
    // is [role="dialog"]; the ConfirmDialog's confirm button is the only "Delete" inside it.
    const confirm = page.locator('[role="dialog"]').getByRole('button', { name: /^Delete$/ }).last();
    await confirm.waitFor({ state: 'visible', timeout: 8_000 }).catch(() => {});
    await confirm.click().catch(() => {});
    await sleep(1000);
  }
  return true;
}

/** Read and JSON.parse the rendered ld+json <script> (post-hydration head). Returns null on miss/parse-fail. */
async function readJsonLd(page) {
  const raw = await page
    .locator('script[type="application/ld+json"]')
    .first()
    .textContent()
    .catch(() => null);
  if (!raw) return null;
  try {
    return JSON.parse(raw);
  } catch {
    return null;
  }
}

export async function runTest16(page, { cookbookId = 1 } = {}) {
  const items = [];
  const rec = (key, title, status, detail) => {
    items.push({ key, title, status, detail });
    const icon = status === 'passed' ? 'PASS' : status === 'skipped' ? 'SKIP' : 'FAIL';
    console.log(`[test16] ${key} (${title}): ${icon} — ${detail}`);
  };
  let recipeId = null;

  try {
    // ── Setup: clean any leftover, then create a fresh throwaway recipe ──────────
    console.log('\n[test16] Phase 16 v1.4 integration UAT — setup');
    await deleteRecipeByName(page, cookbookId, RECIPE_NAME);

    await page.goto(`${BASE_URL}/cookbooks/${cookbookId}/recipes/new`, {
      waitUntil: 'networkidle',
      timeout: 30_000,
    });
    await page.waitForSelector('input[aria-label="Recipe title"]', { timeout: 20_000 });

    if (!(await clickVisible(page, /^Paste raw text$/))) {
      throw new Error('Could not find the "Paste raw text" button on the new-recipe editor.');
    }
    const rawArea = page.locator('textarea[placeholder^="Paste or write"]');
    await rawArea.waitFor({ state: 'visible', timeout: 10_000 });
    await rawArea.fill(YAML_RECIPE);
    if (!(await clickVisible(page, /^Import$/))) {
      throw new Error('Could not find the "Import" button in the Paste-raw dialog.');
    }
    await page.waitForFunction(
      (name) => {
        const el = document.querySelector('input[aria-label="Recipe title"]');
        return el && el.value === name;
      },
      RECIPE_NAME,
      { timeout: 10_000 }
    );

    const cbSel = page.locator('aside select.cb-select').first();
    if ((await cbSel.count()) > 0) {
      const opts = await cbSel.locator('option').all();
      for (const o of opts) {
        if ((await o.getAttribute('value')) === String(cookbookId)) {
          await cbSel.selectOption(String(cookbookId)).catch(() => {});
          break;
        }
      }
    }

    if (!(await clickVisible(page, /^Save$/))) {
      throw new Error('No visible Save button on the new-recipe editor.');
    }
    await page
      .waitForURL((u) => /\/cookbooks\/\d+(?:$|[/?#])/.test(u.toString()), { timeout: 15_000 })
      .catch(() => {});
    await sleep(800);

    await page.goto(`${BASE_URL}/cookbooks/${cookbookId}`, { waitUntil: 'networkidle', timeout: 30_000 });
    const card = page.locator('.cb-card[role="button"]').filter({ hasText: RECIPE_NAME }).first();
    await card.waitFor({ state: 'visible', timeout: 15_000 });
    await card.click();
    await page
      .waitForURL((u) => /\/recipes\/\d+(?:$|[/?#])/.test(u.toString()), { timeout: 15_000 })
      .catch(() => {});
    const m = page.url().match(/\/recipes\/(\d+)/);
    if (!m) throw new Error(`Could not resolve the new recipe id (url=${page.url()}).`);
    recipeId = parseInt(m[1], 10);
    console.log(`[test16] Throwaway recipe created: id=${recipeId}`);

    const nutritionSection = page.locator('section[aria-label="Estimated nutrition"]');
    const disclaimer = page.locator('[role="note"][aria-label="Nutrition data notice"]');
    const DISCLAIMER_RE = /Estimated nutrition — not suitable for medical dietary planning\. Data: Health Canada, Canadian Nutrient File \(2015\)\./;

    // ── A: Nutrition State 1 — CTA, "not yet calculated", disclaimer; JSON-LD has no nutrition ──
    try {
      await nutritionSection.waitFor({ state: 'visible', timeout: 15_000 });
      const hasCta = await nutritionSection.getByRole('button', { name: /^Calculate nutrition$/ }).isVisible();
      const notYet = await nutritionSection.getByText('Nutrition not yet calculated.').isVisible();
      const discText = (await disclaimer.textContent().catch(() => '')) || '';
      const discOk = DISCLAIMER_RE.test(discText);
      // Macro grid must NOT be present yet.
      const macroBefore = await nutritionSection.getByText('· Energy').count();
      if (hasCta && notYet && discOk && macroBefore === 0) {
        rec('A', 'Nutrition State 1 (CTA + disclaimer, no macros)', 'passed',
          'CTA + "Nutrition not yet calculated." + exact disclaimer present; no macro grid pre-compute.');
      } else {
        rec('A', 'Nutrition State 1 (CTA + disclaimer, no macros)', 'failed',
          `cta=${hasCta} notYet=${notYet} disclaimer=${discOk} macrosPresent=${macroBefore}.`);
      }
    } catch (e) {
      rec('A', 'Nutrition State 1 (CTA + disclaimer, no macros)', 'failed', e.message);
    }

    // ── A2: JSON-LD present + structurally valid + nutrition key ABSENT pre-compute ──
    let jsonLdBefore = null;
    try {
      jsonLdBefore = await readJsonLd(page);
      const isRecipe = jsonLdBefore && (jsonLdBefore['@type'] === 'Recipe' || Array.isArray(jsonLdBefore['@graph']));
      const hasName = jsonLdBefore && typeof jsonLdBefore.name === 'string' && jsonLdBefore.name.length > 0;
      const noNutrition = jsonLdBefore && jsonLdBefore.nutrition === undefined;
      if (jsonLdBefore && isRecipe && hasName && noNutrition) {
        rec('A2', 'JSON-LD valid + nutrition absent pre-compute', 'passed',
          `ld+json parses; @type Recipe; name="${jsonLdBefore.name}"; nutrition key absent.`);
      } else {
        rec('A2', 'JSON-LD valid + nutrition absent pre-compute', 'failed',
          `parsed=${!!jsonLdBefore} isRecipe=${isRecipe} hasName=${hasName} noNutrition=${noNutrition}.`);
      }
    } catch (e) {
      rec('A2', 'JSON-LD valid + nutrition absent pre-compute', 'failed', e.message);
    }

    // ── B: CTA → State 2 — macro grid, coverage line, "--" unmatched, Per/Total toggle ──
    try {
      await nutritionSection.getByRole('button', { name: /^Calculate nutrition$/ }).click();
      // Wait for State 2: the macro grid label "· Energy" appears (or an error banner).
      const reachedState2 = await nutritionSection
        .getByText('· Energy')
        .first()
        .waitFor({ state: 'visible', timeout: 30_000 })
        .then(() => true)
        .catch(() => false);

      if (!reachedState2) {
        const errored = await nutritionSection.getByText('Nutrition calculation failed').isVisible().catch(() => false);
        rec('B', 'Compute → State 2 (macros + coverage + "--")', 'failed',
          errored ? 'Compute reached the error state instead of State 2.' : 'Macro grid did not appear within 30s.');
      } else {
        // Macro labels (all four present).
        const macroLabels = await Promise.all(
          ['· Energy', '· Protein', '· Carbs', '· Fat'].map((t) =>
            nutritionSection.getByText(t).first().isVisible().catch(() => false)
          )
        );
        const allMacros = macroLabels.every(Boolean);

        // First .num value = per-serving energy.
        const energyText = (await nutritionSection.locator('.num').first().textContent().catch(() => '')) || '';
        const energyNum = parseFloat(energyText.replace(/[^\d.]/g, ''));
        const energyOk = Number.isFinite(energyNum);

        // Coverage line "Matched N of M ingredients" — M must equal the 5 ingredients.
        const coverageText =
          (await nutritionSection.getByText(/Matched \d+ of \d+ ingredients/).first().textContent().catch(() => '')) || '';
        const cm = coverageText.match(/Matched (\d+) of (\d+) ingredients/);
        const matched = cm ? parseInt(cm[1], 10) : -1;
        const total = cm ? parseInt(cm[2], 10) : -1;
        const coverageOk = total === 5 && matched >= 1 && matched <= total;

        // Unmatched "--": the "edible gold flake" row should render "--" (never "0").
        const goldRowVisible = await nutritionSection.getByText('edible gold flake').isVisible().catch(() => false);
        const sectionText = (await nutritionSection.textContent().catch(() => '')) || '';
        const dashPresent = /--/.test(sectionText);
        const unmatchedOk = goldRowVisible && dashPresent;

        // Per-serving / Total toggle: Total flips aria-checked AND energy value changes (servings=4).
        const totalRadio = nutritionSection.getByRole('radio', { name: 'Total' });
        await totalRadio.click();
        await sleep(300);
        const totalChecked = (await totalRadio.getAttribute('aria-checked')) === 'true';
        const energyTotalText = (await nutritionSection.locator('.num').first().textContent().catch(() => '')) || '';
        const energyTotal = parseFloat(energyTotalText.replace(/[^\d.]/g, ''));
        // Total >= per-serving (equal only if both ~0); for a matched recipe they differ.
        const toggleOk = totalChecked && Number.isFinite(energyTotal) && energyTotal >= energyNum;
        // Restore Per-serving for any downstream read.
        await nutritionSection.getByRole('radio', { name: 'Per serving' }).click().catch(() => {});

        const pass = allMacros && energyOk && coverageOk && unmatchedOk && toggleOk;
        rec('B', 'Compute → State 2 (macros + coverage + "--")', pass ? 'passed' : 'failed',
          `macros=${allMacros} energy=${energyText.trim()} coverage="${coverageText.trim()}" ` +
          `unmatched(gold/"--")=${goldRowVisible}/${dashPresent} toggle(total=${energyTotal})=${toggleOk}.`);
      }
    } catch (e) {
      rec('B', 'Compute → State 2 (macros + coverage + "--")', 'failed', e.message);
    }

    // ── B2: "Show all N matches" expands/collapses the matched (HIGH) coverage rows (15-UAT item 8) ──
    try {
      const showAll = nutritionSection.getByRole('button', { name: /^Show all \d+ matches$/ });
      if (await showAll.isVisible().catch(() => false)) {
        const badgesBefore = await nutritionSection.locator('.cb-badge').count();
        await showAll.click();
        await sleep(200);
        const hideBtn = nutritionSection.getByRole('button', { name: /^Hide matched$/ });
        const expanded = await hideBtn.isVisible().catch(() => false);
        const badgesAfter = await nutritionSection.locator('.cb-badge').count();
        await hideBtn.click().catch(() => {}); // collapse again for a clean final state
        rec('B2', 'Show-all-matches toggle (item 8)', expanded && badgesAfter >= badgesBefore ? 'passed' : 'failed',
          `expanded=${expanded} coverageRows ${badgesBefore} → ${badgesAfter} (HIGH matches hidden by default).`);
      } else {
        rec('B2', 'Show-all-matches toggle (item 8)', 'skipped',
          'No "Show all matches" button (0 energy-bearing matches for this recipe).');
      }
    } catch (e) {
      rec('B2', 'Show-all-matches toggle (item 8)', 'failed', e.message);
    }

    await screenshot(page, 'test16-nutrition-state2.png');

    // ── C: JSON-LD after compute carries nutrition.calories (NUTR-06 / Phase 16 SC2 half) ──
    try {
      // The CTA rebuilds _jsonLd; give HeadContent a beat to re-render.
      await sleep(400);
      const jsonLdAfter = await readJsonLd(page);
      const nut = jsonLdAfter && jsonLdAfter.nutrition;
      const typeOk = nut && nut['@type'] === 'NutritionInformation';
      const calOk = nut && typeof nut.calories === 'string' && /\d/.test(nut.calories);
      if (jsonLdAfter && typeOk && calOk) {
        rec('C', 'JSON-LD nutrition.calories after compute (NUTR-06 / SC2)', 'passed',
          `nutrition.@type=NutritionInformation; calories="${nut.calories}".`);
      } else {
        rec('C', 'JSON-LD nutrition.calories after compute (NUTR-06 / SC2)', 'failed',
          `parsed=${!!jsonLdAfter} hasNutrition=${!!nut} typeOk=${typeOk} caloriesOk=${calOk}.`);
      }
    } catch (e) {
      rec('C', 'JSON-LD nutrition.calories after compute (NUTR-06 / SC2)', 'failed', e.message);
    }

    // Document the unverifiable half of SC2 rather than silently skipping it.
    rec('C2', 'JSON-LD image (gallery hero) cross-theme', 'skipped',
      'Projector emits `image` only for an absolute https URL (P8). On a localhost http host every photo path is non-https and omitted by design — needs a deployed https host to verify.');

    // ── D: Cooklang "Export as .cook" produces a non-empty .cook with @ingredient tokens ──
    // We capture the base64 payload the app hands to window.cookBotDownloadFile rather than
    // intercept the browser download: download.js revokes the blob URL synchronously after the
    // anchor click, so the Playwright download artifact is gone before path()/saveAs() can read
    // it (ENOENT). Wrapping cookBotDownloadFile inspects the actual exported bytes (the
    // CooklangRecipeProjector output reaching the download seam) deterministically.
    try {
      await page.evaluate(() => {
        window.__cookCapture = null;
        const orig = window.cookBotDownloadFile;
        window.cookBotDownloadFile = (fileName, mimeType, base64) => {
          window.__cookCapture = { fileName, mimeType, base64 };
          // Intentionally do NOT call orig — avoids the real download + the blob-revoke race.
          return orig === undefined ? undefined : undefined;
        };
      });
      const clicked = await clickVisible(page, /^Export as \.cook$/);
      if (!clicked) {
        rec('D', 'Cooklang .cook export (INTEROP-02)', 'failed', 'No visible "Export as .cook" button on RecipeView.');
      } else {
        await page.waitForFunction(() => window.__cookCapture !== null, { timeout: 10_000 });
        const cap = await page.evaluate(() => window.__cookCapture);
        const content = cap && cap.base64 ? Buffer.from(cap.base64, 'base64').toString('utf8') : '';
        const nameOk = /\.cook$/.test(cap?.fileName ?? '');
        const nonEmpty = content.trim().length > 0;
        const hasIngredientTokens = /@/.test(content) && /flour/i.test(content);
        if (nameOk && nonEmpty && hasIngredientTokens) {
          rec('D', 'Cooklang .cook export (INTEROP-02)', 'passed',
            `Exported "${cap.fileName}" (${content.length} bytes) with @ingredient tokens.`);
        } else {
          rec('D', 'Cooklang .cook export (INTEROP-02)', 'failed',
            `file="${cap?.fileName}" nameOk=${nameOk} nonEmpty=${nonEmpty} hasTokens=${hasIngredientTokens}.`);
        }
      }
    } catch (e) {
      rec('D', 'Cooklang .cook export (INTEROP-02)', 'failed', e.message);
    }
  } catch (err) {
    await screenshot(page, 'test16-FAIL-setup.png').catch(() => {});
    rec('setup', 'Setup', 'failed', err.message);
  } finally {
    try {
      await deleteRecipeByName(page, cookbookId, RECIPE_NAME);
      console.log('[test16] cleanup: throwaway recipe deleted.');
    } catch (e) {
      console.warn(`[test16] cleanup: could not delete throwaway recipe: ${e.message}`);
    }
  }

  // ── Aggregate ─────────────────────────────────────────────────────────────────
  const failed = items.filter((i) => i.status === 'failed');
  const passed = items.filter((i) => i.status === 'passed');
  const skipped = items.filter((i) => i.status === 'skipped');
  const lines = items
    .map((i) => `    [${i.status === 'passed' ? 'PASS' : i.status === 'skipped' ? 'SKIP' : 'FAIL'}] ${i.key}: ${i.title} — ${i.detail}`)
    .join('\n');

  return {
    status: failed.length > 0 ? 'failed' : 'passed',
    message:
      `Phase 16 v1.4 integration UAT: ${passed.length} passed, ${skipped.length} skipped, ${failed.length} failed` +
      (recipeId ? ` (throwaway recipe ${recipeId}, cleaned up)` : '') +
      `\n${lines}`,
    items,
  };
}
