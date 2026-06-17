using Authly.Core.Authorization;
using Authly.Core.Entities;
using Authly.Core.Interfaces;

namespace Authly.Modules.Operators;

/// <summary>
/// Seeds and manages the per-organization operator-RBAC catalogue (system <see cref="OperatorRole"/>s
/// + <see cref="OperatorPermission"/>s) and grants roles to memberships. The console-operator
/// counterpart of <c>RbacService</c>; never touches end-user RBAC.
/// </summary>
public interface IOperatorRbacService
{
    /// <summary>Idempotently ensures the operator permission catalogue and the four system roles
    /// (org_owner/org_admin/project_admin/viewer) exist for the org, with their default grants.</summary>
    Task EnsureSystemRolesAsync(Guid organizationId, CancellationToken ct = default);

    /// <summary>Grants a system operator role (by name) to a membership. Used when founding/inviting.</summary>
    Task AssignSystemRoleAsync(Guid organizationId, Guid membershipId, string roleName, Guid? grantedByAccountId, CancellationToken ct = default);
}

/// <inheritdoc />
public sealed class OperatorRbacService : IOperatorRbacService
{
    private readonly IOperatorRoleRepository _roles;
    private readonly IMemberRoleRepository _memberRoles;

    public OperatorRbacService(IOperatorRoleRepository roles, IMemberRoleRepository memberRoles)
    {
        _roles = roles;
        _memberRoles = memberRoles;
    }

    public async Task EnsureSystemRolesAsync(Guid organizationId, CancellationToken ct = default)
    {
        // 1) Ensure the operator permission catalogue exists, indexed by resource.action.
        var existingPerms = (await _roles.ListPermissionsAsync(organizationId, ct))
            .ToDictionary(p => p.Name, StringComparer.Ordinal);

        foreach (var def in OperatorRbac.Permissions)
        {
            if (existingPerms.ContainsKey(def.Name)) continue;
            var permission = new OperatorPermission { OrganizationId = organizationId, Resource = def.Resource, Action = def.Action };
            await _roles.AddPermissionAsync(permission, ct);
            existingPerms[def.Name] = permission;
        }

        // 2) Ensure each system role exists. Default permissions are applied only on first creation —
        //    re-seeding must never overwrite an admin's later customizations.
        foreach (var roleName in OperatorRbac.RoleNames)
        {
            if (await _roles.GetRoleByNameAsync(organizationId, roleName, ct) is not null) continue;

            var role = new OperatorRole { OrganizationId = organizationId, Name = roleName, IsSystem = true, CreatedAt = DateTimeOffset.UtcNow };
            await _roles.AddRoleAsync(role, ct);

            var desired = OperatorRbac.PermissionsFor(roleName)
                .Where(existingPerms.ContainsKey)
                .Select(n => existingPerms[n].Id)
                .ToList();
            await _roles.SetRolePermissionsAsync(role.Id, desired, ct);
        }
    }

    public async Task AssignSystemRoleAsync(Guid organizationId, Guid membershipId, string roleName, Guid? grantedByAccountId, CancellationToken ct = default)
    {
        var role = await _roles.GetRoleByNameAsync(organizationId, roleName, ct)
            ?? throw new InvalidOperationException($"Operator role '{roleName}' is not seeded for organization {organizationId}.");

        await _memberRoles.AssignAsync(new MemberRole
        {
            OrganizationMembershipId = membershipId,
            OperatorRoleId = role.Id,
            OrganizationId = organizationId,
            GrantedByAccountId = grantedByAccountId,
            GrantedAt = DateTimeOffset.UtcNow
        }, ct);
    }
}
