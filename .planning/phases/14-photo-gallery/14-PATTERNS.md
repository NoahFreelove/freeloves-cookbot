# Phase 14: Photo Gallery — Pattern Map

**Mapped:** 2026-06-07
**Files analyzed:** 13 new/modified files
**Analogs found:** 13 / 13

---

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|-------------------|------|-----------|----------------|---------------|
| `src/CookBot.Domain/Entities/RecipePhoto.cs` | model | CRUD | `src/CookBot.Domain/Entities/RecipeIngredient.cs` | exact |
| `src/CookBot.Infrastructure/Data/Configurations/RecipePhotoConfiguration.cs` | config | CRUD | `src/CookBot.Infrastructure/Data/Configurations/RecipeIngredientConfiguration.cs` + `RecipeConfiguration.cs` line 29 | exact |
| `src/CookBot.Infrastructure/Migrations/{ts}_AddRecipePhotosTable.cs` | migration | batch | `src/CookBot.Infrastructure/Migrations/20260516034336_AddRecipeTagTable.cs` | exact |
| `src/CookBot.Infrastructure/Data/CookBotDbContext.cs` (MODIFIED) | config | CRUD | self — add `DbSet<RecipePhoto>` following existing `DbSet<RecipeTag>` pattern | exact |
| `src/CookBot.Application/Services/RecipePhotoService.cs` | service | CRUD | `src/CookBot.Application/Services/RecipeService.cs` | exact |
| `src/CookBot.Application/Services/PhotoUrlHeadValidator.cs` | service | request-response | `src/CookBot.Application/Services/RecipePhotoUrlValidator.cs` + `src/CookBot.Infrastructure/AI/AnthropicAiService.cs` HttpClient pattern | role-match |
| `src/CookBot.Application/DTOs/CookBotSettings.cs` (MODIFIED) | config | — | self — `DatabaseBackupRetention` field lines 25-26 | exact |
| `src/CookBot.Web/Services/LocalRecipePhotoStorage.cs` (MODIFIED) | service | file-I/O | self — `AssertPathInsideUploadsDirectory` lines 118-134 | exact |
| `src/CookBot.Application/Services/RecipeService.cs` (MODIFIED) | service | CRUD | self — `DeleteAsync` lines 268-280, `CreateAsync`/`UpdateAsync` PhotoUrl write | exact |
| `src/CookBot.Domain/Entities/Recipe.cs` (MODIFIED) | model | CRUD | self — existing nav collections (`RecipeIngredients`, `Tags`) lines 24-26 | exact |
| `src/CookBot.Web/Components/Pages/RecipeView.razor` (MODIFIED) | component | request-response | self — hero block lines 133-150 | exact |
| `src/CookBot.Web/Components/Pages/RecipeEditorParts/RecipePhotoGalleryManager.razor` (NEW) | component | request-response | `src/CookBot.Web/Components/Pages/RecipeEditorParts/RecipePhotoComposite.razor` | exact |
| `src/CookBot.Application/DependencyInjection.cs` + `Program.cs` (MODIFIED) | config | — | self — existing service registrations | exact |

---

## Pattern Assignments

### `src/CookBot.Domain/Entities/RecipePhoto.cs` (model, CRUD)

**Analog:** `src/CookBot.Domain/Entities/RecipeIngredient.cs`

**Entity shape pattern** (full file, 15 lines):
```csharp
namespace CookBot.Domain.Entities;

public class RecipeIngredient
{
    public int Id { get; set; }
    public int RecipeId { get; set; }
    public int IngredientId { get; set; }
    public int RecipeLocalId { get; set; }
    public double Amount { get; set; }
    public string Unit { get; set; } = string.Empty;
    public string? Note { get; set; }

    public Recipe Recipe { get; set; } = null!;
    public Ingredient Ingredient { get; set; } = null!;
}
```

**Target shape** (per D-14-03):
- `int Id` — PK
- `int RecipeId` — FK
- `string Url` — required, max 2048 (matches `Recipe.PhotoUrl` configured at `RecipeConfiguration.cs:19`)
- `string? Caption` — nullable, max 512
- `int SortOrder` — default 0
- `bool IsPrimary` — default false
- `Recipe Recipe { get; set; } = null!;` — nav back-ref (same `null!` convention as `RecipeIngredient.Recipe`)

**Note on Recipe navigation:** Add `ICollection<RecipePhoto> Photos { get; set; } = new List<RecipePhoto>();` to `Recipe.cs` alongside the existing `ICollection<RecipeIngredient> RecipeIngredients` at line 25.

---

### `src/CookBot.Infrastructure/Data/Configurations/RecipePhotoConfiguration.cs` (config, CRUD)

