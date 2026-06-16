using SadcOMS.Domain.Services;

namespace SadcOMS.Tests.Domain;

public class SadcCurrencyValidatorTests
{
    [Theory]
    [InlineData("ZA", "ZAR", true)]
    [InlineData("BW", "BWP", true)]
    [InlineData("ZW", "ZWL", true)]
    [InlineData("ZW", "USD", true)]
    [InlineData("NA", "NAD", true)]
    [InlineData("LS", "LSL", true)]
    [InlineData("SZ", "SZL", true)]
    public void IsValidPairing_PrimaryCurrency_ReturnsTrue(
        string country, string currency, bool expected)
    {
        Assert.Equal(expected, SadcCurrencyValidator.IsValidPairing(country, currency));
    }

    [Theory]
    [InlineData("ZA", "NAD")]
    [InlineData("ZA", "LSL")]
    [InlineData("ZA", "SZL")]
    [InlineData("NA", "ZAR")]
    [InlineData("LS", "ZAR")]
    [InlineData("SZ", "ZAR")]
    public void IsValidPairing_CmaMutualAcceptance_ReturnsTrue(string country, string currency)
    {
        Assert.True(SadcCurrencyValidator.IsValidPairing(country, currency));
    }

    [Theory]
    [InlineData("ZA", "USD")]
    [InlineData("BW", "ZAR")]
    [InlineData("ZW", "ZAR")]
    [InlineData("XX", "ZAR")]
    public void IsValidPairing_InvalidPairing_ReturnsFalse(string country, string currency)
    {
        Assert.False(SadcCurrencyValidator.IsValidPairing(country, currency));
    }

    [Fact]
    public void GetAllowedCurrencies_CmaCountry_IncludesAllCmaCurrencies()
    {
        var allowed = SadcCurrencyValidator.GetAllowedCurrencies("ZA");
        Assert.Contains("ZAR", allowed, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("NAD", allowed, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("LSL", allowed, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("SZL", allowed, StringComparer.OrdinalIgnoreCase);
    }
}
