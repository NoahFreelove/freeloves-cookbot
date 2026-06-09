namespace CookBot.Application.Services;

/// <summary>
/// Abstracts physical file deletion for local <c>/uploads/…</c> photo paths.
/// Implemented by <c>LocalRecipePhotoStorage</c> in the Web layer (which has the
/// <see cref="Microsoft.AspNetCore.Hosting.IWebHostEnvironment"/> dependency).
/// This interface lives in Application so that <see cref="RecipeService"/> and
/// <see cref="RecipePhotoService"/> can depend on it without creating a project
/// reference from Application → Web (Clean Architecture invariant).
/// </summary>
public interface IRecipePhotoFileStorage
{
    /// <summary>
    /// Deletes the physical file for a local <c>/uploads/{guid}.ext</c> URL.
    /// No-op when the file does not exist (non-fatal).
    /// Throws <see cref="InvalidOperationException"/> on path-traversal attempt.
    /// </summary>
    void DeletePhysicalFile(string url);
}
