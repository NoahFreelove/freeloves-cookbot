using Bunit;
using CookBot.Domain.Interfaces;
using CookBot.Web.Components.Pages.RecipeEditorParts;
using CookBot.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace CookBot.Tests.Web;

/// <summary>
/// Hand-rolled ICbDialogService recorder: captures ShowAsync calls so tests can
/// assert dispatch arguments without rendering CbDialogHost. Phase 7 / Plan 07-01
/// migrated RecipeStepEditor from IDialogService to ICbDialogService — the recorder
/// follows.
/// </summary>
internal sealed class FakeCbDialogService : ICbDialogService
{
    public sealed record ShowCall(Type DialogType, string Title, CbDialogParameters Parameters, CbDialogOptions Options);
    public List<ShowCall> ShowCalls { get; } = new();
    public CbDialogResult NextResult { get; set; } = CbDialogResult.Ok(true);

    public Task<CbDialogResult> ShowAsync<TDialog>(string title, CbDialogParameters? parameters = null, CbDialogOptions? options = null) where TDialog : ComponentBase
        => ShowAsync(typeof(TDialog), title, parameters, options);

    public Task<CbDialogResult> ShowAsync(Type dialogType, string title, CbDialogParameters? parameters = null, CbDialogOptions? options = null)
    {
        ShowCalls.Add(new ShowCall(dialogType, title, parameters ?? new CbDialogParameters(), options ?? CbDialogOptions.Default));
        return Task.FromResult(NextResult);
    }

    // Internal event isn't exercised by the recorder — production wiring lives in CbDialogHost.
    event Func<CbDialogRequest, Task>? ICbDialogService.OnRequest { add { } remove { } }
}

public class StepSectionToggleTests
{
    private static (Bunit.TestContext ctx, FakeCbDialogService dialogs) CreateContext(CbDialogResult? nextResult = null)
    {
        var ctx = new Bunit.TestContext();
        // Register a recorder for the Cb dialog service (Phase 7 / Plan 07-01 migration target).
        var fake = new FakeCbDialogService();
        if (nextResult != null) fake.NextResult = nextResult;
        ctx.Services.AddSingleton<ICbDialogService>(fake);
        // RecipeStepEditor + descendants are Cb-only after Phase 7 / Plan 07-07 strip; loose
        // JS interop covers the chip composer's ping probe and the bind/unbind segment events.
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        ctx.JSInterop.Setup<string>("RecipeChipComposer.ping").SetResult("ok");
        return (ctx, fake);
    }

    [Fact]
    public void OnParametersSet_DerivesKindFromIsSection_DB1()
    {
        var (ctx, _) = CreateContext();
        using (ctx)
        {
            var step = new ParsedStep { Text = "Whisk eggs", IsSection = false };
            var cut = ctx.RenderComponent<RecipeStepEditor>(p => p
                .Add(c => c.Step, step)
                .Add(c => c.Index, 0)
                .Add(c => c.IsLast, false)
                .Add(c => c.Ingredients, new List<ParsedIngredient>()));
            Assert.Contains("Step", cut.Markup);
            Assert.Contains("Section", cut.Markup);
        }
    }

