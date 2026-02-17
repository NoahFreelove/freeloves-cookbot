using CookBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CookBot.Infrastructure.Data.Configurations;

public class GroceryListConfiguration : IEntityTypeConfiguration<GroceryList>
{
    public void Configure(EntityTypeBuilder<GroceryList> builder)
    {
        builder.HasKey(g => g.Id);
        builder.Property(g => g.Name).HasMaxLength(200).IsRequired();
        builder.HasMany(g => g.Items).WithOne(i => i.GroceryList).HasForeignKey(i => i.GroceryListId).OnDelete(DeleteBehavior.Cascade);
    }
}
