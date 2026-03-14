namespace CookBot.Application.Services;

public static class FractionFormatter
{
    private static readonly (double Fraction, string Display)[] CommonFractions =
    {
        (1.0 / 8, "1/8"),
        (1.0 / 4, "1/4"),
        (1.0 / 3, "1/3"),
        (3.0 / 8, "3/8"),
        (1.0 / 2, "1/2"),
        (2.0 / 3, "2/3"),
        (3.0 / 4, "3/4"),
        (7.0 / 8, "7/8"),
    };

    private const double Tolerance = 0.01;

    public static string Format(double value)
    {
        if (value < 0) return $"-{Format(-value)}";

        var whole = (int)value;
        var fractional = value - whole;

        if (fractional < Tolerance)
            return whole.ToString();

        if (1.0 - fractional < Tolerance)
            return (whole + 1).ToString();

        foreach (var (fraction, display) in CommonFractions)
        {
            if (Math.Abs(fractional - fraction) < Tolerance)
            {
                return whole > 0 ? $"{whole} {display}" : display;
            }
        }

        return value.ToString("0.##");
    }
}
