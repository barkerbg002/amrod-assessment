using Microsoft.EntityFrameworkCore;
using SadcOMS.Domain.Entities;
using SadcOMS.Domain.Enums;
using SadcOMS.Infrastructure.Persistence;

namespace SadcOMS.Infrastructure.Repositories;

public class OrderRepository : IOrderRepository
{
    private readonly SadcOmsDbContext _context;

    public OrderRepository(SadcOmsDbContext context) => _context = context;

    public async Task<Order?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await _context.Orders.FindAsync([id], ct);

    public async Task<Order?> GetByIdWithLineItemsAsync(Guid id, CancellationToken ct = default) =>
        await _context.Orders
            .AsNoTracking()
            .Include(o => o.LineItems)
            .Include(o => o.Customer)
            .FirstOrDefaultAsync(o => o.Id == id, ct);

    public async Task<(IReadOnlyList<Order> Items, int TotalCount)> SearchAsync(
        Guid? customerId, OrderStatus? status, int page, int pageSize, string? sort,
        CancellationToken ct = default)
    {
        var query = _context.Orders.AsNoTracking().Include(o => o.LineItems).AsQueryable();

        if (customerId.HasValue)
            query = query.Where(o => o.CustomerId == customerId.Value);

        if (status.HasValue)
            query = query.Where(o => o.Status == status.Value);

        var totalCount = await query.CountAsync(ct);

        query = (sort?.ToLowerInvariant()) switch
        {
            "createdat_desc" => query.OrderByDescending(o => o.CreatedAt),
            "totalamount" => query.OrderBy(o => o.TotalAmount),
            "totalamount_desc" => query.OrderByDescending(o => o.TotalAmount),
            _ => query.OrderBy(o => o.CreatedAt)
        };

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task AddAsync(Order order, CancellationToken ct = default) =>
        await _context.Orders.AddAsync(order, ct);

    public async Task AddOutboxMessageAsync(OutboxMessage message, CancellationToken ct = default) =>
        await _context.OutboxMessages.AddAsync(message, ct);

    public Task SaveChangesAsync(CancellationToken ct = default) =>
        _context.SaveChangesAsync(ct);
}
