using System.Text.Json;
using CookBot.Application.DTOs;
using CookBot.Application.Services;
using CookBot.Domain.Entities;
using CookBot.Domain.Interfaces;
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

    public CookbookTransferService(
        CookBotDbContext db,
        CookbookService cookbookService,
        RecipeService recipeService)
    {
        _db = db;
        _cookbookService = cookbookService;
        _recipeService = recipeService;
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

    public static CookbookTransferDocument? Deserialize(string json, out List<string> errors)
    {
        errors = new List<string>();
        CookbookTransferDocument? doc;
        try
        {
            doc = JsonSerializer.Deserialize<CookbookTransferDocument>(json, JsonOptions);
        }
        catch (Exception ex)
        {
            errors.Add($"Invalid JSON: {ex.Message}");
            return null;
        }

        if (doc == null)
        {
            errors.Add("File was empty or unreadable.");
            return null;
        }

        if (doc.SchemaVersion != 1)
            errors.Add($"Unsupported schema version: {doc.SchemaVersion} (expected 1).");

        if (string.IsNullOrWhiteSpace(doc.Cookbook.Name))
            errors.Add("Cookbook name is required.");

        for (var i = 0; i < doc.Recipes.Count; i++)
        {
            var r = doc.Recipes[i];
            if (string.IsNullOrWhiteSpace(r.Name))
                errors.Add($"Recipe #{i + 1} is missing a name.");
        }

        return errors.Count == 0 ? doc : null;
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
