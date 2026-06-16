using SadcOMS.Infrastructure.Fx;

namespace SadcOMS.Tests.Domain;

public class FxConversionHelperTests
{
    [Theory]
    [InlineData(100.00, 1.0, 100.00)]
    [InlineData(100.00, 18.50, 1850.00)]
    [InlineData(10.555, 1.0, 10.56)]  // AwayFromZero: 10.555 → 10.56
    [InlineData(10.554, 1.0, 10.55)]
    public void ConvertToZar_RoundsAwayFromZero(decimal amount, decimal rate, decimal expected)
    {
        var result = FxConversionHelper.ConvertToZar(amount, rate);
        Assert.Equal(expected, result);
    }
}
