namespace Authly.Core.Entities;

/// <summary>
/// Single-use, expiring token for an employee (operator) invite. Only the SHA-256 hash is stored;
/// the raw token travels in the accept link and is never persisted. Accepting it sets the invited
/// <see cref="Account"/>'s password (when none yet) + email-verified flag, and flips their
/// <see cref="OrganizationMembership"/> for <see cref="OrganizationId"/> to Active. Maps to table
/// "account_invite_tokens". Mirrors <see cref="PasswordResetToken"/> (with an org dimension).
/// </summary>
public class AccountInviteToken
{
    public Guid Id { get; set; }

    /// <summary>The invited global console account (find-or-created at invite time).</summary>
    public Guid AccountId { get; set; }

    /// <summary>The organization the invite grants membership to.</summary>
    public Guid OrganizationId { get; set; }

    /// <summary>SHA-256 of the raw token. Unique.</summary>
    public string TokenHash { get; set; } = default!;

    public DateTimeOffset ExpiresAt { get; set; }
    public bool Used { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public Account Account { get; set; } = default!;
}
