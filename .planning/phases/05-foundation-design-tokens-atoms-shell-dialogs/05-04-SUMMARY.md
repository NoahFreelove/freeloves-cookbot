---
phase: 05-foundation-design-tokens-atoms-shell-dialogs
plan: 04
subsystem: design-system
tags:
  - design-system
  - dialogs
  - toasts
  - dropdowns
  - razor
requires:
  - cookbot-design.css token surface (Plan 05-01)
  - Display atoms shipped (Plan 05-02)
  - Form atoms + DIALOGS_INSERTION_POINT sentinel preserved (Plan 05-03)
provides:
  - CbDialog (presentational primitive — IsOpen @bind, MaxWidth/FullWidth/CloseOnEscape/CloseOnScrim, Header/Body/Footer slots) — DIALOG-01
  - CbDialogHost (subscribes to ICbDialogService.OnRequest; <DynamicComponent> stack with cascaded CbDialogInstance) — DIALOG-02
  - ICbDialogService / CbDialogService Scoped (ShowAsync<TDialog>(...) returns Task<CbDialogResult>) — DIALOG-02
  - CbDialogResult / CbDialogParameters / CbDialogOptions / CbDialogMaxWidth / CbDialogInstance support types — DIALOG-02
  - CbToastHost (max 3 stacked, 5s auto-fade, 4 severity tints) — DIALOG-03
  - ICbToastService / CbToastService Singleton (Show(message, severity)) + CbToastSeverity + CbToastMessage — DIALOG-03
  - CbDropdown<TValue> (trigger button + popup menu, ESC + outside-click + item-select close, generic on TValue) — DIALOG-04
  - CbDropdownItem<TValue> (CascadingParameter child registering with parent CbDropdown) — DIALOG-04
  - wwwroot/js/cb-dialog.js — window.cookbotDialog with trapFocus / releaseFocus / bindOutsideClick / unbindOutsideClick (refcounted body scroll lock)
  - CSS rules — .cb-scrim, .cb-dialog (size-xs..xl), .cb-dialog-header / -body / -footer, .cb-toast-host / .cb-toast (success/error/info/warning), .cb-dropdown / .cb-dropdown-menu / .cb-dropdown-item
  - Sandbox Dialogs section demonstrating each primitive in light + dark modes
  - SampleDialogContent.razor — dialog-content component for the service-driven demo
affects:
  - src/CookBot.Web/Program.cs (AddScoped<ICbDialogService> + AddSingleton<ICbToastService> after AddMudServices)
  - src/CookBot.Web/Components/_Imports.razor (@using CookBot.Web.Components.Dialogs added)
  - src/CookBot.Web/Components/App.razor (cb-dialog.js script reference added)
  - src/CookBot.Web/wwwroot/css/cookbot-design.css (157 lines appended after Plan 05-03 form-atom rules)
  - src/CookBot.Web/Components/Pages/DesignSandbox.razor (DIALOGS_INSERTION_POINT replaced; CbDialogHost + CbToastHost mounted at top)
tech-stack:
  added: []
  patterns:
    - DI-event-driven dialog/toast hosts — service raises an event, host subscribes and renders
    - <DynamicComponent Type="..." Parameters="..."> for type-erased dialog-content rendering inside CbDialogHost
    - CascadingValue<CbDialogInstance> so dialog content can self-close via Close(CbDialogResult.Ok(...))
    - TaskCompletionSource<CbDialogResult> bridging the synchronous ShowAsync caller with async dialog dismissal
    - DotNetObjectReference + [JSInvokable] for ESC + outside-click event marshaling JS → .NET
    - Refcounted document.body scroll lock so stacked dialogs unlock cleanly on close
    - InvokeAsync-marshaled list/timer mutations in CbToastHost (Singleton service event → Scoped renderer)
    - Hard-cap MaxStacked = 3 with FIFO eviction + CTS cancel of evicted toast timers
    - Generic <CbDropdown TValue> with [CascadingParameter] coordination to <CbDropdownItem TValue>
    - <button> dropdown items so native Tab order provides keyboard nav without roving-tabindex
