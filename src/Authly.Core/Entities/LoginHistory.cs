namespace Authly.Core.Entities;

/// <summary>
/// Append-only record of an authentication attempt (success or failure). Maps to table
/// "login_history". Written for every login attempt so tenants can audit access and the
/// platform can drive lockout / anomaly detection (later phases).
/// </summary>
public class LoginHistory
{
    public Guid Id { get; set; }

    /// <summary>Null when the attempt could not be tied to a known user (e.g. unknown email).</summary>
    public Guid? UserId { get; set; }

    public Guid TenantId { get; set; }

    /// <summary>success | failed | blocked | mfa_required</summary>
    public string Result { get; set; } = default!;

    /// <summary>password | google | whatsapp_otp | magic_link | ...</summary>
    public string? Method { get; set; }

    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? Device { get; set; }
    public string? Location { get; set; }

    /// <summary>Why the attempt failed or was blocked. Never contains the attempted password.</summary>
    public string? Reason { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
