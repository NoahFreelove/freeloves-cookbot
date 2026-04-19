using Microsoft.JSInterop;
using MudBlazor;

namespace CookBot.Web.Services;

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
        ISnackbar snackbar)
    {
        var doc = await transferService.BuildExportAsync(cookbookId, userId);
        if (doc == null)
        {
            snackbar.Add("You do not have access to export this cookbook.", Severity.Error);
            return false;
        }

        var bytes = pdfService.GeneratePdf(doc);
        var stem = SafeFileStem(doc.Cookbook.Name);
        await js.InvokeVoidAsync("cookBotDownloadFile", $"{stem}.pdf", "application/pdf",
            Convert.ToBase64String(bytes));
        snackbar.Add("PDF download started.", Severity.Success);
        return true;
    }

    public static async Task<bool> TryDownloadJsonAsync(
        CookbookTransferService transferService,
        IJSRuntime js,
        int cookbookId,
        int userId,
        ISnackbar snackbar)
    {
        var doc = await transferService.BuildExportAsync(cookbookId, userId);
        if (doc == null)
        {
            snackbar.Add("You do not have access to export this cookbook.", Severity.Error);
            return false;
        }

        var bytes = CookbookTransferService.SerializeToUtf8Json(doc);
        var stem = SafeFileStem(doc.Cookbook.Name);
        await js.InvokeVoidAsync("cookBotDownloadFile", $"{stem}.cookbook.json", "application/json",
            Convert.ToBase64String(bytes));
        snackbar.Add("JSON download started.", Severity.Success);
        return true;
    }
}
