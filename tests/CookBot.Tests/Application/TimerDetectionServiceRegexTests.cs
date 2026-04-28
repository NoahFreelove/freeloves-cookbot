using CookBot.Application.Services;

namespace CookBot.Tests.Application;

public class TimerDetectionServiceRegexTests
{
    // Backward compatibility: simple cases unchanged.
    [Theory]
    [InlineData("Bake for 25 minutes until golden.", 25, "min")]
    [InlineData("Rest 2 hours.", 2, "hr")]
    [InlineData("Stir 30 seconds.", 30, "sec")]
    public void Simple_BackwardCompatible(string text, int expectedDuration, string expectedUnit)
    {
        var timers = TimerDetectionService.DetectTimers(text);
        Assert.Single(timers);
        Assert.Equal(expectedDuration, timers[0].Duration);
        Assert.Equal(expectedUnit, timers[0].Unit);
    }

    // Fractional.
    [Theory]
    [InlineData("Bake for 1 1/2 hours", 5400)]
    [InlineData("Rest 1/2 hour", 1800)]
    [InlineData("Simmer 0.5 hours", 1800)] // decimals via SimplePattern
    public void Fractional_DetectsTotalSeconds(string text, int expectedSeconds)
    {
        var detected = TimerDetectionService.Detect(text);
        Assert.Single(detected);
        Assert.Equal(expectedSeconds, detected[0].TotalSeconds);
    }

    // Range — persists lowest (Assumption A4).
    [Theory]
    [InlineData("Cook 20-25 minutes", 1200)]   // lowest = 20 min
    [InlineData("Bake 20 to 25 minutes", 1200)]
    [InlineData("Roast 30–35 minutes", 1800)]  // en dash
    [InlineData("Roast 30—35 minutes", 1800)]  // em dash
    public void Range_PersistsLowestBound(string text, int expectedSeconds)
    {
        var detected = TimerDetectionService.Detect(text);
        Assert.Single(detected);
        Assert.Equal(expectedSeconds, detected[0].TotalSeconds);
    }

    // Multi-segment.
    [Theory]
    [InlineData("Slow cook 1 hour 30 minutes", 5400)]
    [InlineData("Marinate 2h 15m", 8100)]
    public void MultiSegment_CombinesHoursAndMinutes(string text, int expectedSeconds)
    {
        var detected = TimerDetectionService.Detect(text);
        Assert.Single(detected);
        Assert.Equal(expectedSeconds, detected[0].TotalSeconds);
    }

    // Order-of-application: multi-segment must NOT be eaten by simple.
    [Fact]
    public void MultiSegment_NotEatenBySimple()
    {
        var detected = TimerDetectionService.Detect("Marinate 2h 15m before serving");
        // Should be 1 detection (the multi-segment), not 2 (2h + 15m as separate simples).
        Assert.Single(detected);
        Assert.Equal(8100, detected[0].TotalSeconds);
    }

    // Multiple distinct timers in one step still detected separately.
    [Fact]
    public void MultipleSimpleTimers_AllDetected()
    {
        var detected = TimerDetectionService.Detect("Bake 25 minutes, rest 10 minutes, then ice 5 minutes.");
        Assert.Equal(3, detected.Count);
        Assert.Equal(1500, detected[0].TotalSeconds);
        Assert.Equal(600, detected[1].TotalSeconds);
        Assert.Equal(300, detected[2].TotalSeconds);
    }
}
