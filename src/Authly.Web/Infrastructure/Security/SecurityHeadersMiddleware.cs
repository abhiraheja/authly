namespace Authly.Web.Infrastructure.Security;

/// <summary>
/// Adds baseline security response headers (CSP, frame/Referrer/permissions policy, nosniff) to
/// every response. HSTS is applied separately via <c>UseHsts</c> in non-dev. The CSP allows the
/// CDN assets the hosted/admin pages load (Bootstrap, Lucide, Google Fonts) plus inline styles/
/// scripts the views emit; tighten to nonces if those are removed.
/// </summary>
public sealed class SecurityHeadersMiddleware
{
    // Captcha providers (hCaptcha / Cloudflare Turnstile) need their script + frame + connect origins.
    private const string Csp =
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

    public Task InvokeAsync(HttpContext context)
    {
        var h = context.Response.Headers;
        h["X-Content-Type-Options"] = "nosniff";
        h["X-Frame-Options"] = "DENY";
        h["Referrer-Policy"] = "strict-origin-when-cross-origin";
        h["Permissions-Policy"] = "geolocation=(), microphone=(), camera=(), payment=()";
        if (!h.ContainsKey("Content-Security-Policy"))
            h["Content-Security-Policy"] = Csp;
        return _next(context);
    }
}
