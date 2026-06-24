using Authly.Modules.Messaging;
using Authly.Modules.Social;

namespace Authly.Modules.Security;

/// <summary>Which hosted sign-in methods are actually offerable for a tenant: a method is effective
/// only when its toggle is on AND any prerequisite is met (social needs an active provider; phone
/// needs WhatsApp + a linked OTP template). Password / magic-link / passkey have no prerequisite.</summary>
public sealed record EnabledSignInMethods(bool Password, bool MagicLink, bool Passkey, bool Social, bool Phone)
{
    public int EffectiveCount =>
        (Password ? 1 : 0) + (MagicLink ? 1 : 0) + (Passkey ? 1 : 0) + (Social ? 1 : 0) + (Phone ? 1 : 0);

    /// <summary>True when at least one method actually works (used to block a lock-out config).</summary>
    public bool Any => EffectiveCount > 0;
}

/// <summary>Single source of truth for sign-in method availability — feeds both the save-time
/// "at least one effective method" guard and the login-page rendering / endpoint guards.</summary>
public interface IAuthMethodPolicy
{
    /// <summary>Effective methods for the tenant's currently-saved settings.</summary>
    Task<EnabledSignInMethods> GetEffectiveAsync(Guid tenantId, CancellationToken ct = default);

    /// <summary>Effective methods for a candidate (not-yet-saved) settings instance, using the
    /// tenant's current provider readiness. Used by the save-time guard.</summary>
    Task<EnabledSignInMethods> GetEffectiveAsync(Guid tenantId, TenantSecuritySettings settings, CancellationToken ct = default);
}

/// <inheritdoc />
public sealed class AuthMethodPolicy : IAuthMethodPolicy
{
    private readonly ISecuritySettingsService _settings;
    private readonly IMessagingService _messaging;
    private readonly ISocialLoginService _social;

    public AuthMethodPolicy(ISecuritySettingsService settings, IMessagingService messaging, ISocialLoginService social)
    {
        _settings = settings;
        _messaging = messaging;
        _social = social;
    }

    public async Task<EnabledSignInMethods> GetEffectiveAsync(Guid tenantId, CancellationToken ct = default)
        => await GetEffectiveAsync(tenantId, await _settings.GetAsync(tenantId, ct), ct);

    public async Task<EnabledSignInMethods> GetEffectiveAsync(Guid tenantId, TenantSecuritySettings settings, CancellationToken ct = default)
    {
        var whatsAppReady = await _messaging.IsWhatsAppOtpReadyAsync(tenantId, ct);
        var hasSocial = (await _social.ListActiveOptionsAsync(tenantId, ct)).Count > 0;
        return new EnabledSignInMethods(
            Password: settings.AllowPasswordLogin,
            MagicLink: settings.AllowMagicLinkLogin,
            Passkey: settings.AllowPasskeyLogin,
            Social: settings.AllowSocialLogin && hasSocial,
            Phone: settings.AllowPhoneLogin && whatsAppReady);
    }
}
