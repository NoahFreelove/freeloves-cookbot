namespace CookBot.Application.Services;

public static class RecipeScalingService
{
    public static double ScaleAmount(double amount, int originalServings, int targetServings)
    {
        if (originalServings <= 0) return amount;
        return amount * ((double)targetServings / originalServings);
    }

    public static string FormatScaledAmount(double amount, int originalServings, int targetServings)
    {
        var scaled = ScaleAmount(amount, originalServings, targetServings);
        return FractionFormatter.Format(scaled);
    }
}
