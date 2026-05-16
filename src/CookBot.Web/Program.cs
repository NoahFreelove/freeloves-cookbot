using CookBot.Application.DTOs;
using CookBot.Application.Recipes;
using CookBot.Infrastructure;
using CookBot.Infrastructure.Data;
using CookBot.Web.Components;
using CookBot.Web.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.FileProviders;
using QuestPDF.Infrastructure;

QuestPDF.Settings.License = LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);

// Phase 9 / Plan 09-01 / PHOTO-04 + PITFALL H1 — raise all three server-side size
// limits to 12 MB (12 * 1024 * 1024 bytes). The LocalRecipePhotoStorage per-file cap
// is 10 MB; these outer limits sit 2 MB higher so a 10 MB upload doesn't trip an
// outer boundary first. All three knobs are necessary: the Blazor SignalR
// MaximumReceiveMessageSize is the limit that silently drops circuits at 32 KB by
// default. Literals are intentionally repeated at each call site so a static
// auditor / grep can confirm all three are 12 MB without chasing a constant.
builder.Services.Configure<KestrelServerOptions>(o => o.Limits.MaxRequestBodySize = 12 * 1024 * 1024);
builder.Services.Configure<FormOptions>(o => o.MultipartBodyLengthLimit = 12 * 1024 * 1024);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    // Blazor Server SignalR per-hub limit (PITFALL H1) — without this, the
    // SignalR circuit silently truncates messages larger than 32 KB (default),
    // dropping the upload circuit. MaximumReceiveMessageSize is the load-bearing
    // knob; the Kestrel + FormOptions limits above sit above the wire frame.
    .AddHubOptions(o => o.MaximumReceiveMessageSize = 12 * 1024 * 1024);

builder.Services.AddScoped<ICbDialogService, CbDialogService>();
builder.Services.AddSingleton<ICbToastService, CbToastService>();
builder.Services.AddInfrastructure(builder.Configuration);

// Phase 9 / Plan 09-04 / PROD-07: ApplicationName is load-bearing — changing it
// invalidates every encrypted AI key (09-RESEARCH pitfall #6). Keep this literal.
builder.Services.AddDataProtection()
    .SetApplicationName("FreelovesCookBot")
    .PersistKeysToDbContext<CookBotDbContext>();

builder.Services.AddScoped<CurrentUserService>();
builder.Services.AddScoped<AiApiKeyResolutionService>();
builder.Services.AddScoped<AiApiKeyShareService>();
builder.Services.AddScoped<CookbookTransferService>();
builder.Services.AddScoped<CookbookPdfService>();
builder.Services.AddScoped<LocalRecipePhotoStorage>();
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

// Phase 9 / Plan 09-01 / PHOTO-06 + PITFALL H3 — serve /uploads via an explicit
// PhysicalFileProvider with X-Content-Type-Options: nosniff on every response. The
// nosniff header prevents the browser from sniffing an uploaded image as HTML or
// SVG (which could execute scripts), even if a future bug let a non-image past the
// magic-byte check.
var uploadsPath = Path.Combine(app.Environment.WebRootPath, "uploads");
Directory.CreateDirectory(uploadsPath); // idempotent — covers fresh-clone path
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(uploadsPath),
    RequestPath = "/uploads",
    OnPrepareResponse = ctx =>
    {
        ctx.Context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    },
});

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Seed database
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<CookBotDbContext>();
    var backupService = scope.ServiceProvider.GetRequiredService<IDatabaseBackupService>();
    var canonicalSerializer = scope.ServiceProvider.GetRequiredService<JsonRecipeSerializer>();
    // Phase 9 / Plan 09-04 — supply Data Protection provider + logger so the seeder
    // can run the idempotent sentinel-prefix re-encryption pass on legacy plaintext keys.
    var dataProtectionProvider = scope.ServiceProvider.GetRequiredService<IDataProtectionProvider>();
    var seederLogger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
        .CreateLogger("DatabaseSeeder");
    await DatabaseSeeder.SeedAsync(
        context,
        backupService,
        canonicalSerializer,
        dataProtectionProvider,
        seederLogger,
        app.Environment.ContentRootPath);
}

app.Run();
