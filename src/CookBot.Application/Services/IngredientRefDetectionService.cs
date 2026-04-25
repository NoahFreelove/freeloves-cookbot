using System.Text.RegularExpressions;
using CookBot.Domain.Interfaces;

namespace CookBot.Application.Services;

public static class IngredientRefDetectionService
{
    private static readonly Regex MarkdownLinkPattern = new(
        @"\[([^\]]+)\]\(#(\d+)\)",
        RegexOptions.Compiled);

    public static List<int> DetectRefs(string stepText, List<ParsedIngredient> ingredients)
    {
        // Plan 01-02 / FORMAT-05 / Pitfall C1: the substring-match fallback was deleted.
        // [name](#id) markdown links are the single source of truth for ingredient refs;
        // there is no "did the step text happen to mention the ingredient name?" branch.
        // The `ingredients` parameter is retained for caller back-compat (RecipeService,
        // tests) but is intentionally unused.
        _ = ingredients;

        var refs = new HashSet<int>();
        foreach (Match match in MarkdownLinkPattern.Matches(stepText))
        {
            if (int.TryParse(match.Groups[2].Value, out var id))
                refs.Add(id);
        }
        return refs.OrderBy(x => x).ToList();
    }
}
