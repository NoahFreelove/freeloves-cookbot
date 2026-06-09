using System;
using System.Linq;
using CookBot.Application.Recipes;
using CookBot.Domain.Recipes;

namespace CookBot.Tests.Recipes;

/// <summary>
/// FORMAT-07 / SC2 data-layer guarantee. Proves that all four v4 field groups
/// (equipment, provenance, per-ingredient substitutions, per-step donenessCue) survive a
/// <see cref="JsonRecipeSerializer.Serialize"/> → <see cref="JsonRecipeSerializer.Deserialize"/>
/// round-trip with field-level equality, and that a fully-populated
/// <see cref="RecipeDocument"/> also survives the ParsedRecipe projection path
/// (<see cref="CookBot.Application.Services.RecipeFormatParser.TryParse"/>).
/// </summary>
public class RecipeRoundTripTests
{
    // ---- helpers ----------------------------------------------------------------

    private static JsonRecipeSerializer Serializer() => new();

    private static RecipeDocument BuildFullDoc() => new()
    {
        Version = 4,
        Name = "Test Cake",
        Servings = 8,
        Equipment = ["stand mixer", "9-inch cake pan", "parchment paper"],
        Provenance = new RecipeProvenance
        {
            SourceUrl = "https://example.com/cake",
            AuthorName = "Jane Doe",
            SourceName = "The Baking Book",
        },
        Tags = ["dessert"],
        Ingredients =
        [
            new IngredientEntry
            {
                Id = 1,
                Name = "All-purpose flour",
                Amount = 300,
                Unit = "g",
                Substitutions =
                [
                    // note-only substitution (edge: no structured fields)
                    new IngredientSubstitution { Note = "use gluten-free blend for GF version" },
                    // structured substitution
                    new IngredientSubstitution
                    {
                        Note = "cake flour gives a more tender crumb",
                        Name = "cake flour",
                        Amount = 290,
                        Unit = "g",
                    },
                ],
            },
            new IngredientEntry
            {
                Id = 2,
                Name = "Whole milk",
                Amount = 240,
                Unit = "ml",
                // no substitutions — should default to empty list
            },
        ],
        Steps =
        [
            new ContentStep
            {
                Text = "Mix [all-purpose flour](#1) with [whole milk](#2) until combined.",
                DonenessCue = "smooth batter with no lumps",
            },
            new ContentStep
            {
                Text = "Bake in the preheated oven.",
                Temperature = new StepTemperature { Value = 175, Unit = TemperatureUnit.C },
                DonenessCue = "golden brown on top and toothpick comes out clean",
            },
        ],
    };

    private static RecipeDocument BuildEmptyGroupsDoc() => new()
    {
        Version = 4,
        Name = "Simple Soup",
        Servings = 2,
        // Equipment, Provenance, Substitutions, DonenessCue all absent / default
        Ingredients =
        [
            new IngredientEntry { Id = 1, Name = "Water", Amount = 1, Unit = "L" },
        ],
        Steps =
        [
            new ContentStep { Text = "Boil [water](#1)." },
        ],
    };

    // ---- all-present round-trip -------------------------------------------------

    /// <summary>SC2: fully-populated RecipeDocument survives Serialize → Deserialize with field-level equality.</summary>
    [Fact]
    public void RoundTrip_AllGroupsPresent_EquipmentRoundTrips()
    {
        var serializer = Serializer();
        var doc = BuildFullDoc();

        var json = serializer.Serialize(doc);
        var rt = serializer.Deserialize(json);

        Assert.Equal(doc.Equipment.Count, rt.Equipment.Count);
        Assert.Equal(doc.Equipment[0], rt.Equipment[0]);
        Assert.Equal(doc.Equipment[1], rt.Equipment[1]);
        Assert.Equal(doc.Equipment[2], rt.Equipment[2]);
    }

    [Fact]
    public void RoundTrip_AllGroupsPresent_ProvenanceRoundTrips()
    {
        var serializer = Serializer();
        var doc = BuildFullDoc();

        var json = serializer.Serialize(doc);
        var rt = serializer.Deserialize(json);

        Assert.NotNull(rt.Provenance);
        Assert.Equal(doc.Provenance!.SourceUrl, rt.Provenance!.SourceUrl);
        Assert.Equal(doc.Provenance!.AuthorName, rt.Provenance!.AuthorName);
        Assert.Equal(doc.Provenance!.SourceName, rt.Provenance!.SourceName);
    }

    [Fact]
    public void RoundTrip_AllGroupsPresent_SubstitutionsRoundTrip()
    {
        var serializer = Serializer();
        var doc = BuildFullDoc();

        var json = serializer.Serialize(doc);
        var rt = serializer.Deserialize(json);

        var origSubs = doc.Ingredients[0].Substitutions;
        var rtSubs = rt.Ingredients[0].Substitutions;

        Assert.Equal(origSubs.Count, rtSubs.Count);
        // note-only substitution
        Assert.Equal(origSubs[0].Note, rtSubs[0].Note);
        Assert.Null(rtSubs[0].Name);
        Assert.Null(rtSubs[0].Amount);
        Assert.Null(rtSubs[0].Unit);
        // structured substitution
        Assert.Equal(origSubs[1].Note, rtSubs[1].Note);
        Assert.Equal(origSubs[1].Name, rtSubs[1].Name);
        Assert.Equal(origSubs[1].Amount, rtSubs[1].Amount);
        Assert.Equal(origSubs[1].Unit, rtSubs[1].Unit);
    }

