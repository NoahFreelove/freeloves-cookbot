using CookBot.Application.DTOs;
using CookBot.Application.Services;
using CookBot.Domain.Entities;
using CookBot.Infrastructure.Data;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.JSInterop;
using CookBot.Web.Services;

namespace CookBot.Web.Components.Pages;

/// <summary>
/// Home dashboard code-behind (Phase 6 / Plan 06-01 / HOME-01..04).
/// Hosts the pantry-match stub algorithm + dashboard counters; the markup
/// lives in Home.razor and binds to the public state below.
/// Logic preserved from the previous Home: CurrentUserService for authz,
/// CookBotSettings.AiFeaturesEnabled + UserProfile.AiEnabled for the
/// AI-off contract on the "Generate a recipe" quick action (D-12).
/// </summary>
public partial class Home : ComponentBase
{
    [Inject] private CurrentUserService UserService { get; set; } = null!;
    [Inject] private NavigationManager Navigation { get; set; } = null!;
    [Inject] private CookBotDbContext DbContext { get; set; } = null!;
    [Inject] private PantryService PantryService { get; set; } = null!;
    [Inject] private IOptions<CookBotSettings> CookBotSettingsOptions { get; set; } = null!;
    [Inject] private IScheduledRecipeService ScheduledRecipeService { get; set; } = null!;
    [Inject] private IRecipeMadeService RecipeMadeService { get; set; } = null!;
    [Inject] private IPantryMatchService PantryMatchService { get; set; } = null!;
    [Inject] private IJSRuntime JS { get; set; } = null!;

    /// <summary>Current user (greeting + per-user authz). Null until first render restores the user.</summary>
    private User? _user;

    /// <summary>Glance-strip counts (HOME-03).</summary>
    private int _recipeCount;
    private int _cookbookCount;
    private int _cookbookSharedCount;
    private int _pantryCount;
    private int _pantryLowCount;
    private int _pantryExpiringCount;
    private int _groceryCount;
    private DateTime? _groceryUpdatedAt;

    /// <summary>HOME-02 pantry-match results (deterministic stub; FUTURE-13).</summary>
    private List<HomePantryMatch> _pantryMatches = new();
    private int _accessiblePantryItemCount;

    /// <summary>True when the global AI kill-switch is off OR the user opted out — drives quick-action visibility (HOME-01).</summary>
    private bool _aiOff = true;

    private int? _loadedUserId;

    /// <summary>
    /// In-progress cooking session — populated from localStorage via JS interop on
    /// first render (Plan 07-09 Feature 2). Null when no session is active or the
    /// user has navigated away. Drives the "Resume cooking" band at the top of Home.
    /// </summary>
    private InProgressCookEntry? _inProgress;

    /// <summary>
    /// Active long-running timer — populated from localStorage via JS interop on
    /// first render (Plan 07-09 Feature 1). Null when no timer is active or the
    /// timer has expired (the JS module clears expired entries automatically).
    /// </summary>
    private ActiveTimerEntry? _activeTimer;

    /// <summary>Recipe-name lookup for the in-progress / active-timer cards (resolved alongside the dashboard load).</summary>
    private Dictionary<int, string> _recipeNameCache = new();

    /// <summary>
    /// Real "Up next" entries — populated from IScheduledRecipeService (Plan 07-09 Feature 1).
    /// Falls back to the placeholder rows below when the user has zero scheduled recipes,
    /// so the card shape is preserved for empty-state users.
    /// </summary>
    private List<HomeUpNext> _upNext = new();

    /// <summary>
    /// Empty-state placeholder rows shown when the user has zero scheduled recipes.
    /// Keeps the card shape and copy intent from the design handoff.
    /// </summary>
    private static readonly (string Name, string When)[] _upNextPlaceholders =
    {
        ("Tartine country loaf", "starts 9 PM · autolyse"),
        ("Slow short rib",       "sat · 6h braise"),
        ("Citrus tart",          "sun · for hannah"),
    };

    /// <summary>
    /// Recently cooked tile metadata — closes FUTURE-Recently-Cooked (Plan 07-09 Feature 2).
    /// Reads from IRecipeMadeService.GetRecentForUserAsync; falls back to most-recently-updated
    /// recipes when the user has no logged cooks yet, so the tile still renders something useful.
    /// </summary>
    private List<HomeRecentRecipe> _recentlyCooked = new();

