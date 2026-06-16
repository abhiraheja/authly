using Authly.Core.Entities;
using Authly.Core.Logging;
using Authly.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Authly.Infrastructure.Logging;

/// <summary>Forward reader over audit_logs for the log-streaming job (platform-level, no RLS).</summary>
public sealed class AuditLogStreamSource : IAuditLogStreamSource
{
    private readonly AppDbContext _db;

    public AuditLogStreamSource(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<AuditLog>> ReadAfterAsync(DateTimeOffset afterCreatedAt, int limit, CancellationToken ct = default)
        => await _db.AuditLogs.AsNoTracking()
            .Where(a => a.CreatedAt > afterCreatedAt)
            .OrderBy(a => a.CreatedAt).ThenBy(a => a.Id)
            .Take(limit)
            .ToListAsync(ct);
}