    [Fact]
    public async Task EmptyStepToSection_NoConfirmation_DB1()
    {
        var (ctx, dialogs) = CreateContext();
        using (ctx)
        {
            var step = new ParsedStep { Text = "Just a heading idea", IsSection = false, Timers = new List<ParsedTimer>() };
            var cut = ctx.RenderComponent<RecipeStepEditor>(p => p
                .Add(c => c.Step, step)
                .Add(c => c.Index, 0)
                .Add(c => c.IsLast, false)
                .Add(c => c.Ingredients, new List<ParsedIngredient>()));
            await cut.InvokeAsync(() => (Task)cut.Instance.GetType().GetMethod("OnKindRequested",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                .Invoke(cut.Instance, new object[] { RecipeStepEditor.StepKind.Section })!);
            Assert.True(step.IsSection);
            Assert.Equal("Just a heading idea", step.Text); // D-B2: text reused as heading
            Assert.Empty(dialogs.ShowCalls); // No timers / no refs → no dialog
        }
    }

    [Fact]
    public async Task NonEmptyStepToSection_ShowsConfirmation_AndCancelReverts_DB3_Pitfall5()
    {
        // Warning 4 fix: use FakeCbDialogService so the dispatch path is actually verified.
        var (ctx, dialogs) = CreateContext(nextResult: CbDialogResult.Cancel());
        using (ctx)
        {
            var step = new ParsedStep
            {
                Text = "Add [Salt](#1) and bake",
                IsSection = false,
                Timers = new List<ParsedTimer> { new() { Duration = 25, Unit = "min" } }
            };
            var cut = ctx.RenderComponent<RecipeStepEditor>(p => p
                .Add(c => c.Step, step)
                .Add(c => c.Index, 0)
                .Add(c => c.IsLast, false)
                .Add(c => c.Ingredients, new List<ParsedIngredient> { new() { LocalId = 1, Name = "Salt" } }));

            await cut.InvokeAsync(() => (Task)cut.Instance.GetType().GetMethod("OnKindRequested",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                .Invoke(cut.Instance, new object[] { RecipeStepEditor.StepKind.Section })!);

            // D-B3 dispatch: exactly one ShowAsync<SectionDropConfirmationDialog> call with TimerCount=1, RefCount=1.
            Assert.Single(dialogs.ShowCalls);
            Assert.Equal(typeof(SectionDropConfirmationDialog), dialogs.ShowCalls[0].DialogType);
            Assert.Equal(1, (int)dialogs.ShowCalls[0].Parameters["TimerCount"]!);
            Assert.Equal(1, (int)dialogs.ShowCalls[0].Parameters["RefCount"]!);

            // Pitfall 5: Cancel reverts visual state. _kind stayed Step; Step is unchanged.
            Assert.False(step.IsSection);
            Assert.Equal("Add [Salt](#1) and bake", step.Text);
            Assert.Single(step.Timers!);
        }
    }

    [Fact]
    public async Task NonEmptyStepToSection_ConfirmedConvert_DropsTimersAndStripsRefs_DB3()
    {
        var (ctx, dialogs) = CreateContext(nextResult: CbDialogResult.Ok(true));
        using (ctx)
        {
            var step = new ParsedStep
            {
                Text = "Add [Salt](#1) and bake",
                IsSection = false,
                Timers = new List<ParsedTimer> { new() { Duration = 25, Unit = "min" } }
            };
            var cut = ctx.RenderComponent<RecipeStepEditor>(p => p
                .Add(c => c.Step, step)
                .Add(c => c.Index, 0)
                .Add(c => c.IsLast, false)
                .Add(c => c.Ingredients, new List<ParsedIngredient> { new() { LocalId = 1, Name = "Salt" } }));

            await cut.InvokeAsync(() => (Task)cut.Instance.GetType().GetMethod("OnKindRequested",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                .Invoke(cut.Instance, new object[] { RecipeStepEditor.StepKind.Section })!);

            Assert.Single(dialogs.ShowCalls); // dispatch happened
            Assert.True(step.IsSection);
            Assert.Empty(step.Timers!);
            Assert.Equal("Add Salt and bake", step.Text); // [name](#id) stripped
        }
    }

    [Fact]
    public void ViewModeToggle_FlipsBetweenChipsAndMarkdown_DA4()
    {
        // D-A4: ephemeral per-step "View as text / View as chips" toggle.
        var (ctx, _) = CreateContext();
        using (ctx)
        {
            var step = new ParsedStep
            {
                Text = "Mix [Flour](#1) and stir",
                IsSection = false,
                Timers = new List<ParsedTimer>()
            };
            var cut = ctx.RenderComponent<RecipeStepEditor>(p => p
                .Add(c => c.Step, step)
                .Add(c => c.Index, 0)
                .Add(c => c.IsLast, false)
                .Add(c => c.Ingredients, new List<ParsedIngredient> { new() { LocalId = 1, Name = "Flour" } }));

            // Initial state: chip view (default).
            var fieldInfo = typeof(RecipeStepEditor)
                .GetField("_showRawMarkdown", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.NotNull(fieldInfo);
            Assert.False((bool)fieldInfo!.GetValue(cut.Instance)!);

            // Default chip-view branch: the raw-markdown placeholder is NOT present.
            Assert.DoesNotContain("Step text (raw [name](#id) markdown)", cut.Markup);

            // Flip the flag (simulating the toggle button's OnClick handler).
            cut.InvokeAsync(() => fieldInfo.SetValue(cut.Instance, true));
            cut.Render();

            Assert.True((bool)fieldInfo.GetValue(cut.Instance)!);
            // After flip: a CbTextarea with the raw-markdown placeholder should be in the markup.
            Assert.Contains("Step text (raw [name](#id) markdown)", cut.Markup);

            // Flip back to chip view.
            cut.InvokeAsync(() => fieldInfo.SetValue(cut.Instance, false));
            cut.Render();
            Assert.False((bool)fieldInfo.GetValue(cut.Instance)!);
            Assert.DoesNotContain("Step text (raw [name](#id) markdown)", cut.Markup);

            // D-A4 invariant: the toggle did NOT mutate Step.Text (text is preserved across flips).
            Assert.Equal("Mix [Flour](#1) and stir", step.Text);

            // D-A4 invariant: ParsedStep has no "view mode" property (it's pure component-local state).
            // Confirmed by reflection — no `ViewMode`, `ShowRawMarkdown`, etc. on the type.
            var stepProps = typeof(ParsedStep).GetProperties().Select(p => p.Name).ToList();
            Assert.DoesNotContain("ViewMode", stepProps);
            Assert.DoesNotContain("ShowRawMarkdown", stepProps);
        }
    }
}
