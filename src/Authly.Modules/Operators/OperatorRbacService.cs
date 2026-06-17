using Authly.Core.Authorization;
using Authly.Core.Entities;
using Authly.Core.Interfaces;
using Authly.Modules.Audit;
using Authly.Modules.Authorization;
using Authly.Modules.Common;

namespace Authly.Modules.Operators;

/// <summary>An operator role together with the permission ids it currently grants (for the edit UI).</summary>
public sealed record OperatorRoleWithPermissions(OperatorRole Role, IReadOnlyList<Guid> PermissionIds);

/// <summary>The last <c>org_owner</c> of an organization cannot have that role (or their membership) removed.</summary>
public sealed class LastOwnerProtectedException()
    : Exception("The last owner of an organization can't be removed. Assign another owner first.");

/// <summary>
/// Seeds and manages the per-organization operator-RBAC catalogue (system <see cref="OperatorRole"/>s
/// + <see cref="OperatorPermission"/>s), grants roles to memberships, and supports the console role /
/// member-role management UIs. The console-operator counterpart of <c>RbacService</c>; never touches
/// end-user RBAC. Reuses the end-user RBAC exceptions (<see cref="RoleNotFoundException"/> etc.).
/// </summary>
public interface IOperatorRbacService
{
    /// <summary>Idempotently ensures the operator permission catalogue and the four system roles
    /// (org_owner/org_admin/project_admin/viewer) exist for the org, with their default grants.</summary>
    Task EnsureSystemRolesAsync(Guid organizationId, CancellationToken ct = default);

    /// <summary>Grants a system operator role (by name) to a membership. Used when founding/inviting.</summary>
    Task AssignSystemRoleAsync(Guid organizationId, Guid membershipId, string roleName, Guid? grantedByAccountId, CancellationToken ct = default);

    // --- Operator role management (drives OperatorRolesController) ---
    Task<IReadOnlyList<OperatorRole>> ListRolesAsync(Guid organizationId, CancellationToken ct = default);
    Task<OperatorRoleWithPermissions?> GetRoleAsync(Guid organizationId, Guid id, CancellationToken ct = default);
    Task<OperatorRole> CreateRoleAsync(Guid organizationId, CreateRoleRequest request, AuditContext actor, CancellationToken ct = default);
    Task SetRolePermissionsAsync(Guid organizationId, Guid roleId, IReadOnlyCollection<Guid> permissionIds, AuditContext actor, CancellationToken ct = default);
    Task DeleteRoleAsync(Guid organizationId, Guid roleId, AuditContext actor, CancellationToken ct = default);
    Task<IReadOnlyList<OperatorPermission>> ListPermissionsAsync(Guid organizationId, CancellationToken ct = default);

    // --- Membership ↔ role assignments (drives MembersController) ---
    Task<IReadOnlyList<OperatorRole>> ListMemberRolesAsync(Guid membershipId, CancellationToken ct = default);
    Task AssignRoleToMemberAsync(Guid organizationId, Guid membershipId, Guid roleId, AuditContext actor, CancellationToken ct = default);
    /// <summary>Removes an operator role from a membership. Refuses to strip the last <c>org_owner</c>.</summary>
    Task RemoveRoleFromMemberAsync(Guid organizationId, Guid membershipId, Guid roleId, AuditContext actor, CancellationToken ct = default);
}

/// <inheritdoc />
public sealed class OperatorRbacService : IOperatorRbacService
{
    private readonly IOperatorRoleRepository _roles;
    private readonly IMemberRoleRepository _memberRoles;
    private readonly IAuditLogger _audit;