key-files:
  created:
    - src/CookBot.Web/Services/CbDialogService.cs
    - src/CookBot.Web/Services/CbToastService.cs
    - src/CookBot.Web/Components/Dialogs/CbDialog.razor
    - src/CookBot.Web/Components/Dialogs/CbDialogHost.razor
    - src/CookBot.Web/Components/Dialogs/CbToastHost.razor
    - src/CookBot.Web/Components/Atoms/CbDropdown.razor
    - src/CookBot.Web/Components/Atoms/CbDropdownItem.razor
    - src/CookBot.Web/Components/Pages/SampleDialogContent.razor
    - src/CookBot.Web/wwwroot/js/cb-dialog.js
  modified:
    - src/CookBot.Web/Program.cs
    - src/CookBot.Web/Components/App.razor
    - src/CookBot.Web/Components/_Imports.razor
    - src/CookBot.Web/wwwroot/css/cookbot-design.css
    - src/CookBot.Web/Components/Pages/DesignSandbox.razor
decisions:
  - CbDialogService.OnRequest is an internal Func<CbDialogRequest, Task>? event — fire-and-forget invocation from ShowAsync (`_ = OnRequest.Invoke(req)`) so the caller's `await Tcs.Task` is never blocked by host StateHasChanged
  - Missing-host case (OnRequest null) sets the Tcs to faulted with InvalidOperationException — fail fast in dev rather than hang forever waiting for a host that was never mounted
  - CbDialogHost uses Blazor's built-in <DynamicComponent> for type-erased rendering instead of a hand-rolled reflection-based parameter setter — fewer moving parts, fewer attack surfaces
  - HandleCancel on scrim/ESC checks Tcs.IsCompleted before SetResult — protects against the race where dialog content already called DialogInstance.Close(Ok(...)) before the cancel handler fires
  - CbToastHost evicts FIFO when count > 3 AND cancels the evicted toast's pending fade timer to avoid leaking CancellationTokenSource in long-running circuits with toast-heavy workflows
  - CbDropdown uses native <button> Tab-order keyboard nav rather than implementing roving-tabindex — simpler, accessible by default, full arrow-key roving deferred to Phase 7 A11Y-01 if needed
  - cookbotDialog.bindOutsideClick defers attaching the document mousedown listener via setTimeout(0) so the click that opened the dropdown doesn't immediately close it
  - CbToastService is Singleton (D-25) but CbToastHost is per-circuit; concurrency mitigated by marshaling all _toasts/_timers mutations through InvokeAsync so they run on the renderer sync context
  - CbDialogHost mounts directly on /design-sandbox in Plan 05-04 (NOT in MainLayout) so this plan is independently verifiable; Plan 05-05 (shell rewrite) moves the mounts into MainLayout and removes the temporary sandbox mounts
metrics:
  duration: ~6 min
  completed: 2026-04-27
requirements:
  - DIALOG-01
  - DIALOG-02
  - DIALOG-03
  - DIALOG-04
---

# Phase 5 Plan 04: Dialog + Toast + Dropdown primitives Summary

Four new presentational primitives (CbDialog, CbDialogHost, CbToastHost, CbDropdown) plus their support types (CbDropdownItem, SampleDialogContent), two DI services (CbDialogService Scoped, CbToastService Singleton), one JS interop module (`wwwroot/js/cb-dialog.js`) and 157 lines of CSS shipped under `src/CookBot.Web/Components/Dialogs/`, `src/CookBot.Web/Components/Atoms/`, `src/CookBot.Web/Services/` and `src/CookBot.Web/wwwroot/`. The `CbDialogService.ShowAsync<TDialog>(title, parameters, options)` API mirrors MudBlazor's `IDialogService.ShowAsync` shape closely enough that Phase 7 dialog migrations are mechanical. `<CbDialogHost />` and `<CbToastHost />` mount on `/design-sandbox` for the Plan 05-04 demo (Plan 05-05 moves them to MainLayout). `dotnet build` clean (0 warnings, 0 errors); `dotnet test --filter "Category!=RequiresApiKey"` baseline preserved (196/196). Existing MudBlazor `IDialogService` / `ISnackbar` call sites in 14+ files (PantryView, CookbookList, GroceryListView, EditProfile, etc.) remain UNTOUCHED and continue to function — D-30 coexistence holds through Phase 5.

