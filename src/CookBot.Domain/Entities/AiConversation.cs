namespace CookBot.Domain.Entities;

public class AiConversation
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Title { get; set; } = "New Conversation";
    public string MessagesJson { get; set; } = "[]";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Recipe format version for this conversation. 1 = pre-Phase-2 (YAML wire),
    /// 2 = Phase 2+ (structured JSON). Default 2 for new conversations created
    /// after Phase 2 ships. Legacy rows are back-filled to 1 by the
    /// AiConversationFormatVersion migration; AiChat re-stamps to 2 on next save
    /// (POLISH-06; consumer wired in Plan 04).
    /// </summary>
    public int FormatVersion { get; set; } = 2;

    public User User { get; set; } = null!;
}
