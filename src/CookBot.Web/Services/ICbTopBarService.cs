using Microsoft.AspNetCore.Components;

namespace CookBot.Web.Services;

/// <summary>
/// Phase 10 / Plan 10-08 / POLISH-04 / D-56.
/// Scoped service backing the TopBar.RightSlot wiring (Plan 10-09).
/// D-56: ICbTopBarService over CascadingValue — honors ROADMAP success criteria 4
/// literal text; event-driven updates are future-proof for LeftSlot/CenterSlot.
/// One instance per SignalR circuit (Scoped lifetime).
/// </summary>
public interface ICbTopBarService
{
    /// <summary>Gets the render fragment currently occupying the TopBar right slot, or null if empty.</summary>
    RenderFragment? RightSlot { get; }

    /// <summary>Raised whenever <see cref="RightSlot"/> changes (set or cleared).</summary>
    event Action? OnChanged;

    /// <summary>Replaces the right slot content and raises <see cref="OnChanged"/>.</summary>
    void SetRightSlot(RenderFragment? content);

    /// <summary>
    /// Clears the right slot and raises <see cref="OnChanged"/>.
    /// Idempotent — does NOT raise <see cref="OnChanged"/> when the slot is already null.
    /// </summary>
    void Clear();
}
