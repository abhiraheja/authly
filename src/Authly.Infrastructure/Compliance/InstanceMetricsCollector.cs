using Authly.Core.Compliance;
using Authly.Core.Interfaces;
using Authly.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Authly.Infrastructure.Compliance;

/// <summary>
/// Sums platform-wide aggregate counts for the self-host telemetry push. Iterates tenants and
/// counts within each tenant scope (RLS-protected tables can't be counted globally without a
/// tenant bound), then totals. Emits numbers only — never any identifying data.
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
        // Tenants table is not RLS-protected.
        var tenantIds = await _db.Tenants.AsNoTracking().Select(t => t.Id).ToListAsync(ct);

        var now = DateTimeOffset.UtcNow;
        int users = 0, apps = 0, sessions = 0;

        foreach (var tid in tenantIds)
        {
            // Bind the tenant so RLS lets these counts through; also filter explicitly (hard rule).
            _tenant.SetTenant(tid);
            users += await _db.Users.AsNoTracking().CountAsync(u => u.TenantId == tid, ct);
            apps += await _db.Applications.AsNoTracking().CountAsync(a => a.TenantId == tid, ct);
            sessions += await _db.Sessions.AsNoTracking()
                .CountAsync(s => s.TenantId == tid && !s.Revoked && s.ExpiresAt > now, ct);
        }

        return new InstanceMetrics(tenantIds.Count, users, apps, sessions);
    }
}
