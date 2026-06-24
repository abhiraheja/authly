using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;

namespace Authly.Web.Infrastructure.Auth;

/// <summary>
/// The half-authenticated state between requesting a phone OTP and entering it. The user is NOT
/// signed in yet — carried in a short-lived, data-protected (encrypted + signed) cookie so the
/// resolved user/tenant can't be forged, and expires quickly. <see cref="Purpose"/> distinguishes a
/// sign-in ("login") from a post-signup phone verification ("signup_verify").
/// </summary>
public sealed record PhonePendingOtp(
    Guid UserId,
    Guid TenantId,
    string Purpose,
    string? ReturnUrl);

/// <summary>Reads/writes the <see cref="PhonePendingOtp"/> cookie using ASP.NET Data Protection.</summary>
public sealed class PhoneLoginPendingStore
{
    public const string CookieName = "authly.phone_otp";
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);

    private readonly ITimeLimitedDataProtector _protector;

    public PhoneLoginPendingStore(IDataProtectionProvider provider)
        => _protector = provider.CreateProtector("Authly.PhonePendingOtp").ToTimeLimitedDataProtector();

    public void Save(HttpContext ctx, PhonePendingOtp pending)
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

    public PhonePendingOtp? Read(HttpContext ctx)
    {
        if (!ctx.Request.Cookies.TryGetValue(CookieName, out var payload) || string.IsNullOrEmpty(payload))
            return null;
        try
        {
            return JsonSerializer.Deserialize<PhonePendingOtp>(_protector.Unprotect(payload));
        }
        catch (Exception)
        {
            return null; // tampered, expired, or key-rotated
        }
    }

    public void Clear(HttpContext ctx) => ctx.Response.Cookies.Delete(CookieName);
}
