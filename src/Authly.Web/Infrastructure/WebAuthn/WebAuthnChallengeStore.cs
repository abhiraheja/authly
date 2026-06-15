using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;

namespace Authly.Web.Infrastructure.WebAuthn;

/// <summary>
/// The in-flight WebAuthn ceremony state, carried in a short-lived data-protected cookie between
/// the begin and complete steps. Holds the FIDO2 challenge <c>State</c> plus the tenant/user the
/// ceremony is for, so the completion can't be re-pointed at another account.
/// </summary>
public sealed record WebAuthnPending(string Purpose, Guid TenantId, Guid UserId, string State);

/// <summary>Reads/writes the <see cref="WebAuthnPending"/> cookie via ASP.NET Data Protection.</summary>
public sealed class WebAuthnChallengeStore
{
    public const string CookieName = "authly.webauthn";
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);

    private readonly ITimeLimitedDataProtector _protector;

    public WebAuthnChallengeStore(IDataProtectionProvider provider)
        => _protector = provider.CreateProtector("Authly.WebAuthnChallenge").ToTimeLimitedDataProtector();

    public void Save(HttpContext ctx, WebAuthnPending pending)
    {
        var payload = _protector.Protect(JsonSerializer.Serialize(pending), Lifetime);
        ctx.Response.Cookies.Append(CookieName, payload, new CookieOptions
        {
            HttpOnly = true,
            Secure = ctx.Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            IsEssential = true,
            MaxAge = Lifetime
        });
    }

    public WebAuthnPending? Read(HttpContext ctx)
    {
        if (!ctx.Request.Cookies.TryGetValue(CookieName, out var payload) || string.IsNullOrEmpty(payload))
            return null;
        try
        {
            return JsonSerializer.Deserialize<WebAuthnPending>(_protector.Unprotect(payload));
        }
        catch (Exception)
        {
            return null; // tampered, expired, or key-rotated
        }
    }

    public void Clear(HttpContext ctx) => ctx.Response.Cookies.Delete(CookieName);
}