    public OperatorRbacService(IOperatorRoleRepository roles, IMemberRoleRepository memberRoles, IAuditLogger audit)
    {
        _roles = roles;
        _memberRoles = memberRoles;
        _audit = audit;
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

    // --- Operator role management ---

    public Task<IReadOnlyList<OperatorRole>> ListRolesAsync(Guid organizationId, CancellationToken ct = default)
        => _roles.ListRolesAsync(organizationId, ct);

    public async Task<OperatorRoleWithPermissions?> GetRoleAsync(Guid organizationId, Guid id, CancellationToken ct = default)
    {
        var role = await _roles.GetRoleAsync(organizationId, id, ct);
        if (role is null) return null;
        var permissionIds = await _roles.ListPermissionIdsForRoleAsync(role.Id, ct);
        return new OperatorRoleWithPermissions(role, permissionIds);
    }

    public async Task<OperatorRole> CreateRoleAsync(Guid organizationId, CreateRoleRequest request, AuditContext actor, CancellationToken ct = default)
    {
        var name = request.Name.Trim();
        if (await _roles.GetRoleByNameAsync(organizationId, name, ct) is not null)
            throw new RoleNameAlreadyExistsException(name);

        var role = new OperatorRole
        {
            OrganizationId = organizationId,
            Name = name,
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            IsSystem = false,
            CreatedAt = DateTimeOffset.UtcNow
        };
        await _roles.AddRoleAsync(role, ct);
        await _audit.LogAsync("operator_role.created", actor,
            resourceType: "operator_role", resourceId: role.Id, metadata: new { role.Name, organizationId }, ct: ct);
        return role;
    }

    public async Task SetRolePermissionsAsync(Guid organizationId, Guid roleId, IReadOnlyCollection<Guid> permissionIds, AuditContext actor, CancellationToken ct = default)
    {
        var role = await _roles.GetRoleAsync(organizationId, roleId, ct) ?? throw new RoleNotFoundException(roleId);

        // Permission ids must belong to this org — never map across org boundaries.
        var validIds = (await _roles.ListPermissionsAsync(organizationId, ct)).Select(p => p.Id).ToHashSet();
        var filtered = permissionIds.Where(validIds.Contains).ToList();

        await _roles.SetRolePermissionsAsync(role.Id, filtered, ct);
        await _audit.LogAsync("operator_role.permissions_updated", actor,
            resourceType: "operator_role", resourceId: role.Id, metadata: new { count = filtered.Count, organizationId }, ct: ct);
    }

    public async Task DeleteRoleAsync(Guid organizationId, Guid roleId, AuditContext actor, CancellationToken ct = default)
    {
        var role = await _roles.GetRoleAsync(organizationId, roleId, ct) ?? throw new RoleNotFoundException(roleId);
        if (role.IsSystem) throw new SystemRoleProtectedException(role.Name);

        await _roles.DeleteRoleAsync(role, ct);
        await _audit.LogAsync("operator_role.deleted", actor,
            resourceType: "operator_role", resourceId: role.Id, metadata: new { role.Name, organizationId }, ct: ct);
    }

    public Task<IReadOnlyList<OperatorPermission>> ListPermissionsAsync(Guid organizationId, CancellationToken ct = default)
        => _roles.ListPermissionsAsync(organizationId, ct);

    // --- Membership ↔ role assignments ---

    public Task<IReadOnlyList<OperatorRole>> ListMemberRolesAsync(Guid membershipId, CancellationToken ct = default)
        => _memberRoles.ListRolesForMembershipAsync(membershipId, ct);

    public async Task AssignRoleToMemberAsync(Guid organizationId, Guid membershipId, Guid roleId, AuditContext actor, CancellationToken ct = default)
    {
        var role = await _roles.GetRoleAsync(organizationId, roleId, ct) ?? throw new RoleNotFoundException(roleId);
        await _memberRoles.AssignAsync(new MemberRole
        {
            OrganizationMembershipId = membershipId,
            OperatorRoleId = role.Id,
            OrganizationId = organizationId,
            GrantedByAccountId = actor.ActorId,
            GrantedAt = DateTimeOffset.UtcNow
        }, ct);
        await _audit.LogAsync("member.role_assigned", actor,
            resourceType: "organization_membership", resourceId: membershipId, metadata: new { role = role.Name, organizationId }, ct: ct);
    }

    public async Task RemoveRoleFromMemberAsync(Guid organizationId, Guid membershipId, Guid roleId, AuditContext actor, CancellationToken ct = default)
    {
        var role = await _roles.GetRoleAsync(organizationId, roleId, ct) ?? throw new RoleNotFoundException(roleId);

        // Never strip the last owner of the org of their owner role.
        if (role.Name == OperatorRbac.OrgOwner)
            await GuardLastOwnerAsync(organizationId, role.Id, ct);

        await _memberRoles.RemoveAsync(membershipId, role.Id, ct);
        await _audit.LogAsync("member.role_removed", actor,
            resourceType: "organization_membership", resourceId: membershipId, metadata: new { role = role.Name, organizationId }, ct: ct);
    }

    private async Task GuardLastOwnerAsync(Guid organizationId, Guid ownerRoleId, CancellationToken ct)
    {
        if (await _memberRoles.CountMembershipsWithRoleAsync(organizationId, ownerRoleId, ct) <= 1)
            throw new LastOwnerProtectedException();
    }
}
