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

        builder.OwnsMany(r => r.Steps, steps =>
        {
            steps.ToJson();
            steps.OwnsMany(s => s.Timers);
        });

        builder.HasMany(r => r.RecipeIngredients).WithOne(ri => ri.Recipe).HasForeignKey(ri => ri.RecipeId).OnDelete(DeleteBehavior.Cascade);
    }
}
