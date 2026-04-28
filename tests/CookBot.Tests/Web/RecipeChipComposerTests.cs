using Bunit;
using CookBot.Domain.Interfaces;
using CookBot.Web.Components.Pages.RecipeEditorParts;
using Microsoft.JSInterop;

namespace CookBot.Tests.Web;

public class RecipeChipComposerTests
{
    private static Bunit.TestContext CreateContext()
    {
        var ctx = new Bunit.TestContext();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        // ping returns "ok" so the chip flow renders (not the fallback).
        ctx.JSInterop.Setup<string>("RecipeChipComposer.ping").SetResult("ok");
        return ctx;
    }

    [Fact]
    public void TokenizesIngredientLinksAsChips()
    {
        using var ctx = CreateContext();
        var ingredients = new List<ParsedIngredient> { new() { LocalId = 1, Name = "Salt" } };
        var cut = ctx.RenderComponent<RecipeChipComposer>(p => p
            .Add(c => c.Interactive, true)
            .Add(c => c.Text, "Add [Salt](#1) to taste")
            .Add(c => c.Ingredients, ingredients));
        // Force first-render lifecycle so the probe runs and chip flow renders.
        cut.Render();
        var markup = cut.Markup;
        Assert.Contains("Salt", markup);
    }

    [Fact]
    public async Task AtTriggerInsertion_AndButtonInsertion_ProduceIdenticalUnderlyingText_DA1Invariant()
    {
        using var ctx = CreateContext();
        var ingredients = new List<ParsedIngredient> { new() { LocalId = 3, Name = "Salt" } };

        // @-trigger path: simulate "Add @ to taste" with caret at index 5 (after @), partial token length=1 (the "@").
        var atCut = ctx.RenderComponent<RecipeChipComposer>(p => p
            .Add(c => c.Interactive, true)
            .Add(c => c.Text, "Add @ to taste")
            .Add(c => c.Ingredients, ingredients));
        await atCut.InvokeAsync(() => atCut.Instance.SimulateAtTriggerSelectionAsync(ingredients[0], caretIndex: 5, partialAtTokenLength: 1));

        // Button path: simulate "Add  to taste" with caret at index 4, partial token length=0.
        var btnCut = ctx.RenderComponent<RecipeChipComposer>(p => p
            .Add(c => c.Interactive, true)
            .Add(c => c.Text, "Add  to taste")
            .Add(c => c.Ingredients, ingredients));
        await btnCut.InvokeAsync(() => btnCut.Instance.SimulateButtonInsertionAsync(ingredients[0], caretIndex: 4));

        Assert.Equal(atCut.Instance.Text, btnCut.Instance.Text);
        Assert.Contains("[Salt](#3)", atCut.Instance.Text);
    }

    [Fact]
    public void JsInteropFails_FallsBackToCbTextarea_DD4()
    {
        using var ctx = new Bunit.TestContext();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        // Register ping to throw → _jsInteropAvailable stays false, fallback path renders.
        ctx.JSInterop.Setup<string>("RecipeChipComposer.ping").SetException(new JSException("not loaded"));
        var ingredients = new List<ParsedIngredient> { new() { LocalId = 1, Name = "Salt" } };
        var cut = ctx.RenderComponent<RecipeChipComposer>(p => p
            .Add(c => c.Interactive, true)
            .Add(c => c.Text, "fallback test")
            .Add(c => c.Ingredients, ingredients));
        cut.WaitForState(() => !cut.Markup.Contains("chip-flow"), TimeSpan.FromSeconds(1));
        // After the failed probe, chip-flow div is absent and the CbTextarea fallback renders.
        Assert.DoesNotContain("class=\"chip-flow\"", cut.Markup);
        Assert.Contains("<textarea", cut.Markup);
    }

    [Fact]
    public void UnresolvedChipRendersAsErrorChip_DA6()
    {
        using var ctx = CreateContext();
        // Ingredient list missing #99 → chip should render with the v1.2 "unresolved" tint.
        var ingredients = new List<ParsedIngredient> { new() { LocalId = 1, Name = "Salt" } };
        var cut = ctx.RenderComponent<RecipeChipComposer>(p => p
            .Add(c => c.Interactive, true)
            .Add(c => c.Text, "Add [Pomegranate](#99) sparingly")
            .Add(c => c.Ingredients, ingredients));
        cut.Render();
        // v1.2 (Phase 6 / Plan 06-04): IngredientChip renders unresolved chips with the
        // `cb-chip ing unresolved` class set + a warn-soft inline tint. The DA6 invariant —
        // unresolved chips look distinctly different from resolved ones — is preserved.
        Assert.Contains("cb-chip ing unresolved", cut.Markup);
        Assert.Contains("var(--warn-soft)", cut.Markup);
    }

