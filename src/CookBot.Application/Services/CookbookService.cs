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

    public async Task<Cookbook?> GetByIdAsync(int id) =>
        await _cookbookRepo.GetByIdAsync(id);

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

    public async Task UpdateAsync(Cookbook cookbook)
    {
        cookbook.UpdatedAt = DateTime.UtcNow;
        await _cookbookRepo.UpdateAsync(cookbook);
    }

    public async Task DeleteAsync(Cookbook cookbook) =>
        await _cookbookRepo.DeleteAsync(cookbook);
}
