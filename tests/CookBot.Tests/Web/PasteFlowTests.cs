using Bunit;
using CookBot.Domain.Interfaces;
using CookBot.Web.Components.Pages;
using Microsoft.Extensions.DependencyInjection;

namespace CookBot.Tests.Web;

public class PasteFlowTests
{
    private sealed class FakeParser : IRecipeFormatParser
    {
        public bool ShouldSucceed { get; set; } = true;
        public ParsedRecipe Result { get; set; } = new();
        public List<string> Errors { get; set; } = new();
        public string LastRawContent { get; private set; } = string.Empty;

        public ParsedRecipe Parse(string rawContent) => Result;
        public string Serialize(ParsedRecipe recipe) => string.Empty;
        public bool TryParse(string rawContent, out ParsedRecipe? recipe, out List<string> errors)
        {
            LastRawContent = rawContent;
            recipe = ShouldSucceed ? Result : null;
            errors = Errors;
            return ShouldSucceed;
        }
    }

    private static Bunit.TestContext CreateContext(FakeParser parser)
    {
        var ctx = new Bunit.TestContext();
        ctx.Services.AddSingleton<IRecipeFormatParser>(parser);
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        return ctx;
    }

    [Fact]
    public void PasteRawTextDialogRenders()
    {
        // Phase 7 / Plan 07-07: PasteRawTextDialog is a CbDialog content component
        // (renders inside CbDialogHost). The test renders the dialog content directly
        // and verifies the prompt label is present.
        using var ctx = CreateContext(new FakeParser());
        var cut = ctx.RenderComponent<PasteRawTextDialog>();
        Assert.Contains("Paste your recipe text here", cut.Markup);
    }
}
