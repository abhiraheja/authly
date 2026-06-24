using System.ComponentModel.DataAnnotations;

namespace Authly.Web.Areas.TenantAdmin.Models;

/// <summary>Tenant-admin form for the workspace security policy (Phase 12).</summary>
public sealed class SecuritySettingsViewModel
{
    [Display(Name = "Allow self-service password sign-up")]
    public bool AllowPasswordSignup { get; set; } = true;

    [Display(Name = "Allow new account creation via social login")]
    public bool AllowSocialSignup { get; set; } = true;

    [Display(Name = "Allow sign-up with a mobile number (WhatsApp OTP)")]
    public bool AllowPhoneSignup { get; set; }

    [Display(Name = "Allow sign-in with a mobile number (WhatsApp OTP or password)")]
    public bool AllowPhoneLogin { get; set; }

    /// <summary>Read-only: whether WhatsApp + a linked OTP template are ready. The phone toggles are
    /// only effective (and only enabled in the UI) when this is true.</summary>
    public bool WhatsAppOtpReady { get; set; }

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

    // --- Conditional / risk-based access (Phase 2) ---
    [Display(Name = "Enable conditional access")]
    public bool ConditionalAccessEnabled { get; set; }

    [Display(Name = "When signing in from a new device or location")]
    public Authly.Modules.Security.ConditionalAction NewDeviceAction { get; set; }
        = Authly.Modules.Security.ConditionalAction.Allow;

    [Display(Name = "When the user's email is unverified")]
    public Authly.Modules.Security.ConditionalAction UnverifiedEmailAction { get; set; }
        = Authly.Modules.Security.ConditionalAction.Allow;
}
