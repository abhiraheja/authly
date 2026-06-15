using Authly.Core.Entities;

namespace Authly.Core.Interfaces;

/// <summary>
/// Persistence for user↔role assignments and the resolution of a user's effective roles and
/// permissions (used for token claim assembly). Tenant-scoped. Implemented in Infrastructure.
/// </summary>
public interface IUserRoleRepository
{
    Task AssignAsync(UserRole assignment, CancellationToken ct = default);
    Task RemoveAsync(Guid tenantId, Guid userId, Guid roleId, CancellationToken ct = default);

    Task<IReadOnlyList<Role>> ListRolesForUserAsync(Guid tenantId, Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<Guid>> ListUserIdsForRoleAsync(Guid tenantId, Guid roleId, CancellationToken ct = default);

    /// <summary>The user's role names (e.g. <c>tenant_admin</c>), for the <c>roles</c> token claim.</summary>
    Task<IReadOnlyList<string>> GetRoleNamesAsync(Guid tenantId, Guid userId, CancellationToken ct = default);

    /// <summary>The user's flattened, distinct permissions as <c>resource.action</c>, for the <c>permissions</c> claim.</summary>
    Task<IReadOnlyList<string>> GetPermissionNamesAsync(Guid tenantId, Guid userId, CancellationToken ct = default);
}
