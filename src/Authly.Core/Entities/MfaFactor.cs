using Authly.Core.Enums;

namespace Authly.Core.Entities;

/// <summary>
/// A second-factor credential enrolled by a user. For TOTP the shared <see cref="Secret"/> is
/// AES-256-GCM encrypted at rest; for email/WhatsApp OTP no secret is stored (codes live in
/// <see cref="OtpCode"/>). Maps to table "mfa_factors".
/// </summary>
public class MfaFactor
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid TenantId { get; set; }

    public MfaFactorType Type { get; set; }

    /// <summary>
    /// AES-256 encrypted TOTP secret, or — for passkeys — the JSON-encoded WebAuthn credential
    /// (public key, sign counter, AAGUID). NULL for OTP factors.
    /// </summary>
    public string? Secret { get; set; }

    /// <summary>
    /// For passkeys: the base64url WebAuthn credential id, used to match an assertion to this
    /// factor. NULL for non-passkey factors.
    /// </summary>
    public string? CredentialId { get; set; }

    public MfaFactorStatus Status { get; set; } = MfaFactorStatus.Pending;

    /// <summary>User-facing label (e.g. "iPhone Authenticator").</summary>
    public string? FriendlyName { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? LastUsedAt { get; set; }
}
