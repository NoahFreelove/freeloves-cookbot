using Microsoft.JSInterop;

namespace CookBot.Web.Services;

// Phase 7 / Plan 07-01: migrated from ISnackbar to ICbToastService alongside the
// CookbookList / CookbookDetail / ShareCookbookDialog rewrites (all callers).
// QuestPDF-backed PDF export through CookbookPdfService is preserved verbatim.
public static class CookbookDownloadHelper
{
    public static string SafeFileStem(string name)
    {
        var s = string.IsNullOrWhiteSpace(name) ? "cookbook" : name.Trim();
        foreach (var c in Path.GetInvalidFileNameChars())
            s = s.Replace(c, '_');
        return s;
    }

    public static async Task<bool> TryDownloadPdfAsync(
        CookbookTransferService transferService,
        CookbookPdfService pdfService,
        IJSRuntime js,
        int cookbookId,
        int userId,
        ICbToastService toast)
    {
        var doc = await transferService.BuildExportAsync(cookbookId, userId);
        if (doc == null)
        {
            toast.Show("You do not have access to export this cookbook.", CbToastSeverity.Error);
            return false;
        }

        var bytes = pdfService.GeneratePdf(doc);
        var stem = SafeFileStem(doc.Cookbook.Name);
        await js.InvokeVoidAsync("cookBotDownloadFile", $"{stem}.pdf", "application/pdf",
            Convert.ToBase64String(bytes));
        toast.Show("PDF download started.", CbToastSeverity.Success);
        return true;
    }

    public static async Task<bool> TryDownloadJsonAsync(
        CookbookTransferService transferService,
        IJSRuntime js,
        int cookbookId,
        int userId,
        ICbToastService toast)
    {
        var doc = await transferService.BuildExportAsync(cookbookId, userId);
        if (doc == null)
        {
            toast.Show("You do not have access to export this cookbook.", CbToastSeverity.Error);
            return false;
        }

        var bytes = CookbookTransferService.SerializeToUtf8Json(doc);
        var stem = SafeFileStem(doc.Cookbook.Name);
        await js.InvokeVoidAsync("cookBotDownloadFile", $"{stem}.cookbook.json", "application/json",
            Convert.ToBase64String(bytes));
        toast.Show("JSON download started.", CbToastSeverity.Success);
        return true;
    }
}
