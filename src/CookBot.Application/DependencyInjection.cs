using CookBot.Application.AI;
using CookBot.Application.Recipes;
using CookBot.Application.Services;
using CookBot.Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace CookBot.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton<IRecipeFormatParser, RecipeFormatParser>();
        services.AddSingleton<IUnitConverter, UnitConversionService>();
        services.AddScoped<CookbookService>();
        services.AddScoped<RecipeService>();
        services.AddScoped<PantryService>();
        services.AddScoped<PantryAiPopulationService>();
        services.AddScoped<GroceryListService>();

        // Phase 1 canonical-format scaffold (Plan 01-01). Stateless pure services -> Singleton.
        services.AddSingleton<IRecipeSchemaDocumentationProvider, RecipeSchemaDocumentationProvider>();
        services.AddSingleton<RecipeJsonSchemaProvider>();
        services.AddSingleton<RecipeValidator>();
        services.AddSingleton<JsonRecipeSerializer>();
        services.AddSingleton<IRecipeUpcaster, Migration_V1_To_V2>();
        services.AddSingleton<RecipeUpcasterChain>();

        // Phase 2 Plan 03 (AI-02 / AI-03). Stateless orchestrator -> Singleton.
        services.AddSingleton<IAiRecipeGenerator, AiRecipeGenerator>();

        return services;
    }
}
