namespace Authly.Core.Monitoring;

/// <summary>Liveness of a backing dependency, with a round-trip latency for the probe.</summary>
public sealed record DependencyHealth(string Name, bool Healthy, long LatencyMs, string? Detail = null);

/// <summary>Aggregate platform health snapshot for the super-admin dashboard.</summary>
public sealed record HealthReport(IReadOnlyList<DependencyHealth> Dependencies)
{
    public bool AllHealthy => Dependencies.All(d => d.Healthy);
}

/// <summary>One day's authentication outcome tally (aggregate counts only — no PII).</summary>
public sealed record DailyLoginStat(DateOnly Day, int Successes, int Failures);

/// <summary>
/// Probes backing services (database, cache) so the super admin can see platform health.
/// Lives in Infrastructure because it touches the DbContext and the Redis multiplexer directly.
/// </summary>
public interface IPlatformHealthProbe
{
    Task<HealthReport> CheckAsync(CancellationToken ct = default);
}

/// <summary>
/// Aggregates login outcomes across all tenants for platform analytics. Iterates tenants and
/// binds each scope so RLS-protected login_history rows are countable; emits daily totals only.
/// </summary>
public interface ILoginAnalyticsStore
{
    Task<IReadOnlyList<DailyLoginStat>> DailyOutcomesAsync(int days, CancellationToken ct = default);
}
