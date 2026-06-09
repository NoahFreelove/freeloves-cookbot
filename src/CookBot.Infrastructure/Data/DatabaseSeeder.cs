using System.Text.Json;
using CookBot.Application.Recipes;
using CookBot.Application.Services;
using CookBot.Domain.Entities;
using CookBot.Domain.Enums;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CookBot.Infrastructure.Data;

public static class DatabaseSeeder
{
    private sealed class SeedIngredient
    {
        public string Name { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public List<string> PreferredUnits { get; set; } = [];
    }

    private sealed class CnfFoodSeedRow
    {
        public int FoodId { get; set; }
        public string FoodDescription { get; set; } = string.Empty;
        public string? FoodGroup { get; set; }
        public double EnergyKcalPer100g { get; set; }
        public double ProteinGPer100g { get; set; }
        public double FatGPer100g { get; set; }
        public double CarbGPer100g { get; set; }
    }

    private sealed class CnfCfSeedRow
    {
        public int FoodId { get; set; }
        public string MeasureDescription { get; set; } = string.Empty;
        public double ConversionFactorValue { get; set; }
    }

    /// <summary>
    /// PROD-09 / PITFALL C3 (Phase 9 / Plan 09-04) — sentinel-prefix detection for
    /// the AI-key migration pass. Returns true iff <paramref name="value"/> looks
    /// like an ASP.NET Core Data Protection ciphertext blob (starts with "CfDJ8" and
    /// is at least 44 characters long). The threshold is the empirical minimum size
    /// of a protected blob; legacy plaintext Anthropic keys are ~108 chars but never
    /// start with "CfDJ8", so this discriminator is safe.
    /// </summary>
    /// <remarks>
    /// Shared between <see cref="SeedAsync"/> (write-path migration) and
    /// <c>AiApiKeyResolutionService</c> (read-path gating). Single source of truth.
    /// </remarks>
    public static bool LooksLikeDataProtectionCiphertext(string? value) =>
        !string.IsNullOrEmpty(value)
        && value.Length >= 44
        && value.StartsWith("CfDJ8", StringComparison.Ordinal);

    public static async Task SeedAsync(
        CookBotDbContext context,
        IDatabaseBackupService backupService,
        JsonRecipeSerializer serializer,
        IDataProtectionProvider dataProtectionProvider,
        ILogger logger,
        string contentRootPath)
    {
        // Step 1: backup before migrate (D-15 / MIGRATION-02 / Pitfall C4).
        // Conditional on a non-empty pending list — skips on no-op startups.
        // D-31: derive the backup label from the FIRST pending migration name (e.g. "AddRecipePhotoUrlAndDescription")
        // so each migration in Plans 07/08/11/12 produces its own correctly-named .pre-{Name}.bak file.
        var pending = (await context.Database.GetPendingMigrationsAsync()).ToList();
        if (pending.Count > 0)
        {
            // Migration names are formatted as "{timestamp}_{Name}" — split on first underscore to recover just the class name.
            var raw = pending[0];
            var label = raw.Split('_', 2).Length == 2 ? raw.Split('_', 2)[1] : raw;
            await backupService.BackupBeforeMigrationAsync(label, CancellationToken.None);
        }

        // Step 2: apply migrations.
        await context.Database.MigrateAsync();

        // NUTR-01 / Phase 15 / Plan 15-03 — load CNF seed tables idempotently.
        // Runs BEFORE the existing-user early-return so it executes on every first-startup
        // regardless of whether a default user row is already present.
        await SeedCnfDataAsync(context, contentRootPath);

        // D-32 step a / CLEAN-01 / D-33: permanent structural invariant.
        // Any code path that creates a Recipe without writing CanonicalDocumentJson is a bug
        // and the guard is the load-bearing detection mechanism going forward.
        var nullCanonicalCount = await context.Recipes.CountAsync(r => r.CanonicalDocumentJson == null);
        if (nullCanonicalCount > 0)
        {
            throw new InvalidOperationException(
                $"{nullCanonicalCount} recipe(s) have null CanonicalDocumentJson after migrate. " +
                "This indicates an incomplete v1.1 backfill — restore from cookbot.db.pre-* backup and re-run.");
        }

        // Step 3 (legacy backfill removed): CLEAN-01 (Plan 10 / D-32 steps b-e).
        // All rows have CanonicalDocumentJson populated from Phase 1's milestone backfill.
        // The null-canonical guard above (D-33) catches any future regression.

        // D-41 (Phase 9 / Plan 09-05) — hardcoded 365-day rolling cleanup of AiUsageLog rows.
        // Runs BEFORE the sentinel-prefix re-encryption pass per 09-CONTEXT "Established Patterns":
        // the cleanup eliminates the edge case of re-encrypting a row that's about to be deleted
        // (note: AiUsageLog has no AiApiKey field — the two passes target different tables — but
        // the documented order is cleanup-then-reencrypt for consistency).
        var aiUsageLogCutoff = DateTime.UtcNow.AddDays(-365);
        var deletedCount = await context.AiUsageLogs
            .Where(r => r.Timestamp < aiUsageLogCutoff)
            .ExecuteDeleteAsync();
        if (deletedCount > 0)
            logger.LogInformation("Pruned {Count} AiUsageLog row(s) older than 365 days.", deletedCount);

        // PROD-09 / PITFALL C3 (Phase 9 / Plan 09-04) — sentinel-prefix re-encryption pass.
        // Idempotent: any UserProfile row whose AiApiKey is non-empty and does NOT already
        // look like a Data Protection ciphertext (CfDJ8…) is treated as legacy plaintext
        // and rewritten in place. Second boot detects the ciphertext sentinel and short-circuits.
        // Note: we intentionally avoid an EF ValueConverter — that approach forces every read
        // through Unprotect, which throws on legacy plaintext during the very migration that's
        // supposed to fix it (09-RESEARCH Item 2 correction).
        var protector = dataProtectionProvider.CreateProtector("AiApiKey.v1");
        var legacyRows = await context.UserProfiles.AsNoTracking()
            .Where(p => p.AiApiKey != null && p.AiApiKey != "")
            .Select(p => new { p.UserId, p.AiApiKey })
            .ToListAsync();
        var reencrypted = 0;
        foreach (var row in legacyRows)
        {
            if (LooksLikeDataProtectionCiphertext(row.AiApiKey))
                continue;
            var encrypted = protector.Protect(row.AiApiKey!);
            await context.UserProfiles
                .Where(p => p.UserId == row.UserId)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.AiApiKey, encrypted));
            reencrypted++;
        }
        if (reencrypted > 0)
        {
            // Log only the COUNT — never the values. The redactor cannot rescue a log line
            // that captures the plaintext directly here.
            logger.LogInformation("Re-encrypted {Count} legacy plaintext AI API key(s) at startup.", reencrypted);
        }

