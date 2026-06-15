using Authly.Core.Entities;
using Authly.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Authly.Infrastructure.Data.Repositories;

public sealed class MessageLogRepository : IMessageLogRepository
{
    private readonly AppDbContext _db;

    public MessageLogRepository(AppDbContext db) => _db = db;

    public async Task AddAsync(MessageLog entry, CancellationToken ct = default)
    {
        _db.MessageLogs.Add(entry);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<MessageLog>> ListRecentByTenantAsync(Guid tenantId, int take, CancellationToken ct = default)
        => await _db.MessageLogs
            .Where(m => m.TenantId == tenantId)
            .OrderByDescending(m => m.CreatedAt)
            .Take(take)
            .ToListAsync(ct);
}
