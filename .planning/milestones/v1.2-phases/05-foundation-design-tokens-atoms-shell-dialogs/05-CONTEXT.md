# Phase 5: Foundation — Design tokens, atoms, shell, dialogs - Context

**Gathered:** 2026-04-27
**Status:** Ready for planning
**Mode:** `--auto` — Claude picked recommended defaults from the design handoff at `.planning/design-handoff/`

<domain>
## Phase Boundary

Build a custom Razor component system end-to-end against the Claude Design handoff bundle (`.planning/design-handoff/`) — design tokens (CSS custom properties + Inter typography + dark-mode parity), 10 reusable atoms (`<CbButton>`, `<CbChip>`, `<CbCard>`, `<CbStat>`, `<CbEyebrow>`, `<StripedPlaceholder>`, `<Icon>`, `<CbBadge>`, form atoms, input atoms), the new shell (`MainLayout` + `<Sidebar>` + `<TopBar>` + `<NavRow>`), and dialog/toast/dropdown primitives that replace `MudDialogProvider` / `MudSnackbarProvider`. Verify the system end-to-end on a `/design-sandbox` route.

**This phase delivers the toolkit, not the application surfaces.** New components coexist with MudBlazor — the `MudBlazor` package reference STAYS LOADED through Phase 5. The running app continues to behave exactly as it did at v1.1 Phase 2 completion. Phase 6 migrates the marquee surfaces. Phase 7 migrates the rest, runs the a11y audit, and deletes MudBlazor at the terminal slice.

**In scope (24 reqs from REQUIREMENTS.md):**
- DS-01..DS-06 — design tokens, accent variants (orange default; terracotta + sage tokens wired but not surfaced), density modes, Inter typography, dark-mode parity, striped photo placeholder
- ATOM-01..ATOM-10 — 10 atom components (no `Mud*` symbol imports in any new component)
- SHELL-01..SHELL-04 — `MainLayout`, `<Sidebar>`, `<TopBar>`, `<NavRow>`
- DIALOG-01..DIALOG-04 — `<CbDialog>`, `CbDialogService`, `CbToastService`, `<CbDropdown>`

**Not in scope (deferred — do NOT pull forward):**
- Migrating any application surface (Home, Cooking Mode, Recipe View, etc.) → Phase 6
- Removing the `MudBlazor` package reference / `AddMudServices()` / `_Imports.razor` MudBlazor using → Phase 7 terminal slice (MIG-01..03)
- User-facing accent variant picker (terracotta/sage) → FUTURE-14 (tokens are wired but no UI surface)
- Self-hosting the Inter font → FUTURE (keep loading from `rsms.me/inter/inter.css`)
- Smart pantry-match algorithm → FUTURE-13 (irrelevant to Phase 5)
- Per-step temperature, tags relational, README docs → v1.1 Phase 4 deferred to v1.3+

</domain>

<decisions>
## Implementation Decisions

### A. CSS Architecture & Token Delivery

