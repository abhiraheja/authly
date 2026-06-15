using Authly.Core.Interfaces;
using Authly.Modules.Audit;
using Authly.Modules.Auth;
using Authly.Modules.Common;

namespace Authly.Modules.TenantAdmins;

/// <inheritdoc />
public sealed class TenantAdminService : ITenantAdminService
{
    private readonly IAuthService _auth;
    private readonly IUserRepository _users;
    private readonly IAuditLogger _audit;

    public TenantAdminService(IAuthService auth, IUserRepository users, IAuditLogger audit)
    {
        _auth = auth;
        _users = users;
        _audit = audit;
    }

    public async Task<TenantAdminSignInResult> SignInAsync(Guid tenantId, string email, string password, RequestInfo info, CancellationToken ct = default)
    {
        var login = await _auth.AuthenticateAsync(tenantId, email, password, info, ct);
        if (!login.Succeeded || login.User is null)
            return new TenantAdminSignInResult(null, false);

        var user = login.User;
        var actor = new AuditContext(user.Id, "user", info.IpAddress, info.UserAgent);

        if (user.IsTenantAdmin)
        {
            await _audit.LogAsync("tenant_admin.login", actor, tenantId, "user", user.Id, ct: ct);
            return new TenantAdminSignInResult(user, false);
        }

        // First-admin bootstrap: only when the workspace has no admins yet.
        if (!await _users.AnyTenantAdminAsync(tenantId, ct))
        {
            user.IsTenantAdmin = true;
            user.UpdatedAt = DateTimeOffset.UtcNow;
            await _users.UpdateAsync(user, ct);
            await _audit.LogAsync("tenant_admin.bootstrapped", actor, tenantId, "user", user.Id, ct: ct);
            return new TenantAdminSignInResult(user, true);
        }

        // A non-admin user in a workspace that already has admins is denied the admin surface.
        await _audit.LogAsync("tenant_admin.login", actor, tenantId, "user", user.Id,
            result: "failure", metadata: new { reason = "not_admin" }, ct: ct);
        return new TenantAdminSignInResult(null, false);
    }
}
