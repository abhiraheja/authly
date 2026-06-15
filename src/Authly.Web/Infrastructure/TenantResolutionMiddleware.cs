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
    /// <summary>Dev-only cookie remembering the chosen tenant slug (non-production only).</summary>
    private const string DevTenantCookie = "authly.dev_tenant";

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

            // Non-production conveniences so a tenant surface can be exercised from a browser
            // without a custom domain: an X-Tenant-Slug header, a ?tenant= query (which is then
            // remembered in a cookie so follow-up navigations and email-link clicks resolve too).
            if (tenant is null && !_env.IsProduction())
            {
                string? slug = null;
                if (context.Request.Headers.TryGetValue("X-Tenant-Slug", out var header))
                    slug = header.ToString();
                else if (context.Request.Query.TryGetValue("tenant", out var query))
                    slug = query.ToString();
                else if (context.Request.Cookies.TryGetValue(DevTenantCookie, out var cookie))
                    slug = cookie;

                if (!string.IsNullOrWhiteSpace(slug))
                {
                    tenant = await tenants.GetBySlugAsync(slug, context.RequestAborted);
                    if (tenant is not null)
                        context.Response.Cookies.Append(DevTenantCookie, tenant.Slug,
                            new CookieOptions { HttpOnly = true, IsEssential = true, SameSite = SameSiteMode.Lax });
                }
            }

            if (tenant is not null)
                tenantContext.SetTenant(tenant.Id);
        }

        await _next(context);
    }
}
