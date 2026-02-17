using System.Security.Cryptography;
using CookBot.Domain.Entities;
using CookBot.Infrastructure.Data;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;
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

    public async Task<bool> UserHasPasswordAsync(int userId)
    {
        var user = await _context.Users.FindAsync(userId);
        return user?.PasswordHash != null;
    }

    public async Task<bool> VerifyPasswordAsync(int userId, string password)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user?.PasswordHash == null) return true;
        return VerifyHash(password, user.PasswordHash);
    }

    public static string HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = KeyDerivation.Pbkdf2(password, salt, KeyDerivationPrf.HMACSHA256, 100_000, 32);
        var combined = new byte[16 + 32];
        Buffer.BlockCopy(salt, 0, combined, 0, 16);
        Buffer.BlockCopy(hash, 0, combined, 16, 32);
        return Convert.ToBase64String(combined);
    }

    private static bool VerifyHash(string password, string storedHash)
    {
        var combined = Convert.FromBase64String(storedHash);
        if (combined.Length != 48) return false;
        var salt = combined[..16];
        var storedKey = combined[16..];
        var hash = KeyDerivation.Pbkdf2(password, salt, KeyDerivationPrf.HMACSHA256, 100_000, 32);
        return CryptographicOperations.FixedTimeEquals(hash, storedKey);
    }
}
