using Authly.Modules.Auth;

namespace Authly.Web.Infrastructure;

/// <summary>
/// Builds the absolute links embedded in verification / reset emails using the current
/// request's scheme + host. The tenant is carried implicitly by the host: a custom domain
/// in production, or the dev tenant cookie set by <see cref="TenantResolutionMiddleware"/>
/// when the link is opened in the same browser. Links are token-only.
/// </summary>
public sealed class AuthUrlBuilder : IAuthUrlBuilder
{
    private readonly IHttpContextAccessor _http;

    public AuthUrlBuilder(IHttpContextAccessor http) => _http = http;

    public string BuildEmailVerificationUrl(Guid tenantId, string rawToken)
        => Absolute("/account/verify-email", rawToken);

    public string BuildPasswordResetUrl(Guid tenantId, string rawToken)
        => Absolute("/account/reset-password", rawToken);

    public string BuildMagicLinkUrl(Guid tenantId, string rawToken)
        => Absolute("/account/magic", rawToken);

    public string BuildContactChangeVerifyUrl(Guid tenantId, string rawToken)
        => Absolute("/account/change/verify", rawToken);

    public string BuildContactChangeCancelUrl(Guid tenantId, string rawToken)
        => Absolute("/account/change/cancel", rawToken);

    public string BuildRecoveryUrl(Guid tenantId, string rawToken)
        => Absolute("/account/recover", rawToken);

    private string Absolute(string path, string rawToken)
    {
        var request = _http.HttpContext?.Request;
        var origin = request is not null
            ? $"{request.Scheme}://{request.Host}"
            : "https://localhost"; // fallback when building outside a request (should not happen in Phase 2)
        return $"{origin}{path}?token={Uri.EscapeDataString(rawToken)}";
    }
}
