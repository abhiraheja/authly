using Authly.Core.Entities;
using Authly.Modules.Common;

namespace Authly.Modules.TenantAdmins;

/// <summary>Outcome of a tenant-admin sign-in.</summary>
/// <param name="User">The authenticated admin, or null if credentials failed or access was denied.</param>
/// <param name="Bootstrapped">True when this login claimed the (previously admin-less) workspace.</param>
public sealed record TenantAdminSignInResult(User? User, bool Bootstrapped)
{
    public bool Succeeded => User is not null;
}

/// <summary>
/// Authenticates tenant administrators. Reuses end-user credential validation, then enforces the
/// tenant-admin flag. As a pre-RBAC bootstrap, the first successful login for a workspace that has
/// no admins yet claims admin (audited); afterwards only flagged users are admitted. Replaced by
/// the <c>tenant_admin</c> role in Phase 4.
/// </summary>
public interface ITenantAdminService
{
    Task<TenantAdminSignInResult> SignInAsync(Guid tenantId, string email, string password, RequestInfo info, CancellationToken ct = default);
}
