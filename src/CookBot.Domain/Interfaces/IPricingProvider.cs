namespace CookBot.Domain.Interfaces;

public interface IPricingProvider
{
    Task<decimal?> GetPriceAsync(string externalId, CancellationToken ct = default);
}
