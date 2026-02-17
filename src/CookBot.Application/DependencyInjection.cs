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
        services.AddScoped<GroceryListService>();
        return services;
    }
}
