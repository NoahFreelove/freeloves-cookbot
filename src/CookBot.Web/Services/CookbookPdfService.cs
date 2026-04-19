using CookBot.Application.DTOs;
using CookBot.Application.Services;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CookBot.Web.Services;

public sealed class CookbookPdfService
{
    public byte[] GeneratePdf(CookbookTransferDocument doc)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Column(col =>
                {
                    col.Spacing(4);
                    col.Item().Text(doc.Cookbook.Name).FontSize(20).SemiBold();
                    if (!string.IsNullOrWhiteSpace(doc.Cookbook.Description))
                        col.Item().Text(doc.Cookbook.Description!).FontSize(11).FontColor(Colors.Grey.Darken2);
                    col.Item().PaddingTop(8).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                });

                page.Content().PaddingTop(16).Column(col =>
                {
                    col.Spacing(20);
                    foreach (var recipe in doc.Recipes)
                    {
                        col.Item().Column(rc =>
                        {
                            rc.Spacing(8);
                            rc.Item().Text(recipe.Name).FontSize(16).SemiBold();

                            var meta = new List<string>();
                            if (recipe.PrepTimeMinutes.HasValue)
                                meta.Add($"Prep: {recipe.PrepTimeMinutes} min");
                            if (recipe.CookTimeMinutes.HasValue)
                                meta.Add($"Cook: {recipe.CookTimeMinutes} min");
                            meta.Add($"{recipe.Servings} servings");
                            if (recipe.Tags.Count > 0)
                                meta.Add("Tags: " + string.Join(", ", recipe.Tags));

                            rc.Item().Text(string.Join(" · ", meta)).FontSize(9).FontColor(Colors.Grey.Darken1);

                            if (recipe.Ingredients.Count > 0)
                            {
                                rc.Item().Text("Ingredients").SemiBold().FontSize(12);
                                foreach (var ing in recipe.Ingredients.OrderBy(i => i.LocalId))
                                {
                                    var line = $"{FractionFormatter.Format(ing.Amount)} {ing.Unit} {ing.Name}".Trim();
                                    if (!string.IsNullOrEmpty(ing.Note))
                                        line += $" ({ing.Note})";
                                    rc.Item().Text("• " + line).FontSize(10);
                                }
                            }

                            if (recipe.Steps.Count > 0)
                            {
                                rc.Item().PaddingTop(4).Text("Instructions").SemiBold().FontSize(12);
                                var n = 1;
                                foreach (var step in recipe.Steps)
                                {
                                    if (step.IsSection)
                                    {
                                        rc.Item().PaddingTop(6).Text(step.Text).SemiBold().FontSize(11);
                                        continue;
                                    }

                                    var body = RecipeStepTextFormatter.ToPlainText(step.Text);
                                    var stepLine = $"{n}. {body}";
                                    if (step.Timers is { Count: > 0 })
                                    {
                                        var t = string.Join(", ",
                                            step.Timers.Select(x =>
                                                $"{x.Duration} {x.Unit}" + (string.IsNullOrEmpty(x.Label) ? "" : $" ({x.Label})")));
                                        stepLine += $" [timer: {t}]";
                                    }

                                    rc.Item().Text(stepLine).FontSize(10);
                                    n++;
                                }
                            }
                        });
                    }
                });

                page.Footer().AlignCenter().Text(txt =>
                {
                    txt.DefaultTextStyle(x => x.FontSize(8).FontColor(Colors.Grey.Medium));
                    txt.Span("CookBot · exported ");
                    txt.Span(doc.ExportedAt);
                });
            });
        }).GeneratePdf();
    }
}
