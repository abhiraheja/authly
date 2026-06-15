using Authly.Core.Entities;

namespace Authly.Core.Interfaces;

/// <summary>Persistence for <see cref="RecoveryContact"/>. Tenant-scoped. Implemented in Infrastructure.</summary>
public interface IRecoveryContactRepository
{
    Task<RecoveryContact?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<RecoveryContact>> ListByUserAsync(Guid tenantId, Guid userId, CancellationToken ct = default);
    Task AddAsync(RecoveryContact contact, CancellationToken ct = default);
    Task UpdateAsync(RecoveryContact contact, CancellationToken ct = default);
    Task DeleteAsync(RecoveryContact contact, CancellationToken ct = default);
}

/// <summary>Persistence for <see cref="PendingContactChange"/>. Tenant-scoped. Implemented in Infrastructure.</summary>
public interface IPendingContactChangeRepository
{
    /// <summary>The user's outstanding (pending) change, if any.</summary>
    Task<PendingContactChange?> GetPendingByUserAsync(Guid tenantId, Guid userId, CancellationToken ct = default);

    /// <summary>Looks up a change by its verify-token hash (no tenant filter — the hash is unique).</summary>
    Task<PendingContactChange?> GetByVerifyHashAsync(string verifyTokenHash, CancellationToken ct = default);

    /// <summary>Looks up a change by its cancel-token hash (no tenant filter — the hash is unique).</summary>
    Task<PendingContactChange?> GetByCancelHashAsync(string cancelTokenHash, CancellationToken ct = default);

    Task AddAsync(PendingContactChange change, CancellationToken ct = default);
    Task UpdateAsync(PendingContactChange change, CancellationToken ct = default);
}