**Analog:** `src/CookBot.Infrastructure/Data/Configurations/RecipeIngredientConfiguration.cs` (full file, 24 lines) + `RecipeConfiguration.cs` line 29

**Full RecipeIngredientConfiguration pattern:**
```csharp
public class RecipeIngredientConfiguration : IEntityTypeConfiguration<RecipeIngredient>
{
    public void Configure(EntityTypeBuilder<RecipeIngredient> builder)
    {
        builder.HasKey(ri => ri.Id);

        // Composite index for the pantry-match join
        builder.HasIndex(ri => new { ri.RecipeId, ri.IngredientId });

        builder.Property(ri => ri.Unit)
            .HasMaxLength(50)
            .IsRequired();

        builder.HasOne(ri => ri.Ingredient).WithMany(i => i.RecipeIngredients).HasForeignKey(ri => ri.IngredientId).OnDelete(DeleteBehavior.Restrict);
    }
}
```

**HasMany FK cascade pattern from `RecipeConfiguration.cs:29`:**
```csharp
builder.HasMany(r => r.RecipeIngredients).WithOne(ri => ri.Recipe).HasForeignKey(ri => ri.RecipeId).OnDelete(DeleteBehavior.Cascade);
```

**RecipePhotoConfiguration must use:**
- `HasKey(p => p.Id)`
- `Property(p => p.Url).HasMaxLength(2048).IsRequired()` — max-length matches `RecipeConfiguration.cs:19`
- `Property(p => p.Caption).HasMaxLength(512)` — nullable, no `IsRequired()`
- `Property(p => p.SortOrder).HasDefaultValue(0)`
- `Property(p => p.IsPrimary).HasDefaultValue(false)`
- `HasIndex(p => new { p.RecipeId, p.SortOrder })` — composite index for `GetPhotosAsync(recipeId)` ordered by `SortOrder`; mirrors `RecipeIngredientConfiguration.cs:16`
- `HasOne(p => p.Recipe).WithMany(r => r.Photos).HasForeignKey(p => p.RecipeId).OnDelete(DeleteBehavior.Cascade)` — configures the FK here (not on `RecipeConfiguration`) so `RecipeConfiguration.cs` is unmodified

`ApplyConfigurationsFromAssembly` in `CookBotDbContext.OnModelCreating` (`CookBotDbContext.cs:41`) will auto-discover the new configuration — no change needed to `CookBotDbContext.OnModelCreating`.

---

### `src/CookBot.Infrastructure/Migrations/{ts}_AddRecipePhotosTable.cs` (migration, batch)

**Analog:** `src/CookBot.Infrastructure/Migrations/20260516034336_AddRecipeTagTable.cs` (full file, 60 lines)

**CreateTable + FK + index + backfill SQL pattern:**
```csharp
protected override void Up(MigrationBuilder migrationBuilder)
{
    migrationBuilder.CreateTable(
        name: "RecipeTags",
        columns: table => new
        {
            Id = table.Column<int>(type: "INTEGER", nullable: false)
                .Annotation("Sqlite:Autoincrement", true),
            RecipeId = table.Column<int>(type: "INTEGER", nullable: false),
            Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false)
        },
        constraints: table =>
        {
            table.PrimaryKey("PK_RecipeTags", x => x.Id);
            table.ForeignKey(
                name: "FK_RecipeTags_Recipes_RecipeId",
                column: x => x.RecipeId,
                principalTable: "Recipes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        });

    migrationBuilder.CreateIndex(
        name: "IX_RecipeTags_RecipeId_Name",
        table: "RecipeTags",
        columns: new[] { "RecipeId", "Name" },
        unique: true);

    // Backfill with raw SQL — runs atomically inside MigrateAsync()
    migrationBuilder.Sql(@"
        INSERT INTO RecipeTags (RecipeId, Name)
        SELECT r.Id, TRIM(json_each.value)
        FROM Recipes r, json_each(r.TagsJson)
        WHERE TRIM(json_each.value) <> ''
        ON CONFLICT DO NOTHING;
    ");
}

protected override void Down(MigrationBuilder migrationBuilder)
{
    migrationBuilder.DropTable(name: "RecipeTags");
}
```

**RecipePhotos migration must add these columns** (five columns — Id, RecipeId, Url, Caption, SortOrder, IsPrimary) and this **GALLERY-01 backfill SQL** (per RESEARCH.md §Pattern 3):
```sql
INSERT INTO RecipePhotos (RecipeId, Url, SortOrder, IsPrimary)
SELECT Id, PhotoUrl, 0, 1
FROM Recipes
WHERE PhotoUrl IS NOT NULL AND PhotoUrl != ''
```

SQLite column type keyword is `"INTEGER"` for `int`, `"TEXT"` for `string`, `"INTEGER"` for `bool` (SQLite stores bool as 0/1). Annotation for autoincrement: `.Annotation("Sqlite:Autoincrement", true)`. `Down()` drops the table.

