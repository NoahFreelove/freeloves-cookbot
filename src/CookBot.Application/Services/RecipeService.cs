using System.Security.Cryptography;
using System.Text;
using CookBot.Application.Recipes;
using CookBot.Domain.Entities;
using CookBot.Domain.Interfaces;
using CookBot.Domain.Recipes;
using Microsoft.Extensions.Logging;

namespace CookBot.Application.Services;

public class RecipeService
{
    private readonly IRecipeFormatParser _parser;
    private readonly IRepository<Recipe> _recipeRepo;
    private readonly IRepository<Ingredient> _ingredientRepo;
    private readonly IRepository<Cookbook> _cookbookRepo;
    private readonly IRepository<RecipeTag> _recipeTagRepo;
    private readonly IRepository<RecipePhoto> _recipePhotoRepo;
    private readonly IRepository<RecipeNutritionCache> _nutritionCacheRepo;
    private readonly IRecipePhotoFileStorage _photoStorage;
    private readonly JsonRecipeSerializer _canonicalSerializer;
    private readonly ILogger<RecipeService> _logger;

    public RecipeService(
        IRecipeFormatParser parser,
        IRepository<Recipe> recipeRepo,
        IRepository<Ingredient> ingredientRepo,
        IRepository<Cookbook> cookbookRepo,
        IRepository<RecipeTag> recipeTagRepo,
        IRepository<RecipePhoto> recipePhotoRepo,
        IRepository<RecipeNutritionCache> nutritionCacheRepo,
        IRecipePhotoFileStorage photoStorage,
        JsonRecipeSerializer canonicalSerializer,
        ILogger<RecipeService> logger)
    {
        _parser = parser;
        _recipeRepo = recipeRepo;
        _ingredientRepo = ingredientRepo;
        _cookbookRepo = cookbookRepo;
        _recipeTagRepo = recipeTagRepo;
        _recipePhotoRepo = recipePhotoRepo;
        _nutritionCacheRepo = nutritionCacheRepo;
        _photoStorage = photoStorage;
        _canonicalSerializer = canonicalSerializer;
        _logger = logger;
    }

