using System.Text.Json.Serialization;
using Authly.Core.Entities;

namespace Authly.Modules.Mfa;

/// <summary>How strictly a tenant requires multi-factor authentication.</summary>
public enum MfaPolicy
{
    /// <summary>Users may enrol MFA; it is only challenged for those who opt in.</summary>
    Optional,

    /// <summary>MFA is mandatory for tenant administrators; optional for everyone else.</summary>
    AdminsOnly,

    /// <summary>Every user must complete MFA to sign in.</summary>
    Required
}

/// <summary>
/// Per-tenant MFA configuration, persisted inside <c>tenants.settings</c> JSON under the
/// <c>mfa</c> key. Defaults (no config) = <see cref="MfaPolicy.Optional"/> with TOTP + email.
/// </summary>
public sealed class TenantMfaSettings
{
    [JsonPropertyName("policy")]
    public MfaPolicy Policy { get; set; } = MfaPolicy.Optional;

    /// <summary>TOTP authenticator apps allowed as a factor.</summary>
    [JsonPropertyName("allow_totp")]
    public bool AllowTotp { get; set; } = true;

    /// <summary>Email one-time passcodes allowed as a factor.</summary>
    [JsonPropertyName("allow_email_otp")]
    public bool AllowEmailOtp { get; set; } = true;
}

/// <summary>What a login attempt must do about MFA after the password check passes.</summary>
public enum MfaLoginRequirement
{
    /// <summary>No second factor needed — issue the session immediately.</summary>
    NotRequired,

    /// <summary>The user has active factor(s); challenge before issuing the session.</summary>
    ChallengeRequired,

    /// <summary>Policy requires MFA but the user has none — force enrolment before sign-in.</summary>
    EnrollmentRequired
}

/// <summary>The challenge methods available to a user at the MFA gate.</summary>
public sealed record MfaAvailableMethods(bool Totp, bool EmailOtp, bool BackupCodes)
{
    public bool Any => Totp || EmailOtp || BackupCodes;
}

/// <summary>Decision returned by the login-time MFA evaluation.</summary>
public sealed record MfaLoginDecision(MfaLoginRequirement Requirement, MfaAvailableMethods Methods);

/// <summary>The data needed to render a TOTP enrolment screen (shown once).</summary>
/// <param name="FactorId">The pending factor to confirm.</param>
/// <param name="Secret">Base32 secret for manual entry.</param>
/// <param name="ProvisioningUri">The <c>otpauth://</c> URI to encode as a QR code.</param>
public sealed record TotpEnrollment(Guid FactorId, string Secret, string ProvisioningUri);

/// <summary>A freshly generated set of recovery codes — shown to the user exactly once.</summary>
public sealed record BackupCodesResult(IReadOnlyList<string> Codes);

/// <summary>The MFA method a user presents at the login challenge.</summary>
public enum MfaChallengeMethod
{
    Totp,
    EmailOtp,
    BackupCode
}

public sealed class MfaFactorNotFoundException : Exception
{
    public MfaFactorNotFoundException(Guid id) : base($"MFA factor '{id}' was not found.") { }
}

public sealed class MfaMethodNotAllowedException : Exception
{
    public MfaMethodNotAllowedException(string method)
        : base($"The MFA method '{method}' is not enabled for this tenant.") { }
}
