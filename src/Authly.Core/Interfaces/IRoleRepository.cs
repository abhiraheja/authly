using Authly.Core.Entities;

namespace Authly.Core.Interfaces;

/// <summary>
/// Persistence for the RBAC aggregate: <see cref="Role"/>, <see cref="Permission"/>, and the
/// role↔permission mapping. All operations are tenant-scoped (the tenant is also the RLS
/// boundary). Implemented in Infrastructure.
/// </summary>
public interface IRoleRepository
{
    // --- Roles ---
    Task<IReadOnlyList<Role>> ListRolesAsync(Guid tenantId, CancellationToken ct = default);
    Task<Role?> GetRoleAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task<Role?> GetRoleByNameAsync(Guid tenantId, string name, CancellationToken ct = default);
    Task<bool> AnyRolesAsync(Guid tenantId, CancellationToken ct = default);
    Task AddRoleAsync(Role role, CancellationToken ct = default);
    Task UpdateRoleAsync(Role role, CancellationToken ct = default);
    Task DeleteRoleAsync(Role role, CancellationToken ct = default);

    // --- Permissions ---
    Task<IReadOnlyList<Permission>> ListPermissionsAsync(Guid tenantId, CancellationToken ct = default);
    Task<Permission?> GetPermissionAsync(Guid tenantId, string resource, string action, CancellationToken ct = default);
    Task AddPermissionAsync(Permission permission, CancellationToken ct = default);

    // --- Role ↔ permission mapping ---
    Task<IReadOnlyList<Guid>> ListPermissionIdsForRoleAsync(Guid roleId, CancellationToken ct = default);
    /// <summary>Replaces the role's permission set with exactly the supplied permission ids.</summary>
    Task SetRolePermissionsAsync(Guid roleId, IReadOnlyCollection<Guid> permissionIds, CancellationToken ct = default);
}
