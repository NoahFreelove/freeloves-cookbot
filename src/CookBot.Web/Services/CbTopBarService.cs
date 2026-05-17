using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;

namespace CookBot.Web.Services;

/// <summary>
/// Phase 10 / Plan 10-08 / POLISH-04 / D-56 / D-57.
/// Internal scoped implementation of <see cref="ICbTopBarService"/>.
/// Registered as Scoped — one instance per SignalR circuit.
/// PATTERNS.md Layering Note 2: first Web-layer scoped service to subscribe to
/// NavigationManager.LocationChanged in its constructor; IDisposable is mandatory
/// so Blazor disposes the handler when the circuit tears down, preventing memory leaks
/// (T-10-08-01 — IDisposable contract mitigates the missed-Dispose DoS threat).
/// D-57: auto-clears RightSlot on every location change — pages do not need to clear
/// on unload; re-set in OnInitializedAsync for pages that need TopBar content.
/// </summary>
internal sealed class CbTopBarService : ICbTopBarService, IDisposable
{
    private readonly NavigationManager _nav;

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
        OnChanged?.Invoke();
    }

    public void Clear()
    {
        if (RightSlot is null) return;  // idempotent — no fire if already empty
        RightSlot = null;
        OnChanged?.Invoke();
    }

    private void HandleLocationChanged(object? sender, LocationChangedEventArgs e) => Clear();

    public void Dispose() => _nav.LocationChanged -= HandleLocationChanged;
}
