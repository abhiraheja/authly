using Authly.Core.Security;

namespace Authly.Web.Infrastructure.Security;

/// <summary>
/// Per-IP fixed-window rate limiting on the sensitive unauthenticated endpoints (login, register,
/// password/magic/recovery requests, passkey, OAuth token). Keyed per path + IP so one abused
/// endpoint doesn't starve others. Over-limit requests get 429 with Retry-After.
/// </summary>
public sealed class RateLimitingMiddleware
{
    // path-prefix → (limit, window). Tightest on credential-guessing surfaces.
    private static readonly (string Path, int Limit, int WindowSeconds)[] Rules =
    {
        ("/account/login", 10, 60),
        ("/account/register", 5, 60),
        ("/account/forgot-password", 5, 60),
        ("/account/magic-link", 5, 60),
        ("/account/recover-request", 5, 60),
        ("/account/passkey", 20, 60),
        ("/connect/token", 60, 60)
    };

    private readonly RequestDelegate _next;
    private readonly IRateLimiter _limiter;

    public RateLimitingMiddleware(RequestDelegate next, IRateLimiter limiter)
    {
        _next = next;
        _limiter = limiter;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Only throttle write attempts; GETs (rendering the form) are cheap and idempotent.
        if (HttpMethods.IsPost(context.Request.Method))
        {
            var path = context.Request.Path.Value ?? "";
            var rule = Rules.FirstOrDefault(r => path.StartsWith(r.Path, StringComparison.OrdinalIgnoreCase));
            if (rule.Path is not null)
            {
                var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                var key = $"{rule.Path}:{ip}";
                var result = await _limiter.CheckAsync(key, rule.Limit, TimeSpan.FromSeconds(rule.WindowSeconds), context.RequestAborted);
                if (!result.Allowed)
                {
                    context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                    context.Response.Headers.RetryAfter = ((int)Math.Ceiling(result.RetryAfter.TotalSeconds)).ToString();
                    await context.Response.WriteAsync("Too many requests. Please slow down and try again shortly.");
                    return;
                }
            }
        }

        await _next(context);
    }
}
