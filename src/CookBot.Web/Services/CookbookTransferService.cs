using System.Text.Json;
using System.Text.Json.Nodes;
using CookBot.Application.DTOs;
using CookBot.Application.Recipes;
using CookBot.Application.Services;
using CookBot.Domain.Entities;
using CookBot.Domain.Interfaces;
using CookBot.Domain.Recipes;
using CookBot.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CookBot.Web.Services;

public sealed class CookbookTransferService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    private readonly CookBotDbContext _db;
    private readonly CookbookService _cookbookService;
    private readonly RecipeService _recipeService;
    private readonly RecipeUpcasterChain _upcasterChain;
    private readonly RecipeValidator _validator;

    public CookbookTransferService(
        CookBotDbContext db,
        CookbookService cookbookService,
        RecipeService recipeService,
        RecipeUpcasterChain upcasterChain,
        RecipeValidator validator)
    {
        _db = db;
        _cookbookService = cookbookService;
        _recipeService = recipeService;
        _upcasterChain = upcasterChain;
        _validator = validator;
    }

    public async Task<CookbookTransferDocument?> BuildExportAsync(int cookbookId, int userId,
        CancellationToken cancellationToken = default)
    {
        var cookbook = await _db.Cookbooks
            .AsNoTracking()
            .Include(c => c.Recipes).ThenInclude(r => r.RecipeIngredients).ThenInclude(ri => ri.Ingredient)
            .FirstOrDefaultAsync(c => c.Id == cookbookId, cancellationToken);

        if (cookbook == null) return null;

        if (!await CanAccessAsync(cookbook, userId, cancellationToken))
            return null;

        var doc = new CookbookTransferDocument
        {
            ExportedAt = DateTime.UtcNow.ToString("O"),
            Cookbook = new CookbookTransferCookbook
            {
                Name = cookbook.Name,
                Description = cookbook.Description,
            },
        };

        foreach (var recipe in cookbook.Recipes.OrderByDescending(r => r.UpdatedAt))
        {
            List<string> tags;
            try
            {
                tags = JsonSerializer.Deserialize<List<string>>(recipe.TagsJson) ?? new();
            }
            catch
            {
                tags = new();
            }

            var tr = new CookbookTransferRecipe
            {
                Name = recipe.Name,
                Servings = recipe.Servings,
                PrepTimeMinutes = recipe.PrepTimeMinutes,
                CookTimeMinutes = recipe.CookTimeMinutes,
                Tags = tags,
            };

            foreach (var ri in recipe.RecipeIngredients.OrderBy(x => x.RecipeLocalId))
            {
                tr.Ingredients.Add(new CookbookTransferIngredient
                {
                    LocalId = ri.RecipeLocalId,
                    Name = ri.Ingredient.Name,
                    Amount = ri.Amount,
                    Unit = ri.Unit,
                    Note = ri.Note,
                });
            }

            foreach (var step in recipe.Steps.OrderBy(s => s.Order))
            {
                tr.Steps.Add(new CookbookTransferStep
                {
                    Text = step.Text,
                    IsSection = step.IsSection,
                    Timers = step.Timers.Count == 0
                        ? null
                        : step.Timers.Select(t => new CookbookTransferTimer
                        {
                            Duration = t.Duration,
                            Unit = t.Unit,
                            Label = t.Label,
                        }).ToList(),
                });
            }

            doc.Recipes.Add(tr);
        }

        return doc;
    }

    public static byte[] SerializeToUtf8Json(CookbookTransferDocument document) =>
        JsonSerializer.SerializeToUtf8Bytes(document, JsonOptions);

    /// <summary>
    /// MIGRATION-04 — parses an envelope, then per-recipe stamps version, routes through
    /// the upcaster chain, deserializes to <see cref="RecipeDocument"/>, and runs the
    /// semantic validator. Per-recipe errors are collected with index/name prefixes; the
    /// envelope is returned even when some recipes fail validation so the caller can show
    /// partial-success UI. Envelope-level failures (malformed JSON, unsupported schema
    /// version) return null.
    ///
    /// Implementation note: per-recipe upcasting must operate on the raw JsonNode from
    /// the input string (not the round-tripped DTO), because the DTO models the v1 wire
    /// shape (<c>localId</c>, <c>isSection</c>) and would silently drop v2-only fields
    /// (<c>id</c>, <c>kind</c>, <c>heading</c>) on a v2 envelope.
    /// </summary>
    public CookbookTransferDocument? Deserialize(string json, out List<string> errors)
    {
        errors = new List<string>();

        // 1. Parse to JsonNode AND DTO. The JsonNode preserves whatever shape was sent
        //    (v1 or v2); the DTO is used to compose the legacy ImportAsNewCookbookAsync
        //    output and for envelope-level metadata.
        JsonNode? root;
        CookbookTransferDocument? envelope;
        try
        {
            root = JsonNode.Parse(json);
            envelope = JsonSerializer.Deserialize<CookbookTransferDocument>(json, JsonOptions);
        }
        catch (Exception ex)
        {
            errors.Add($"Invalid JSON: {ex.Message}");
            return null;
        }

        if (envelope is null || root is null)
        {
            errors.Add("File was empty or unreadable.");
            return null;
        }

        // 2. Accept SchemaVersion in {1, 2} — Phase 2 supports v2 envelopes too.
        if (envelope.SchemaVersion is not (1 or 2))
        {
            errors.Add($"Unsupported schema version: {envelope.SchemaVersion}. Only v1 and v2 are supported.");
            return null;
        }

        if (string.IsNullOrWhiteSpace(envelope.Cookbook.Name))
        {
            errors.Add("Cookbook name is required.");
        }

        // 3. Per-recipe: stamp version -> upcast -> deserialize -> validate.
        //    Operate on raw nodes from the input so v2 fields aren't lost via the v1-shaped DTO.
        var envelopeVersionForStamping = envelope.SchemaVersion; // 1 or 2
        var rawRecipes = root["recipes"] as JsonArray;

        for (var i = 0; i < envelope.Recipes.Count; i++)
        {
            var recipeDto = envelope.Recipes[i];
            if (string.IsNullOrWhiteSpace(recipeDto.Name))
            {
                errors.Add($"Recipe #{i + 1} is missing a name.");
                continue;
            }

            try
            {
                // Take the original (untouched) recipe node from the parsed envelope. If for
                // some reason the array shape mismatches, fall back to serializing the DTO.
                JsonNode node;
                if (rawRecipes is not null && i < rawRecipes.Count && rawRecipes[i] is JsonNode rawNode)
                {
                    node = rawNode.DeepClone();
                }
                else
                {
                    node = JsonSerializer.SerializeToNode(recipeDto, JsonOptions)
                        ?? throw new InvalidOperationException("Recipe serialized to null JsonNode");
                }

                // Stamp version from the envelope if the per-recipe `version` field is absent.
                // Phase 1 invariant (FORMAT-08 / RecipeUpcasterChain): the chain reads `version`
                // off the node. If the v1 export omitted it, stamp to envelopeVersionForStamping.
                if (node["version"] is null)
                {
                    node["version"] = envelopeVersionForStamping;
                }

                var upcasted = _upcasterChain.UpcastToCurrent(node);
                var doc = JsonSerializer.Deserialize<RecipeDocument>(upcasted.ToJsonString(), JsonOptions);
                if (doc is null)
                {
                    errors.Add($"Recipe #{i + 1} ({recipeDto.Name}): could not deserialize to canonical document.");
                    continue;
                }

                var validation = _validator.Validate(doc);
                if (!validation.IsValid)
                {
                    var msgs = string.Join("; ", validation.Errors.Select(e => $"{e.Path}: {e.Message}"));
                    errors.Add($"Recipe #{i + 1} ({recipeDto.Name}): {msgs}");
                }
            }
            catch (Exception ex)
            {
                errors.Add($"Recipe #{i + 1} ({recipeDto.Name}): upcast/deserialize failed — {ex.Message}");
            }
        }

        // Always return the envelope (even with per-recipe errors) — caller decides what to import.
        return envelope;
    }

    public async Task<int> ImportAsNewCookbookAsync(int userId, CookbookTransferDocument doc,
        string? overrideName = null, CancellationToken cancellationToken = default)
    {
        var name = string.IsNullOrWhiteSpace(overrideName) ? doc.Cookbook.Name.Trim() : overrideName.Trim();
        var cookbook = await _cookbookService.CreateAsync(userId, name, doc.Cookbook.Description);

        foreach (var tr in doc.Recipes)
        {
            var parsed = ToParsedRecipe(tr);
            await _recipeService.CreateAsync(cookbook.Id, userId, parsed);
        }

        return cookbook.Id;
    }

    private static ParsedRecipe ToParsedRecipe(CookbookTransferRecipe tr)
    {
        var parsed = new ParsedRecipe
        {
            Name = tr.Name.Trim(),
            Servings = tr.Servings < 1 ? 1 : tr.Servings,
            PrepTimeMinutes = tr.PrepTimeMinutes,
            CookTimeMinutes = tr.CookTimeMinutes,
            Tags = tr.Tags?.Where(t => !string.IsNullOrWhiteSpace(t)).Select(t => t.Trim()).ToList() ?? new(),
        };

        foreach (var ing in tr.Ingredients.OrderBy(i => i.LocalId))
        {
            parsed.Ingredients.Add(new ParsedIngredient
            {
                LocalId = ing.LocalId,
                Name = ing.Name.Trim(),
                Amount = ing.Amount,
                Unit = ing.Unit ?? "",
                Note = string.IsNullOrWhiteSpace(ing.Note) ? null : ing.Note.Trim(),
            });
        }

        foreach (var st in tr.Steps)
        {
            ParsedStep ps = new()
            {
                Text = st.Text ?? "",
                IsSection = st.IsSection,
            };
            if (st.Timers is { Count: > 0 })
            {
                ps.Timers = st.Timers.Select(t => new ParsedTimer
                {
                    Duration = t.Duration,
                    Unit = string.IsNullOrEmpty(t.Unit) ? "min" : t.Unit,
                    Label = t.Label,
                }).ToList();
            }

            parsed.Steps.Add(ps);
        }

        return parsed;
    }

    private async Task<bool> CanAccessAsync(Cookbook cookbook, int userId, CancellationToken cancellationToken)
    {
        if (cookbook.UserId == userId)
            return true;

        return await _db.CookbookShares.AsNoTracking()
            .AnyAsync(s => s.CookbookId == cookbook.Id && s.SharedWithUserId == userId, cancellationToken);
    }
}
