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
        services.AddSingleton<RecipeUnitDisplayService>();
        services.AddScoped<CookbookService>();
        services.AddScoped<RecipeService>();
        services.AddScoped<PantryService>();
        services.AddScoped<PantryAiPopulationService>();
        services.AddScoped<GroceryListService>();

        // Phase 1 canonical-format scaffold (Plan 01-01). Stateless pure services -> Singleton.
        services.AddSingleton<IRecipeSchemaDocumentationProvider, RecipeSchemaDocumentationProvider>();
        services.AddSingleton<RecipeJsonSchemaProvider>();
        services.AddSingleton<RecipeValidator>();
        // Phase 9 / Plan 09-01 / PHOTO-07 — scheme-allowlist validator for paste-URLs and AI-emitted PhotoUrl.
        services.AddSingleton<RecipePhotoUrlValidator>();
        services.AddSingleton<JsonRecipeSerializer>();
        services.AddSingleton<IRecipeUpcaster, Migration_V1_To_V2>();
        services.AddSingleton<IRecipeUpcaster, Migration_V2_To_V3>();
        services.AddSingleton<IRecipeUpcaster, Migration_V3_To_V4>();  // Phase 12
        services.AddSingleton<RecipeUpcasterChain>();

        // Phase 2 Plan 03 (AI-02 / AI-03). Orchestrator is stateless, but IStructuredAiService
        // is registered Scoped (Plan 02 — same instance as IAiService). DI validation forbids
        // a Singleton consuming a Scoped dependency, so the orchestrator is Scoped too.
        services.AddScoped<IAiRecipeGenerator, AiRecipeGenerator>();

        // Phase 10 / QOL-01..03 — pantry-match scoring service (D-44..47).
        // Scoped because it depends on PantryService (also Scoped) and IRecipeMadeService.
        services.AddScoped<IPantryMatchService, PantryMatchService>();

        return services;
    }
}
