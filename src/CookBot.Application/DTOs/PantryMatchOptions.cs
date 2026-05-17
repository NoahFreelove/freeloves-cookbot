namespace CookBot.Application.DTOs;

/// <summary>
/// Phase 10 / D-46 — IOptions-bound POCO for pantry-match scoring knobs.
/// Bound via <c>services.Configure&lt;PantryMatchOptions&gt;(configuration.GetSection("CookBot:PantryMatch"))</c>
/// in <c>Program.cs</c>. Property defaults apply when the <c>CookBot:PantryMatch</c> section
/// is missing from <c>appsettings.json</c> (safe-start guarantee — no configuration required).
/// </summary>
public sealed class PantryMatchOptions
{
    /// <summary>
    /// D-44 — linear-decay coefficient applied to the recency penalty term.
    /// Penalty formula: <c>RecencyPenaltyWeight * exp(−daysSinceCooked / RecencyHalfLifeDays)</c>.
    /// </summary>
    public double RecencyPenaltyWeight { get; set; } = 0.3;

    /// <summary>
    /// D-44 — decay half-life in days. Controls how quickly the recency penalty
    /// diminishes as the gap between the last cook and today grows.
    /// </summary>
    public double RecencyHalfLifeDays { get; set; } = 7.0;

    /// <summary>
    /// Minimum pantry-coverage ratio (matched ingredients / total ingredients)
    /// a recipe must meet to appear in results. Recipes below this ratio are
    /// excluded before scoring.
    /// </summary>
    public double MinCoverageRatio { get; set; } = 0.6;

    /// <summary>
    /// Maximum number of ranked results returned by
    /// <see cref="Services.IPantryMatchService.GetMatchesAsync"/>.
    /// </summary>
    public int ResultCount { get; set; } = 3;

    // Guards the scoring formula's division when callers register IOptions
    // without binding configuration (test hosts, AddApplication without
    // Configure<>). A zero half-life would produce NaN scores silently.
    public double EffectiveHalfLifeDays => RecencyHalfLifeDays > 0 ? RecencyHalfLifeDays : 7.0;
}
