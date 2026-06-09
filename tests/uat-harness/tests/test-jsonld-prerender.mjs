/**
 * tests/test-jsonld-prerender.mjs — UAT prerender JSON-LD assertion (INTEROP-01)
 *
 * Validates that RecipeView server-renders a valid Schema.org Recipe JSON-LD block
 * in the INITIAL HTTP response — the gating fix for INTEROP-01.
 *
 * This test uses a plain `fetch` (NOT a Playwright page / browser) to assert against
 * the RAW initial HTTP response, exactly as a crawler sees it. JSON-LD that appears
 * only after Blazor hydration would pass a browser-DOM check but FAIL here — which is
 * the correct (stronger) seam for the prerender guarantee.
 *
 * Trusted-LAN posture: the prerender JSON-LD is NOT per-user gated (see TODO(AuthMode)
 * in RecipeView.razor), so any existing seeded recipe id works without authentication.
 *
 * @param {object} opts
 * @param {number} [opts.recipeId=1] - Recipe id to fetch (default: the first seeded recipe)
 * @returns {Promise<{status: 'passed'|'failed'|'skipped', message: string}>}
 */

import { BASE_URL } from '../lib/app.mjs';

export async function runJsonLdPrerender({ recipeId = 1 } = {}) {
  const testLabel = 'UAT JSON-LD Prerender (INTEROP-01)';
  const recipeUrl = `${BASE_URL}/recipes/${recipeId}`;

  console.log(`\n[jsonld-prerender] Fetching RAW HTTP response from ${recipeUrl}...`);

  let res;
  let html;

  try {
    res = await fetch(recipeUrl, { signal: AbortSignal.timeout(15_000) });
    html = await res.text();
  } catch (err) {
    return {
      status: 'failed',
      message: `${testLabel}: FAIL (fetch error) — could not fetch ${recipeUrl}: ${err.message}`,
    };
  }

  // Assertion (1): HTTP 200
  if (res.status !== 200) {
    return {
      status: 'failed',
      message: `${testLabel}: FAIL — expected HTTP 200 from ${recipeUrl}, got ${res.status}`,
    };
  }

  // Assertion (2): <script type="application/ld+json"> present in RAW response
  // This is the prerender guard: it must be in the initial HTML, not only post-hydration.
  if (!html.includes('<script type="application/ld+json">')) {
    return {
      status: 'failed',
      message:
        `${testLabel}: FAIL — <script type="application/ld+json"> NOT found in RAW HTTP response ` +
        `for /recipes/${recipeId}. JSON-LD is absent from prerender (INTEROP-01 blocker). ` +
        `Ensure LoadRecipeDocumentForPrerenderAsync runs in OnParametersSetAsync, not OnAfterRenderAsync.`,
    };
  }

  // Assertion (3): "@type":"Recipe" present in the JSON-LD block
  // Extract the script block and check for the @type marker.
  const scriptStart = html.indexOf('<script type="application/ld+json">');
  const scriptEnd = html.indexOf('</script>', scriptStart);
  const scriptContent =
    scriptStart >= 0 && scriptEnd > scriptStart
      ? html.substring(scriptStart + '<script type="application/ld+json">'.length, scriptEnd)
      : '';

  // The JSON-LD projector uses the STJ default (HTML-safe) encoder, so "@type" and "Recipe"
  // appear unescaped in the JSON string value, but < > & in OTHER fields may be \uXXXX-encoded.
  // "@type":"Recipe" itself contains no special chars — check for it directly.
  if (!scriptContent.includes('"@type"') || !scriptContent.includes('"Recipe"')) {
    return {
      status: 'failed',
      message:
        `${testLabel}: FAIL — "@type":"Recipe" NOT found inside the application/ld+json block ` +
        `in RAW HTTP response for /recipes/${recipeId}. ` +
        `scriptContent (first 200 chars): ${scriptContent.substring(0, 200)}`,
    };
  }

  console.log(
    `[jsonld-prerender] <script type="application/ld+json"> + "@type":"Recipe" present in RAW HTTP response for /recipes/${recipeId} — PASS`
  );

  return {
    status: 'passed',
    message:
      `${testLabel}: PASS — application/ld+json + "@type":"Recipe" present in RAW HTTP response ` +
      `for /recipes/${recipeId} (prerender-safe, not post-hydration).`,
  };
}
