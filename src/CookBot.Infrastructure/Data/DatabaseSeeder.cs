using CookBot.Domain.Entities;
using CookBot.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CookBot.Infrastructure.Data;

public static class DatabaseSeeder
{
    public static async Task SeedAsync(CookBotDbContext context)
    {
        await context.Database.MigrateAsync();

        if (await context.Users.AnyAsync())
            return;

        var user = new User
        {
            DisplayName = "Home Chef",
            Profile = new UserProfile
            {
                ExperienceLevel = ExperienceLevel.Intermediate,
                UnitSystem = UnitSystem.Canadian,
            }
        };
        context.Users.Add(user);

        var defaultCookbook = new Cookbook
        {
            User = user,
            Name = "My Recipes",
            Description = "Default cookbook"
        };
        context.Cookbooks.Add(defaultCookbook);

        var ingredients = GetCommonIngredients();
        context.Ingredients.AddRange(ingredients);

        await context.SaveChangesAsync();
    }

    private static List<Ingredient> GetCommonIngredients() =>
    [
        new() { Name = "All-Purpose Flour", NormalizedName = "all purpose flour", Category = IngredientCategory.Pantry },
        new() { Name = "Granulated Sugar", NormalizedName = "granulated sugar", Category = IngredientCategory.Pantry },
        new() { Name = "Brown Sugar", NormalizedName = "brown sugar", Category = IngredientCategory.Pantry },
        new() { Name = "Salt", NormalizedName = "salt", Category = IngredientCategory.Spices },
        new() { Name = "Black Pepper", NormalizedName = "black pepper", Category = IngredientCategory.Spices },
        new() { Name = "Butter", NormalizedName = "butter", Category = IngredientCategory.Dairy },
        new() { Name = "Eggs", NormalizedName = "eggs", Category = IngredientCategory.Dairy },
        new() { Name = "Whole Milk", NormalizedName = "whole milk", Category = IngredientCategory.Dairy },
        new() { Name = "Olive Oil", NormalizedName = "olive oil", Category = IngredientCategory.Condiments },
        new() { Name = "Vegetable Oil", NormalizedName = "vegetable oil", Category = IngredientCategory.Condiments },
        new() { Name = "Garlic", NormalizedName = "garlic", Category = IngredientCategory.Produce },
        new() { Name = "Onion", NormalizedName = "onion", Category = IngredientCategory.Produce },
        new() { Name = "Tomatoes", NormalizedName = "tomatoes", Category = IngredientCategory.Produce },
        new() { Name = "Chicken Breast", NormalizedName = "chicken breast", Category = IngredientCategory.Meat },
        new() { Name = "Ground Beef", NormalizedName = "ground beef", Category = IngredientCategory.Meat },
        new() { Name = "Rice", NormalizedName = "rice", Category = IngredientCategory.Grains },
        new() { Name = "Pasta", NormalizedName = "pasta", Category = IngredientCategory.Grains },
        new() { Name = "Baking Soda", NormalizedName = "baking soda", Category = IngredientCategory.Pantry },
        new() { Name = "Baking Powder", NormalizedName = "baking powder", Category = IngredientCategory.Pantry },
        new() { Name = "Vanilla Extract", NormalizedName = "vanilla extract", Category = IngredientCategory.Pantry },
        new() { Name = "Cinnamon", NormalizedName = "cinnamon", Category = IngredientCategory.Spices },
        new() { Name = "Paprika", NormalizedName = "paprika", Category = IngredientCategory.Spices },
        new() { Name = "Cumin", NormalizedName = "cumin", Category = IngredientCategory.Spices },
        new() { Name = "Soy Sauce", NormalizedName = "soy sauce", Category = IngredientCategory.Condiments },
        new() { Name = "Lemon", NormalizedName = "lemon", Category = IngredientCategory.Produce },
        new() { Name = "Cheddar Cheese", NormalizedName = "cheddar cheese", Category = IngredientCategory.Dairy },
        new() { Name = "Parmesan Cheese", NormalizedName = "parmesan cheese", Category = IngredientCategory.Dairy },
        new() { Name = "Heavy Cream", NormalizedName = "heavy cream", Category = IngredientCategory.Dairy },
        new() { Name = "Chicken Broth", NormalizedName = "chicken broth", Category = IngredientCategory.Canned },
        new() { Name = "Chocolate Chips", NormalizedName = "chocolate chips", Category = IngredientCategory.Pantry },
    ];
}
