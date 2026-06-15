using System.Security.Claims;
using Authly.Core.Interfaces;
using Authly.Modules.Common;
using Authly.Web.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Authly.Web.Areas.TenantAdmin.Controllers;

/// <summary>
/// Base for authenticated tenant-admin controllers. Enforces the tenant-admin policy and a
/// cross-tenant guard: the signed-in admin's tenant (from the cookie) must match the tenant
/// resolved for the current host. All queries are scoped to that resolved tenant (also the RLS
/// boundary).
/// </summary>
[Area("TenantAdmin")]
[Authorize(Policy = AuthPolicies.TenantAdmin)]
public abstract class TenantAdminControllerBase : Controller
{
    private readonly ITenantContext _tenant;

    protected TenantAdminControllerBase(ITenantContext tenant) => _tenant = tenant;

    protected Guid TenantId => _tenant.TenantId
        ?? throw new InvalidOperationException("No tenant resolved for this request.");

    protected Guid CurrentUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    protected AuditContext CurrentAudit() => new(
        CurrentUserId, "user",
        HttpContext.Connection.RemoteIpAddress?.ToString(),
        Request.Headers.UserAgent.ToString() is { Length: > 0 } ua ? ua : null);

    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        // Resolved tenant (host/dev cookie) must match the admin cookie's tenant.
        var cookieTenant = User.FindFirstValue(TenantAdminClaims.TenantId);
        if (!_tenant.HasTenant || cookieTenant != _tenant.TenantId.ToString())
        {
            await HttpContext.SignOutAsync(AuthSchemes.TenantAdmin);
            context.Result = RedirectToAction("Login", "Account", new { area = "TenantAdmin" });
            return;
        }

        await next();
    }
}
