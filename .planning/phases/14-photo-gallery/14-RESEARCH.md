# Phase 14: Photo Gallery — Research

**Researched:** 2026-06-07
**Domain:** .NET 10 Blazor Server — multi-photo entity, sequential file upload over SignalR, HEAD-validation, EF Core migration + backfill, orphaned-file cleanup, text-only AI helper transport
**Confidence:** HIGH (all findings verified against live codebase reads or official Microsoft/ASP.NET Core documentation)

---

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions
- **D-14-01 (FLAGGED):** `Recipe.PhotoUrl` stays as a **denormalized mirror** of the primary `RecipePhoto.Url`, re-synced by `RecipeService` on every gallery mutation. Gallery rows live only in `RecipePhoto` and never enter `CanonicalDocumentJson`. Confirmed: four downstream readers (`RecipeView` hero, `JsonLdRecipeProjector.image`, Home thumbnails, cookbook-collage thumbnails) depend on `Recipe.PhotoUrl`; rewiring them all is high-blast-radius and contradicts the milestone's additive stance.
- **D-14-02:** `RecipePhoto` is a relational FK child entity (NOT owned-JSON), mirroring the `RecipeIngredient` / `RecipeConfiguration.cs:29` pattern — `HasMany(r => r.Photos).WithOne(p => p.Recipe).HasForeignKey(p => p.RecipeId).OnDelete(DeleteBehavior.Cascade)`.
- **D-14-03 (shape):** `RecipePhoto { int Id; int RecipeId; string Url (max 2048); string? Caption (max ~512); int SortOrder; bool IsPrimary; }`. Exactly one `IsPrimary` per recipe, enforced in the service layer.
- **D-14-04-cap:** `CookBotSettings.MaxPhotosPerRecipe` (default 10, clamped [1,20]). Enforced server-side.
- **D-14-05 (upload):** Single `<InputFile multiple>` whose handler persists each file **strictly sequentially** — one `await PhotoStorage.SaveAsync(file)` per file in a loop, with per-file try/catch. Never buffer all files at once. Each file stays under the 12 MB SignalR cap.
- **D-14-06 (reorder):** Move-up / move-down + "Set as hero" buttons. No HTML5 drag-and-drop.
- **D-14-07-ai:** Button ("Suggest photo search terms"), gated by `AiFeaturesEnabled && UserProfile.AiEnabled`. Sends recipe text, returns plain text (one-line dish description + 3–5 search phrases + free-licensed site names). The AI **must not** emit or auto-fill any image URL.
- **D-14-08-transport:** Use the **existing text path on `IAiService`** — `SendMessageAsync`. Not `IAiRecipeGenerator` / structured output. Not a vision path.
- **D-14-09-disclaimer:** Copyright disclaimer visible on every photo input surface (upload, paste-URL, AI helper output).
- **D-14-10:** Paste-URL HEAD-validation before persist: (1) `RecipePhotoUrlValidator` scheme check, (2) HTTP `HEAD` with short timeout + 2xx + `Content-Type: image/*`. Fall back to ranged `GET bytes=0-511` on `405`. Failure blocks persist.
- **D-14-11:** EF cascade deletes rows, not files. Explicit service-layer file deletion on single-photo delete and recipe delete. Guard with `AssertPathInsideUploadsDirectory`. Skip external `http(s)://` URLs. Recipe-delete runs file cleanup before/within the cascade.
- **D-14-12:** Photos omitted from `.cookbook.json` entirely — no transfer-DTO change.

### Claude's Discretion
- Exact `RecipePhoto` caption max-length (~512 proposed).
- Backfill location: migration-SQL in `Up()` vs. `DatabaseSeeder` post-`MigrateAsync` — recommend migration-SQL.
- Gallery UI layout (hero + thumbnail strip in RecipeView; card-grid in RecipeEditor).
- Exact AI-helper prompt wording.
- Dedicated `RecipePhotoService` vs. extending `RecipeService` — recommend `RecipePhotoService`.

### Deferred Ideas (OUT OF SCOPE)
- HTML5 drag-and-drop reorder.
- Per-step photo linking.
- Unsplash/Pexels API integration.
- AI vision / image-input path.
- Photos in `.cookbook.json` export/import.
- Strict interpretation (B) of the canonical invariant (remove `PhotoUrl` from canonical).
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|-----------------|
| GALLERY-01 | `RecipePhoto` entity (ordered, optional caption, one primary); EF migration backfills existing `Recipe.PhotoUrl` to a primary `RecipePhoto` row with no data loss | §EF Core Migration + Backfill, §Don't Hand-Roll (backfill SQL), §Pitfall: cascade vs. files |
| GALLERY-02 | Upload multiple photos sequentially, reorder, set captions, choose primary/hero from RecipeEditor (respecting v1.3 12 MB / magic-byte / scheme-allowlist) | §Blazor Multi-File Upload, §Sequential Upload Code, §Gallery UI |
| GALLERY-03 | RecipeView shows gallery (primary as hero); deleting a photo or recipe removes the file from `wwwroot/uploads/` (no orphaned files) | §Orphaned-File Cleanup, §Delete Path, §Gallery UI |
| GALLERY-04 | AI helper (gated) suggests text-only search terms; AI never emits/auto-embeds a URL; paste-URL HEAD-validated before persist; copyright disclaimer on every photo input surface | §AI Text-Helper Transport, §HEAD-Validation, §Security Threat Model |
</phase_requirements>

---

## Summary

Phase 14 extends the existing v1.3 single-hero photo pipeline into a multi-photo gallery backed by a new `RecipePhoto` relational entity. Every major design decision is already locked in CONTEXT.md. Research confirms those decisions against the live codebase and fills in five genuine implementation unknowns:

**1. Blazor multi-file upload (P14):** The `<InputFile multiple>` component transfers all selected file metadata in a single SignalR message. With the existing `MaximumReceiveMessageSize = 12 MB` (`Program.cs:34`) and a per-file cap of 10 MB, selecting even two large files can push the manifest past 12 MB and silently drop the circuit. The safe pattern is strictly sequential processing — `e.GetMultipleFiles(maxCount)` returns an enumerable; `await`-ing each `SaveAsync` in a `foreach` loop keeps each individual SignalR frame well under the limit. Per-file `try/catch` prevents one bad file from aborting the entire batch. No `MaximumReceiveMessageSize` change needed.

**2. Paste-URL HEAD-validation (D-14-10):** Step 1 is `RecipePhotoUrlValidator.TryValidate` (already defangs `javascript:`/`data:`/`file:`). Step 2 is an HTTP `HEAD` with a 5-second timeout via the existing `HttpClient` pattern, accepting only `2xx + Content-Type: image/*`. Many CDNs return `405 Method Not Allowed` to `HEAD` — the fallback is a `GET` with `Range: bytes=0-511`, which fetches the first 512 bytes of the resource; if that returns `2xx` or `206` with `Content-Type: image/*`, validation passes. SSRF is mitigated by the scheme allowlist already in place (step 1 blocks non-http/https). This logic belongs in `Application` (as a new `IPhotoUrlHeadValidator` or method on `RecipePhotoService`) because it involves business-rule validation, not Blazor infrastructure.

