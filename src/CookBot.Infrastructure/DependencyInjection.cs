using CookBot.Application;
using CookBot.Application.AI;
using CookBot.Application.Services;
using CookBot.Domain.Interfaces;
using CookBot.Infrastructure.AI;
using CookBot.Infrastructure.Data;
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
        // AI-01 — same instance, second interface (Plan 02-02). AnthropicAiService implements both.
        services.AddScoped<IStructuredAiService>(sp => (IStructuredAiService)sp.GetRequiredService<IAiService>());
        services.AddScoped<PromptBuilderService>();

        // Phase 1 / D-15: pre-migration backup.
        // CLEAN-01 (Plan 10): projector DI registrations removed (D-32 step d).
        services.AddSingleton<IDatabaseBackupService, DatabaseBackupService>();

        services.AddApplication();

        return services;
    }
}
