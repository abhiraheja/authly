using Authly.Core.Security;

namespace Authly.Modules.Security;

/// <summary>
/// Screens sign-up / sign-in attempts against the tenant's bot, breached-password, and block-list
/// policy. Used by the auth controllers before creating accounts or sessions.
/// </summary>
public interface ISecurityScreeningService
{
    /// <summary>Full registration screen: CAPTCHA + email block list + IP + breached password.</summary>
    Task<ScreeningResult> ScreenRegistrationAsync(Guid tenantId, string email, string password,
        string? captchaToken, string? remoteIp, CancellationToken ct = default);

    /// <summary>True if CAPTCHA is satisfied (or not enabled for the tenant).</summary>
    Task<bool> VerifyCaptchaAsync(Guid tenantId, string? captchaToken, string? remoteIp, CancellationToken ct = default);

    /// <summary>True if the password appears in a known breach (false when the check is disabled).</summary>
    Task<bool> IsPasswordBreachedAsync(Guid tenantId, string password, CancellationToken ct = default);
}

/// <inheritdoc />
public sealed class SecurityScreeningService : ISecurityScreeningService
{
    private readonly ISecuritySettingsService _settings;
    private readonly IBlockListService _blockList;
    private readonly ICaptchaGateway _captcha;
    private readonly IBreachedPasswordGateway _breached;

    public SecurityScreeningService(ISecuritySettingsService settings, IBlockListService blockList,
        ICaptchaGateway captcha, IBreachedPasswordGateway breached)
    {
        _settings = settings;
        _blockList = blockList;
        _captcha = captcha;
        _breached = breached;
    }

    public async Task<ScreeningResult> ScreenRegistrationAsync(Guid tenantId, string email, string password,
        string? captchaToken, string? remoteIp, CancellationToken ct = default)
    {
        var settings = await _settings.GetAsync(tenantId, ct);
        var result = new ScreeningResult
        {
            CaptchaFailed = !await VerifyWithSettingsAsync(settings, captchaToken, remoteIp, ct),
            EmailBlocked = BlockListPolicy.IsEmailBlocked(email, settings.BlockedEmailDomains, settings.BlockDisposableEmails),
            IpBlocked = BlockListPolicy.IsIpBlocked(remoteIp, settings.BlockedIps)
                        || !BlockListPolicy.IsIpAllowed(remoteIp, settings.AllowedIps)
        };
        if (settings.BreachedPasswordCheck)
            result.PasswordBreached = await _breached.IsBreachedAsync(password, ct);
        return result;
    }

    public async Task<bool> VerifyCaptchaAsync(Guid tenantId, string? captchaToken, string? remoteIp, CancellationToken ct = default)
        => await VerifyWithSettingsAsync(await _settings.GetAsync(tenantId, ct), captchaToken, remoteIp, ct);

    public async Task<bool> IsPasswordBreachedAsync(Guid tenantId, string password, CancellationToken ct = default)
    {
        var settings = await _settings.GetAsync(tenantId, ct);
        return settings.BreachedPasswordCheck && await _breached.IsBreachedAsync(password, ct);
    }

    private async Task<bool> VerifyWithSettingsAsync(TenantSecuritySettings settings, string? token, string? remoteIp, CancellationToken ct)
    {
        if (!settings.HasCaptcha) return true; // not enforced for this tenant
        var secret = _settings.DecryptCaptchaSecret(settings);
        if (string.IsNullOrEmpty(secret) || string.IsNullOrEmpty(token)) return false;
        return await _captcha.VerifyAsync(settings.CaptchaProvider!, secret, token, remoteIp, ct);
    }
}
