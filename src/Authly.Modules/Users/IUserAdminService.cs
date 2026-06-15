using Authly.Core.Common;
using Authly.Core.Entities;
using Authly.Modules.Common;

namespace Authly.Modules.Users;

/// <summary>
/// Administrative user management for the Management API: list/get/create/update, lifecycle
/// (suspend/reactivate/delete), forced password reset, and session inspection/revocation. All
/// operations are tenant-scoped.
/// </summary>
public interface IUserAdminService
{
    Task<PagedResult<User>> ListAsync(Guid tenantId, Pagination page, string? emailContains, CancellationToken ct = default);
    Task<User?> GetAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task<User> CreateAsync(Guid tenantId, CreateUserRequest request, AuditContext actor, CancellationToken ct = default);
    Task<User> UpdateAsync(Guid tenantId, Guid id, UpdateUserRequest request, AuditContext actor, CancellationToken ct = default);

    Task SuspendAsync(Guid tenantId, Guid id, AuditContext actor, CancellationToken ct = default);
    Task ReactivateAsync(Guid tenantId, Guid id, AuditContext actor, CancellationToken ct = default);
    Task DeleteAsync(Guid tenantId, Guid id, AuditContext actor, CancellationToken ct = default);

    /// <summary>Revokes all sessions and issues a password-reset email so the user must set a new password.</summary>
    Task ForcePasswordResetAsync(Guid tenantId, Guid id, AuditContext actor, CancellationToken ct = default);

    Task<IReadOnlyList<Session>> ListSessionsAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task<int> RevokeAllSessionsAsync(Guid tenantId, Guid id, AuditContext actor, CancellationToken ct = default);
}