**3. EF Core migration + backfill (GALLERY-01):** The standard EF pattern for a relational child table is `migrationBuilder.CreateTable(...)` followed by `migrationBuilder.Sql("INSERT INTO RecipePhotos ...")`. Doing the backfill in the migration `Up()` is the recommended approach for this codebase: it runs atomically with the schema change inside the same `MigrateAsync()` call, matches the forward-only convention, and mirrors the v1.1 `CanonicalDocumentJson` backfill precedent. The raw SQL is simple: one `INSERT INTO RecipePhotos (RecipeId, Url, SortOrder, IsPrimary) SELECT Id, PhotoUrl, 0, 1 FROM Recipes WHERE PhotoUrl IS NOT NULL AND PhotoUrl != ''`.

**4. Orphaned-file cleanup (D-14-11):** EF `DeleteBehavior.Cascade` removes rows, not files. `RecipeService.DeleteAsync` currently calls `_recipeRepo.DeleteAsync(recipe)` with no file cleanup (`RecipeService.cs:268-280`). The delete path must enumerate `recipe.Photos` (requires `.Include(r => r.Photos)` on the load query) **before** the cascade runs, then delete local files via `LocalRecipePhotoStorage`'s `AssertPathInsideUploadsDirectory` guard. For photo-service single deletes, the same guard applies. Missing-file deletes are non-fatal (log-and-continue). The key sequencing constraint: file deletion must happen **before or within** the `DeleteAsync` call that triggers the cascade — if done after, the rows are already gone and the URL list is unavailable.

**5. AI text-helper transport (D-14-08):** The right method is `IAiService.SendMessageAsync(systemPrompt, messages, apiKey, modelId)` — the same non-streaming text-in / text-out path used by `AiChat`. It returns a single `string`. The call is NOT structured-output (`IStructuredAiService.SendStructuredAsync`) because the result is free prose, not a `RecipeDocument`. The AI gate `_aiOn = hostOn && userOn` from `RecipeEditor.razor:519-521` is reused verbatim.

**Primary recommendation:** Implement in four focused plans: (1) `RecipePhoto` entity + EF migration + backfill + `RecipePhotoService` skeleton; (2) `RecipeEditor` multi-photo manager (sequential upload, reorder, caption, set-hero) + HEAD-validation; (3) `RecipeView` gallery strip + `RecipeService` delete-path cleanup; (4) AI text helper + copyright disclaimer pass across all photo surfaces.

---

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| `RecipePhoto` entity shape + POCO | Domain | — | Pure POCO, no framework refs |
| `RecipePhotoConfiguration` (EF fluent config) | Infrastructure | — | EF model config belongs in Infrastructure |
| EF migration + backfill SQL | Infrastructure | — | Schema + data change, forward-only |
| `RecipePhotoService` (add/reorder/set-primary/delete) | Application | — | Business logic, owns one-primary invariant and `PhotoUrl` re-sync |
| Paste-URL HEAD-validation logic | Application | — | Business-rule validation; `IPhotoUrlHeadValidator` interface in Application, HttpClient dependency injected |
| `LocalRecipePhotoStorage.DeleteAsync` (new method) | Web (existing service) | — | Has `IWebHostEnvironment` dependency; `AssertPathInsideUploadsDirectory` lives here |
| AI search-term helper call | Application / Web | — | `IAiService.SendMessageAsync` injected into the Razor component directly (same pattern as AiChat) |
| `RecipeView` gallery strip + hero swap | Web (Blazor) | — | Display-only; never mutates canonical |
| `RecipeEditor` multi-photo manager | Web (Blazor) | Application | UI calls `RecipePhotoService` methods; sequential upload calls `LocalRecipePhotoStorage` |
| `Recipe.PhotoUrl` re-sync | Application (`RecipeService`) | — | Single owner of canonical writes per P15 invariant |

---

## Standard Stack

### Core (all pre-existing — zero new NuGet packages)

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| `Microsoft.EntityFrameworkCore` | 10.x (already in project) | `RecipePhoto` entity, migration, backfill | Project ORM — locked |
| `Microsoft.EntityFrameworkCore.Sqlite` | 10.x (already in project) | SQLite provider | Project DB — locked |
| `Microsoft.AspNetCore.Components.Forms` | 10.x (already in project) | `InputFile` multi-select, `IBrowserFile` | Blazor built-in |
| `System.Net.Http` | 10.x (BCL) | HEAD/ranged-GET validation | No new client; reuse `new HttpClient()` pattern from `AnthropicAiService` (known tech debt, acceptable for this low-volume case) |

No new NuGet packages. This is a hard invariant from STATE.md and CLAUDE.md.

**Version verification:** All packages are already present in the project. The `dotnet.csproj` targets `.NET 10`.

---

## Package Legitimacy Audit

No new packages are being installed for this phase. Hard invariant: zero new NuGet packages. Audit is not required.

---

## Architecture Patterns

### System Architecture Diagram

```
User browser
  │  (file bytes over SignalR frames, each file sequentially)
  ▼
RecipeEditor.razor (Web)
  │  foreach file: await LocalRecipePhotoStorage.SaveAsync(file) → "/uploads/{guid}.ext"
  │  await RecipePhotoService.AddPhotoAsync(recipeId, url, userId)
  ▼
RecipePhotoService (Application)          ◄── RecipeService.SyncPrimaryPhotoUrl()
  │  owns: add / reorder / set-primary /        (writes Recipe.PhotoUrl + CanonicalDocumentJson)
  │        delete + file-cleanup
  ▼
RecipePhoto rows (Infrastructure / SQLite)
  FK→ Recipe (OnDelete Cascade)

URL paste path:
RecipePhotoComposite (clone/evolution)
  │  RecipePhotoUrlValidator.TryValidate (scheme check)
  │    └── if passes: IPhotoUrlHeadValidator.ValidateAsync(url)
  │          ├── HEAD → 2xx + image/* → accept
  │          ├── 405 → GET Range:bytes=0-511 → 2xx/206 + image/* → accept
  │          └── other → reject (blocks persist)
  ▼
RecipePhotoService.AddPhotoAsync(...)

AI helper path:
RecipeEditor.razor  (_aiOn gate: hostOn && userOn)
  │  IAiService.SendMessageAsync(systemPrompt, [{role:"user", content: recipeText}], ...)
  │  → plain-text response: dish description + search phrases + free-licensed site names
  └── Displayed as guidance text; user pastes URL manually → paste-URL path above

Delete paths:
RecipePhotoService.DeletePhotoAsync(recipeId, photoId, userId)
  │  1. Load RecipePhoto row
  │  2. If Url.StartsWith("/uploads/"): LocalRecipePhotoStorage.DeleteAsync(url) [AssertPathInsideUploadsDirectory]
  │  3. EF delete row
  │  4. If was primary: promote next (lowest SortOrder)
  │  5. RecipeService.SyncPrimaryPhotoUrl(recipeId)

RecipeService.DeleteAsync(recipeId, userId)  [MODIFIED]
  │  1. Load recipe WITH .Include(r => r.Photos)
  │  2. For each photo: if local path → file delete (non-fatal)
  │  3. _recipeRepo.DeleteAsync(recipe) → cascade drops RecipePhoto rows
```

### Recommended Project Structure

