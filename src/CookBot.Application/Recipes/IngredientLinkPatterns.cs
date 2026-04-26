using System.Text.RegularExpressions;

namespace CookBot.Application.Recipes;

/// <summary>
/// Single source of truth for the [name](#id) ingredient link regex. Phase 1 D-13 made link-resolution
/// the only highlight path; Phase 3 chip composer reuses this same pattern. Do NOT redefine elsewhere.
/// </summary>
internal static class IngredientLinkPatterns
{
    public static readonly Regex Pattern = new(
        @"\[([^\]]*)\]\(#(\d+)\)",
        RegexOptions.Compiled);
}
