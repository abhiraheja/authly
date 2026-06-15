namespace Authly.Modules.Security;

/// <summary>
/// Per-tenant security policy, persisted under the <c>"security"</c> node of <c>tenants.settings</c>.
/// Secrets (the CAPTCHA secret) are stored AES-encrypted in this JSON, never in plaintext.
/// </summary>
public sealed class TenantSecuritySettings
{
    // --- Account lockout ---
    public bool LockoutEnabled { get; set; } = true;
    /// <summary>Consecutive failures before the account is locked.</summary>
    public int LockoutThreshold { get; set; } = 5;

    // --- Breached-password check ---
    public bool BreachedPasswordCheck { get; set; } = true;

    // --- Bot / CAPTCHA ---
    public bool CaptchaEnabled { get; set; }
    /// <summary>"hcaptcha" or "turnstile".</summary>
    public string? CaptchaProvider { get; set; }
    public string? CaptchaSiteKey { get; set; }
    /// <summary>AES-encrypted CAPTCHA secret (write-only in the UI).</summary>
    public string? CaptchaSecretEncrypted { get; set; }

    // --- Block / allow lists ---
    public List<string> BlockedEmailDomains { get; set; } = new();
    public bool BlockDisposableEmails { get; set; }
    /// <summary>Blocked IPs or CIDR ranges (e.g. "203.0.113.0/24").</summary>
    public List<string> BlockedIps { get; set; } = new();
    /// <summary>Blocked ISO-3166 alpha-2 country codes (requires a geo source to enforce on country).</summary>
    public List<string> BlockedCountries { get; set; } = new();
    /// <summary>Per-org IP allowlist; when non-empty, only these IPs/CIDRs may authenticate.</summary>
    public List<string> AllowedIps { get; set; } = new();

    // --- Conditional / risk-based access (Phase 2) ---
    /// <summary>Master switch for the conditional-access rules below.</summary>
    public bool ConditionalAccessEnabled { get; set; }
    /// <summary>Action when a sign-in comes from a new device + location (unseen IP and user-agent).</summary>
    public ConditionalAction NewDeviceAction { get; set; } = ConditionalAction.Allow;
    /// <summary>Action when the signing-in user's email is not yet verified.</summary>
    public ConditionalAction UnverifiedEmailAction { get; set; } = ConditionalAction.Allow;

    public bool HasCaptcha => CaptchaEnabled
        && !string.IsNullOrWhiteSpace(CaptchaProvider)
        && !string.IsNullOrWhiteSpace(CaptchaSecretEncrypted);
}

/// <summary>Outcome of screening a registration attempt against the tenant's security policy.</summary>
public sealed class ScreeningResult
{
    public bool CaptchaFailed { get; set; }
    public bool EmailBlocked { get; set; }
    public bool PasswordBreached { get; set; }
    public bool IpBlocked { get; set; }

    public bool Passed => !CaptchaFailed && !EmailBlocked && !PasswordBreached && !IpBlocked;
}

/// <summary>Exponential-backoff lockout schedule (pure).</summary>
public static class LockoutPolicy
{
    private static readonly TimeSpan Base = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan Cap = TimeSpan.FromHours(1);

    /// <summary>
    /// Lockout duration once the failure count is at/over the threshold: base doubles for each
    /// failure beyond the threshold, capped. Returns <see cref="TimeSpan.Zero"/> below the threshold.
    /// </summary>
    public static TimeSpan DurationFor(int failures, int threshold)
    {
        if (failures < threshold) return TimeSpan.Zero;
        var over = failures - threshold;                 // 0 at threshold
        var minutes = Base.TotalMinutes * Math.Pow(2, Math.Min(over, 12));
        var span = TimeSpan.FromMinutes(minutes);
        return span > Cap ? Cap : span;
    }
}
