using SadcOMS.Domain.Entities;
using SadcOMS.Domain.Enums;

namespace SadcOMS.Infrastructure.Repositories;

public interface ICustomerRepository
{
    Task<Customer?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<(IReadOnlyList<Customer> Items, int TotalCount)> SearchAsync(
        string? search, int page, int pageSize, CancellationToken ct = default);
    Task AddAsync(Customer customer, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}

public interface IOrderRepository
{
    Task<Order?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Order?> GetByIdWithLineItemsAsync(Guid id, CancellationToken ct = default);
    Task<(IReadOnlyList<Order> Items, int TotalCount)> SearchAsync(
        Guid? customerId, OrderStatus? status, int page, int pageSize, string? sort,
        CancellationToken ct = default);
    Task AddAsync(Order order, CancellationToken ct = default);
    Task AddOutboxMessageAsync(OutboxMessage message, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}

public interface IIdempotencyRepository
{
    Task<IdempotencyKey?> GetByKeyAndPathAsync(string key, string requestPath, CancellationToken ct = default);
    Task AddAsync(IdempotencyKey entry, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}

public interface IOutboxRepository
{
    Task<IReadOnlyList<OutboxMessage>> GetUnprocessedBatchAsync(int batchSize, CancellationToken ct = default);
    Task MarkProcessedAsync(Guid id, CancellationToken ct = default);
    Task MarkFailedAsync(Guid id, string error, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
