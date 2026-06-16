using System.Text.Json;
using SadcOMS.API.DTOs;
using SadcOMS.API.Mapping;
using SadcOMS.Domain.Entities;
using SadcOMS.Domain.Enums;
using SadcOMS.Domain.Events;
using SadcOMS.Domain.Services;
using SadcOMS.Infrastructure.Fx;
using SadcOMS.Infrastructure.Repositories;

namespace SadcOMS.API.Services;

public class CustomerService
{
    private readonly ICustomerRepository _customerRepository;

    public CustomerService(ICustomerRepository customerRepository) =>
        _customerRepository = customerRepository;

    public async Task<CustomerResponse> CreateAsync(CreateCustomerRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ArgumentException("Name is required.");
        if (string.IsNullOrWhiteSpace(request.Email))
            throw new ArgumentException("Email is required.");
        if (!SadcCurrencyValidator.IsSadCountry(request.CountryCode))
            throw new ArgumentException($"Country code '{request.CountryCode}' is not a supported SADC country.");

        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Email = request.Email.Trim().ToLowerInvariant(),
            CountryCode = request.CountryCode.ToUpperInvariant(),
            CreatedAt = DateTime.UtcNow
        };

        await _customerRepository.AddAsync(customer, ct);
        await _customerRepository.SaveChangesAsync(ct);
        return EntityMapper.ToResponse(customer);
    }

    public async Task<CustomerResponse?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var customer = await _customerRepository.GetByIdAsync(id, ct);
        return customer is null ? null : EntityMapper.ToResponse(customer);
    }

    public async Task<PagedResponse<CustomerResponse>> SearchAsync(
        string? search, int page, int pageSize, CancellationToken ct)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        page = Math.Max(page, 1);

        var (items, totalCount) = await _customerRepository.SearchAsync(search, page, pageSize, ct);
        var responses = items.Select(EntityMapper.ToResponse).ToList();
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        return new PagedResponse<CustomerResponse>(responses, page, pageSize, totalCount, totalPages);
    }
}

public class OrderService
{
    private readonly IOrderRepository _orderRepository;
    private readonly ICustomerRepository _customerRepository;

    public OrderService(IOrderRepository orderRepository, ICustomerRepository customerRepository)
    {
        _orderRepository = orderRepository;
        _customerRepository = customerRepository;
    }

    public async Task<OrderResponse> CreateAsync(CreateOrderRequest request, CancellationToken ct)
    {
        if (request.LineItems is null || request.LineItems.Count == 0)
            throw new ArgumentException("At least one line item is required.");

        var customer = await _customerRepository.GetByIdAsync(request.CustomerId, ct)
            ?? throw new KeyNotFoundException($"Customer '{request.CustomerId}' not found.");

        if (!SadcCurrencyValidator.IsValidPairing(customer.CountryCode, request.CurrencyCode))
        {
            throw new ArgumentException(
                $"Currency '{request.CurrencyCode}' is not valid for customer country '{customer.CountryCode}'.");
        }

        foreach (var item in request.LineItems)
            OrderTotalCalculator.ValidateLineItem(item.Quantity, item.UnitPrice);

        var orderId = Guid.NewGuid();
        var lineItems = request.LineItems.Select(li => new OrderLineItem
        {
            Id = Guid.NewGuid(),
            OrderId = orderId,
            ProductSku = li.ProductSku.Trim(),
            Quantity = li.Quantity,
            UnitPrice = li.UnitPrice
        }).ToList();

        var totalAmount = OrderTotalCalculator.Calculate(
            lineItems.Select(li => (li.Quantity, li.UnitPrice)));

        var order = new Order
        {
            Id = orderId,
            CustomerId = request.CustomerId,
            Status = OrderStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            CurrencyCode = request.CurrencyCode.ToUpperInvariant(),
            TotalAmount = totalAmount,
            LineItems = lineItems
        };

        var outboxEvent = new OrderCreatedEvent(
            order.Id,
            order.CustomerId,
            order.CurrencyCode,
            order.TotalAmount,
            order.CreatedAt,
            lineItems.Select(li => new OrderCreatedLineItem(
                li.Id, li.ProductSku, li.Quantity, li.UnitPrice)).ToList());

        var outboxMessage = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            Type = OutboxMessageTypes.OrderCreated,
            Payload = JsonSerializer.Serialize(outboxEvent),
            OccurredOn = DateTime.UtcNow
        };

