using CookBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CookBot.Infrastructure.Data.Configurations;

/// <summary>
/// Phase 9 / Plan 09-05 / PROD-14 + PITFALL M8 — EF configuration for the
/// <see cref="AiUsageLog"/> log row. Mirrors the <c>RecipeMadeConfiguration</c>
/// shape (composite index on (FK, Timestamp), explicit max-length on a name-style
/// column, two FKs back to <c>User</c>). Diverges from RecipeMade in two ways:
/// the cost column needs <c>decimal(18,6)</c> for sub-cent precision (PITFALL H10
/// + 09-RESEARCH Item 1 — Haiku 100/50 = $0.00035 must not round to 0), and the
/// KeyOwnerId FK uses <see cref="DeleteBehavior.Restrict"/> so historical telemetry
/// rows survive an admin deleting the key owner.
/// </summary>
public class AiUsageLogConfiguration : IEntityTypeConfiguration<AiUsageLog>
{
    public void Configure(EntityTypeBuilder<AiUsageLog> builder)
    {
        builder.HasKey(l => l.Id);

        builder.Property(l => l.ModelName)
            .HasMaxLength(80)
            .IsRequired();

        // 09-RESEARCH Item 1 — currency math; column type must preserve sub-cent precision
        // for Haiku-tier calls (a 100-input / 50-output Haiku call is $0.00035).
        builder.Property(l => l.EstimatedCostUsd)
            .HasColumnType("decimal(18, 6)");

        // PROD-14 / PITFALL M8 — composite index on (KeyOwnerId, Timestamp). The
        // Phase 10 widget aggregates "spending by owner over the last 30 days" so the
        // leading column is KeyOwnerId. SQLite ignores explicit DESC direction on
        // single-column index parts; the "DESC" intent lives in this comment.
        builder.HasIndex(l => new { l.KeyOwnerId, l.Timestamp })
            .HasDatabaseName("IX_AiUsageLogs_KeyOwnerId_Timestamp");

        // Cascade on UserId — deleting the triggering user removes their rows.
        builder.HasOne(l => l.User)
            .WithMany()
            .HasForeignKey(l => l.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Restrict on KeyOwnerId — telemetry has historical value; do not cascade-delete
        // log rows when the key owner is removed. Admins must explicitly purge.
        builder.HasOne(l => l.KeyOwner)
            .WithMany()
            .HasForeignKey(l => l.KeyOwnerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
