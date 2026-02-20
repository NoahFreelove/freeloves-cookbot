namespace CookBot.Domain.Entities;

public class Pantry
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int OwnerId { get; set; }
    public bool IsPersonal { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User Owner { get; set; } = null!;
    public ICollection<PantryItem> Items { get; set; } = new List<PantryItem>();
    public ICollection<PantryMember> Members { get; set; } = new List<PantryMember>();
}
