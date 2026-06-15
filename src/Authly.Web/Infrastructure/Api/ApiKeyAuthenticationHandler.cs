using System.Security.Claims;
using System.Text.Encodings.Web;
using Authly.Core.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Authly.Web.Infrastructure.Api;

public sealed class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions
{
    public const string HeaderName = "X-API-Key";
}

/// <summary>
/// Authenticates Management API requests presenting an <c>X-API-Key</c> header. The key is hashed
/// (SHA-256) and looked up globally (api_keys is not RLS-scoped); the key's own tenant then scopes
/// the request. The resulting principal carries <c>sub</c>, <c>tenant_id</c>, and one
/// <c>permissions</c> claim per granted scope — the same shape as a Bearer access token, so the
/// permission filter works uniformly across both auth methods.
/// </summary>
public sealed class ApiKeyAuthenticationHandler : AuthenticationHandler<ApiKeyAuthenticationOptions>
{
    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<ApiKeyAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder) { }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(ApiKeyAuthenticationOptions.HeaderName, out var values))
            return AuthenticateResult.NoResult(); // no key presented → let other schemes try

        var presented = values.ToString();
        if (string.IsNullOrWhiteSpace(presented))
            return AuthenticateResult.Fail("Empty API key.");

        var services = Context.RequestServices;
        var hasher = services.GetRequiredService<ITokenHasher>();
        var repo = services.GetRequiredService<IApiKeyRepository>();

        var key = await repo.GetByHashAsync(hasher.Hash(presented), Context.RequestAborted);
        if (key is null || !key.IsActive(DateTimeOffset.UtcNow))
            return AuthenticateResult.Fail("Invalid or inactive API key.");

        var identity = new ClaimsIdentity(AuthSchemes.ApiKey);
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, (key.UserId ?? key.Id).ToString()));
        identity.AddClaim(new Claim(TokenClaims.TenantId, key.TenantId.ToString()));
        foreach (var scope in key.Scopes)
            identity.AddClaim(new Claim(TokenClaims.Permissions, scope));

        // Best-effort usage timestamp; never fail auth because the touch failed.
        try { await repo.TouchLastUsedAsync(key.Id, DateTimeOffset.UtcNow, Context.RequestAborted); }
        catch { /* non-critical */ }

        var principal = new ClaimsPrincipal(identity);
        return AuthenticateResult.Success(new AuthenticationTicket(principal, AuthSchemes.ApiKey));
    }
}
