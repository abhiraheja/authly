using Authly.Core.Enums;

namespace Authly.Core.Entities;

/// <summary>
/// Links an <see cref="Account"/> to an <see cref="Organization"/> (the employee directory entry).
/// Operator roles are granted against this membership (org-level scope). Global / RLS-exempt.
/// Unique per (AccountId, OrganizationId). Maps to table "organization_memberships".
/// </summary>
public class OrganizationMembership
{
    public Guid Id { get; set; }

    public Guid AccountId { get; set; }

    public Guid OrganizationId { get; set; }

    public MembershipStatus Status { get; set; } = MembershipStatus.Invited;

    /// <summary>The account that issued the invitation (null for the founding owner).</summary>
    public Guid? InvitedByAccountId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public Account? Account { get; set; }
    public Organization? Organization { get; set; }
}
