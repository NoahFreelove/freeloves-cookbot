using CookBot.Application.DTOs;
using CookBot.Infrastructure;
using CookBot.Infrastructure.Data;
using CookBot.Web.Components;
using CookBot.Web.Services;
using MudBlazor.Services;
using QuestPDF.Infrastructure;

QuestPDF.Settings.License = LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddMudServices();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScoped<CurrentUserService>();
builder.Services.AddScoped<AiApiKeyResolutionService>();
builder.Services.AddScoped<AiApiKeyShareService>();
builder.Services.AddScoped<CookbookTransferService>();
builder.Services.AddScoped<CookbookPdfService>();
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
    await DatabaseSeeder.SeedAsync(context, app.Environment.ContentRootPath);
}

app.Run();