    public async Task<Recipe> CreateAsync(int cookbookId, int userId, ParsedRecipe parsed)
    {
        var cookbook = await _cookbookRepo.GetByIdAsync(cookbookId)
            ?? throw new InvalidOperationException("Cookbook not found.");

        if (cookbook.UserId != userId)
            throw new UnauthorizedAccessException("You do not own this cookbook.");

        var recipe = new Recipe
        {
            CookbookId = cookbookId,
            Name = parsed.Name,
            Servings = parsed.Servings,
            PrepTimeMinutes = parsed.PrepTimeMinutes,
            CookTimeMinutes = parsed.CookTimeMinutes,
            // Phase 9 / Plan 09-02 — write the new v3 columns to the Recipe
            // entity (Phase 8 SCHEMA-05/06 added the columns; Phase 9 wires
            // the editor → persistence path). The canonical doc round-trip
            // below ALSO writes PhotoUrl/Description into the canonical JSON,
            // so the SQL column reads and the canonical doc reads stay in
            // lockstep on every save (v1.1 canonical-first invariant).
            PhotoUrl = parsed.PhotoUrl,
            Description = parsed.Description,
        };

        // CLEAN-02 (Plan 11): relational RecipeTag rows are the sole tag persistence path (D-26 finalized).
        // D-34: trim whitespace, preserve case ("Vegan"/"vegan" are distinct tags).
        // NOTE: Callers that READ tags via Recipe.Tags must .Include(r => r.Tags) on the Recipe query.
        // CreateAsync: new entity — Tags collection starts empty, Add works directly without Include.
        foreach (var name in parsed.Tags.Select(t => t.Trim()).Where(t => t.Length > 0))
        {
            recipe.Tags.Add(new RecipeTag { Name = name });
        }

        foreach (var pi in parsed.Ingredients)
        {
            var ingredient = await ResolveIngredientAsync(pi.Name);
            recipe.RecipeIngredients.Add(new RecipeIngredient
            {
                IngredientId = ingredient.Id,
                RecipeLocalId = pi.LocalId,
                Amount = pi.Amount,
                Unit = pi.Unit,
                Note = pi.Note,
            });
        }

        int order = 0;
        foreach (var ps in parsed.Steps)
        {
            var step = new RecipeStep
            {
                Order = order++,
                Text = ps.Text,
                IsSection = ps.IsSection,
                Timers = ps.IsSection
                    ? new()
                    : (ps.Timers ?? new()).Select(t => new StepTimer { Duration = t.Duration, Unit = t.Unit, Label = t.Label }).ToList(),
                // Plan 03-04 / EDITOR-03 final clause: explicit timer chips are the only
                // persisted source. The previous regex-based auto-write fallback (which
                // silently produced timer entries from step text like "Cook 25 minutes")
                // is removed — surfacing detections is now the inline-suggestion popover's
                // job; persistence requires the user to accept a chip.
                //
                // Plan 01-02 / D-13: writes to RecipeStep.IngredientRefs are retired this
                // milestone. The column persists for safe rollback; Phase 4 drops it.
                // Cooking-mode highlighting now resolves [name](#id) links at render time.
            };
            recipe.Steps.Add(step);
        }

        // MIGRATION-03 hybrid persistence: relational columns continue to be written;
        // canonical document JSON is recomputed on every save (Plan 01-03 / D-12).
        // CLEAN-01 (Plan 10 / D-32 step b): direct RecipeDocument construction from parsed.
        // NOTE: Callers that READ tags via Recipe.Tags must .Include(r => r.Tags) on the Recipe query.
        var canonicalDoc = new RecipeDocument
        {
            Version = RecipeUpcasterChain.CurrentVersion,
            Name = parsed.Name,
            Servings = parsed.Servings,
            PrepTimeMinutes = parsed.PrepTimeMinutes,
            CookTimeMinutes = parsed.CookTimeMinutes,
            PhotoUrl = parsed.PhotoUrl,
            Description = parsed.Description,
            Tags = recipe.Tags.Select(t => t.Name).ToList(),
            Equipment = parsed.Equipment.ToList(),
            Provenance = parsed.Provenance,
            Ingredients = parsed.Ingredients.Select(i => new IngredientEntry { Id = i.LocalId, Name = i.Name, Amount = i.Amount, Unit = i.Unit, Note = i.Note, Substitutions = i.Substitutions.ToList() }).ToList(),
            Steps = parsed.Steps.Select<ParsedStep, StepNode>(s => s.IsSection
                ? new SectionStep { Heading = s.Text }
                : new ContentStep
                {
                    Text = s.Text,
                    Timers = s.Timers?.Select(t => new TimerEntry { Duration = t.Duration, Unit = t.Unit, Label = t.Label }).ToList(),
                    Temperature = s.Temperature,
                    DonenessCue = s.DonenessCue,
                }).ToList(),
        };
        recipe.CanonicalDocumentJson = _canonicalSerializer.Serialize(canonicalDoc);

        // Phase 15 / NUTR-02 / SC1 — stale-mark on canonical write.
        // On CreateAsync there is normally no cache yet (no-op), but the code
        // path is uniform so future pre-seeded cache rows are handled correctly.
        // CRITICAL: NEVER call the nutrition compute service here — save must not block on nutrition (P7).
        await MarkNutritionCacheStaleIfChangedAsync(recipe);

        return await _recipeRepo.AddAsync(recipe);
    }

    public async Task<Recipe> CreateFromTextAsync(int cookbookId, int userId, string rawInput)
    {
        var parsed = _parser.Parse(rawInput);
        return await CreateAsync(cookbookId, userId, parsed);
    }