    /// <summary>
    /// PHOTO-11 / PITFALL H4 — one-shot photo-load-failure tracker keyed by recipe id.
    /// When an <img> @onerror fires we add the recipe id to this set and re-render
    /// with the StripedPlaceholder fallback; the browser cannot loop on a broken URL
    /// because the <img> element is gone. HashSet membership makes repeat HandlePhotoError
    /// calls a no-op (idempotent), and failures are scoped per recipe id so a single
    /// broken tile does not gate the whole row.
    /// </summary>
    private readonly HashSet<int> _photoFailedFor = new();

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (UserService.CurrentUserId.HasValue && _loadedUserId != UserService.CurrentUserId.Value)
        {
            _loadedUserId = UserService.CurrentUserId.Value;
            await LoadDashboardAsync(_loadedUserId.Value);
            await LoadActiveSessionAsync();
            StateHasChanged();
        }
        else if (firstRender && UserService.CurrentUserId.HasValue && _inProgress == null && _activeTimer == null)
        {
            // Edge case: dashboard already loaded synchronously (re-rendered home) but
            // localStorage hadn't been read yet because JS interop is unavailable in
            // OnInitialized. firstRender is the first opportunity.
            await LoadActiveSessionAsync();
            StateHasChanged();
        }

        // POLISH-05 — start the live JS tick loop for the active-timer band on first render.
        // Runs after LoadActiveSessionAsync so _activeTimer is populated. The tick mutates
        // the DOM element directly every 1 second without a Blazor re-render per tick.
        if (firstRender && _activeTimer != null)
        {
            try
            {
                await JS.InvokeVoidAsync(
                    "CookbotSession.startTickLoop",
                    _activeTimerCountdownId,
                    _activeTimer.StartedAtIso,
                    _activeTimer.DurationSeconds);
            }
            catch (Microsoft.JSInterop.JSException) { }
            catch (Microsoft.JSInterop.JSDisconnectedException) { }
        }
    }

    /// <summary>
    /// Plan 07-09 — reads the in-progress cooking session and active-timer entries
    /// from localStorage and resolves the associated recipe names. Fail-soft: any JS
    /// exception (prerender, private mode, disconnected circuit) leaves the cards hidden.
    /// </summary>
    private async Task LoadActiveSessionAsync()
    {
        try
        {
            var inProgress = await JS.InvokeAsync<InProgressCookEntry?>("CookbotSession.readInProgress");
            var activeTimer = await JS.InvokeAsync<ActiveTimerEntry?>("CookbotSession.readActiveTimer");

            // Resolve recipe names for any recipe ids referenced by the bands.
            var ids = new HashSet<int>();
            if (inProgress != null) ids.Add(inProgress.RecipeId);
            if (activeTimer != null) ids.Add(activeTimer.RecipeId);

            if (ids.Count > 0 && UserService.CurrentUserId.HasValue)
            {
                var userId = UserService.CurrentUserId.Value;
                var lookup = await DbContext.Recipes
                    .AsNoTracking()
                    .Where(r => ids.Contains(r.Id)
                                && (r.Cookbook.UserId == userId
                                    || r.Cookbook.Shares.Any(s => s.SharedWithUserId == userId)))
                    .Select(r => new { r.Id, r.Name })
                    .ToListAsync();
                _recipeNameCache = lookup.ToDictionary(r => r.Id, r => r.Name);
            }

            // Drop stale entries whose recipe is no longer accessible (deleted /
            // share revoked) — defensive cleanup so the bands never claim a phantom recipe.
            if (inProgress != null && !_recipeNameCache.ContainsKey(inProgress.RecipeId))
            {
                try { await JS.InvokeVoidAsync("CookbotSession.clearInProgress"); } catch { }
                inProgress = null;
            }
            if (activeTimer != null && !_recipeNameCache.ContainsKey(activeTimer.RecipeId))
            {
                try { await JS.InvokeVoidAsync("CookbotSession.clearActiveTimer"); } catch { }
                activeTimer = null;
            }

            _inProgress = inProgress;
            _activeTimer = activeTimer;
        }
        catch (JSException) { /* prerender or module unavailable */ }
        catch (Microsoft.JSInterop.JSDisconnectedException) { }
    }

    private string ResumeCookingLabel(int recipeId)
        => _recipeNameCache.TryGetValue(recipeId, out var name) ? name : "Cooking session";

    private async Task LoadDashboardAsync(int userId)
    {
        _user = await UserService.GetCurrentUserAsync();
        if (_user == null) return;

        // PHOTO-11 / PITFALL H4 — fresh page-session pull, fresh photo-failure tracking.
        // Reset before any data load so newly-set PhotoUrls on previously-failed recipes
        // get a fresh chance to render.
        _photoFailedFor.Clear();

        // AI-off contract (HOME-01 / D-12): host kill-switch AND user opt-in must both be on.
        var aiHostOn = CookBotSettingsOptions.Value.AiFeaturesEnabled;
        var aiUserOn = _user.Profile?.AiEnabled ?? false;
        _aiOff = !(aiHostOn && aiUserOn);

        // HOME-03 — counts. Owned + shared cookbooks are visible to the user; reuse the same
        // access predicate the rest of the app relies on (cookbook.UserId == userId
        // OR cookbook.Shares.Any(s => s.SharedWithUserId == userId)).
        _recipeCount = await DbContext.Recipes.CountAsync(r =>
            r.Cookbook.UserId == userId ||
            r.Cookbook.Shares.Any(s => s.SharedWithUserId == userId));

        _cookbookCount = await DbContext.Cookbooks.CountAsync(c =>
            c.UserId == userId ||
            c.Shares.Any(s => s.SharedWithUserId == userId));

        // "{N} shared with the house" — non-personal shares OUT from this user's cookbooks (D-04).
        _cookbookSharedCount = await DbContext.CookbookShares
            .CountAsync(s => s.Cookbook.UserId == userId);

        var allItems = await PantryService.GetAllUserAccessibleItemsAsync(userId);
        _accessiblePantryItemCount = allItems.Count;
        _pantryCount = _accessiblePantryItemCount;

        // Low / expiring heuristics for the glance sub-text (D-04 PRAGMATIC):
        //   Low      → Amount < 1 (works for either ct-style integer counts or fractional units;
        //              the existing PantryService has no canonical "low" flag yet — FUTURE).
        //   Expiring → ExpirationDate within 7 days from today (UTC).
        var now = DateTime.UtcNow;
        var expiringWindow = now.AddDays(7);
        _pantryLowCount = allItems.Count(p => p.Amount > 0 && p.Amount < 1);
        _pantryExpiringCount = allItems.Count(p => p.ExpirationDate.HasValue
            && p.ExpirationDate.Value <= expiringWindow
            && p.ExpirationDate.Value >= now);

        var groceryLists = await DbContext.GroceryLists
            .Where(g => g.UserId == userId)
            .ToListAsync();
        _groceryCount = groceryLists.Count;
        _groceryUpdatedAt = groceryLists.Count == 0
            ? (DateTime?)null
            : groceryLists.Max(g => g.CreatedAt);

        // HOME-02 — real pantry-match via IPantryMatchService (QOL-01..03 / Plan 10-04).
        _pantryMatches = await BuildPantryMatchesAsync(userId);

        // HOME-04 — Recently cooked. Plan 07-09 Feature 2 wires this to the real
        // RecipeMade log; fall back to the 4 most-recently-updated recipes when the
        // user has no cooks logged yet so the tile still renders something useful.
        var madeLog = await RecipeMadeService.GetRecentForUserAsync(userId, 4);
        if (madeLog.Count > 0)
        {
            _recentlyCooked = madeLog.Select(m => new HomeRecentRecipe(
                m.RecipeId,
                m.Recipe?.Name ?? "(deleted recipe)",
                $"cooked {DescribeRelative(m.CompletedAt)}",
                m.Recipe?.PhotoUrl)).ToList();
        }
        else
        {
            var recent = await DbContext.Recipes
                .AsNoTracking()
                .Where(r => r.Cookbook.UserId == userId
                            || r.Cookbook.Shares.Any(s => s.SharedWithUserId == userId))
                .OrderByDescending(r => r.UpdatedAt)
                .Take(4)
                .Select(r => new { r.Id, r.Name, r.UpdatedAt, r.PhotoUrl })
                .ToListAsync();
            _recentlyCooked = recent.Select(r => new HomeRecentRecipe(
                r.Id,
                r.Name,
                DescribeRelative(r.UpdatedAt),
                r.PhotoUrl)).ToList();
        }

        // Plan 07-09 Feature 1 — populate the "Up next" card from real ScheduledRecipe
        // entries. Empty list -> markup falls back to the placeholder rows.
        var upcoming = await ScheduledRecipeService.GetUpcomingAsync(userId, 3);
        _upNext = upcoming.Select(s => new HomeUpNext(
            s.Id,
            s.RecipeId,
            s.Recipe?.Name ?? "(deleted recipe)",
            FormatScheduledFor(s.ScheduledFor),
            s.Notes)).ToList();
    }

    /// <summary>
    /// Formats a scheduled UTC time as a friendly local-time sub-line for the
    /// Up next card. "today, 7:30 PM" / "tomorrow, 9 AM" / "fri · 6 PM" / etc.
    /// </summary>
    private static string FormatScheduledFor(DateTime scheduledForUtc)
    {
        var local = scheduledForUtc.ToLocalTime();
        var today = DateTime.Now.Date;
        var when = local.Date;
        var time = local.ToString("h:mm tt").ToLowerInvariant();
        if (when == today) return $"today · {time}";
        if (when == today.AddDays(1)) return $"tomorrow · {time}";
        if ((when - today).TotalDays < 7)
        {
            return $"{local:ddd} · {time}";
        }
        return local.ToString("MMM d · h:mm tt").ToLowerInvariant();
    }

    /// <summary>
    /// Thin projection over <see cref="IPantryMatchService.GetMatchesAsync"/> (QOL-01..03 / Plan 10-04).
    /// All scoring logic (D-44 exponential-decay, D-45 dietary filter, D-46 configurable weights)
    /// lives in <see cref="PantryMatchService"/>. Home is now a pure projection layer.
    /// </summary>
    private async Task<List<HomePantryMatch>> BuildPantryMatchesAsync(int userId, CancellationToken ct = default)
    {
        var results = await PantryMatchService.GetMatchesAsync(userId, ct);
        return results.Select(r => new HomePantryMatch(
            r.RecipeId,
            r.RecipeName,
            r.MatchedCount,
            r.TotalCount,
            $"uses {r.MatchedCount} of {r.TotalCount} ingredients",
            r.FirstMissingIngredientName,
            r.PhotoUrl)).ToList();
    }

    private static string DescribeRelative(DateTime utc)
    {
        var delta = DateTime.UtcNow - utc;
        if (delta.TotalMinutes < 60) return "just now";
        if (delta.TotalHours < 24) return $"{(int)delta.TotalHours}h ago";
        if (delta.TotalDays < 7) return $"{(int)delta.TotalDays}d ago";
        if (delta.TotalDays < 30) return $"{(int)(delta.TotalDays / 7)}w ago";
        return utc.ToLocalTime().ToString("MMM d");
    }

    private string GroceryAgoLine() => _groceryUpdatedAt switch
    {
        null => _groceryCount == 0 ? "no lists yet" : "list ready",
        _    => $"list updated {DescribeRelative(_groceryUpdatedAt.Value)}",
    };

    private string PantryStatusLine()
    {
        if (_pantryCount == 0) return "pantry empty";
        if (_pantryLowCount == 0 && _pantryExpiringCount == 0) return "all stocked";
        var parts = new List<string>();
        if (_pantryLowCount > 0)      parts.Add($"{_pantryLowCount} low");
        if (_pantryExpiringCount > 0) parts.Add($"{_pantryExpiringCount} expiring");
        return string.Join(" · ", parts);
    }

    private string CookbookSubLine() => _cookbookSharedCount switch
    {
        0     => "private only",
        1     => "1 shared with the house",
        var n => $"{n} shared with the house",
    };

    private string HeroHeadline() => _pantryMatches.Count switch
    {
        0     => "Stock the pantry to see tonight's options.",
        1     => "One recipe matches what's in stock.",
        var n => $"{NumberWord(n)} recipes match what's in stock.",
    };

    private string HeroBody()
    {
        if (_pantryMatches.Count == 0)
            return _accessiblePantryItemCount == 0
                ? "Add to your pantry to see what you can cook tonight."
                : $"None of your recipes match {_accessiblePantryItemCount} pantry items yet — try adding more staples.";
        return $"Based on the {_accessiblePantryItemCount} items in your pantry. We avoided anything expiring after this week.";
    }

    private static string NumberWord(int n) => n switch
    {
        1 => "One",
        2 => "Two",
        3 => "Three",
        _ => n.ToString(),
    };

    /// <summary>Maps a pantry-match to a CbBadge status: in-stock when complete, otherwise low (=missing one).</summary>
    private static CookBot.Web.Components.Atoms.CbBadge.CbBadgeStatus BadgeStatusFor(HomePantryMatch m) =>
        m.MatchedCount == m.TotalCount
            ? CookBot.Web.Components.Atoms.CbBadge.CbBadgeStatus.InStock
            : CookBot.Web.Components.Atoms.CbBadge.CbBadgeStatus.Low;

    private static string BadgeLabelFor(HomePantryMatch m) =>
        m.MatchedCount == m.TotalCount
            ? "in stock"
            : (m.MissingIngredientName != null ? $"missing {m.MissingIngredientName}" : "missing items");

    /// <summary>
    /// PHOTO-11 / PITFALL H4 — Blazor-side one-shot debounce for a broken tile thumbnail.
    /// HashSet.Add is idempotent, so a re-fire while StateHasChanged is pending is a no-op.
    /// </summary>
    private void HandlePhotoError(int recipeId)
    {
        _photoFailedFor.Add(recipeId);
        StateHasChanged();
    }

    private void GoToRecipe(int recipeId) => Navigation.NavigateTo($"/recipes/{recipeId}");
    private void GoToCookbooks() => Navigation.NavigateTo("/cookbooks");
    private void GoToPantry() => Navigation.NavigateTo("/pantry");
    private void GoToGrocery() => Navigation.NavigateTo("/grocery-lists");
    private void GoToAi() => Navigation.NavigateTo("/ai");
    private void GoToCook(int recipeId) => Navigation.NavigateTo($"/recipes/{recipeId}/cook");

    /// <summary>
    /// Formats a "started 12m ago" string from an ISO timestamp emitted by the JS
    /// CookbotSession module. Mirrors the JS-side formatter so the C#-rendered cards
    /// match what JS would tick to on subsequent updates.
    /// </summary>
    private static string FormatStartedAgo(string startedAtIso)
    {
        if (string.IsNullOrEmpty(startedAtIso)) return "";
        if (!DateTime.TryParse(startedAtIso, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.RoundtripKind, out var startedAt))
            return "";
        var delta = DateTime.UtcNow - startedAt.ToUniversalTime();
        if (delta.TotalMinutes < 1) return "just now";
        if (delta.TotalMinutes < 60) return $"{(int)delta.TotalMinutes}m ago";
        if (delta.TotalHours < 24) return $"{(int)delta.TotalHours}h ago";
        return $"{(int)delta.TotalDays}d ago";
    }

    /// <summary>Formats remaining timer seconds as MM:SS / HH:MM:SS for the active-timer band.</summary>
    private static string FormatTimerRemaining(int totalSeconds)
    {
        if (totalSeconds < 0) totalSeconds = 0;
        var hours = totalSeconds / 3600;
        var minutes = (totalSeconds % 3600) / 60;
        var seconds = totalSeconds % 60;
        return hours > 0
            ? $"{hours:D2}:{minutes:D2}:{seconds:D2}"
            : $"{minutes:D2}:{seconds:D2}";
    }

    /// <summary>Stable id so a future enhancement could JS-tick the countdown without a re-render.</summary>
    private readonly string _activeTimerCountdownId = $"home-active-timer-{Guid.NewGuid():N}";
}

