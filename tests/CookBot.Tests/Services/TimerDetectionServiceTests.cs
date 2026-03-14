using CookBot.Application.Services;
using CookBot.Domain.Entities;

namespace CookBot.Tests.Services;

public class TimerDetectionServiceTests
{
    [Fact]
    public void DetectTimers_SimpleMinutes()
    {
        var timers = TimerDetectionService.DetectTimers("Bake for 25 minutes until golden.");
        Assert.Single(timers);
        Assert.Equal(25, timers[0].Duration);
        Assert.Equal("min", timers[0].Unit);
    }

    [Fact]
    public void DetectTimers_MultipleTimers()
    {
        var timers = TimerDetectionService.DetectTimers("Cook 10 minutes, then rest for 5 mins.");
        Assert.Equal(2, timers.Count);
        Assert.Equal(10, timers[0].Duration);
        Assert.Equal(5, timers[1].Duration);
    }

    [Fact]
    public void DetectTimers_Hours()
    {
        var timers = TimerDetectionService.DetectTimers("Slow cook for 3 hours.");
        Assert.Single(timers);
        Assert.Equal(3, timers[0].Duration);
        Assert.Equal("hr", timers[0].Unit);
    }

    [Fact]
    public void DetectTimers_Seconds()
    {
        var timers = TimerDetectionService.DetectTimers("Microwave for 30 seconds.");
        Assert.Single(timers);
        Assert.Equal(30, timers[0].Duration);
        Assert.Equal("sec", timers[0].Unit);
    }

    [Fact]
    public void DetectTimers_NoTimers()
    {
        var timers = TimerDetectionService.DetectTimers("Mix flour and sugar together.");
        Assert.Empty(timers);
    }
}
