using Authly.Core.Interfaces;
using Authly.Modules.Messaging;
using Authly.Modules.Security;
using Authly.Modules.Social;
using Authly.Web.Areas.TenantAdmin.Models;
using Authly.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace Authly.Web.Areas.TenantAdmin.Controllers;

/// <summary>Tenant-admin configuration of the workspace security policy (Phase 12).</summary>
[Route("tenantadmin/security")]
public sealed class SecurityController : TenantAdminControllerBase
{
    private readonly ISecuritySettingsService _settings;
    private readonly IMessagingService _messaging;
    private readonly ISocialLoginService _social;
    private readonly IAuthMethodPolicy _authMethods;

    public SecurityController(ISecuritySettingsService settings, IMessagingService messaging,
        ISocialLoginService social, IAuthMethodPolicy authMethods, ITenantContext tenant) : base(tenant)
    {
        _settings = settings;
        _messaging = messaging;
        _social = social;
        _authMethods = authMethods;
    }

    [RequireOperatorPermission("project.read")]
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewData["Title"] = "Security";
        var s = await _settings.GetAsync(TenantId, ct);
        var whatsAppReady = await _messaging.IsWhatsAppOtpReadyAsync(TenantId, ct);
        var hasSocial = (await _social.ListActiveOptionsAsync(TenantId, ct)).Count > 0;
        return View(new SecuritySettingsViewModel
        {
            AllowPasswordSignup = s.AllowPasswordSignup,
            AllowSocialSignup = s.AllowSocialSignup,
            AllowPhoneSignup = s.AllowPhoneSignup,
            AllowPhoneLogin = s.AllowPhoneLogin,
            WhatsAppOtpReady = whatsAppReady,
            AllowPasswordLogin = s.AllowPasswordLogin,
            AllowMagicLinkLogin = s.AllowMagicLinkLogin,
            AllowPasskeyLogin = s.AllowPasskeyLogin,
            AllowSocialLogin = s.AllowSocialLogin,
            HasSocialProvider = hasSocial,
            LockoutEnabled = s.LockoutEnabled,
            LockoutThreshold = s.LockoutThreshold,
            BreachedPasswordCheck = s.BreachedPasswordCheck,
            CaptchaEnabled = s.CaptchaEnabled,
            CaptchaProvider = s.CaptchaProvider ?? "hcaptcha",
            CaptchaSiteKey = s.CaptchaSiteKey,
            HasCaptchaSecret = !string.IsNullOrWhiteSpace(s.CaptchaSecretEncrypted),
            BlockDisposableEmails = s.BlockDisposableEmails,
            BlockedEmailDomains = Join(s.BlockedEmailDomains),
            BlockedIps = Join(s.BlockedIps),
            BlockedCountries = Join(s.BlockedCountries),
            AllowedIps = Join(s.AllowedIps),
            ConditionalAccessEnabled = s.ConditionalAccessEnabled,
            NewDeviceAction = s.NewDeviceAction,
            UnverifiedEmailAction = s.UnverifiedEmailAction
        });
    }

    [RequireOperatorPermission("project.write")]
    [HttpPost("")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(SecuritySettingsViewModel model, CancellationToken ct)
    {
        ViewData["Title"] = "Security";
        var whatsAppReady = await _messaging.IsWhatsAppOtpReadyAsync(TenantId, ct);
        var hasSocial = (await _social.ListActiveOptionsAsync(TenantId, ct)).Count > 0;

        IActionResult Rerender()
        {
            model.WhatsAppOtpReady = whatsAppReady;
            model.HasSocialProvider = hasSocial;
            return View(model);
        }

        if (!ModelState.IsValid) return Rerender();

        var settings = new TenantSecuritySettings
        {
            AllowPasswordSignup = model.AllowPasswordSignup,
            AllowSocialSignup = model.AllowSocialSignup,
            // Phone auth can only be enabled when WhatsApp + the OTP template are ready (server guard).
            AllowPhoneSignup = whatsAppReady && model.AllowPhoneSignup,
            AllowPhoneLogin = whatsAppReady && model.AllowPhoneLogin,
            // Sign-in methods. Social can only be enabled when a provider is active (server guard);
            // password/magic/passkey have no prerequisite.
            AllowPasswordLogin = model.AllowPasswordLogin,
            AllowMagicLinkLogin = model.AllowMagicLinkLogin,
            AllowPasskeyLogin = model.AllowPasskeyLogin,
            AllowSocialLogin = hasSocial && model.AllowSocialLogin,
            LockoutEnabled = model.LockoutEnabled,
            LockoutThreshold = model.LockoutThreshold,
            BreachedPasswordCheck = model.BreachedPasswordCheck,
            CaptchaEnabled = model.CaptchaEnabled,
            CaptchaProvider = model.CaptchaProvider,
            CaptchaSiteKey = model.CaptchaSiteKey?.Trim(),
            BlockDisposableEmails = model.BlockDisposableEmails,
            BlockedEmailDomains = Split(model.BlockedEmailDomains),
            BlockedIps = Split(model.BlockedIps),
            BlockedCountries = Split(model.BlockedCountries),
            AllowedIps = Split(model.AllowedIps),
            ConditionalAccessEnabled = model.ConditionalAccessEnabled,
            NewDeviceAction = model.NewDeviceAction,
            UnverifiedEmailAction = model.UnverifiedEmailAction
        };

        // At least one sign-in method must actually work, or the tenant locks everyone out.
        var effective = await _authMethods.GetEffectiveAsync(TenantId, settings, ct);
        if (!effective.Any)
        {
            ModelState.AddModelError(nameof(model.AllowPasswordLogin),
                "At least one sign-in method must remain enabled.");
            return Rerender();
        }

        await _settings.SaveAsync(TenantId, settings, model.CaptchaSecret, CurrentAudit(), ct);

        TempData["Success"] = "Security settings saved.";
        return RedirectToAction(nameof(Index));
    }

    private static string Join(IEnumerable<string> items) => string.Join("\n", items);

    private static List<string> Split(string? text) => string.IsNullOrWhiteSpace(text)
        ? new List<string>()
        : text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
}
