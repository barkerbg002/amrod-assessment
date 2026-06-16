namespace SadcOMS.Infrastructure.Fx;

public record FxRate(string FromCurrency, string ToCurrency, decimal Rate, DateTime RetrievedAt);

public interface IFxRateProvider
{
    Task<FxRate> GetRateToZarAsync(string fromCurrency, CancellationToken ct = default);
    Task<IReadOnlyDictionary<string, FxRate>> GetAllSadRatesToZarAsync(CancellationToken ct = default);
}
