using CookBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CookBot.Infrastructure.Data.Configurations;

public class GroceryListItemConfiguration : IEntityTypeConfiguration<GroceryListItem>
{
    public void Configure(EntityTypeBuilder<GroceryListItem> builder)
    {
        builder.HasKey(i => i.Id);
        builder.HasOne(i => i.Ingredient).WithMany().HasForeignKey(i => i.IngredientId).OnDelete(DeleteBehavior.Restrict);
    }
}