        // Step 4: existing seed logic — unchanged.
        if (await context.Users.AnyAsync())
        {
            // Ensure all existing users have a personal pantry
            var usersWithoutPantry = await context.Users
                .Where(u => !context.Pantries.Any(p => p.OwnerId == u.Id && p.IsPersonal))
                .ToListAsync();
            foreach (var user in usersWithoutPantry)
            {
                context.Pantries.Add(new Pantry
                {
                    Owner = user,
                    Name = "Personal Pantry",
                    IsPersonal = true,
                });
            }
            if (usersWithoutPantry.Any())
                await context.SaveChangesAsync();
            await EnsureAtLeastOneCookBotAdminAsync(context);
            return;
        }

        var defaultUser = new User
        {
            DisplayName = "Home Chef",
            IsCookBotAdmin = true,
            Profile = new UserProfile
            {
                ExperienceLevel = ExperienceLevel.Intermediate,
                UnitSystem = UnitSystem.Canadian,
            }
        };
        context.Users.Add(defaultUser);

        var personalPantry = new Pantry
        {
            Owner = defaultUser,
            Name = "Personal Pantry",
            IsPersonal = true,
        };
        context.Pantries.Add(personalPantry);

        var defaultCookbook = new Cookbook
        {
            User = defaultUser,
            Name = "My Recipes",
            Description = "Default cookbook"
        };
        context.Cookbooks.Add(defaultCookbook);

        var ingredients = await LoadIngredientsFromSeedFile(contentRootPath);
        var existingNormalized = await context.Ingredients
            .Select(i => i.NormalizedName)
            .ToHashSetAsync();

        foreach (var ingredient in ingredients)
        {
            if (!existingNormalized.Contains(ingredient.NormalizedName))
            {
                context.Ingredients.Add(ingredient);
            }
        }

