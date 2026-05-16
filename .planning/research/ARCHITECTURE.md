# Architecture Research

**Domain:** v1.3 Production-Ready & Format Maturity — integration points for new capabilities into existing Clean/Onion stack
**Researched:** 2026-05-15
**Confidence:** HIGH (all findings drawn from direct source inspection of the live codebase + official ASP.NET Core 10 docs)

---

## Integration Point Map

Every v1.3 capability is listed below with its named seam in the existing code. "New file" means a file that does not yet exist; "touch" means an existing file that must change.

### Bucket 1 — Schema v3 + Photos

#### RecipeDocument v3 placement

**Decision:** v3 is a new `sealed record RecipeDocumentV3` in the same file, `src/CookBot.Domain/Recipes/RecipeDocument.cs`, following the same `sealed record RecipeDocument` declaration that is v2 today.

v1.1 Phase 1 did not version the type name — it just replaced the monolithic entity with a single canonical record. There is no `RecipeDocumentV1.cs` / `RecipeDocumentV2.cs` pattern to mirror because the upcasting happens at the JSON-node level (`IRecipeUpcaster.Upcast(JsonNode)`) before typed deserialization, so the C# type is always the current version. The correct pattern therefore is:

1. Add three new nullable properties to the **existing** `RecipeDocument` record: `PhotoUrl`, `Description`, and (on `ContentStep`) `Temperature`.
2. Bump `RecipeUpcasterChain.CurrentVersion` from `2` to `3`.
3. Add a new `Migration_V2_To_V3` upcaster that stamps `version: 3` and otherwise no-ops (all three fields default `null` when absent).

This keeps the C# type stable — there is no `RecipeDocumentV2` alias to maintain — while the version field in the JSON column tracks the schema generation. `JsonRecipeSerializer` (which uses `WhenWritingNull`) will omit the three new nullable fields from compact storage when not set, preserving backward-compat on read.

Files:
- **Touch** `src/CookBot.Domain/Recipes/RecipeDocument.cs` — add `PhotoUrl string?`, `Description string?`
- **Touch** `src/CookBot.Domain/Recipes/StepNode.cs` — add `Temperature int?` to `ContentStep`
- **Touch** `src/CookBot.Application/Recipes/RecipeUpcasterChain.cs` — bump `CurrentVersion = 3`
- **New** `src/CookBot.Application/Recipes/Migration_V2_To_V3.cs` — trivial: stamps version, no-ops on data
- **Touch** `src/CookBot.Application/DependencyInjection.cs` — register `services.AddSingleton<IRecipeUpcaster, Migration_V2_To_V3>()`

#### V2→V3 upcaster default values

`Migration_V2_To_V3.Upcast(JsonNode)` must:
- Set `obj["version"] = 3`
- Leave `photoUrl`, `description`, and per-step `temperature` absent (not set to null JSON tokens) — the typed deserializer maps absent JSON keys to `null` for nullable C# properties, and `JsonRecipeSerializer` skips null on serialize. No explicit null-injection needed.

This is simpler than `Migration_V1_To_V2`, which had to rewrite the step shape. V2→V3 is a pure stamp.

#### Per-step temperature placement

`Temperature` belongs on `ContentStep` as `int? Temperature` (degrees, unit unambiguous from context — the AI prompt documents the unit). It does NOT warrant a sub-record (`Temperature` value object) at v1.3; a plain nullable int is sufficient and avoids polymorphic schema complexity.

`CookingMode.razor` reads steps from the canonical doc via `Recipe.CanonicalDocumentJson` → `JsonRecipeSerializer.Deserialize` → `RecipeDocument.Steps`. Temperature should render as a compact inline chip ("Preheat to 375°F") alongside the step text when non-null. A dedicated "preheat to" callout block is reserved for v1.4+ when doneness cues (FUTURE-04) arrive and a richer step metadata panel makes sense. At v1.3 the chip pattern is consistent with how timer chips appear on steps today.

#### File upload storage decision

**Decision: `wwwroot/uploads/` with `UseStaticFiles` middleware added in `Program.cs`.**

Rationale, sourced from official ASP.NET Core 10 docs: `MapStaticAssets()` (which is what `Program.cs` already calls) only serves assets **discovered at build time** via content fingerprinting. Files written to `wwwroot/uploads/` at runtime are invisible to `MapStaticAssets` and return 404. The official guidance for runtime-uploaded files is to call `UseStaticFiles` additionally, configured with a `PhysicalFileProvider` pointing at the upload directory. The directory can be inside `wwwroot` or outside it; the URL path is configured independently.

Using `wwwroot/uploads/` (inside the web root) keeps the directory relative to `ContentRootPath`, which Docker mounts as a single volume already containing the SQLite file. Storing outside `wwwroot` (e.g. `App_Data/uploads/`) would require a second volume mount for no architectural gain.

The correct `Program.cs` addition, placed before `app.MapStaticAssets()`:

```csharp
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(
        Path.Combine(builder.Environment.WebRootPath, "uploads")),
    RequestPath = "/uploads"
});
```

This does not conflict with the existing `MapStaticAssets()` call — they serve different content.

**Upload pipeline ownership:** file write is a Web-layer responsibility. `IFormFile` / `IBrowserFile` are ASP.NET / Blazor types unavailable in Application or Domain. The write sits in a new `IRecipePhotoStorage` interface in `CookBot.Domain/Interfaces/` (keeping it abstract) with an implementation `LocalRecipePhotoStorage` in `CookBot.Web/Services/`. The interface takes a stream + content-type, returns a relative URL string (`/uploads/{filename}`).

New files:
- **New** `src/CookBot.Domain/Interfaces/IRecipePhotoStorage.cs` — `Task<string> StoreAsync(Stream content, string contentType, CancellationToken ct)`
- **New** `src/CookBot.Web/Services/LocalRecipePhotoStorage.cs` — writes to `wwwroot/uploads/`, enforces size cap + content-type allowlist (`image/jpeg`, `image/png`, `image/webp`, `image/gif`)
- **Touch** `src/CookBot.Web/Program.cs` — register `IRecipePhotoStorage → LocalRecipePhotoStorage` (Scoped), add `UseStaticFiles` for `/uploads`

