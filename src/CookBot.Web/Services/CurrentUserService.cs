using CookBot.Domain.Entities;
using CookBot.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CookBot.Web.Services;

public class CurrentUserService
{
    private readonly CookBotDbContext _context;
    public int? CurrentUserId { get; set; }

    public CurrentUserService(CookBotDbContext context)
    {
        _context = context;
    }

    public async Task<User?> GetCurrentUserAsync()
    {
        if (CurrentUserId == null) return null;
        return await _context.Users
            .Include(u => u.Profile)
            .FirstOrDefaultAsync(u => u.Id == CurrentUserId);
    }

    public async Task<List<User>> GetAllUsersAsync()
    {
        return await _context.Users.OrderBy(u => u.DisplayName).ToListAsync();
    }

    public async Task<User> CreateUserAsync(string displayName)
    {
        var user = new User
        {
            DisplayName = displayName,
            Profile = new UserProfile()
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        return user;
    }

    public async Task InitializeAsync()
    {
        if (CurrentUserId != null) return;
        var firstUser = await _context.Users.FirstOrDefaultAsync();
        if (firstUser != null)
        {
            CurrentUserId = firstUser.Id;
        }
    }
}
