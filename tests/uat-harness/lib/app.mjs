/**
 * lib/app.mjs — App readiness helper for the CookBot UAT harness.
 *
 * The harness ASSUMES the app is already running on http://localhost:7000.
 * Run `./run.sh` (or `dotnet run --project src/CookBot.Web`) BEFORE running
 * `npm test`. See README.md.
 *
 * waitForApp() polls GET http://localhost:7000/healthz until it returns 200
 * (or the timeout fires). The healthz endpoint is wired in Program.cs via
 * app.MapHealthChecks("/healthz").
 */

import { mkdir } from 'node:fs/promises';

const APP_URL = 'http://localhost:7000';
const HEALTHZ_URL = `${APP_URL}/healthz`;
const POLL_INTERVAL_MS = 1000;
const TIMEOUT_MS = 60_000;

/**
 * Polls /healthz until the app responds with HTTP 200.
 * @returns {Promise<void>} Resolves when the app is ready.
 * @throws {Error} If the app is not ready within TIMEOUT_MS.
 */
export async function waitForApp() {
  const deadline = Date.now() + TIMEOUT_MS;
  let lastError = null;

  console.log(`[app] Waiting for app on ${HEALTHZ_URL} (timeout ${TIMEOUT_MS / 1000}s)...`);

  while (Date.now() < deadline) {
    try {
      const res = await fetch(HEALTHZ_URL, { signal: AbortSignal.timeout(3000) });
      if (res.status === 200) {
        console.log('[app] App is ready (healthz 200).');
        return;
      }
      lastError = new Error(`healthz returned HTTP ${res.status}`);
    } catch (e) {
      lastError = e;
    }
    await sleep(POLL_INTERVAL_MS);
  }

  throw new Error(
    `App did not become ready within ${TIMEOUT_MS / 1000}s. ` +
    `Last error: ${lastError?.message ?? 'unknown'}. ` +
    `Start the app with: ./run.sh`
  );
}

/**
 * The base URL of the running app.
 */
export const BASE_URL = APP_URL;

/**
 * Directory where harness screenshots are written. Git-ignored (see
 * tests/uat-harness/.gitignore) so artifacts never get committed.
 */
export const ARTIFACTS_DIR = new URL('../artifacts/', import.meta.url).pathname;

/**
 * Save a full-page screenshot into artifacts/ for human spot-checking at key
 * assertion points. Best-effort: failures are swallowed so a screenshot problem
 * never masks or fails a real assertion.
 *
 * @param {import('playwright').Page} page
 * @param {string} filename e.g. "test7-719px-recipeview.png"
 * @returns {Promise<string|null>} The absolute path written, or null on failure.
 */
export async function screenshot(page, filename) {
  try {
    await mkdir(ARTIFACTS_DIR, { recursive: true });
    const out = ARTIFACTS_DIR + filename;
    await page.screenshot({ path: out, fullPage: true });
    console.log(`[artifact] Saved screenshot: ${out}`);
    return out;
  } catch (e) {
    console.warn(`[artifact] Could not save screenshot ${filename}: ${e.message}`);
    return null;
  }
}

function sleep(ms) {
  return new Promise(resolve => setTimeout(resolve, ms));
}