## What shipped

### `CbDialogService.cs` (DIALOG-02 / D-24)

| Type | Surface |
| --- | --- |
| `ICbDialogService` | `Task<CbDialogResult> ShowAsync<TDialog>(string title, CbDialogParameters? parameters = null, CbDialogOptions? options = null) where TDialog : ComponentBase` + non-generic `Type`-overload + `internal event Func<CbDialogRequest, Task>? OnRequest` |
| `CbDialogService` | Internal sealed implementation. `ShowAsync` builds a `CbDialogRequest`, fire-and-forgets `OnRequest.Invoke(req)` (host completes the Tcs when dialog closes), and returns `tcs.Task`. Missing-host case (OnRequest null) sets the Tcs faulted with `InvalidOperationException("CbDialogHost is not mounted...")` — fails fast in dev. |
| `CbDialogResult(bool Canceled, object? Data)` | record. Static `Ok(data)` / `Cancel()` factories. |
| `CbDialogParameters : Dictionary<string, object?>` | Builder method `Add(name, value)` returning `this` for chaining. |
| `CbDialogOptions(MaxWidth, FullWidth, CloseOnEscape, CloseOnScrim)` | record. `Default` static cached instance. |
| `CbDialogMaxWidth` | enum: ExtraSmall / Sm / Md / Lg / Xl. |
| `CbDialogRequest` | `internal sealed record` carrying DialogType, Title, Parameters, Options, Tcs to the host. |
| `CbDialogInstance` | Cascaded into TDialog content by CbDialogHost. `Close(CbDialogResult)` sets the Tcs (guarded by IsCompleted to prevent double-set). |

Registered in `Program.cs`: `builder.Services.AddScoped<ICbDialogService, CbDialogService>();` (D-24 — per-circuit because dialog state is browser-tab-scoped).

### `CbToastService.cs` (DIALOG-03 / D-25)

| Type | Surface |
| --- | --- |
| `ICbToastService` | `void Show(string message, CbToastSeverity severity = CbToastSeverity.Info)` + `event Action<CbToastMessage>? OnToast` |
| `CbToastService` | Internal sealed. `Show` whitespace-checks message and raises `OnToast` with a fresh `CbToastMessage(Guid, message, severity, DateTime.UtcNow)`. |
| `CbToastSeverity` | enum: Success / Error / Info / Warning. |
| `CbToastMessage` | `record(Guid Id, string Message, CbToastSeverity Severity, DateTime CreatedAt)`. |

Registered in `Program.cs`: `builder.Services.AddSingleton<ICbToastService, CbToastService>();` (D-25 — toasts are app-wide, not user-scoped).

### `Components/Dialogs/CbDialog.razor` (DIALOG-01 / D-23)

Presentational primitive. Renders nothing when `IsOpen=false`; on true, emits `<div class="cb-scrim"><div class="cb-dialog size-{xs|sm|md|lg|xl}" tabindex="-1" role="dialog" aria-modal="true">` with optional Header / Body / Footer slots. Scrim click bubbles to `HandleScrimClick` (closes when `CloseOnScrim`); the inner card has `@onclick:stopPropagation="true"` so clicks inside don't cascade. ESC keydown is captured by JS in `cookbotDialog.trapFocus` and forwarded to `[JSInvokable] OnEscape()` which closes when `CloseOnEscape`. `OnAfterRenderAsync` flips a `_wasOpen` latch — opens trigger `cookbotDialog.trapFocus`, closes trigger `releaseFocus`. `IAsyncDisposable` cleans up the `DotNetObjectReference` and releases focus if still trapped.

