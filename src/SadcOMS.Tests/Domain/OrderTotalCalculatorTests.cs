using SadcOMS.Domain.Services;

namespace SadcOMS.Tests.Domain;

public class OrderTotalCalculatorTests
{
    [Fact]
    public void Calculate_SumsQuantityTimesUnitPrice()
    {
        var lineItems = new[]
        {
            (Quantity: 2, UnitPrice: 10.50m),
            (Quantity: 3, UnitPrice: 5.00m),
            (Quantity: 1, UnitPrice: 100.00m)
        };

        var total = OrderTotalCalculator.Calculate(lineItems);

        Assert.Equal(136.00m, total);
    }

    [Fact]
    public void Calculate_EmptyLineItems_ReturnsZero()
    {
        Assert.Equal(0m, OrderTotalCalculator.Calculate(Array.Empty<(int, decimal)>()));
    }

    [Theory]
    [InlineData(0, 10.0)]
    [InlineData(-1, 10.0)]
    public void ValidateLineItem_InvalidQuantity_Throws(int quantity, decimal unitPrice)
    {
        Assert.Throws<ArgumentException>(() =>
            OrderTotalCalculator.ValidateLineItem(quantity, unitPrice));
    }

    [Theory]
    [InlineData(1, -0.01)]
    public void ValidateLineItem_NegativeUnitPrice_Throws(int quantity, decimal unitPrice)
    {
        Assert.Throws<ArgumentException>(() =>
            OrderTotalCalculator.ValidateLineItem(quantity, unitPrice));
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(5, 99.99)]
    public void ValidateLineItem_ValidInput_DoesNotThrow(int quantity, decimal unitPrice)
    {
        var ex = Record.Exception(() =>
            OrderTotalCalculator.ValidateLineItem(quantity, unitPrice));
        Assert.Null(ex);
    }
}