        await _orderRepository.AddAsync(order, ct);
        await _orderRepository.AddOutboxMessageAsync(outboxMessage, ct);
        await _orderRepository.SaveChangesAsync(ct);

        return EntityMapper.ToResponse(order);
    }

    public async Task<OrderResponse?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var order = await _orderRepository.GetByIdWithLineItemsAsync(id, ct);
        return order is null ? null : EntityMapper.ToResponse(order);
    }

    public async Task<PagedResponse<OrderResponse>> SearchAsync(
        Guid? customerId, OrderStatus? status, int page, int pageSize, string? sort, CancellationToken ct)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        page = Math.Max(page, 1);

        var (items, totalCount) = await _orderRepository.SearchAsync(
            customerId, status, page, pageSize, sort, ct);
        var responses = items.Select(EntityMapper.ToResponse).ToList();
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        return new PagedResponse<OrderResponse>(responses, page, pageSize, totalCount, totalPages);
    }

    public async Task<OrderResponse> UpdateStatusAsync(
        Guid id, UpdateOrderStatusRequest request, CancellationToken ct)
    {
        var order = await _orderRepository.GetByIdWithLineItemsAsync(id, ct)
            ?? throw new KeyNotFoundException($"Order '{id}' not found.");

        var providedRowVersion = Convert.FromBase64String(request.RowVersion);
        if (!order.RowVersion.SequenceEqual(providedRowVersion))
            throw new ConcurrencyConflictException("The order was modified by another request.");

        OrderStatusTransitionValidator.ValidateTransition(order.Status, request.Status);

        // Re-fetch tracked entity for update
        var tracked = await _orderRepository.GetByIdAsync(id, ct)
            ?? throw new KeyNotFoundException($"Order '{id}' not found.");

        if (!tracked.RowVersion.SequenceEqual(providedRowVersion))
            throw new ConcurrencyConflictException("The order was modified by another request.");

        tracked.Status = request.Status;

        try
        {
            await _orderRepository.SaveChangesAsync(ct);
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException)
        {
            throw new ConcurrencyConflictException("The order was modified by another request.");
        }

        var updated = await _orderRepository.GetByIdWithLineItemsAsync(id, ct);
        return EntityMapper.ToResponse(updated!);
    }
}

public class ReportService
{
    private readonly IOrderRepository _orderRepository;
    private readonly IFxRateProvider _fxRateProvider;

    public ReportService(IOrderRepository orderRepository, IFxRateProvider fxRateProvider)
    {
        _orderRepository = orderRepository;
        _fxRateProvider = fxRateProvider;
    }

    public async Task<OrderZarReportResponse> GetOrdersInZarAsync(CancellationToken ct)
    {
        var (orders, _) = await _orderRepository.SearchAsync(null, null, 1, 10000, null, ct);
        var items = new List<OrderZarReportItem>();

        foreach (var order in orders)
        {
            var rate = await _fxRateProvider.GetRateToZarAsync(order.CurrencyCode, ct);
            items.Add(new OrderZarReportItem(
                order.Id,
                order.CustomerId,
                order.Status,
                order.CreatedAt,
                order.CurrencyCode,
                order.TotalAmount,
                rate.Rate,
                rate.RetrievedAt,
                FxConversionHelper.ConvertToZar(order.TotalAmount, rate.Rate)));
        }

        return new OrderZarReportResponse(items, DateTime.UtcNow);
    }
}

public class IdempotencyService
{
    private readonly IIdempotencyRepository _repository;

    public IdempotencyService(IIdempotencyRepository repository) => _repository = repository;

    public async Task<IdempotencyKey?> GetExistingAsync(
        string key, string requestPath, CancellationToken ct) =>
        await _repository.GetByKeyAndPathAsync(key, requestPath, ct);

    public async Task StoreAsync(
        string key, string requestPath, string responseBody, int statusCode, CancellationToken ct)
    {
        await _repository.AddAsync(new IdempotencyKey
        {
            Id = Guid.NewGuid(),
            Key = key,
            RequestPath = requestPath,
            ResponseBody = responseBody,
            StatusCode = statusCode,
            CreatedAt = DateTime.UtcNow
        }, ct);
        await _repository.SaveChangesAsync(ct);
    }
}