        await context.SaveChangesAsync();
        await EnsureAtLeastOneCookBotAdminAsync(context);
    }

    /// <summary>If no admin is set (e.g. legacy DB), promote the Home Chef account or the lowest-Id user.</summary>
    private static async Task EnsureAtLeastOneCookBotAdminAsync(CookBotDbContext context)
    {
        if (await context.Users.AnyAsync(u => u.IsCookBotAdmin))
            return;

        var homeChef = await context.Users
            .Where(u => u.DisplayName == "Home Chef")
            .OrderBy(u => u.Id)
            .FirstOrDefaultAsync();
        var promote = homeChef ?? await context.Users.OrderBy(u => u.Id).FirstAsync();
        promote.IsCookBotAdmin = true;
        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Resolves a bundled seed file by walking up from the content root looking for
    /// <c>{ancestor}/{segments}</c>. Robust across `dotnet run` (content root =
    /// <c>src/CookBot.Web</c>, seeds two levels up at the repo root) and Docker (seeds copied beside
    /// the content root at <c>/app/seeds</c>, matched at the first ancestor). Returns the first
    /// existing match, or null when no seed exists above the content root (the "nutrition
    /// unavailable" path). The search is intentionally bounded to the content root's ancestors so a
    /// missing seed is genuinely missing — it does not fall back to the binary's base directory.
    /// </summary>
    private static string? ResolveSeedFile(string contentRootPath, params string[] segments)
    {
        for (var dir = new DirectoryInfo(contentRootPath); dir is not null; dir = dir.Parent)
        {
            var parts = new string[segments.Length + 1];
            parts[0] = dir.FullName;
            Array.Copy(segments, 0, parts, 1, segments.Length);
            var candidate = Path.GetFullPath(Path.Combine(parts));
            if (File.Exists(candidate)) return candidate;
        }
        return null;
    }

    private static async Task<List<Ingredient>> LoadIngredientsFromSeedFile(string contentRootPath)
    {
        var seedPath = ResolveSeedFile(contentRootPath, "seeds", "ingredients.json");

        if (seedPath is null)
        {
            return [];
        }

        var json = await File.ReadAllTextAsync(seedPath);
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var seedItems = JsonSerializer.Deserialize<List<SeedIngredient>>(json, options) ?? [];

        var ingredients = new List<Ingredient>();
        foreach (var item in seedItems)
        {
            if (!Enum.TryParse<IngredientCategory>(item.Category, ignoreCase: true, out var category))
            {
                category = IngredientCategory.Other;
            }

            ingredients.Add(new Ingredient
            {
                Name = item.Name,
                NormalizedName = IngredientResolver.Normalize(item.Name),
                Category = category,
                PreferredUnitsJson = item.PreferredUnits.Count > 0
                    ? JsonSerializer.Serialize(item.PreferredUnits)
                    : null,
            });
        }

        return ingredients;
    }

    /// <summary>
    /// NUTR-01 / Phase 15 / Plan 15-03 — loads the bundled CNF seed files idempotently.
    /// <para>
    /// Idempotent guard: returns immediately when <c>CnfFoods</c> already has rows — a second
    /// startup does not re-seed. Mirrors the <c>context.Users.AnyAsync()</c> guard at line 120.
    /// </para>
    /// <para>
    /// Missing seed file → quiet return, no startup throw (T-15-06 mitigation).
    /// Nutrition is simply unavailable until the seed is present.
    /// </para>
    /// <para>
    /// Two-pass load: foods first (SaveChanges to commit the PKs), then conversion factors
    /// (requires CnfFood rows to exist for the FK constraint).
    /// </para>
    /// <para>
    /// <c>NormalizedDescription</c> is pre-computed at seed time via <c>IngredientNormalizer.Normalize</c>
    /// (Research Target 4) so runtime matching does not re-normalize 5 690 strings per ingredient.
    /// Values are inserted verbatim — OGL-Canada forbids modifying nutrient values (T-15-05 mitigation).
    /// </para>
    /// </summary>
    private static async Task SeedCnfDataAsync(CookBotDbContext context, string contentRootPath)
    {
        // Idempotent guard — mirrors the context.Users.AnyAsync() early-return pattern.
        if (await context.CnfFoods.AnyAsync()) return;

        // ── Pass 1: CNF foods ──────────────────────────────────────────────────────────────
        var foodsPath = ResolveSeedFile(contentRootPath, "seeds", "nutrition", "cnf_foods.json");
        if (foodsPath is null) return; // T-15-06: quiet return — nutrition unavailable, app still boots

        var foodsJson = await File.ReadAllTextAsync(foodsPath);
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var foodRows = JsonSerializer.Deserialize<List<CnfFoodSeedRow>>(foodsJson, options) ?? [];

        foreach (var row in foodRows)
        {
            context.CnfFoods.Add(new CnfFood
            {
                FoodId              = row.FoodId,
                FoodDescription     = row.FoodDescription,
                NormalizedDescription = IngredientNormalizer.Normalize(row.FoodDescription),
                FoodGroup           = row.FoodGroup,
                EnergyKcalPer100g   = row.EnergyKcalPer100g,  // verbatim — OGL-Canada
                ProteinGPer100g     = row.ProteinGPer100g,
                FatGPer100g         = row.FatGPer100g,
                CarbGPer100g        = row.CarbGPer100g,
            });
        }
        await context.SaveChangesAsync();

        // ── Pass 2: conversion factors (after CnfFood PKs are committed) ──────────────────
        var cfPath = ResolveSeedFile(contentRootPath, "seeds", "nutrition", "cnf_conversion_factors.json");
        if (cfPath is null) return; // missing CF file — foods loaded, factors unavailable

        var cfJson = await File.ReadAllTextAsync(cfPath);
        var cfRows = JsonSerializer.Deserialize<List<CnfCfSeedRow>>(cfJson, options) ?? [];

        foreach (var cf in cfRows)
        {
            context.CnfConversionFactors.Add(new CnfConversionFactor
            {
                FoodId                = cf.FoodId,
                MeasureDescription    = cf.MeasureDescription,
                ConversionFactorValue = cf.ConversionFactorValue, // verbatim — OGL-Canada
            });
        }
        await context.SaveChangesAsync();
    }
}
