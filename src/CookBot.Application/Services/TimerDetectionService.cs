using System.Text.RegularExpressions;
using CookBot.Domain.Entities;

namespace CookBot.Application.Services;

public static class TimerDetectionService
{
    private static readonly Regex TimerPattern = new(
        @"(\d+)\s*(minutes?|mins?|hours?|hrs?|seconds?|secs?)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static List<StepTimer> DetectTimers(string text)
    {
        var timers = new List<StepTimer>();
        foreach (Match match in TimerPattern.Matches(text))
        {
            var duration = int.Parse(match.Groups[1].Value);
            var unitStr = match.Groups[2].Value.ToLowerInvariant();
            var unit = unitStr switch
            {
                var u when u.StartsWith("sec") => "sec",
                var u when u.StartsWith("hr") || u.StartsWith("hour") => "hr",
                _ => "min"
            };
            timers.Add(new StepTimer { Duration = duration, Unit = unit });
        }
        return timers;
    }
}
