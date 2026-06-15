using System.Security.Claims;
using Authly.Modules.Common;
using Authly.Web.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Authly.Web.Areas.SuperAdmin.Controllers;

/// <summary>
/// Base for all authenticated super-admin pages: applies the area, the super-admin
/// authorization policy, and forces a password change before anything else can be used.
/// </summary>
[Area("SuperAdmin")]
[Authorize(Policy = AuthPolicies.SuperAdmin)]
public abstract class SuperAdminControllerBase : Controller
{
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        var mustChange = User.FindFirst(SuperAdminClaims.MustChangePassword)?.Value == "true";
        if (mustChange)
        {
            context.Result = RedirectToAction("ChangePassword", "Account",
                new { area = "SuperAdmin" });
        }

        base.OnActionExecuting(context);
    }

    /// <summary>Builds an audit context for the current super admin and request.</summary>
    protected AuditContext CurrentAudit()
    {
        Guid? actorId = Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
        return new AuditContext(
            actorId,
            "super_admin",
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            Request.Headers.UserAgent.ToString());
    }
}
