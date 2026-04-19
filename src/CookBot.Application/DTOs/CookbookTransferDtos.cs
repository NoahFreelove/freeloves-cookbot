namespace CookBot.Application.DTOs;

/// <summary>Portable cookbook file for backup and sharing (JSON).</summary>
public sealed class CookbookTransferDocument
{
    public int SchemaVersion { get; set; } = 1;
    public string ExportedAt { get; set; } = "";
    public string SourceApp { get; set; } = "CookBot";
    public CookbookTransferCookbook Cookbook { get; set; } = new();
    public List<CookbookTransferRecipe> Recipes { get; set; } = new();
}

public sealed class CookbookTransferCookbook
{
    public string Name { get; set; } = "";
    public string? Description { get; set; }
}

public sealed class CookbookTransferRecipe
{
    public string Name { get; set; } = "";
    public int Servings { get; set; } = 1;
    public int? PrepTimeMinutes { get; set; }
    public int? CookTimeMinutes { get; set; }
    public List<string> Tags { get; set; } = new();
    public List<CookbookTransferIngredient> Ingredients { get; set; } = new();
    public List<CookbookTransferStep> Steps { get; set; } = new();
}

public sealed class CookbookTransferIngredient
{
    public int LocalId { get; set; }
    public string Name { get; set; } = "";
    public double Amount { get; set; }
    public string Unit { get; set; } = "";
    public string? Note { get; set; }
}

public sealed class CookbookTransferStep
{
    public string Text { get; set; } = "";
    public bool IsSection { get; set; }
    public List<CookbookTransferTimer>? Timers { get; set; }
}

public sealed class CookbookTransferTimer
{
    public int Duration { get; set; }
    public string Unit { get; set; } = "min";
    public string? Label { get; set; }
}
