namespace Authly.Core.Entities;

/// <summary>
/// Single-use, expiring token for verifying ownership of an email/phone (or for a magic
/// link). Only the SHA-256 hash is stored; the raw token travels in the link/message and
/// is never persisted. Maps to table "verification_tokens".
/// </summary>
public class VerificationToken
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    /// <summary>email | phone | magic_link</summary>
    public string Type { get; set; } = default!;

    /// <summary>The email/phone being verified.</summary>
    public string Target { get; set; } = default!;

    /// <summary>SHA-256 of the raw token. Unique.</summary>
    public string TokenHash { get; set; } = default!;

    public DateTimeOffset ExpiresAt { get; set; }
    public bool Used { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public User User { get; set; } = default!;
}
