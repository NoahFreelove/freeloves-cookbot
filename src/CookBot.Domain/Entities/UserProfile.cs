using CookBot.Domain.Enums;

namespace CookBot.Domain.Entities;

public class UserProfile
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public ExperienceLevel ExperienceLevel { get; set; } = ExperienceLevel.Beginner;
    public UnitSystem UnitSystem { get; set; } = UnitSystem.Imperial;
    public string KitchenToolsJson { get; set; } = "[]";
    public string DietaryPreferencesJson { get; set; } = "[]";
    public string? AiApiKey { get; set; }

    public User User { get; set; } = null!;
}
