using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;

namespace Authly.Web.Infrastructure.Social;

/// <summary>
/// The OAuth <c>state</c> payload — tamper-proof and short-lived. Carries the tenant, provider,
/// the exact redirect URI used in the authorization request (so token exchange matches), and the
/// post-login return URL. Being data-protected makes it unforgeable, which is the CSRF defence.
/// </summary>
public sealed record SocialState(Guid TenantId, string Provider, string RedirectUri, string? ReturnUrl);

public sealed class SocialStateProtector
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(15);
    private readonly ITimeLimitedDataProtector _protector;

    public SocialStateProtector(IDataProtectionProvider provider)
        => _protector = provider.CreateProtector("Authly.SocialState").ToTimeLimitedDataProtector();

    public string Protect(SocialState state)
        => _protector.Protect(JsonSerializer.Serialize(state), Lifetime);

    public SocialState? Read(string? token)
    {
        if (string.IsNullOrEmpty(token)) return null;
        try { return JsonSerializer.Deserialize<SocialState>(_protector.Unprotect(token)); }
        catch (Exception) { return null; }
    }
}
