using Authly.Core.Authorization;
using Authly.Core.Interfaces;
using Authly.Modules.Audit;
using Authly.Modules.Auth;
using Authly.Modules.Authorization;
using Authly.Modules.Common;

namespace Authly.Modules.TenantAdmins;

/// <inheritdoc />
public sealed class TenantAdminService : ITenantAdminService
{
    private readonly IAuthService _auth;
    private readonly IUserRepository _users;
    private readonly IRbacService _rbac;
    private readonly IRoleRepository _roles;
    private readonly IAuditLogger _audit;

    public TenantAdminService(IAuthService auth, IUserRepository users, IRbacService rbac, IRoleRepository roles, IAuditLogger audit)
    {
        _auth = auth;
        _users = users;
        _rbac = rbac;
        _roles = roles;
        _audit = audit;
    }

    public async Task<TenantAdminSignInResult> SignInAsync(Guid tenantId, string email, string password, RequestInfo info, CancellationToken ct = default)
    {
        var login = await _auth.AuthenticateAsync(tenantId, email, password, info, ct);
        if (!login.Succeeded || login.User is null)
            return new TenantAdminSignInResult(null, false);

        var user = login.User;
        var actor = new AuditContext(user.Id, "user", info.IpAddress, info.UserAgent);

        // Sign-in happens in the tenant's resolved context (RLS permits the writes), so this is the
        // natural place to idempotently seed the tenant's system roles + baseline permissions.
        await _rbac.EnsureSystemRolesAsync(tenantId, ct);

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

            // Grant the seeded tenant_admin role so the bootstrap admin's tokens carry full permissions.
            if (await _roles.GetRoleByNameAsync(tenantId, SystemRbac.TenantAdmin, ct) is { } adminRole)
                await _rbac.AssignRoleAsync(tenantId, user.Id, adminRole.Id, actor, ct);

            await _audit.LogAsync("tenant_admin.bootstrapped", actor, tenantId, "user", user.Id, ct: ct);
            return new TenantAdminSignInResult(user, true);
        }

        // A non-admin user in a workspace that already has admins is denied the admin surface.
        await _audit.LogAsync("tenant_admin.login", actor, tenantId, "user", user.Id,
            result: "failure", metadata: new { reason = "not_admin" }, ct: ct);
        return new TenantAdminSignInResult(null, false);
    }
}
