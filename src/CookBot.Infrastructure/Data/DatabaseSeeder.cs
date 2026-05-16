using System.Text.Json;
using CookBot.Application.Recipes;
using CookBot.Application.Services;
using CookBot.Domain.Entities;
using CookBot.Domain.Enums;
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
        JsonRecipeSerializer serializer,
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
