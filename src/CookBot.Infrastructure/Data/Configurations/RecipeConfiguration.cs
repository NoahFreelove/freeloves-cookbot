using CookBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CookBot.Infrastructure.Data.Configurations;

public class RecipeConfiguration : IEntityTypeConfiguration<Recipe>
{
    public void Configure(EntityTypeBuilder<Recipe> builder)
    {
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Name).HasMaxLength(300).IsRequired();
        builder.Property(r => r.TagsJson).HasDefaultValue("[]");

        // Phase 1 / D-12: canonical RecipeDocument JSON snapshot. TEXT, nullable.
        builder.Property(r => r.CanonicalDocumentJson)
            .HasColumnType("TEXT");

        // Phase 8 / SCHEMA-05: hero photo URL, nullable. Max-length enforced via fluent API per D-28.
        builder.Property(r => r.PhotoUrl).HasMaxLength(2048);
        // Phase 8 / SCHEMA-06: recipe description, nullable. Max-length enforced via fluent API per D-28.
        builder.Property(r => r.Description).HasMaxLength(4096);

        builder.OwnsMany(r => r.Steps, steps =>
        {
            steps.ToJson();
            steps.OwnsMany(s => s.Timers);
        });

        builder.HasMany(r => r.RecipeIngredients).WithOne(ri => ri.Recipe).HasForeignKey(ri => ri.RecipeId).OnDelete(DeleteBehavior.Cascade);
    }
}