Parameters:

| Parameter | Type | Default | Notes |
| --- | --- | --- | --- |
| `IsOpen` | bool | false | `@bind-IsOpen` |
| `MaxWidth` | CbDialogMaxWidth | Sm | size-xs..xl class |
| `FullWidth` | bool | true | reserved for Phase 6/7 layout tuning; current CSS uses size-* widths capped at viewport-32 |
| `CloseOnEscape` | bool | true | |
| `CloseOnScrim` | bool | true | |
| `Header` / `Body` / `Footer` | RenderFragment? | null | |
| `OnClose` | EventCallback | — | fires after IsOpen flips false |

### `Components/Dialogs/CbDialogHost.razor` (DIALOG-02 / D-24)

Subscribes to `DialogService.OnRequest += HandleRequest` in `OnInitialized`; unsubscribes in `Dispose`. Maintains `_stack: List<HostEntry>` where each entry has Id / DialogType / Title / Parameters / Options / Tcs. `HandleRequest` adds the entry, hooks `Tcs.Task.ContinueWith` to remove on completion, and returns `InvokeAsync(StateHasChanged)`. Renders each entry as `<CbDialog @key="entry.Id" ...>` whose Body wraps `<CascadingValue Value="new CbDialogInstance(entry.Tcs)" IsFixed="true">` around `<DynamicComponent Type="entry.DialogType" Parameters="entry.Parameters" />`. Multiple stacked dialogs work because each `<CbDialog>` instance has its own `_dialogId` — JS focus-trap operates on individual elements; the latest one captures focus by virtue of being most recently mounted. `HandleCancel` (fires from scrim click / ESC) checks `Tcs.IsCompleted` before `SetResult(Cancel())` — this guards the race where inner content already called `DialogInstance.Close(Ok(...))`.

### `Components/Dialogs/CbToastHost.razor` (DIALOG-03 / D-25)

Subscribes to `ToastService.OnToast` in `OnInitialized`; unsubscribes in `Dispose`. `HandleToast` marshals onto the renderer sync context via `InvokeAsync` (Singleton→Scoped boundary), appends to `_toasts: List<CbToastMessage>`, evicts FIFO when `count > MaxStacked=3` AND cancels the evicted toast's pending fade-timer CTS to avoid leaking, then schedules a `Task.Delay(LifetimeMs=5000, cts.Token)` continuation that removes the toast and re-renders. Renders `<div class="cb-toast-host">` with one `<div class="cb-toast {severity}">@msg.Message</div>` per active toast. `Dispose` cancels all timers and unsubscribes.

### `Components/Atoms/CbDropdown.razor` (DIALOG-04 / D-26)

Generic on TValue. Trigger is `<button class="cb-btn ghost cb-dropdown-trigger">` with optional StartIcon, label text, and a trailing `<Icon Name="chevD" Size="12" />`. `Toggle` flips `IsOpen`; on open, registers `cookbotDialog.bindOutsideClick(_id, _ref)` and on close `unbindOutsideClick(_id)`. `[JSInvokable] OnOutsideClick` fires from the document mousedown listener and closes the menu. `HandleKey` (ESC) closes. `SelectAsync(TValue)` is called by child `<CbDropdownItem>` instances — it sets `Value`, closes the menu, invokes `ValueChanged.InvokeAsync(value)`, and unbinds the outside-click listener. `IsSelected(TValue)` uses `EqualityComparer<TValue>.Default.Equals`. Disposes its `DotNetObjectReference`.

### `Components/Atoms/CbDropdownItem.razor` (DIALOG-04 / D-26)

