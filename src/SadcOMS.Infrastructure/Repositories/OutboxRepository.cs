using Microsoft.EntityFrameworkCore;
using SadcOMS.Domain.Entities;
using SadcOMS.Infrastructure.Persistence;

namespace SadcOMS.Infrastructure.Repositories;

public class OutboxRepository : IOutboxRepository
{
    private readonly SadcOmsDbContext _context;

    public OutboxRepository(SadcOmsDbContext context) => _context = context;

    public async Task<IReadOnlyList<OutboxMessage>> GetUnprocessedBatchAsync(
        int batchSize, CancellationToken ct = default)
    {
        return await _context.OutboxMessages
            .Where(m => m.ProcessedOn == null)
            .OrderBy(m => m.OccurredOn)
            .Take(batchSize)
            .ToListAsync(ct);
    }

    public async Task MarkProcessedAsync(Guid id, CancellationToken ct = default)
    {
        var message = await _context.OutboxMessages.FindAsync([id], ct);
        if (message is not null)
        {
            message.ProcessedOn = DateTime.UtcNow;
            message.Error = null;
        }
    }

    public async Task MarkFailedAsync(Guid id, string error, CancellationToken ct = default)
    {
        var message = await _context.OutboxMessages.FindAsync([id], ct);
        if (message is not null)
            message.Error = error;
    }

    public Task SaveChangesAsync(CancellationToken ct = default) =>
        _context.SaveChangesAsync(ct);
}
