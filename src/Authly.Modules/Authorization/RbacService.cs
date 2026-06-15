using Authly.Core.Authorization;
using Authly.Core.Entities;
using Authly.Core.Interfaces;
using Authly.Modules.Audit;
using Authly.Modules.Common;

namespace Authly.Modules.Authorization;

/// <inheritdoc />
public sealed class RbacService : IRbacService
{
    private readonly IRoleRepository _roles;
    private readonly IUserRoleRepository _userRoles;
    private readonly IAuditLogger _audit;

    public RbacService(IRoleRepository roles, IUserRoleRepository userRoles, IAuditLogger audit)
    {
        _roles = roles;
        _userRoles = userRoles;
        _audit = audit;
    }

    public async Task EnsureSystemRolesAsync(Guid tenantId, CancellationToken ct = default)
    {
        // 1) Ensure the baseline permission catalogue exists, indexed by resource.action.
        var existingPerms = (await _roles.ListPermissionsAsync(tenantId, ct))
            .ToDictionary(p => p.Name, StringComparer.Ordinal);

        foreach (var def in SystemRbac.Permissions)
        {
            if (existingPerms.ContainsKey(def.Name)) continue;
            var permission = new Permission { TenantId = tenantId, Resource = def.Resource, Action = def.Action };
            await _roles.AddPermissionAsync(permission, ct);
            existingPerms[def.Name] = permission;
        }

        // 2) Ensure each system role exists. Default permissions are applied only when the role is
        //    first created — re-seeding must never overwrite an admin's later customizations.
        foreach (var roleName in SystemRbac.RoleNames)
        {
            if (await _roles.GetRoleByNameAsync(tenantId, roleName, ct) is not null) continue;

            var role = new Role { TenantId = tenantId, Name = roleName, IsSystem = true, CreatedAt = DateTimeOffset.UtcNow };
            await _roles.AddRoleAsync(role, ct);

            var desired = SystemRbac.PermissionsFor(roleName)
                .Where(existingPerms.ContainsKey)
                .Select(n => existingPerms[n].Id)
                .ToList();
            await _roles.SetRolePermissionsAsync(role.Id, desired, ct);
        }
    }

    // --- Roles ---

    public Task<IReadOnlyList<Role>> ListRolesAsync(Guid tenantId, CancellationToken ct = default)
        => _roles.ListRolesAsync(tenantId, ct);

    public async Task<RoleWithPermissions?> GetRoleAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        var role = await _roles.GetRoleAsync(tenantId, id, ct);
        if (role is null) return null;
        var permissionIds = await _roles.ListPermissionIdsForRoleAsync(role.Id, ct);
        return new RoleWithPermissions(role, permissionIds);
    }

    public async Task<Role> CreateRoleAsync(Guid tenantId, CreateRoleRequest request, AuditContext actor, CancellationToken ct = default)
    {
        var name = request.Name.Trim();
        if (await _roles.GetRoleByNameAsync(tenantId, name, ct) is not null)
            throw new RoleNameAlreadyExistsException(name);

        var role = new Role
        {
            TenantId = tenantId,
            Name = name,
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            IsSystem = false,
            CreatedAt = DateTimeOffset.UtcNow
        };
        await _roles.AddRoleAsync(role, ct);
        await _audit.LogAsync("role.created", actor, tenantId, "role", role.Id, metadata: new { role.Name }, ct: ct);
        return role;
    }

    public async Task SetRolePermissionsAsync(Guid tenantId, Guid roleId, IReadOnlyCollection<Guid> permissionIds, AuditContext actor, CancellationToken ct = default)
    {
        var role = await _roles.GetRoleAsync(tenantId, roleId, ct) ?? throw new RoleNotFoundException(roleId);

        // Permission ids must belong to this tenant — never map across tenant boundaries.
        var validIds = (await _roles.ListPermissionsAsync(tenantId, ct)).Select(p => p.Id).ToHashSet();
        var filtered = permissionIds.Where(validIds.Contains).ToList();

        await _roles.SetRolePermissionsAsync(role.Id, filtered, ct);
        await _audit.LogAsync("role.permissions_updated", actor, tenantId, "role", role.Id,
            metadata: new { count = filtered.Count }, ct: ct);
    }

    public async Task DeleteRoleAsync(Guid tenantId, Guid roleId, AuditContext actor, CancellationToken ct = default)
    {
        var role = await _roles.GetRoleAsync(tenantId, roleId, ct) ?? throw new RoleNotFoundException(roleId);
        if (role.IsSystem) throw new SystemRoleProtectedException(role.Name);

        await _roles.DeleteRoleAsync(role, ct);
        await _audit.LogAsync("role.deleted", actor, tenantId, "role", role.Id, metadata: new { role.Name }, ct: ct);
    }

    // --- Permissions ---

    public Task<IReadOnlyList<Permission>> ListPermissionsAsync(Guid tenantId, CancellationToken ct = default)
        => _roles.ListPermissionsAsync(tenantId, ct);

    // --- User assignments ---

    public Task<IReadOnlyList<Role>> ListUserRolesAsync(Guid tenantId, Guid userId, CancellationToken ct = default)
        => _userRoles.ListRolesForUserAsync(tenantId, userId, ct);

    public async Task AssignRoleAsync(Guid tenantId, Guid userId, Guid roleId, AuditContext actor, CancellationToken ct = default)
    {
        var role = await _roles.GetRoleAsync(tenantId, roleId, ct) ?? throw new RoleNotFoundException(roleId);
        await _userRoles.AssignAsync(new UserRole
        {
            UserId = userId,
            RoleId = role.Id,
            TenantId = tenantId,
            GrantedBy = actor.ActorId,
            GrantedAt = DateTimeOffset.UtcNow
        }, ct);
        await _audit.LogAsync("user.role_assigned", actor, tenantId, "user", userId,
            metadata: new { role = role.Name }, ct: ct);
    }

    public async Task RemoveRoleAsync(Guid tenantId, Guid userId, Guid roleId, AuditContext actor, CancellationToken ct = default)
    {
        var role = await _roles.GetRoleAsync(tenantId, roleId, ct) ?? throw new RoleNotFoundException(roleId);
        await _userRoles.RemoveAsync(tenantId, userId, role.Id, ct);
        await _audit.LogAsync("user.role_removed", actor, tenantId, "user", userId,
            metadata: new { role = role.Name }, ct: ct);
    }

    public async Task<UserAuthorization> GetUserAuthorizationAsync(Guid tenantId, Guid userId, CancellationToken ct = default)
    {
        var roles = await _userRoles.GetRoleNamesAsync(tenantId, userId, ct);
        var permissions = await _userRoles.GetPermissionNamesAsync(tenantId, userId, ct);
        return new UserAuthorization(roles, permissions);
    }
}
