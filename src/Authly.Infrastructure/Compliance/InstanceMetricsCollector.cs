using Authly.Core.Compliance;
using Authly.Core.Interfaces;
using Authly.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Authly.Infrastructure.Compliance;

/// <summary>
/// Aggregate counts for the currently-bound project (the active tenant scope). Counts only within the
/// request's resolved tenant — RLS-protected tables are read under that existing binding, never
/// rebinding the set-once tenant context. Emits numbers only — never any identifying data. The
/// org-wide project count is supplied by the caller (the tenants table is not RLS-protected).
/// </summary>
public sealed class InstanceMetricsCollector : IInstanceMetricsCollector
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;

    public InstanceMetricsCollector(AppDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<InstanceMetrics> CollectAsync(CancellationToken ct = default)
    {
        if (_tenant.TenantId is not { } tid)
            return new InstanceMetrics(0, 0, 0, 0);

        var now = DateTimeOffset.UtcNow;
        // The request's tenant is already bound (RLS active); count within it, filtering explicitly too.
        var users = await _db.Users.AsNoTracking().CountAsync(u => u.TenantId == tid, ct);
        var apps = await _db.Applications.AsNoTracking().CountAsync(a => a.TenantId == tid, ct);
        var sessions = await _db.Sessions.AsNoTracking()
            .CountAsync(s => s.TenantId == tid && !s.Revoked && s.ExpiresAt > now, ct);

        return new InstanceMetrics(1, users, apps, sessions);
    }
}
