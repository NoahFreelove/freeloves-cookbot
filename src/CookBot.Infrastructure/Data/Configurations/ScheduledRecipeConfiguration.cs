using CookBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CookBot.Infrastructure.Data.Configurations;

public class ScheduledRecipeConfiguration : IEntityTypeConfiguration<ScheduledRecipe>
{
    public void Configure(EntityTypeBuilder<ScheduledRecipe> builder)
    {
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Notes).HasMaxLength(500);

        // (UserId, ScheduledFor) is the read path: GetUpcomingAsync filters
        // by user + sorts ascending by ScheduledFor.
        builder.HasIndex(s => new { s.UserId, s.ScheduledFor });

        builder.HasOne(s => s.Recipe).WithMany().HasForeignKey(s => s.RecipeId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(s => s.User).WithMany().HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
