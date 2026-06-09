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
    /// <summary>Equipment / tools list (FORMAT-02). Mutable for editor; never null.</summary>
    public List<string> Equipment { get; set; } = new();
    /// <summary>Source / provenance metadata (FORMAT-04). Reuses Domain record directly (no ParsedProvenance parallel).</summary>
    public RecipeProvenance? Provenance { get; set; }
}

public class ParsedStep
{
    public string Text { get; set; } = string.Empty;
    public bool IsSection { get; set; }
    public List<ParsedTimer>? Timers { get; set; }
    public StepTemperature? Temperature { get; set; }
    /// <summary>Per-step doneness cue (FORMAT-03). Follows the Temperature nullable pattern.</summary>
    public string? DonenessCue { get; set; }
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
    /// <summary>Per-ingredient substitution options (FORMAT-01). Mutable for editor; never null.</summary>
    public List<IngredientSubstitution> Substitutions { get; set; } = new();
}

public interface IRecipeFormatParser
{
    ParsedRecipe Parse(string rawContent);
    string Serialize(ParsedRecipe recipe);
    bool TryParse(string rawContent, out ParsedRecipe? recipe, out List<string> errors);
}
