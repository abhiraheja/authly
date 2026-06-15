using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;

namespace Authly.Web.Infrastructure;

/// <summary>
/// Issues the end-user cookie identity. Shared by the password login path and the MFA gate so
/// both surfaces build an identical principal (same claims, same scheme).
/// </summary>
public static class UserSignIn
{
    public static Task SignInAsync(HttpContext ctx, Guid userId, string email, Guid tenantId, Guid sessionId, bool emailVerified,
        Guid? impersonatorId = null, string? impersonatorEmail = null)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Name, email),
            new(UserClaims.TenantId, tenantId.ToString()),
            new(UserClaims.SessionId, sessionId.ToString()),
            new(UserClaims.EmailVerified, emailVerified ? "true" : "false")
        };
        if (impersonatorId is { } impId)
        {
            claims.Add(new Claim(UserClaims.ImpersonatorId, impId.ToString()));
            claims.Add(new Claim(UserClaims.ImpersonatorEmail, impersonatorEmail ?? ""));
        }
        var identity = new ClaimsIdentity(claims, AuthSchemes.User);
        return ctx.SignInAsync(AuthSchemes.User, new ClaimsPrincipal(identity),
            // Impersonation cookies are session-scoped (not persistent) so they don't linger.
            new AuthenticationProperties { IsPersistent = impersonatorId is null });
    }
}
