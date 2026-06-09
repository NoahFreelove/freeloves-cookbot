using CookBot.Application.Services;

namespace CookBot.Tests.Nutrition;

/// <summary>
/// Tests for <see cref="IngredientNormalizer.Normalize"/> — the shared deny-list normalizer
/// used by the CNF seeder (Plan 03) and the runtime matcher (Plan 05).
/// </summary>
public class IngredientNormalizerTests
{
    // ── Strip cases: prep/quality/instruction modifiers (D-15-05) ────────────

    [Fact]
    public void Normalize_StripsChopped()
    {
        var result = IngredientNormalizer.Normalize("chopped onion");
        Assert.DoesNotContain("chopped", result);
        Assert.Contains("onion", result);
    }

    [Fact]
    public void Normalize_StripsMinced()
    {
        var result = IngredientNormalizer.Normalize("minced garlic");
        Assert.DoesNotContain("minced", result);
        Assert.Contains("garlic", result);
    }

    [Fact]
    public void Normalize_StripsDiced()
    {
        var result = IngredientNormalizer.Normalize("diced tomatoes");
        Assert.DoesNotContain("diced", result);
        Assert.Contains("tomatoes", result);
    }

    [Fact]
    public void Normalize_StripsSliced()
    {
        var result = IngredientNormalizer.Normalize("sliced mushrooms");
        Assert.DoesNotContain("sliced", result);
        Assert.Contains("mushrooms", result);
    }

    [Fact]
    public void Normalize_StripsShredded()
    {
        var result = IngredientNormalizer.Normalize("shredded cheese");
        Assert.DoesNotContain("shredded", result);
        Assert.Contains("cheese", result);
    }

    [Fact]
    public void Normalize_StripsGrated()
    {
        var result = IngredientNormalizer.Normalize("grated parmesan");
        Assert.DoesNotContain("grated", result);
        Assert.Contains("parmesan", result);
    }

    [Fact]
    public void Normalize_StripsSifted()
    {
        var result = IngredientNormalizer.Normalize("sifted flour");
        Assert.DoesNotContain("sifted", result);
        Assert.Contains("flour", result);
    }

    [Fact]
    public void Normalize_StripsPacked()
    {
        var result = IngredientNormalizer.Normalize("packed brown sugar");
        Assert.DoesNotContain("packed", result);
        Assert.Contains("brown sugar", result);
    }

    [Fact]
    public void Normalize_StripsFinely()
    {
        var result = IngredientNormalizer.Normalize("finely chopped parsley");
        Assert.DoesNotContain("finely", result);
        Assert.DoesNotContain("chopped", result);
        Assert.Contains("parsley", result);
    }

    [Fact]
    public void Normalize_StripsRoughly()
    {
        var result = IngredientNormalizer.Normalize("roughly chopped walnuts");
        Assert.DoesNotContain("roughly", result);
        Assert.Contains("walnuts", result);
    }

    [Fact]
    public void Normalize_StripsFreshly()
    {
        var result = IngredientNormalizer.Normalize("freshly ground black pepper");
        Assert.DoesNotContain("freshly", result);
        Assert.Contains("black pepper", result);
    }

    [Fact]
    public void Normalize_StripsRoomTemperatureHyphenated()
    {
        var result = IngredientNormalizer.Normalize("room-temperature eggs");
        Assert.DoesNotContain("room", result);
        Assert.DoesNotContain("temperature", result);
        Assert.Contains("eggs", result);
    }

    [Fact]
    public void Normalize_StripsRoomTemperatureSpaced()
    {
        var result = IngredientNormalizer.Normalize("room temperature butter");
        Assert.DoesNotContain("room", result);
        Assert.DoesNotContain("temperature", result);
        Assert.Contains("butter", result);
    }

    [Fact]
    public void Normalize_StripsCold()
    {
        var result = IngredientNormalizer.Normalize("cold water");
        Assert.DoesNotContain("cold", result);
        Assert.Contains("water", result);
    }

    [Fact]
    public void Normalize_StripsWarm()
    {
        var result = IngredientNormalizer.Normalize("warm milk");
        Assert.DoesNotContain("warm", result);
        Assert.Contains("milk", result);
    }

    [Fact]
    public void Normalize_StripsGoodQualityHyphenated()
    {
        var result = IngredientNormalizer.Normalize("good-quality olive oil, divided");
        Assert.DoesNotContain("good", result);
        Assert.DoesNotContain("quality", result);
        Assert.DoesNotContain("divided", result);
        Assert.Contains("olive", result);
        Assert.Contains("oil", result);
    }

