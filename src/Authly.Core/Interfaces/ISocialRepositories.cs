using Authly.Core.Entities;

namespace Authly.Core.Interfaces;

/// <summary>Persistence for user↔provider links. Tenant-scoped.</summary>
public interface ISocialIdentityRepository
{
    /// <summary>The identity for a provider account within a tenant, if linked.</summary>
    Task<SocialIdentity?> GetAsync(Guid tenantId, string provider, string providerId, CancellationToken ct = default);

    Task<IReadOnlyList<SocialIdentity>> ListByUserAsync(Guid tenantId, Guid userId, CancellationToken ct = default);

    Task AddAsync(SocialIdentity identity, CancellationToken ct = default);
    Task UpdateAsync(SocialIdentity identity, CancellationToken ct = default);
}

/// <summary>Persistence for per-tenant social-provider configuration. Tenant-scoped.</summary>
public interface ISocialProviderRepository
{
    Task<IReadOnlyList<SocialProvider>> ListByTenantAsync(Guid tenantId, CancellationToken ct = default);

    /// <summary>The tenant's config for a provider key, if present.</summary>
    Task<SocialProvider?> GetAsync(Guid tenantId, string provider, CancellationToken ct = default);

    Task<SocialProvider?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task AddAsync(SocialProvider provider, CancellationToken ct = default);
    Task UpdateAsync(SocialProvider provider, CancellationToken ct = default);
    Task DeleteAsync(SocialProvider provider, CancellationToken ct = default);
}
