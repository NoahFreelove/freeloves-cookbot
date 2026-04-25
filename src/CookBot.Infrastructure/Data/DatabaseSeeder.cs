using System.Text.Json;
using CookBot.Application.Recipes;
using CookBot.Application.Services;
using CookBot.Domain.Entities;
using CookBot.Domain.Enums;
using CookBot.Infrastructure.Data.Migrations.Helpers;
using Microsoft.EntityFrameworkCore;

namespace CookBot.Infrastructure.Data;

public static class DatabaseSeeder
{
    private sealed class SeedIngredient
    {
        public string Name { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public List<string> PreferredUnits { get; set; } = [];
    }

    public static async Task SeedAsync(
        CookBotDbContext context,
        IDatabaseBackupService backupService,
        LegacyRecipeProjector projector,
        JsonRecipeSerializer serializer,
        string contentRootPath)
    {
        // Step 1: backup before migrate (D-15 / MIGRATION-02 / Pitfall C4).
        // Conditional on a non-empty pending list — skips on no-op startups.
        var pending = (await context.Database.GetPendingMigrationsAsync()).ToList();
        if (pending.Count > 0)
        {
            await backupService.BackupBeforeMigrationAsync("RecipeCanonicalDocument", CancellationToken.None);
        }

        // Step 2: apply migrations.
        await context.Database.MigrateAsync();

        // Step 3: idempotent backfill (D-16 / MIGRATION-01 / MIGRATION-07).
        await BackfillCanonicalDocumentAsync(context, projector, serializer);

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

    /// <summary>
    /// Idempotent canonical-document backfill (D-16 / MIGRATION-01 / MIGRATION-07).
    /// Selects only rows where <c>CanonicalDocumentJson</c> is NULL, batched at 50 to bound memory.
    /// On a fresh install (zero recipes) or a re-run after backfill is complete, this is a no-op.
    /// </summary>
    private static async Task BackfillCanonicalDocumentAsync(
        CookBotDbContext db,
        LegacyRecipeProjector projector,
        JsonRecipeSerializer serializer)
    {
        const int batchSize = 50;
        while (true)
        {
            var batch = await db.Recipes
                .Include(r => r.RecipeIngredients).ThenInclude(ri => ri.Ingredient)
                .Include(r => r.Steps)
                .Where(r => r.CanonicalDocumentJson == null)
                .Take(batchSize)
                .ToListAsync();
            if (batch.Count == 0) break;
            foreach (var recipe in batch)
            {
                var doc = projector.Project(recipe);
                recipe.CanonicalDocumentJson = serializer.Serialize(doc);
            }
            await db.SaveChangesAsync();
        }
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

    private static async Task<List<Ingredient>> LoadIngredientsFromSeedFile(string contentRootPath)
    {
        var seedPath = Path.GetFullPath(Path.Combine(contentRootPath, "..", "seeds", "ingredients.json"));

        if (!File.Exists(seedPath))
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
}