    [Fact]
    public void Normalize_StripsFine()
    {
        var result = IngredientNormalizer.Normalize("fine sea salt");
        Assert.DoesNotContain("fine", result);
        Assert.Contains("sea salt", result);
    }

    [Fact]
    public void Normalize_StripsCoarse()
    {
        var result = IngredientNormalizer.Normalize("coarse kosher salt");
        Assert.DoesNotContain("coarse", result);
        Assert.Contains("kosher salt", result);
    }

    [Fact]
    public void Normalize_StripsLarge()
    {
        var result = IngredientNormalizer.Normalize("large egg");
        Assert.DoesNotContain("large", result);
        Assert.Contains("egg", result);
    }

    [Fact]
    public void Normalize_StripsSmall()
    {
        var result = IngredientNormalizer.Normalize("small onion");
        Assert.DoesNotContain("small", result);
        Assert.Contains("onion", result);
    }

    [Fact]
    public void Normalize_StripsMedium()
    {
        var result = IngredientNormalizer.Normalize("medium apple");
        Assert.DoesNotContain("medium", result);
        Assert.Contains("apple", result);
    }

    [Fact]
    public void Normalize_StripsRipe()
    {
        var result = IngredientNormalizer.Normalize("ripe banana");
        Assert.DoesNotContain("ripe", result);
        Assert.Contains("banana", result);
    }

    [Fact]
    public void Normalize_StripsOrganic()
    {
        var result = IngredientNormalizer.Normalize("organic spinach");
        Assert.DoesNotContain("organic", result);
        Assert.Contains("spinach", result);
    }

    [Fact]
    public void Normalize_StripsToTaste()
    {
        var result = IngredientNormalizer.Normalize("salt, to taste");
        Assert.DoesNotContain("to taste", result);
        Assert.Contains("salt", result);
    }

    [Fact]
    public void Normalize_StripsOptional()
    {
        var result = IngredientNormalizer.Normalize("heavy cream, optional");
        Assert.DoesNotContain("optional", result);
        Assert.Contains("cream", result);
    }

    [Fact]
    public void Normalize_StripsDivided()
    {
        var result = IngredientNormalizer.Normalize("olive oil, divided");
        Assert.DoesNotContain("divided", result);
        Assert.Contains("olive oil", result);
    }

    [Fact]
    public void Normalize_StripsForGarnish()
    {
        var result = IngredientNormalizer.Normalize("parsley, for garnish");
        Assert.DoesNotContain("for garnish", result);
        Assert.Contains("parsley", result);
    }

    [Fact]
    public void Normalize_StripsPlusMore()
    {
        var result = IngredientNormalizer.Normalize("olive oil, plus more");
        Assert.DoesNotContain("plus more", result);
        Assert.Contains("olive oil", result);
    }

    // ── Behavior cases from plan (composite) ────────────────────────────────

    [Fact]
    public void Normalize_FinelyChoppedUnsaltedButter_KeepsUnsaltedAndButter()
    {
        var result = IngredientNormalizer.Normalize("finely chopped unsalted butter");
        Assert.Contains("unsalted", result);
        Assert.Contains("butter", result);
        Assert.DoesNotContain("finely", result);
        Assert.DoesNotContain("chopped", result);
    }

    [Fact]
    public void Normalize_RoomTemperatureWholeMilk_KeepsWholeAndMilk()
    {
        var result = IngredientNormalizer.Normalize("room-temperature whole milk");
        Assert.Contains("whole", result);
        Assert.Contains("milk", result);
        Assert.DoesNotContain("room", result);
        Assert.DoesNotContain("temperature", result);
    }

    [Fact]
    public void Normalize_GoodQualityOliveOilDivided_KeepsOliveOil()
    {
        var result = IngredientNormalizer.Normalize("good-quality olive oil, divided");
        Assert.Contains("olive", result);
        Assert.Contains("oil", result);
        Assert.DoesNotContain("good", result);
        Assert.DoesNotContain("quality", result);
        Assert.DoesNotContain("divided", result);
    }

    // ── Keep cases: nutrition-changing modifiers (D-15-05) ───────────────────

    [Fact]
    public void Normalize_KeepsUnsalted()
    {
        var result = IngredientNormalizer.Normalize("unsalted butter");
        Assert.Contains("unsalted", result);
        Assert.Contains("butter", result);
    }

    [Fact]
    public void Normalize_KeepsSalted()
    {
        var result = IngredientNormalizer.Normalize("salted peanuts");
        Assert.Contains("salted", result);
        Assert.Contains("peanuts", result);
    }

