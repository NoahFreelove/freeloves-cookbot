namespace CookBot.Application.DTOs;

/// <summary>
/// Per-serving nutrition value object passed to <c>JsonLdRecipeProjector.Project</c>
/// as its optional third parameter (D-15-13 / NUTR-06 / Phase 15).
/// <para>
/// Pure value type — no EF, no DI, no data-service access. The projector stays pure;
/// the Web layer (<c>RecipeView</c>) constructs this from the <c>RecipeNutritionCache</c>
/// and passes it in. Schema.org <c>nutrition</c> is per-serving, so all four fields
/// represent per-serving values.
/// </para>
/// </summary>
public sealed record NutritionInfoDto(
    double CaloriesPerServing,
    double ProteinGPerServing,
    double FatGPerServing,
    double CarbGPerServing
);