---

### `src/CookBot.Infrastructure/Data/CookBotDbContext.cs` (MODIFIED)

**Pattern:** Add `DbSet<RecipePhoto>` following the `RecipeTag` / `AiUsageLog` pattern at lines 32-34:
```csharp
// Existing pattern at lines 31-34:
public DbSet<RecipeTag> RecipeTags => Set<RecipeTag>();
public DbSet<AiUsageLog> AiUsageLogs => Set<AiUsageLog>();
```
Add immediately after `RecipeTags`:
```csharp
public DbSet<RecipePhoto> RecipePhotos => Set<RecipePhoto>();
```
No change to `OnModelCreating` — `ApplyConfigurationsFromAssembly` discovers `RecipePhotoConfiguration` automatically.

---

### `src/CookBot.Application/Services/RecipePhotoService.cs` (service, CRUD)

**Analog:** `src/CookBot.Application/Services/RecipeService.cs`

**Constructor injection pattern** (lines 17-31):
```csharp
public RecipeService(
    IRecipeFormatParser parser,
    IRepository<Recipe> recipeRepo,
    IRepository<Ingredient> ingredientRepo,
    IRepository<Cookbook> cookbookRepo,
    IRepository<RecipeTag> recipeTagRepo,
    JsonRecipeSerializer canonicalSerializer)
{
    _parser = parser;
    _recipeRepo = recipeRepo;
    // ...
}
```

**Ownership check pattern** (lines 35-39, 154-158) — copy verbatim before any mutation:
```csharp
var cookbook = await _cookbookRepo.GetByIdAsync(recipe.CookbookId)
    ?? throw new InvalidOperationException("Cookbook not found.");
if (cookbook.UserId != userId)
    throw new UnauthorizedAccessException("You do not own this cookbook.");
```

**`RecipePhotoService` takes `CookBotDbContext` directly** (same as `RecipeEditor.razor:11`) for the `_db.RecipePhotos` queries that need `Include`, `OrderBy`, and `Where` — the generic `IRepository<T>` does not expose `Include`. Inject `CookBotDbContext` alongside `IRepository<Cookbook>` (for ownership check) and `RecipeService` (for `SyncPrimaryPhotoUrlAsync` calls per D-14-01).

**Method signatures** (per RESEARCH.md §Pattern 4):
```csharp
public class RecipePhotoService
{
    Task<IReadOnlyList<RecipePhoto>> GetPhotosAsync(int recipeId, int userId);
    Task<RecipePhoto> AddPhotoAsync(int recipeId, string url, int userId, string? caption = null);
    Task SetPrimaryAsync(int recipeId, int photoId, int userId);
    Task ReorderAsync(int recipeId, int[] orderedPhotoIds, int userId);
    Task DeleteAsync(int recipeId, int photoId, int userId);
    Task UpdateCaptionAsync(int recipeId, int photoId, string? caption, int userId);
}
```

**IsPrimary invariant:** When setting a new primary, execute a single bulk update to clear all flags for the recipe before setting the new one — prevents the two-primary pitfall (RESEARCH.md §Pitfall 3):
```csharp
// Clear all IsPrimary for this recipe, then set the target
await _db.RecipePhotos
    .Where(p => p.RecipeId == recipeId)
    .ExecuteUpdateAsync(s => s.SetProperty(p => p.IsPrimary, false));
photo.IsPrimary = true;
await _db.SaveChangesAsync();
```

**Server-side cap enforcement** (D-14-04-cap) in `AddPhotoAsync`:
```csharp
var count = await _db.RecipePhotos.CountAsync(p => p.RecipeId == recipeId);
var max = _settings.MaxPhotosPerRecipe;  // IOptions<CookBotSettings>
if (count >= max)
    throw new InvalidOperationException($"Maximum {max} photos per recipe.");
```

---

### `src/CookBot.Application/Services/PhotoUrlHeadValidator.cs` (service, request-response)

**Analog 1 (scheme check):** `src/CookBot.Application/Services/RecipePhotoUrlValidator.cs`

**`TryValidate` envelope pattern** (lines 34-83) — step 1 reuse:
```csharp
public bool TryValidate(string? input, out string? normalized, out string? errorCode)
{
    if (string.IsNullOrWhiteSpace(input)) { normalized = null; errorCode = null; return true; }
    // ... protocol-relative, path-only, Uri.TryCreate, scheme check ...
    if (uri.Scheme is not ("http" or "https")) { normalized = null; errorCode = "SCHEME_NOT_ALLOWED"; return false; }
    normalized = uri.AbsoluteUri;
    errorCode = null;
    return true;
}
```

