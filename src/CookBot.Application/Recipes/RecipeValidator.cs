using System.Text.RegularExpressions;
using CookBot.Domain.Recipes;

namespace CookBot.Application.Recipes;

/// <summary>
/// Semantic post-deserialize validator for <see cref="RecipeDocument"/>. Returns a
/// <see cref="ValidationResult"/> data envelope; <strong>never throws</strong> (FORMAT-07).
/// Schema-strict checks (shape, types) happen earlier in the pipeline; this layer covers
/// invariants the type system can't catch — duplicate ingredient ids, dangling
/// <c>[name](#id)</c> step links, empty section headings, etc.
/// </summary>
public sealed class RecipeValidator
{
    private static readonly Regex IngredientLink = new(
        @"\[([^\]]+)\]\(#(\d+)\)",
        RegexOptions.Compiled);

    /// <summary>
    /// Validates a recipe. Never throws on any input including null; null produces a single
    /// <see cref="ValidationError"/> at path "/".
    /// </summary>
    public ValidationResult Validate(RecipeDocument doc)
    {
        if (doc is null)
        {
            return new ValidationResult(
                new[] { new ValidationError("/", "REQUIRED", "Recipe document is null.") },
                Array.Empty<ValidationWarning>());
        }

        var errors = new List<ValidationError>();
        var warnings = new List<ValidationWarning>();

        if (string.IsNullOrWhiteSpace(doc.Name))
        {
            errors.Add(new ValidationError("/name", "REQUIRED", "Recipe name is required."));
        }

        if (doc.Servings <= 0)
        {
            errors.Add(new ValidationError("/servings", "OUT_OF_RANGE", "Servings must be > 0."));
        }

        var ids = doc.Ingredients.Select(i => i.Id).ToList();
        if (ids.Count != ids.Distinct().Count())
        {
            errors.Add(new ValidationError(
                "/ingredients",
                "DUPLICATE_ID",
                "Ingredient ids must be unique within a recipe."));
        }

        for (int i = 0; i < doc.Steps.Count; i++)
        {
            switch (doc.Steps[i])
            {
                case ContentStep content:
                    foreach (Match m in IngredientLink.Matches(content.Text))
                    {
                        var idText = m.Groups[2].Value;
                        if (!int.TryParse(idText, out var refId) || !ids.Contains(refId))
                        {
                            errors.Add(new ValidationError(
                                $"/steps/{i}/text",
                                "DANGLING_REF",
                                $"Step references ingredient #{idText} which is not in ingredients."));
                        }
                    }
                    break;

                case SectionStep section:
                    if (string.IsNullOrWhiteSpace(section.Heading))
                    {
                        errors.Add(new ValidationError(
                            $"/steps/{i}/heading",
                            "REQUIRED",
                            "Section heading is required."));
                    }
                    break;
            }
        }

        return new ValidationResult(errors, warnings);
    }
}
