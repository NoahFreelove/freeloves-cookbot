using System.Text.RegularExpressions;

namespace CookBot.Application.Services;

/// <summary>
/// Shared ingredient-name normalizer (Phase 15 / D-15-05 / NUTR-02).
/// <para>
/// This is the <b>single owner</b> of ingredient-name normalization for nutrition matching.
/// It is used both at seed load time (Plan 03 — pre-computing <c>CnfFood.NormalizedDescription</c>)
/// and at runtime match time (Plan 05 — normalizing recipe ingredient names before CNF lookup),
/// guaranteeing that the two sides are always aligned.
/// </para>
/// <para>
/// Pipeline (mirrors <c>IngredientResolver.Normalize</c> then adds deny-list stripping):
/// <list type="number">
///   <item>ToLowerInvariant + Trim</item>
///   <item>Replace <c>[-_]</c> with space (hyphenated compounds → tokens)</item>
///   <item>Strip commas and other punctuation that appear in CNF genus-first descriptions</item>
///   <item>Collapse <c>\s+</c> to single space</item>
///   <item>Strip deny-list tokens as whole words (not substrings) — prep/quality/instruction modifiers that do NOT change nutrition</item>
///   <item>Re-collapse <c>\s+</c> + Trim</item>
/// </list>
/// </para>
/// <para>
/// <b>Deny-list (strip — non-nutritive):</b>
/// chopped, minced, diced, sliced, shredded, grated, ground, sifted, packed, finely, roughly, freshly,
/// room temperature, cold, warm, good quality, good, fine, coarse, large, small, medium, ripe, organic,
/// to taste, optional, divided, for garnish, plus more.
/// </para>
/// <para>
/// <b>Kept (nutrition-changing — NOT in deny-list):</b>
/// unsalted, salted, skinless, lowfat, low-fat (→ low fat after hyphen step), whole, light, heavy.
/// </para>
/// <para>
/// <b>ReDoS safety:</b> All regexes are linear-time — <c>[-_]</c>, <c>[,;]</c>, <c>\s+</c>, and
/// fixed <c>\b{Regex.Escape(token)}\b</c> over a bounded ingredient name. No nested quantifiers or
/// catastrophic backtracking (T-15-08).
/// </para>
/// </summary>
public static class IngredientNormalizer
{
    // Deny-list: prep/quality/instruction modifiers that do NOT change nutrition (D-15-05).
    // Multi-word tokens (e.g. "room temperature") are listed before their constituent single words
    // so that the longer phrase is stripped as a unit first.
    // KEEP (not here): unsalted, salted, skinless, lowfat, low-fat, whole, light, heavy.
    private static readonly string[] DenyList =
    [
        // ── Multi-word tokens (strip as a phrase, must come before single-word constituents) ──
        "room temperature",   // hyphenated variant: hyphen→space step converts "room-temperature" → "room temperature"
        "good quality",       // hyphenated variant: "good-quality" → "good quality"
        "to taste",
        "for garnish",
        "plus more",
        // ── Single-word prep/handling modifiers ──────────────────────────────────────────────
        "chopped",
        "minced",
        "diced",
        "sliced",
        "shredded",
        "grated",
        "ground",
        "sifted",
        "packed",
        "finely",
        "roughly",
        "freshly",
        "cold",
        "warm",
        "good",
        "fine",
        "coarse",
        "large",
        "small",
        "medium",
        "ripe",
        "organic",
        "optional",
        "divided",
    ];

    /// <summary>
    /// Normalizes an ingredient name (or CNF food description) for nutrition matching.
    /// Safe to call with any string length — all operations are ordinal, no catastrophic regex.
    /// </summary>
    /// <param name="name">Raw ingredient name or CNF description.</param>
    /// <returns>Normalized string; empty when <paramref name="name"/> is empty.</returns>
    public static string Normalize(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return string.Empty;

        // Step 1: lowercase + trim
        var lower = name.ToLowerInvariant().Trim();

        // Step 2: replace hyphens and underscores with space
        // "room-temperature" → "room temperature"; "low-fat" → "low fat"; "good-quality" → "good quality"
        lower = Regex.Replace(lower, @"[-_]", " ");

        // Step 3: strip commas and semicolons (CNF genus-first descriptions use comma separators)
        lower = Regex.Replace(lower, @"[,;]", " ");

        // Step 4: collapse whitespace
        lower = Regex.Replace(lower, @"\s+", " ").Trim();

        // Step 5: strip deny-list tokens as whole words only (not substrings)
        // \b word-boundary anchors protect compound words: "groundnut" is not damaged by "ground"
        foreach (var token in DenyList)
            lower = Regex.Replace(lower, $@"\b{Regex.Escape(token)}\b", " ");

        // Step 6: re-collapse whitespace after stripping
        lower = Regex.Replace(lower, @"\s+", " ").Trim();

        return lower;
    }
}
