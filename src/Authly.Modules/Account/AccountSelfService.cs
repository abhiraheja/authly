using Authly.Core.Entities;
using Authly.Core.Interfaces;
using Authly.Modules.Audit;
using Authly.Modules.Common;

namespace Authly.Modules.Account;

/// <inheritdoc />
public sealed class AccountSelfService : IAccountSelfService
{
    private readonly IUserRepository _users;
    private readonly ISessionRepository _sessions;
    private readonly ILoginHistoryRepository _loginHistory;
    private readonly IPasswordHasher _hasher;
    private readonly IAuditLogger _audit;

    public AccountSelfService(IUserRepository users, ISessionRepository sessions,
        ILoginHistoryRepository loginHistory, IPasswordHasher hasher, IAuditLogger audit)
    {
        _users = users;
        _sessions = sessions;
        _loginHistory = loginHistory;
        _hasher = hasher;
        _audit = audit;
    }

    public Task<User?> GetAsync(Guid tenantId, Guid userId, CancellationToken ct = default)
        => _users.GetByIdAsync(tenantId, userId, ct);

    public async Task<bool> UpdateProfileAsync(Guid tenantId, Guid userId, ProfileUpdate update, AuditContext actor, CancellationToken ct = default)
    {
        var user = await _users.GetByIdAsync(tenantId, userId, ct);
        if (user is null) return false;

        user.FirstName = Trimmed(update.FirstName);
        user.LastName = Trimmed(update.LastName);
        user.Timezone = string.IsNullOrWhiteSpace(update.Timezone) ? "UTC" : update.Timezone.Trim();
        user.Locale = string.IsNullOrWhiteSpace(update.Locale) ? "en" : update.Locale.Trim();
        user.UpdatedAt = DateTimeOffset.UtcNow;
        await _users.UpdateAsync(user, ct);

        await _audit.LogAsync("user.profile_updated", actor, tenantId: tenantId,
            resourceType: "user", resourceId: userId, ct: ct);
        return true;
    }

    public async Task<PasswordChangeResult> ChangePasswordAsync(Guid tenantId, Guid userId, string? currentPassword,
        string newPassword, Guid keepSessionId, AuditContext actor, CancellationToken ct = default)
    {
        var user = await _users.GetByIdAsync(tenantId, userId, ct);
        if (user is null) return PasswordChangeResult.UserNotFound;

        // A social-only account (no password yet) may set one without proving a current password.
        if (user.PasswordHash is not null)
        {
            if (string.IsNullOrEmpty(currentPassword) || !_hasher.Verify(user.PasswordHash, currentPassword))
                return PasswordChangeResult.WrongCurrentPassword;
        }

        user.PasswordHash = _hasher.Hash(newPassword);
        user.UpdatedAt = DateTimeOffset.UtcNow;
        await _users.UpdateAsync(user, ct);

        // Changing a password evicts the user's other sessions (keep the current one signed in).
        await RevokeOtherSessionsInternalAsync(tenantId, userId, keepSessionId, ct);

        await _audit.LogAsync("user.password_changed", actor, tenantId: tenantId,
            resourceType: "user", resourceId: userId, ct: ct);
        return PasswordChangeResult.Success;
    }

    public Task<IReadOnlyList<Session>> ListSessionsAsync(Guid tenantId, Guid userId, CancellationToken ct = default)
        => _sessions.ListActiveForUserAsync(tenantId, userId, ct);

    public async Task RevokeSessionAsync(Guid tenantId, Guid userId, Guid sessionId, AuditContext actor, CancellationToken ct = default)
    {
        var session = await _sessions.GetByIdAsync(sessionId, ct);
        // Ownership guard: never touch a session that isn't this user's in this tenant.
        if (session is null || session.UserId != userId || session.TenantId != tenantId || session.Revoked) return;

        session.Revoked = true;
        await _sessions.UpdateAsync(session, ct);

        await _audit.LogAsync("session.revoked", actor, tenantId: tenantId,
            resourceType: "session", resourceId: sessionId, ct: ct);
    }

    public async Task<int> RevokeOtherSessionsAsync(Guid tenantId, Guid userId, Guid keepSessionId, AuditContext actor, CancellationToken ct = default)
    {
        var count = await RevokeOtherSessionsInternalAsync(tenantId, userId, keepSessionId, ct);
        if (count > 0)
            await _audit.LogAsync("session.revoked_others", actor, tenantId: tenantId,
                resourceType: "user", resourceId: userId, metadata: new { revoked = count }, ct: ct);
        return count;
    }

    public Task<IReadOnlyList<LoginHistory>> ListLoginHistoryAsync(Guid tenantId, Guid userId, int limit = 50, CancellationToken ct = default)
        => _loginHistory.ListForUserAsync(tenantId, userId, limit, ct);

    private async Task<int> RevokeOtherSessionsInternalAsync(Guid tenantId, Guid userId, Guid keepSessionId, CancellationToken ct)
    {
        var active = await _sessions.ListActiveForUserAsync(tenantId, userId, ct);
        var count = 0;
        foreach (var s in active)
        {
            if (s.Id == keepSessionId) continue;
            s.Revoked = true;
            await _sessions.UpdateAsync(s, ct);
            count++;
        }
        return count;
    }

    private static string? Trimmed(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
