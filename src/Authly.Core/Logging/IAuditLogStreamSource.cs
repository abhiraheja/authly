using Authly.Core.Entities;

namespace Authly.Core.Logging;

/// <summary>
/// Reads audit-log rows in forward (oldest-first) order for streaming to an external sink.
/// audit_logs is platform-level (no RLS), so this reads across all tenants for the operator's SIEM.
/// </summary>
public interface IAuditLogStreamSource
{
    /// <summary>Entries strictly after the cursor timestamp, oldest first, up to <paramref name="limit"/>.</summary>
    Task<IReadOnlyList<AuditLog>> ReadAfterAsync(DateTimeOffset afterCreatedAt, int limit, CancellationToken ct = default);
}
