using CookBot.Application;
using CookBot.Application.Services;
using CookBot.Domain.Interfaces;
using CookBot.Infrastructure.AI;
using CookBot.Infrastructure.Data;
using CookBot.Infrastructure.Data.Migrations.Helpers;
using CookBot.Infrastructure.Data.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CookBot.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<CookBotDbContext>(options =>
            options.UseSqlite(configuration.GetConnectionString("DefaultConnection") ?? "Data Source=cookbot.db"));

        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        services.AddScoped<IAiService, AnthropicAiService>();
        services.AddScoped<PromptBuilderService>();

        // Phase 1 / D-15: pre-migration backup; D-14: relational -> RecipeDocument projector.
        services.AddSingleton<IDatabaseBackupService, DatabaseBackupService>();
        services.AddScoped<LegacyRecipeProjector>();
        // Bind IRecipeProjector to the same scoped instance so RecipeService and the seeder
        // see the same projector and CookBot.Application stays infrastructure-free.
        services.AddScoped<CookBot.Application.Recipes.IRecipeProjector>(sp => sp.GetRequiredService<LegacyRecipeProjector>());

        services.AddApplication();

        return services;
    }
}
