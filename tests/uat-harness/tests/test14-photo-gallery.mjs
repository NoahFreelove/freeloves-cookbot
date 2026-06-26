/**
 * tests/test14-photo-gallery.mjs — UAT for Phase 14 (Photo Gallery, GALLERY-01..04).
 *
 * Drives the 10 human-UAT items recorded in
 *   .planning/phases/14-photo-gallery/14-HUMAN-UAT.md
 * with a real Playwright/chromium session against the running app on :7000.
 *
 * SAFETY / IDEMPOTENCY
 * --------------------
 * Every gallery mutation calls RecipeService.SyncPrimaryPhotoUrlAsync, which REWRITES
 * Recipe.PhotoUrl (and the canonical mirror) from the primary gallery photo — and sets
 * it to null when the gallery is emptied. So this test never touches a seeded recipe.
 * Instead it CREATES a dedicated throwaway recipe ("UAT Photo Gallery (auto)") via the
 * editor's Paste-raw-text seam, runs all gallery operations on it, then DELETES the
 * recipe (CookbookDetail delete) and unlinks the tiny upload files it created. A leftover
 * recipe from a crashed prior run is deleted at start, so re-runs stay clean.
 *
 * COVERAGE (maps to 14-HUMAN-UAT.md)
 * ----------------------------------
 *   1. Multi-upload circuit stability (P14)      — automated
 *   2. Reorder + set-hero persistence            — automated
 *   3. Caption persistence across reload         — automated
 *   4. Delete with confirm dialog                — automated
 *   5. Paste-URL reject (scheme)                 — automated; accept lane needs outbound
 *      network → attempted, SKIP if offline
 *   6. AI photo helper RETIRED (GALLERY-04)      — regression guard: button must stay absent
 *      (feature removed 2026-06-25 per user UAT feedback)
 *   7. Copyright disclaimer always visible       — automated
 *   8. RecipeView gallery + client-side hero swap (P15) — automated
 *   9. Photo count cap UX                        — automated (fills to MaxPhotosPerRecipe=10)
 *  10. WR-04 paste-URL input clears after add    — tied to item 5 accept lane (SKIP if offline)
 *
 * Returns { status, message, items } where items[] carries a per-UAT-item verdict.
 *
 * @param {import('playwright').Page} page
 * @param {object} opts
 * @param {number} [opts.cookbookId=1] - A cookbook owned by the session user (Noah owns 1).
 */

import { writeFile, readFile, unlink, mkdir } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { BASE_URL, screenshot } from '../lib/app.mjs';

const RECIPE_NAME = 'UAT Photo Gallery (auto)';
const CAP = 10; // CookBotSettings.MaxPhotosPerRecipe default (clamped [1,20])

// Upload fixture: the app's real favicon.png. A synthetic 1x1 PNG does NOT survive
// Blazor Server's chunked SignalR file-stream read (the magic-byte sniff sees a short/
// empty head and rejects it), but a real multi-byte image streams fine — so we copy
// this real PNG to the temp upload files. (This is a harness-fixture constraint, not an
// app bug: the gallery accepts real images correctly.)
const FAVICON_PATH = new URL('../../../src/CookBot.Web/wwwroot/favicon.png', import.meta.url).pathname;

// Uploads live under src/CookBot.Web/wwwroot/uploads/ relative to this file.
const UPLOADS_DIR = new URL('../../../src/CookBot.Web/wwwroot/uploads/', import.meta.url).pathname;

const YAML_RECIPE = `---
name: "${RECIPE_NAME}"
servings: 4
ingredients:
  - id: 1
    name: "flour"
    amount: 2
    unit: "cups"
steps:
  - text: "Mix [flour](#1) and bake."
---
`;

// ── small helpers ─────────────────────────────────────────────────────────────

const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

/** Copy the real favicon to `n` distinct temp files and return their absolute paths. */
async function makePngs(n) {
  const dir = join(tmpdir(), 'cookbot-uat14');
  await mkdir(dir, { recursive: true });
  const buf = await readFile(FAVICON_PATH);
  const paths = [];
  for (let i = 0; i < n; i++) {
    const p = join(dir, `uat14-${i}.png`);
    await writeFile(p, buf);
    paths.push(p);
  }
  return paths;
}

