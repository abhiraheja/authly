using System.ComponentModel.DataAnnotations;
using Authly.Modules.Mfa;

namespace Authly.Web.Models;

/// <summary>The MFA login gate: which methods are offered and where to post the code.</summary>
public sealed class MfaChallengeViewModel
{
    public MfaAvailableMethods Methods { get; set; } = new(false, false, false);

    /// <summary>Set after an email OTP is dispatched so the UI can show the email entry field.</summary>
    public bool EmailOtpSent { get; set; }

    public string? Error { get; set; }

    [Display(Name = "Code")]
    public string? Code { get; set; }
}

/// <summary>Forced TOTP enrolment shown when policy requires MFA and the user has none.</summary>
public sealed class MfaEnrollViewModel
{
    public Guid FactorId { get; set; }
    public string Secret { get; set; } = string.Empty;
    public string QrSvg { get; set; } = string.Empty;

    [Required, Display(Name = "6-digit code")]
    public string Code { get; set; } = string.Empty;

    public string? Error { get; set; }
}

/// <summary>Self-service security overview (the end-user portal).</summary>
public sealed class SecurityOverviewViewModel
{
    public IReadOnlyList<Authly.Core.Entities.MfaFactor> Factors { get; set; } = Array.Empty<Authly.Core.Entities.MfaFactor>();
    public int UnusedBackupCodes { get; set; }
    public TenantMfaSettings Policy { get; set; } = new();
    public bool HasTotp { get; set; }
    public bool HasEmailOtp { get; set; }

    /// <summary>Shown exactly once right after (re)generation.</summary>
    public IReadOnlyList<string>? NewBackupCodes { get; set; }
}

/// <summary>TOTP setup screen in the self-service portal.</summary>
public sealed class SecurityTotpSetupViewModel
{
    public Guid FactorId { get; set; }
    public string Secret { get; set; } = string.Empty;
    public string QrSvg { get; set; } = string.Empty;

    [Required, Display(Name = "6-digit code")]
    public string Code { get; set; } = string.Empty;

    [Display(Name = "Device name")]
    public string? FriendlyName { get; set; }

    public string? Error { get; set; }
}

/// <summary>Tenant-admin MFA policy form.</summary>
public sealed class MfaPolicyViewModel
{
    [Display(Name = "Policy")]
    public MfaPolicy Policy { get; set; } = MfaPolicy.Optional;

    [Display(Name = "Allow authenticator apps (TOTP)")]
    public bool AllowTotp { get; set; } = true;

    [Display(Name = "Allow email one-time codes")]
    public bool AllowEmailOtp { get; set; } = true;
}
