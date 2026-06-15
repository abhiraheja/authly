using Authly.Core.Common;
using Authly.Core.Entities;

namespace Authly.Core.Interfaces;

/// <summary>Append-only persistence for <see cref="AuditLog"/>. Implemented in Infrastructure.</summary>
public interface IAuditLogRepository
{
    Task AddAsync(AuditLog entry, CancellationToken ct = default);

    /// <summary>Paginated, newest-first audit entries for a tenant, optionally filtered by event or actor.</summary>
    Task<PagedResult<AuditLog>> ListByTenantAsync(
        Guid tenantId, Pagination page, string? @event = null, Guid? actorId = null, CancellationToken ct = default);
}