Generic on TValue. Receives parent `CbDropdown<TValue>?` via `[CascadingParameter]`. Renders `<button type="button" class="cb-dropdown-item {selected}" role="menuitem" @onclick=HandleClick>` containing optional `<Icon Name="@Icon" Size="14">` and `ChildContent ?? @Label`. `HandleClick` calls `Parent.SelectAsync(Value)`. `IsSelected` returns `Parent.IsSelected(Value)`.

### `wwwroot/js/cb-dialog.js`

Self-invoking factory exposing `window.cookbotDialog` with four functions:

| Function | Behavior |
| --- | --- |
| `trapFocus(elementId, dotnetRef)` | Stashes `document.activeElement`; focuses first focusable child (or the element itself); attaches a `keydown` listener that (a) calls `dotnetRef.invokeMethodAsync('OnEscape')` on ESC, (b) cycles focus on Tab/Shift+Tab between first/last focusable nodes inside `element`. Increments refcount; sets `document.body.style.overflow = 'hidden'` on first lock. |
| `releaseFocus(elementId)` | Removes keydown listener; restores stashed focus; decrements refcount; clears body overflow when zero. |
| `bindOutsideClick(elementId, dotnetRef)` | Registers a document mousedown listener (deferred via `setTimeout(0)` to avoid catching the open-click event) that calls `dotnetRef.invokeMethodAsync('OnOutsideClick')` when click target is not inside element. |
| `unbindOutsideClick(elementId)` | Removes the listener and clears the trap entry. |

### CSS appended to `cookbot-design.css` (157 lines after Plan 05-03 form-atom rules)

| Rule | Purpose |
| --- | --- |
| `.cb-scrim` | fixed-inset, rgba(0,0,0,0.36) (0.6 in dark mode), z-index 1000, flex-center child, fade-in keyframes |
| `.cb-dialog` + `.cb-dialog.size-{xs sm md lg xl}` | paper bg, line border, 14px radius, drop-shadow, max-height calc(100vh - 64px), per-size widths (360/480/640/840/1080) capped at calc(100vw - 32px), translateY+scale entry keyframes |
| `.cb-dialog-header / -body / -footer` | 16/20 / 18/20 / 12/20 padding, 16px-600 header, scrollable body, paper-2 footer with right-aligned 8px-gap flex |
| `.cb-toast-host` | fixed bottom-right (24/24), column-reverse + 8px gap, z-index 1100, pointer-events none |
| `.cb-toast` + `.cb-toast.{success error info warning}` | paper-2 baseline; severity tints from green-soft / warn-soft / paper-2 / warn-soft tokens; 240-380px width; toast-in keyframes |
| `.cb-dropdown` / `.cb-dropdown-menu` / `.cb-dropdown-item` | relative trigger wrapper, absolute popup with paper bg + line border + 4px padding + max-height 320 + scroll-y, item button rows with hover-tint and selected accent-soft state, dark-mode overrides for hover tint |

### Sandbox section (DesignSandbox.razor)

`<!-- DIALOGS_INSERTION_POINT -->` replaced with a 2-column grid of two `<CbCard>` panels (CbDialog/Service / Toast triggers) + a wider third `<CbCard>` for the CbDropdown demo + an inline `<CbDialog @bind-IsOpen="_inlineOpen">` rendered at end of body. `<CbDialogHost />` and `<CbToastHost />` mounted at the top of the page (before `<div class="cb">`) so the demo works without the Plan 05-05 shell rewrite. New `@inject ICbDialogService DialogService` and `@inject ICbToastService Toast` directives. New `@code` state: `_inlineOpen`, `_dropdownValue = "Maya"`, `_lastDialogResult = "(none yet)"`, plus `ShowSampleDialog()` which awaits `DialogService.ShowAsync<SampleDialogContent>(...)` and updates the echo span.

`SampleDialogContent.razor` is a tiny dialog-content component: renders `<p>@Message</p>` + Cancel/Confirm buttons that call `DialogInstance?.Close(CbDialogResult.Cancel())` or `Close(CbDialogResult.Ok("confirmed"))`.

