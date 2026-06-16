namespace SadcOMS.Domain.Services;

/// <summary>
/// SADC country/currency validation including CMA (Common Monetary Area) logic.
/// CMA currencies (ZAR, NAD, LSL, SZL) are mutually acceptable for ZA, NA, LS, SZ.
/// </summary>
public static class SadcCurrencyValidator
{
    private static readonly HashSet<string> CmaCurrencies = new(StringComparer.OrdinalIgnoreCase)
    {
        "ZAR", "NAD", "LSL", "SZL"
    };

    private static readonly Dictionary<string, HashSet<string>> CountryToCurrencies = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ZA"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "ZAR" },
        ["BW"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "BWP" },
        ["ZW"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "ZWL", "USD" },
        ["NA"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "NAD" },
        ["LS"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "LSL" },
        ["SZ"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SZL" },
        ["MZ"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "MZN" },
        ["MW"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "MWK" },
        ["ZM"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "ZMW" },
        ["AO"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "AOA" },
        ["CD"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "CDF" },
        ["TZ"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "TZS" },
        ["MG"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "MGA" },
        ["MU"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "MUR" },
        ["SC"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SCR" },
        ["KM"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "KMF" },
    };

    private static readonly HashSet<string> CmaCountries = new(StringComparer.OrdinalIgnoreCase)
    {
        "ZA", "NA", "LS", "SZ"
    };

    public static bool IsValidPairing(string countryCode, string currencyCode)
    {
        if (string.IsNullOrWhiteSpace(countryCode) || string.IsNullOrWhiteSpace(currencyCode))
            return false;

        if (!CountryToCurrencies.TryGetValue(countryCode, out var allowedCurrencies))
            return false;

        if (allowedCurrencies.Contains(currencyCode))
            return true;

        // CMA logic: ZAR/NAD/LSL/SZL mutually acceptable for ZA/NA/LS/SZ
        if (CmaCountries.Contains(countryCode) && CmaCurrencies.Contains(currencyCode))
            return true;

        return false;
    }

    public static IReadOnlyCollection<string> GetAllowedCurrencies(string countryCode)
    {
        if (!CountryToCurrencies.TryGetValue(countryCode, out var baseCurrencies))
            return Array.Empty<string>();

        if (CmaCountries.Contains(countryCode))
        {
            return baseCurrencies.Union(CmaCurrencies).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        return baseCurrencies.ToList();
    }

    public static bool IsSadCountry(string countryCode) =>
        CountryToCurrencies.ContainsKey(countryCode);
}
