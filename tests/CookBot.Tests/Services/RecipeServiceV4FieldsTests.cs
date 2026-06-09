using CookBot.Application.Recipes;
using CookBot.Application.Services;
using CookBot.Domain.Entities;
using CookBot.Domain.Interfaces;
using CookBot.Domain.Recipes;
using CookBot.Infrastructure.Data;
using CookBot.Infrastructure.Data.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace CookBot.Tests.Services;

/// <summary>
/// Regression tests for CR-01 / CR-02 (Phase 12): proves that the four v4 field groups
/// (equipment, provenance, per-ingredient substitutions, per-step doneness cue) survive the
/// full RecipeService.CreateAsync → DB → reload → CanonicalDocumentJson → Deserialize path.
/// This exercises the production save path that was not covered by the parser-only round-trip
/// tests in RecipeRoundTripTests, and was the gap that allowed CR-01 to ship.
/// </summary>
public class RecipeServiceV4FieldsTests : IDisposable
{
    private readonly CookBotDbContext _db;
    private readonly RecipeService _service;
    private readonly JsonRecipeSerializer _serializer;
    private readonly int _userId;
    private readonly int _cookbookId;

    public RecipeServiceV4FieldsTests()
    {
        var options = new DbContextOptionsBuilder<CookBotDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;
        _db = new CookBotDbContext(options);
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();

        _serializer = new JsonRecipeSerializer();

        var user = new User { DisplayName = "TestUser" };
        _db.Users.Add(user);
        _db.SaveChanges();
        _userId = user.Id;

        var cookbook = new Cookbook { UserId = _userId, Name = "Test Cookbook" };
        _db.Cookbooks.Add(cookbook);
        _db.SaveChanges();
        _cookbookId = cookbook.Id;

        var recipeRepo = new Repository<Recipe>(_db);
        var ingredientRepo = new Repository<Ingredient>(_db);
        var cookbookRepo = new Repository<Cookbook>(_db);
        var recipeTagRepo = new Repository<RecipeTag>(_db);
        var recipePhotoRepo = new Repository<RecipePhoto>(_db);
        var nutritionCacheRepo = new Repository<RecipeNutritionCache>(_db);

        _service = new RecipeService(
            new StubParser(),
            recipeRepo,
            ingredientRepo,
            cookbookRepo,
            recipeTagRepo,
            recipePhotoRepo,
            nutritionCacheRepo,
            new NullPhotoFileStorage(),
            _serializer,
            NullLogger<RecipeService>.Instance);
    }

    // ---- helper -----------------------------------------------------------------

    private static ParsedRecipe BuildV4ParsedRecipe() => new()
    {
        Name = "V4 Regression Cake",
        Servings = 6,
        Equipment = ["stand mixer", "bundt pan"],
        Provenance = new RecipeProvenance
        {
            SourceName = "The Test Kitchen",
            AuthorName = "Jane Tester",
            SourceUrl = "https://example.com/v4-cake",
        },
        Ingredients =
        [
            new ParsedIngredient
            {
                LocalId = 1,
                Name = "All-purpose flour",
                Amount = 250,
                Unit = "g",
                Substitutions =
                [
                    new IngredientSubstitution { Note = "gluten-free blend works too", Name = "GF blend", Amount = 240, Unit = "g" },
                ],
            },
            new ParsedIngredient
            {
                LocalId = 2,
                Name = "Butter",
                Amount = 100,
                Unit = "g",
                // no substitutions — verifies empty list survives round-trip
            },
        ],
        Steps =
        [
            new ParsedStep
            {
                Text = "Mix dry ingredients.",
                IsSection = false,
                DonenessCue = "well combined and no clumps",
            },
            new ParsedStep
            {
                Text = "Bake until done.",
                IsSection = false,
                DonenessCue = "golden brown and toothpick comes out clean",
            },
        ],
        Tags = ["dessert"],
    };

    // ---- full-service round-trip tests ------------------------------------------

    [Fact]
    public async Task CreateAsync_Equipment_SurvivesCanonicalDocRoundTrip()
    {
        // Arrange
        var parsed = BuildV4ParsedRecipe();

        // Act
        var created = await _service.CreateAsync(_cookbookId, _userId, parsed);

        // Reload from DB to verify CanonicalDocumentJson was written
        var reloaded = await _db.Recipes.FindAsync(created.Id);
        Assert.NotNull(reloaded);
        Assert.False(string.IsNullOrEmpty(reloaded!.CanonicalDocumentJson),
            "CanonicalDocumentJson must not be empty after CreateAsync");

        var doc = _serializer.Deserialize(reloaded.CanonicalDocumentJson!);

        // Assert — equipment
        Assert.Equal(2, doc.Equipment.Count);
        Assert.Equal("stand mixer", doc.Equipment[0]);
        Assert.Equal("bundt pan", doc.Equipment[1]);
    }