```
src/CookBot.Domain/Entities/
├── RecipePhoto.cs               # NEW — POCO entity

src/CookBot.Infrastructure/Data/
├── CookBotDbContext.cs           # MODIFIED — add DbSet<RecipePhoto>
├── Configurations/
│   └── RecipePhotoConfiguration.cs  # NEW — EF fluent config (FK + cascade + indexes)
└── Migrations/
    └── {timestamp}_AddRecipePhotosTable.cs  # NEW — table + backfill SQL

src/CookBot.Application/Services/
├── RecipePhotoService.cs         # NEW — gallery CRUD + one-primary invariant
├── IPhotoUrlHeadValidator.cs     # NEW (or method on RecipePhotoService) — HEAD/GET validation
└── RecipeService.cs              # MODIFIED — DeleteAsync loads photos before cascade; SyncPrimaryPhotoUrl helper

src/CookBot.Application/DTOs/
└── CookBotSettings.cs            # MODIFIED — add MaxPhotosPerRecipe (int, default 10, clamped [1,20])

src/CookBot.Web/Services/
└── LocalRecipePhotoStorage.cs    # MODIFIED — add DeleteAsync(string url) method

src/CookBot.Web/Components/Pages/
├── RecipeView.razor              # MODIFIED — gallery strip + thumbnail hero swap
├── RecipeEditor.razor            # MODIFIED — multi-photo manager block + AI helper button
└── RecipeEditorParts/
    ├── RecipePhotoComposite.razor     # MODIFIED or CLONED — evolve to multi-photo input
    └── RecipePhotoGalleryManager.razor  # NEW (optional split) — the multi-photo management card grid
```

### Pattern 1: Sequential Multi-File Upload in Blazor Server

**What:** Process each `IBrowserFile` from `InputFileChangeEventArgs` one at a time inside a `foreach` loop with `await` on each save. Never collect all bytes before starting.

**When to use:** Any multi-file upload in Blazor Server where files may be near the 10 MB per-file limit.

