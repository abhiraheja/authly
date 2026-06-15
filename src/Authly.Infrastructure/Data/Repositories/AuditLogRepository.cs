using Authly.Core.Entities;
using Authly.Core.Interfaces;

namespace Authly.Infrastructure.Data.Repositories;

/// <summary>Append-only: only inserts are exposed; entries are never updated or deleted.</summary>
public sealed class AuditLogRepository : IAuditLogRepository
{
    private readonly AppDbContext _db;

    public AuditLogRepository(AppDbContext db) => _db = db;

    public async Task AddAsync(AuditLog entry, CancellationToken ct = default)
    {
        _db.AuditLogs.Add(entry);
        await _db.SaveChangesAsync(ct);
    }
}
