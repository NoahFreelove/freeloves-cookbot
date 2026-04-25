namespace CookBot.Application.DTOs;

/// <summary>Portable cookbook file for backup and sharing (JSON).</summary>
public sealed class CookbookTransferDocument
{
    /// <summary>
    /// Envelope shape version. Independent of <c>RecipeDocument.Version</c>. Bump when the
    /// cookbook envelope shape (metadata + recipes array) changes. Bumped from 1 to 2 in
    /// Phase 1 (D-17 / MIGRATION-05) to track that this export was produced by a v2-aware
    /// install; the deserializer hot path stays on the v1 path until Phase 2 wires the
    /// upcaster chain (MIGRATION-04 / MIGRATION-06).
    /// </summary>
    public int SchemaVersion { get; set; } = 2;
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
