using Authly.Core.Interfaces;
using Authly.Modules.Auth;

namespace Authly.Web.Infrastructure;

/// <summary>
/// Builds the absolute links embedded in verification / reset emails using the current
/// request's scheme + host, plus the tenant slug as a <c>&amp;tenant=</c> query param.
///
/// The host alone can't identify the tenant on the shared platform host (e.g.
/// <c>authly.saarvix.in</c> serves every tenant), and the tenant-hint cookie set by
/// <see cref="TenantResolutionMiddleware"/> only exists in the browser that requested the link.
/// Carrying the slug in the URL lets <see cref="TenantResolutionMiddleware"/> resolve the tenant
/// when the link is opened in a different browser (or the real email recipient's browser). A
/// per-tenant custom domain still resolves via the host first, so the extra param is harmless there.
/// </summary>
public sealed class AuthUrlBuilder : IAuthUrlBuilder
{
    private readonly IHttpContextAccessor _http;
    private readonly ITenantRepository _tenants;

    public AuthUrlBuilder(IHttpContextAccessor http, ITenantRepository tenants)
    {
        _http = http;
        _tenants = tenants;
    }

    public Task<string> BuildEmailVerificationUrl(Guid tenantId, string rawToken)
        => AbsoluteAsync("/account/verify-email", rawToken, tenantId);

    public Task<string> BuildPasswordResetUrl(Guid tenantId, string rawToken)
        => AbsoluteAsync("/account/reset-password", rawToken, tenantId);

    public async Task<string> BuildMagicLinkUrl(Guid tenantId, string rawToken, string? returnUrl = null)
    {
        var url = await AbsoluteAsync("/account/magic", rawToken, tenantId);
        if (!string.IsNullOrEmpty(returnUrl))
            url += $"&returnUrl={Uri.EscapeDataString(returnUrl)}";
        return url;
    }

    public Task<string> BuildContactChangeVerifyUrl(Guid tenantId, string rawToken)
        => AbsoluteAsync("/account/change/verify", rawToken, tenantId);

    public Task<string> BuildContactChangeCancelUrl(Guid tenantId, string rawToken)
        => AbsoluteAsync("/account/change/cancel", rawToken, tenantId);

    public Task<string> BuildRecoveryUrl(Guid tenantId, string rawToken)
        => AbsoluteAsync("/account/recover", rawToken, tenantId);

    public string BuildInviteAcceptUrl(string rawToken)
        => Absolute("/invite/accept", rawToken); // tenant-less path — no tenant hint needed

    private async Task<string> AbsoluteAsync(string path, string rawToken, Guid tenantId)
    {
        var url = Absolute(path, rawToken);
        // Append the tenant slug so the link resolves on the shared host in any browser. If the
        // tenant can't be found (deleted mid-flight), fall back to the token-only link rather than
        // failing the whole send — host/cookie resolution may still cover it.
        var tenant = await _tenants.GetByIdAsync(tenantId, _http.HttpContext?.RequestAborted ?? default);
        return tenant is null ? url : $"{url}&tenant={Uri.EscapeDataString(tenant.Slug)}";
    }

    private string Absolute(string path, string rawToken)
    {
        var request = _http.HttpContext?.Request;
        var origin = request is not null
            ? $"{request.Scheme}://{request.Host}"
            : "https://localhost"; // fallback when building outside a request (should not happen in Phase 2)
        return $"{origin}{path}?token={Uri.EscapeDataString(rawToken)}";
    }
}