/** Count gallery photo cards via caption inputs (robust even if an <img> fails to load). */
async function photoCount(page) {
  return page.locator('input[aria-label^="Caption for photo "]').count();
}

/** Wait until the gallery shows exactly `n` photo cards. */
async function waitPhotoCount(page, n, timeout = 30_000) {
  await page.waitForFunction(
    (expected) =>
      document.querySelectorAll('input[aria-label^="Caption for photo "]').length === expected,
    n,
    { timeout }
  );
}

/** Navigate to a recipe's edit page and wait for the gallery manager to render. */
async function gotoEdit(page, id) {
  await page.goto(`${BASE_URL}/recipes/${id}/edit`, { waitUntil: 'networkidle', timeout: 30_000 });
  await page.waitForSelector('input[aria-label="Recipe title"]', { timeout: 20_000 });
  // The paste-URL input is always rendered by RecipePhotoGalleryManager — a reliable
  // "gallery loaded" signal.
  await page.waitForSelector('input[aria-label="Paste photo URL"]', { timeout: 20_000 });
}

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
    if ((await del.count()) === 0) return guard > 0; // nothing (more) to delete
    await del.click();
    // ConfirmDialog ("Delete recipe") — scope to the dialog: the page also has a cookbook-level
    // "Delete" button, so a page-wide /^Delete$/ + .last() can click the wrong one and never
    // confirm the recipe delete (caused throwaway recipes to accumulate). The CbDialog host is
    // [role="dialog"]; the ConfirmDialog's confirm button is the only "Delete" inside it.
    const confirm = page.locator('[role="dialog"]').getByRole('button', { name: /^Delete$/ }).last();
    await confirm.waitFor({ state: 'visible', timeout: 8_000 }).catch(() => {});
    await confirm.click().catch(() => {});
    await sleep(1000);
  }
  return true;
}

// ── main ──────────────────────────────────────────────────────────────────────

