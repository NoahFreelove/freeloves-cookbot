using CookBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CookBot.Infrastructure.Data.Configurations;

public class PantryMemberConfiguration : IEntityTypeConfiguration<PantryMember>
{
    public void Configure(EntityTypeBuilder<PantryMember> builder)
    {
        builder.HasKey(pm => pm.Id);
        builder.HasIndex(pm => new { pm.PantryId, pm.UserId }).IsUnique();
        builder.HasOne(pm => pm.User).WithMany(u => u.PantryMemberships).HasForeignKey(pm => pm.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
