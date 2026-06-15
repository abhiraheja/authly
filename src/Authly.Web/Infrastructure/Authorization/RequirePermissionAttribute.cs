using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Authly.Web.Infrastructure.Authorization;

/// <summary>
/// Authorization filter that requires the caller's token to carry a specific
/// <c>resource.action</c> permission (in a <see cref="TokenClaims.Permissions"/> claim). Used to
/// guard Management API endpoints (Phase 5). An unauthenticated caller gets 401; an authenticated
/// caller lacking the permission gets 403.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class RequirePermissionAttribute : Attribute, IAuthorizationFilter
{
    private readonly string _permission;

    /// <param name="permission">The required permission as <c>resource.action</c> (e.g. <c>user.delete</c>).</param>
    public RequirePermissionAttribute(string permission) => _permission = permission;

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var user = context.HttpContext.User;
        if (user?.Identity?.IsAuthenticated != true)
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        if (!PermissionChecker.HasPermission(user, _permission))
            context.Result = new ForbidResult();
    }
}
