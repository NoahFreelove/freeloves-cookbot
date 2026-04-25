using CookBot.Application.DTOs;
using CookBot.Application.Recipes;
using CookBot.Domain.Entities;
using CookBot.Domain.Recipes;
using CookBot.Infrastructure.Data;
using CookBot.Infrastructure.Data.Migrations.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace CookBot.Tests.Migration;

/// <summary>
/// MIGRATION-08 smoke test (D-25): in-memory SQLite seeded with three representative
/// recipes. Asserts <see cref="LegacyRecipeProjector"/> + <see cref="JsonRecipeSerializer"/>
/// + <see cref="RecipeValidator"/> round-trips with no value drift.
///
/// Plus the backup-file integration check covering RESEARCH Open Q4.
/// </summary>
public class CanonicalBackfillTests : IDisposable
{
    private readonly CookBotDbContext _db;
    private readonly LegacyRecipeProjector _projector = new();
    private readonly JsonRecipeSerializer _serializer = new();
    private readonly RecipeValidator _validator = new();

    public CanonicalBackfillTests()
    {
        var options = new DbContextOptionsBuilder<CookBotDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;
        _db = new CookBotDbContext(options);
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();
    }

    [Fact]
    public void Backfill_ThreeRecipes_RoundTripsWithoutValueDrift()
    {
        // Arrange: seed Cookbook + User; the helper-built recipes attach to CookbookId = cookbook.Id.
        var user = new User { DisplayName = "Test" };
        _db.Users.Add(user);
        _db.SaveChanges();
        var cookbook = new Cookbook { Name = "Test", UserId = user.Id };
        _db.Cookbooks.Add(cookbook);
        _db.SaveChanges();

        var simple = BuildRelationalRecipe(
            cookbookId: cookbook.Id,
            name: "Simple Pasta", servings: 4, prep: 10, cook: 15,
            ingredients: new (string, double, string, string?)[]
            {
                ("Pasta", 200.0, "g", null),
                ("Salt", 1, "tsp", null),
            },
            steps: new (string, bool, int[]?)[]
            {
                ("Boil water in a large pot.", false, null),
                ("Cook [pasta](#1) for 10 minutes.", false, new[] { 10 }),
            });

        var sectioned = BuildRelationalRecipe(
            cookbookId: cookbook.Id,
            name: "Sectioned Cake", servings: 8, prep: 25, cook: 40,
            ingredients: new (string, double, string, string?)[]
            {
                ("Flour", 250.0, "g", null),
                ("Sugar", 200.0, "g", null),
                ("Eggs", 3.0, "", null),
            },
            steps: new (string, bool, int[]?)[]
            {
                ("Wet ingredients", true, null),
                ("Mix [eggs](#3) with sugar.", false, null),
                ("Dry ingredients", true, null),
                ("Combine [flour](#1).", false, null),
            });

        var multiTimer = BuildRelationalRecipe(
            cookbookId: cookbook.Id,
            name: "Multi-Timer Bread", servings: 1, prep: 30, cook: 45,
            ingredients: new (string, double, string, string?)[]
            {
                ("Flour", 500.0, "g", null),
                ("Yeast", 7.0, "g", null),
                ("Water", 350.0, "ml", null),
            },
            steps: new (string, bool, int[]?)[]
            {
                ("Knead.", false, new[] { 10 }),
                ("Rise.", false, new[] { 60 }),
                ("Bake.", false, new[] { 45 }),
            });

        _db.Recipes.AddRange(simple, sectioned, multiTimer);
        _db.SaveChanges();

        foreach (var original in new[] { simple, sectioned, multiTimer })
        {
            // Act: project -> serialize -> deserialize -> validate
            var doc = _projector.Project(original);
            var json = _serializer.Serialize(doc);
            var roundTripped = _serializer.Deserialize(json);
            var result = _validator.Validate(roundTripped);

            // Assert
            Assert.True(result.IsValid,
                $"Recipe '{original.Name}' validation failed: {string.Join("; ", result.Errors.Select(e => e.Message))}");
            Assert.Equal(2, roundTripped.Version);
            Assert.Equal(original.Name, roundTripped.Name);
            Assert.Equal(original.Servings, roundTripped.Servings);
            Assert.Equal(original.PrepTimeMinutes, roundTripped.PrepTimeMinutes);
            Assert.Equal(original.CookTimeMinutes, roundTripped.CookTimeMinutes);
            Assert.Equal(original.RecipeIngredients.Count, roundTripped.Ingredients.Count);
            Assert.Equal(original.Steps.Count, roundTripped.Steps.Count);

            // Field-by-field ingredient assertion (id, name, amount, unit, note).
            var origIngredients = original.RecipeIngredients
                .OrderBy(ri => ri.RecipeLocalId)
                .ToList();
            for (int i = 0; i < origIngredients.Count; i++)
            {
                var origIng = origIngredients[i];
                var rtIng = roundTripped.Ingredients[i];
                Assert.Equal(origIng.RecipeLocalId, rtIng.Id);
                Assert.Equal(origIng.Ingredient.Name, rtIng.Name);
                Assert.Equal(origIng.Amount, rtIng.Amount);
                Assert.Equal(origIng.Unit, rtIng.Unit);
                Assert.Equal(origIng.Note, rtIng.Note);
            }

            // Step polymorphism + text/heading equality.
            var origSteps = original.Steps.OrderBy(s => s.Order).ToList();
            for (int i = 0; i < origSteps.Count; i++)
            {
                var origStep = origSteps[i];
                var rtStep = roundTripped.Steps[i];
                if (origStep.IsSection)
                {
                    var section = Assert.IsType<SectionStep>(rtStep);
                    Assert.Equal(origStep.Text, section.Heading);
                }
                else
                {
                    var content = Assert.IsType<ContentStep>(rtStep);
                    Assert.Equal(origStep.Text, content.Text);
                    if (origStep.Timers.Count == 0)
                    {
                        Assert.True(content.Timers == null || content.Timers.Count == 0);
                    }
                    else
                    {
                        Assert.NotNull(content.Timers);
                        Assert.Equal(origStep.Timers.Count, content.Timers!.Count);
                        for (int j = 0; j < origStep.Timers.Count; j++)
                        {
                            Assert.Equal(origStep.Timers[j].Duration, content.Timers[j].Duration);
                            Assert.Equal(origStep.Timers[j].Unit, content.Timers[j].Unit);
                            Assert.Equal(origStep.Timers[j].Label, content.Timers[j].Label);
                        }
                    }
                }
            }
        }
    }