**Analog 2 (HttpClient pattern):** `src/CookBot.Infrastructure/AI/AnthropicAiService.cs` lines 51-61

```csharp
protected virtual HttpClient CreateHttpClient(string? apiKey)
{
    // ...
    var http = new HttpClient();
    http.DefaultRequestHeaders.Add("x-api-key", key);
    http.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
    return http;
}
```

**`PhotoUrlHeadValidator` uses the same per-call `new HttpClient()` pattern** (no IHttpClientFactory in this codebase) with `AllowAutoRedirect = false` and a 5-second timeout per D-14-10:
```csharp
using var http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false });
http.Timeout = TimeSpan.FromSeconds(5);
```

**Result type convention** — matches the `RecipePhotoUrlValidator` never-throw tri-out envelope:
```csharp
public record PhotoUrlValidationResult(bool IsValid, string? ErrorMessage)
{
    public static PhotoUrlValidationResult Valid => new(true, null);
    public static PhotoUrlValidationResult Timeout => new(false, "URL validation timed out — check the URL and try again.");
    // ... other factory methods per RESEARCH.md §Pattern 2
}
```

`PhotoUrlHeadValidator` is stateless (no DI dependencies beyond `ILogger`) → register as `Singleton` in `DependencyInjection.cs`, following `RecipePhotoUrlValidator` at line 27.

---

### `src/CookBot.Application/DTOs/CookBotSettings.cs` (MODIFIED, config)

**Analog:** existing `DatabaseBackupRetention` field, lines 25-26:
```csharp
/// <summary>
/// Maximum number of `.pre-*.bak` files to retain alongside the SQLite DB. Default 3 (D-15).
/// Effective range: clamped to [1, 10] at runtime by `DatabaseBackupService`.
/// </summary>
public int DatabaseBackupRetention { get; set; } = 3;
```

**Add immediately after `DatabaseBackupRetention`** (D-14-04-cap):
```csharp
/// <summary>
/// Maximum number of photos per recipe. Default 10 (D-14-04-cap).
/// Effective range: clamped to [1, 20] at runtime by <c>RecipePhotoService</c>.
/// </summary>
public int MaxPhotosPerRecipe { get; set; } = 10;
```

Note the clamping is done in the consuming service (same as `DatabaseBackupRetention` is clamped by `DatabaseBackupService`), not in the DTO getter.

---

### `src/CookBot.Web/Services/LocalRecipePhotoStorage.cs` (MODIFIED, service, file-I/O)

**Analog:** `AssertPathInsideUploadsDirectory` lines 118-134 (full method already read above) — the new `DeletePhysicalFile` method calls into it.

**Add this method** after `AssertPathInsideUploadsDirectory` (D-14-11):
```csharp
/// <summary>
/// Deletes the physical file for a local <c>/uploads/{guid}.ext</c> URL.
/// No-op if the file does not exist (missing-file deletes are non-fatal — log and continue).
/// Throws <see cref="InvalidOperationException"/> on path-traversal attempt (PITFALL H2).
/// </summary>
public void DeletePhysicalFile(string url)
{
    // url is "/uploads/{guid}.ext" — extract the filename only
    var fileName = Path.GetFileName(url);
    var fullPath = Path.Combine(_uploadsDir, fileName);
    AssertPathInsideUploadsDirectory(fullPath);  // PITFALL H2 guard
    if (File.Exists(fullPath))
    {
        File.Delete(fullPath);
        _logger.LogInformation("Deleted photo file {FileName}", fileName);
    }
    // Missing file is intentionally non-fatal
}
```

`_uploadsDir` and `_logger` are already the private fields at lines 42-43. `AssertPathInsideUploadsDirectory` is already `public` at line 118 so `DeletePhysicalFile` (internal service method) and test suite can both call it.

---

### `src/CookBot.Application/Services/RecipeService.cs` (MODIFIED)

**Analog:** self — `DeleteAsync` lines 268-280 (the current gap), `CreateAsync` PhotoUrl write lines 48-55, `UpdateAsync` PhotoUrl write lines 172-183.

**Current `DeleteAsync` (lines 268-280) — the gap:**
```csharp
public async Task DeleteAsync(int recipeId, int userId)
{
    var recipe = await _recipeRepo.GetByIdAsync(recipeId)
        ?? throw new InvalidOperationException("Recipe not found.");
    var cookbook = await _cookbookRepo.GetByIdAsync(recipe.CookbookId)
        ?? throw new InvalidOperationException("Cookbook not found.");
    if (cookbook.UserId != userId)
        throw new UnauthorizedAccessException("You do not own this cookbook.");
    await _recipeRepo.DeleteAsync(recipe);
}
```

