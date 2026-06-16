using System.Text.Json;
using System.Text.Json.Serialization;
using SadcOMS.API.DTOs;
using SadcOMS.Domain.Entities;
using SadcOMS.Domain.Enums;

namespace SadcOMS.API.Mapping;

public static class EntityMapper
{
    public static CustomerResponse ToResponse(Customer customer) =>
        new(customer.Id, customer.Name, customer.Email, customer.CountryCode, customer.CreatedAt);

    public static OrderResponse ToResponse(Order order)
    {
        var lineItems = order.LineItems.Select(li => new OrderLineItemResponse(
            li.Id, li.ProductSku, li.Quantity, li.UnitPrice, li.Quantity * li.UnitPrice)).ToList();

        return new OrderResponse(
            order.Id,
            order.CustomerId,
            order.Status,
            order.CreatedAt,
            order.CurrencyCode,
            order.TotalAmount,
            Convert.ToBase64String(order.RowVersion),
            lineItems);
    }

    public static string SerializeOrderResponse(OrderResponse response) =>
        JsonSerializer.Serialize(response, ApiJsonOptions.Default);
}

public static class ApiJsonOptions
{
    public static readonly JsonSerializerOptions Default = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };
}
