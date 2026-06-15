using Authly.Core.Enums;

namespace Authly.Core.Entities;

/// <summary>
/// A short-lived, single-use numeric passcode delivered out-of-band (email/WhatsApp). Only the
/// hash is stored; <see cref="Attempts"/> caps brute force. Maps to table "otp_codes".
/// </summary>
public class OtpCode
{
    public Guid Id { get; set; }

    /// <summary>The user the code was issued to (nullable for pre-account flows).</summary>
    public Guid? UserId { get; set; }
    public Guid TenantId { get; set; }

    public OtpChannel Channel { get; set; }

    /// <summary>SHA-256 of the numeric code.</summary>
    public string CodeHash { get; set; } = default!;

    public int Attempts { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public bool Used { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
