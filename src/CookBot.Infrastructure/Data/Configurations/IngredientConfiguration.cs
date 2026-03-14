using CookBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CookBot.Infrastructure.Data.Configurations;

public class IngredientConfiguration : IEntityTypeConfiguration<Ingredient>
{
    public void Configure(EntityTypeBuilder<Ingredient> builder)
    {
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Name).HasMaxLength(200).IsRequired();
        builder.Property(i => i.NormalizedName).HasMaxLength(200).IsRequired();
        builder.HasIndex(i => i.NormalizedName).IsUnique();

        builder.Property(i => i.PreferredUnitsJson).HasMaxLength(500);
        builder.Property(i => i.ExternalId).HasMaxLength(100);
        builder.Property(i => i.NutritionalInfoJson).HasMaxLength(500);
    }
}
