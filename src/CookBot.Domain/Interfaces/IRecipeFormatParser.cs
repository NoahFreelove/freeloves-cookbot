using CookBot.Domain.Recipes;

namespace CookBot.Domain.Interfaces;

public class ParsedRecipe
{
    public string Name { get; set; } = string.Empty;
    public int Servings { get; set; } = 1;
    public int? PrepTimeMinutes { get; set; }
    public int? CookTimeMinutes { get; set; }
    public string? PhotoUrl { get; set; }
    public string? Description { get; set; }
    public List<string> Tags { get; set; } = new();
    public List<ParsedIngredient> Ingredients { get; set; } = new();
    public List<ParsedStep> Steps { get; set; } = new();
}

public class ParsedStep
{
    public string Text { get; set; } = string.Empty;
    public bool IsSection { get; set; }
    public List<ParsedTimer>? Timers { get; set; }
    public StepTemperature? Temperature { get; set; }
}

public class ParsedTimer
{
    public int Duration { get; set; }
    public string Unit { get; set; } = "min";
    public string? Label { get; set; }
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