**Modified `DeleteAsync`** must add photo file cleanup before cascade (D-14-11). Because `IRepository<T>` does not expose `Include`, load photos directly from `DbContext` (inject via constructor). Sequence: load photos → delete local files (non-fatal) → call `DeleteAsync` on recipe (cascade drops rows):
```csharp
// NEW: enumerate local-path photos BEFORE cascade deletes the rows
var photos = await _db.RecipePhotos
    .Where(p => p.RecipeId == recipeId)
    .AsNoTracking()
    .ToListAsync();
foreach (var photo in photos)
{
    if (photo.Url.StartsWith("/uploads/", StringComparison.Ordinal))
    {
        try { _photoStorage.DeletePhysicalFile(photo.Url); }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not delete photo file {Url} during recipe delete", photo.Url);
        }
    }
}
await _recipeRepo.DeleteAsync(recipe);  // EF cascade removes RecipePhoto rows
```

**New `SyncPrimaryPhotoUrlAsync` helper** — called after every gallery mutation per D-14-01. This is the only place that writes `Recipe.PhotoUrl` and `CanonicalDocumentJson` for the gallery sync (P15 — canonical writes stay in `RecipeService`):
```csharp
// Pattern matches the existing PhotoUrl write in CreateAsync (lines 54, 115) and UpdateAsync (lines 182, 246):
//   recipe.PhotoUrl = parsed.PhotoUrl;
//   canonicalDoc = new RecipeDocument { ..., PhotoUrl = parsed.PhotoUrl, ... };
//   recipe.CanonicalDocumentJson = _canonicalSerializer.Serialize(canonicalDoc);

internal async Task SyncPrimaryPhotoUrlAsync(int recipeId)
{
    // Load recipe (existing pattern: _recipeRepo.GetByIdAsync)
    var recipe = await _recipeRepo.GetByIdAsync(recipeId)
        ?? throw new InvalidOperationException("Recipe not found during photo sync.");
    var primary = await _db.RecipePhotos
        .Where(p => p.RecipeId == recipeId && p.IsPrimary)
        .OrderBy(p => p.SortOrder)
        .FirstOrDefaultAsync()
        ?? await _db.RecipePhotos
            .Where(p => p.RecipeId == recipeId)
            .OrderBy(p => p.SortOrder)
            .FirstOrDefaultAsync();
    recipe.PhotoUrl = primary?.Url;
    var doc = _canonicalSerializer.Deserialize(recipe.CanonicalDocumentJson);
    recipe.CanonicalDocumentJson = _canonicalSerializer.Serialize(doc with { PhotoUrl = recipe.PhotoUrl });
    await _recipeRepo.UpdateAsync(recipe);
}
```

`RecipeService` must receive `CookBotDbContext` and `LocalRecipePhotoStorage` as new constructor dependencies (both already registered in `Program.cs` as Scoped).

---

### `src/CookBot.Domain/Entities/Recipe.cs` (MODIFIED)

**Analog:** existing nav collection declarations lines 24-26:
```csharp
public Cookbook Cookbook { get; set; } = null!;
public List<RecipeStep> Steps { get; set; } = new();
public ICollection<RecipeIngredient> RecipeIngredients { get; set; } = new List<RecipeIngredient>();
public ICollection<RecipeTag> Tags { get; set; } = new List<RecipeTag>();
```

**Add after `Tags`:**
```csharp
public ICollection<RecipePhoto> Photos { get; set; } = new List<RecipePhoto>();
```

Convention: `ICollection<T>` initialized to `new List<T>()`. Same pattern as `RecipeIngredients` and `Tags`.

---

### `src/CookBot.Web/Components/Pages/RecipeView.razor` (MODIFIED, component, request-response)

**Analog:** hero block lines 133-150 (already read above in full).

**Hero `<img>` hardening pattern to extend** (lines 138-150):
```razor
@if (!string.IsNullOrEmpty(_doc.PhotoUrl) && !_heroPhotoFailed)
{
    <img src="@_doc.PhotoUrl"
         alt="@($"{_doc.Name} hero photo")"
         referrerpolicy="no-referrer"
         loading="lazy"
         @onerror="HandleHeroPhotoError"
         style="width:100%;height:420px;object-fit:cover;border-radius:10px;display:block;" />
}
else
{
    <StripedPlaceholder Width="100%" Height="420" Label="hero photo · 4:3" />
}
```

**Gallery extension pattern** — replace `_doc.PhotoUrl` with `_displayedPhoto?.Url` (the currently hero-swapped photo), use `HashSet<int> _failedPhotoIds` instead of single `_heroPhotoFailed`, and add the thumbnail strip (per UI-SPEC.md §Surface 1):

