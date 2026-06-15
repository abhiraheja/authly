using Authly.Core.Entities;
using Authly.Modules.Common;

namespace Authly.Modules.Account;

/// <summary>
/// The signed-in end-user's self-service operations for the portal (Phase 10): profile, password,
/// active sessions, and login history. Every method is scoped to (tenantId, userId) so a user can
/// only ever read or mutate their own data.
/// </summary>
public interface IAccountSelfService
{
    Task<User?> GetAsync(Guid tenantId, Guid userId, CancellationToken ct = default);

    /// <summary>Updates the user-owned profile fields. No-op signals via return (false = user missing).</summary>
    Task<bool> UpdateProfileAsync(Guid tenantId, Guid userId, ProfileUpdate update, AuditContext actor, CancellationToken ct = default);

    /// <summary>
    /// Changes the password. Verifies <paramref name="currentPassword"/> against the stored hash
    /// (skipped for social-only accounts that are setting a password for the first time), then
    /// revokes the user's other sessions, keeping <paramref name="keepSessionId"/>.
    /// </summary>
    Task<PasswordChangeResult> ChangePasswordAsync(Guid tenantId, Guid userId, string? currentPassword,
        string newPassword, Guid keepSessionId, AuditContext actor, CancellationToken ct = default);

    Task<IReadOnlyList<Session>> ListSessionsAsync(Guid tenantId, Guid userId, CancellationToken ct = default);

    /// <summary>Revokes one of the user's own sessions. Ignores sessions that don't belong to the user.</summary>
    Task RevokeSessionAsync(Guid tenantId, Guid userId, Guid sessionId, AuditContext actor, CancellationToken ct = default);

    /// <summary>Revokes all of the user's sessions except <paramref name="keepSessionId"/>; returns the count revoked.</summary>
    Task<int> RevokeOtherSessionsAsync(Guid tenantId, Guid userId, Guid keepSessionId, AuditContext actor, CancellationToken ct = default);

    Task<IReadOnlyList<LoginHistory>> ListLoginHistoryAsync(Guid tenantId, Guid userId, int limit = 50, CancellationToken ct = default);
}