    /// <summary>
    /// Phase 10 / Plan 10-10 / POLISH-01 — <paramref name="newCookbookId"/> allows the caller to
    /// reparent the recipe to a different cookbook the user owns. When <paramref name="newCookbookId"/>
    /// is non-null and differs from the recipe's current <c>CookbookId</c>, destination ownership is
    /// validated via an inline check (PATTERNS.md correction #5 — <c>db.UserCanAccessCookbookAsync</c>
    /// does not exist in this codebase; use the inline pattern from <c>CreateAsync</c>).
    /// </summary>
    public async Task<Recipe> UpdateAsync(int recipeId, int userId, ParsedRecipe parsed, int? newCookbookId = null)
    {
        var recipe = await _recipeRepo.GetByIdAsync(recipeId)
            ?? throw new InvalidOperationException("Recipe not found.");

        var cookbook = await _cookbookRepo.GetByIdAsync(recipe.CookbookId)
            ?? throw new InvalidOperationException("Cookbook not found.");

        if (cookbook.UserId != userId)
            throw new UnauthorizedAccessException("You do not own this cookbook.");

        // Phase 10 / Plan 10-10 / POLISH-01 — reparent block (PATTERNS.md correction #5).
        // Inline destination-ownership check mirrors the CreateAsync pattern at lines 35-39.
        // T-10-10-01: cross-user reparenting throws UnauthorizedAccessException BEFORE assignment.
        if (newCookbookId.HasValue && newCookbookId.Value != recipe.CookbookId)
        {
            var destination = await _cookbookRepo.GetByIdAsync(newCookbookId.Value)
                ?? throw new InvalidOperationException("Destination cookbook not found.");
            if (destination.UserId != userId)
                throw new UnauthorizedAccessException("You do not own the destination cookbook.");
            recipe.CookbookId = newCookbookId.Value;
        }

        recipe.Name = parsed.Name;
        recipe.Servings = parsed.Servings;
        recipe.PrepTimeMinutes = parsed.PrepTimeMinutes;
        recipe.CookTimeMinutes = parsed.CookTimeMinutes;
        // Phase 9 / Plan 09-02 — write the new v3 columns into the loaded
        // Recipe entity (Phase 8 SCHEMA-05/06 added the columns; Phase 9 wires
        // the editor → persistence path). The canonical doc round-trip below
        // ALSO writes PhotoUrl/Description into the canonical JSON, so the SQL
        // column reads and the canonical doc reads stay in lockstep on every
        // save (v1.1 canonical-first invariant).
        recipe.PhotoUrl = parsed.PhotoUrl;
        recipe.Description = parsed.Description;
        recipe.UpdatedAt = DateTime.UtcNow;

        // CLEAN-02 (Plan 11): relational RecipeTag rows are the sole tag persistence path (D-26 finalized).
        // D-34: trim whitespace, preserve case. Clear existing tags first.
        // If Tags nav is loaded (via change tracker from caller's .Include(r => r.Tags)), Clear() issues
        // EF DELETE commands. For robustness, also explicitly delete via _recipeTagRepo.
        var existingTags = await _recipeTagRepo.FindAsync(t => t.RecipeId == recipe.Id);
        foreach (var tag in existingTags)
            await _recipeTagRepo.DeleteAsync(tag);

        recipe.Tags.Clear();
        foreach (var name in parsed.Tags.Select(t => t.Trim()).Where(t => t.Length > 0))
        {
            recipe.Tags.Add(new RecipeTag { Name = name });
        }

        recipe.RecipeIngredients.Clear();
        foreach (var pi in parsed.Ingredients)
        {
            var ingredient = await ResolveIngredientAsync(pi.Name);
            recipe.RecipeIngredients.Add(new RecipeIngredient
            {
                RecipeId = recipe.Id,
                IngredientId = ingredient.Id,
                RecipeLocalId = pi.LocalId,
                Amount = pi.Amount,
                Unit = pi.Unit,
                Note = pi.Note,
            });
        }

        recipe.Steps.Clear();
        int order = 0;
        foreach (var ps in parsed.Steps)
        {
            var step = new RecipeStep
            {
                Order = order++,
                Text = ps.Text,
                IsSection = ps.IsSection,
                Timers = ps.IsSection
                    ? new()
                    : (ps.Timers ?? new()).Select(t => new StepTimer { Duration = t.Duration, Unit = t.Unit, Label = t.Label }).ToList(),
                // Plan 03-04 / EDITOR-03 final clause: explicit timer chips only. See
                // CreateAsync above for the full rationale.
                //
                // Plan 01-02 / D-13: writes to RecipeStep.IngredientRefs are retired this
                // milestone. See comment in CreateAsync above.
            };
            recipe.Steps.Add(step);
        }

        // MIGRATION-03 hybrid persistence: recompute canonical document on every save.
        // CLEAN-01 (Plan 10 / D-32 step b): direct RecipeDocument construction from parsed.
        // NOTE: Callers that READ tags via Recipe.Tags must .Include(r => r.Tags) on the Recipe query.
        var canonicalDoc = new RecipeDocument
        {
            Version = RecipeUpcasterChain.CurrentVersion,
            Name = parsed.Name,
            Servings = parsed.Servings,
            PrepTimeMinutes = parsed.PrepTimeMinutes,
            CookTimeMinutes = parsed.CookTimeMinutes,
            PhotoUrl = parsed.PhotoUrl,
            Description = parsed.Description,
            Tags = recipe.Tags.Select(t => t.Name).ToList(),
            Equipment = parsed.Equipment.ToList(),
            Provenance = parsed.Provenance,
            Ingredients = parsed.Ingredients.Select(i => new IngredientEntry { Id = i.LocalId, Name = i.Name, Amount = i.Amount, Unit = i.Unit, Note = i.Note, Substitutions = i.Substitutions.ToList() }).ToList(),
            Steps = parsed.Steps.Select<ParsedStep, StepNode>(s => s.IsSection
                ? new SectionStep { Heading = s.Text }
                : new ContentStep
                {
                    Text = s.Text,
                    Timers = s.Timers?.Select(t => new TimerEntry { Duration = t.Duration, Unit = t.Unit, Label = t.Label }).ToList(),
                    Temperature = s.Temperature,
                    DonenessCue = s.DonenessCue,
                }).ToList(),
        };
        recipe.CanonicalDocumentJson = _canonicalSerializer.Serialize(canonicalDoc);

        // Phase 15 / NUTR-02 / SC1 — stale-mark on canonical write.
        // CRITICAL: NEVER call the nutrition compute service here — save must not block on nutrition (P7).
        await MarkNutritionCacheStaleIfChangedAsync(recipe);

        await _recipeRepo.UpdateAsync(recipe);
        return recipe;
    }

