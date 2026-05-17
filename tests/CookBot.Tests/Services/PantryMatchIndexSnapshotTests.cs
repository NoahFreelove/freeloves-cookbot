using CookBot.Domain.Entities;
using CookBot.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CookBot.Tests.Services;

/// <summary>
/// Phase 10 / QOL-03 / Plan 10-04 — EF model snapshot assertions that guard the Phase 8
/// composite indexes against accidental removal during future migrations.
/// Uses EnsureCreated() on in-memory SQLite, same pattern as OwnershipTests.cs — the EF
/// model introspection API reads from the code-defined model (IEntityTypeConfiguration),
/// not from the raw SQLite schema.
/// </summary>
public class PantryMatchIndexSnapshotTests : IDisposable
{
    private readonly CookBotDbContext _db;

    public PantryMatchIndexSnapshotTests()
    {
        var options = new DbContextOptionsBuilder<CookBotDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;
        _db = new CookBotDbContext(options);
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public void RecipeIngredient_HasCompositeIndexOn_RecipeId_IngredientId()
    {
        // Arrange — read the EF model snapshot (not the migration file).
        var entityType = _db.Model.FindEntityType(typeof(RecipeIngredient));
        Assert.NotNull(entityType);

        var indexes = entityType.GetIndexes().ToList();

        // Act + Assert — at least one index covers (RecipeId, IngredientId) in that order.
        var hasIndex = indexes.Any(ix =>
            ix.Properties
              .Select(p => p.Name)
              .SequenceEqual(new[] { "RecipeId", "IngredientId" }));

        Assert.True(
            hasIndex,
            "Expected a composite index on RecipeIngredient(RecipeId, IngredientId) " +
            "— Phase 8 AddPantryMatchIndexes migration added it for QOL-03 join performance. " +
            $"Found indexes: [{string.Join(", ", indexes.Select(ix => $"({string.Join(", ", ix.Properties.Select(p => p.Name))})"))}]");
    }

    [Fact]
    public void PantryItem_HasCompositeIndexOn_PantryId_IngredientId()
    {
        // Arrange — read the EF model snapshot (not the migration file).
        var entityType = _db.Model.FindEntityType(typeof(PantryItem));
        Assert.NotNull(entityType);

        var indexes = entityType.GetIndexes().ToList();

        // Act + Assert — at least one index covers (PantryId, IngredientId) in that order.
        var hasIndex = indexes.Any(ix =>
            ix.Properties
              .Select(p => p.Name)
              .SequenceEqual(new[] { "PantryId", "IngredientId" }));

        Assert.True(
            hasIndex,
            "Expected a composite index on PantryItem(PantryId, IngredientId) " +
            "— PantryItemConfiguration.HasIndex declares it as a UNIQUE index for the pantry-match join (QOL-03). " +
            $"Found indexes: [{string.Join(", ", indexes.Select(ix => $"({string.Join(", ", ix.Properties.Select(p => p.Name))})"))}]");
    }

    [Fact]
    public void AddPantryMatchIndexes_MigrationFile_Exists()
    {
        // Scan for the migration file by name pattern — fails loud if it is deleted,
        // which would indicate the Phase 8 composite index guarantee was broken.
        // AppContext.BaseDirectory = .../tests/CookBot.Tests/bin/Debug/net10.0/
        // Navigate: net10.0/ → Debug/ → bin/ → CookBot.Tests/ → tests/ → (repo root)
        var migrationsDir = Path.GetFullPath(
            Path.Combine(
                AppContext.BaseDirectory,
                "../../../../../src/CookBot.Infrastructure/Migrations"));

        var migrationFile = Directory.GetFiles(
            migrationsDir,
            "*_AddPantryMatchIndexes.cs",
            SearchOption.TopDirectoryOnly)
            .FirstOrDefault();

        Assert.True(
            migrationFile != null,
            $"Expected to find a migration file matching '*_AddPantryMatchIndexes.cs' in " +
            $"'{migrationsDir}'. If the migration was deleted, the Phase 8 QOL-03 composite " +
            $"index guarantee may have been broken — restore or re-create the migration.");
    }
}