/// <summary>Pantry-match row record (HOME-02). MissingIngredientName drives the "missing parsley"-style chip.</summary>
/// <remarks>PhotoUrl flows from Recipe.PhotoUrl when available — drives the tonight-from-your-pantry
/// hero thumbnail (PHOTO-11). Null when the recipe has no photo set yet; UI renders StripedPlaceholder.</remarks>
public sealed record HomePantryMatch(
    int RecipeId,
    string RecipeName,
    int MatchedCount,
    int TotalCount,
    string MetaLine,
    string? MissingIngredientName,
    string? PhotoUrl);

/// <summary>Recently cooked tile (HOME-04). PhotoUrl drives the tile thumbnail (PHOTO-11).</summary>
public sealed record HomeRecentRecipe(int RecipeId, string Name, string SubLine, string? PhotoUrl);

/// <summary>Up next row (Plan 07-09 Feature 1) — backed by ScheduledRecipe.</summary>
public sealed record HomeUpNext(int ScheduledRecipeId, int RecipeId, string RecipeName, string WhenLine, string? Notes);

/// <summary>
/// Mirror of CookbotSession.readInProgress payload — STJ deserializes camelCase JS
/// keys into the matching record properties via Blazor's default interop options.
/// </summary>
public sealed record InProgressCookEntry(int RecipeId, int CurrentStepIndex, int ScaledServings, string StartedAtIso);

/// <summary>
/// Mirror of CookbotSession.readActiveTimer payload (with computed remainingSeconds).
/// </summary>
public sealed record ActiveTimerEntry(int RecipeId, string StepLabel, int DurationSeconds, string StartedAtIso, int RemainingSeconds);
