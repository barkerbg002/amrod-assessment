using SadcOMS.Domain.Enums;

namespace SadcOMS.API.DTOs;

public record CreateCustomerRequest(string Name, string Email, string CountryCode);

public record CustomerResponse(
    Guid Id,
    string Name,
    string Email,
    string CountryCode,
    DateTime CreatedAt);

public record PagedResponse<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);

public record CreateOrderLineItemRequest(string ProductSku, int Quantity, decimal UnitPrice);

public record CreateOrderRequest(
    Guid CustomerId,
    string CurrencyCode,
    IReadOnlyList<CreateOrderLineItemRequest> LineItems);

public record OrderLineItemResponse(
    Guid Id,
    string ProductSku,
    int Quantity,
    decimal UnitPrice,
    decimal LineTotal);

public record OrderResponse(
    Guid Id,
    Guid CustomerId,
    OrderStatus Status,
    DateTime CreatedAt,
    string CurrencyCode,
    decimal TotalAmount,
    string RowVersion,
    IReadOnlyList<OrderLineItemResponse> LineItems);

public record UpdateOrderStatusRequest(OrderStatus Status, string RowVersion);

public record OrderZarReportItem(
    Guid OrderId,
    Guid CustomerId,
    OrderStatus Status,
    DateTime CreatedAt,
    string OriginalCurrency,
    decimal OriginalAmount,
    decimal RateToZar,
    DateTime RateRetrievedAt,
    decimal AmountInZar);

public record OrderZarReportResponse(
    IReadOnlyList<OrderZarReportItem> Orders,
    DateTime GeneratedAt);

public record ErrorResponse(string Message, string? Details = null);

public record TokenResponse(string AccessToken, int ExpiresIn);
