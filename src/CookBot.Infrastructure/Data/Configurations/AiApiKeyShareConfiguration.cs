using CookBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CookBot.Infrastructure.Data.Configurations;

public class AiApiKeyShareConfiguration : IEntityTypeConfiguration<AiApiKeyShare>
{
    public void Configure(EntityTypeBuilder<AiApiKeyShare> builder)
    {
        builder.HasKey(s => s.Id);
        builder.HasIndex(s => new { s.OwnerUserId, s.RecipientUserId }).IsUnique();
        builder.HasOne(s => s.Owner).WithMany(u => u.AiApiKeySharesOwned).HasForeignKey(s => s.OwnerUserId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(s => s.Recipient).WithMany(u => u.AiApiKeySharesReceived).HasForeignKey(s => s.RecipientUserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
