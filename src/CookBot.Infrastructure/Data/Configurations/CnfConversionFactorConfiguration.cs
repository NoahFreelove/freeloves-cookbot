using CookBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CookBot.Infrastructure.Data.Configurations;

/// <summary>
/// EF fluent configuration for <see cref="CnfConversionFactor"/> (NUTR-01 / Phase 15 / Plan 15-03).
/// Discovered automatically by ApplyConfigurationsFromAssembly — no change to CookBotDbContext.cs needed.
/// The HasMany side of the CnfFood → CnfConversionFactor relationship is configured in CnfFoodConfiguration.
/// </summary>
public class CnfConversionFactorConfiguration : IEntityTypeConfiguration<CnfConversionFactor>
{
    public void Configure(EntityTypeBuilder<CnfConversionFactor> builder)
    {
        builder.HasKey(cf => cf.Id);

        // Index for GetFactors(foodId) lookup queries
        builder.HasIndex(cf => cf.FoodId);

        builder.Property(cf => cf.MeasureDescription).HasMaxLength(100).IsRequired();
        // FK configured on child side — CnfFoodConfiguration configures the HasMany side
    }
}
