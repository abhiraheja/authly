using Authly.Core.Branding;
using Authly.Modules.Common;

namespace Authly.Modules.Branding;

/// <summary>
/// Reads and updates a tenant's hosted-page branding and custom domain. Tenant-scoped — every
/// call takes the resolved tenant id.
/// </summary>
public interface IBrandingService
{
    /// <summary>Current branding, parsed from <c>tenants.branding</c> (platform default if unset).</summary>
    Task<TenantBranding> GetAsync(Guid tenantId, CancellationToken ct = default);

    /// <summary>The tenant's configured custom auth domain (e.g. <c>auth.acme.com</c>), or null.</summary>
    Task<string?> GetCustomDomainAsync(Guid tenantId, CancellationToken ct = default);

    /// <summary>Validates and persists branding. Throws <see cref="BrandingConfigInvalidException"/> on bad input.</summary>
    Task SaveAsync(Guid tenantId, BrandingInput input, AuditContext actor, CancellationToken ct = default);

    /// <summary>
    /// Sets (or clears with null/blank) the tenant's custom domain. Validates the host shape and
    /// rejects a domain already claimed by another tenant. Throws <see cref="BrandingConfigInvalidException"/>.
    /// </summary>
    Task SetCustomDomainAsync(Guid tenantId, string? domain, AuditContext actor, CancellationToken ct = default);
}