    [Fact]
    public void Normalize_KeepsSkinless()
    {
        var result = IngredientNormalizer.Normalize("skinless chicken breast");
        Assert.Contains("skinless", result);
        Assert.Contains("chicken", result);
    }

    [Fact]
    public void Normalize_KeepsLowfatOneWord()
    {
        var result = IngredientNormalizer.Normalize("lowfat plain yogurt");
        Assert.Contains("lowfat", result);
        Assert.Contains("yogurt", result);
    }

    [Fact]
    public void Normalize_KeepsLowFatHyphenated()
    {
        // "low-fat" hyphen becomes space → "low fat"; neither "low" nor "fat" is in deny-list
        var result = IngredientNormalizer.Normalize("low-fat plain yogurt");
        // After hyphen→space: "low fat plain yogurt" → strip "plain" (not in deny-list → kept)
        // The result must contain "low" or "lowfat" and "fat"
        Assert.Contains("low", result);
        Assert.Contains("fat", result);
        Assert.Contains("yogurt", result);
    }

    [Fact]
    public void Normalize_KeepsWhole()
    {
        var result = IngredientNormalizer.Normalize("whole milk");
        Assert.Contains("whole", result);
        Assert.Contains("milk", result);
    }

    [Fact]
    public void Normalize_KeepsLight()
    {
        var result = IngredientNormalizer.Normalize("light cream");
        Assert.Contains("light", result);
        Assert.Contains("cream", result);
    }

    [Fact]
    public void Normalize_KeepsHeavy()
    {
        var result = IngredientNormalizer.Normalize("heavy cream");
        Assert.Contains("heavy", result);
        Assert.Contains("cream", result);
    }

    // ── CNF description tokenization ─────────────────────────────────────────

    [Fact]
    public void Normalize_CnfDescription_TokenizesCleanly()
    {
        // CNF genus-first: "Grains, wheat flour, white, all purpose, enriched"
        var result = IngredientNormalizer.Normalize("Grains, wheat flour, white, all purpose, enriched");
        Assert.Contains("flour", result);
        // Commas/punctuation collapsed; deny-listed words removed
        Assert.DoesNotContain(",", result);
    }

    // ── Whole-word guard: substring safety ───────────────────────────────────

    [Fact]
    public void Normalize_Buttermilk_NotStrippedByButter()
    {
        // "butter" is not in the deny-list, but this test guards the whole-word invariant:
        // no deny-list token should strip a substring of a legitimate word.
        // Use "ground" from deny-list: "groundnut" should not be mangled even though "ground" is denied.
        var result = IngredientNormalizer.Normalize("groundnut oil");
        // "groundnut" must survive; "ground" as a standalone word is stripped, but "groundnut" is a compound.
        Assert.Contains("groundnut", result);
    }

    [Fact]
    public void Normalize_GratedParmesan_DoesNotMutateOtherCompounds()
    {
        // "grated" stripped; word boundaries protect adjacent letters
        var result = IngredientNormalizer.Normalize("grated parmesan cheese");
        Assert.DoesNotContain("grated", result);
        Assert.Contains("parmesan", result);
        Assert.Contains("cheese", result);
    }

    // ── Basic pipeline ────────────────────────────────────────────────────────

    [Fact]
    public void Normalize_LowercasesInput()
    {
        var result = IngredientNormalizer.Normalize("OLIVE OIL");
        Assert.Equal("olive oil", result);
    }

    [Fact]
    public void Normalize_CollapsesWhitespace()
    {
        var result = IngredientNormalizer.Normalize("olive  oil");
        Assert.Equal("olive oil", result);
    }

    [Fact]
    public void Normalize_TrimsLeadingTrailingSpaces()
    {
        var result = IngredientNormalizer.Normalize("  butter  ");
        Assert.Equal("butter", result);
    }

    [Fact]
    public void Normalize_ReplacesHyphenWithSpace()
    {
        // After replace, whitespace collapses; neither token is in deny-list
        var result = IngredientNormalizer.Normalize("all-purpose");
        Assert.Equal("all purpose", result);
    }

    [Fact]
    public void Normalize_ReplacesUnderscoreWithSpace()
    {
        var result = IngredientNormalizer.Normalize("olive_oil");
        Assert.Equal("olive oil", result);
    }

    [Fact]
    public void Normalize_EmptyString_ReturnsEmpty()
    {
        var result = IngredientNormalizer.Normalize(string.Empty);
        Assert.Equal(string.Empty, result);
    }
}