```razor
@* Thumbnail strip — only when > 1 photo *@
@if (_photos.Count > 1)
{
    <div class="recipe-gallery-strip">
        @for (int i = 0; i < _photos.Count; i++)
        {
            var photo = _photos[i];
            var idx = i;
            if (_failedPhotoIds.Contains(photo.Id)) continue;
            <img src="@photo.Url"
                 alt="@($"{_doc.Name} photo {idx + 1}")"
                 referrerpolicy="no-referrer"
                 loading="lazy"
                 @onerror="@(() => { _failedPhotoIds.Add(photo.Id); StateHasChanged(); })"
                 tabindex="0"
                 role="button"
                 aria-label="View @(_doc.Name) photo @(idx + 1)"
                 aria-pressed="@(photo.Id == _displayedPhotoId)"
                 style="height:72px;width:auto;aspect-ratio:4/3;object-fit:cover;border-radius:6px;cursor:pointer;@(photo.Id == _displayedPhotoId ? "outline:2px solid var(--accent);outline-offset:2px;" : "")"
                 @onclick="@(() => SwapHero(photo.Id))"
                 @onkeydown="@((e) => { if (e.Key is "Enter" or " ") SwapHero(photo.Id); })" />
        }
    </div>
}
```

Caption display (per UI-SPEC.md §Caption): render below hero if `_displayedPhoto?.Caption` is non-null/non-empty, at `font-size:14px;color:var(--ink-3);margin-top:4px;font-style:italic;`.

New component-level state: `List<RecipePhoto> _photos`, `int _displayedPhotoId`, `HashSet<int> _failedPhotoIds = new()`. Load `_photos` via `RecipePhotoService.GetPhotosAsync` in `OnAfterRenderAsync` where the rest of the recipe loads.

---

### `src/CookBot.Web/Components/Pages/RecipeEditorParts/RecipePhotoGalleryManager.razor` (NEW, component, request-response)

**Analog:** `src/CookBot.Web/Components/Pages/RecipeEditorParts/RecipePhotoComposite.razor` (full file, 222 lines)

**Namespace, inject, and parameter pattern** (lines 30-38):
```razor
@namespace CookBot.Web.Components.Pages.RecipeEditorParts
@using CookBot.Application.Services
@using CookBot.Web.Components.Atoms
@using CookBot.Web.Services
@using Microsoft.AspNetCore.Components.Forms
@inject RecipePhotoUrlValidator UrlValidator
@inject LocalRecipePhotoStorage PhotoStorage
@inject ICbToastService Toast
```

**New component additionally injects:** `RecipePhotoService`, `PhotoUrlHeadValidator`, `IAiService`, `AiApiKeyResolutionService`, `IOptions<CookBotSettings>`.

