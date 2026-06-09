/**
 * run.mjs — CookBot UAT Harness Entry Point
 *
 * Drives Phase 10 UAT Test 5 (cookbook reparenting), Test 7 (responsive collapse),
 * and the CLEANUP-04 Conversion smoke check using a real Playwright/chromium
 * session against the running app on :7000.
 * Test 4 (validation-fail fallback) is recorded as SKIP/deferred — see tests/test4.
 *
 * Prerequisites:
 *   - App is running on http://localhost:7000 (run `./run.sh` in the project root)
 *   - At least one recipe + at least two cookbooks owned by the default user
 *     (the first user, Noah, on the seeded DB) — needed for Test 5 reparenting
 *   - Plan 11-02 fixes applied (CLEANUP-01/02) for Test 7 to fully pass
 *   - Plan 11-04 fix applied (CLEANUP-04) for the Conversion check to pass;
 *     recipe id 1 ("Apple Blueberry Crumble") carries the 900 g ingredient
 *
 * Usage:
 *   npm install          # first time only
 *   npm test             # or: node run.mjs
 *
 * Exit codes:
 *   0 — all executed tests PASSED (SKIP does not fail the run)
 *   1 — one or more tests FAILED
 *
 * Chromium:
 *   Uses /snap/bin/chromium (system snap) via executablePath.
 *   If snap confinement prevents launch, install the Playwright-bundled browser:
 *     npx playwright install chromium
 *   Then set PLAYWRIGHT_CHROMIUM_EXECUTABLE_PATH="" to use the bundled browser,
 *   or comment out executablePath below.
 */

import { chromium } from 'playwright';
import { waitForApp } from './lib/app.mjs';
import { establishSession, findFirstRecipe } from './lib/session.mjs';
import { runTest5 } from './tests/test5-reparenting.mjs';
import { runTest7 } from './tests/test7-responsive.mjs';
import { runTest4 } from './tests/test4-validation-fail.mjs';
import { runConversionTest } from './tests/test-conversion.mjs';

// ── Chromium launch configuration ───────────────────────────────────────────
// System snap chromium verified working: smoke test (2026-06-05) confirmed
// that chromium at /snap/bin/chromium launches headless via Playwright with
// --no-sandbox flags. Use executablePath to avoid a separate Playwright download.
const CHROMIUM_ARGS = [
  '--no-sandbox',
  '--disable-setuid-sandbox',
  '--disable-dev-shm-usage',
];

function buildLaunchOptions() {
  const snapPath = '/snap/bin/chromium';
  return {
    executablePath: snapPath,
    headless: true,
    args: CHROMIUM_ARGS,
  };
}

// ── Main ─────────────────────────────────────────────────────────────────────

const results = [];

try {
  // 1. Wait for the app to be ready
  await waitForApp();

  // 2. Launch chromium
  const launchOpts = buildLaunchOptions();
  console.log(`\n[harness] Launching chromium (executablePath=${launchOpts.executablePath})...`);
  const browser = await chromium.launch(launchOpts).catch(async (err) => {
    // Fallback: if snap chromium fails, try without executablePath (uses playwright-bundled)
    console.warn(`[harness] Snap chromium failed (${err.message.substring(0, 100)}), trying Playwright-bundled chromium...`);
    return chromium.launch({ headless: true, args: CHROMIUM_ARGS });
  });

  const context = await browser.newContext();
  const page = await context.newPage();

  // 3. Establish trusted-LAN session (default = first user, Noah; no password)
  await establishSession(page);

  // 4. Discover a recipe to use for Tests 5 and 7
  let recipeId, recipeName;
  try {
    const found = await findFirstRecipe(page);
    recipeId = found.recipeId;
    recipeName = found.recipeName;
  } catch (e) {
    console.error(`\n[harness] Could not find a recipe: ${e.message}`);
    console.error('[harness] Seed at least one recipe before running the harness.');
    process.exit(1);
  }

  console.log(`\n[harness] Using recipe: id=${recipeId}, name="${recipeName}"`);

  // 5. Run Test 5 — Cookbook reparenting
  const t5 = await runTest5(page, { recipeId, recipeName });
  results.push({ name: 'UAT Test 5', ...t5 });

  // If Test 5 reparented the recipe, the recipeId is still valid but it now
  // lives in a different cookbook. Test 7 only needs the recipe to exist at
  // /recipes/{id} — which is still true after reparenting.
  // Re-establish session after Test 5 navigation side effects.
  await establishSession(page);

  // 6. Run Test 7 — Responsive collapse at 719px
  const t7 = await runTest7(page, { recipeId });
  results.push({ name: 'UAT Test 7', ...t7 });

  // 7. Run the Conversion smoke check (CLEANUP-04) — always against recipe id 1,
  //    the seeded "Apple Blueberry Crumble" that carries the 900 g ingredient.
  //    Re-establish the session first to clear any narrow-viewport / nav state
  //    left by Test 7 (the test also resets the viewport to desktop itself).
  await establishSession(page);
  const tc = await runConversionTest(page, { recipeId: 1 });
  results.push({ name: 'UAT Conversion (CLEANUP-04)', ...tc });

  // 8. Run Test 4 — Validation-fail fallback (deferred/skip)
  const t4 = await runTest4();
  results.push({ name: 'UAT Test 4', ...t4 });

  await browser.close();

} catch (err) {
  console.error(`\n[harness] Fatal error: ${err.message}`);
  if (err.stack) console.error(err.stack);
  process.exit(1);
}

// ── Print summary ─────────────────────────────────────────────────────────────

console.log('\n' + '─'.repeat(60));
console.log('UAT HARNESS RESULTS');
console.log('─'.repeat(60));

let anyFailed = false;

for (const r of results) {
  const icon = r.status === 'passed' ? 'PASS' : r.status === 'skipped' ? 'SKIP' : 'FAIL';
  console.log(`${r.name}: ${icon}`);
  if (r.status !== 'passed') {
    console.log(`  -> ${r.message}`);
  }
  if (r.status === 'failed') anyFailed = true;
}

console.log('─'.repeat(60));

if (anyFailed) {
  console.log('RESULT: FAIL — one or more tests failed.');
  process.exit(1);
} else {
  const skipCount = results.filter(r => r.status === 'skipped').length;
  const passCount = results.filter(r => r.status === 'passed').length;
  console.log(`RESULT: PASS — ${passCount} passed, ${skipCount} skipped, 0 failed.`);
  process.exit(0);
}
