using CookBot.Domain.Entities;

namespace CookBot.Domain.Interfaces;

public class ParsedRecipe
{
    public string Name { get; set; } = string.Empty;
    public int Servings { get; set; }
    public int? PrepTimeMinutes { get; set; }
    public int? CookTimeMinutes { get; set; }
    public List<string> Tags { get; set; } = new();
    public List<ParsedIngredient> Ingredients { get; set; } = new();
    public string MarkdownBody { get; set; } = string.Empty;
    public string RawContent { get; set; } = string.Empty;
}

public class ParsedIngredient
{
    public int LocalId { get; set; }
    public string Name { get; set; } = string.Empty;
    public double Amount { get; set; }
    public string Unit { get; set; } = string.Empty;
    public string? Note { get; set; }
}

public interface IRecipeFormatParser
{
    ParsedRecipe Parse(string rawContent);
    string Serialize(ParsedRecipe recipe);
    bool TryParse(string rawContent, out ParsedRecipe? recipe, out List<string> errors);
}
