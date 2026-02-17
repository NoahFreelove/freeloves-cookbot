using CookBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CookBot.Infrastructure.Data.Configurations;

public class RecipeIngredientConfiguration : IEntityTypeConfiguration<RecipeIngredient>
{
    public void Configure(EntityTypeBuilder<RecipeIngredient> builder)
    {
        builder.HasKey(ri => ri.Id);
        builder.HasOne(ri => ri.Ingredient).WithMany(i => i.RecipeIngredients).HasForeignKey(ri => ri.IngredientId).OnDelete(DeleteBehavior.Restrict);
    }
}
