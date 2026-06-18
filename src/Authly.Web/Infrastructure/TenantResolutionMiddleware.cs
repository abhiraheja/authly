using Authly.Core.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.WebUtilities;

namespace Authly.Web.Infrastructure;

/// <summary>
/// Resolves the current tenant for tenant-facing surfaces and records it in
/// <see cref="ITenantContext"/> (which the DB connection interceptor pushes into
/// <c>app.current_tenant</c> for the RLS backstop).
///
/// Resolution order: custom domain (Host) → a tenant; then a tenant hint — an
/// <c>X-Tenant-Slug</c> header, a <c>?tenant=</c> query (or the same nested in the OAuth
/// <c>ReturnUrl</c>), or the remembered cookie. The hint lets the shared platform host serve
/// every tenant (GCIP-style) without a per-tenant custom domain. Platform surfaces (Hangfire,
/// static assets) are intentionally tenant-less.
/// </summary>
public sealed class TenantResolutionMiddleware
{
    /// <summary>Cookie remembering the chosen tenant slug across follow-up navigations / form posts.</summary>
    private const string TenantHintCookie = "authly.tenant_hint";

    private readonly RequestDelegate _next;

    public TenantResolutionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ITenantContext tenantContext, ITenantRepository tenants)
    {
        var path = context.Request.Path;
        // Platform surfaces are tenant-less; the Management API binds its tenant from the token,
        // not the host, so it is excluded from host/dev-cookie resolution here.
        if (!path.StartsWithSegments("/hangfire")
            && !path.StartsWithSegments("/signup")
            && !path.StartsWithSegments("/invite")
            && !path.StartsWithSegments("/api"))
        {
            // The operator console binds its workspace from the signed-in admin's auth cookie (set at
            // login, re-issued on workspace switch) — never from the host or the tenant hint. The hint
            // targets end-user / hosted-login surfaces on the shared platform host; letting it win here
            // would pin /tenantadmin to a stale/other workspace and the base guard would bounce the
            // admin back to login. The base guard still re-checks cookie == resolved tenant.
            if (path.StartsWithSegments("/tenantadmin"))
            {
                var auth = await context.AuthenticateAsync(AuthSchemes.TenantAdmin);
                if (Guid.TryParse(auth.Principal?.FindFirst(TenantAdminClaims.TenantId)?.Value, out var adminTenant))
                    tenantContext.SetTenant(adminTenant);
            }
            else
            {
                var host = context.Request.Host.Host;
                var tenant = await tenants.GetByCustomDomainOrNullAsync(host, context.RequestAborted);

                // Tenant hint: lets the shared platform host serve any tenant without a custom domain.
                // An X-Tenant-Slug header, a ?tenant= query (which is then remembered in a cookie so
                // follow-up navigations, form posts and email-link clicks resolve too).
                if (tenant is null)
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
                    else if (context.Request.Cookies.TryGetValue(TenantHintCookie, out var cookie))
                        slug = cookie;

                    if (!string.IsNullOrWhiteSpace(slug))
                    {
                        tenant = await tenants.GetBySlugAsync(slug, context.RequestAborted);
                        if (tenant is not null)
                            context.Response.Cookies.Append(TenantHintCookie, tenant.Slug,
                                new CookieOptions
                                {
                                    HttpOnly = true,
                                    IsEssential = true,
                                    SameSite = SameSiteMode.Lax,
                                    Secure = context.Request.IsHttps
                                });
                    }
                }

                if (tenant is not null)
                    tenantContext.SetTenant(tenant.Id);
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
