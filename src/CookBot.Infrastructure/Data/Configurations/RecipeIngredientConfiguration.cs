using CookBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CookBot.Infrastructure.Data.Configurations;

public class RecipeIngredientConfiguration : IEntityTypeConfiguration<RecipeIngredient>
{
    public void Configure(EntityTypeBuilder<RecipeIngredient> builder)
    {
        builder.HasKey(ri => ri.Id);

        // QOL-03 / Phase 8 AddPantryMatchIndexes — composite index for the pantry-match join
        // performance guarantee. Declaring it here keeps the EF model snapshot in sync with
        // the migration-applied index so a future scaffold migration does not accidentally drop it.
        builder.HasIndex(ri => new { ri.RecipeId, ri.IngredientId });

        builder.Property(ri => ri.Unit)
            .HasMaxLength(50)
            .IsRequired();

        builder.HasOne(ri => ri.Ingredient).WithMany(i => i.RecipeIngredients).HasForeignKey(ri => ri.IngredientId).OnDelete(DeleteBehavior.Restrict);
    }
}
