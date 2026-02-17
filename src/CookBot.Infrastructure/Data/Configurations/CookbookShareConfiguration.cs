using CookBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CookBot.Infrastructure.Data.Configurations;

public class CookbookShareConfiguration : IEntityTypeConfiguration<CookbookShare>
{
    public void Configure(EntityTypeBuilder<CookbookShare> builder)
    {
        builder.HasKey(cs => cs.Id);
        builder.HasIndex(cs => new { cs.CookbookId, cs.SharedWithUserId }).IsUnique();
        builder.HasOne(cs => cs.Cookbook).WithMany(c => c.Shares).HasForeignKey(cs => cs.CookbookId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(cs => cs.SharedWithUser).WithMany().HasForeignKey(cs => cs.SharedWithUserId).OnDelete(DeleteBehavior.Cascade);
    }
}
