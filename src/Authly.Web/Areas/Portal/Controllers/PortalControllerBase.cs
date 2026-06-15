using System.Security.Claims;
using Authly.Modules.Common;
using Authly.Web.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Authly.Web.Areas.Portal.Controllers;

/// <summary>
/// Base for the end-user self-service portal. Authenticated by the end-user cookie and scoped to
/// the signed-in user. Tenant id and session id come from the cookie principal (set at login from
/// the resolved tenant), so every action operates only on the current user's own data.
/// </summary>
[Area("Portal")]
[Authorize(Policy = AuthPolicies.User)]
public abstract class PortalControllerBase : Controller
{
    protected Guid TenantId => Guid.Parse(User.FindFirstValue(UserClaims.TenantId)!);
    protected Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    protected string UserEmail => User.FindFirstValue(ClaimTypes.Name) ?? "";

    /// <summary>The session behind the current cookie (preserved across password change / revoke-others).</summary>
    protected Guid CurrentSessionId =>
        Guid.TryParse(User.FindFirstValue(UserClaims.SessionId), out var id) ? id : Guid.Empty;

    protected AuditContext CurrentAudit() => new(
        UserId, "user",
        HttpContext.Connection.RemoteIpAddress?.ToString(),
        Request.Headers.UserAgent.ToString() is { Length: > 0 } ua ? ua : null);
}
