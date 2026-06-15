using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;

namespace Authly.Web.Infrastructure.Mfa;

/// <summary>
/// The half-authenticated state between a correct password and a completed MFA challenge. The
/// user is NOT signed in yet — this is carried in a short-lived, data-protected (encrypted +
/// signed) cookie so it can't be forged, and expires quickly so a stalled challenge can't be
/// resumed much later.
/// </summary>
public sealed record MfaPendingLogin(
    Guid UserId,
    Guid TenantId,
    Guid SessionId,
    string Email,
    bool EmailVerified,
    string? ReturnUrl);

/// <summary>Reads/writes the <see cref="MfaPendingLogin"/> cookie using ASP.NET Data Protection.</summary>
public sealed class MfaPendingStore
{
    public const string CookieName = "authly.mfa";
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);

    private readonly ITimeLimitedDataProtector _protector;

    public MfaPendingStore(IDataProtectionProvider provider)
        => _protector = provider.CreateProtector("Authly.MfaPending").ToTimeLimitedDataProtector();

    public void Save(HttpContext ctx, MfaPendingLogin pending)
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

    public MfaPendingLogin? Read(HttpContext ctx)
    {
        if (!ctx.Request.Cookies.TryGetValue(CookieName, out var payload) || string.IsNullOrEmpty(payload))
            return null;
        try
        {
            var json = _protector.Unprotect(payload);
            return JsonSerializer.Deserialize<MfaPendingLogin>(json);
        }
        catch (Exception)
        {
            // Tampered, expired, or key-rotated — treat as no pending challenge.
            return null;
        }
    }

    public void Clear(HttpContext ctx) => ctx.Response.Cookies.Delete(CookieName);
}
