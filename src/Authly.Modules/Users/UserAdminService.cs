using Authly.Core.Common;
using Authly.Core.Entities;
using Authly.Core.Enums;
using Authly.Core.Interfaces;
using Authly.Modules.Audit;
using Authly.Modules.Auth;
using Authly.Modules.Common;

namespace Authly.Modules.Users;

/// <inheritdoc />
public sealed class UserAdminService : IUserAdminService
{
    private readonly IUserRepository _users;
    private readonly ISessionRepository _sessions;
    private readonly IPasswordHasher _hasher;
    private readonly IAuthService _auth;
    private readonly IAuditLogger _audit;

    public UserAdminService(
        IUserRepository users,
        ISessionRepository sessions,
        IPasswordHasher hasher,
        IAuthService auth,
        IAuditLogger audit)
    {
        _users = users;
        _sessions = sessions;
        _hasher = hasher;
        _auth = auth;
        _audit = audit;
    }

    public Task<PagedResult<User>> ListAsync(Guid tenantId, Pagination page, string? emailContains, CancellationToken ct = default)
        => _users.ListPagedAsync(tenantId, page, emailContains, ct);

    public Task<User?> GetAsync(Guid tenantId, Guid id, CancellationToken ct = default)
        => _users.GetByIdAsync(tenantId, id, ct);

    public async Task<User> CreateAsync(Guid tenantId, CreateUserRequest request, AuditContext actor, CancellationToken ct = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        if (await _users.EmailExistsAsync(tenantId, email, ct))
            throw new UserEmailAlreadyExistsException(email);

        var now = DateTimeOffset.UtcNow;
        var user = new User
        {
            TenantId = tenantId,
            Email = email,
            EmailVerified = request.EmailVerified,
            PasswordHash = string.IsNullOrEmpty(request.Password) ? null : _hasher.Hash(request.Password),
            FirstName = request.FirstName,
            LastName = request.LastName,
            Status = UserStatus.Active,
            CreatedAt = now,
            UpdatedAt = now
        };
        await _users.AddAsync(user, ct);

        // Non-sensitive user fields ride along so webhook subscribers can provision without a callback.
        // Imports/migrations may suppress the webhook (they provision downstream state themselves).
        await _audit.LogAsync("user.created", actor, tenantId, "user", user.Id,
            metadata: new { email, firstName = user.FirstName, lastName = user.LastName, phone = user.Phone, avatarUrl = user.AvatarUrl },
            publishEvent: !request.SuppressEvents, ct: ct);
        return user;
    }

    public async Task<User> UpdateAsync(Guid tenantId, Guid id, UpdateUserRequest request, AuditContext actor, CancellationToken ct = default)
    {
        var user = await _users.GetByIdAsync(tenantId, id, ct) ?? throw new UserNotFoundException(id);

        if (request.FirstName is not null) user.FirstName = request.FirstName;
        if (request.LastName is not null) user.LastName = request.LastName;
        if (request.Phone is not null) user.Phone = request.Phone;
        if (request.Timezone is not null) user.Timezone = request.Timezone;
        if (request.Locale is not null) user.Locale = request.Locale;
        user.UpdatedAt = DateTimeOffset.UtcNow;

        await _users.UpdateAsync(user, ct);
        // Non-sensitive profile fields ride along so webhook subscribers can sync without a callback.
        await _audit.LogAsync("user.updated", actor, tenantId, "user", user.Id,
            metadata: new { firstName = user.FirstName, lastName = user.LastName, phone = user.Phone },
            ct: ct);
        return user;
    }

    public Task SuspendAsync(Guid tenantId, Guid id, AuditContext actor, CancellationToken ct = default)
        => SetStatusAsync(tenantId, id, UserStatus.Suspended, "user.suspended", actor, revokeSessions: true, ct);

    public Task ReactivateAsync(Guid tenantId, Guid id, AuditContext actor, CancellationToken ct = default)
        => SetStatusAsync(tenantId, id, UserStatus.Active, "user.reactivated", actor, revokeSessions: false, ct);

    public async Task DeleteAsync(Guid tenantId, Guid id, AuditContext actor, CancellationToken ct = default)
    {
        // Soft delete: mark deleted + revoke sessions. Rows are retained for audit/forensics;
        // hard erasure is handled by the GDPR/DPDP flow in Phase 13.
        var user = await _users.GetByIdAsync(tenantId, id, ct) ?? throw new UserNotFoundException(id);
        user.Status = UserStatus.Deleted;
        user.UpdatedAt = DateTimeOffset.UtcNow;
        await _users.UpdateAsync(user, ct);
        await _sessions.RevokeAllForUserAsync(tenantId, id, ct);
        await _audit.LogAsync("user.deleted", actor, tenantId, "user", user.Id, ct: ct);
    }

    public async Task ForcePasswordResetAsync(Guid tenantId, Guid id, AuditContext actor, CancellationToken ct = default)
    {
        var user = await _users.GetByIdAsync(tenantId, id, ct) ?? throw new UserNotFoundException(id);

        await _sessions.RevokeAllForUserAsync(tenantId, id, ct);
        // Reuse the end-user reset flow: issues a single-use token + queues the email.
        await _auth.RequestPasswordResetAsync(tenantId, user.Email, RequestInfo.Unknown, ct);
        await _audit.LogAsync("user.force_password_reset", actor, tenantId, "user", user.Id, ct: ct);
    }

    public async Task<IReadOnlyList<Session>> ListSessionsAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        _ = await _users.GetByIdAsync(tenantId, id, ct) ?? throw new UserNotFoundException(id);
        return await _sessions.ListActiveForUserAsync(tenantId, id, ct);
    }

    public async Task<int> RevokeAllSessionsAsync(Guid tenantId, Guid id, AuditContext actor, CancellationToken ct = default)
    {
        _ = await _users.GetByIdAsync(tenantId, id, ct) ?? throw new UserNotFoundException(id);
        var revoked = await _sessions.RevokeAllForUserAsync(tenantId, id, ct);
        await _audit.LogAsync("user.sessions_revoked", actor, tenantId, "user", id, metadata: new { revoked }, ct: ct);
        return revoked;
    }

    private async Task SetStatusAsync(Guid tenantId, Guid id, UserStatus status, string @event, AuditContext actor, bool revokeSessions, CancellationToken ct)
    {
        var user = await _users.GetByIdAsync(tenantId, id, ct) ?? throw new UserNotFoundException(id);
        user.Status = status;
        user.UpdatedAt = DateTimeOffset.UtcNow;
        await _users.UpdateAsync(user, ct);
        if (revokeSessions) await _sessions.RevokeAllForUserAsync(tenantId, id, ct);
        await _audit.LogAsync(@event, actor, tenantId, "user", user.Id, ct: ct);
    }
}
