using Authly.Core.Common;
using Authly.Core.Entities;

namespace Authly.Core.Interfaces;

/// <summary>
/// Persistence for <see cref="User"/>. All lookups are tenant-scoped because email is
/// unique only per tenant — callers must always pass the resolved tenant id.
/// Implemented in Infrastructure.
/// </summary>
public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task<User?> GetByEmailAsync(Guid tenantId, string email, CancellationToken ct = default);

    /// <summary>Resolves the user whose VERIFIED phone matches (normalized form), for phone+password
    /// login. Only verified phones are matchable so an unverified number can't be used to sign in.</summary>
    Task<User?> GetByVerifiedPhoneAsync(Guid tenantId, string normalizedPhone, CancellationToken ct = default);

    /// <summary>Resolves the user holding this phone (normalized), regardless of verification — used by
    /// the OTP login path, where a successful code both proves ownership and verifies the number.
    /// Phone is unique per tenant, so this returns at most one user.</summary>
    Task<User?> GetByPhoneAsync(Guid tenantId, string normalizedPhone, CancellationToken ct = default);

    /// <summary>All users in a tenant (newest first), for tenant-admin management screens.</summary>
    Task<IReadOnlyList<User>> ListByTenantAsync(Guid tenantId, CancellationToken ct = default);

    /// <summary>Paginated users in a tenant (newest first), optionally filtered by email substring.</summary>
    Task<PagedResult<User>> ListPagedAsync(Guid tenantId, Pagination page, string? emailContains = null, CancellationToken ct = default);

    /// <summary>Removes a user permanently (hard delete). Status-based soft delete is preferred for most flows.</summary>
    Task DeleteAsync(User user, CancellationToken ct = default);
    Task<bool> EmailExistsAsync(Guid tenantId, string email, CancellationToken ct = default);
    Task AddAsync(User user, CancellationToken ct = default);
    Task UpdateAsync(User user, CancellationToken ct = default);
}
