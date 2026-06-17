using Authly.Core.Authorization;
using Authly.Core.Enums;
using Authly.Core.Interfaces;
using Authly.Modules.Audit;
using Authly.Modules.Common;
using Authly.Modules.Operators;

namespace Authly.Modules.Members;

/// <summary>A single operator's directory row: their membership, identity, status, and operator roles.</summary>
public sealed record MemberRow(
    Guid MembershipId,
    Guid AccountId,
    string Email,
    string? Name,
    MembershipStatus Status,
    bool IsOwner,
    IReadOnlyList<string> RoleNames);

/// <summary>The employee/member that could not be found in the organization.</summary>
public sealed class MemberNotFoundException(Guid membershipId) : Exception($"Membership {membershipId} was not found in this organization.");

/// <summary>
/// Read + lifecycle of an organization's operator directory (list, view, remove). Role granting lives
/// on <see cref="IOperatorRbacService"/>; inviting lives on <see cref="IInvitationService"/>.
/// </summary>
public interface IMemberDirectoryService
{
    Task<IReadOnlyList<MemberRow>> ListMembersAsync(Guid organizationId, CancellationToken ct = default);
    Task<MemberRow?> GetMemberAsync(Guid organizationId, Guid membershipId, CancellationToken ct = default);
    /// <summary>Removes a member from the org (cascading their role grants). Refuses the last owner.</summary>
    Task RemoveMemberAsync(Guid organizationId, Guid membershipId, AuditContext actor, CancellationToken ct = default);
}

/// <inheritdoc />
public sealed class MemberDirectoryService : IMemberDirectoryService
{
    private readonly IOrganizationMembershipRepository _memberships;
    private readonly IMemberRoleRepository _memberRoles;
    private readonly IOperatorRoleRepository _roles;
    private readonly IAuditLogger _audit;

    public MemberDirectoryService(
        IOrganizationMembershipRepository memberships,
        IMemberRoleRepository memberRoles,
        IOperatorRoleRepository roles,
        IAuditLogger audit)
    {
        _memberships = memberships;
        _memberRoles = memberRoles;
        _roles = roles;
        _audit = audit;
    }

    public async Task<IReadOnlyList<MemberRow>> ListMembersAsync(Guid organizationId, CancellationToken ct = default)
    {
        var memberships = await _memberships.ListByOrganizationWithAccountsAsync(organizationId, ct);
        var rows = new List<MemberRow>(memberships.Count);
        foreach (var m in memberships)
        {
            var roleNames = await _memberRoles.GetRoleNamesAsync(m.Id, ct);
            rows.Add(ToRow(m, roleNames));
        }
        return rows;
    }

    public async Task<MemberRow?> GetMemberAsync(Guid organizationId, Guid membershipId, CancellationToken ct = default)
    {
        var membership = await _memberships.GetByIdAsync(membershipId, ct);
        if (membership is null || membership.OrganizationId != organizationId) return null;

        // GetByIdAsync doesn't eager-load the account; resolve it from the org directory listing.
        var withAccount = (await _memberships.ListByOrganizationWithAccountsAsync(organizationId, ct))
            .FirstOrDefault(m => m.Id == membershipId);
        if (withAccount is null) return null;

        var roleNames = await _memberRoles.GetRoleNamesAsync(membershipId, ct);
        return ToRow(withAccount, roleNames);
    }

    public async Task RemoveMemberAsync(Guid organizationId, Guid membershipId, AuditContext actor, CancellationToken ct = default)
    {
        var membership = await _memberships.GetByIdAsync(membershipId, ct);
        if (membership is null || membership.OrganizationId != organizationId)
            throw new MemberNotFoundException(membershipId);

        // Can't remove the last owner — they'd orphan the organization.
        var ownerRole = await _roles.GetRoleByNameAsync(organizationId, OperatorRbac.OrgOwner, ct);
        if (ownerRole is not null)
        {
            var roleNames = await _memberRoles.GetRoleNamesAsync(membershipId, ct);
            if (roleNames.Contains(OperatorRbac.OrgOwner)
                && await _memberRoles.CountMembershipsWithRoleAsync(organizationId, ownerRole.Id, ct) <= 1)
                throw new LastOwnerProtectedException();
        }

        // member_roles cascade on the membership FK, so removing the membership clears its grants.
        membership.Status = MembershipStatus.Disabled;
        await _memberships.UpdateAsync(membership, ct);
        await _memberRoles.RemoveAllForMembershipAsync(membershipId, ct);

        await _audit.LogAsync("member.removed", actor,
            resourceType: "organization_membership", resourceId: membershipId, metadata: new { organizationId }, ct: ct);
    }

    private static MemberRow ToRow(Core.Entities.OrganizationMembership m, IReadOnlyList<string> roleNames) => new(
        m.Id, m.AccountId,
        m.Account?.Email ?? "(unknown)",
        m.Account is null ? null : $"{m.Account.FirstName} {m.Account.LastName}".Trim() is { Length: > 0 } n ? n : null,
        m.Status,
        roleNames.Contains(OperatorRbac.OrgOwner),
        roleNames);
}
