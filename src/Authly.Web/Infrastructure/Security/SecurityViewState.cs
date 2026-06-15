using Authly.Modules.Security;

namespace Authly.Web.Infrastructure.Security;

/// <summary>The CAPTCHA widget details a view needs to render the challenge (never the secret).</summary>
public sealed record CaptchaWidget(string Provider, string SiteKey);

/// <summary>
/// Surfaces the bits of a tenant's security policy the auth views need (the CAPTCHA widget). Keeps
/// controllers from poking at settings shapes directly.
/// </summary>
public sealed class SecurityViewState
{
    private readonly ISecuritySettingsService _settings;

    public SecurityViewState(ISecuritySettingsService settings) => _settings = settings;

    /// <summary>The CAPTCHA widget to render for the tenant, or null when CAPTCHA isn't enabled/configured.</summary>
    public async Task<CaptchaWidget?> GetCaptchaAsync(Guid tenantId, CancellationToken ct = default)
    {
        var s = await _settings.GetAsync(tenantId, ct);
        return s.HasCaptcha && !string.IsNullOrWhiteSpace(s.CaptchaSiteKey)
            ? new CaptchaWidget(s.CaptchaProvider!, s.CaptchaSiteKey!)
            : null;
    }
}