    public async Task DeleteAsync(int recipeId, int userId)
    {
        var recipe = await _recipeRepo.GetByIdAsync(recipeId)
            ?? throw new InvalidOperationException("Recipe not found.");

        var cookbook = await _cookbookRepo.GetByIdAsync(recipe.CookbookId)
            ?? throw new InvalidOperationException("Cookbook not found.");

        if (cookbook.UserId != userId)
            throw new UnauthorizedAccessException("You do not own this cookbook.");

        // D-14-11 / P13: enumerate local-path photos BEFORE cascade deletes the rows.
        // External http(s):// URLs have no local file — only /uploads/ paths are touched.
        var photos = await _recipePhotoRepo.FindAsync(p => p.RecipeId == recipeId);
        foreach (var photo in photos)
        {
            if (photo.Url.StartsWith("/uploads/", StringComparison.Ordinal))
            {
                try
                {
                    _photoStorage.DeletePhysicalFile(photo.Url);
                }
                catch (Exception ex)
                {
                    // Non-fatal — log and continue (D-14-11: missing-file deletes are non-fatal)
                    _logger.LogWarning(ex, "Could not delete photo file {Url} during recipe delete", photo.Url);
                }
            }
        }

        await _recipeRepo.DeleteAsync(recipe); // EF cascade removes RecipePhoto rows
    }

