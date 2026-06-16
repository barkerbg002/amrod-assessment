using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace SadcOMS.Infrastructure.Fx;

public class FxCacheOptions
{
    public const string SectionName = "FxCache";
    public int TtlMinutes { get; set; } = 15;
}

public class MockFxRateProvider : IFxRateProvider
{
    private static readonly Dictionary<string, decimal> BaseRatesToZar = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ZAR"] = 1.0m,
        ["BWP"] = 1.35m,
        ["ZWL"] = 0.05m,
        ["USD"] = 18.50m,
        ["NAD"] = 1.0m,
        ["LSL"] = 1.0m,
        ["SZL"] = 1.0m,
        ["MZN"] = 0.28m,
        ["MWK"] = 0.009m,
        ["ZMW"] = 0.65m,
        ["AOA"] = 0.022m,
        ["CDF"] = 0.007m,
        ["TZS"] = 0.0075m,
        ["MGA"] = 0.004m,
        ["MUR"] = 0.40m,
        ["SCR"] = 1.35m,
        ["KMF"] = 0.04m,
    };

    private readonly IMemoryCache _cache;
    private readonly FxCacheOptions _options;
    private readonly Random _random = new(42);

    public MockFxRateProvider(IMemoryCache cache, IOptions<FxCacheOptions> options)
    {
        _cache = cache;
        _options = options.Value;
    }

    public async Task<FxRate> GetRateToZarAsync(string fromCurrency, CancellationToken ct = default)
    {
        var cacheKey = $"fx:{fromCurrency}:ZAR";
        if (_cache.TryGetValue(cacheKey, out FxRate? cached) && cached is not null)
            return cached;

        var rate = await Task.FromResult(GenerateRate(fromCurrency));
        _cache.Set(cacheKey, rate, TimeSpan.FromMinutes(_options.TtlMinutes));
        return rate;
    }

    public async Task<IReadOnlyDictionary<string, FxRate>> GetAllSadRatesToZarAsync(CancellationToken ct = default)
    {
        var result = new Dictionary<string, FxRate>(StringComparer.OrdinalIgnoreCase);
        foreach (var currency in BaseRatesToZar.Keys)
        {
            result[currency] = await GetRateToZarAsync(currency, ct);
        }
        return result;
    }

    private FxRate GenerateRate(string fromCurrency)
    {
        if (!BaseRatesToZar.TryGetValue(fromCurrency, out var baseRate))
            baseRate = 1.0m;

        // Small random jitter (+/- 2%) to simulate live rates
        var jitter = 1.0m + ((decimal)_random.NextDouble() * 0.04m - 0.02m);
        var rate = Math.Round(baseRate * jitter, 6, MidpointRounding.AwayFromZero);

        return new FxRate(fromCurrency, "ZAR", rate, DateTime.UtcNow);
    }
}

public static class FxConversionHelper
{
    public static decimal ConvertToZar(decimal amount, decimal rateToZar) =>
        Math.Round(amount * rateToZar, 2, MidpointRounding.AwayFromZero);
}
