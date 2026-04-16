namespace CookBot.Domain.Entities;

/// <summary>
/// Grants <see cref="RecipientUserId"/> the ability to use <see cref="OwnerUserId"/>'s Anthropic API key (server-side only).
/// </summary>
public class AiApiKeyShare
{
    public int Id { get; set; }
    public int OwnerUserId { get; set; }
    public int RecipientUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User Owner { get; set; } = null!;
    public User Recipient { get; set; } = null!;
}
