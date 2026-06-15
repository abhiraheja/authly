namespace Authly.Core.Entities;

/// <summary>
/// A persisted login session for an end-user. For server-rendered cookie logins the
/// session id is carried in the auth cookie; the refresh-token hash is a SHA-256 of an
/// opaque secret used to look the session up and (from Phase 3) to rotate OAuth refresh
/// tokens. Maps to table "sessions".
/// </summary>
public class Session
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }
    public Guid TenantId { get; set; }

    /// <summary>Set once OAuth applications exist (Phase 3); null for first-party cookie sessions.</summary>
    public Guid? ApplicationId { get; set; }

    /// <summary>SHA-256 of the opaque session/refresh secret. Never store the raw value.</summary>
    public string RefreshTokenHash { get; set; } = default!;

    /// <summary>Groups rotated tokens so a reused (stale) token can kill the whole family.</summary>
    public Guid RefreshFamilyId { get; set; }

    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? DeviceFingerprint { get; set; }
    public string? Location { get; set; }

    public bool Trusted { get; set; }

    public DateTimeOffset LastActiveAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public bool Revoked { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public User User { get; set; } = default!;
}
