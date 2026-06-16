using SadcOMS.Domain.Enums;
using SadcOMS.Domain.Services;

namespace SadcOMS.Tests.Domain;

public class OrderStatusTransitionValidatorTests
{
    [Theory]
    [InlineData(OrderStatus.Pending, OrderStatus.Paid, true)]
    [InlineData(OrderStatus.Pending, OrderStatus.Cancelled, true)]
    [InlineData(OrderStatus.Paid, OrderStatus.Fulfilled, true)]
    [InlineData(OrderStatus.Paid, OrderStatus.Cancelled, true)]
    public void CanTransition_ValidTransitions_ReturnsTrue(
        OrderStatus current, OrderStatus target, bool expected)
    {
        Assert.Equal(expected, OrderStatusTransitionValidator.CanTransition(current, target));
    }

    [Theory]
    [InlineData(OrderStatus.Pending, OrderStatus.Fulfilled)]
    [InlineData(OrderStatus.Pending, OrderStatus.Pending)]
    [InlineData(OrderStatus.Paid, OrderStatus.Pending)]
    [InlineData(OrderStatus.Paid, OrderStatus.Paid)]
    [InlineData(OrderStatus.Fulfilled, OrderStatus.Paid)]
    [InlineData(OrderStatus.Fulfilled, OrderStatus.Cancelled)]
    [InlineData(OrderStatus.Cancelled, OrderStatus.Pending)]
    [InlineData(OrderStatus.Cancelled, OrderStatus.Paid)]
    public void CanTransition_InvalidTransitions_ReturnsFalse(
        OrderStatus current, OrderStatus target)
    {
        Assert.False(OrderStatusTransitionValidator.CanTransition(current, target));
    }

    [Fact]
    public void ValidateTransition_Invalid_ThrowsInvalidOrderStatusTransitionException()
    {
        Assert.Throws<InvalidOrderStatusTransitionException>(() =>
            OrderStatusTransitionValidator.ValidateTransition(
                OrderStatus.Pending, OrderStatus.Fulfilled));
    }
}
