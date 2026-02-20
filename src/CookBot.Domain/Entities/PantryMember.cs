namespace CookBot.Domain.Entities;

public class PantryMember
{
    public int Id { get; set; }
    public int PantryId { get; set; }
    public int UserId { get; set; }
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

    public Pantry Pantry { get; set; } = null!;
    public User User { get; set; } = null!;
}