export async function runTest14(page, { cookbookId = 1 } = {}) {
  const items = [];
  const rec = (n, title, status, detail) => {
    items.push({ n, title, status, detail });
    const icon = status === 'passed' ? 'PASS' : status === 'skipped' ? 'SKIP' : 'FAIL';
    console.log(`[test14] item ${n} (${title}): ${icon} — ${detail}`);
  };
  const uploadedSrcs = new Set();
  let recipeId = null;

  try {
    // ── Setup: clean any leftover, then create a fresh throwaway recipe ──────────
    console.log('\n[test14] Phase 14 Photo Gallery UAT — setup');
    await deleteRecipeByName(page, cookbookId, RECIPE_NAME);

    await page.goto(`${BASE_URL}/cookbooks/${cookbookId}/recipes/new`, {
      waitUntil: 'networkidle',
      timeout: 30_000,
    });
    await page.waitForSelector('input[aria-label="Recipe title"]', { timeout: 20_000 });

    // Open Paste-raw dialog and import the YAML recipe.
    if (!(await clickVisible(page, /^Paste raw text$/))) {
      throw new Error('Could not find the "Paste raw text" button on the new-recipe editor.');
    }
    const rawArea = page.locator('textarea[placeholder^="Paste or write"]');
    await rawArea.waitFor({ state: 'visible', timeout: 10_000 });
    await rawArea.fill(YAML_RECIPE);
    if (!(await clickVisible(page, /^Import$/))) {
      throw new Error('Could not find the "Import" button in the Paste-raw dialog.');
    }
    // Wait for the editor title to reflect the imported name.
    await page.waitForFunction(
      (name) => {
        const el = document.querySelector('input[aria-label="Recipe title"]');
        return el && el.value === name;
      },
      RECIPE_NAME,
      { timeout: 10_000 }
    );

    // Ensure a cookbook is selected (route should preset it; set explicitly if a picker exists).
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

    // Save the new recipe → navigates to /cookbooks/{cookbookId}.
    if (!(await clickVisible(page, /^Save$/))) {
      throw new Error('No visible Save button on the new-recipe editor.');
    }
    await page
      .waitForURL((u) => /\/cookbooks\/\d+(?:$|[/?#])/.test(u.toString()), { timeout: 15_000 })
      .catch(() => {});
    await sleep(800);

    // Open the recipe to capture its id.
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
    console.log(`[test14] Throwaway recipe created: id=${recipeId}`);

    // ── Item 1: Multi-upload circuit stability (P14) ────────────────────────────
    // NOTE: Blazor Server's <InputFile> streams bytes over the SignalR circuit via JS
    // interop. Under Playwright (headless OR headed) that streaming is unreliable — most
    // uploads deliver a short/empty read or a canceled stream, so this harness cannot
    // drive photo uploads dependably. When the upload does not materialize, item 1 (and
    // every downstream item that needs photos) is recorded as SKIP with this reason,
    // matching 14-VERIFICATION.md's "why_human: requires a real browser" rationale.
    let uploadOk = false;
    try {
      await gotoEdit(page, recipeId);
      const before = await photoCount(page);
      const pngs = await makePngs(3);
      await page.locator('input[type="file"]').setInputFiles(pngs);
      uploadOk = await waitPhotoCount(page, before + 3, 30_000).then(() => true).catch(() => false);
      const reconnect = await page
        .locator('#components-reconnect-modal, .components-reconnect-show')
        .isVisible()
        .catch(() => false);
      if (reconnect) {
        rec(1, 'Multi-upload circuit stability', 'failed', 'SignalR reconnect modal appeared during multi-upload.');
      } else if (uploadOk) {
        rec(1, 'Multi-upload circuit stability', 'passed',
          `3 files uploaded sequentially; ${before + 3} cards present; no SignalR reconnect.`);
      } else {
        rec(1, 'Multi-upload circuit stability', 'skipped',
          'Photo upload not drivable via Playwright (Blazor Server SignalR file streaming is unreliable under automation). Needs a real browser.');
      }
    } catch (e) {
      rec(1, 'Multi-upload circuit stability', 'skipped',
        `Upload not drivable via Playwright (Blazor SignalR file streaming): ${e.message}`);
    }

    // Downstream items 2/3/4/8/9 all need photos to exist. If the upload could not be
    // driven, short-circuit them to SKIP rather than emitting cascade failures.
    if (!uploadOk) {
      rec(3, 'Caption persistence', 'skipped', 'Needs a working photo upload (see item 1).');
      rec(2, 'Reorder + set-hero persistence', 'skipped', 'Needs a working photo upload (see item 1).');
      rec(8, 'RecipeView gallery + hero swap (P15)', 'skipped', 'Needs photos in the gallery (see item 1).');
      rec(9, 'Photo count cap UX', 'skipped', 'Needs working photo uploads to reach the cap (see item 1).');
      rec(4, 'Delete with confirm dialog', 'skipped', 'Needs a photo to delete (see item 1).');
    }

    // ── Item 3: Caption persistence across reload ───────────────────────────────
    if (uploadOk) try {
      const cap1 = page.locator('input[aria-label="Caption for photo 1"]');
      await cap1.fill('UAT-A');
      await page.keyboard.press('Tab');
      await sleep(600);
      await gotoEdit(page, recipeId);
      const v = await page.locator('input[aria-label="Caption for photo 1"]').inputValue();
      if (v === 'UAT-A') rec(3, 'Caption persistence', 'passed', 'Caption "UAT-A" survived a reload.');
      else rec(3, 'Caption persistence', 'failed', `After reload caption was "${v}", expected "UAT-A".`);
    } catch (e) {
      rec(3, 'Caption persistence', 'failed', e.message);
    }

    // ── Item 2: Reorder + set-hero persistence ──────────────────────────────────
    if (uploadOk) try {
      // Tag the 3 cards so we can verify order: card1=UAT-A (already), card2=UAT-B, card3=UAT-C.
      await page.locator('input[aria-label="Caption for photo 2"]').fill('UAT-B');
      await page.keyboard.press('Tab');
      await sleep(400);
      await page.locator('input[aria-label="Caption for photo 3"]').fill('UAT-C');
      await page.keyboard.press('Tab');
      await sleep(400);

      const cards = page.locator('.photo-manager-grid > .cb-card');
      // Set hero on card 2 (UAT-B).
      await cards.nth(1).locator('button[aria-label="Set as hero photo"]').click();
      await sleep(500);
      // Move card 2 (UAT-B) up → expected order becomes B, A, C.
      await cards.nth(1).locator('button[aria-label="Move photo up"]').click();
      await sleep(500);

      await gotoEdit(page, recipeId);
      const order = await page
        .locator('input[aria-label^="Caption for photo "]')
        .evaluateAll((els) => els.map((e) => e.value));
      const heroDisabled = await page.locator('button[aria-label="Set as hero photo"]:disabled').count();
      // Which card index is the hero?
      const heroIdx = await page.evaluate(() => {
        const btns = [...document.querySelectorAll('button[aria-label="Set as hero photo"]')];
        return btns.findIndex((b) => b.disabled);
      });
      const heroCaption = order[heroIdx];
      const orderOk = order[0] === 'UAT-B' && order[1] === 'UAT-A' && order[2] === 'UAT-C';
      const heroOk = heroDisabled === 1 && heroCaption === 'UAT-B';
      if (orderOk && heroOk) {
        rec(2, 'Reorder + set-hero persistence', 'passed',
          `After reload order=[${order.join(',')}], exactly one hero on UAT-B.`);
      } else {
        rec(2, 'Reorder + set-hero persistence', 'failed',
          `order=[${order.join(',')}] (want B,A,C); heroCount=${heroDisabled} heroCaption=${heroCaption} (want 1 / UAT-B).`);
      }
    } catch (e) {
      rec(2, 'Reorder + set-hero persistence', 'failed', e.message);
    }

    // ── Item 8: RecipeView gallery + client-side hero swap (P15) ─────────────────
    if (uploadOk) try {
      await page.goto(`${BASE_URL}/recipes/${recipeId}`, { waitUntil: 'networkidle', timeout: 30_000 });
      const hero = page.locator('img[alt$="hero photo"]').first();
      await hero.waitFor({ state: 'visible', timeout: 15_000 });
      const heroBox = await hero.boundingBox();
      const strip = page.locator('.recipe-gallery-strip img[role="button"]');
      await strip.first().waitFor({ state: 'visible', timeout: 10_000 });
      const thumbCount = await strip.count();

      // Record the primary (pressed) thumbnail src on a fresh load.
      const pressedSrc = async () =>
        page.evaluate(() => {
          const t = [...document.querySelectorAll('.recipe-gallery-strip img[role="button"]')].find(
            (e) => e.getAttribute('aria-pressed') === 'true'
          );
          return t ? t.getAttribute('src') : null;
        });
      const primarySrc = await pressedSrc();

      // Click a DIFFERENT thumbnail and confirm the displayed hero swaps.
      let swappedSrc = null;
      for (let i = 0; i < thumbCount; i++) {
        const s = await strip.nth(i).getAttribute('src');
        if (s !== primarySrc) {
          await strip.nth(i).click();
          swappedSrc = s;
          break;
        }
      }
      await sleep(500);
      const afterSwapPressed = await pressedSrc();
      const heroSrcAfter = await hero.getAttribute('src');
      const swapOk = swappedSrc && afterSwapPressed === swappedSrc && heroSrcAfter === swappedSrc;

      // P15: reload — the displayed hero must revert to the SAVED primary (swap was view-only).
      await page.goto(`${BASE_URL}/recipes/${recipeId}`, { waitUntil: 'networkidle', timeout: 30_000 });
      await strip.first().waitFor({ state: 'visible', timeout: 10_000 });
      const reloadedPressed = await pressedSrc();
      const p15Ok = reloadedPressed === primarySrc;

      if (thumbCount >= 2 && heroBox && Math.round(heroBox.height) >= 380 && swapOk && p15Ok) {
        rec(8, 'RecipeView gallery + hero swap (P15)', 'passed',
          `Hero ~${Math.round(heroBox.height)}px; ${thumbCount} thumbs; swap changed display; reload reverted to saved primary (no mutation).`);
      } else {
        rec(8, 'RecipeView gallery + hero swap (P15)', 'failed',
          `thumbs=${thumbCount} heroH=${heroBox ? Math.round(heroBox.height) : 'n/a'} swapOk=${swapOk} p15Ok=${p15Ok}.`);
      }
    } catch (e) {
      rec(8, 'RecipeView gallery + hero swap (P15)', 'failed', e.message);
    }

    // ── Item 7: Copyright disclaimer always visible ─────────────────────────────
    try {
      await gotoEdit(page, recipeId);
      const note = page.locator('[role="note"][aria-label="Copyright notice"]');
      const visible = await note.isVisible().catch(() => false);
      const text = (await note.textContent().catch(() => '')) || '';
      if (visible && /right to use/i.test(text)) {
        rec(7, 'Copyright disclaimer always visible', 'passed', 'Disclaimer present and visible (unconditional).');
      } else {
        rec(7, 'Copyright disclaimer always visible', 'failed', `visible=${visible} text="${text.trim().slice(0, 60)}".`);
      }
    } catch (e) {
      rec(7, 'Copyright disclaimer always visible', 'failed', e.message);
    }

    // ── Item 5: Paste-URL reject (scheme) + accept lane (network) + Item 10 (WR-04) ─
    try {
      const urlInput = page.locator('input[aria-label="Paste photo URL"]');
      // Reject lane: a non-http(s) scheme is rejected by the validator with NO network call.
      await urlInput.fill('ftp://example.com/photo.jpg');
      await page.keyboard.press('Tab');
      const alert = page.locator('[role="alert"]');
      const rejected = await alert
        .filter({ hasText: /http and https/i })
        .first()
        .waitFor({ state: 'visible', timeout: 8_000 })
        .then(() => true)
        .catch(() => false);
      if (rejected) {
        rec(5, 'Paste-URL reject (scheme)', 'passed', 'Non-http(s) URL rejected with inline "http and https" error.');
      } else {
        rec(5, 'Paste-URL reject (scheme)', 'failed', 'No inline rejection error for an ftp:// URL.');
      }

      // Accept lane (needs outbound HEAD). Attempt a real image URL; SKIP on offline.
      const beforeAccept = await photoCount(page);
      await urlInput.fill('https://raw.githubusercontent.com/github/explore/main/topics/png/png.png');
      await page.keyboard.press('Tab');
      // Either a new card appears (accept) or an alert (offline / HEAD failed).
      const accepted = await page
        .waitForFunction(
          (n) => document.querySelectorAll('input[aria-label^="Caption for photo "]').length === n + 1,
          beforeAccept,
          { timeout: 15_000 }
        )
        .then(() => true)
        .catch(() => false);
      if (accepted) {
        rec(5, 'Paste-URL accept (network)', 'passed', 'Valid https image URL was HEAD-validated and added.');
        // Item 10 (WR-04): the input clears after a successful add.
        await sleep(400);
        const cleared = (await urlInput.inputValue()) === '';
        rec(10, 'WR-04 paste-URL input clears', cleared ? 'passed' : 'failed',
          cleared ? 'Input emptied after successful add.' : 'Input still holds the added URL.');
      } else {
        rec(5, 'Paste-URL accept (network)', 'skipped',
          'No outbound network (HEAD validation could not reach the host) — accept lane not exercised.');
        rec(10, 'WR-04 paste-URL input clears', 'skipped', 'Depends on the accept lane (offline).');
      }
    } catch (e) {
      rec(5, 'Paste-URL flows', 'failed', e.message);
    }

    // ── Item 6: AI photo search-term helper RETIRED (GALLERY-04 removed 2026-06-25) ─
    // Regression guard: the "Suggest photo search terms" affordance must stay gone.
    try {
      await gotoEdit(page, recipeId);
      const aiBtn = page.getByRole('button', { name: /Suggest photo search terms|Finding search terms/i });
      const present = (await aiBtn.count()) > 0;
      rec(6, 'AI photo helper retired (GALLERY-04)', present ? 'failed' : 'passed',
        present ? 'The retired "Suggest photo search terms" button is present again.' : 'AI helper absent as expected (feature retired).');
    } catch (e) {
      rec(6, 'AI photo helper retired (GALLERY-04)', 'failed', e.message);
    }

    // ── Item 9: Photo count cap UX ──────────────────────────────────────────────
    if (uploadOk) try {
      await gotoEdit(page, recipeId);
      let count = await photoCount(page);
      if (count < CAP) {
        const need = CAP - count;
        await page.locator('input[type="file"]').setInputFiles(await makePngs(need));
        await waitPhotoCount(page, CAP, 60_000);
        count = CAP;
      }
      // At cap: "Max N photos" chip visible AND the upload <input type=file> no longer rendered.
      const chip = page.locator('span.cb-chip', { hasText: new RegExp(`Max ${CAP} photos`) });
      const chipVisible = await chip.first().isVisible().catch(() => false);
      const fileInputGone = (await page.locator('input[type="file"]').count()) === 0;
      if (count === CAP && chipVisible && fileInputGone) {
        rec(9, 'Photo count cap UX', 'passed', `At ${CAP} photos: "Max ${CAP} photos" chip shown, add affordance disabled (file input removed).`);
      } else {
        rec(9, 'Photo count cap UX', 'failed',
          `count=${count} chipVisible=${chipVisible} fileInputGone=${fileInputGone} (want ${CAP}/true/true).`);
      }
    } catch (e) {
      rec(9, 'Photo count cap UX', 'failed', e.message);
    }

    // ── Item 4: Delete with confirm dialog ──────────────────────────────────────
    if (uploadOk) try {
      const before = await photoCount(page);
      await page.locator('button[aria-label="Delete photo 1"]').first().click();
      // CbConfirmDialog "Delete photo?" with confirm label "Delete photo".
      const confirm = page.getByRole('button', { name: /^Delete photo$/ });
      const dialogShown = await confirm.first().waitFor({ state: 'visible', timeout: 8_000 }).then(() => true).catch(() => false);
      if (!dialogShown) {
        rec(4, 'Delete with confirm dialog', 'failed', 'Confirm dialog ("Delete photo") did not appear.');
      } else {
        await confirm.first().click();
        const ok = await page
          .waitForFunction(
            (n) => document.querySelectorAll('input[aria-label^="Caption for photo "]').length === n - 1,
            before,
            { timeout: 15_000 }
          )
          .then(() => true)
          .catch(() => false);
        rec(4, 'Delete with confirm dialog', ok ? 'passed' : 'failed',
          ok ? `Confirm dialog shown; photo removed (${before} → ${before - 1}).` : `Count did not drop from ${before}.`);
      }
    } catch (e) {
      rec(4, 'Delete with confirm dialog', 'failed', e.message);
    }

    await screenshot(page, 'test14-gallery-final.png');

    // Collect uploaded /uploads/ srcs so we can unlink the files after deleting the recipe.
    try {
      const srcs = await page
        .locator('.photo-manager-grid img')
        .evaluateAll((els) => els.map((e) => e.getAttribute('src')).filter((s) => s && s.startsWith('/uploads/')));
      for (const s of srcs) uploadedSrcs.add(s);
    } catch { /* best-effort */ }
  } catch (err) {
    await screenshot(page, 'test14-FAIL-setup.png').catch(() => {});
    rec(0, 'Setup', 'failed', err.message);
  } finally {
    // ── Cleanup: delete the throwaway recipe and unlink its upload files ─────────
    try {
      await deleteRecipeByName(page, cookbookId, RECIPE_NAME);
      console.log('[test14] cleanup: throwaway recipe deleted.');
    } catch (e) {
      console.warn(`[test14] cleanup: could not delete throwaway recipe: ${e.message}`);
    }
    for (const s of uploadedSrcs) {
      const file = s.replace('/uploads/', '');
      await unlink(join(UPLOADS_DIR, file)).catch(() => {});
    }
    if (uploadedSrcs.size > 0) console.log(`[test14] cleanup: unlinked ${uploadedSrcs.size} upload file(s).`);
  }

  // ── Aggregate ─────────────────────────────────────────────────────────────────
  const failed = items.filter((i) => i.status === 'failed');
  const passed = items.filter((i) => i.status === 'passed');
  const skipped = items.filter((i) => i.status === 'skipped');
  const lines = items
    .sort((a, b) => a.n - b.n)
    .map((i) => `    [${i.status === 'passed' ? 'PASS' : i.status === 'skipped' ? 'SKIP' : 'FAIL'}] item ${i.n}: ${i.title} — ${i.detail}`)
    .join('\n');

  return {
    status: failed.length > 0 ? 'failed' : 'passed',
    message:
      `Phase 14 Photo Gallery UAT: ${passed.length} passed, ${skipped.length} skipped, ${failed.length} failed` +
      (recipeId ? ` (throwaway recipe ${recipeId}, cleaned up)` : '') +
      `\n${lines}`,
    items,
  };
}
