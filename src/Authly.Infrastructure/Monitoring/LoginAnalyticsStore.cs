using Authly.Core.Interfaces;
using Authly.Core.Monitoring;
using Authly.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Authly.Infrastructure.Monitoring;

/// <summary>
/// Aggregates login outcomes across every tenant for the platform analytics chart. login_history
/// is RLS-protected, so each tenant scope is bound before counting (same pattern as the metrics
/// collector). Emits per-day success/failure totals only — no users, IPs, or other identifiers.
/// </summary>
public sealed class LoginAnalyticsStore : ILoginAnalyticsStore
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;

    public LoginAnalyticsStore(AppDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<IReadOnlyList<DailyLoginStat>> DailyOutcomesAsync(int days, CancellationToken ct = default)
    {
        if (days < 1) days = 1;
        var since = DateTimeOffset.UtcNow.AddDays(-days + 1).Date;
        var sinceOffset = new DateTimeOffset(since, TimeSpan.Zero);

        // Seed every day in the window with zeros so the chart has no gaps.
        var buckets = new Dictionary<DateOnly, (int ok, int fail)>();
        for (var d = 0; d < days; d++)
            buckets[DateOnly.FromDateTime(since.AddDays(d))] = (0, 0);

        var tenantIds = await _db.Tenants.AsNoTracking().Select(t => t.Id).ToListAsync(ct);
        foreach (var tid in tenantIds)
        {
            _tenant.SetTenant(tid);
            var rows = await _db.LoginHistory.AsNoTracking()
                .Where(h => h.TenantId == tid && h.CreatedAt >= sinceOffset)
                .Select(h => new { h.CreatedAt, h.Result })
                .ToListAsync(ct);

            foreach (var r in rows)
            {
                var day = DateOnly.FromDateTime(r.CreatedAt.UtcDateTime);
                if (!buckets.TryGetValue(day, out var cur)) continue;
                if (string.Equals(r.Result, "success", StringComparison.OrdinalIgnoreCase))
                    buckets[day] = (cur.ok + 1, cur.fail);
                else
                    buckets[day] = (cur.ok, cur.fail + 1);
            }
        }

        return buckets.OrderBy(b => b.Key)
            .Select(b => new DailyLoginStat(b.Key, b.Value.ok, b.Value.fail))
            .ToList();
    }
}
