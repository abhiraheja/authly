using Authly.Core.Entities;

namespace Authly.Core.Interfaces;

/// <summary>Tenant-scoped persistence for ABAC access policies.</summary>
public interface IAccessPolicyRepository
{
    Task<IReadOnlyList<AccessPolicy>> ListByTenantAsync(Guid tenantId, CancellationToken ct = default);
    Task<IReadOnlyList<AccessPolicy>> ListEnabledAsync(Guid tenantId, CancellationToken ct = default);
    Task<AccessPolicy?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task AddAsync(AccessPolicy policy, CancellationToken ct = default);
    Task UpdateAsync(AccessPolicy policy, CancellationToken ct = default);
    Task DeleteAsync(AccessPolicy policy, CancellationToken ct = default);
}
