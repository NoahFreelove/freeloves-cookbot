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
        var refs = new HashSet<int>();

        // First: explicit markdown links [name](#id)
        foreach (Match match in MarkdownLinkPattern.Matches(stepText))
        {
            if (int.TryParse(match.Groups[2].Value, out var id))
                refs.Add(id);
        }

        // Second: plain text name matching (case-insensitive)
        var textLower = stepText.ToLowerInvariant();
        foreach (var ingredient in ingredients)
        {
            if (refs.Contains(ingredient.LocalId)) continue;
            var nameLower = ingredient.Name.ToLowerInvariant();
            if (nameLower.Length >= 3 && textLower.Contains(nameLower))
                refs.Add(ingredient.LocalId);
        }

        return refs.OrderBy(x => x).ToList();
    }
}