    /// <summary>
    /// Re-syncs <c>Recipe.PhotoUrl</c> and <c>CanonicalDocumentJson</c> to the primary
    /// <see cref="RecipePhoto"/> after every gallery mutation (D-14-01 / P15).
    /// This is the ONLY place that writes <c>Recipe.PhotoUrl</c> / <c>CanonicalDocumentJson</c>
    /// for gallery-driven changes — projectors and photo services never touch canonical.
    /// </summary>
    public async Task SyncPrimaryPhotoUrlAsync(int recipeId)
    {
        var recipe = await _recipeRepo.GetByIdAsync(recipeId)
            ?? throw new InvalidOperationException("Recipe not found during photo sync.");

        // Find the primary photo; fall back to lowest SortOrder if none is flagged (defensive)
        var allPhotos = await _recipePhotoRepo.FindAsync(p => p.RecipeId == recipeId);
        var primary = allPhotos.FirstOrDefault(p => p.IsPrimary)
            ?? allPhotos.OrderBy(p => p.SortOrder).FirstOrDefault();

        recipe.PhotoUrl = primary?.Url;

        // Re-serialize canonical doc with the updated PhotoUrl (same pattern as CreateAsync/UpdateAsync)
        // Defensive fallback: if CanonicalDocumentJson is null (pre-migration recipe), use an empty doc
        var doc = string.IsNullOrEmpty(recipe.CanonicalDocumentJson)
            ? new CookBot.Domain.Recipes.RecipeDocument { Version = RecipeUpcasterChain.CurrentVersion, Name = recipe.Name, Servings = recipe.Servings }
            : _canonicalSerializer.Deserialize(recipe.CanonicalDocumentJson);
        recipe.CanonicalDocumentJson = _canonicalSerializer.Serialize(doc with { PhotoUrl = recipe.PhotoUrl });

        // Phase 15 / NUTR-02 / SC1 — stale-mark on canonical write (photo-URL change).
        // A photo-URL-only change is still a hash change → mark stale for correctness.
        // CRITICAL: NEVER call the nutrition compute service here — save must not block on nutrition (P7).
        await MarkNutritionCacheStaleIfChangedAsync(recipe);

        await _recipeRepo.UpdateAsync(recipe);
    }

    /// <summary>
    /// Computes the SHA-256 hex digest of the recipe's current CanonicalDocumentJson
    /// and marks an existing <see cref="RecipeNutritionCache"/> stale when the hash changed.
    /// If no cache row exists, this is a cheap no-op.
    ///
    /// <b>NEVER calls the nutrition compute service</b> — only writes a
    /// staleness flag via IRepository&lt;RecipeNutritionCache&gt; (SC1/P7).
    /// </summary>
    private async Task MarkNutritionCacheStaleIfChangedAsync(Recipe recipe)
    {
        var canonicalJson = recipe.CanonicalDocumentJson;
        if (string.IsNullOrEmpty(canonicalJson))
            return;

        // BCL SHA-256 — no new NuGet package; zero-alloc HashData (BCL API, .NET 5+).
        var newHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalJson)));

        // recipe.Id == 0 when called from CreateAsync (before AddAsync assigns the PK).
        // In that case there is no cache row yet, so the lookup is guaranteed a no-op.
        if (recipe.Id == 0)
            return;

        var caches = await _nutritionCacheRepo.FindAsync(c => c.RecipeId == recipe.Id);
        var cache = caches.FirstOrDefault();
        if (cache is null)
            return;

        if (cache.CanonicalDocHash != newHash)
        {
            cache.IsStale           = true;
            cache.CanonicalDocHash  = newHash;
            // Do NOT call _nutritionCacheRepo.UpdateAsync(cache) here — that would call
            // SaveChangesAsync on the shared DbContext and flush the in-flight recipe
            // mutation before _recipeRepo.UpdateAsync(recipe) is called (CR-01).
            // The cache entity is already tracked; the single SaveChangesAsync inside
            // _recipeRepo.UpdateAsync below commits both atomically.
        }
    }

    private async Task<Ingredient> ResolveIngredientAsync(string name)
    {
        var normalized = IngredientResolver.Normalize(name);
        var existing = await _ingredientRepo.FindAsync(i => i.NormalizedName == normalized);
        if (existing.Any())
            return existing.First();

        return await _ingredientRepo.AddAsync(new Ingredient
        {
            Name = name,
            NormalizedName = normalized,
        });
    }
}