**Why it matters:** The `InputFile` component sends the file selection manifest as a single SignalR message. With `MaximumReceiveMessageSize = 12 MB` (already set at `Program.cs:34`), selecting many large files simultaneously can cause the manifest to exceed the limit and silently drop the circuit (issue [dotnet/aspnetcore#42993](https://github.com/dotnet/aspnetcore/issues/42993)). Sequential processing keeps each frame under the limit and gives per-file progress feedback and per-file error recovery.

```csharp
// Source: [CITED: learn.microsoft.com/en-us/aspnet/core/blazor/file-uploads]
// + codebase pattern from LocalRecipePhotoStorage.SaveAsync

private async Task OnMultipleFilesPicked(InputFileChangeEventArgs e)
{
    // D-14-04-cap: clamp to configured max, not an arbitrary constant
    var maxCount = _settings.MaxPhotosPerRecipe;
    var remaining = maxCount - _photos.Count;  // already-saved photos count against cap
    if (remaining <= 0)
    {
        Toast.Show($"Maximum {maxCount} photos per recipe.", CbToastSeverity.Warning);
        return;
    }

    // GetMultipleFiles caps at remaining slots — rejects excess files up-front
    foreach (var file in e.GetMultipleFiles(remaining))
    {
        // P14 circuit-safety: per-file pre-stream size check before opening SignalR stream
        if (file.Size > 10L * 1024L * 1024L)
        {
            Toast.Show($"{file.Name}: File too large — 10 MB max.", CbToastSeverity.Warning);
            continue;
        }

        try
        {
            // SEQUENTIAL: await fully completes before next file starts
            // LocalRecipePhotoStorage.SaveAsync handles magic-byte sniff (reuse verbatim)
            var url = await PhotoStorage.SaveAsync(file, _cts.Token);
            await PhotoService.AddPhotoAsync(_recipeId, url, _userId);
            // Update local UI state — show per-file progress
            _photos = await PhotoService.GetPhotosAsync(_recipeId, _userId);
            StateHasChanged();
        }
        catch (InvalidImageException ex)
        {
            Toast.Show($"{file.Name}: {ex.Message}", CbToastSeverity.Warning);
            // continue to next file — one bad file does NOT abort the batch
        }
        catch (Exception ex)
        {
            Toast.Show($"{file.Name}: Upload failed — {ex.Message}", CbToastSeverity.Error);
        }
    }
}
```

**Key invariant:** `LocalRecipePhotoStorage.SaveAsync` already opens `IBrowserFile.OpenReadStream(maxAllowedSize: MaxUploadBytes)` twice — once for magic-byte sniff (first 12 bytes), once for the full payload. Both opens use the 10 MB cap (`MaxUploadBytes = 10 * 1024 * 1024`, `LocalRecipePhotoStorage.cs:40`). Reuse this verbatim per-file. [VERIFIED: codebase read]

**Accept attribute:** `<InputFile multiple accept="image/jpeg,image/png,image/gif,image/webp">` — matches the existing single-file picker in `RecipePhotoComposite.razor:79`.

### Pattern 2: Paste-URL HEAD-Validation with CDN 405 Fallback

**What:** Two-step gating of pasted image URLs before persist: (1) scheme check via `RecipePhotoUrlValidator`, (2) network validation via HEAD (with ranged-GET fallback for CDN 405s).

**When to use:** Any pasted URL photo input surface (replaces/extends the existing paste path in `RecipePhotoComposite.razor:153`).

**Why it matters:** D-14-10 makes HEAD-validation a hard gate that blocks persist on failure. The existing `RecipePhotoUrlValidator` only validates scheme/format — it does not check network reachability or content type. Many CDNs (Cloudflare, Fastly, imgix) reject `HEAD` with `405 Method Not Allowed` on image endpoints. Falling back to `GET Range: bytes=0-511` fetches only the first 512 bytes (enough for magic-byte inspection + Content-Type header from the response). [ASSUMED — CDN 405 behavior observed widely in practice; no official spec mandates it]

```csharp
// Application layer: IPhotoUrlHeadValidator (or inline in RecipePhotoService)
// Source: [ASSUMED] — pattern derived from HTTP spec + codebase HttpClient usage

public sealed class PhotoUrlHeadValidator
{
    // Reuse the per-call HttpClient pattern from AnthropicAiService (known tech debt,
    // acceptable for this low-call-volume validation path — max 1 call per paste event)
    public async Task<PhotoUrlValidationResult> ValidateAsync(string url, CancellationToken ct = default)
    {
        using var http = new HttpClient();
        http.Timeout = TimeSpan.FromSeconds(5);  // short timeout per D-14-10

        try
        {
            // Step 1: try HEAD
            var headRequest = new HttpRequestMessage(HttpMethod.Head, url);
            var headResponse = await http.SendAsync(headRequest,
                HttpCompletionOption.ResponseHeadersRead, ct);

            if ((int)headResponse.StatusCode == 405)
            {
                // CDN 405 fallback: tiny ranged GET fetches first 512 bytes only
                // Returns the response Content-Type header which is what we need
                var rangeRequest = new HttpRequestMessage(HttpMethod.Get, url);
                rangeRequest.Headers.Range = new RangeHeaderValue(0, 511);
                var rangeResponse = await http.SendAsync(rangeRequest,
                    HttpCompletionOption.ResponseHeadersRead, ct);

                return EvaluateResponse(rangeResponse);
            }

            return EvaluateResponse(headResponse);
        }
        catch (TaskCanceledException)
        {
            return PhotoUrlValidationResult.Timeout;
        }
        catch (HttpRequestException)
        {
            return PhotoUrlValidationResult.NetworkError;
        }
    }

    private static PhotoUrlValidationResult EvaluateResponse(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode)
            return PhotoUrlValidationResult.HttpError(response.StatusCode);

        var ct = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
        return ct.StartsWith("image/", StringComparison.OrdinalIgnoreCase)
            ? PhotoUrlValidationResult.Valid
            : PhotoUrlValidationResult.NotAnImage(ct);
    }
}

// Result type (lightweight — no exception for the reject lanes)
public record PhotoUrlValidationResult(bool IsValid, string? ErrorMessage)
{
    public static PhotoUrlValidationResult Valid => new(true, null);
    public static PhotoUrlValidationResult Timeout => new(false, "URL validation timed out — check the URL and try again.");
    public static PhotoUrlValidationResult NetworkError => new(false, "Could not reach the photo URL — check connectivity.");
    public static PhotoUrlValidationResult NotAnImage(string ct) => new(false, $"URL did not return an image (Content-Type: {ct}).");
    public static PhotoUrlValidationResult HttpError(HttpStatusCode sc) => new(false, $"URL returned HTTP {(int)sc} — not accessible.");
}
```

**SSRF posture:** For a trusted-LAN self-host, the only SSRF mitigation needed is (a) the scheme allowlist in `RecipePhotoUrlValidator` (already blocks `file:`/`javascript:`/`data:`) and (b) no auto-follow to internal-host redirects. Set `AllowAutoRedirect = false` on the `HttpClient` for this validator, or limit redirect following to `http`/`https` schemes only. The user is the initiating actor (not server-side automation), so full SSRF hardening is out of scope.

**Layer placement:** This belongs in `Application` (new file `Services/PhotoUrlHeadValidator.cs`) because it is business-rule validation called by `RecipePhotoService`, not Blazor infrastructure. Inject `IHttpClientFactory` if available, or follow the `AnthropicAiService` per-call `new HttpClient()` pattern for this low-frequency path.

### Pattern 3: EF Core Child-Entity Configuration + Backfill Migration

**What:** Add `RecipePhoto` table as a relational FK child entity of `Recipe`, mirroring the `RecipeIngredient` / `RecipeConfiguration.cs:29` pattern, plus a raw-SQL backfill inside the migration `Up()`.

**When to use:** Any new EF child entity in this codebase.

**Template — `RecipePhotoConfiguration.cs`:**

```csharp
// Source: [VERIFIED: codebase — mirrors RecipeConfiguration.cs:29 + RecipeIngredientConfiguration.cs verbatim]
public class RecipePhotoConfiguration : IEntityTypeConfiguration<RecipePhoto>
{
    public void Configure(EntityTypeBuilder<RecipePhoto> builder)
    {
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Url)
            .HasMaxLength(2048)      // matches Recipe.PhotoUrl max-length (RecipeConfiguration.cs:19)
            .IsRequired();

        builder.Property(p => p.Caption)
            .HasMaxLength(512);      // Claude's Discretion value from CONTEXT.md

        builder.Property(p => p.SortOrder)
            .HasDefaultValue(0);

        builder.Property(p => p.IsPrimary)
            .HasDefaultValue(false);

        // Composite index for the GetPhotosAsync(recipeId) query — mirrors RecipeIngredientConfiguration.cs:16
        builder.HasIndex(p => new { p.RecipeId, p.SortOrder });

        // FK is configured here instead of on RecipeConfiguration
        // so it does not require touching RecipeConfiguration.cs
        builder.HasOne(p => p.Recipe)
               .WithMany(r => r.Photos)
               .HasForeignKey(p => p.RecipeId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
```

**Template — Migration `Up()` with backfill SQL:**

```csharp
// Source: [CITED: learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/managing]
// + [VERIFIED: codebase — pattern of AddRecipePhotoUrlAndDescription migration]
protected override void Up(MigrationBuilder migrationBuilder)
{
    migrationBuilder.CreateTable(
        name: "RecipePhotos",
        columns: table => new
        {
            Id        = table.Column<int>(nullable: false)
                             .Annotation("Sqlite:Autoincrement", true),
            RecipeId  = table.Column<int>(nullable: false),
            Url       = table.Column<string>(maxLength: 2048, nullable: false),
            Caption   = table.Column<string>(maxLength: 512, nullable: true),
            SortOrder = table.Column<int>(nullable: false, defaultValue: 0),
            IsPrimary = table.Column<bool>(nullable: false, defaultValue: false),
        },
        constraints: table =>
        {
            table.PrimaryKey("PK_RecipePhotos", x => x.Id);
            table.ForeignKey(
                name: "FK_RecipePhotos_Recipes_RecipeId",
                column: x => x.RecipeId,
                principalTable: "Recipes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        });

    migrationBuilder.CreateIndex(
        name: "IX_RecipePhotos_RecipeId_SortOrder",
        table: "RecipePhotos",
        columns: new[] { "RecipeId", "SortOrder" });

    // GALLERY-01 BACKFILL: one primary row per recipe for every recipe that
    // already has a PhotoUrl. Runs atomically with the schema change inside
    // MigrateAsync(). Matches the forward-only convention. Empty-string guard
    // is intentional — the existing schema allows '' as well as NULL.
    migrationBuilder.Sql(@"
        INSERT INTO RecipePhotos (RecipeId, Url, SortOrder, IsPrimary)
        SELECT Id, PhotoUrl, 0, 1
        FROM Recipes
        WHERE PhotoUrl IS NOT NULL AND PhotoUrl != ''
    ");
}
```

**Why migration-SQL over DatabaseSeeder:** The backfill is a one-time, atomic, schema-coupled operation. Doing it in `DatabaseSeeder.SeedAsync` after `MigrateAsync()` would work but introduces a window where the `RecipePhotos` table exists empty while the app starts — any concurrent request during that gap sees no photos for recipes that should have them. Migration-SQL is atomic with the table creation. This is the same pattern used for the `CanonicalDocumentJson` backfill in v1.1. [ASSUMED — based on EF Core migration documentation and project conventions; either approach is functionally safe at low concurrency]

### Pattern 4: `RecipePhotoService` — One-Primary Invariant

**What:** A focused Application-layer service that owns all gallery mutations and enforces that exactly one `RecipePhoto.IsPrimary == true` exists per recipe at all times.

**Method signatures:**

```csharp
// Source: [VERIFIED: codebase — mirrors RecipeService ownership pattern]
public class RecipePhotoService
{
    // Returns ordered photos for display/editor
    Task<IReadOnlyList<RecipePhoto>> GetPhotosAsync(int recipeId, int userId);

    // Validates ownership + cap; persists row; if first photo, sets IsPrimary = true
    // and calls RecipeService.SyncPrimaryPhotoUrlAsync(recipeId) to re-sync Recipe.PhotoUrl
    Task<RecipePhoto> AddPhotoAsync(int recipeId, string url, int userId, string? caption = null);

    // Sets IsPrimary=true on photoId, IsPrimary=false on all others for this recipe
    // Calls RecipeService.SyncPrimaryPhotoUrlAsync(recipeId) after
    Task SetPrimaryAsync(int recipeId, int photoId, int userId);

    // Re-assigns SortOrder values based on supplied ordered ID list
    Task ReorderAsync(int recipeId, int[] orderedPhotoIds, int userId);

    // Deletes file if local path, then deletes row; if was primary, promotes lowest SortOrder
    // Calls RecipeService.SyncPrimaryPhotoUrlAsync(recipeId) after
    Task DeleteAsync(int recipeId, int photoId, int userId);

    // Sets caption on a single photo
    Task UpdateCaptionAsync(int recipeId, int photoId, string? caption, int userId);
}
```

**The `SyncPrimaryPhotoUrl` helper on `RecipeService`:**

```csharp
// Internal to RecipeService — called after every gallery mutation (D-14-01)
// Reads the current IsPrimary row (or first by SortOrder if none flagged) and
// writes Recipe.PhotoUrl + CanonicalDocumentJson. P15: only RecipeService writes canonical.
internal async Task SyncPrimaryPhotoUrlAsync(int recipeId)
{
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
    // Re-serialize canonical doc to keep PhotoUrl in sync with CanonicalDocumentJson
    var doc = _canonicalSerializer.Deserialize(recipe.CanonicalDocumentJson);
    recipe.CanonicalDocumentJson = _canonicalSerializer.Serialize(doc with { PhotoUrl = recipe.PhotoUrl });
    await _recipeRepo.UpdateAsync(recipe);
}
```

### Pattern 5: Orphaned-File Cleanup

**What:** Explicit file deletion in the service layer, guarded by `AssertPathInsideUploadsDirectory`.

**Key sequencing constraint:** The photo rows must be enumerated **before** EF cascade deletes them. The `Include(r => r.Photos)` on the load query is required.

```csharp
// RecipeService.DeleteAsync — MODIFIED (RecipeService.cs:268-280 today has no file cleanup)
// Source: [VERIFIED: codebase — RecipeService.cs:268-280]
public async Task DeleteAsync(int recipeId, int userId)
{
    var recipe = await _recipeRepo.GetByIdAsync(recipeId, q => q.Include(r => r.Photos))
        ?? throw new InvalidOperationException("Recipe not found.");
    // ... ownership check (existing) ...

    // NEW: enumerate local-path photos BEFORE cascade deletes the rows
    foreach (var photo in recipe.Photos)
    {
        if (photo.Url.StartsWith("/uploads/", StringComparison.Ordinal))
        {
            try
            {
                _photoStorage.DeletePhysicalFile(photo.Url);  // calls AssertPathInsideUploadsDirectory
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not delete photo file {Url} during recipe delete", photo.Url);
                // non-fatal — continue deletion
            }
        }
        // External http(s):// URLs: no local file to delete (D-14-11)
    }

    await _recipeRepo.DeleteAsync(recipe);  // EF cascade removes RecipePhoto rows
}
```

**`LocalRecipePhotoStorage.DeleteAsync` (new method):**

```csharp
// Source: [VERIFIED: codebase — LocalRecipePhotoStorage.AssertPathInsideUploadsDirectory:118-134]
public void DeletePhysicalFile(string url)
{
    // url is "/uploads/{guid}.ext" — convert to absolute path
    var safeName = url.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
    var fullPath = Path.Combine(_uploadsDir, Path.GetFileName(safeName));
    AssertPathInsideUploadsDirectory(fullPath);  // throws if traversal attempt
    if (File.Exists(fullPath))
        File.Delete(fullPath);
    // Missing file is non-fatal — log and continue
}
```

**Note on `IRepository.GetByIdAsync` signature:** The current `IRepository<T>` interface may not accept an `Include` expression. If so, `RecipePhotoService.DeleteAsync` should load the photos directly via `DbContext.RecipePhotos.Where(p => p.RecipeId == recipeId)` before calling `RecipeService.DeleteAsync`, and `RecipeService.DeleteAsync` receives the photo URLs as a parameter — or `RecipePhotoService` coordinates the two operations.

### Pattern 6: AI Text-Helper Transport

**What:** Call `IAiService.SendMessageAsync` (the AiChat text path) with a prompt that constrains output to plain text search terms.

**Method to use:**

```csharp
// Source: [VERIFIED: codebase — IAiService.cs:14, AnthropicAiService.cs:77-89]
// Same path used by AiChat.razor's non-streaming "send" action
string result = await _aiService.SendMessageAsync(
    systemPrompt: systemPrompt,
    messages: new List<AiMessage>
    {
        new() { Role = "user", Content = recipeText }
    },
    apiKey: _resolvedApiKey,  // from AiApiKeyResolutionService, same as RecipeEditor's AI calls
    modelId: null             // use default (claude-sonnet-4-6)
);
```

**AI gate reuse:** Copy the pattern from `RecipeEditor.razor:519-521` verbatim:

```csharp
// Source: [VERIFIED: codebase — RecipeEditor.razor:519-521]
var hostOn = CookBotSettingsOptions.Value.AiFeaturesEnabled;
var userOn = user?.Profile?.AiEnabled ?? false;
_aiOn = hostOn && userOn;
```

The "Suggest photo search terms" button is disabled/hidden when `!_aiOn`. [VERIFIED: codebase]

**Prompt shape that structurally cannot emit a usable image URL:**

```
System:
You are a food photography search assistant. The user will give you a recipe.
You must respond with ONLY:
1. One sentence describing what the finished dish looks like (color, texture, presentation).
2. A numbered list of 3–5 photo search phrases to use on free stock photo sites.
3. A short list of recommended free-licensed photo sites to search: Unsplash, Pexels, Wikimedia Commons.

Rules:
- Do not include any URLs in your response.
- Do not suggest any specific photographs or image sources by URL.
- Do not use markdown links or href syntax.
- Respond in plain text only.

User:
{recipe name}, {description}, ingredients: {top 5 ingredient names}
```

The explicit "do not include any URLs" rule + the structural constraint of only listing sites by name (not URL) makes it structurally improbable for the model to emit a usable image URL. The output is plain text, displayed verbatim to the user as guidance. [ASSUMED — prompt wording is recommended; final wording is Claude's Discretion per CONTEXT.md]

**Output display:** Shown as guidance text (not a link). The user reads the suggestions and searches manually, then pastes their chosen URL into the paste-URL field. This satisfies D-14-07: the user is the final actor before any URL is saved.

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Per-file upload size validation | Custom `Stream.Length` check before `OpenReadStream` | `file.Size` pre-stream check + `maxAllowedSize` on `OpenReadStream` | Already in `RecipePhotoComposite.razor:181` and `LocalRecipePhotoStorage.cs:40` — reuse verbatim |
| Magic-byte sniff for uploaded files | Custom byte-comparison logic | `ImageMagicBytes.DetectExtension` + `LocalRecipePhotoStorage.SaveAsync` | Already covers JPEG/PNG/GIF/WebP; PITFALL H3 documented |
| Path-traversal guard on file delete | Manual string comparison | `AssertPathInsideUploadsDirectory` (`LocalRecipePhotoStorage.cs:118-134`) | Already has trailing-separator bug fix; PITFALL H2 documented |
| Scheme validation on paste-URL | New validator class | `RecipePhotoUrlValidator.TryValidate` (`RecipePhotoUrlValidator.cs`) | Defangs `javascript:`/`data:`/`file:`/protocol-relative; used across AnthropicAiService too |
| AI gate logic | Duplicate `AiFeaturesEnabled && AiEnabled` check | `_aiOn = hostOn && userOn` from `RecipeEditor.razor:519-521` | Canonical gate; any duplication risks drift |
| `Recipe.PhotoUrl` write path | Direct property set from gallery code | `RecipeService.SyncPrimaryPhotoUrlAsync` | P15: only `RecipeService` writes `CanonicalDocumentJson` |

---

## Runtime State Inventory

This phase is not a rename/refactor phase. The only runtime state concern is the **photo file backfill**: existing `Recipe.PhotoUrl` values that are local `/uploads/` paths become `RecipePhoto.Url` values after the migration. No external services store photo metadata. The backfill runs in the EF migration `Up()` which is called by `DatabaseSeeder.SeedAsync → MigrateAsync()` at startup. No manual data migration steps outside the migration file.

| Category | Items Found | Action Required |
|----------|-------------|-----------------|
| Stored data | Existing `Recipe.PhotoUrl` column values — may be `/uploads/{guid}.ext` (local) or `https://...` (pasted external) | Backfill SQL in migration `Up()` — INSERT INTO RecipePhotos for each non-null PhotoUrl row |
| Live service config | None — photos are self-contained in the SQLite DB and wwwroot/uploads/ | None |
| OS-registered state | None | None |
| Secrets/env vars | None — photos are not gated by a key | None |
| Build artifacts | None | None |

---

## Common Pitfalls

### Pitfall 1: Circuit Drop on Multi-File Selection (P14)
**What goes wrong:** User selects 5 photos simultaneously. Blazor sends the selection manifest as a single SignalR message. With the current `MaximumReceiveMessageSize = 12 MB` (`Program.cs:34`), a selection of multiple large files can push the manifest past the limit, causing a silent circuit reconnect and losing the selection. [VERIFIED: dotnet/aspnetcore#42993]
**Why it happens:** `InputFile` does not split the selection manifest across multiple SignalR messages.
**How to avoid:** Process files **strictly sequentially** in a `foreach` loop with `await` on each `SaveAsync`. The sequential pattern means only one file's data frames are in flight at a time. The existing `MaximumReceiveMessageSize = 12 MB` is sufficient for one file's metadata.
**Warning signs:** Users see "Connection lost" or a blank page after selecting multiple photos; the Blazor circuit reconnects. Selecting files one at a time works fine.

### Pitfall 2: Cascade Deletes Rows Before File Cleanup (P13)
**What goes wrong:** `RecipeService.DeleteAsync` (currently `RecipeService.cs:268-280`) calls `_recipeRepo.DeleteAsync(recipe)` with no file cleanup. After the cascade, the `RecipePhoto` rows are gone; there is no way to recover the file paths without adding a separate query.
**Why it happens:** EF cascade delete removes DB rows; the filesystem is outside the transaction scope.
**How to avoid:** Load `recipe.Photos` via `.Include(r => r.Photos)` **before** the delete call. Enumerate local-path photos and delete files first. Then call `DeleteAsync` to trigger the cascade.
**Warning signs:** After recipe delete, files remain in `wwwroot/uploads/`. Docker volume grows unbounded during UAT delete cycles.

### Pitfall 3: `IsPrimary` Invariant Drift
**What goes wrong:** After a delete or reorder operation, either zero or two `RecipePhoto` rows have `IsPrimary = true` for the same recipe. This causes `RecipeView` to pick the wrong hero photo (first by SortOrder instead of the user's chosen primary), or causes `Recipe.PhotoUrl` to be out of sync.
**Why it happens:** Race conditions in the service layer if multiple mutations happen in quick succession (Blazor Server is single-circuit, so true races are rare, but forgetting to clear the old primary when setting a new one is the common bug).
**How to avoid:** Always do the full "clear all IsPrimary = false for this recipe, then set the target IsPrimary = true" in a single DB operation or transaction. Never set `IsPrimary = true` without first clearing the other flags.
**Warning signs:** Multiple `RecipePhoto` rows with `IsPrimary = true` for the same recipe (detectable via a simple `WHERE IsPrimary = 1 GROUP BY RecipeId HAVING COUNT(*) > 1` query in tests).

### Pitfall 4: HEAD-Validation Timeout Blocks the UI
**What goes wrong:** The 5-second HEAD validation timeout blocks the Blazor Server render loop. If the user pastes a URL to a slow CDN, the paste-URL input freezes for 5 seconds before showing an error or accepting.
**Why it happens:** Calling `await headValidator.ValidateAsync(url)` synchronously in the Razor component's event handler blocks the render.
**How to avoid:** Show a "Validating URL..." loading state immediately, then `await` the validation — Blazor Server event handlers are already `async`. The 5-second timeout is the maximum wait; most fast CDNs respond in < 500ms. Do not increase the timeout.
**Warning signs:** The paste-URL input appears frozen after pasting a URL from a slow host.

### Pitfall 5: AI Helper Output Contains a URL
**What goes wrong:** Despite prompt instructions, the model includes a URL in its search-term suggestions (e.g., `"https://unsplash.com/s/photos/sourdough"`). The user mistakes this for a validated image URL and pastes it. The URL is not an image URL and will fail the `HEAD Content-Type: image/*` check — so the paste will be rejected. However, this creates a confusing UX.
**Why it happens:** LLMs may emit URLs when listing sources. The prompt says "list free-licensed sites" and the model adds a URL.
**How to avoid:** The prompt explicitly forbids URLs ("Do not include any URLs in your response"). Additionally, the output is displayed as plain guidance text and the user must manually paste into the paste-URL field — there is no auto-fill path. The HEAD-validation gate is the final safety net.
**Warning signs:** The AI helper output contains `http://` or `https://` strings. Detection: scan the output string with `Uri.TryCreate` before display; if a URL is detected, strip it or show a soft warning.

### Pitfall 6: `Recipe.PhotoUrl` Desync After Gallery Mutation
**What goes wrong:** `RecipePhotoService.SetPrimaryAsync` updates `RecipePhoto.IsPrimary` but forgets to call `RecipeService.SyncPrimaryPhotoUrlAsync`. The gallery UI shows the new primary but `Recipe.PhotoUrl`, `CanonicalDocumentJson`, and consequently `JsonLdRecipeProjector.image` still point to the old primary.
**Why it happens:** The sync responsibility lives in `RecipeService` (P15 — canonical writes only there), but `RecipePhotoService` is the mutation owner. This cross-service dependency is easy to forget.
**How to avoid:** Every `RecipePhotoService` mutation method (AddPhoto, SetPrimary, Reorder, Delete) ends with a call to `RecipeService.SyncPrimaryPhotoUrlAsync`. This is a defined contract, not an afterthought. The Phase 13 `JsonLdRecipeProjector` reads `doc.PhotoUrl` — if it desync'd, JSON-LD breaks silently.
**Warning signs:** After reordering or set-hero in the editor, the JSON-LD `image` field still points to the old primary URL. The `RecipeView` hero shows the new primary (from `_photos.FirstOrDefault(p => p.IsPrimary)`) while the JSON-LD head still has the old URL.

---

## Code Examples

### Gallery `<img>` Hardening (apply to every gallery image)

```razor
@* Source: [VERIFIED: codebase — RecipeView.razor:140-145 hero pattern extended to gallery strip] *@
@foreach (var photo in _galleryPhotos)
{
    <img src="@photo.Url"
         alt="@(_recipe.Name) photo @(photo.SortOrder + 1)"
         referrerpolicy="no-referrer"
         loading="lazy"
         @onerror="@(() => OnGalleryImageError(photo.Id))"
         class="gallery-thumb @(photo.IsPrimary ? "gallery-thumb--hero" : "")"
         @onclick="@(() => SwapHero(photo.Id))" />
}
```

The `@onerror` is a one-shot per photo ID (set a `HashSet<int> _failedPhotoIds` flag; skip rendering that `<img>` and show a broken-thumbnail placeholder). This mirrors the `_heroPhotoFailed` bool pattern in `RecipeView.razor:138-150` extended to N photos.

### Move-Up / Move-Down Reorder (no drag-drop per D-14-06)

```razor
@* Source: [VERIFIED: codebase — mirrors v1.2 editor immutable-id reorder convention] *@
@for (int i = 0; i < _photos.Count; i++)
{
    var photo = _photos[i];
    var idx = i;  // capture for lambda
    <div class="photo-card">
        <img src="@photo.Url" ... />
        <button @onclick="@(() => MoveUp(idx))" disabled="@(idx == 0)" aria-label="Move photo up">
            ↑
        </button>
        <button @onclick="@(() => MoveDown(idx))" disabled="@(idx == _photos.Count - 1)" aria-label="Move photo down">
            ↓
        </button>
        <button @onclick="@(() => SetAsHero(photo.Id))" disabled="@photo.IsPrimary" aria-label="Set as hero photo">
            @(photo.IsPrimary ? "Hero" : "Set hero")
        </button>
        <button @onclick="@(() => DeletePhoto(photo.Id))" aria-label="Delete photo">
            Delete
        </button>
    </div>
}

@code {
    private async Task MoveUp(int index)
    {
        if (index == 0) return;
        var ids = _photos.Select(p => p.Id).ToArray();
        (ids[index - 1], ids[index]) = (ids[index], ids[index - 1]);
        await PhotoService.ReorderAsync(_recipeId, ids, _userId);
        _photos = await PhotoService.GetPhotosAsync(_recipeId, _userId);
    }
}
```

### Copyright Disclaimer (all photo input surfaces)

```razor
@* GALLERY-04 / D-14-09 — visible on upload, paste-URL, and AI helper output *@
<div class="photo-copyright-notice" role="note" aria-label="Copyright notice"
     style="font-size:11.5px;color:var(--ink-3);line-height:1.4;margin-top:6px;">
    Only add photos you have the right to use.
    AI suggestions are search terms only — verify the license at the source.
</div>
```

---

## State of the Art

| Old Approach | Current Approach | Notes |
|--------------|------------------|-------|
| Blazor `InputFile` drag-to-upload for multi-file | Sequential `foreach` with `await` per file | Prevents circuit drops on manifest size exceeded |
| `Recipe.PhotoUrl` as sole photo storage | `RecipePhoto` entity table (ordered, captioned, one-primary) | Operational data separated from canonical format data |
| HEAD-validate-never for paste-URL | HEAD with 405→ranged-GET fallback; blocks persist on failure | CDN compatibility + GALLERY-04 hard gate |
| Single-hero editor composite | Multi-photo manager with move-up/down + set-hero | Reorder without drag-drop for A11Y + SignalR safety |

**Deprecated/outdated notes from milestone research:**
- ARCHITECTURE.md §Theme 5 mentions "AI reverse-image / vision path" and "drag-to-reorder (HTML5)" — both superseded by D-14-06 (move-up/down) and D-14-07/08 (text-only AI helper). Do not implement.
- ARCHITECTURE.md §Theme 5 mentions `AiGenerated BIT` column on `RecipePhoto` — not in D-14-03 shape; omit.

---

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | CDN 405 on HEAD is common enough to require the ranged-GET fallback | Pattern 2 (HEAD-Validation) | If risk is overstated, the fallback code is harmless but dead. If risk is understated, some valid URLs are rejected. |
| A2 | Migration-SQL backfill is safe and atomic at the concurrency level of this self-hosted app | Pattern 3 (Migration) | If wrong (e.g., startup concurrency race), the alternative is DatabaseSeeder post-`MigrateAsync`. Both are functionally correct; atomicity argument favors migration-SQL. |
| A3 | The AI text prompt shape proposed prevents URL emission with high reliability | Pattern 6 (AI Helper) | Prompt wording is Claude's Discretion per CONTEXT.md. If the model emits URLs despite the rule, the HEAD-validation gate and display-as-guidance-text approach still prevent auto-embed. |
| A4 | `IRepository<T>.GetByIdAsync` does not accept an `Include` expression in the current codebase | Pattern 5 (Orphaned-file cleanup) | If the interface supports Include, the pattern simplifies. If not (most likely given generic IRepository), the load must use DbContext directly or pass photo URLs separately. |

---

## Open Questions

1. **`IRepository<T>.GetByIdAsync` Include support**
   - What we know: `IRepository<T>` is generic; `RecipeService.DeleteAsync` currently calls `_recipeRepo.GetByIdAsync(recipeId)` without Include.
   - What's unclear: Whether the generic `Repository<T>` implementation supports an `Include` overload or if DbContext must be used directly.
   - Recommendation: The planner should read `src/CookBot.Infrastructure/Data/Repositories/Repository.cs` to confirm the signature. If no Include support, coordinate delete via `RecipePhotoService` which has direct DbContext access.

2. **`LocalRecipePhotoStorage` layer access**
   - What we know: `LocalRecipePhotoStorage` is in `CookBot.Web/Services/` and has an `IWebHostEnvironment` constructor dependency. `RecipePhotoService` is in `Application`.
   - What's unclear: The cleanest injection path — either (a) define `ILocalRecipePhotoStorage` interface in Application with `DeletePhysicalFile(string url)` method and implement in Web, or (b) keep file-deletion in the Web layer (Razor component calls `PhotoStorage.DeletePhysicalFile` before calling `PhotoService.DeleteAsync`), or (c) pass the `wwwroot` path as a config value into the Application service.
   - Recommendation: Option (b) is simplest and matches the existing pattern (RecipeEditor already calls `LocalRecipePhotoStorage` directly). The Web component handles file I/O; the Application service handles DB mutations.

3. **`RecipeEditor` PhotoUrl sync during editing session**
   - What we know: `RecipeEditor.razor` currently binds `_photoUrl` (a local string) to `RecipePhotoComposite` via `PhotoUrl` / `PhotoUrlChanged`. On save, `parsed.PhotoUrl = _photoUrl` populates the `ParsedRecipe`.
   - What's unclear: With the gallery model, should the editor continue using `ParsedRecipe.PhotoUrl` for the primary, or should gallery mutations be persisted immediately (fire-and-forget on each add/delete/set-hero) rather than waiting for the full recipe save?
   - Recommendation: **Immediate persist** — gallery mutations call `RecipePhotoService` immediately (add/delete/set-hero each fire at interaction time, not on recipe save). This is simpler: it avoids staging a gallery diff in memory and avoids the complexity of reconciling pending gallery changes with the full recipe save. It also means the `RecipeEditor` works with live `RecipePhoto` rows (loaded at edit start, refreshed after each mutation). The recipe's text/ingredient/step fields still save on the "Save" button.

---

## Environment Availability

This phase adds no external tools, runtimes, or services. All dependencies are pre-existing in the project. Skipped.

---

## Security Domain

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | no — trusted-LAN, no new auth surfaces | — |
| V3 Session Management | no | — |
| V4 Access Control | yes — `RecipePhotoService` ownership checks (verify recipeId belongs to userId via cookbook ownership) | Mirror `RecipeService` ownership check pattern |
| V5 Input Validation | yes — file upload (magic-byte), URL input (scheme allowlist + HEAD validation) | `ImageMagicBytes`, `RecipePhotoUrlValidator`, `PhotoUrlHeadValidator` |
| V6 Cryptography | no — no new cryptographic operations | — |

### Threat Model for Phase 14

| Threat | STRIDE Category | Standard Mitigation |
|--------|----------------|---------------------|
| Upload of SVG / HTML-as-image (XSS via `<img src>` + malicious content) | Tampering | `ImageMagicBytes.DetectExtension` magic-byte sniff — PITFALL H3 already covers this |
| Path traversal in file delete (delete files outside `wwwroot/uploads/`) | Elevation of Privilege | `AssertPathInsideUploadsDirectory` (`LocalRecipePhotoStorage.cs:118-134`) — PITFALL H2 |
| HEAD-fetch SSRF (user pastes `http://internal-host/secret`) | Spoofing | Step 1 scheme allowlist (`RecipePhotoUrlValidator`) defangs `file:`/`data:`; for LAN SSRF, `AllowAutoRedirect = false` on the HEAD HttpClient. Trusted-LAN posture means the user is already inside the LAN; risk is low but redirect-following should still be disabled. |
| AI helper output contains a URL that is auto-embedded | Spoofing / Tampering | Prompt explicitly forbids URLs; output is display-only guidance text; user must manually paste into the paste-URL field (HEAD-validation gate is final safety net) |
| Cross-user gallery access (user A deletes user B's photo) | Elevation of Privilege | `RecipePhotoService` ownership check: load recipe → verify cookbook.UserId == userId before any mutation. Mirror `RecipeService.DeleteAsync` pattern. |
| Unbounded photo upload (DoS via disk fill) | Denial of Service | `MaxPhotosPerRecipe` server-side cap enforced in `RecipePhotoService.AddPhotoAsync`; per-file 10 MB cap in `LocalRecipePhotoStorage` |

**Note on SSRF posture:** For trusted-LAN self-hosting, the HEAD-fetch SSRF risk is inherent (any LAN user can already reach internal hosts from their browser). The mitigation is proportionate: disable automatic redirect-following on the validation `HttpClient` so the validator does not follow a redirect from an external URL to an internal host. Full SSRF filtering (deny-list of private IP ranges) is out of scope for this posture.

---

## Sources

### Primary (HIGH confidence — verified against live codebase)
- `src/CookBot.Web/Services/LocalRecipePhotoStorage.cs` — upload pipeline, `MaxUploadBytes`, `AssertPathInsideUploadsDirectory:118-134`, magic-byte sniff pattern
- `src/CookBot.Application/Services/RecipePhotoUrlValidator.cs` — scheme allowlist implementation, `TryValidate` signature
- `src/CookBot.Infrastructure/Data/Configurations/RecipeConfiguration.cs:29` — `HasMany.WithOne.HasForeignKey.OnDelete(Cascade)` template
- `src/CookBot.Infrastructure/Data/Configurations/RecipeIngredientConfiguration.cs` — FK + index convention template
- `src/CookBot.Infrastructure/Migrations/20260516032653_AddRecipePhotoUrlAndDescription.cs` — most recent column-add migration shape
- `src/CookBot.Application/Services/RecipeService.cs:268-280` — current `DeleteAsync` (no file cleanup — this is the gap)
- `src/CookBot.Application/Services/RecipeService.cs:48-56,172-183` — `PhotoUrl` write site in `CreateAsync`/`UpdateAsync`
- `src/CookBot.Infrastructure/AI/AnthropicAiService.cs` — `IAiService.SendMessageAsync` implementation (non-streaming text path)
- `src/CookBot.Domain/Interfaces/IAiService.cs` — `SendMessageAsync` signature
- `src/CookBot.Web/Components/Pages/RecipeEditorParts/RecipePhotoComposite.razor` — existing single-photo upload + paste-URL composite (sequential foreach pattern template, per-file size check)
- `src/CookBot.Web/Components/Pages/RecipeView.razor:133-150` — hero `<img>` hardening pattern (`referrerpolicy`, `loading="lazy"`, one-shot `@onerror`)
- `src/CookBot.Web/Components/Pages/RecipeEditor.razor:519-521` — AI gate `_aiOn = hostOn && userOn`
- `src/CookBot.Application/DTOs/CookBotSettings.cs:25-26` — `DatabaseBackupRetention` clamped-int precedent for `MaxPhotosPerRecipe`
- `src/CookBot.Infrastructure/Data/DatabaseSeeder.cs` — startup migration flow (`MigrateAsync` at step 2)
- `src/CookBot.Web/Program.cs:34` — `MaximumReceiveMessageSize = 12 MB`
- `src/CookBot.Infrastructure/Data/CookBotDbContext.cs` — `ApplyConfigurationsFromAssembly` pattern; no `RecipePhotos` DbSet yet (confirms this is greenfield)

### Secondary (HIGH confidence — official Microsoft documentation)
- [ASP.NET Core Blazor file uploads — Microsoft Learn](https://learn.microsoft.com/en-us/aspnet/core/blazor/file-uploads?view=aspnetcore-10.0) — `GetMultipleFiles(maxAllowedFiles)` pattern, `OpenReadStream(maxAllowedSize)` semantics, sequential foreach recommendation [CITED]
- [EF Core Managing Migrations — Microsoft Learn](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/managing) — `migrationBuilder.Sql()` for data backfill in `Up()` [CITED]

### Tertiary (MEDIUM confidence — issue trackers / web)
- [dotnet/aspnetcore#42993](https://github.com/dotnet/aspnetcore/issues/42993) — InputFile multi-file manifest vs. SignalR `MaximumReceiveMessageSize` (confirms P14 pitfall) [CITED]
- CDN HEAD→405→ranged-GET fallback pattern [ASSUMED — widely observed in practice, not from a single authoritative source]

---

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — all pre-existing packages; no new NuGet
- Architecture: HIGH — all patterns verified against live codebase files
- Sequential upload pattern: HIGH — confirmed against official Blazor docs + aspnetcore issue tracker
- HEAD-validation + CDN 405 fallback: MEDIUM — CDN 405 behavior is ASSUMED (widely observed, not from an official CDN spec)
- AI helper prompt shape: MEDIUM — wording is Claude's Discretion; structural constraint is HIGH

**Research date:** 2026-06-07
**Valid until:** 2026-07-07 (stable .NET 10 + EF Core 10 ecosystem; Blazor SignalR behavior is stable)

---

## RESEARCH COMPLETE
