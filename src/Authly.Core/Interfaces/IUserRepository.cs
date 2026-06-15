using Authly.Core.Entities;

namespace Authly.Core.Interfaces;

/// <summary>
/// Persistence for <see cref="User"/>. All lookups are tenant-scoped because email is
/// unique only per tenant — callers must always pass the resolved tenant id.
/// Implemented in Infrastructure.
/// </summary>
public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task<User?> GetByEmailAsync(Guid tenantId, string email, CancellationToken ct = default);

    /// <summary>All users in a tenant (newest first), for tenant-admin management screens.</summary>
    Task<IReadOnlyList<User>> ListByTenantAsync(Guid tenantId, CancellationToken ct = default);
    Task<bool> EmailExistsAsync(Guid tenantId, string email, CancellationToken ct = default);

    /// <summary>True if the tenant already has at least one tenant admin (used for first-admin bootstrap).</summary>
    Task<bool> AnyTenantAdminAsync(Guid tenantId, CancellationToken ct = default);
    Task AddAsync(User user, CancellationToken ct = default);
    Task UpdateAsync(User user, CancellationToken ct = default);
}
