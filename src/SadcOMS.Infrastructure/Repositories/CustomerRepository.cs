using Microsoft.EntityFrameworkCore;
using SadcOMS.Domain.Entities;
using SadcOMS.Infrastructure.Persistence;

namespace SadcOMS.Infrastructure.Repositories;

public class CustomerRepository : ICustomerRepository
{
    private readonly SadcOmsDbContext _context;

    public CustomerRepository(SadcOmsDbContext context) => _context = context;

    public async Task<Customer?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await _context.Customers.FindAsync([id], ct);

    public async Task<(IReadOnlyList<Customer> Items, int TotalCount)> SearchAsync(
        string? search, int page, int pageSize, CancellationToken ct = default)
    {
        var query = _context.Customers.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(c =>
                c.Name.Contains(term) || c.Email.Contains(term));
        }

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderBy(c => c.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task AddAsync(Customer customer, CancellationToken ct = default) =>
        await _context.Customers.AddAsync(customer, ct);

    public Task SaveChangesAsync(CancellationToken ct = default) =>
        _context.SaveChangesAsync(ct);
}
