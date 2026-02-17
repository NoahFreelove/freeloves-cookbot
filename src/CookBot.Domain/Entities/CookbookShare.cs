namespace CookBot.Domain.Entities;

public class CookbookShare
{
    public int Id { get; set; }
    public int CookbookId { get; set; }
    public int SharedWithUserId { get; set; }
    public DateTime SharedAt { get; set; } = DateTime.UtcNow;

    public Cookbook Cookbook { get; set; } = null!;
    public User SharedWithUser { get; set; } = null!;
}
