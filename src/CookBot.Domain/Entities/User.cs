namespace CookBot.Domain.Entities;

public class User
{
    public int Id { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? PasswordHash { get; set; }
    public string? IdentityUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public UserProfile Profile { get; set; } = null!;
    public ICollection<Cookbook> Cookbooks { get; set; } = new List<Cookbook>();
    public ICollection<PantryItem> PantryItems { get; set; } = new List<PantryItem>();
    public ICollection<GroceryList> GroceryLists { get; set; } = new List<GroceryList>();
    public ICollection<AiConversation> AiConversations { get; set; } = new List<AiConversation>();
}
