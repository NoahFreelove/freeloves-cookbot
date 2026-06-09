using CookBot.Application;
using CookBot.Application.AI;
using CookBot.Application.Services;
using CookBot.Domain.Interfaces;
using CookBot.Infrastructure.AI;
using CookBot.Infrastructure.Data;
using CookBot.Infrastructure.Data.Repositories;
using CookBot.Infrastructure.Services;
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
        // AI-01 — same instance, second interface (Plan 02-02). AnthropicAiService implements both.
        services.AddScoped<IStructuredAiService>(sp => (IStructuredAiService)sp.GetRequiredService<IAiService>());
        services.AddScoped<PromptBuilderService>();

        // Phase 9 / Plan 09-05 / PROD-14 — AiRecipeGenerator (Application) sees this interface only.
        services.AddScoped<IAiUsageLogWriter, AiUsageLogWriter>();

        // Phase 1 / D-15: pre-migration backup.
        // CLEAN-01 (Plan 10): projector DI registrations removed (D-32 step d).
        services.AddSingleton<IDatabaseBackupService, DatabaseBackupService>();

        // Phase 14 / Plan 14-03 / GALLERY-02/03 — gallery CRUD service.
        // Lives in Infrastructure (not Application) because it needs CookBotDbContext directly
        // for ExecuteUpdateAsync / OrderBy / CountAsync that the generic IRepository<T> doesn't expose.
        services.AddScoped<RecipePhotoService>();

        services.AddApplication();

        return services;
    }
}
