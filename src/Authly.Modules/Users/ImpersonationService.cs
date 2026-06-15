using Authly.Core.Entities;
using Authly.Core.Enums;
using Authly.Core.Interfaces;
using Authly.Modules.Audit;
using Authly.Modules.Auth;
using Authly.Modules.Common;

namespace Authly.Modules.Users;

/// <summary>Outcome of starting impersonation: the new session + the impersonated user.</summary>
public sealed record ImpersonationResult(Session Session, User User);

/// <summary>Raised when an admin tries to impersonate a missing or non-active user.</summary>
public sealed class ImpersonationNotAllowedException(string reason) : Exception(reason);

/// <summary>
/// Admin "log in as user". Mints a real (audited) session for a target user in the admin's own
/// tenant without a password. Heavily logged on both start and stop — impersonation is a sensitive
/// support action. Tenant-scoped: the target must belong to <paramref name="tenantId"/>.
/// </summary>
public interface IImpersonationService
{
    Task<ImpersonationResult> StartAsync(Guid tenantId, Guid adminUserId, Guid targetUserId,
        RequestInfo info, AuditContext actor, CancellationToken ct = default);

    Task StopAsync(Guid tenantId, Guid sessionId, AuditContext actor, CancellationToken ct = default);
}

/// <inheritdoc />
public sealed class ImpersonationService : IImpersonationService
{
    private readonly IUserRepository _users;
    private readonly IAuthService _auth;
    private readonly IAuditLogger _audit;

    public ImpersonationService(IUserRepository users, IAuthService auth, IAuditLogger audit)
    {
        _users = users;
        _auth = auth;
        _audit = audit;
    }

    public async Task<ImpersonationResult> StartAsync(Guid tenantId, Guid adminUserId, Guid targetUserId,
        RequestInfo info, AuditContext actor, CancellationToken ct = default)
    {
        if (adminUserId == targetUserId)
            throw new ImpersonationNotAllowedException("You can't impersonate yourself.");

        var target = await _users.GetByIdAsync(tenantId, targetUserId, ct)
            ?? throw new ImpersonationNotAllowedException("User not found.");
        if (target.Status != UserStatus.Active)
            throw new ImpersonationNotAllowedException("Only active users can be impersonated.");

        var session = await _auth.StartSessionAsync(target, "impersonation", info, ct);

        await _audit.LogAsync("user.impersonation_started", actor, tenantId, "user", target.Id,
            metadata: new { admin_user_id = adminUserId, session_id = session.Id }, ct: ct);

        return new ImpersonationResult(session, target);
    }

    public async Task StopAsync(Guid tenantId, Guid sessionId, AuditContext actor, CancellationToken ct = default)
    {
        await _auth.RevokeSessionAsync(sessionId, ct);
        await _audit.LogAsync("user.impersonation_stopped", actor, tenantId,
            metadata: new { session_id = sessionId }, ct: ct);
    }
}
