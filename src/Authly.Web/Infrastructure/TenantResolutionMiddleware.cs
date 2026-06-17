using Authly.Core.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.WebUtilities;

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
        // Platform surfaces are tenant-less; the Management API binds its tenant from the token,
        // not the host, so it is excluded from host/dev-cookie resolution here.
        if (!path.StartsWithSegments("/superadmin")
            && !path.StartsWithSegments("/hangfire")
            && !path.StartsWithSegments("/signup")
            && !path.StartsWithSegments("/invite")
            && !path.StartsWithSegments("/api"))
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
                // On the hosted login (and other interstitials), the OAuth request — including its
                // tenant — is carried in the ReturnUrl, not as a top-level query param. Read it from
                // there so a directly-opened login link (or a cleared cookie) still resolves.
                else if (TryGetTenantFromReturnUrl(context.Request, out var nested))
                    slug = nested;
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

            // Self-serve workspaces have no custom domain, so host resolution finds nothing. For the
            // tenant-admin surface, fall back to the signed-in admin's own cookie claim so their
            // workspace still resolves. TenantAdminControllerBase re-checks cookie == resolved tenant,
            // so this can only ever resolve the admin's own tenant.
            if (!tenantContext.HasTenant && path.StartsWithSegments("/tenantadmin"))
            {
                var auth = await context.AuthenticateAsync(AuthSchemes.TenantAdmin);
                if (Guid.TryParse(auth.Principal?.FindFirst(TenantAdminClaims.TenantId)?.Value, out var adminTenant))
                    tenantContext.SetTenant(adminTenant);
            }
        }

        await _next(context);
    }

    /// <summary>
    /// Extracts a <c>tenant</c> slug nested inside a <c>ReturnUrl</c> query parameter (e.g. the
    /// hosted login's <c>ReturnUrl=/connect/authorize?...&amp;tenant=aura</c>). Dev convenience only.
    /// </summary>
    private static bool TryGetTenantFromReturnUrl(HttpRequest request, out string? slug)
    {
        slug = null;
        if (!request.Query.TryGetValue("ReturnUrl", out var returnUrl))
            return false;

        var value = returnUrl.ToString();
        var queryStart = value.IndexOf('?');
        if (queryStart < 0)
            return false;

        var parsed = QueryHelpers.ParseQuery(value[queryStart..]);
        if (parsed.TryGetValue("tenant", out var t) && !string.IsNullOrWhiteSpace(t))
        {
            slug = t.ToString();
            return true;
        }
        return false;
    }
}