`_Imports.razor` gained `@using CookBot.Web.Components.Dialogs` so `<CbDialog>`, `<CbDialogHost>`, `<CbToastHost>` resolve.

`App.razor` gained `<script src="js/cb-dialog.js"></script>` after `cookbot-shell.js`.

## Verification

- **`dotnet build src/CookBot.Web/CookBot.Web.csproj -c Debug --nologo`** — PASSED (0 warnings, 0 errors, ~3.95 s).
- **`dotnet test --filter "Category!=RequiresApiKey" --nologo`** — PASSED (196/196, 1 s baseline preserved).
- **Plan automated-verify clauses:**
  - Task 1: `CbDialogService.cs` + `CbToastService.cs` exist; `AddScoped<ICbDialogService` + `AddSingleton<ICbToastService` + `AddMudServices` all present in `Program.cs`; build clean.
  - Task 2: `cb-dialog.js` exists with `trapFocus` + `releaseFocus`; `App.razor` references `js/cb-dialog.js`; CSS has `.cb-scrim` / `.cb-dialog` / `.cb-toast` / `.cb-dropdown` rules.
  - Task 3: `CbDialog.razor` + `CbDialogHost.razor` exist; CbDialog calls `cookbotDialog.trapFocus` and has `JSInvokable`; CbDialogHost uses `DynamicComponent` + subscribes to `OnRequest`; neither imports MudBlazor.
  - Task 4: `CbToastHost.razor` + `CbDropdown.razor` + `CbDropdownItem.razor` exist; toast host hooks `OnToast`; dropdown calls `cookbotDialog.bindOutsideClick`; item uses `CascadingParameter`; none import MudBlazor.
  - Task 5: Sandbox has `<CbDialogHost`, `<CbToastHost`, `<CbDropdown`, `DialogService.ShowAsync`; `SampleDialogContent.razor` exists; build clean.
- **Hard invariants (D-30):**
  - `grep -rn "Mud[A-Z]\|MudBlazor" src/CookBot.Web/Components/Dialogs/ src/CookBot.Web/Services/Cb*.cs src/CookBot.Web/Components/Atoms/CbDropdown*.razor src/CookBot.Web/Components/Pages/SampleDialogContent.razor src/CookBot.Web/wwwroot/js/cb-dialog.js` → ZERO matches.
  - `MudBlazor` package reference still in `CookBot.Web.csproj` (untouched).
  - `_Imports.razor` still has `@using MudBlazor`.
  - `Program.cs` still calls `AddMudServices()` on line 18, before the new `AddScoped<ICbDialogService>` / `AddSingleton<ICbToastService>` registrations.
  - `MainLayout.razor` still has 26 `Mud*` references (existing dialog/snackbar/layout providers unchanged) — Plan 05-05 rewrites it.
- **API surface for Phase 7 migration ergonomics:**
  - `CbDialogService.ShowAsync<TDialog>(title, parameters, options)` — exact same shape as MudBlazor `IDialogService.ShowAsync<T>(title, parameters, options)` modulo type renames.
  - `CbDialogParameters().Add("Foo", value)` — same builder shape as `DialogParameters().Add(...)`.
  - `result.Canceled` / `result.Data` — same as MudBlazor `DialogResult.Canceled` / `DialogResult.Data`.
  - `CbToastService.Show(msg, severity)` — same shape as `ISnackbar.Add(msg, severity)`.

## Manual smoke pass (queued)

Not executed in this session (no live browser). Smoke after this commit:

