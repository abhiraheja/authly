using Authly.Core.Entities;

namespace Authly.Core.Interfaces;

/// <summary>
/// Persistence for <see cref="ApiKey"/>. Lookup-by-hash is global (not tenant-scoped) because it
/// runs during authentication before any tenant is in scope — the key is found by its unique
/// high-entropy hash, then its own <c>tenant_id</c> establishes the request's tenant.
/// Implemented in Infrastructure.
/// </summary>
public interface IApiKeyRepository
{
    /// <summary>Finds a key by its hash regardless of tenant (used during authentication).</summary>
    Task<ApiKey?> GetByHashAsync(string keyHash, CancellationToken ct = default);

    Task<IReadOnlyList<ApiKey>> ListByTenantAsync(Guid tenantId, CancellationToken ct = default);
    Task<ApiKey?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task AddAsync(ApiKey key, CancellationToken ct = default);
    Task UpdateAsync(ApiKey key, CancellationToken ct = default);

    /// <summary>Best-effort touch of <c>last_used_at</c> on a successful authentication.</summary>
    Task TouchLastUsedAsync(Guid id, DateTimeOffset whenUtc, CancellationToken ct = default);
}
