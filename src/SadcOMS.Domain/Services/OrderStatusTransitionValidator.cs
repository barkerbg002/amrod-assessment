using SadcOMS.Domain.Enums;

namespace SadcOMS.Domain.Services;

public static class OrderStatusTransitionValidator
{
    private static readonly Dictionary<OrderStatus, HashSet<OrderStatus>> AllowedTransitions = new()
    {
        [OrderStatus.Pending] = new HashSet<OrderStatus> { OrderStatus.Paid, OrderStatus.Cancelled },
        [OrderStatus.Paid] = new HashSet<OrderStatus> { OrderStatus.Fulfilled, OrderStatus.Cancelled },
        [OrderStatus.Fulfilled] = new HashSet<OrderStatus>(),
        [OrderStatus.Cancelled] = new HashSet<OrderStatus>()
    };

    public static bool CanTransition(OrderStatus current, OrderStatus target) =>
        AllowedTransitions.TryGetValue(current, out var allowed) && allowed.Contains(target);

    public static void ValidateTransition(OrderStatus current, OrderStatus target)
    {
        if (!CanTransition(current, target))
        {
            throw new InvalidOrderStatusTransitionException(
                $"Cannot transition order from '{current}' to '{target}'.");
        }
    }
}

public class InvalidOrderStatusTransitionException : Exception
{
    public InvalidOrderStatusTransitionException(string message) : base(message) { }
}

public class ConcurrencyConflictException : Exception
{
    public ConcurrencyConflictException(string message) : base(message) { }
}
