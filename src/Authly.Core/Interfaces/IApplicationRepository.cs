using Authly.Core.Entities;

namespace Authly.Core.Interfaces;

/// <summary>
/// Persistence for the tenant-facing <see cref="Application"/> mirror and its
/// <see cref="ApplicationSecret"/>s. The OAuth protocol registration itself lives in
/// OpenIddict's store; this keeps the tenant-owned metadata. Implemented in Infrastructure.
/// </summary>
public interface IApplicationRepository
{
    Task<Application?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);

    /// <summary>Cross-tenant lookup by the globally-unique client id (token-endpoint context has no tenant).</summary>
    Task<Application?> GetByClientIdAsync(string clientId, CancellationToken ct = default);

    Task<IReadOnlyList<Application>> ListByTenantAsync(Guid tenantId, CancellationToken ct = default);

    /// <summary>
    /// Every registered redirect URI across all tenants. Used to derive the browser CORS web-origin
    /// allowlist for SPA clients — the <c>applications</c> table is not RLS-scoped, so this is a
    /// deliberate cross-tenant read. Returns raw URIs; the caller reduces them to origins.
    /// </summary>
    Task<IReadOnlyList<string>> ListAllRedirectUrisAsync(CancellationToken ct = default);

    Task AddAsync(Application application, CancellationToken ct = default);
    Task UpdateAsync(Application application, CancellationToken ct = default);
    Task DeleteAsync(Application application, CancellationToken ct = default);

    Task<IReadOnlyList<ApplicationSecret>> ListSecretsAsync(Guid applicationId, CancellationToken ct = default);
    Task AddSecretAsync(ApplicationSecret secret, CancellationToken ct = default);

    /// <summary>Marks every existing secret for the application revoked (single-active-secret rotation).</summary>
    Task RevokeSecretsAsync(Guid applicationId, CancellationToken ct = default);
}
