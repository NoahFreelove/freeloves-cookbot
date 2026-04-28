using Microsoft.AspNetCore.Components;

namespace CookBot.Web.Services;

// Phase 5 / Plan 05-04 / DIALOG-02. CbDialogService is the DI surface that
// Plan 05-05 (shell rewrite) and Phase 7 dialog migrations consume. The shape
// mirrors the existing dialog-service idiom (IDialogService.ShowAsync<T>) closely
// so Phase 7 swaps are mechanical. Registered Scoped (per-circuit) in Program.cs.

public sealed record CbDialogResult(bool Canceled, object? Data)
{
    public static CbDialogResult Ok(object? data = null) => new(false, data);
    public static CbDialogResult Cancel() => new(true, null);
}

public sealed class CbDialogParameters : Dictionary<string, object?>
{
    public new CbDialogParameters Add(string name, object? value)
    {
        this[name] = value;
        return this;
    }
}

public enum CbDialogMaxWidth { ExtraSmall, Sm, Md, Lg, Xl }

public sealed record CbDialogOptions(
    CbDialogMaxWidth MaxWidth = CbDialogMaxWidth.Sm,
    bool FullWidth = true,
    bool CloseOnEscape = true,
    bool CloseOnScrim = true)
{
    public static CbDialogOptions Default { get; } = new();
}

internal sealed record CbDialogRequest(
    Type DialogType,
    string Title,
    CbDialogParameters Parameters,
    CbDialogOptions Options,
    TaskCompletionSource<CbDialogResult> Tcs);

public interface ICbDialogService
{
    Task<CbDialogResult> ShowAsync<TDialog>(string title, CbDialogParameters? parameters = null, CbDialogOptions? options = null) where TDialog : ComponentBase;
    Task<CbDialogResult> ShowAsync(Type dialogType, string title, CbDialogParameters? parameters = null, CbDialogOptions? options = null);
    internal event Func<CbDialogRequest, Task>? OnRequest;
}

internal sealed class CbDialogService : ICbDialogService
{
    public event Func<CbDialogRequest, Task>? OnRequest;

    public Task<CbDialogResult> ShowAsync<TDialog>(string title, CbDialogParameters? parameters = null, CbDialogOptions? options = null) where TDialog : ComponentBase
        => ShowAsync(typeof(TDialog), title, parameters, options);

    public Task<CbDialogResult> ShowAsync(Type dialogType, string title, CbDialogParameters? parameters = null, CbDialogOptions? options = null)
    {
        var tcs = new TaskCompletionSource<CbDialogResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var req = new CbDialogRequest(
            dialogType,
            title,
            parameters ?? new CbDialogParameters(),
            options ?? CbDialogOptions.Default,
            tcs);

        if (OnRequest is null)
        {
            // No host mounted — fail fast so missing-host bugs surface in dev rather than hanging forever.
            tcs.SetException(new InvalidOperationException(
                "CbDialogHost is not mounted. Add <CbDialogHost /> to MainLayout.razor (or to the page hosting the dialog)."));
        }
        else
        {
            // Fire-and-forget host invocation; host completes the Tcs when the dialog closes.
            _ = OnRequest.Invoke(req);
        }
        return tcs.Task;
    }
}

/// <summary>
/// Cascaded into a TDialog rendered by CbDialogHost so the inner content can self-close
/// via DialogInstance?.Close(CbDialogResult.Ok(...)).
/// </summary>
public sealed class CbDialogInstance
{
    private readonly TaskCompletionSource<CbDialogResult> _tcs;
    public CbDialogInstance(TaskCompletionSource<CbDialogResult> tcs) { _tcs = tcs; }
    public void Close(CbDialogResult result) { if (!_tcs.Task.IsCompleted) _tcs.SetResult(result); }
    public void Cancel() => Close(CbDialogResult.Cancel());
}