    [Fact]
    public void RoundTrip_AllGroupsPresent_DonenessCueRoundTrips()
    {
        var serializer = Serializer();
        var doc = BuildFullDoc();

        var json = serializer.Serialize(doc);
        var rt = serializer.Deserialize(json);

        var origStep0 = (ContentStep)doc.Steps[0];
        var rtStep0 = (ContentStep)rt.Steps[0];
        Assert.Equal(origStep0.DonenessCue, rtStep0.DonenessCue);

        var origStep1 = (ContentStep)doc.Steps[1];
        var rtStep1 = (ContentStep)rt.Steps[1];
        Assert.Equal(origStep1.DonenessCue, rtStep1.DonenessCue);
    }

    // ---- null / empty case ------------------------------------------------------

    /// <summary>No four-group fields → equipment empty, provenance null, substitutions empty, donenessCue null — no exception.</summary>
    [Fact]
    public void RoundTrip_NoGroupsPresent_EmptyAndNullDefaults_NoException()
    {
        var serializer = Serializer();
        var doc = BuildEmptyGroupsDoc();

        var json = serializer.Serialize(doc);
        var rt = serializer.Deserialize(json);

        Assert.Empty(rt.Equipment);
        Assert.Null(rt.Provenance);
        Assert.Empty(rt.Ingredients[0].Substitutions);
        var rtStep = (ContentStep)rt.Steps[0];
        Assert.Null(rtStep.DonenessCue);
    }

    // ---- edge cases -------------------------------------------------------------

    /// <summary>Edge: substitution with only Note (no structured fields) round-trips intact.</summary>
    [Fact]
    public void RoundTrip_Edge_NoteOnlySubstitution_RoundTripsIntact()
    {
        var serializer = Serializer();
        var doc = new RecipeDocument
        {
            Version = 4,
            Name = "Edge Bread",
            Servings = 1,
            Ingredients =
            [
                new IngredientEntry
                {
                    Id = 1,
                    Name = "Bread flour",
                    Amount = 500,
                    Unit = "g",
                    Substitutions = [new IngredientSubstitution { Note = "all-purpose works in a pinch" }],
                },
            ],
            Steps = [new ContentStep { Text = "Mix [bread flour](#1) with water." }],
        };

        var json = serializer.Serialize(doc);
        var rt = serializer.Deserialize(json);

        var sub = rt.Ingredients[0].Substitutions.Single();
        Assert.Equal("all-purpose works in a pinch", sub.Note);
        Assert.Null(sub.Name);
        Assert.Null(sub.Amount);
        Assert.Null(sub.Unit);
    }

    /// <summary>Edge: provenance with only SourceName (no URL / author) round-trips intact.</summary>
    [Fact]
    public void RoundTrip_Edge_ProvenanceSourceNameOnly_RoundTripsIntact()
    {
        var serializer = Serializer();
        var doc = new RecipeDocument
        {
            Version = 4,
            Name = "Edge Soup",
            Servings = 1,
            Provenance = new RecipeProvenance { SourceName = "Grandma's Cookbook" },
            Ingredients = [new IngredientEntry { Id = 1, Name = "Water", Amount = 1, Unit = "L" }],
            Steps = [new ContentStep { Text = "Boil [water](#1)." }],
        };

        var json = serializer.Serialize(doc);
        var rt = serializer.Deserialize(json);

        Assert.NotNull(rt.Provenance);
        Assert.Equal("Grandma's Cookbook", rt.Provenance!.SourceName);
        Assert.Null(rt.Provenance!.AuthorName);
        Assert.Null(rt.Provenance!.SourceUrl);
    }

    // ---- ProjectToParsedRecipe projection path ----------------------------------

    /// <summary>
    /// All four field groups flow correctly through the full parser bridge:
    /// ParseDocument → ProjectToParsedRecipe produces ParsedRecipe with populated groups.
    /// </summary>
    [Fact]
    public void RoundTrip_ProjectToParsedRecipe_AllGroupsProjected()
    {
        var parser = new CookBot.Application.Services.RecipeFormatParser(
            new CookBot.Application.Recipes.RecipeUpcasterChain(
                new IRecipeUpcaster[] { new Migration_V1_To_V2(), new Migration_V2_To_V3(), new Migration_V3_To_V4() }),
            new JsonRecipeSerializer(),
            new RecipeValidator());

        var json = new JsonRecipeSerializer().Serialize(BuildFullDoc());
        var ok = parser.TryParse(json, out var parsed, out var errors);

        Assert.True(ok, string.Join("; ", errors));
        Assert.NotNull(parsed);
        Assert.Equal(3, parsed!.Equipment.Count);
        Assert.Equal("stand mixer", parsed.Equipment[0]);
        Assert.NotNull(parsed.Provenance);
        Assert.Equal("Jane Doe", parsed.Provenance!.AuthorName);
        Assert.Equal(2, parsed.Ingredients[0].Substitutions.Count);
        Assert.Equal("use gluten-free blend for GF version", parsed.Ingredients[0].Substitutions[0].Note);
        // second ingredient has no substitutions
        Assert.Empty(parsed.Ingredients[1].Substitutions);
        // step donenessCue
        Assert.Equal("smooth batter with no lumps", parsed.Steps[0].DonenessCue);
        Assert.Equal("golden brown on top and toothpick comes out clean", parsed.Steps[1].DonenessCue);
    }
}
