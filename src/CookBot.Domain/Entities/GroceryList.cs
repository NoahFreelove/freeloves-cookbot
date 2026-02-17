namespace CookBot.Domain.Entities;

public class GroceryList
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
    public ICollection<GroceryListItem> Items { get; set; } = new List<GroceryListItem>();
}
