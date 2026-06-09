using CookBot.Domain.Entities;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CookBot.Infrastructure.Data;

// PROD-06 / Phase 9 / Plan 09-04: implements IDataProtectionKeyContext so the
// Data Protection key ring persists into cookbot.db alongside the rest of the schema.
// Without this interface, AddDataProtection().PersistKeysToDbContext<CookBotDbContext>()
// in Program.cs has nowhere to store its keys and the migration AddDataProtectionKeysTable
// has no DbSet to model.
public class CookBotDbContext : DbContext, IDataProtectionKeyContext
{
    public CookBotDbContext(DbContextOptions<CookBotDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
    public DbSet<Cookbook> Cookbooks => Set<Cookbook>();
    public DbSet<Recipe> Recipes => Set<Recipe>();
    public DbSet<Ingredient> Ingredients => Set<Ingredient>();
    public DbSet<RecipeIngredient> RecipeIngredients => Set<RecipeIngredient>();
    public DbSet<PantryItem> PantryItems => Set<PantryItem>();
    public DbSet<Pantry> Pantries => Set<Pantry>();
    public DbSet<PantryMember> PantryMembers => Set<PantryMember>();
    public DbSet<GroceryList> GroceryLists => Set<GroceryList>();
    public DbSet<GroceryListItem> GroceryListItems => Set<GroceryListItem>();
    public DbSet<AiConversation> AiConversations => Set<AiConversation>();
    public DbSet<CookbookShare> CookbookShares => Set<CookbookShare>();
    public DbSet<AiApiKeyShare> AiApiKeyShares => Set<AiApiKeyShare>();
    public DbSet<ScheduledRecipe> ScheduledRecipes => Set<ScheduledRecipe>();
    public DbSet<RecipeMade> RecipeMades => Set<RecipeMade>();
    public DbSet<RecipeTag> RecipeTags => Set<RecipeTag>();
    // GALLERY-01 / Phase 14 / Plan 14-01 — multi-photo gallery backing store.
    public DbSet<RecipePhoto> RecipePhotos => Set<RecipePhoto>();
    // PROD-14 / Phase 9 / Plan 09-05 — token-cost telemetry log row written by AiRecipeGenerator.
    public DbSet<AiUsageLog> AiUsageLogs => Set<AiUsageLog>();
    // PROD-06: Data Protection key ring storage (Microsoft.AspNetCore.DataProtection.EntityFrameworkCore).
    public DbSet<Microsoft.AspNetCore.DataProtection.EntityFrameworkCore.DataProtectionKey> DataProtectionKeys =>
        Set<Microsoft.AspNetCore.DataProtection.EntityFrameworkCore.DataProtectionKey>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CookBotDbContext).Assembly);
    }
}
