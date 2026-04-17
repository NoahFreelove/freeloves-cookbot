using Microsoft.EntityFrameworkCore;

namespace CookBot.Infrastructure.Data;

public static class RecipeAccessExtensions
{
    /// <summary>
    /// True if the user owns the recipe's cookbook or has a share on that cookbook.
    /// </summary>
    public static Task<bool> UserCanAccessRecipeAsync(
        this CookBotDbContext db,
        int recipeId,
        int userId,
        CancellationToken cancellationToken = default) =>
        db.Recipes.AsNoTracking()
            .Where(r => r.Id == recipeId)
            .AnyAsync(
                r => r.Cookbook.UserId == userId
                     || r.Cookbook.Shares.Any(s => s.SharedWithUserId == userId),
                cancellationToken);
}
