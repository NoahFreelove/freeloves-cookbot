namespace CookBot.Domain.Entities;

public class StepTimer
{
    public int Duration { get; set; }
    public string Unit { get; set; } = "min";
    public string? Label { get; set; }
}
