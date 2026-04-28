using CookBot.Application.DTOs;
using CookBot.Application.Recipes;
using CookBot.Infrastructure;
using CookBot.Infrastructure.Data;
using CookBot.Infrastructure.Data.Migrations.Helpers;
using CookBot.Web.Components;
using CookBot.Web.Services;
using QuestPDF.Infrastructure;

QuestPDF.Settings.License = LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddScoped<ICbDialogService, CbDialogService>();
builder.Services.AddSingleton<ICbToastService, CbToastService>();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScoped<CurrentUserService>();
builder.Services.AddScoped<AiApiKeyResolutionService>();
builder.Services.AddScoped<AiApiKeyShareService>();
builder.Services.AddScoped<CookbookTransferService>();
builder.Services.AddScoped<CookbookPdfService>();
builder.Services.AddScoped<IScheduledRecipeService, ScheduledRecipeService>();
builder.Services.AddScoped<IRecipeMadeService, RecipeMadeService>();
builder.Services.Configure<CookBotSettings>(builder.Configuration.GetSection("CookBot"));

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Seed database
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<CookBotDbContext>();
    var backupService = scope.ServiceProvider.GetRequiredService<IDatabaseBackupService>();
    var projector = scope.ServiceProvider.GetRequiredService<LegacyRecipeProjector>();
    var canonicalSerializer = scope.ServiceProvider.GetRequiredService<JsonRecipeSerializer>();
    await DatabaseSeeder.SeedAsync(
        context,
        backupService,
        projector,
        canonicalSerializer,
        app.Environment.ContentRootPath);
}

app.Run();
