using System.Text.RegularExpressions;
using CookBot.Domain.Interfaces;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace CookBot.Application.Services;

public class RecipeFormatParser : IRecipeFormatParser
{
    private static readonly Regex FrontmatterRegex = new(
        @"^---\s*\n(.*?)\n---\s*\n(.*)$",
        RegexOptions.Singleline | RegexOptions.Compiled);

    private readonly IDeserializer _deserializer;
    private readonly ISerializer _serializer;

    public RecipeFormatParser()
    {
        _deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        _serializer = new SerializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
            .Build();
    }

    public ParsedRecipe Parse(string rawContent)
    {
        var match = FrontmatterRegex.Match(rawContent.TrimStart());
        if (!match.Success)
            throw new FormatException("Invalid recipe format: missing YAML frontmatter (--- delimiters).");

        var yamlContent = match.Groups[1].Value;
        var markdownBody = match.Groups[2].Value.Trim();

        var frontmatter = _deserializer.Deserialize<RecipeFrontmatter>(yamlContent)
            ?? throw new FormatException("Failed to parse YAML frontmatter.");

        return new ParsedRecipe
        {
            Name = frontmatter.Name ?? "Untitled Recipe",
            Servings = frontmatter.Servings,
            PrepTimeMinutes = frontmatter.PrepTime,
            CookTimeMinutes = frontmatter.CookTime,
            Tags = frontmatter.Tags ?? new List<string>(),
            Ingredients = (frontmatter.Ingredients ?? new List<IngredientFrontmatter>())
                .Select(i => new ParsedIngredient
                {
                    LocalId = i.Id,
                    Name = i.Name ?? "unknown",
                    Amount = i.Amount,
                    Unit = i.Unit ?? "piece",
                    Note = i.Note,
                }).ToList(),
            MarkdownBody = markdownBody,
            RawContent = rawContent,
        };
    }

    public string Serialize(ParsedRecipe recipe)
    {
        var frontmatter = new RecipeFrontmatter
        {
            Name = recipe.Name,
            Servings = recipe.Servings,
            PrepTime = recipe.PrepTimeMinutes,
            CookTime = recipe.CookTimeMinutes,
            Tags = recipe.Tags.Any() ? recipe.Tags : null,
            Ingredients = recipe.Ingredients.Select(i => new IngredientFrontmatter
            {
                Id = i.LocalId,
                Name = i.Name,
                Amount = i.Amount,
                Unit = i.Unit,
                Note = i.Note,
            }).ToList(),
        };

        var yaml = _serializer.Serialize(frontmatter).TrimEnd();
        return $"---\n{yaml}\n---\n\n{recipe.MarkdownBody}";
    }

    public bool TryParse(string rawContent, out ParsedRecipe? recipe, out List<string> errors)
    {
        errors = new List<string>();
        recipe = null;

        if (string.IsNullOrWhiteSpace(rawContent))
        {
            errors.Add("Recipe content is empty.");
            return false;
        }

        var match = FrontmatterRegex.Match(rawContent.TrimStart());
        if (!match.Success)
        {
            errors.Add("Missing YAML frontmatter. Content must start with --- and end with ---.");
            return false;
        }

        try
        {
            recipe = Parse(rawContent);

            if (string.IsNullOrWhiteSpace(recipe.Name))
                errors.Add("Recipe name is required.");
            if (recipe.Servings <= 0)
                errors.Add("Servings must be greater than 0.");
            if (!recipe.Ingredients.Any())
                errors.Add("At least one ingredient is required.");

            var ids = recipe.Ingredients.Select(i => i.LocalId).ToList();
            if (ids.Count != ids.Distinct().Count())
                errors.Add("Ingredient IDs must be unique.");

            return !errors.Any();
        }
        catch (Exception ex)
        {
            errors.Add($"Parse error: {ex.Message}");
            return false;
        }
    }

    private class RecipeFrontmatter
    {
        public string? Name { get; set; }
        public int Servings { get; set; } = 1;
        public int? PrepTime { get; set; }
        public int? CookTime { get; set; }
        public List<string>? Tags { get; set; }
        public List<IngredientFrontmatter>? Ingredients { get; set; }
    }

    private class IngredientFrontmatter
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public double Amount { get; set; }
        public string? Unit { get; set; }
        public string? Note { get; set; }
    }
}