- **D-01:** Tokens live in a **single global stylesheet** at `src/CookBot.Web/wwwroot/css/cookbot-design.css` — referenced once via `<link rel="stylesheet" href="css/cookbot-design.css" />` in `App.razor` (or `_Layout.cshtml` head, whichever the existing project uses). NOT scoped CSS, NOT CSS-in-Razor. Reason: matches `.planning/design-handoff/project/styles.css` 1:1; allows the `:root` / `[data-density]` / `[data-accent]` attribute selectors to drive every component without per-component overrides.
- **D-02:** The stylesheet is a near-verbatim port of `.planning/design-handoff/project/styles.css` — same custom properties, same class names (`.cb-btn`, `.cb-card`, `.cb-chip`, `.cb-row`, `.cb-stat`, `.cb-shell`, `.cb-ph`, `.cb-recipe-cap`, `.cb-rule`, `.cb-kbd`, `.eyebrow`, `.num`, `.mono`). Razor components emit these classes; they don't carry inline styles for anything tokenized.
- **D-03:** Inter font loads from `https://rsms.me/inter/inter.css` (same source as the design handoff). `<link>` tag in the head; preconnect to `fonts.googleapis.com` is NOT used because we're not using Google Fonts. Self-hosting is FUTURE.
- **D-04:** Dark-mode strategy keeps the existing **class-based** approach: `<body class="dark-mode">` via the existing `cookbot_dark_mode` localStorage toggle in `MainLayout.razor`. The new stylesheet defines dark-mode counterparts inside `body.dark-mode :root { --cream: …; }` selectors so every token has a dark variant. The handoff's `data-theme` attribute is NOT adopted (would require renaming the existing JS interop call). Reason: minimum disruption to existing behavior.
- **D-05:** Accent variants (`data-accent="orange|terracotta|sage"`) and density modes (`data-density="comfy|compact"`) attach to `<html>`. `MainLayout.razor` writes these via JS interop on first render; default is `orange` + `comfy`. The Profile surface in Phase 7 lands the density toggle (PROF-01). Accent picker is FUTURE-14 — tokens are wired in this phase, no UI.

### B. Component Naming & File Layout

