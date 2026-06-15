using Authly.Core.Interfaces;
using Authly.Modules.Security;
using Authly.Web.Areas.TenantAdmin.Models;
using Microsoft.AspNetCore.Mvc;

namespace Authly.Web.Areas.TenantAdmin.Controllers;

/// <summary>Tenant-admin configuration of the workspace security policy (Phase 12).</summary>
[Route("tenantadmin/security")]
public sealed class SecurityController : TenantAdminControllerBase
{
    private readonly ISecuritySettingsService _settings;

    public SecurityController(ISecuritySettingsService settings, ITenantContext tenant) : base(tenant)
        => _settings = settings;

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewData["Title"] = "Security";
        var s = await _settings.GetAsync(TenantId, ct);
        return View(new SecuritySettingsViewModel
        {
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

    [HttpPost("")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(SecuritySettingsViewModel model, CancellationToken ct)
    {
        ViewData["Title"] = "Security";
        if (!ModelState.IsValid) return View(model);

        await _settings.SaveAsync(TenantId, new TenantSecuritySettings
        {
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
        }, model.CaptchaSecret, CurrentAudit(), ct);

        TempData["Success"] = "Security settings saved.";
        return RedirectToAction(nameof(Index));
    }

    private static string Join(IEnumerable<string> items) => string.Join("\n", items);

    private static List<string> Split(string? text) => string.IsNullOrWhiteSpace(text)
        ? new List<string>()
        : text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
}
