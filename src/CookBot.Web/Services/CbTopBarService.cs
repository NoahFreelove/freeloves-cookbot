using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;

namespace CookBot.Web.Services;

/// <summary>
/// Phase 10 / Plan 10-08 / POLISH-04 / D-56 / D-57 (revised by 999.1, 2026-05-23).
/// Internal scoped implementation of <see cref="ICbTopBarService"/>.
/// Registered as Scoped — one instance per SignalR circuit.
/// PATTERNS.md Layering Note 2: first Web-layer scoped service to subscribe to
/// NavigationManager.LocationChanged in its constructor; IDisposable is mandatory
/// so Blazor disposes the handler when the circuit tears down, preventing memory leaks
/// (T-10-08-01 — IDisposable contract mitigates the missed-Dispose DoS threat).
///
/// D-57 (revised): the original "auto-clear on every LocationChanged" wiped the slot
/// that the new page had just set in OnInitialized — because LocationChanged fires
/// AFTER OnInitialized in the Blazor Server lifecycle (~4ms later in observed traces).
/// Instead, SetRightSlot stamps the URL it was called at, and HandleLocationChanged
/// skips the clear when the stamped URL matches the event's destination URL — meaning
/// the slot was set FOR this page and must be preserved. Only when the destination URL
/// differs (i.e., a page that DIDN'T re-set its slot during init) does the auto-clear
/// fire, wiping the stale slot from the previous page.
/// </summary>
internal sealed class CbTopBarService : ICbTopBarService, IDisposable
{
    private readonly NavigationManager _nav;
    private string? _slotSetAtUrl;

    public RenderFragment? RightSlot { get; private set; }

    public event Action? OnChanged;

    public CbTopBarService(NavigationManager nav)
    {
        _nav = nav;
        _nav.LocationChanged += HandleLocationChanged;
    }

    public void SetRightSlot(RenderFragment? content)
    {
        RightSlot = content;
        _slotSetAtUrl = content is null ? null : _nav.Uri;
        OnChanged?.Invoke();
    }

    public void Clear()
    {
        if (RightSlot is null) return;  // idempotent — no fire if already empty
        RightSlot = null;
        _slotSetAtUrl = null;
        OnChanged?.Invoke();
    }

    private void HandleLocationChanged(object? sender, LocationChangedEventArgs e)
    {
        // 999.1 fix: if a page already set the slot for this destination URL during
        // its OnInitialized (which fires BEFORE LocationChanged subscribers), the slot
        // belongs to that page and must survive this navigation event. Clear only when
        // the stamped URL doesn't match — that means the slot is stale from a prior page.
        if (_slotSetAtUrl is not null
            && string.Equals(_slotSetAtUrl, e.Location, StringComparison.Ordinal))
        {
            return;
        }
        Clear();
    }

    public void Dispose() => _nav.LocationChanged -= HandleLocationChanged;
}
