using System.Diagnostics;
using Authly.Core.Monitoring;
using Authly.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;

namespace Authly.Infrastructure.Monitoring;

/// <summary>
/// Checks that PostgreSQL and Redis are reachable, timing each probe. Never throws — a failed
/// dependency is reported as unhealthy with its error so the dashboard stays available.
/// </summary>
public sealed class PlatformHealthProbe : IPlatformHealthProbe
{
    private readonly AppDbContext _db;
    private readonly IConnectionMultiplexer _redis;

    public PlatformHealthProbe(AppDbContext db, IConnectionMultiplexer redis)
    {
        _db = db;
        _redis = redis;
    }

    public async Task<HealthReport> CheckAsync(CancellationToken ct = default)
    {
        var deps = new List<DependencyHealth>
        {
            await ProbeAsync("PostgreSQL", async () => await _db.Database.CanConnectAsync(ct)),
            await ProbeAsync("Redis", async () =>
            {
                var pong = await _redis.GetDatabase().PingAsync();
                return pong >= TimeSpan.Zero;
            })
        };
        return new HealthReport(deps);
    }

    private static async Task<DependencyHealth> ProbeAsync(string name, Func<Task<bool>> check)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var ok = await check();
            sw.Stop();
            return new DependencyHealth(name, ok, sw.ElapsedMilliseconds, ok ? null : "Probe returned false");
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new DependencyHealth(name, false, sw.ElapsedMilliseconds, ex.GetType().Name);
        }
    }
}
