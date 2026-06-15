using System.Security.Claims;
using Authly.Modules.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace Authly.Web.Infrastructure;

/// <summary>
/// Per-request validation of the end-user cookie against the <c>sessions</c> table: if the session
/// behind the cookie has been revoked or has expired (e.g. the user revoked the device from the
/// portal, or changed their password), the principal is rejected and the cookie cleared. Without
/// this, "revoke session" would only hide a row — the cookie would still authenticate.
/// </summary>
public static class SessionCookieValidator
{
    public static async Task ValidateAsync(CookieValidatePrincipalContext context)
    {
        var sessionId = context.Principal?.FindFirstValue(UserClaims.SessionId);
        if (!Guid.TryParse(sessionId, out var id))
        {
            await RejectAsync(context);
            return;
        }

        var auth = context.HttpContext.RequestServices.GetRequiredService<IAuthService>();
        var session = await auth.GetActiveSessionAsync(id, context.HttpContext.RequestAborted);
        if (session is null)
            await RejectAsync(context);
    }

    private static async Task RejectAsync(CookieValidatePrincipalContext context)
    {
        context.RejectPrincipal();
        await context.HttpContext.SignOutAsync(AuthSchemes.User);
    }
}
