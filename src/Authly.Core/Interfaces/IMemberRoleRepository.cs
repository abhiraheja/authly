using Authly.Core.Entities;

namespace Authly.Core.Interfaces;

/// <summary>
/// Persistence for membership↔operator-role assignments and the resolution of a membership's effective
/// operator roles and permissions (used by the console guard). Organization-scoped (global, NO RLS).
/// Implemented in Infrastructure. Mirrors <see cref="IUserRoleRepository"/>.
/// </summary>
public interface IMemberRoleRepository
{
    Task AssignAsync(MemberRole assignment, CancellationToken ct = default);
    Task RemoveAsync(Guid organizationMembershipId, Guid operatorRoleId, CancellationToken ct = default);

    Task<IReadOnlyList<OperatorRole>> ListRolesForMembershipAsync(Guid organizationMembershipId, CancellationToken ct = default);

    /// <summary>The membership's operator-role names (e.g. <c>org_owner</c>).</summary>
    Task<IReadOnlyList<string>> GetRoleNamesAsync(Guid organizationMembershipId, CancellationToken ct = default);

    /// <summary>The membership's flattened, distinct operator permissions as <c>resource.action</c>.</summary>
    Task<IReadOnlyList<string>> GetPermissionNamesAsync(Guid organizationMembershipId, CancellationToken ct = default);

    /// <summary>How many memberships in the org hold the given operator role (for "last owner" guards).</summary>
    Task<int> CountMembershipsWithRoleAsync(Guid organizationId, Guid operatorRoleId, CancellationToken ct = default);
}
