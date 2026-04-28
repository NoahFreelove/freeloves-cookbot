namespace CookBot.Web.Services;

// Phase 5 / Plan 05-04 / DIALOG-03. CbToastService is the DI surface for one-shot
// non-blocking notifications. Registered Singleton (D-25) — toasts are app-wide UI
// events, not user-scoped state. CbToastHost subscribes to OnToast to render the
// bottom-right stack.

public enum CbToastSeverity { Success, Error, Info, Warning }

public sealed record CbToastMessage(Guid Id, string Message, CbToastSeverity Severity, DateTime CreatedAt);

public interface ICbToastService
{
    void Show(string message, CbToastSeverity severity = CbToastSeverity.Info);
    event Action<CbToastMessage>? OnToast;
}

internal sealed class CbToastService : ICbToastService
{
    public event Action<CbToastMessage>? OnToast;

    public void Show(string message, CbToastSeverity severity = CbToastSeverity.Info)
    {
        if (string.IsNullOrWhiteSpace(message)) return;
        OnToast?.Invoke(new CbToastMessage(Guid.NewGuid(), message, severity, DateTime.UtcNow));
    }
}
