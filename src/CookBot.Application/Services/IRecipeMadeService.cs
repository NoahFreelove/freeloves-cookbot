using CookBot.Domain.Entities;

namespace CookBot.Application.Services;

public interface IRecipeMadeService
{
    Task<RecipeMade> LogMadeAsync(int recipeId, int userId, string? notes = null, CancellationToken ct = default);
    Task<int> GetMadeCountAsync(int recipeId, int userId, CancellationToken ct = default);
    Task<RecipeMade?> GetLastCookAsync(int recipeId, int userId, CancellationToken ct = default);
    Task<List<RecipeMade>> GetRecentForUserAsync(int userId, int take = 4, CancellationToken ct = default);
}