    [Fact]
    public async Task BackupBeforeMigration_CreatesBackupFile_WithExpectedName()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "cookbot-backup-int-" + Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);
        try
        {
            var dbPath = Path.Combine(tempDir, "cookbot.db");
            File.WriteAllText(dbPath, "preexisting db content");

            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = $"Data Source={dbPath}"
                })
                .Build();
            var settings = Options.Create(new CookBotSettings { DatabaseBackupRetention = 3 });
            var svc = new DatabaseBackupService(config, settings);

            await svc.BackupBeforeMigrationAsync("RecipeCanonicalDocument", CancellationToken.None);

            var expected = Path.Combine(tempDir, "cookbot.db.pre-RecipeCanonicalDocument.bak");
            Assert.True(File.Exists(expected), $"expected backup at {expected}");
            Assert.Equal("preexisting db content", File.ReadAllText(expected));
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true);
        }
    }

    /// <summary>
    /// Builds a relational <see cref="Recipe"/> entity (with attached <see cref="RecipeIngredient"/>
    /// rows backed by Ingredient inserts) suitable for projector input.
    /// </summary>
    private Recipe BuildRelationalRecipe(
        int cookbookId,
        string name,
        int servings,
        int prep,
        int cook,
        (string Name, double Amount, string Unit, string? Note)[] ingredients,
        (string text, bool isSection, int[]? timers)[] steps)
    {
        var recipe = new Recipe
        {
            CookbookId = cookbookId,
            Name = name,
            Servings = servings,
            PrepTimeMinutes = prep,
            CookTimeMinutes = cook,
            TagsJson = "[]",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        int idx = 1;
        foreach (var (iname, amount, unit, note) in ingredients)
        {
            var normalized = iname.ToLower();
            var ing = _db.Ingredients.FirstOrDefault(i => i.NormalizedName == normalized);
            if (ing == null)
            {
                ing = new Ingredient { Name = iname, NormalizedName = normalized };
                _db.Ingredients.Add(ing);
                _db.SaveChanges();
            }
            recipe.RecipeIngredients.Add(new RecipeIngredient
            {
                Recipe = recipe,
                IngredientId = ing.Id,
                Ingredient = ing,
                RecipeLocalId = idx++,
                Amount = amount,
                Unit = unit,
                Note = note,
            });
        }

        int order = 0;
        foreach (var (text, isSection, timers) in steps)
        {
            recipe.Steps.Add(new RecipeStep
            {
                Text = text,
                IsSection = isSection,
                Order = order++,
                Timers = timers != null
                    ? timers.Select(d => new StepTimer { Duration = d, Unit = "min" }).ToList()
                    : new List<StepTimer>(),
                IngredientRefs = new(),
            });
        }
        return recipe;
    }

    public void Dispose() => _db.Dispose();
}
