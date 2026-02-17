using CookBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CookBot.Infrastructure.Data.Configurations;

public class PantryItemConfiguration : IEntityTypeConfiguration<PantryItem>
{
    public void Configure(EntityTypeBuilder<PantryItem> builder)
    {
        builder.HasKey(p => p.Id);
        builder.HasIndex(p => new { p.UserId, p.IngredientId }).IsUnique();
        builder.HasOne(p => p.Ingredient).WithMany(i => i.PantryItems).HasForeignKey(p => p.IngredientId).OnDelete(DeleteBehavior.Restrict);
    }
}
