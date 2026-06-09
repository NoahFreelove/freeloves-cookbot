using CookBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CookBot.Infrastructure.Data.Configurations;

/// <summary>
/// EF fluent configuration for <see cref="RecipePhoto"/> (GALLERY-01 / Phase 14 / Plan 14-01).
/// FK + cascade are configured on the child side so <c>RecipeConfiguration.cs</c> is untouched.
/// <c>ApplyConfigurationsFromAssembly</c> in <c>CookBotDbContext.OnModelCreating</c> discovers
/// this configuration automatically — no change to <c>CookBotDbContext.cs</c> needed.
/// </summary>
public class RecipePhotoConfiguration : IEntityTypeConfiguration<RecipePhoto>
{
    public void Configure(EntityTypeBuilder<RecipePhoto> builder)
    {
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Url)
            .HasMaxLength(2048)
            .IsRequired();

        builder.Property(p => p.Caption)
            .HasMaxLength(512);
        // Caption is nullable — no IsRequired()

        builder.Property(p => p.SortOrder)
            .HasDefaultValue(0);

        builder.Property(p => p.IsPrimary)
            .HasDefaultValue(false);

        // Composite index for GetPhotosAsync(recipeId) ordered by SortOrder
        builder.HasIndex(p => new { p.RecipeId, p.SortOrder });

        // FK configured on the child entity — RecipeConfiguration.cs stays untouched (D-14-02)
        builder.HasOne(p => p.Recipe)
            .WithMany(r => r.Photos)
            .HasForeignKey(p => p.RecipeId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
