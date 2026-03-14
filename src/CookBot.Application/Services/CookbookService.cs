using CookBot.Domain.Entities;
using CookBot.Domain.Interfaces;

namespace CookBot.Application.Services;

public class CookbookService
{
    private readonly IRepository<Cookbook> _cookbookRepo;

    public CookbookService(IRepository<Cookbook> cookbookRepo)
    {
        _cookbookRepo = cookbookRepo;
    }

    public async Task<IReadOnlyList<Cookbook>> GetUserCookbooksAsync(int userId) =>
        await _cookbookRepo.FindAsync(c => c.UserId == userId);

    public async Task<Cookbook?> GetByIdAsync(int id, int userId)
    {
        var cookbook = await _cookbookRepo.GetByIdAsync(id);
        if (cookbook == null) return null;

        if (cookbook.UserId == userId)
            return cookbook;

        if (cookbook.Shares.Any(s => s.SharedWithUserId == userId))
            return cookbook;

        throw new UnauthorizedAccessException("You do not have access to this cookbook.");
    }

    public async Task<Cookbook> CreateAsync(int userId, string name, string? description)
    {
        var cookbook = new Cookbook
        {
            UserId = userId,
            Name = name,
            Description = description,
        };
        return await _cookbookRepo.AddAsync(cookbook);
    }

    public async Task UpdateAsync(int cookbookId, int userId, string name, string? description)
    {
        var cookbook = await _cookbookRepo.GetByIdAsync(cookbookId)
            ?? throw new InvalidOperationException("Cookbook not found.");

        if (cookbook.UserId != userId)
            throw new UnauthorizedAccessException("You do not own this cookbook.");

        cookbook.Name = name;
        cookbook.Description = description;
        cookbook.UpdatedAt = DateTime.UtcNow;
        await _cookbookRepo.UpdateAsync(cookbook);
    }

    public async Task DeleteAsync(int cookbookId, int userId)
    {
        var cookbook = await _cookbookRepo.GetByIdAsync(cookbookId)
            ?? throw new InvalidOperationException("Cookbook not found.");

        if (cookbook.UserId != userId)
            throw new UnauthorizedAccessException("You do not own this cookbook.");

        await _cookbookRepo.DeleteAsync(cookbook);
    }
}