**Parameters:** `[Parameter] public int RecipeId { get; set; }` and `[Parameter] public int UserId { get; set; }` (immediate-persist model per RESEARCH.md §Open Questions #3 — no batch-with-save staging).

**Pre-stream size check pattern** (line 181) — reuse verbatim in the multi-file loop:
```csharp
if (file.Size > 10L * 1024L * 1024L)
{
    Toast.Show("File too large — 10 MB max.", CbToastSeverity.Warning);
    return;  // in multi-file loop: continue to next file
}
```

**Upload+catch pattern** (lines 186-203) — per-file try/catch in the `foreach` loop:
```csharp
try
{
    var url = await PhotoStorage.SaveAsync(file, default);
    // ...
    await PhotoUrlChanged.InvokeAsync(url);
}
catch (InvalidImageException ex)
{
    Toast.Show($"Image rejected: {ex.Message} — JPEG, PNG, GIF, WebP only.", CbToastSeverity.Warning);
}
catch (Exception ex)
{
    Toast.Show($"Upload failed: {ex.Message}", CbToastSeverity.Error);
}
```

**URL validation pattern** (lines 140-168) — reuse `RecipePhotoUrlValidator.TryValidate` as step 1, then add async HEAD validation as step 2:
```csharp
if (UrlValidator.TryValidate(value, out var normalized, out var errorCode))
{
    // Step 2: HEAD validation (new for Phase 14)
    _urlValidating = true;
    StateHasChanged();
    var result = await HeadValidator.ValidateAsync(normalized!);
    _urlValidating = false;
    if (!result.IsValid) { _urlError = result.ErrorMessage; return; }
    // accept...
}
else
{
    _urlError = errorCode switch { "SCHEME_NOT_ALLOWED" => "...", "MALFORMED" => "...", _ => "..." };
}
```

**Error display pattern** (lines 68-73):
```razor
@if (_urlError is not null)
{
    <div role="alert" style="color:var(--warn);font-size:12px;line-height:1.4;">
        @_urlError
    </div>
}
```

**Disclaimer pattern** (lines 91-94) — reuse verbatim, update text per D-14-09:
```razor
<div style="font-size:11.5px;color:var(--ink-3);line-height:1.4;">
    Only add photos you have the right to use.
    AI suggestions are search terms only — verify the license at the source.
</div>
```

**Upload label pattern** (lines 75-82) — evolve from single-file to multi-file:
```razor
<label class="cb-btn ghost"
       style="cursor:pointer;display:inline-flex;align-items:center;gap:6px;font-size:13px;padding:6px 12px;margin:0;">
    <Icon Name="@Icon.Names.Plus" Size="14" /> Or upload file
    <InputFile OnChange="OnFilePicked"
               accept="image/jpeg,image/png,image/gif,image/webp"
               style="display:none;" />
</label>
```

Multi-file: change `OnChange="OnFilePicked"` to `OnChange="OnMultipleFilesPicked"` and add `multiple` attribute. Accept attribute stays identical.

**AI gate pattern** (`RecipeEditor.razor:519-521`) — reuse verbatim:
```csharp
var hostOn = CookBotSettingsOptions.Value.AiFeaturesEnabled;
var userOn = user?.Profile?.AiEnabled ?? false;
_aiOn = hostOn && userOn;
```

**`IAiService.SendMessageAsync` transport** (`IAiService.cs:14`):
```csharp
Task<string> SendMessageAsync(string systemPrompt, List<AiMessage> messages, string? apiKey = null, string? modelId = null, int maxTokens = 4096);
```

**RecipeEditor integration:** Replace the `<RecipePhotoComposite>` usage at `RecipeEditor.razor:85-86` with `<RecipePhotoGalleryManager RecipeId="@_recipeId" UserId="@_userId" />`. The old `_photoUrl` / `PhotoUrlChanged` binding is dropped — the gallery manager operates on live `RecipePhoto` rows directly (immediate-persist model per RESEARCH.md §Open Questions #3). `RecipeService.CreateAsync` / `UpdateAsync` no longer receive `PhotoUrl` from the editor; `RecipeService.SyncPrimaryPhotoUrlAsync` keeps `Recipe.PhotoUrl` in sync automatically after every gallery mutation.

---

### `src/CookBot.Application/DependencyInjection.cs` + `src/CookBot.Web/Program.cs` (MODIFIED)

**DI registration analog** (`DependencyInjection.cs:27` for singleton, `DependencyInjection.cs:17-19` for scoped):
```csharp
// Existing singleton pattern (stateless, no DI deps):
services.AddSingleton<RecipePhotoUrlValidator>();

// Existing scoped pattern:
services.AddScoped<RecipeService>();
```

**New registrations in `DependencyInjection.cs`:**
```csharp
services.AddSingleton<PhotoUrlHeadValidator>();  // stateless, no DI deps → Singleton
services.AddScoped<RecipePhotoService>();         // has DbContext dependency → Scoped
```

**`Program.cs`** already registers `LocalRecipePhotoStorage` at line 60 (`AddScoped`) — no change needed. `RecipePhotoGalleryManager` injects `RecipePhotoService` and `PhotoUrlHeadValidator` — they are available because `AddApplication()` and `AddScoped<LocalRecipePhotoStorage>()` are already called.

---

## Shared Patterns

### Hero `<img>` Hardening (apply to every gallery `<img>`)
**Source:** `src/CookBot.Web/Components/Pages/RecipeView.razor` lines 140-145
**Apply to:** Every `<img>` in `RecipeView.razor` gallery strip, `RecipePhotoGalleryManager.razor` photo cards
```razor
referrerpolicy="no-referrer"
loading="lazy"
@onerror="@(/* one-shot per-photo-id flag */)"
```
For N photos use `HashSet<int> _failedPhotoIds` instead of `bool _heroPhotoFailed`. One-shot means: on `onerror`, add photo ID to set + `StateHasChanged()`; the conditional `if (!_failedPhotoIds.Contains(photo.Id))` stops re-rendering the broken `<img>`.

### Ownership Authorization Check
**Source:** `src/CookBot.Application/Services/RecipeService.cs` lines 154-158
**Apply to:** Every `RecipePhotoService` mutation method (before any DB write)
```csharp
var cookbook = await _cookbookRepo.GetByIdAsync(recipe.CookbookId)
    ?? throw new InvalidOperationException("Cookbook not found.");
if (cookbook.UserId != userId)
    throw new UnauthorizedAccessException("You do not own this cookbook.");
```

### Scheme Allowlist + Never-Throw Envelope
**Source:** `src/CookBot.Application/Services/RecipePhotoUrlValidator.cs` lines 34-83
**Apply to:** `RecipePhotoGalleryManager.razor` paste-URL input (step 1 before HEAD validation), any AI helper output scrub
```csharp
if (UrlValidator.TryValidate(value, out var normalized, out var errorCode))
{ /* accept */ }
else
{ /* reject with errorCode-to-message switch */ }
```

### Per-File Upload: Pre-Stream Size Check + Try/Catch
**Source:** `src/CookBot.Web/Components/Pages/RecipePhotoComposite.razor` lines 181-203
**Apply to:** `RecipePhotoGalleryManager.razor` multi-file `foreach` loop
```csharp
if (file.Size > 10L * 1024L * 1024L)
{
    Toast.Show("File too large — 10 MB max.", CbToastSeverity.Warning);
    continue;  // skip this file, don't abort batch
}
try { var url = await PhotoStorage.SaveAsync(file, ct); ... }
catch (InvalidImageException ex) { Toast.Show(..., CbToastSeverity.Warning); }
catch (Exception ex) { Toast.Show(..., CbToastSeverity.Error); }
```

### AI Gate
**Source:** `src/CookBot.Web/Components/Pages/RecipeEditor.razor` lines 519-521
**Apply to:** `RecipePhotoGalleryManager.razor` AI helper button visibility
```csharp
var hostOn = CookBotSettingsOptions.Value.AiFeaturesEnabled;
var userOn = user?.Profile?.AiEnabled ?? false;
_aiOn = hostOn && userOn;
```
When `!_aiOn`: the "Suggest photo search terms" button is **not rendered** (not just disabled) — consistent with existing RecipeEditor AI gating.

### EF Fluent HasMany Cascade Pattern
**Source:** `src/CookBot.Infrastructure/Data/Configurations/RecipeConfiguration.cs` line 29
**Apply to:** `RecipePhotoConfiguration.cs`
```csharp
builder.HasMany(r => r.RecipeIngredients).WithOne(ri => ri.Recipe).HasForeignKey(ri => ri.RecipeId).OnDelete(DeleteBehavior.Cascade);
```

### Copyright Disclaimer
**Source:** `src/CookBot.Web/Components/Pages/RecipeEditorParts/RecipePhotoComposite.razor` lines 91-94
**Apply to:** `RecipePhotoGalleryManager.razor` (once, at bottom — covers upload, paste-URL, and AI helper surfaces in the same component)
```razor
<div style="font-size:11.5px;color:var(--ink-3);line-height:1.4;">
    Only add photos you have the right to use.
    AI suggestions are search terms only — verify the license at the source.
</div>
```

---

## No Analog Found

All files have close analogs. No greenfield file requires falling back to RESEARCH.md patterns exclusively.

---

## Metadata

**Analog search scope:** `src/CookBot.Domain/Entities/`, `src/CookBot.Infrastructure/Data/Configurations/`, `src/CookBot.Infrastructure/Migrations/`, `src/CookBot.Application/Services/`, `src/CookBot.Application/DTOs/`, `src/CookBot.Web/Services/`, `src/CookBot.Web/Components/Pages/`, `src/CookBot.Infrastructure/AI/`
**Files read:** 18 source files
**Pattern extraction date:** 2026-06-07

---

## PATTERN MAPPING COMPLETE

**Phase:** 14 - Photo Gallery
**Files classified:** 13
**Analogs found:** 13 / 13

### Coverage
- Files with exact analog: 11
- Files with role-match analog: 2 (`PhotoUrlHeadValidator` — hybrid of scheme-validator + HttpClient; `RecipePhotoGalleryManager` — evolved from `RecipePhotoComposite`)
- Files with no analog: 0

### Key Patterns Identified
1. All new relational child entities follow `RecipeIngredient` POCO + `RecipeIngredientConfiguration` EF fluent pattern (`HasKey`, `HasIndex` on composite, `HasOne.WithMany.HasForeignKey.OnDelete(Cascade)` configured on the child — not touching `RecipeConfiguration.cs`)
2. All new EF migrations follow `AddRecipeTagTable` shape: `CreateTable` + `CreateIndex` + `migrationBuilder.Sql(...)` backfill inside `Up()`, `DropTable` in `Down()`
3. `RecipePhotoService` uses `CookBotDbContext` directly (same as `RecipeEditor.razor` injecting `DbContext`) for `Include`/`OrderBy`/`Where` queries — generic `IRepository<T>` does not support these
4. `LocalRecipePhotoStorage.DeletePhysicalFile` is a thin wrapper over the existing `AssertPathInsideUploadsDirectory` guard (lines 118-134) — all file deletion in the project goes through this guard
5. `IAiService.SendMessageAsync` is the correct text-only AI transport (`IAiService.cs:14`); `maxTokens` defaults to 4096 which is sufficient for the search-term helper output

### File Created
`/home/noah/Desktop/projects/freeloves-cookbot/.planning/phases/14-photo-gallery/14-PATTERNS.md`

### Ready for Planning
Pattern mapping complete. Planner can now reference analog patterns in PLAN.md files.
