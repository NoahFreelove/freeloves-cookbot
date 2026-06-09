/**
 * lib/session.mjs — Trusted-LAN session establishment for the CookBot UAT harness.
 *
 * CookBot uses a trusted-LAN posture: there is NO login form. User selection
 * is entirely client-side. The DEFAULT user for a fresh circuit is the FIRST
 * user — CurrentUserService picks the lowest user id when sessionStorage has no
 * explicit selection. On this seeded database that is "Noah" (id 1, admin,
 * UnitSystem=Canadian), who owns every cookbook and recipe. So the harness
 * session is already Noah without any user-picker interaction — which is exactly
 * what Tests 5/7/Conversion need, because Noah owns the recipes they mutate.
 *
 * Session recipe (MainLayout.razor):
 *   1. Open http://localhost:7000/ — the Blazor circuit boots and
 *      CurrentUserService resolves the current user (defaulting to the first
 *      user, Noah, when sessionStorage["cookbot_current_user"] is unset).
 *   2. After the first navigation the session is established for the tab.
 *
 * establishSession(page) navigates to / and waits until the TopBar header is
 * rendered and non-empty — that signals the circuit is up and the default user
 * is active, so test navigation can proceed.
 *
 * getCurrentUserId(page) reads sessionStorage["cookbot_current_user"] so test
 * helpers can pass a numeric id to other helpers that need it.
 */

import { BASE_URL } from './app.mjs';

const HOME_URL = `${BASE_URL}/`;

// Cookbook ids to probe when discovering a recipe. The seeded database exposes
// cookbook 1 ("My Recipes") and 2 ("Desserts"), both owned by Noah. We probe a
// small range so the harness still works if cookbook ids shift after a reseed.
const COOKBOOK_PROBE_IDS = [1, 2, 3, 4, 5];

/**
 * Establish the trusted-LAN session on `page`.
 *
 * Navigates to the app root and lets the default first user (Noah) load — no
 * password required (VerifyPasswordAsync returns true when PasswordHash is
 * null). Waits until the header TopBar has rendered, confirming a live circuit.
 *
 * @param {import('playwright').Page} page
 * @returns {Promise<void>}
 */
export async function establishSession(page) {
  console.log('[session] Navigating to app root to establish default-user (Noah) session...');
  await page.goto(HOME_URL, { waitUntil: 'networkidle', timeout: 30_000 });

  // Wait for the TopBar header to appear — it renders the current user label and
  // confirms the Blazor circuit is live. The label mirrors the DisplayName
  // ("Noah" for the default first user).
  await page.waitForSelector('header', { timeout: 20_000 });

  // Wait for Blazor interactivity: the header text content is non-empty once the
  // circuit is ready and the user picker has rendered.
  await page.waitForFunction(
    () => {
      const header = document.querySelector('header');
      return header && header.textContent && header.textContent.trim().length > 0;
    },
    { timeout: 20_000 }
  );

  const userId = await getCurrentUserId(page);
  console.log(`[session] Session established. cookbot_current_user = ${userId ?? '(default first user / Noah, not yet stored)'}`);
}

/**
 * Read the current user id from sessionStorage.
 *
 * @param {import('playwright').Page} page
 * @returns {Promise<number|null>} The numeric user id, or null if not set.
 */
export async function getCurrentUserId(page) {
  const raw = await page.evaluate(() =>
    sessionStorage.getItem('cookbot_current_user')
  );
  if (raw === null || raw === undefined) return null;
  const parsed = parseInt(raw, 10);
  return isNaN(parsed) ? null : parsed;
}

/**
 * Discover a usable recipe by navigating cookbook detail pages.
 *
 * The home page does NOT expose /recipes/{id} anchors. Recipes are only
 * reachable from /cookbooks/{id}, where each recipe renders as
 *   <div role="button" class="cb-card" @onclick=ViewRecipe(id)>
 * with the recipe name in an inner <div style="font-weight:600">. Clicking the
 * card calls Navigation.NavigateTo($"/recipes/{id}") (CookbookDetail.razor).
 *
 * Strategy: probe a small set of cookbook ids, find the first cookbook that has
 * at least one recipe card, click the first card, and capture the resulting
 * /recipes/{id} URL plus the recipe's display name. This exercises the real
 * navigation path (not a fabricated URL) and returns the recipe id + name.
 *
 * @param {import('playwright').Page} page
 * @returns {Promise<{recipeId: number, recipeName: string, cookbookId: number}>}
 * @throws {Error} If no recipe is found in any probed cookbook.
 */
export async function findFirstRecipe(page) {
  for (const cookbookId of COOKBOOK_PROBE_IDS) {
    const cookbookUrl = `${BASE_URL}/cookbooks/${cookbookId}`;
    await page.goto(cookbookUrl, { waitUntil: 'networkidle', timeout: 30_000 });

    // Wait briefly for Blazor to render the recipe list (cards arrive after the
    // interactive render, not in the prerender). Tolerate cookbooks with zero
    // recipes — we just move on to the next id.
    const card = page.locator('.cb-card[role="button"]').first();
    const appeared = await card
      .waitFor({ state: 'visible', timeout: 6_000 })
      .then(() => true)
      .catch(() => false);

    if (!appeared) {
      console.log(`[session] Cookbook ${cookbookId}: no recipe cards — trying next.`);
      continue;
    }

    // Capture the recipe name from the inner bold div before navigating.
    const recipeName = await card
      .locator('div[style*="font-weight:600"]')
      .first()
      .textContent()
      .then((t) => (t ?? '').trim())
      .catch(() => '');

    // Click the card and wait for navigation to /recipes/{id}.
    await card.click();
    await page
      .waitForURL((url) => /\/recipes\/\d+(?:$|[/?#])/.test(url.toString()), { timeout: 15_000 })
      .catch(() => null);

    const m = page.url().match(/\/recipes\/(\d+)/);
    if (!m) {
      console.log(`[session] Cookbook ${cookbookId}: card click did not land on /recipes/{id} (url=${page.url()}); trying next.`);
      continue;
    }

    const recipeId = parseInt(m[1], 10);
    console.log(`[session] Found recipe via cookbook ${cookbookId}: id=${recipeId} name="${recipeName}"`);
    return { recipeId, recipeName, cookbookId };
  }

  throw new Error(
    `[session] No recipe found in cookbooks ${COOKBOOK_PROBE_IDS.join(', ')}. ` +
    'Seed at least one recipe in a cookbook before running the harness.'
  );
}