    [Fact]
    public async Task CreateAsync_Provenance_SurvivesCanonicalDocRoundTrip()
    {
        var parsed = BuildV4ParsedRecipe();
        var created = await _service.CreateAsync(_cookbookId, _userId, parsed);

        var reloaded = await _db.Recipes.FindAsync(created.Id);
        var doc = _serializer.Deserialize(reloaded!.CanonicalDocumentJson!);

        Assert.NotNull(doc.Provenance);
        Assert.Equal("The Test Kitchen", doc.Provenance!.SourceName);
        Assert.Equal("Jane Tester", doc.Provenance!.AuthorName);
        Assert.Equal("https://example.com/v4-cake", doc.Provenance!.SourceUrl);
    }

    [Fact]
    public async Task CreateAsync_Substitutions_SurvivesCanonicalDocRoundTrip()
    {
        var parsed = BuildV4ParsedRecipe();
        var created = await _service.CreateAsync(_cookbookId, _userId, parsed);

        var reloaded = await _db.Recipes.FindAsync(created.Id);
        var doc = _serializer.Deserialize(reloaded!.CanonicalDocumentJson!);

        // Ingredient at Id=1 has 1 substitution
        var flour = doc.Ingredients.First(i => i.Id == 1);
        Assert.Single(flour.Substitutions);
        Assert.Equal("gluten-free blend works too", flour.Substitutions[0].Note);
        Assert.Equal("GF blend", flour.Substitutions[0].Name);
        Assert.Equal(240, flour.Substitutions[0].Amount);
        Assert.Equal("g", flour.Substitutions[0].Unit);

        // Ingredient at Id=2 has no substitutions
        var butter = doc.Ingredients.First(i => i.Id == 2);
        Assert.Empty(butter.Substitutions);
    }

    [Fact]
    public async Task CreateAsync_DonenessCue_SurvivesCanonicalDocRoundTrip()
    {
        var parsed = BuildV4ParsedRecipe();
        var created = await _service.CreateAsync(_cookbookId, _userId, parsed);

        var reloaded = await _db.Recipes.FindAsync(created.Id);
        var doc = _serializer.Deserialize(reloaded!.CanonicalDocumentJson!);

        var step0 = (ContentStep)doc.Steps[0];
        var step1 = (ContentStep)doc.Steps[1];
        Assert.Equal("well combined and no clumps", step0.DonenessCue);
        Assert.Equal("golden brown and toothpick comes out clean", step1.DonenessCue);
    }

    [Fact]
    public async Task UpdateAsync_AllV4Fields_SurviveCanonicalDocRoundTrip()
    {
        // Create first
        var initialParsed = new ParsedRecipe
        {
            Name = "Initial Recipe",
            Servings = 2,
            Ingredients = [new ParsedIngredient { LocalId = 1, Name = "Water", Amount = 1, Unit = "L" }],
            Steps = [new ParsedStep { Text = "Boil.", IsSection = false }],
        };
        var created = await _service.CreateAsync(_cookbookId, _userId, initialParsed);

        // Now update with all four v4 groups
        var updatedParsed = BuildV4ParsedRecipe();
        await _service.UpdateAsync(created.Id, _userId, updatedParsed);

        var reloaded = await _db.Recipes.FindAsync(created.Id);
        var doc = _serializer.Deserialize(reloaded!.CanonicalDocumentJson!);

        // Equipment
        Assert.Equal(2, doc.Equipment.Count);
        Assert.Equal("stand mixer", doc.Equipment[0]);

        // Provenance
        Assert.NotNull(doc.Provenance);
        Assert.Equal("Jane Tester", doc.Provenance!.AuthorName);

        // Substitutions
        var flour = doc.Ingredients.First(i => i.Id == 1);
        Assert.Single(flour.Substitutions);
        Assert.Equal("gluten-free blend works too", flour.Substitutions[0].Note);

        // DonenessCue
        var step0 = (ContentStep)doc.Steps[0];
        Assert.Equal("well combined and no clumps", step0.DonenessCue);
    }

    public void Dispose() => _db.Dispose();

    /// <summary>Minimal stub parser — not exercised in these service-path tests.</summary>
    private class StubParser : IRecipeFormatParser
    {
        public ParsedRecipe Parse(string rawContent) => new();
        public string Serialize(ParsedRecipe recipe) => "";
        public bool TryParse(string rawContent, out ParsedRecipe? recipe, out List<string> errors)
        {
            recipe = new ParsedRecipe();
            errors = new List<string>();
            return true;
        }
    }

    /// <summary>No-op file storage stub — tests don't exercise file I/O.</summary>
    private class NullPhotoFileStorage : CookBot.Application.Services.IRecipePhotoFileStorage
    {
        public void DeletePhysicalFile(string url) { }
    }
}
