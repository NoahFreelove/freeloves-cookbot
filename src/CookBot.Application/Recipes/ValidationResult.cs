namespace CookBot.Application.Recipes;

/// <summary>A single validation error: machine-readable code + JSON-pointer-style path + human message.</summary>
public sealed record ValidationError(string Path, string Code, string Message);

/// <summary>A non-fatal validation warning (e.g. parser-level coercion).</summary>
public sealed record ValidationWarning(string Path, string Code, string Message);

/// <summary>
/// Result of running <see cref="RecipeValidator.Validate"/>. The validator never throws;
/// callers interpret <see cref="IsValid"/> and the lists.
/// </summary>
public sealed record ValidationResult(
    IReadOnlyList<ValidationError> Errors,
    IReadOnlyList<ValidationWarning> Warnings)
{
    public bool IsValid => Errors.Count == 0;

    public static ValidationResult Empty { get; } = new([], []);
}
