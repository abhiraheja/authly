namespace Authly.Core.Entities;

/// <summary>
/// Assignment of an <see cref="OperatorRole"/> to an <see cref="OrganizationMembership"/> — the
/// operator analogue of <see cref="UserRole"/>, keyed on the membership so removing a member cascades
/// their role grants. Org-scoped. Composite primary key (organization_membership_id, operator_role_id).
/// Maps to table "member_roles".
/// </summary>
public class MemberRole
{
    public Guid OrganizationMembershipId { get; set; }
    public Guid OperatorRoleId { get; set; }
    public Guid OrganizationId { get; set; }

    /// <summary>The account that granted this role (null for system-seeded / founding grants).</summary>
    public Guid? GrantedByAccountId { get; set; }

    public DateTimeOffset GrantedAt { get; set; }

    public OperatorRole OperatorRole { get; set; } = default!;
}
