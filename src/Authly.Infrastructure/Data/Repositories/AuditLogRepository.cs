using Authly.Core.Common;
using Authly.Core.Entities;
using Authly.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Authly.Infrastructure.Data.Repositories;

/// <summary>Append-only: only inserts and reads are exposed; entries are never updated or deleted.</summary>
public sealed class AuditLogRepository : IAuditLogRepository
{
    private readonly AppDbContext _db;

    public AuditLogRepository(AppDbContext db) => _db = db;

    public async Task AddAsync(AuditLog entry, CancellationToken ct = default)
    {
        _db.AuditLogs.Add(entry);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<PagedResult<AuditLog>> ListByTenantAsync(
        Guid tenantId, Pagination page, string? @event = null, Guid? actorId = null, CancellationToken ct = default)
    {
        var query = _db.AuditLogs.Where(a => a.TenantId == tenantId);
        if (!string.IsNullOrWhiteSpace(@event)) query = query.Where(a => a.Event == @event);
        if (actorId is { } actor) query = query.Where(a => a.ActorId == actor);

        var total = await query.LongCountAsync(ct);
        var items = await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip(page.Skip).Take(page.Limit)
            .ToListAsync(ct);

        return new PagedResult<AuditLog>(items, total);
    }
}