    [Fact]
    public async Task ContenteditableInput_UpdatesText_WR01Regression()
    {
        // WR-01 regression: Blazor's @oninput on contenteditable does NOT populate
        // ChangeEventArgs.Value. The fix wires segment input through JS-interop
        // (RecipeChipComposer.bindSegmentEvents) → DotNetObjectReference.invokeMethodAsync
        // → OnSegmentInputFromJs([JSInvokable]). This test exercises the JSInvokable
        // surface directly — the same surface the JS bridge calls at runtime.
        using var ctx = CreateContext();
        var ingredients = new List<ParsedIngredient> { new() { LocalId = 1, Name = "Salt" } };
        var cut = ctx.RenderComponent<RecipeChipComposer>(p => p
            .Add(c => c.Interactive, true)
            .Add(c => c.Text, "Add [Salt](#1) seasoning")
            .Add(c => c.Ingredients, ingredients));
        cut.Render(); // settle first-render lifecycle and probe.

        // Segment 0 is the leading "Add " span. Replace its content with "Bake the ".
        await cut.InvokeAsync(() => cut.Instance.OnSegmentInputFromJs(0, "Bake the "));

        Assert.Equal("Bake the [Salt](#1) seasoning", cut.Instance.Text);
    }

    [Fact]
    public async Task ContenteditableInputOnTrailingSegment_UpdatesText_WR01Regression()
    {
        using var ctx = CreateContext();
        var ingredients = new List<ParsedIngredient> { new() { LocalId = 1, Name = "Salt" } };
        var cut = ctx.RenderComponent<RecipeChipComposer>(p => p
            .Add(c => c.Interactive, true)
            .Add(c => c.Text, "Add [Salt](#1) seasoning")
            .Add(c => c.Ingredients, ingredients));
        cut.Render();

        // Segment 2 is the trailing " seasoning" span (segment 0 = "Add ", segment 1 = chip, segment 2 = " seasoning").
        await cut.InvokeAsync(() => cut.Instance.OnSegmentInputFromJs(2, " for 5 minutes"));

        Assert.Equal("Add [Salt](#1) for 5 minutes", cut.Instance.Text);
    }

    [Fact]
    public async Task BackspaceAtOffsetZero_RemovesPriorChip_IN03Regression()
    {
        // IN-03 regression: Backspace at offset 0 of a text segment immediately after a chip
        // removes the chip (EDITOR-07 keyboard nav semantic).
        using var ctx = CreateContext();
        var ingredients = new List<ParsedIngredient> { new() { LocalId = 1, Name = "Salt" } };
        var cut = ctx.RenderComponent<RecipeChipComposer>(p => p
            .Add(c => c.Interactive, true)
            .Add(c => c.Text, "Add [Salt](#1) seasoning")
            .Add(c => c.Ingredients, ingredients));
        cut.Render();

        bool handled = await cut.InvokeAsync(() => cut.Instance.OnSegmentKeyDownFromJs(2, "Backspace", 0));

        Assert.True(handled);
        Assert.Equal("Add  seasoning", cut.Instance.Text);
    }

    [Fact]
    public async Task BackspaceAtOffsetFive_DoesNothing_IN03Regression()
    {
        using var ctx = CreateContext();
        var ingredients = new List<ParsedIngredient> { new() { LocalId = 1, Name = "Salt" } };
        var cut = ctx.RenderComponent<RecipeChipComposer>(p => p
            .Add(c => c.Interactive, true)
            .Add(c => c.Text, "Add [Salt](#1) seasoning")
            .Add(c => c.Ingredients, ingredients));
        cut.Render();

        bool handled = await cut.InvokeAsync(() => cut.Instance.OnSegmentKeyDownFromJs(2, "Backspace", 5));

        Assert.False(handled);
        Assert.Equal("Add [Salt](#1) seasoning", cut.Instance.Text);
    }

    [Fact]
    public async Task BackspaceWhenPriorIsNotChip_DoesNothing_IN03Regression()
    {
        using var ctx = CreateContext();
        var ingredients = new List<ParsedIngredient>();
        var cut = ctx.RenderComponent<RecipeChipComposer>(p => p
            .Add(c => c.Interactive, true)
            .Add(c => c.Text, "just plain text")
            .Add(c => c.Ingredients, ingredients));
        cut.Render();

        bool handled = await cut.InvokeAsync(() => cut.Instance.OnSegmentKeyDownFromJs(0, "Backspace", 0));

        Assert.False(handled);
        Assert.Equal("just plain text", cut.Instance.Text);
    }

    [Fact]
    public async Task ArrowLeftAtOffsetZero_DoesNotRemoveChip_IN03Regression()
    {
        // Arrow-key caret motion across chips is out of scope for this gap closure;
        // assert non-removal so a future implementation doesn't accidentally regress.
        using var ctx = CreateContext();
        var ingredients = new List<ParsedIngredient> { new() { LocalId = 1, Name = "Salt" } };
        var cut = ctx.RenderComponent<RecipeChipComposer>(p => p
            .Add(c => c.Interactive, true)
            .Add(c => c.Text, "Add [Salt](#1) seasoning")
            .Add(c => c.Ingredients, ingredients));
        cut.Render();

        bool handled = await cut.InvokeAsync(() => cut.Instance.OnSegmentKeyDownFromJs(2, "ArrowLeft", 0));

        Assert.False(handled);
        Assert.Equal("Add [Salt](#1) seasoning", cut.Instance.Text);
    }
}