Why `IRecipePhotoStorage` in Domain rather than Application: it is a pure capability interface with no business logic. Application services can depend on it for URL-building without taking a Blazor or HTTP dependency.

#### RecipeJsonSchemaProvider update for AI

`RecipeJsonSchemaProvider.BuildSchema()` uses `JsonSchemaExporter` against the `RecipeDocument` type. Adding `PhotoUrl string?` and `Description string?` to `RecipeDocument`, and `Temperature int?` to `ContentStep`, means the schema is updated automatically at the next build — no manual schema editing required. The exporter reflects the C# type.

One manual concern: the lint denylist in `PromptBuilderService` (Plan 01-04 from v1.1) guards against the AI emitting forbidden field aliases. `image`, `imageUrl`, and `picture` should be added to that denylist because `photoUrl` is the canonical name and the AI might hallucinate alternatives.

Files:
- **Touch** `src/CookBot.Application/Services/PromptBuilderService.cs` — add `"image"`, `"imageUrl"`, `"picture"` to lint denylist

#### URL validator seam

A `RecipePhotoUrlValidator` static helper in Application validates URL scheme (allow only `http`/`https`; reject `javascript:`, `data:`, `file:`). It is called in three places:

1. `RecipeEditor.razor` — on input change, blocks save if invalid
2. `RecipeService.CreateAsync` / `UpdateAsync` — before persisting (defense-in-depth)
3. `AiRecipeGenerator` post-process — after deserialization, before returning `StructuredResult`

New file:
- **New** `src/CookBot.Application/Recipes/RecipePhotoUrlValidator.cs` — `public static bool IsAllowed(string? url)` returning `false` for non-http/https schemes or URLs exceeding 2048 chars

---

### Bucket 2 — Format Cleanup

#### LegacyRecipeProjector deletion sequence

`LegacyRecipeProjector` is referenced in six places today:

| File | Role |
|------|------|
| `src/CookBot.Infrastructure/DependencyInjection.cs` | Registered as `IRecipeProjector` |
| `src/CookBot.Application/Services/RecipeService.cs` | Injected as `IRecipeProjector _projector`; called in `CreateAsync` and `UpdateAsync` |
| `src/CookBot.Infrastructure/Data/DatabaseSeeder.cs` | Parameter to `SeedAsync`, used to backfill `CanonicalDocumentJson` |
| `src/CookBot.Infrastructure/Data/Migrations/Helpers/LegacyRecipeProjector.cs` | The class itself |
| `src/CookBot.Web/Program.cs` | Passed as argument to `DatabaseSeeder.SeedAsync` |
| `src/CookBot.Application/Recipes/IRecipeProjector.cs` | The interface |

Deletion order (fail-loud cutover):
1. Audit that all `Recipe.CanonicalDocumentJson` rows are non-null (backfill completed in v1.1 Phase 1 — but add a startup assertion in `DatabaseSeeder.SeedAsync` that throws if any row still has a null canonical document).
2. Replace the `_projector.Project(recipe)` call in `RecipeService.CreateAsync` / `UpdateAsync` with a direct `CanonicalDocumentFromParsed` constructor pattern that builds `RecipeDocument` from the in-memory `ParsedRecipe` (this is what the projector was doing anyway, minus the relational entity navigation).
3. Remove `LegacyRecipeProjector` from `DependencyInjection.cs`, from `DatabaseSeeder.SeedAsync` signature, and from `Program.cs`.
4. Delete files: `LegacyRecipeProjector.cs`, `IRecipeProjector.cs`.

After deletion, `RecipeService` no longer needs `IRecipeProjector` in its constructor.

Files to touch: `RecipeService.cs`, `DependencyInjection.cs` (Infrastructure), `DatabaseSeeder.cs`, `Program.cs`
Files to delete: `LegacyRecipeProjector.cs`, `IRecipeProjector.cs`

#### TagsJson → relational RecipeTag

**New entity:** `RecipeTag(int Id, int RecipeId, string Name)` with a composite unique index on `(RecipeId, Name)`.

Integration points:
- **New** `src/CookBot.Domain/Entities/RecipeTag.cs`
- **New** `src/CookBot.Infrastructure/Data/Configurations/RecipeTagConfiguration.cs`
- **Touch** `src/CookBot.Infrastructure/Data/CookBotDbContext.cs` — add `DbSet<RecipeTag> RecipeTags`
- **New** EF migration `AddRecipeTagTable` (includes data migration: reads `Recipe.TagsJson`, inserts `RecipeTag` rows, does NOT drop `TagsJson` in this migration — `TagsJson` becomes a deletion-target column, removed in the next migration after the app has run once with both columns present)

`Recipe.Tags` (new navigation property `ICollection<RecipeTag>`) is the authoritative read path. New code must never read `Recipe.TagsJson`. The canonical doc `RecipeDocument.Tags` (which is a `IReadOnlyList<string>`) is populated from the `RecipeTag` rows at serialize time.

The `LegacyRecipeProjector` already reads `TagsJson` — this is another reason to delete it before or alongside the tags migration.

`RecipeService.CreateAsync` / `UpdateAsync` writes tags to `RecipeTag` rows after the migration. The canonical doc's `Tags` list is sourced from those rows.

#### Prompt snapshot test