1. `./run.sh` → visit `http://localhost:7000/design-sandbox`.
2. Scroll to **Dialogs, toasts, dropdown** section.
3. Click **Open inline dialog** — scrim + paper card animate in; ESC closes; clicking the scrim closes; clicking inside the card does NOT close.
4. Click **Open via CbDialogService** — confirmation dialog renders via `<DynamicComponent>`; click **Confirm** → echo line shows `ok (confirmed)`; reopen → click **Cancel** → echo shows `canceled`; ESC also yields `canceled`.
5. Click each toast button — toasts appear bottom-right with severity colors (green-soft success, warn-soft error/warning, paper-2 info); after spamming a 4th, the oldest disappears (max 3); each fades after 5s.
6. Click the **CbDropdown** — popup opens below trigger; click outside → closes; ESC → closes; click "Hannah" → label updates to `User: Hannah`, popup closes, echo shows `selected: Hannah`.
7. Tab into open dialog → focus traps inside (Shift+Tab from first wraps to last; Tab from last wraps to first); ESC closes (when CloseOnEscape).
8. Open inline dialog, then trigger service dialog from somewhere (would need a button inside the inline dialog — current sandbox doesn't have one, but the multi-dialog stacking is exercised via the host's `_stack` and the @key per CbDialog). Visual verification of stacked-z-index left to Phase 7 surface migrations.
9. Toggle dark mode (existing button in MainLayout) — scrim darkens to rgba(0,0,0,0.6); paper card stays paper-toned per token; toast tints flip; dropdown menu paper bg + hover tint flip.
10. Confirm `IDialogService` / `ISnackbar` call sites still function: open `/cookbooks` and trigger any cookbook delete / share dialog — should still work (MudDialog renders inside MudDialogProvider unchanged).

## Multi-dialog stacking + focus-trap

The CbDialogHost renders `_stack: List<HostEntry>` as a `@foreach` over CbDialog instances. Each gets its own `_dialogId` so `cookbotDialog.trapFocus` operates on distinct DOM elements. Body scroll-lock refcount in `cb-dialog.js` ensures stacking N dialogs and closing them in any order leaves `document.body.style.overflow` correctly cleared. The latest-mounted dialog naturally captures focus because its `firstFocusable.focus()` runs after its render. Phase 7 multi-dialog scenarios (e.g., a confirmation inside an editor dialog) will exercise this path; for Plan 05-04 the demo path is single-dialog.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Removed unsupported `[Parameter(Name = "...")]` attribute on `CbDropdownItem.razor`**

- **Found during:** Task 4 build verification.
- **Issue:** Initial draft used `[Parameter(Name = "Icon")] public string? IconName { get; set; }` to alias the parameter — this syntax doesn't exist on .NET 10's `ParameterAttribute` (no `Name` property). Build error CS0246: 'Name' could not be found.
- **Fix:** Renamed the C# property to `Icon` directly, matching the existing CbChip convention (`[Parameter] public string? Icon { get; set; }`). Razor cleanly distinguishes the `<Icon>` component reference from the `@Icon` property reference because component names resolve via Blazor's component resolver while `@Icon` resolves to the C# member binding context.
- **Files modified:** `src/CookBot.Web/Components/Atoms/CbDropdownItem.razor`
- **Commit:** Folded into `6f2984a` (Task 4 commit) — fix happened before the commit was created.

**2. [Rule 1 - Hygiene] Stripped a literal "MudBlazor" mention from a code comment in `CbDialogService.cs`**

- **Found during:** Final hard-invariant verification (Task 5 wrap).
- **Issue:** The header comment had a casual reference to "MudBlazor's IDialogService.ShowAsync<T>" for documentation purposes. Hard invariant says "Zero `Mud*` symbols in any new code" — strictly this is a doc string not a symbol import, but Plan 05-03's stricter interpretation (and the `grep -rn 'Mud[A-Z]'` repo-wide check in the verification) treats any literal as a hit.
- **Fix:** Reworded the comment to "the existing dialog-service idiom (IDialogService.ShowAsync<T>)" — preserves the intent (Phase 7 swap ergonomics) without the literal "MudBlazor" string.
- **Files modified:** `src/CookBot.Web/Services/CbDialogService.cs`
- **Commit:** Folded into `1604643` (Task 5 commit).

