using Authly.Core.Entities;
using Authly.Core.Enums;

namespace Authly.Core.Interfaces;

/// <summary>Persistence for enrolled MFA factors. Tenant-scoped. Implemented in Infrastructure.</summary>
public interface IMfaFactorRepository
{
    Task<MfaFactor?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<MfaFactor>> ListByUserAsync(Guid tenantId, Guid userId, CancellationToken ct = default);

    /// <summary>Active factors only (those usable to satisfy a login challenge).</summary>
    Task<IReadOnlyList<MfaFactor>> ListActiveByUserAsync(Guid tenantId, Guid userId, CancellationToken ct = default);

    /// <summary>The single active factor of a given type, if any.</summary>
    Task<MfaFactor?> GetActiveByTypeAsync(Guid tenantId, Guid userId, MfaFactorType type, CancellationToken ct = default);

    /// <summary>All active factors of a given type (a user may enrol several passkeys).</summary>
    Task<IReadOnlyList<MfaFactor>> ListActiveByTypeAsync(Guid tenantId, Guid userId, MfaFactorType type, CancellationToken ct = default);

    /// <summary>True if the user has at least one active factor.</summary>
    Task<bool> AnyActiveAsync(Guid tenantId, Guid userId, CancellationToken ct = default);

    Task AddAsync(MfaFactor factor, CancellationToken ct = default);
    Task UpdateAsync(MfaFactor factor, CancellationToken ct = default);
}
