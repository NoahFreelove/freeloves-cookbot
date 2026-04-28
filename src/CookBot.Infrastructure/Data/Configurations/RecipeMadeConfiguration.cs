using CookBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CookBot.Infrastructure.Data.Configurations;

public class RecipeMadeConfiguration : IEntityTypeConfiguration<RecipeMade>
{
    public void Configure(EntityTypeBuilder<RecipeMade> builder)
    {
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Notes).HasMaxLength(1000);

        // (UserId, CompletedAt DESC) supports the "Recently cooked" feed on Home.
        builder.HasIndex(r => new { r.UserId, r.CompletedAt });
        // (RecipeId, CompletedAt DESC) supports the per-recipe last-cook callout +
        // made-count surfacing in RecipeView.
        builder.HasIndex(r => new { r.RecipeId, r.CompletedAt });

        builder.HasOne(r => r.Recipe).WithMany().HasForeignKey(r => r.RecipeId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(r => r.User).WithMany().HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
