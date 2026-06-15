using System.ComponentModel.DataAnnotations;

namespace Authly.Web.Areas.TenantAdmin.Models;

/// <summary>Tenant-admin form for the workspace security policy (Phase 12).</summary>
public sealed class SecuritySettingsViewModel
{
    [Display(Name = "Lock accounts after repeated failures")]
    public bool LockoutEnabled { get; set; } = true;

    [Display(Name = "Failures before lockout"), Range(3, 20)]
    public int LockoutThreshold { get; set; } = 5;

    [Display(Name = "Reject breached passwords (HaveIBeenPwned)")]
    public bool BreachedPasswordCheck { get; set; } = true;

    [Display(Name = "Require CAPTCHA on sign-in / sign-up")]
    public bool CaptchaEnabled { get; set; }

    [Display(Name = "CAPTCHA provider")]
    public string? CaptchaProvider { get; set; } = "hcaptcha";

    [Display(Name = "CAPTCHA site key")]
    public string? CaptchaSiteKey { get; set; }

    [Display(Name = "CAPTCHA secret")]
    public string? CaptchaSecret { get; set; }

    public bool HasCaptchaSecret { get; set; }

    [Display(Name = "Block disposable email domains")]
    public bool BlockDisposableEmails { get; set; }

    [Display(Name = "Blocked email domains (one per line)")]
    public string? BlockedEmailDomains { get; set; }

    [Display(Name = "Blocked IPs / CIDR ranges (one per line)")]
    public string? BlockedIps { get; set; }

    [Display(Name = "Blocked countries (ISO codes, one per line)")]
    public string? BlockedCountries { get; set; }

    [Display(Name = "IP allowlist (one per line; empty = allow all)")]
    public string? AllowedIps { get; set; }
}
