namespace CookBot.Domain.Entities;

public class AiConversation
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Title { get; set; } = "New Conversation";
    public string MessagesJson { get; set; } = "[]";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
}
