using Authly.Core.Interfaces;

namespace Authly.Web.Infrastructure;

/// <summary>
/// Resolves the current tenant for tenant-facing surfaces and records it in
/// <see cref="ITenantContext"/> (which the DB connection interceptor pushes into
/// <c>app.current_tenant</c> for the RLS backstop).
///
/// Resolution order: custom domain (Host) → a tenant; then, in non-production, an
/// <c>X-Tenant-Slug</c> header for testing. Platform surfaces (super admin, Hangfire,
/// static assets) are intentionally tenant-less. Tenant admin / hosted-login routing
/// is added in later phases.
/// </summary>
public sealed class TenantResolutionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IWebHostEnvironment _env;

    public TenantResolutionMiddleware(RequestDelegate next, IWebHostEnvironment env)
    {
        _next = next;
        _env = env;
    }

    public async Task InvokeAsync(HttpContext context, ITenantContext tenantContext, ITenantRepository tenants)
    {
        var path = context.Request.Path;
        if (!path.StartsWithSegments("/superadmin") && !path.StartsWithSegments("/hangfire"))
        {
            var host = context.Request.Host.Host;
            var tenant = await tenants.GetByCustomDomainOrNullAsync(host, context.RequestAborted);

            if (tenant is null && !_env.IsProduction()
                && context.Request.Headers.TryGetValue("X-Tenant-Slug", out var slug))
            {
                tenant = await tenants.GetBySlugAsync(slug.ToString(), context.RequestAborted);
            }

            if (tenant is not null)
                tenantContext.SetTenant(tenant.Id);
        }

        await _next(context);
    }
}
