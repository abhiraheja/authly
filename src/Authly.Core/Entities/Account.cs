using Authly.Core.Enums;

namespace Authly.Core.Entities;

/// <summary>
/// A global console operator (employee) login. NOT tenant-scoped and NOT an app end-user
/// (<see cref="User"/>) — accounts operate the admin console and never receive OAuth tokens.
/// An account is a member of one or more <see cref="Organization"/>s. Maps to table "accounts".
/// </summary>
public class Account
{
    public Guid Id { get; set; }

    /// <summary>Globally unique login email (lower-cased).</summary>
    public string Email { get; set; } = default!;

    /// <summary>Argon2id hash. Null while an invite is pending (cannot sign in until accepted).</summary>
    public string? PasswordHash { get; set; }

    public string? FirstName { get; set; }
    public string? LastName { get; set; }

    public bool EmailVerified { get; set; }

    public AccountStatus Status { get; set; } = AccountStatus.Active;

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? LastLoginAt { get; set; }

    public ICollection<OrganizationMembership> Memberships { get; set; } = new List<OrganizationMembership>();
}
