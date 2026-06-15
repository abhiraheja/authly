using Authly.Core.Entities;
using Authly.Modules.Common;

namespace Authly.Modules.Authorization;

/// <summary>
/// Role-based access control for a tenant: seeds the system roles/permissions, manages custom
/// roles and their permission mappings, assigns roles to users, and resolves a user's effective
/// roles + permissions for token claim assembly. All operations are tenant-scoped.
/// </summary>
public interface IRbacService
{
    /// <summary>
    /// Idempotently seeds the system roles, the baseline permission catalogue, and their default
    /// mappings for the tenant. Safe to call repeatedly (e.g. on tenant-admin bootstrap).
    /// </summary>
    Task EnsureSystemRolesAsync(Guid tenantId, CancellationToken ct = default);

    // --- Roles ---
    Task<IReadOnlyList<Role>> ListRolesAsync(Guid tenantId, CancellationToken ct = default);
    Task<RoleWithPermissions?> GetRoleAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task<Role> CreateRoleAsync(Guid tenantId, CreateRoleRequest request, AuditContext actor, CancellationToken ct = default);
    Task SetRolePermissionsAsync(Guid tenantId, Guid roleId, IReadOnlyCollection<Guid> permissionIds, AuditContext actor, CancellationToken ct = default);
    Task DeleteRoleAsync(Guid tenantId, Guid roleId, AuditContext actor, CancellationToken ct = default);

    // --- Permissions ---
    Task<IReadOnlyList<Permission>> ListPermissionsAsync(Guid tenantId, CancellationToken ct = default);

    // --- User assignments ---
    Task<IReadOnlyList<Role>> ListUserRolesAsync(Guid tenantId, Guid userId, CancellationToken ct = default);
    Task AssignRoleAsync(Guid tenantId, Guid userId, Guid roleId, AuditContext actor, CancellationToken ct = default);
    Task RemoveRoleAsync(Guid tenantId, Guid userId, Guid roleId, AuditContext actor, CancellationToken ct = default);

    /// <summary>Resolves the user's effective roles + permissions for injection into tokens.</summary>
    Task<UserAuthorization> GetUserAuthorizationAsync(Guid tenantId, Guid userId, CancellationToken ct = default);
}
