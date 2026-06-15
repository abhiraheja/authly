using Authly.Modules.Security;

namespace Authly.Web.Infrastructure.Security;

/// <summary>
/// Optional IP allowlist for the platform super-admin surface. When <c>SUPERADMIN_IP_ALLOWLIST</c>
/// (comma-separated IPs/CIDRs) is configured, requests to <c>/superadmin</c> from any other IP get
/// 404 (not 403 — don't advertise the panel's existence). Empty config = no restriction.
/// </summary>
public sealed class SuperAdminIpAllowlistMiddleware
{
    private readonly RequestDelegate _next;
    private readonly string[] _allowed;

    public SuperAdminIpAllowlistMiddleware(RequestDelegate next, IConfiguration config)
    {
        _next = next;
        _allowed = (config["SUPERADMIN_IP_ALLOWLIST"] ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    public Task InvokeAsync(HttpContext context)
    {
        if (_allowed.Length > 0 && context.Request.Path.StartsWithSegments("/superadmin"))
        {
            var ip = context.Connection.RemoteIpAddress?.ToString();
            if (!BlockListPolicy.IsIpAllowed(ip, _allowed))
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return Task.CompletedTask;
            }
        }
        return _next(context);
    }
}
