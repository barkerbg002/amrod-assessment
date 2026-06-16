using Microsoft.EntityFrameworkCore;
using SadcOMS.Domain.Entities;
using SadcOMS.Infrastructure.Persistence;

namespace SadcOMS.Infrastructure.Repositories;

public class IdempotencyRepository : IIdempotencyRepository
{
    private readonly SadcOmsDbContext _context;

    public IdempotencyRepository(SadcOmsDbContext context) => _context = context;

    public async Task<IdempotencyKey?> GetByKeyAndPathAsync(
        string key, string requestPath, CancellationToken ct = default) =>
        await _context.IdempotencyKeys.AsNoTracking()
            .FirstOrDefaultAsync(k => k.Key == key && k.RequestPath == requestPath, ct);

    public async Task AddAsync(IdempotencyKey entry, CancellationToken ct = default) =>
        await _context.IdempotencyKeys.AddAsync(entry, ct);

    public Task SaveChangesAsync(CancellationToken ct = default) =>
        _context.SaveChangesAsync(ct);
}
