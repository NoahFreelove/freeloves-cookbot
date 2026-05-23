using CookBot.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;

namespace CookBot.Tests.Services;

// Phase 10 / Plan 10-08 / POLISH-04 / D-56 / D-57.
// Tests verify: SetRightSlot raises OnChanged; LocationChanged auto-clears RightSlot;
// Clear() is idempotent (no event when already null); Dispose() unsubscribes from
// NavigationManager so LocationChanged no longer triggers Clear → OnChanged.
//
// W-06 NOTE: The .NET 10 NavigationManager.NotifyLocationChanged protected method
// signature is `NotifyLocationChanged(bool isInternalNavigation)` — one parameter.
// The URI is set separately via Navigate before calling NotifyLocationChanged.
// TestNavigationManager exposes a two-step helper that matches the conceptual
// "navigate to uri, internal" contract specified in the plan.
public class CbTopBarServiceTests
{
    // W-06 RESOLUTION: the base method parameter name is `isInternalNavigation`.
    // In .NET 10.0.1 NotifyLocationChanged takes one bool parameter; we set the
    // Uri by calling Navigate first, then notify. The public wrapper preserves the
    // named-argument call site `isInternalNavigation:` so the plan assertion holds.
    private sealed class TestNavigationManager : NavigationManager
    {
        public TestNavigationManager() { Initialize("http://localhost/", "http://localhost/"); }

        public void NotifyLocationChanged(string uri, bool isInternalNavigation)
        {
            Uri = uri;
            base.NotifyLocationChanged(isInternalNavigation);
        }

        // 999.1 test helper: simulate Blazor's behavior where the browser navigates and updates
        // the URL BEFORE the LocationChanged event subscribers fire. Lets a test stamp a slot at
        // the destination URL and then fire LocationChanged for that same URL.
        public void SetUriDirectly(string uri) => Uri = uri;
    }

    [Fact]
    public void SetRightSlot_RaisesOnChanged()
    {
        var nav = new TestNavigationManager();
        var svc = new CbTopBarService(nav);
        var fired = 0;
        svc.OnChanged += () => fired++;

        svc.SetRightSlot(builder => builder.AddContent(0, "x"));

        Assert.Equal(1, fired);
        Assert.NotNull(svc.RightSlot);
    }

    [Fact]
    public void LocationChanged_AutoClearsRightSlot()
    {
        var nav = new TestNavigationManager();
        var svc = new CbTopBarService(nav);
        svc.SetRightSlot(builder => builder.AddContent(0, "hello"));

        nav.NotifyLocationChanged("http://localhost/recipes/1", isInternalNavigation: true);

        Assert.Null(svc.RightSlot);
    }

    [Fact]
    public void Clear_Idempotent_DoesNotFireWhenAlreadyEmpty()
    {
        var nav = new TestNavigationManager();
        var svc = new CbTopBarService(nav);
        var fired = 0;
        svc.OnChanged += () => fired++;

        // RightSlot is already null — Clear() must not invoke OnChanged
        svc.Clear();

        Assert.Equal(0, fired);
    }

    // 999.1 regression guard (2026-05-23): Blazor Server fires NavigationManager.LocationChanged
    // AFTER the new page's OnInitialized has run. The original D-57 contract ("auto-clear on every
    // navigation") was wiping the slot the new page had just set. SetRightSlot now stamps the URL
    // it was called at, and LocationChanged preserves the slot when the destination URL matches.
    [Fact]
    public void LocationChanged_PreservesSlot_WhenSetForSameUrl()
    {
        var nav = new TestNavigationManager();
        // Simulate Blazor's real ordering:
        //   1. URL changes to /recipes/2 (browser/router-driven, BEFORE event subscribers run)
        //   2. RecipeView.OnInitialized runs, calls SetRightSlot — stamps URL=/recipes/2
        //   3. LocationChanged event fires with destination URL=/recipes/2
        // The slot must survive step 3.
        nav.SetUriDirectly("http://localhost/recipes/2");
        var svc = new CbTopBarService(nav);
        svc.SetRightSlot(builder => builder.AddContent(0, "buttons"));

        nav.NotifyLocationChanged("http://localhost/recipes/2", isInternalNavigation: true);

        Assert.NotNull(svc.RightSlot);
    }

    [Fact]
    public void LocationChanged_ClearsSlot_WhenStampedUrlDiffersFromDestination()
    {
        var nav = new TestNavigationManager();
        // Slot was set for /recipes/2 (e.g., the user was on that page), then user navigates
        // to /home which doesn't set its own slot. The stale slot from /recipes/2 must clear.
        nav.SetUriDirectly("http://localhost/recipes/2");
        var svc = new CbTopBarService(nav);
        svc.SetRightSlot(builder => builder.AddContent(0, "stale"));

        nav.NotifyLocationChanged("http://localhost/home", isInternalNavigation: true);

        Assert.Null(svc.RightSlot);
    }

    [Fact]
    public void Dispose_UnsubscribesFromNavigationManager()
    {
        var nav = new TestNavigationManager();
        var svc = new CbTopBarService(nav);
        // Set a slot so that after Dispose, a LocationChanged would have triggered Clear
        // which would have raised OnChanged — confirm it does NOT.
        svc.SetRightSlot(builder => builder.AddContent(0, "slot"));
        svc.Dispose();

        var fired = 0;
        svc.OnChanged += () => fired++;

        nav.NotifyLocationChanged("http://localhost/x", isInternalNavigation: true);

        Assert.Equal(0, fired);
    }
}
