using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using SadcOMS.API.DTOs;
using SadcOMS.Domain.Enums;

namespace SadcOMS.Tests.Integration;

public class IdempotencyIntegrationTests : IClassFixture<SadcOmsWebApplicationFactory>
{
    private readonly SadcOmsWebApplicationFactory _factory;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    public IdempotencyIntegrationTests(SadcOmsWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task UpdateStatus_WithIdempotencyKey_ReturnsCachedResponseOnReplay()
    {
        var client = _factory.CreateClient();
        var idempotencyKey = Guid.NewGuid().ToString();

        var customer = await CreateCustomerAsync(client);
        var order = await CreateOrderAsync(client, customer.Id);

        var updateRequest = new UpdateOrderStatusRequest(OrderStatus.Paid, order.RowVersion);
        var request = new HttpRequestMessage(HttpMethod.Put, $"/api/orders/{order.Id}/status")
        {
            Content = JsonContent.Create(updateRequest)
        };
        request.Headers.Add("Idempotency-Key", idempotencyKey);

        var firstResponse = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        var firstBody = await firstResponse.Content.ReadAsStringAsync();

        var replayRequest = new HttpRequestMessage(HttpMethod.Put, $"/api/orders/{order.Id}/status")
        {
            Content = JsonContent.Create(updateRequest)
        };
        replayRequest.Headers.Add("Idempotency-Key", idempotencyKey);

        var secondResponse = await client.SendAsync(replayRequest);
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        var secondBody = await secondResponse.Content.ReadAsStringAsync();

        Assert.Equal(firstBody, secondBody);
    }

    [Fact]
    public async Task UpdateStatus_InvalidTransition_Returns409()
    {
        var client = _factory.CreateClient();
        var customer = await CreateCustomerAsync(client);
        var order = await CreateOrderAsync(client, customer.Id);

        var updateRequest = new UpdateOrderStatusRequest(OrderStatus.Fulfilled, order.RowVersion);
        var response = await client.PutAsJsonAsync(
            $"/api/orders/{order.Id}/status", updateRequest);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task UpdateStatus_SameIdempotencyKeyOnDifferentPath_DoesNotReturnCachedResponse()
    {
        var client = _factory.CreateClient();
        var idempotencyKey = Guid.NewGuid().ToString();

        var customer = await CreateCustomerAsync(client);
        var order1 = await CreateOrderAsync(client, customer.Id);
        var order2 = await CreateOrderAsync(client, customer.Id);

        var updateRequest1 = new UpdateOrderStatusRequest(OrderStatus.Paid, order1.RowVersion);
        var firstRequest = new HttpRequestMessage(HttpMethod.Put, $"/api/orders/{order1.Id}/status")
        {
            Content = JsonContent.Create(updateRequest1)
        };
        firstRequest.Headers.Add("Idempotency-Key", idempotencyKey);

        var firstResponse = await client.SendAsync(firstRequest);
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        var firstBody = await firstResponse.Content.ReadFromJsonAsync<OrderResponse>(_jsonOptions);

        var updateRequest2 = new UpdateOrderStatusRequest(OrderStatus.Paid, order2.RowVersion);
        var secondRequest = new HttpRequestMessage(HttpMethod.Put, $"/api/orders/{order2.Id}/status")
        {
            Content = JsonContent.Create(updateRequest2)
        };
        secondRequest.Headers.Add("Idempotency-Key", idempotencyKey);

        var secondResponse = await client.SendAsync(secondRequest);
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        var secondBody = await secondResponse.Content.ReadFromJsonAsync<OrderResponse>(_jsonOptions);

        Assert.NotNull(firstBody);
        Assert.NotNull(secondBody);
        Assert.Equal(order1.Id, firstBody.Id);
        Assert.Equal(order2.Id, secondBody.Id);
        Assert.NotEqual(firstBody.Id, secondBody.Id);
    }

    [Fact]
    public async Task UpdateStatus_StaleRowVersion_Returns409()
    {
        var client = _factory.CreateClient();
        var customer = await CreateCustomerAsync(client);
        var order = await CreateOrderAsync(client, customer.Id);
        var staleRowVersion = order.RowVersion;

        var paidRequest = new UpdateOrderStatusRequest(OrderStatus.Paid, staleRowVersion);
        var firstResponse = await client.PutAsJsonAsync(
            $"/api/orders/{order.Id}/status", paidRequest);
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);

        // InMemory provider does not bump rowversion; simulate a concurrent writer.
        await _factory.SimulateRowVersionBumpAsync(order.Id);

        var staleRequest = new UpdateOrderStatusRequest(OrderStatus.Fulfilled, staleRowVersion);
        var secondResponse = await client.PutAsJsonAsync(
            $"/api/orders/{order.Id}/status", staleRequest);

        Assert.Equal(HttpStatusCode.Conflict, secondResponse.StatusCode);
        var error = await secondResponse.Content.ReadFromJsonAsync<ErrorResponse>(_jsonOptions);
        Assert.NotNull(error);
        Assert.Contains("modified", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<CustomerResponse> CreateCustomerAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/customers", new CreateCustomerRequest(
            "Test Customer", $"test{Guid.NewGuid():N}@example.com", "ZA"));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CustomerResponse>(_jsonOptions))!;
    }

    private async Task<OrderResponse> CreateOrderAsync(HttpClient client, Guid customerId)
    {
        var response = await client.PostAsJsonAsync("/api/orders", new CreateOrderRequest(
            customerId, "ZAR",
            new[] { new CreateOrderLineItemRequest("SKU-001", 2, 50.00m) }));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<OrderResponse>(_jsonOptions))!;
    }
}
