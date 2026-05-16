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
                    foreach (Match m in IngredientLinkPatterns.Pattern.Matches(content.Text))
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
                    if (content.Temperature is { } temp)
                    {
                        switch (temp.Unit)
                        {
                            case TemperatureUnit.F:
                            case TemperatureUnit.C:
                                if (temp.Value != Math.Truncate(temp.Value))
                                    errors.Add(new ValidationError($"/steps/{i}/temperature/value",
                                        "INVALID_TEMPERATURE", $"{temp.Unit} temperature must be whole-degree."));
                                break;
                            case TemperatureUnit.Gas:
                                if (temp.Value % 0.5m != 0m || temp.Value < 1.0m || temp.Value > 9.5m)
                                    errors.Add(new ValidationError($"/steps/{i}/temperature/value",
                                        "INVALID_TEMPERATURE", "Gas mark must be a 0.5-step value in [1.0, 9.5]."));
                                break;
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

        // AI-SPEC §1b enhancements (warnings, not errors — do not trigger the repair loop):
        DetectOrphanIngredients(doc, warnings);
        DetectEmptySections(doc, warnings);

        return new ValidationResult(errors, warnings);
    }

    /// <summary>
    /// AI-SPEC §1b — surfaces ingredients that are present in <c>doc.Ingredients</c> but
    /// never referenced by a <c>[name](#id)</c> markdown link in any <see cref="ContentStep.Text"/>.
    /// Warning only — does not flip <see cref="ValidationResult.IsValid"/>.
    /// </summary>
    private static void DetectOrphanIngredients(RecipeDocument doc, List<ValidationWarning> warnings)
    {
        if (doc.Ingredients.Count == 0) return;

        // Collect every `[text](#id)` numeric id referenced in step text.
        var referencedIds = new HashSet<int>();
        foreach (var step in doc.Steps.OfType<ContentStep>())
        {
            if (string.IsNullOrEmpty(step.Text)) continue;
            foreach (Match m in IngredientLinkPatterns.Pattern.Matches(step.Text))
            {
                if (int.TryParse(m.Groups[2].Value, out var refId))
                    referencedIds.Add(refId);
            }
        }

        for (var i = 0; i < doc.Ingredients.Count; i++)
        {
            var ing = doc.Ingredients[i];
            if (!referencedIds.Contains(ing.Id))
            {
                warnings.Add(new ValidationWarning(
                    Path: $"/ingredients/{i}",
                    Code: "OrphanIngredient",
                    Message: $"Ingredient '{ing.Name}' (id={ing.Id}) is not referenced by any step."));
            }
        }
    }

    /// <summary>
    /// AI-SPEC §1b — surfaces <see cref="SectionStep"/>s that are immediately followed by
    /// another <see cref="SectionStep"/> (or end-of-list) with no <see cref="ContentStep"/>
    /// in between. Warning only — does not flip <see cref="ValidationResult.IsValid"/>.
    /// </summary>
    private static void DetectEmptySections(RecipeDocument doc, List<ValidationWarning> warnings)
    {
        for (var i = 0; i < doc.Steps.Count; i++)
        {
            if (doc.Steps[i] is not SectionStep section) continue;

            // Look ahead until next SectionStep or end-of-list — must find a ContentStep.
            var hasContentInSection = false;
            for (var j = i + 1; j < doc.Steps.Count; j++)
            {
                if (doc.Steps[j] is SectionStep) break;
                if (doc.Steps[j] is ContentStep) { hasContentInSection = true; break; }
            }

            if (!hasContentInSection)
            {
                warnings.Add(new ValidationWarning(
                    Path: $"/steps/{i}",
                    Code: "EmptySection",
                    Message: $"Section '{section.Heading}' has no steps."));
            }
        }
    }
}
