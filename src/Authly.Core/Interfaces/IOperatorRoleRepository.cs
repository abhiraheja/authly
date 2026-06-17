using Authly.Core.Entities;

namespace Authly.Core.Interfaces;

/// <summary>
/// Persistence for the operator-RBAC aggregate: <see cref="OperatorRole"/>, <see cref="OperatorPermission"/>,
/// and their mapping. All operations are organization-scoped (global tables, NO RLS — read before any
/// tenant is resolved). Implemented in Infrastructure. Mirrors <see cref="IRoleRepository"/>.
/// </summary>
public interface IOperatorRoleRepository
{
    // --- Roles ---
    Task<IReadOnlyList<OperatorRole>> ListRolesAsync(Guid organizationId, CancellationToken ct = default);
    Task<OperatorRole?> GetRoleAsync(Guid organizationId, Guid id, CancellationToken ct = default);
    Task<OperatorRole?> GetRoleByNameAsync(Guid organizationId, string name, CancellationToken ct = default);
    Task AddRoleAsync(OperatorRole role, CancellationToken ct = default);
    Task UpdateRoleAsync(OperatorRole role, CancellationToken ct = default);
    Task DeleteRoleAsync(OperatorRole role, CancellationToken ct = default);

    // --- Permissions ---
    Task<IReadOnlyList<OperatorPermission>> ListPermissionsAsync(Guid organizationId, CancellationToken ct = default);
    Task AddPermissionAsync(OperatorPermission permission, CancellationToken ct = default);

    // --- Role ↔ permission mapping ---
    Task<IReadOnlyList<Guid>> ListPermissionIdsForRoleAsync(Guid roleId, CancellationToken ct = default);
    /// <summary>Replaces the role's permission set with exactly the supplied permission ids.</summary>
    Task SetRolePermissionsAsync(Guid roleId, IReadOnlyCollection<Guid> permissionIds, CancellationToken ct = default);
}