A new xUnit test class in `tests/CookBot.Tests/Services/PromptBuilderServiceTests.cs` (the file may already exist with other tests; if not, create it). Snapshot files live in `tests/CookBot.Tests/Snapshots/` (flat, next to the test files' output). The test calls `PromptBuilderService.BuildSystemPrompt(defaultProfile, emptyPantry)` and compares the output against a committed `.txt` snapshot. Any change to the prompt requires a deliberate snapshot update (the test fails and the developer updates the file).

No new NuGet package is required — use a simple `File.ReadAllText` + `Assert.Equal` pattern. A helper method `UpdateSnapshot(string name, string actual)` writes the file when an env var `COOKBOT_UPDATE_SNAPSHOTS=1` is set, keeping the pattern self-documenting.

New files:
- **New** `tests/CookBot.Tests/Snapshots/BuildSystemPrompt_DefaultProfile.txt` (committed)
- **Touch or new** `tests/CookBot.Tests/Services/PromptBuilderServiceTests.cs`

---

### Bucket 3 — QOL

#### Smart pantry-match

The current `BuildPantryMatchesAsync` in `Home.razor.cs` (a 40-line inline method, comment-flagged `FUTURE-13`) must move into an `IPantryMatchService` in the Application layer. The interface belongs in Application because the algorithm is pure business logic — it requires pantry items, recipe ingredient data, user dietary prefs, and recent cooks (from `IRecipeMadeService`), all of which flow in as parameters with no UI or HTTP concerns.

**Decision:** Extend `IPantryService` for simple add-ons but use a new `IPantryMatchService` for the scoring algorithm. The scoring algorithm is a distinct concern; mixing it into `IPantryService` would grow that class beyond its current CRUD scope.

Interface:
```csharp
// src/CookBot.Application/Services/IPantryMatchService.cs
public interface IPantryMatchService
{
    Task<IReadOnlyList<PantryMatchResult>> GetMatchesAsync(
        int userId,
        IList<PantryItem> pantryItems,
        int maxResults = 3,
        CancellationToken ct = default);
}

public sealed record PantryMatchResult(
    int RecipeId,
    string RecipeName,
    int MatchedCount,
    int TotalCount,
    string MetaLine,
    string? MissingIngredientName,
    double Score);
```

Implementation `PantryMatchService` in Application (scoped) injected with `CookBotDbContext` directly — the same pattern used by `Home.razor.cs` today. The service reads `RecipeIngredients` with `Include(Ingredient)`, computes the ingredient-coverage ratio, filters by dietary prefs from `UserProfile`, boosts recently-cooked recipes slightly in rank, excludes expiring pantry items from the "covered" count.

`Home.razor.cs` removes `BuildPantryMatchesAsync` and injects `IPantryMatchService` instead. `_pantryMatches` stays typed to the new record.

New files:
- **New** `src/CookBot.Application/Services/IPantryMatchService.cs` (interface + result record)
- **New** `src/CookBot.Application/Services/PantryMatchService.cs` (implementation)
- **Touch** `src/CookBot.Application/DependencyInjection.cs` — `AddScoped<IPantryMatchService, PantryMatchService>()`
- **Touch** `src/CookBot.Web/Components/Pages/Home.razor.cs` — inject `IPantryMatchService`, remove `BuildPantryMatchesAsync`

#### AiChat "Edit anyway" hardening

The current `OpenDraftInEditor` in `AiChat.razor` (lines 717–755) already routes through `Parser.TryParse(rawJson, ...)`. The fragile path is the D-09 fallback (line 753): if parsing fails, a toast says "copy it and paste into the editor" — actionable but manual. The hardening at v1.3 routes the D-09 path to a `RawRecipeEditorDialog` instead of just a toast.

The architectural seam is: `OpenDraftInEditor` → on parse failure → `CbDialogService.ShowAsync<RawRecipeEditorDialog>(rawJson)`. `RawRecipeEditorDialog` presents a textarea pre-filled with the raw JSON, a "Try to parse" button (which re-runs `Parser.TryParse` and on success opens `SaveRecipeDialog`), and a "Copy to clipboard" button. This keeps `AiChat.razor` as the entry point — no new routing surface — and avoids exposing a bypass that persists non-conforming recipes (the re-parse gate is preserved).

No new service is required; this is a dialog + existing parser call.

New files:
- **New** `src/CookBot.Web/Components/Pages/RawRecipeEditorDialog.razor`
- **Touch** `src/CookBot.Web/Components/Pages/AiChat.razor` — replace D-09 toast with dialog open

#### Accent variant picker

**Decision: localStorage, matching the density toggle pattern.** The density toggle (v1.2 Phase 7 / Plan 07-05) explicitly uses `localStorage` because adding a `Density` column to `UserProfile` would require a migration for a pure UI preference. The exact same reasoning applies to accent: it is a cosmetic browser preference, not a user data attribute. The existing `cookbot-shell.js` already implements `window.cookbot.setAccent(name)` (with `orange | terracotta | sage` allowlist) and `window.cookbot.applyDefaults()` restores it on load. The accent key is `cookbot_accent` (implied by the shell code structure; confirm and persist it there).

What is missing: the Profile page has no UI for the picker. `EditProfile.razor` needs a new section (after the density toggle) with three accent-swatch buttons that call `await JS.InvokeVoidAsync("cookbot.setAccent", name)` and also write to `localStorage` via the existing helper.

Files:
- **Touch** `src/CookBot.Web/wwwroot/js/cookbot-shell.js` — add `localStorage.setItem("cookbot_accent", v)` to `setAccent` and restore it in `applyDefaults`
- **Touch** `src/CookBot.Web/Components/Pages/EditProfile.razor` — add accent picker UI + JS interop

No new migration. No `UserProfile` column.

#### Profile-side AI prompt editor

`UserProfile.AiSystemPromptTemplate` already exists (the column is present and `PromptBuilderService.ResolveTemplate` reads it). What is missing is an editor surface on `EditProfile.razor`.

The editor is a `<textarea>` bound to `_aiPromptTemplate` plus a variable-insertion dropdown showing the tokens (`{{experience_level}}`, `{{unit_system}}`, `{{equipment}}`, `{{dietary_preferences}}`, `{{pantry}}`, `{{recipe_format}}`). A "Reset to default" button clears `_aiPromptTemplate` (sets to `null`), which causes `PromptBuilderService.ResolveTemplate` to use `DefaultTemplate`.

The save path goes through `CurrentUserService` or directly through `CookBotDbContext.UserProfiles` (same pattern as other profile fields). No new service is needed.

Files:
- **Touch** `src/CookBot.Web/Components/Pages/EditProfile.razor` — add prompt editor section

---

### Bucket 4 — Small-stuff Polish

#### Cookbook reparenting on edit (D-26)

`RecipeService.UpdateAsync(int recipeId, int userId, ParsedRecipe parsed)` does not accept a `cookbookId`. Reparenting requires:

1. Add an optional `int? newCookbookId` parameter to `UpdateAsync`.
2. When non-null: load the new cookbook, verify `cookbook.UserId == userId` (user must own the destination), update `recipe.CookbookId`.
3. `RecipeEditor.razor` exposes a cookbook picker (dropdown over the user's owned cookbooks) that passes `newCookbookId` on save.

Shared cookbooks cannot be moved into (the ownership check blocks it). This is correct — if user A shares cookbook X with user B, user B cannot reparent a recipe into X.

Files:
- **Touch** `src/CookBot.Application/Services/RecipeService.cs` — add `newCookbookId` param
- **Touch** `src/CookBot.Web/Components/Pages/RecipeEditor.razor` — cookbook picker

#### Pantry per-row quick-add (D-37)

`GroceryListService` has `GenerateFromRecipeAsync` and `GenerateAllFromRecipeAsync` but no single-item add. A new `AddItemAsync(int userId, string ingredientName, string? unit = null)` method is needed that:
1. Resolves or creates the user's primary grocery list (first list owned by `userId`, or creates one).
2. Appends a `GroceryListItem`.

`PantryView.razor` adds a grocery-cart icon button per pantry row calling `GroceryListService.AddItemAsync`.

Files:
- **Touch** `src/CookBot.Application/Services/GroceryListService.cs` — add `AddItemAsync`
- **Touch** `src/CookBot.Web/Components/Pages/PantryView.razor` — quick-add button per row

#### Moon glyph (D-15)

The dark-mode toggle in `TopBar.razor` renders `<Icon Name="@Icon.Names.Sun" Size="16" />` regardless of mode. The toggle should show the moon glyph when in light mode (indicating "click to go dark") and the sun when in dark mode. This requires:

1. Adding a `Moon` constant to `Icon.Names` and an SVG path entry in `Icon.razor`.
2. Updating `TopBar.razor` to use `IsDarkMode ? Icon.Names.Sun : Icon.Names.Moon`.

The moon SVG (crescent): `<path d="M21 12.79A9 9 0 1 1 11.21 3 7 7 0 0 0 21 12.79z"/>`.

Files:
- **Touch** `src/CookBot.Web/Components/Atoms/Icon.razor` — add `Moon = "moon"` constant and SVG path

#### TopBar RightSlot passthrough (D-16)

`TopBar.razor` already has `[Parameter] public RenderFragment? RightSlot { get; set; }` (line 80) and renders it at line 46 (`@RightSlot`). The gap is that `MainLayout.razor` does not expose a way for child pages to populate the slot.

The idiomatic Blazor Server pattern for page → layout slot injection is a cascading parameter from `MainLayout` to its `@Body` subtree. `MainLayout` exposes:
- `[Parameter] public RenderFragment? TopBarRightSlot { get; set; }` — Blazor doesn't support this directly on layout.
- Correct approach: use a service (`ICbTopBarService`) with a `RenderFragment? RightSlot` property and `StateHasChanged` callback, registered as Scoped. `MainLayout` subscribes and passes the current `RightSlot` to `TopBar`. Pages inject the service and set `RightSlot` in `OnInitialized`.

This is the same pattern used by toast services (already present as `ICbToastService`). A new `ICbTopBarService` follows that shape.

New files:
- **New** `src/CookBot.Web/Services/CbTopBarService.cs` — `RenderFragment? RightSlot`, `Action? OnChanged`
- **Touch** `src/CookBot.Web/Program.cs` — register `AddScoped<ICbTopBarService, CbTopBarService>()`
- **Touch** `src/CookBot.Web/Components/Layout/MainLayout.razor` — inject service, subscribe, pass `RightSlot` to `TopBar`

#### Home active-timer live JS tick (D-16 / Plan 07-09 adjacent)

The active-timer countdown on `Home.razor` renders a server-side `FormatTimerRemaining(t.RemainingSeconds)` at page-load time. It does not tick. The DOM element already has `id="@_activeTimerCountdownId"` and `data-started-at` / `data-duration` attributes (built for exactly this purpose — "a future enhancement could JS-tick the countdown without a re-render").

The fix is a small JS addition to `cooking-timers.js` (or a new `home-timer-tick.js` — prefer the existing file to avoid an extra `<script>` tag):

```js
// Ticks the home active-timer countdown element without Blazor re-render.
window.CookingTimers.startHomeTick = function (elementId) {
    var el = document.getElementById(elementId);
    if (!el) return;
    var started = new Date(el.dataset.startedAt).getTime();
    var duration = parseInt(el.dataset.duration, 10) * 1000;
    setInterval(function () {
        var remaining = Math.max(0, Math.round((started + duration - Date.now()) / 1000));
        // format MM:SS
        var m = Math.floor(remaining / 60), s = remaining % 60;
        el.textContent = m.toString().padStart(2,'0') + ':' + s.toString().padStart(2,'0');
    }, 1000);
};
```

`Home.razor.cs` calls `JS.InvokeVoidAsync("CookingTimers.startHomeTick", _activeTimerCountdownId)` in `OnAfterRenderAsync` after `_activeTimer` is populated.

Files:
- **Touch** `src/CookBot.Web/wwwroot/js/cooking-timers.js` — add `startHomeTick`
- **Touch** `src/CookBot.Web/Components/Pages/Home.razor.cs` — call `startHomeTick` after session load

---

### Bucket 5 — Prod-Ready

#### AI key encrypt-at-rest

**Trust model for AiApiKeyShare:** The sharer owns the key in their `UserProfile.AiApiKey` (encrypted). The share table (`AiApiKeyShares`) contains only `OwnerUserId` / `RecipientUserId` — no key copy. `AiApiKeyResolutionService.ResolveAsync` joins the share table against the owner's decrypted key at resolution time (server-side only, same as today). The recipient never sees or stores the cleartext key. This means a single data-protection scope covers all key reads — there is no per-sharer scope or re-encryption needed.

**IDataProtector integration:** ASP.NET Core `Microsoft.AspNetCore.DataProtection` is already a transitive dependency (confirmed in `obj/project.assets.json`). The wrapping is in `AiApiKeyResolutionService` and in `EditProfile.razor` save path:

- On **write** (`EditProfile.razor` saves the key): `IDataProtector.Protect(plaintextKey)` → store ciphertext in `UserProfile.AiApiKey`.
- On **read** (`AiApiKeyResolutionService.ResolveAsync`): `IDataProtector.Unprotect(ciphertext)` → pass cleartext to HTTP client.

`IDataProtector` is registered in `Program.cs` via `builder.Services.AddDataProtection()`. For Docker: key ring persists to `/app/keys/` via `.PersistKeysToFileSystem(new DirectoryInfo("/app/keys/"))`. The `Program.cs` registration reads the path from `appsettings.json` (`"CookBot": { "DataProtectionKeysPath": "/app/keys/" }`) with a fallback to a dev-local path.

A one-time migration is needed for existing plaintext keys: `DatabaseSeeder.SeedAsync` detects unprotected keys (heuristic: if `AiApiKey` does not start with `CfDJ8` — the DataProtection ciphertext prefix — re-encrypt it) and writes back the protected form. This is safe to run on every startup (already-protected keys pass the check without re-protection).

Files:
- **Touch** `src/CookBot.Web/Program.cs` — `builder.Services.AddDataProtection().PersistKeysToFileSystem(...)`
- **Touch** `src/CookBot.Web/Services/AiApiKeyResolutionService.cs` — inject `IDataProtector`, call `Unprotect` before returning key
- **Touch** `src/CookBot.Web/Components/Pages/EditProfile.razor` — inject `IDataProtector`, call `Protect` before saving
- **Touch** `src/CookBot.Infrastructure/Data/DatabaseSeeder.cs` — re-encrypt any existing plaintext key on startup

#### Dockerfile + compose

**Placement:** `docker/Dockerfile` and `docker/docker-compose.yml`. Not at repo root — the existing `run.sh` is at root and is the local-dev entrypoint. Docker assets in a subdirectory avoid cluttering the root for users who do not self-host via Docker.

`run.sh` is unchanged for local dev. `docker compose up` is the self-hoster path.

Three persistent volumes for the compose file:
- `./data/cookbot.db:/app/cookbot.db` (SQLite file)
- `./data/uploads:/app/wwwroot/uploads` (recipe photos)
- `./data/keys:/app/keys` (DataProtection key ring)

The connection string in the container environment overrides to `Data Source=/app/cookbot.db`. The `WebRootPath` resolves to `/app/wwwroot` inside the container; the `UseStaticFiles` `PhysicalFileProvider` path for uploads becomes `/app/wwwroot/uploads` at runtime.

Files:
- **New** `docker/Dockerfile`
- **New** `docker/docker-compose.yml`
- **New** `docker/.env.example` (documents `COOKBOT_ANTHROPIC_API_KEY`, `COOKBOT_DATA_PROTECTION_KEYS_PATH`)

#### Token-cost telemetry

**New entity:** `AiUsageLog` in Domain.

```csharp
// src/CookBot.Domain/Entities/AiUsageLog.cs
public class AiUsageLog
{
    public int Id { get; set; }
    public int UserId { get; set; }         // who triggered the call
    public int? KeyOwnerUserId { get; set; } // null = own key; non-null = shared key owner
    public string ModelName { get; set; } = string.Empty;
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public decimal EstimatedCostUsd { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
}
```

**Write point:** Inside `AnthropicAiService.SendStructuredAsync<T>` — specifically by parsing the `message_start` SSE event's `usage.input_tokens` and the `message_delta` event's `usage.output_tokens`. These fields are present in the Anthropic SSE stream but the current `SendStructuredAsync` ignores them. Both the stream loop and the `message_start` event handling need additions.

However, `AnthropicAiService` is in Infrastructure and cannot write directly to `CookBotDbContext` today (it is stateless / `Scoped` but does not hold a `DbContext` reference). The cleanest seam is a callback: `SendStructuredAsync` returns a `StructuredResult<T>` that is extended to carry `int InputTokens, int OutputTokens`. The caller (`AiRecipeGenerator.GenerateAsync` in Application) passes the counts up to whatever write-point is appropriate. In practice the write point is in `AiChat.razor` after `GenerateAsync` returns — it writes an `AiUsageLog` row via `CookBotDbContext`.

`StreamMessageAsync` (used for non-structured chat) similarly should surface token counts. Since it is `IAsyncEnumerable<string>` today, the cleanest addition is a new overload that also reports final usage, or a `ValueTask<(int inputTokens, int outputTokens)>` out param on a wrapper method.

**Aggregation surface:** A Profile widget shows per-user totals (`SUM(InputTokens)`, `SUM(OutputTokens)`, `SUM(EstimatedCostUsd)`) and the key owner sees a breakdown by `UserId` for all share recipients.

Integration:
- **New** `src/CookBot.Domain/Entities/AiUsageLog.cs`
- **New** `src/CookBot.Infrastructure/Data/Configurations/AiUsageLogConfiguration.cs`
- **Touch** `src/CookBot.Infrastructure/Data/CookBotDbContext.cs` — add `DbSet<AiUsageLog>`
- **New** EF migration `AddAiUsageLog`
- **Touch** `src/CookBot.Infrastructure/AI/AnthropicAiService.cs` — parse `message_start` / `message_delta` usage fields, include counts in `StructuredResult<T>`
- **Touch** `src/CookBot.Application/AI/AiRecipeGenerator.cs` — surface token counts from `StructuredResult`
- **Touch** `src/CookBot.Web/Components/Pages/AiChat.razor` — write `AiUsageLog` row after structured generate + streaming complete
- **Touch** `src/CookBot.Web/Components/Pages/EditProfile.razor` — add telemetry summary widget

---

## Data Flow Diagrams

### File Upload → Canonical Document

```
RecipeEditor.razor
  │
  ├─ User pastes URL into CbInput  → _photoUrl (string?)
  │    │
  │    └─ RecipePhotoUrlValidator.IsAllowed(_photoUrl)
  │         ├─ false → validation error chip, save blocked
  │         └─ true  → allow
  │
  ├─ User selects file (IBrowserFile)
  │    │
  │    └─ IRecipePhotoStorage.StoreAsync(stream, contentType)
  │         └─ LocalRecipePhotoStorage
  │              ├─ content-type check (allow jpeg/png/webp/gif)
  │              ├─ size cap (e.g. 10 MB)
  │              ├─ write to wwwroot/uploads/{guid}.{ext}
  │              └─ return "/uploads/{guid}.{ext}"
  │
  └─ Save → RecipeService.UpdateAsync(recipeId, userId, parsed)
       │    (parsed.PhotoUrl = validated URL or storage URL)
       │
       └─ RecipeDocument { ..., PhotoUrl = parsed.PhotoUrl }
            └─ JsonRecipeSerializer.Serialize → Recipe.CanonicalDocumentJson (DB column)

Browser fetch:
  /uploads/{file}
    → UseStaticFiles PhysicalFileProvider(wwwroot/uploads)
    → file bytes served with correct Content-Type
```

### Encrypt-at-Rest → AiApiKeyShare Read Path

```
EditProfile.razor (save)
  → IDataProtector.Protect(plaintextKey)
  → UserProfile.AiApiKey = ciphertext
  → CookBotDbContext.SaveChanges()

AiApiKeyResolutionService.ResolveAsync(userId)
  → DbContext.UserProfiles.AsNoTracking() → profile
  ├─ profile.AiApiKey (ciphertext, non-null)
  │    └─ IDataProtector.Unprotect(ciphertext) → plaintextKey
  │         └─ return EffectiveAiCredentials(plaintextKey, ...)
  │
  └─ (shared key path) join AiApiKeyShares + UserProfiles
       → owner.AiApiKey (ciphertext)
       → IDataProtector.Unprotect(owner.AiApiKey) → plaintextKey
       → return EffectiveAiCredentials(plaintextKey, SharedFromUserId = ownerId, ...)

AnthropicAiService.CreateHttpClient(apiKey)
  → http.DefaultRequestHeaders.Add("x-api-key", apiKey)  ← plaintext, never persisted
```

### Token-Cost Telemetry Write + Read

```
Write path (structured generation):
  AiChat.razor → IAiRecipeGenerator.GenerateAsync(prompt, apiKey, modelId)
    → AnthropicAiService.SendStructuredAsync<RecipeDocument>(...)
         │  (SSE stream accumulation)
         ├─ message_start event → inputTokens
         └─ message_delta event → outputTokens
    → StructuredResult<RecipeDocument> { ..., InputTokens, OutputTokens }
  AiChat.razor ← GenerateAsync result
    → DbContext.AiUsageLog.Add(new AiUsageLog {
           UserId = currentUserId,
           KeyOwnerUserId = credentials.SharedFromUserId,
           ModelName = credentials.ModelId ?? "default",
           InputTokens, OutputTokens,
           EstimatedCostUsd = ComputeCost(modelName, inputTokens, outputTokens)
       })
    → DbContext.SaveChangesAsync()

Write path (streaming chat):
  AiChat.razor → IAiService.StreamMessageAsync(...)
    → (after stream ends) → AnthropicAiService reports final usage via wrapper
    → AiUsageLog written same as above

Read path (Profile widget — own user):
  EditProfile.razor
    → DbContext.AiUsageLogs
        .Where(l => l.UserId == userId)
        .GroupBy(_ => 1)
        .Select(g => new { TotalInput = g.Sum(l => l.InputTokens), ... })

Read path (key-owner view — shared recipients):
  EditProfile.razor (key owner section)
    → DbContext.AiUsageLogs
        .Where(l => l.KeyOwnerUserId == userId)
        .GroupBy(l => l.UserId)
        .Select(g => new { UserId = g.Key, ... })
```

### Smart Pantry-Match Inputs/Outputs

```
Home.razor.cs.LoadDashboardAsync(userId)
  → PantryService.GetAllUserAccessibleItemsAsync(userId)
       → allPantryItems (IList<PantryItem>)
  → IPantryMatchService.GetMatchesAsync(userId, allPantryItems)
       │  (inside PantryMatchService)
       ├─ CookBotDbContext.Recipes
       │    .Include(RecipeIngredients).ThenInclude(Ingredient)
       │    .Where(accessible to userId)
       │    → recipes
       │
       ├─ UserProfile.DietaryPreferencesJson → dietary filter set
       │
       ├─ IRecipeMadeService.GetRecentForUserAsync(userId, 30)
       │    → recentCookIds (for recency boost)
       │
       └─ Score each recipe:
            coverageRatio = matchedIngredients / totalIngredients
            recencyBoost  = recentCookIds.Contains(recipe.Id) ? 0.05 : 0
            score = coverageRatio + recencyBoost
            filter: score >= 0.6
            sort desc by score, then by name
            take maxResults (3)
       → IReadOnlyList<PantryMatchResult>
  → _pantryMatches (Home.razor.cs field, bound to markup)
```

---

## Build Order (Foundation → Consumer)

Dependencies determine shipping order within v1.3. The table below names the phase each bucket maps to.

| Order | Bucket / Feature | Depends On | Phase |
|-------|-----------------|------------|-------|
| 1 | Schema v3 (`RecipeDocument` + `ContentStep` + `Migration_V2_To_V3`) | Nothing | Phase 8 |
| 2 | EF migrations: `AddRecipePhotoUrl`, `AddRecipeDescription`, `AddRecipeTagTable`, `AddAiUsageLog` | Schema v3 types exist | Phase 8 |
| 3 | `LegacyRecipeProjector` deletion + `RecipeService` rebuild | v3 upcaster registered; all rows have `CanonicalDocumentJson` | Phase 8 |
| 4 | `RecipePhotoUrlValidator`, `IRecipePhotoStorage` / `LocalRecipePhotoStorage`, `UseStaticFiles` registration | Schema v3 (PhotoUrl field) | Phase 8/9 |
| 5 | Encrypt-at-rest (`AddDataProtection`, `AiApiKeyResolutionService` wrap, `DatabaseSeeder` re-encrypt) | `Program.cs` DI, existing key storage | Phase 9 |
| 6 | Token-cost telemetry (`AiUsageLog` entity, `AnthropicAiService` SSE parse, `AiChat` write, `EditProfile` widget) | `AiUsageLog` migration (step 2) | Phase 9 |
| 7 | Dockerfile + compose | encrypt-at-rest (keys volume), uploads volume, DB volume | Phase 9 |
| 8 | Smart pantry-match (`IPantryMatchService`, `PantryMatchService`) | `RecipeMade` entity (already exists), `UserProfile` dietary prefs | Phase 10 |
| 9 | AiChat "Edit anyway" hardening (`RawRecipeEditorDialog`) | Nothing (UI only) | Phase 10 |
| 10 | Prompt snapshot test | `PromptBuilderService` stable (unchanged at this point) | Phase 10 |
| 11 | Accent picker UI, moon glyph, TopBar RightSlot service, Home live tick | Nothing (UI / JS) | Phase 10 |
| 12 | Cookbook reparenting, pantry quick-add, Profile AI prompt editor | `RecipeService.UpdateAsync` ext for reparent; `GroceryListService.AddItemAsync` | Phase 10 |

**Foundation phases (8–9) must ship before consumer phases (10).** Within each phase, items within the same phase can be parallelized at the plan level.

---

## Cross-Cutting Concerns

### DatabaseSeeder touch points

`DatabaseSeeder.SeedAsync` must be touched for:
1. Plaintext-key re-encryption (Bucket 5 encrypt-at-rest) — detect and re-protect existing keys.
2. Canonical-doc null assertion (Bucket 2 LegacyRecipeProjector deletion guard) — throw if any `Recipe.CanonicalDocumentJson` is null.
3. Schema v3 backfill — `RecipeUpcasterChain.UpcastToCurrent` is already called at startup for any v1/v2 doc in the DB column; no explicit seeder change needed beyond bumping `CurrentVersion`.

### Program.cs registration touch points

`Program.cs` must be touched for:
1. `UseStaticFiles` for `/uploads` (Bucket 1 file storage).
2. `AddDataProtection().PersistKeysToFileSystem(...)` (Bucket 5 encrypt-at-rest).
3. `AddScoped<IRecipePhotoStorage, LocalRecipePhotoStorage>()`.
4. `AddScoped<IPantryMatchService, PantryMatchService>()`.
5. `AddScoped<ICbTopBarService, CbTopBarService>()`.

### AI-off contract compliance

Every new AI surface must check both `CookBotSettings.AiFeaturesEnabled` and `UserProfile.AiEnabled`. The telemetry widget on Profile does not require these checks — it shows historical data, not live AI calls. The `PantryMatchService` is not an AI surface. The `RawRecipeEditorDialog` is not a new AI surface (it processes an already-returned AI response). No new AI gates are required for Buckets 2, 3 (non-AI QOL), 4, or the Docker/telemetry parts of Bucket 5.

### Canonical-first invariant compliance

New code must never read from `Recipe.IngredientsJson`, `Recipe.StepsJson`, `Recipe.IngredientRefs`, or `Recipe.TagsJson` (the last of which is being deleted in Bucket 2). After the `RecipeTag` migration, `Tags` reads from the `RecipeTag` table. After `LegacyRecipeProjector` deletion, `RecipeService` builds `RecipeDocument` from `ParsedRecipe` (already canonical format) rather than from entity navigation.

---

## Anti-Patterns to Avoid

### Anti-Pattern: Putting upload write in Application layer

**What people do:** Define `IRecipePhotoStorage` in Application and implement it there using `System.IO.File`.
**Why it's wrong:** Application must remain free of I/O framework concerns. More critically, the Blazor `IBrowserFile` abstraction (which provides the stream) is a `Microsoft.AspNetCore.Components.Forms` type — unavailable in Application. The write must happen in Web layer code that has access to Blazor's file input result before converting to a plain `Stream`.
**Do this instead:** Interface in Domain (pure contract), implementation in `CookBot.Web/Services/`, registration in `Program.cs`. Pages call the interface.

### Anti-Pattern: Encrypting the shared-key copy

**What people do:** Store an encrypted copy of the key in the `AiApiKeyShares` table so each recipient's key survives the owner removing their key.
**Why it's wrong:** This changes the trust model — now the DB has N copies of the key, each potentially rotatable independently, and a revoked share does not revoke access to the already-stored ciphertext.
**Do this instead:** `AiApiKeyShares` contains only user-ID references. Encryption sits on `UserProfile.AiApiKey`. Resolution always reads the owner's encrypted key live — a revoked share or deleted key is immediately effective.

### Anti-Pattern: Registering DataProtector as Singleton with Scoped consumers

**What people do:** `services.AddSingleton<IDataProtector>(sp => sp.GetRequiredService<IDataProtectionProvider>().CreateProtector("AiApiKey"))`.
**Why it's wrong:** `IDataProtector` itself is fine as singleton. However `AiApiKeyResolutionService` is `Scoped` and `EditProfile.razor` is a Blazor component (circuit-scoped). Inject `IDataProtectionProvider` and call `CreateProtector` once per service instance — `IDataProtectionProvider` is registered Singleton by `AddDataProtection()`.
**Do this instead:** Inject `IDataProtectionProvider` into `AiApiKeyResolutionService` and call `.CreateProtector("CookBot.AiApiKey")` in the constructor.

### Anti-Pattern: MapStaticAssets for runtime uploads

**What people do:** Write uploaded files to `wwwroot/uploads/` and expect `MapStaticAssets()` to serve them.
**Why it's wrong:** `MapStaticAssets` fingerprints assets at **build time**. Files written at runtime are invisible to it and return 404 (confirmed by ASP.NET Core 10 official docs and community reports).
**Do this instead:** Add a `UseStaticFiles(new StaticFileOptions { FileProvider = new PhysicalFileProvider(...), RequestPath = "/uploads" })` call before `MapStaticAssets()`.

### Anti-Pattern: Inline token counting in the Razor page

**What people do:** Parse the Anthropic response JSON in `AiChat.razor` to extract `usage.input_tokens`.
**Why it's wrong:** The SSE events are already consumed and discarded by `AnthropicAiService` before `AiChat` sees the result. `AiChat` only receives the final `StructuredResult<T>`.
**Do this instead:** Extend `StructuredResult<T>` with `InputTokens` and `OutputTokens` fields populated inside `SendStructuredAsync` by parsing `message_start` and `message_delta` SSE events. Bubble up through `AiRecipeGenerator.GenerateAsync` to the caller.

---

## New vs. Modified Files — Complete List

### New files

```
src/CookBot.Domain/Entities/RecipeTag.cs
src/CookBot.Domain/Entities/AiUsageLog.cs
src/CookBot.Domain/Interfaces/IRecipePhotoStorage.cs
src/CookBot.Application/Recipes/Migration_V2_To_V3.cs
src/CookBot.Application/Recipes/RecipePhotoUrlValidator.cs
src/CookBot.Application/Services/IPantryMatchService.cs
src/CookBot.Application/Services/PantryMatchService.cs
src/CookBot.Infrastructure/Data/Configurations/RecipeTagConfiguration.cs
src/CookBot.Infrastructure/Data/Configurations/AiUsageLogConfiguration.cs
src/CookBot.Infrastructure/Migrations/{timestamp}_AddRecipePhotoUrl.cs
src/CookBot.Infrastructure/Migrations/{timestamp}_AddRecipeTagTable.cs
src/CookBot.Infrastructure/Migrations/{timestamp}_AddAiUsageLog.cs
src/CookBot.Web/Services/LocalRecipePhotoStorage.cs
src/CookBot.Web/Services/CbTopBarService.cs
src/CookBot.Web/Components/Pages/RawRecipeEditorDialog.razor
docker/Dockerfile
docker/docker-compose.yml
docker/.env.example
tests/CookBot.Tests/Snapshots/BuildSystemPrompt_DefaultProfile.txt
```

### Modified files

```
src/CookBot.Domain/Recipes/RecipeDocument.cs          — add PhotoUrl, Description
src/CookBot.Domain/Recipes/StepNode.cs                — add Temperature to ContentStep
src/CookBot.Application/Recipes/RecipeUpcasterChain.cs — bump CurrentVersion = 3
src/CookBot.Application/DependencyInjection.cs         — register Migration_V2_To_V3, IPantryMatchService
src/CookBot.Application/Services/PromptBuilderService.cs — lint denylist additions
src/CookBot.Application/Services/RecipeService.cs      — remove IRecipeProjector; add newCookbookId; add GroceryListService.AddItemAsync
src/CookBot.Application/Services/GroceryListService.cs — add AddItemAsync
src/CookBot.Application/AI/AiRecipeGenerator.cs        — surface InputTokens/OutputTokens
src/CookBot.Infrastructure/DependencyInjection.cs      — remove LegacyRecipeProjector registrations
src/CookBot.Infrastructure/Data/CookBotDbContext.cs    — add RecipeTags, AiUsageLogs DbSets
src/CookBot.Infrastructure/Data/DatabaseSeeder.cs      — null-canonical guard, key re-encryption
src/CookBot.Infrastructure/AI/AnthropicAiService.cs    — parse usage SSE events, extend StructuredResult
src/CookBot.Web/Program.cs                             — UseStaticFiles, AddDataProtection, new registrations
src/CookBot.Web/Services/AiApiKeyResolutionService.cs  — IDataProtector.Unprotect on read
src/CookBot.Web/Components/Pages/AiChat.razor          — write AiUsageLog, open RawRecipeEditorDialog
src/CookBot.Web/Components/Pages/RecipeEditor.razor    — PhotoUrl input, cookbook picker
src/CookBot.Web/Components/Pages/EditProfile.razor     — accent picker, AI prompt editor, telemetry widget
src/CookBot.Web/Components/Pages/PantryView.razor      — quick-add button
src/CookBot.Web/Components/Pages/Home.razor.cs         — inject IPantryMatchService, JS tick call
src/CookBot.Web/Components/Layout/MainLayout.razor     — ICbTopBarService integration
src/CookBot.Web/Components/Layout/TopBar.razor         — moon glyph conditional
src/CookBot.Web/Components/Atoms/Icon.razor            — Moon icon constant + SVG path
src/CookBot.Web/wwwroot/js/cookbot-shell.js            — persist cookbot_accent to localStorage
src/CookBot.Web/wwwroot/js/cooking-timers.js           — startHomeTick helper
tests/CookBot.Tests/Services/PromptBuilderServiceTests.cs — snapshot test
```

### Deleted files

```
src/CookBot.Infrastructure/Data/Migrations/Helpers/LegacyRecipeProjector.cs
src/CookBot.Application/Recipes/IRecipeProjector.cs
```

---

## Sources

- ASP.NET Core 10 Static Files docs — `MapStaticAssets` vs `UseStaticFiles` for runtime-uploaded files: [https://learn.microsoft.com/en-us/aspnet/core/fundamentals/static-files?view=aspnetcore-10.0](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/static-files?view=aspnetcore-10.0)
- Direct source inspection: all findings above derived from reading the live codebase files at `src/` (HIGH confidence — no inference from training data)

---

*Architecture research for: FreelovesCookBot v1.3 Production-Ready & Format Maturity*
*Researched: 2026-05-15*
