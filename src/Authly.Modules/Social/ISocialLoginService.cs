using Authly.Core.Entities;
using Authly.Modules.Common;

namespace Authly.Modules.Social;

/// <summary>
/// Social / external login: builds provider authorization URLs, completes the callback
/// (token exchange → JIT user creation or account linking → session), and the tenant-admin
/// configuration surface. Tenant-scoped; the caller binds the tenant context.
/// </summary>
public interface ISocialLoginService
{
    /// <summary>The active providers for a tenant, for rendering login buttons.</summary>
    Task<IReadOnlyList<SocialLoginOption>> ListActiveOptionsAsync(Guid tenantId, CancellationToken ct = default);

    /// <summary>Builds the provider's authorization URL to redirect the user to.</summary>
    Task<string> BuildAuthorizationUrlAsync(Guid tenantId, string provider, string redirectUri, string state, CancellationToken ct = default);

    /// <summary>
    /// Completes the OAuth callback: exchanges the code, resolves the user (link by verified email
    /// or JIT-create), stores the encrypted tokens, and starts a session.
    /// </summary>
    Task<SocialLoginResult> CompleteLoginAsync(Guid tenantId, string provider, string code, string redirectUri, RequestInfo info, CancellationToken ct = default);

    // --- Admin config ---
    Task<IReadOnlyList<SocialProvider>> ListProvidersAsync(Guid tenantId, CancellationToken ct = default);
    Task<SocialProvider?> GetProviderAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task SaveProviderAsync(Guid tenantId, SocialProviderInput input, AuditContext actor, CancellationToken ct = default);
    Task DeleteProviderAsync(Guid tenantId, Guid id, AuditContext actor, CancellationToken ct = default);
}
