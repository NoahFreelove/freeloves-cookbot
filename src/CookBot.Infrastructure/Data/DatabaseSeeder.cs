using System.Text.Json;
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

    public static async Task SeedAsync(CookBotDbContext context, string contentRootPath)
    {
        await context.Database.MigrateAsync();

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
            return;
        }

        var defaultUser = new User
        {
            DisplayName = "Home Chef",
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
