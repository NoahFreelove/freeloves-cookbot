namespace CookBot.Domain.Entities;

/// <summary>
/// A recipe queued for a future cook (Plan 07-09 Feature 1).
/// Persists per-user "Up next" entries surfaced on the Home dashboard.
/// Reminders / push notifications are out of scope — this is persistence + visibility only.
/// </summary>
public class ScheduledRecipe
{
    public int Id { get; set; }
    public int RecipeId { get; set; }
    public int UserId { get; set; }
    public DateTime ScheduledFor { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Recipe Recipe { get; set; } = null!;
    public User User { get; set; } = null!;
}
