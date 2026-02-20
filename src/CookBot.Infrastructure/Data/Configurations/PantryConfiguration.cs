using CookBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CookBot.Infrastructure.Data.Configurations;

public class PantryConfiguration : IEntityTypeConfiguration<Pantry>
{
    public void Configure(EntityTypeBuilder<Pantry> builder)
    {
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Name).HasMaxLength(100).IsRequired();
        builder.HasOne(p => p.Owner).WithMany(u => u.OwnedPantries).HasForeignKey(p => p.OwnerId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(p => p.Items).WithOne(i => i.Pantry).HasForeignKey(i => i.PantryId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(p => p.Members).WithOne(m => m.Pantry).HasForeignKey(m => m.PantryId).OnDelete(DeleteBehavior.Cascade);
    }
}
