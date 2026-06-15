using Authly.Core.Branding;
using Authly.Core.Interfaces;

namespace Authly.Web.Infrastructure.Branding;

/// <summary>
/// Request-scoped accessor that resolves the current tenant's branding for the view layer.
/// Layouts <c>@inject</c> this to theme the hosted login/portal pages. Loads once per request and
/// caches; falls back to the platform default when no tenant is resolved (e.g. the super-admin
/// surface or an unmatched host). Never throws — branding must not break a page render.
/// </summary>
public sealed class CurrentBranding
{
    private readonly ITenantContext _tenant;
    private readonly ITenantRepository _tenants;

    private bool _loaded;
    private TenantBranding _branding = TenantBranding.Default;
    private string? _tenantName;

    public CurrentBranding(ITenantContext tenant, ITenantRepository tenants)
    {
        _tenant = tenant;
        _tenants = tenants;
    }

    /// <summary>Resolved branding (platform default when no tenant is in scope).</summary>
    public async Task<TenantBranding> GetAsync(CancellationToken ct = default)
    {
        await EnsureLoadedAsync(ct);
        return _branding;
    }

    /// <summary>The tenant's display name, used as the brand text when no logo is set. "Authly" when tenant-less.</summary>
    public async Task<string> GetDisplayNameAsync(CancellationToken ct = default)
    {
        await EnsureLoadedAsync(ct);
        return string.IsNullOrWhiteSpace(_tenantName) ? "Authly" : _tenantName!;
    }

    private async Task EnsureLoadedAsync(CancellationToken ct)
    {
        if (_loaded) return;
        _loaded = true;

        if (!_tenant.HasTenant) return;
        var tenant = await _tenants.GetByIdAsync(_tenant.TenantId!.Value, ct);
        if (tenant is null) return;

        _tenantName = tenant.Name;
        _branding = TenantBrandingJson.Parse(tenant.Branding);
    }
}
