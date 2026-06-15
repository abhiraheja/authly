using Authly.Core.Entities;

namespace Authly.Core.Interfaces;

/// <summary>
/// Persistence for <see cref="SelfHostedInstance"/>. Platform-level (cloud control plane); NOT
/// tenant-scoped. Implemented in Infrastructure.
/// </summary>
public interface ISelfHostedInstanceRepository
{
    Task<SelfHostedInstance?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Resolves an instance by the SHA-256 hash of its sync key (constant lookup; the hash is unique).</summary>
    Task<SelfHostedInstance?> GetBySyncKeyHashAsync(string syncKeyHash, CancellationToken ct = default);

    Task<IReadOnlyList<SelfHostedInstance>> ListAsync(CancellationToken ct = default);
    Task AddAsync(SelfHostedInstance instance, CancellationToken ct = default);
    Task UpdateAsync(SelfHostedInstance instance, CancellationToken ct = default);
    Task DeleteAsync(SelfHostedInstance instance, CancellationToken ct = default);
}

/// <summary>Persistence for <see cref="ConsentRecord"/>. Tenant-scoped. Implemented in Infrastructure.</summary>
public interface IConsentRecordRepository
{
    Task<IReadOnlyList<ConsentRecord>> ListByUserAsync(Guid tenantId, Guid userId, CancellationToken ct = default);
    Task AddAsync(ConsentRecord record, CancellationToken ct = default);
}
