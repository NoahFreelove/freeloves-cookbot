using System.Text.Json.Nodes;

namespace CookBot.Application.Recipes;

/// <summary>
/// Orchestrates an ordered list of <see cref="IRecipeUpcaster"/> impls. Reads the input
/// node's <c>version</c> field (defaulting to 1 if absent — Pitfall H1 stamp), applies
/// upcasters in version order, and returns the upcasted node. Validates the chain has
/// no version gaps at construction time.
/// </summary>
public sealed class RecipeUpcasterChain
{
    /// <summary>Latest <see cref="Domain.Recipes.RecipeDocument.Version"/> the app understands.</summary>
    public const int CurrentVersion = 3;

    private readonly IReadOnlyList<IRecipeUpcaster> _upcasters;

    public RecipeUpcasterChain(IEnumerable<IRecipeUpcaster> upcasters)
    {
        _upcasters = upcasters.OrderBy(u => u.FromVersion).ToList();

        for (int i = 0; i < _upcasters.Count - 1; i++)
        {
            if (_upcasters[i].ToVersion != _upcasters[i + 1].FromVersion)
            {
                throw new InvalidOperationException(
                    $"Upcaster chain has a gap: {_upcasters[i].ToVersion} -> {_upcasters[i + 1].FromVersion}");
            }
        }
    }

    /// <summary>
    /// Upcasts <paramref name="input"/> through the registered chain to <see cref="CurrentVersion"/>.
    /// Throws <see cref="InvalidOperationException"/> if the input version is greater than
    /// what this build supports.
    /// </summary>
    public JsonNode UpcastToCurrent(JsonNode input)
    {
        var node = input;
        var version = node["version"]?.GetValue<int>() ?? 1;

        foreach (var upcaster in _upcasters)
        {
            if (version == CurrentVersion)
            {
                break;
            }
            if (upcaster.FromVersion != version)
            {
                continue;
            }
            node = upcaster.Upcast(node);
            version = upcaster.ToVersion;
        }

        if (version > CurrentVersion)
        {
            throw new InvalidOperationException(
                $"Recipe version {version} is newer than current ({CurrentVersion}). Update the app.");
        }

        return node;
    }
}
