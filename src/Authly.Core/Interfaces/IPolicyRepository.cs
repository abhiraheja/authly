using Authly.Core.Entities;

namespace Authly.Core.Interfaces;

/// <summary>
/// Persistence for the policies/consent engine: <see cref="Policy"/> + its immutable
/// <see cref="PolicyVersion"/>s, uploaded <see cref="PolicyAsset"/> documents, and per-user
/// <see cref="PolicyDecision"/>s. Tenant-scoped. Implemented in Infrastructure.
/// </summary>
public interface IPolicyRepository
{
    // --- Policies ---
    Task<IReadOnlyList<Policy>> ListByTenantAsync(Guid tenantId, CancellationToken ct = default);
    Task<IReadOnlyList<Policy>> ListPublishedAsync(Guid tenantId, CancellationToken ct = default);
    Task<Policy?> GetAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task AddAsync(Policy policy, CancellationToken ct = default);
    Task UpdateAsync(Policy policy, CancellationToken ct = default);
    Task DeleteAsync(Policy policy, CancellationToken ct = default);

    // --- Versions ---
    Task<IReadOnlyList<PolicyVersion>> ListVersionsAsync(Guid policyId, CancellationToken ct = default);
    Task<PolicyVersion?> GetVersionAsync(Guid tenantId, Guid versionId, CancellationToken ct = default);
    Task<int> NextVersionNumberAsync(Guid policyId, CancellationToken ct = default);
    Task AddVersionAsync(PolicyVersion version, CancellationToken ct = default);

    // --- Assets (PDF) ---
    Task<PolicyAsset?> GetAssetAsync(Guid id, CancellationToken ct = default);
    Task AddAssetAsync(PolicyAsset asset, CancellationToken ct = default);

    // --- Decisions ---
    Task<IReadOnlyList<PolicyDecision>> ListDecisionsForUserAsync(Guid tenantId, Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<PolicyDecision>> ListDecisionsForPolicyAsync(Guid tenantId, Guid policyId, CancellationToken ct = default);
    Task AddDecisionAsync(PolicyDecision decision, CancellationToken ct = default);
}
