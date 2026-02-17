using CookBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CookBot.Infrastructure.Data;

public class CookBotDbContext : DbContext
{
    public CookBotDbContext(DbContextOptions<CookBotDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
    public DbSet<Cookbook> Cookbooks => Set<Cookbook>();
    public DbSet<Recipe> Recipes => Set<Recipe>();
    public DbSet<Ingredient> Ingredients => Set<Ingredient>();
    public DbSet<RecipeIngredient> RecipeIngredients => Set<RecipeIngredient>();
    public DbSet<PantryItem> PantryItems => Set<PantryItem>();
    public DbSet<GroceryList> GroceryLists => Set<GroceryList>();
    public DbSet<GroceryListItem> GroceryListItems => Set<GroceryListItem>();
    public DbSet<AiConversation> AiConversations => Set<AiConversation>();
    public DbSet<CookbookShare> CookbookShares => Set<CookbookShare>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CookBotDbContext).Assembly);
    }
}
