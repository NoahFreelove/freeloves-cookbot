using CookBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CookBot.Infrastructure.Data.Configurations;

public class RecipeTagConfiguration : IEntityTypeConfiguration<RecipeTag>
{
    public void Configure(EntityTypeBuilder<RecipeTag> builder)
    {
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Name).HasMaxLength(200).IsRequired();
        builder.HasIndex(t => new { t.RecipeId, t.Name }).IsUnique();
        builder.HasOne(t => t.Recipe)
            .WithMany(r => r.Tags)
            .HasForeignKey(t => t.RecipeId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
