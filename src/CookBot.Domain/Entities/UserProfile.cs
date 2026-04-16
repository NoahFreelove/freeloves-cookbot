using CookBot.Domain.Enums;

namespace CookBot.Domain.Entities;

public class UserProfile
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public ExperienceLevel ExperienceLevel { get; set; } = ExperienceLevel.Beginner;
    public UnitSystem UnitSystem { get; set; } = UnitSystem.Imperial;
    /// <summary>
    /// Optional free-text unit rules for AI features (exceptions to the preset unit system).
    /// </summary>
    public string? AiUnitExceptions { get; set; }
    public string KitchenToolsJson { get; set; } = "[]";
    public string DietaryPreferencesJson { get; set; } = "[]";
    public string? AiApiKey { get; set; }
    /// <summary>
    /// When the user has no own API key but multiple people share with them, which owner's key to use for AI calls.
    /// </summary>
    public int? AiSharedKeyOwnerUserId { get; set; }
    public bool AiEnabled { get; set; }
    public string? AiModel { get; set; }
    public string? AiSystemPromptTemplate { get; set; }

    public User User { get; set; } = null!;
}
