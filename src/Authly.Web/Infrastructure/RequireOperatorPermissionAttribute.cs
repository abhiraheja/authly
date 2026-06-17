using Authly.Core.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Authly.Web.Infrastructure;

/// <summary>Keys for per-request console-authorization state cached by the tenant-admin guard.</summary>
public static class ConsoleItems
{
    /// <summary>The operator's effective permission set (<c>resource.action</c>) for the active org+project.</summary>
    public const string Permissions = "authly:operator_permissions";
}

/// <summary>
/// Gates a tenant-admin (console) action on an operator permission (e.g. <c>client.manage</c>),
/// evaluated against the set cached by <c>TenantAdminControllerBase</c> using the same
/// <see cref="PermissionEvaluator"/> wildcard semantics as end-user RBAC. No extra DB hit.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
public sealed class RequireOperatorPermissionAttribute : Attribute, IActionFilter
{
    public string Permission { get; }

    public RequireOperatorPermissionAttribute(string permission) => Permission = permission;

    public void OnActionExecuting(ActionExecutingContext context)
    {
        var granted = context.HttpContext.Items[ConsoleItems.Permissions] as IReadOnlyList<string>;
        if (granted is null || !PermissionEvaluator.Satisfies(granted, Permission))
            context.Result = new ForbidResult(AuthSchemes.TenantAdmin);
    }

    public void OnActionExecuted(ActionExecutedContext context) { }
}