### Authentication gates

None.

### Other minor scope adjustments per executor task prompt

- **Dropdown items demo names:** Task prompt specified Maya / Hannah / Theo. Plan-file Task 5 sketch had Maya / Alex / Sam. I followed the prompt because it's the latest direction (consistent with Plan 05-03 precedent — executor prompts are authoritative for label-only changes).
- **CbDialog "close X button" optional:** Plan task description noted the close X is optional for Phase 5 ("use a styled `<button>` with text '✕' or omit for Phase 5 simplicity"). I omitted — the dialogs all have explicit Footer buttons or service-driven close paths, and ESC + scrim-click cover the keyboard/mouse dismissal. Phase 6/7 surfaces can add an X button when their content benefits from one.
- **CbDialog `FullWidth` parameter present but currently unused at the CSS level.** The plan keeps it in the public API for future flexibility (Plan 05-05 shell may want a fullwidth variant for narrow viewports). The size-* widths cap at `calc(100vw - 32px)` so very-narrow viewports already render full-width; the explicit fullwidth class is reserved.

## MudBlazor coexistence (D-30)

- `MudBlazor` and `MudBlazor.Services` package references still in `CookBot.Web.csproj` (untouched).
- `@using MudBlazor` still in `_Imports.razor`.
- `_content/MudBlazor/MudBlazor.min.css` + `MudBlazor.min.js` still referenced from `App.razor`.
- `MainLayout.razor` still mounts `<MudThemeProvider>`, `<MudPopoverProvider>`, `<MudDialogProvider>`, `<MudSnackbarProvider>` (Plan 05-05 will rewrite this).
- `Program.cs` still calls `AddMudServices()` (line 18); the new lines 19-20 add `AddScoped<ICbDialogService>` + `AddSingleton<ICbToastService>` AFTER it, so DI resolution order is unchanged.
- New code imports zero `Mud*` symbols (`grep -rn "Mud[A-Z]\|MudBlazor" src/CookBot.Web/Components/Dialogs/ src/CookBot.Web/Services/Cb*.cs src/CookBot.Web/Components/Atoms/CbDropdown*.razor src/CookBot.Web/Components/Pages/SampleDialogContent.razor src/CookBot.Web/wwwroot/js/cb-dialog.js` → 0 matches).

## Self-Check: PASSED

All 9 created files exist:

- `src/CookBot.Web/Services/CbDialogService.cs` — FOUND
- `src/CookBot.Web/Services/CbToastService.cs` — FOUND
- `src/CookBot.Web/Components/Dialogs/CbDialog.razor` — FOUND
- `src/CookBot.Web/Components/Dialogs/CbDialogHost.razor` — FOUND
- `src/CookBot.Web/Components/Dialogs/CbToastHost.razor` — FOUND
- `src/CookBot.Web/Components/Atoms/CbDropdown.razor` — FOUND
- `src/CookBot.Web/Components/Atoms/CbDropdownItem.razor` — FOUND
- `src/CookBot.Web/Components/Pages/SampleDialogContent.razor` — FOUND
- `src/CookBot.Web/wwwroot/js/cb-dialog.js` — FOUND

All 5 task commits exist in git log:

- `25afe0e` (Task 1: CbDialogService + CbToastService + DI) — FOUND
- `c044167` (Task 2: cb-dialog.js + CSS append) — FOUND
- `50e835d` (Task 3: CbDialog + CbDialogHost) — FOUND
- `6f2984a` (Task 4: CbToastHost + CbDropdown + CbDropdownItem) — FOUND
- `1604643` (Task 5: sandbox demo + DIALOGS_INSERTION_POINT replaced + Mud-comment cleanup) — FOUND

Build clean (0 warnings, 0 errors). Tests at baseline (196/196 default filter). MudBlazor coexistence intact (D-30). Plan-level final commit hash recorded after this file is staged.
