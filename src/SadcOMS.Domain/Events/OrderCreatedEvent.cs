namespace SadcOMS.Domain.Events;

public record OrderCreatedEvent(
    Guid OrderId,
    Guid CustomerId,
    string CurrencyCode,
    decimal TotalAmount,
    DateTime CreatedAt,
    IReadOnlyList<OrderCreatedLineItem> LineItems);

public record OrderCreatedLineItem(
    Guid LineItemId,
    string ProductSku,
    int Quantity,
    decimal UnitPrice);

public static class OutboxMessageTypes
{
    public const string OrderCreated = "OrderCreated";
}
