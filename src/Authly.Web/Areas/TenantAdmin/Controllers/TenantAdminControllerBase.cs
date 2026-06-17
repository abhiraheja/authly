using System.Security.Claims;
using Authly.Core.Interfaces;
using Authly.Modules.Common;
using Authly.Modules.Operators;
using Authly.Web.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;

namespace Authly.Web.Areas.TenantAdmin.Controllers;

/// <summary>
/// Base for authenticated tenant-admin (console operator) controllers. Enforces the tenant-admin
/// policy plus a two-part authorization (doc 06 §5): the signed-in account must have an Active
/// membership in the active org AND the active project must belong to that org — resolved via
/// <see cref="IConsoleAccessService"/>. The resulting operator permission set is cached for
/// per-action <see cref="RequireOperatorPermissionAttribute"/> checks. Failure → sign out + login.
/// </summary>
[Area("TenantAdmin")]
[Authorize(Policy = AuthPolicies.TenantAdmin)]
public abstract class TenantAdminControllerBase : Controller
{
    private readonly ITenantContext _tenant;

    protected TenantAdminControllerBase(ITenantContext tenant) => _tenant = tenant;

    protected Guid TenantId => _tenant.TenantId
        ?? throw new InvalidOperationException("No tenant resolved for this request.");

    /// <summary>The signed-in console account id.</summary>
    protected Guid CurrentAccountId =>
        Guid.Parse(User.FindFirstValue(TenantAdminClaims.AccountId) ?? User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>Back-compat alias — the acting principal is now the console Account.</summary>
    protected Guid CurrentUserId => CurrentAccountId;

    /// <summary>The active organization.</summary>
    protected Guid OrgId => Guid.Parse(User.FindFirstValue(TenantAdminClaims.OrgId)!);

    protected AuditContext CurrentAudit() => new(
        CurrentAccountId, "account",
        HttpContext.Connection.RemoteIpAddress?.ToString(),
        Request.Headers.UserAgent.ToString() is { Length: > 0 } ua ? ua : null);

    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var http = context.HttpContext;

        var accountIdRaw = User.FindFirstValue(TenantAdminClaims.AccountId) ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        var orgIdRaw = User.FindFirstValue(TenantAdminClaims.OrgId);
        var projectIdRaw = User.FindFirstValue(TenantAdminClaims.TenantId);

        if (!Guid.TryParse(accountIdRaw, out var accountId)
            || !Guid.TryParse(orgIdRaw, out var orgId)
            || !Guid.TryParse(projectIdRaw, out var projectId)
            || !_tenant.HasTenant || _tenant.TenantId != projectId)
        {
            await Deny(http);
            context.Result = LoginRedirect();
            return;
        }

        var access = await http.RequestServices
            .GetRequiredService<IConsoleAccessService>()
            .ResolveAsync(accountId, orgId, projectId, http.RequestAborted);

        if (access is null)
        {
            await Deny(http);
            context.Result = LoginRedirect();
            return;
        }

        // Cache the effective operator permissions for [RequireOperatorPermission] on this request.
        http.Items[ConsoleItems.Permissions] = access.Permissions;

        await next();
    }

    private static async Task Deny(HttpContext http) => await http.SignOutAsync(AuthSchemes.TenantAdmin);

    private IActionResult LoginRedirect() => RedirectToAction("Login", "Account", new { area = "TenantAdmin" });
}
