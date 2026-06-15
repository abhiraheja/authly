namespace Authly.Core.Entities;

/// <summary>
/// Single-use, expiring token for resetting a user's password. Only the SHA-256 hash is
/// stored; the raw token travels in the reset link and is never persisted. Maps to table
/// "password_reset_tokens".
/// </summary>
public class PasswordResetToken
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    /// <summary>SHA-256 of the raw token. Unique.</summary>
    public string TokenHash { get; set; } = default!;

    public DateTimeOffset ExpiresAt { get; set; }
    public bool Used { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public User User { get; set; } = default!;
}
