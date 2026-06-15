using System.Security.Claims;
using Authly.Core.Interfaces;
using Authly.Modules.Common;
using Authly.Web.Infrastructure;
using Authly.Web.Infrastructure.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Authly.Web.Controllers.Api;

/// <summary>
/// Base for all <c>/api/v1</c> controllers. Authenticated by the <see cref="AuthSchemes.Api"/>
/// policy scheme (X-API-Key or Bearer), wrapped by the error-envelope filter, and tenant-scoped
/// from the authenticated principal's <c>tenant_id</c> claim (which also drives the RLS backstop).
/// </summary>
[ApiController]
[Authorize(AuthenticationSchemes = AuthSchemes.Api)]
[ServiceFilter(typeof(ApiExceptionFilter))]
[Produces("application/json")]
public abstract class ApiControllerBase : ControllerBase, IActionFilter
{
    private readonly ITenantContext _tenant;

    protected ApiControllerBase(ITenantContext tenant) => _tenant = tenant;

    protected Guid TenantId => _tenant.TenantId
        ?? throw new InvalidOperationException("No tenant bound for this API request.");

    protected AuditContext ApiAudit()
    {
        var actorId = Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : (Guid?)null;
        var actorType = string.Equals(User.Identity?.AuthenticationType, AuthSchemes.ApiKey, StringComparison.Ordinal)
            ? "api_key" : "service";
        return new AuditContext(actorId, actorType,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            Request.Headers.UserAgent.ToString() is { Length: > 0 } ua ? ua : null);
    }

    /// <summary>Binds the request's tenant from the token's <c>tenant_id</c> claim before the action runs.</summary>
    public void OnActionExecuting(ActionExecutingContext context)
    {
        var claim = User.FindFirstValue(TokenClaims.TenantId);
        if (!Guid.TryParse(claim, out var tenantId))
        {
            context.Result = new ObjectResult(ApiErrorEnvelope.Of("no_tenant", "The credential is not scoped to a tenant."))
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
            return;
        }

        if (!_tenant.HasTenant) _tenant.SetTenant(tenantId);
    }

    public void OnActionExecuted(ActionExecutedContext context) { }

    protected ObjectResult ApiError(int status, string code, string message)
        => new(ApiErrorEnvelope.Of(code, message)) { StatusCode = status };

    protected ObjectResult NotFoundError(string message = "Resource not found.")
        => ApiError(StatusCodes.Status404NotFound, "not_found", message);
}
