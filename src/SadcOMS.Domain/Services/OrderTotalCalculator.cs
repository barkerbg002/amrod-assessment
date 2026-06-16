namespace SadcOMS.Domain.Services;

public static class OrderTotalCalculator
{
    public static decimal Calculate(IEnumerable<(int Quantity, decimal UnitPrice)> lineItems)
    {
        return lineItems.Sum(item => item.Quantity * item.UnitPrice);
    }

    public static void ValidateLineItem(int quantity, decimal unitPrice)
    {
        if (quantity <= 0)
            throw new ArgumentException("Quantity must be greater than zero.", nameof(quantity));

        if (unitPrice < 0)
            throw new ArgumentException("Unit price must be greater than or equal to zero.", nameof(unitPrice));
    }
}