- **D-06:** Razor component prefix is **`Cb`** (matches the `cb-*` CSS class convention). Final names: `CbButton`, `CbChip`, `CbCard`, `CbStat`, `CbEyebrow`, `StripedPlaceholder` (no Cb prefix — it's a generic primitive name and the `Striped` part is unique enough), `Icon` (no Cb prefix — same reason), `CbBadge`, `CbToggle`, `CbCheckbox`, `CbRadio`, `CbInput`, `CbTextarea`, `CbSelect`, `CbDropdown`, `CbDialog`, `Sidebar`, `TopBar`, `NavRow`. Exception: `Sidebar`/`TopBar`/`NavRow` are unprefixed because they're shell-specific (one of each in the app).
- **D-07:** File layout under `src/CookBot.Web/Components/`:
  ```
  Components/
    Atoms/                          ← new
      CbButton.razor
      CbChip.razor
      CbCard.razor
      CbStat.razor
      CbEyebrow.razor
      StripedPlaceholder.razor
      Icon.razor                    ← single component for all 36 outline icons
      CbBadge.razor
      CbToggle.razor
      CbCheckbox.razor
      CbRadio.razor
      CbInput.razor
      CbTextarea.razor
      CbSelect.razor
    Dialogs/                        ← new (primitives, not the existing dialog content components)
      CbDialog.razor
      CbDialogHost.razor            ← global mount point (rendered once in MainLayout)
      CbToastHost.razor             ← global mount point
    Layout/                         ← existing, but rewritten
      MainLayout.razor              ← rewrite (no MudLayout)
      Sidebar.razor                 ← new (replaces NavMenu.razor)
      TopBar.razor                  ← new
      NavRow.razor                  ← new
      ... (existing dialogs preserved for Phase 7 migration)
  ```
  Existing `Components/Layout/AddUserDialog.razor`, `AdminManageUsersDialog.razor`, `PasswordPromptDialog.razor` stay in place; they migrate from `MudDialog` → `<CbDialog>` content slot in Phase 7 (NOT this phase).

### C. Icon System

- **D-08:** Single `<Icon>` Razor component renders all 36 outline icons via inline SVG paths. The icon set is encoded as a static `Dictionary<string, RenderFragment>` (or equivalent — likely a `switch` expression) keyed by the icon name from `.planning/design-handoff/project/icons.jsx`: `home`, `book`, `pantry`, `cart`, `spark`, `prompt`, `user`, `menu`, `search`, `plus`, `check`, `clock`, `flame`, `pause`, `play`, `arrowR`, `arrowL`, `bell`, `sun`, `share`, `download`, `copy`, `pencil`, `more`, `trash`, `scale`, `bolt`, `filter`, `grid`, `list`, `chevD`, `chevR`, `flag`, `send`, `save`, `link`. Stroke width 1.6, `currentColor` for stroke, `viewBox="0 0 24 24"`, sized via a `Size` parameter (defaults to 18). Unknown icon names render a placeholder `?` glyph and log a warning (dev only).
- **D-09:** Material icons in existing `MudIcon` / `Icons.Material.Filled.*` references are NOT removed in Phase 5 — they keep working alongside the new `<Icon>` because `MudBlazor` is still loaded. Phase 6/7 swap them out per surface.

### D. Atom Component API Patterns

- **D-10:** Buttons (`<CbButton>`):
  ```razor
  <CbButton Variant="CbButtonVariant.Primary" StartIcon="@Icon.Names.Plus"
            FullWidth="false" Disabled="false" OnClick="@HandleClick">
    Save recipe
  </CbButton>
  ```
  Variants: `Primary` (cocoa fill, default), `Accent` (orange fill), `Ghost` (transparent + line border), `Subtle` (light gray fill). 999px pill radius. `StartIcon`/`EndIcon` accept icon name strings (not `RenderFragment`) — looked up by `<Icon>`. Renders as `<button class="cb-btn {variant}">`.
- **D-11:** Chips (`<CbChip>`): `Variant` enum (`Default`, `Timer`, `Ing`, `Tag`); `Icon` (string, optional); `Label` parameter OR child content. Renders `<span class="cb-chip {variant}">`.
- **D-12:** Cards (`<CbCard>`): `Padding` parameter (default 22px) + ChildContent. Renders `<div class="cb-card" style="padding:{p}px">`. `Style` slot for additional inline styles when needed.
- **D-13:** Stats (`<CbStat>`): `Label`, `Value` (string — caller supplies "128", "47", etc.), `Sub` (optional sub-text below value). Renders `<div class="cb-stat">`.
- **D-14:** `<CbEyebrow>`: child content only; renders `<div class="eyebrow">{ChildContent}</div>`.
- **D-15:** `<StripedPlaceholder>`: `Width`, `Height` (px or "100%"), `Label` (string). Renders `<div class="cb-ph" style="width:…;height:…">{Label}</div>`.
- **D-16:** Form atoms (`<CbToggle>`, `<CbCheckbox>`, `<CbRadio>`): `@bind-Value` two-way binding. Renders custom-styled markup matching the design (no native checkbox/radio styling).
- **D-17:** Input atoms (`<CbInput>`, `<CbTextarea>`, `<CbSelect>`): `@bind-Value` two-way binding; standard `Placeholder`, `Disabled` parameters. `<CbSelect>` accepts `<CbOption Value="@x">label</CbOption>` children. Renders native `<input>` / `<textarea>` / `<select>` styled via class.
- **D-18:** Badges (`<CbBadge>`): `Status` enum (`InStock`, `Low`, `Expiring`, `Out`); `Label` (string). Renders `<span class="cb-chip {status-class}">`.

### E. Shell

- **D-19:** `MainLayout.razor` is rewritten end-to-end. Removes `<MudThemeProvider>`, `<MudPopoverProvider>`, `<MudDialogProvider>`, `<MudSnackbarProvider>`, `<MudLayout>`, `<MudAppBar>`, `<MudDrawer>`, `<MudMainContent>`, `<MudContainer>`. Renders:
  ```razor
  <CbDialogHost />
  <CbToastHost />
  <div class="cb cb-shell">
    <Sidebar Active="@_active" AiOff="@_aiOff" />
    <main>
      <TopBar Title="@_title" Sub="@_sub" Breadcrumb="@_breadcrumb" RightSlot="@_rightSlot" />
      <CascadingValue Value="UserService.CurrentUserId" Name="CurrentUserId">
        @Body
      </CascadingValue>
    </main>
  </div>
  ```
  The body cascading value preserves existing per-user authorization wiring. The `<TopBar>`'s right slot is set by individual pages via a cascading parameter or layout context.
- **D-20:** `<Sidebar>` reads `UserProfile.AiEnabled` to drive the `AiOff` parameter; AI Assistant + Prompt Builder rows are hidden when AI is off (no flicker — uses `OnInitializedAsync` to load the current user before first paint, matching existing `NavMenu.razor` behavior). Active row is determined by route matching (`NavLinkMatch.Prefix` semantics, ported to Razor's `NavManager.LocationChanged`).
- **D-21:** `<TopBar>` user-switcher dropdown is implemented via `<CbDropdown>` (D-25). Password prompt continues to fire via `CbDialogService` (D-23). Dark-mode toggle continues to write `cookbot_dark_mode` localStorage and toggle the `body.dark-mode` class — preserved exactly from current `MainLayout.razor`. Admin "Manage users" button continues to launch `AdminManageUsersDialog` via `CbDialogService`.
- **D-22:** `<NavRow>` accepts `Icon` (string, looked up by `<Icon>`), `Label`, `Active` (bool), `Hidden` (bool), `Kbd` (optional kbd hint string), `Href` (string for navigation). Renders `<NavLink class="cb-row {active}">` to preserve route-matching active state.

### F. Dialog & Toast Primitives

- **D-23:** `<CbDialog>` is a presentational primitive. Usage:
  ```razor
  <CbDialog @bind-IsOpen="_isOpen" MaxWidth="CbDialogMaxWidth.Sm"
            FullWidth="true" CloseOnEscape="true" CloseOnScrim="true">
    <Header>Confirm delete</Header>
    <Body>Are you sure you want to delete this cookbook?</Body>
    <Footer>
      <CbButton Variant="Ghost" OnClick="@Cancel">Cancel</CbButton>
      <CbButton Variant="Accent" OnClick="@Confirm">Delete</CbButton>
    </Footer>
  </CbDialog>
  ```
  Internally renders a fixed-position scrim + centered card; focus trap implemented via JS interop (`wwwroot/js/cb-dialog.js`); body scroll lock applied while any dialog is open.
- **D-24:** `CbDialogService` (DI Scoped) replaces `IDialogService`. API matches MudBlazor's surface for migration ease:
  ```csharp
  public interface ICbDialogService
  {
      Task<CbDialogResult> ShowAsync<TDialog>(string title, CbDialogParameters parameters, CbDialogOptions options) where TDialog : ComponentBase;
      // ... shorter overloads
  }
  public sealed record CbDialogResult(bool Canceled, object? Data);
  ```
  Implementation: dispatches a `(TDialog, parameters, options)` triple to `CbDialogHost` via an event, awaits a `TaskCompletionSource<CbDialogResult>` resolved when the dialog calls `Close(result)` or is dismissed. Multiple-dialog stacking supported via a stack in `CbDialogHost`.
- **D-25:** `CbToastService` (DI Singleton) replaces `MudSnackbarProvider`. API:
  ```csharp
  public interface ICbToastService
  {
      void Show(string message, CbToastSeverity severity = CbToastSeverity.Info);
  }
  ```
  Severities: `Success` (green-soft bg), `Error` (warn-soft bg), `Info` (paper-2 bg), `Warning` (warn-soft bg). Toasts queue in `CbToastHost`; each fades after 5s; max 3 stacked bottom-right. No swipe-to-dismiss this phase (mobile users tap to dismiss via close button).
- **D-26:** `<CbDropdown>` is a standalone select-style dropdown (used by `<TopBar>` user-switcher). API:
  ```razor
  <CbDropdown @bind-Value="_selectedUserId" Label="@_currentUserName">
    @foreach (var u in _users)
    {
      <CbDropdownItem Value="@u.Id">@u.DisplayName</CbDropdownItem>
    }
  </CbDropdown>
  ```
  Keyboard nav (Up/Down/Enter/Escape), close on outside click, close on item select. NOT the same as `<CbSelect>` (which is a form input wrapping native `<select>`). `<CbDropdown>` is for menus; `<CbSelect>` is for forms.

### G. Sandbox Verification Surface

- **D-27:** A `/design-sandbox` route is added (`Components/Pages/DesignSandbox.razor`). It renders one of every atom + a sample dialog + sample toast trigger + a Sidebar + a TopBar + every icon name. **Light-mode AND dark-mode visual smoke pass** is the verification gate for Phase 5 SC#1, SC#3, SC#4. Hidden in production routing — only accessible via direct navigation. **Phase 7 deletes this page** as part of MIG cleanup.
- **D-28:** No bUnit tests authored in Phase 5 unless trivially helpful. The sandbox-page visual verification is the gate. Reason: per-atom unit tests are low-leverage at this stage; the real integration test is "every Phase 6 surface renders correctly using these atoms" — that's where behavior-level tests pay off. (User can override this later if they want unit coverage.)

### H. AI-Off Contract Preservation

- **D-29:** `<Sidebar>` is the only surface in Phase 5 that materially reacts to `UserProfile.AiEnabled`. The contract carrying forward from v1.1: when AI is off, the AI Assistant and Prompt Builder rows are hidden (no flicker on first render). Existing `NavMenu.razor` reads the user's profile in `OnInitializedAsync` AND `OnAfterRenderAsync` to handle prerender vs interactive boundaries — `<Sidebar>` keeps the same pattern. **Phase 5 SC#2 verifies this.**

### I. Build & Dependency Hygiene

- **D-30:** `MudBlazor` and `MudBlazor.Services` package references stay in `CookBot.Web.csproj` through Phase 5. `_Imports.razor` keeps `@using MudBlazor`. `Program.cs` keeps `builder.Services.AddMudServices()`. New code in this phase imports zero `Mud*` symbols. **Phase 5 SC#5: `dotnet build` succeeds and `dotnet test` passes; the existing `MudBlazor` package reference still loads.**

</decisions>

<canonical_refs>
## Canonical Refs (MANDATORY for downstream agents)

Every downstream researcher / planner / executor MUST read these:

- `.planning/design-handoff/README.md` — design package brief
- `.planning/design-handoff/chats/chat1.md` — design conversation transcript (intent + system commitments)
- `.planning/design-handoff/project/index.html` — design canvas (9 surfaces)
- `.planning/design-handoff/project/styles.css` — **the design system tokens — port near-verbatim into `src/CookBot.Web/wwwroot/css/cookbot-design.css`**
- `.planning/design-handoff/project/icons.jsx` — **the icon set — port the SVG path data into the `<Icon>` Razor component**
- `.planning/design-handoff/project/shell.jsx` — Sidebar/TopBar/Logo intent
- `.planning/design-handoff/project/screens/home.jsx` — sample of how atoms compose (Sidebar + TopBar + atom usage); reference for Phase 6 but informs API design here
- `.planning/REQUIREMENTS.md` — v1.2 requirements (DS-01..DS-06, ATOM-01..ATOM-10, SHELL-01..SHELL-04, DIALOG-01..DIALOG-04)
- `.planning/ROADMAP.md` — Phase 5 success criteria (SC#1..SC#5)
- `.planning/PROJECT.md` — Key Decisions (v1.2 D1..D5 are foundational)
- `.planning/codebase/STRUCTURE.md` — file layout conventions
- `.planning/codebase/CONVENTIONS.md` — Razor/C# style conventions (nullable refs, implicit usings)
- `src/CookBot.Web/Components/Layout/MainLayout.razor` — current layout (about to be rewritten — preserve dark-mode/user-switcher behavior)
- `src/CookBot.Web/Components/Layout/NavMenu.razor` — current nav (about to be replaced with `<Sidebar>` — preserve AI-off contract)

</canonical_refs>

<code_context>
## Existing Code Insights

- **Existing `MainLayout.razor`** (lines 1–229) uses `<MudThemeProvider>`, `<MudLayout>`, `<MudAppBar>`, `<MudDrawer>` with hardcoded MudTheme palette (orange `#E65100` primary, cream `#FFF8E1` background). The new design tightens these tokens (`--accent: #C2410C`, `--cream: #FBF6E7`); the new MainLayout drops the MudThemeProvider entirely and relies on the global stylesheet.
- **Dark-mode toggle** lives in `MainLayout.razor` (`ToggleDarkMode` method at ~line 161). Writes `cookbot_dark_mode` localStorage and toggles `body.dark-mode` class via JS interop. Phase 5 preserves this exactly — only the styling rules change.
- **User-switcher** in `MainLayout.razor` (~lines 19–30) uses `<MudSelect>` styled as transparent text. Phase 5 replaces with `<CbDropdown>` rendered in `<TopBar>`. Password prompt flow (line 167+) preserves through `CbDialogService`.
- **`NavMenu.razor`** (lines 1–41) is short — straightforward port to `<Sidebar>` + `<NavRow>`. Reads user profile in `OnInitializedAsync` AND `OnAfterRenderAsync` to drive `_aiEnabled` — must preserve in `<Sidebar>`.
- **Dialog call sites:** ~14 dialogs invoke `IDialogService.ShowAsync<TDialog>(...)` from various Razor pages (search for `DialogService.ShowAsync` to enumerate). Phase 7 migrates them; Phase 5 just provides the `CbDialogService` API.
- **MudThemeProvider's typography** specifies Inter as `FontFamily = new[] { "Inter", "Helvetica", "Arial", "sans-serif" }`. The new global stylesheet adopts the same stack but adds `font-feature-settings: "ss01", "cv11"` and tabular-numeral support.

</code_context>

<specifics>
## Specific Ideas

- **Sandbox surface route** is `/design-sandbox` — direct navigation only (not in sidebar nav). Renders every atom in light + dark mode side-by-side using a CSS multi-column layout for compact verification.
- **Component file structure** uses `Components/Atoms/`, `Components/Dialogs/`, `Components/Layout/` directories so a developer searching for a component finds it by category.
- **Icon component** dispatches via `switch` expression on `Name` parameter; SVG `<path>` data ported verbatim from `icons.jsx`. Stroke is `currentColor` so chevron/etc. inherit text color.
- **`<CbDialog>` mount pattern**: `<CbDialogHost />` and `<CbToastHost />` mounted ONCE in `MainLayout.razor`; nothing else mounts them. They subscribe to events from the DI services and render dialogs/toasts as needed.
- **Focus trap** implemented in `wwwroot/js/cb-dialog.js` (new file). Listens for `keydown` on the dialog element, traps Tab/Shift+Tab inside; restores focus to the trigger element on close.

</specifics>

<deferred>
## Deferred Ideas

Captured for later — not in Phase 5.

- User-facing **accent variant picker** (terracotta/sage) → FUTURE-14 in REQUIREMENTS.md. Tokens are wired in this phase; the toggle UI lands when there's user demand.
- **Per-atom bUnit unit tests** — D-28 explicitly defers these. Sandbox visual smoke pass is the gate. Add later if regressions emerge.
- **Mobile sidebar drawer collapse** (the design's index.html has a `<Tablet>` and `<Phone>` frame for cooking + grocery, but the sidebar is desktop-shaped at 232px wide). Phase 7 GR-* (grocery mobile-first) is where the responsive shell decision lives — Phase 5 ships the desktop-shaped shell; mobile breakpoint behavior of `<Sidebar>` is decided in Phase 7.
- **Toast swipe-to-dismiss** — D-25 ships click/auto-dismiss only.
- **Animation library** — no GSAP/Framer this milestone. CSS keyframes (already present in handoff styles) cover streaming caret + drafting pulse.

</deferred>
