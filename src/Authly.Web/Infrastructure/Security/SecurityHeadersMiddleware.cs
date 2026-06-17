using Authly.Web.Infrastructure.Clients;

namespace Authly.Web.Infrastructure.Security;

/// <summary>
/// Adds baseline security response headers (CSP, frame/Referrer/permissions policy, nosniff) to
/// every response. HSTS is applied separately via <c>UseHsts</c> in non-dev. The CSP allows the
/// CDN assets the hosted/admin pages load (Bootstrap, Lucide, Google Fonts) plus inline styles/
/// scripts the views emit; tighten to nonces if those are removed.
///
/// <para>The <c>form-action</c> directive is built dynamically: besides <c>'self'</c> it includes
/// every registered client origin (<see cref="IClientOriginProvider"/>). This is required for the
/// hosted-login → <c>/connect/authorize</c> → client callback chain: after the login form POST the
/// browser applies <c>form-action</c> to the *redirect target* too, so a cross-origin redirect to a
/// SPA (e.g. <c>http://localhost:4200</c>) would be blocked under <c>'self'</c> alone. Deriving the
/// origins from registered redirect URIs keeps it dynamic — no hard-coded app origins.</para>
/// </summary>
public sealed class SecurityHeadersMiddleware
{
    // Everything up to (and including) the start of form-action; client origins are appended.
    // Captcha providers (hCaptcha / Cloudflare Turnstile) need their script + frame + connect origins.
    private const string CspPrefix =
        "default-src 'self'; " +
        "script-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net https://unpkg.com https://js.hcaptcha.com https://hcaptcha.com https://challenges.cloudflare.com; " +
        "style-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net https://fonts.googleapis.com https://*.hcaptcha.com; " +
        "font-src 'self' https://fonts.gstatic.com; " +
        "img-src 'self' data: https:; " +
        "connect-src 'self' https://hcaptcha.com https://*.hcaptcha.com https://challenges.cloudflare.com; " +
        "frame-src https://*.hcaptcha.com https://hcaptcha.com https://challenges.cloudflare.com; " +
        "frame-ancestors 'none'; " +
        "base-uri 'self'; " +
        "form-action 'self'";

    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, IClientOriginProvider origins)
    {
        var h = context.Response.Headers;
        h["X-Content-Type-Options"] = "nosniff";
        h["X-Frame-Options"] = "DENY";
        h["Referrer-Policy"] = "strict-origin-when-cross-origin";
        h["Permissions-Policy"] = "geolocation=(), microphone=(), camera=(), payment=()";
        if (!h.ContainsKey("Content-Security-Policy"))
            h["Content-Security-Policy"] = await BuildCspAsync(origins, context.RequestAborted);

        await _next(context);
    }

    private static async Task<string> BuildCspAsync(IClientOriginProvider origins, CancellationToken ct)
    {
        try
        {
            var allowed = await origins.GetAllowedOriginsAsync(ct);
            return allowed.Count == 0 ? CspPrefix : CspPrefix + " " + string.Join(' ', allowed);
        }
        catch
        {
            // Never let a transient origin-lookup failure drop the security header on a response.
            return CspPrefix;
        }
    }
}
