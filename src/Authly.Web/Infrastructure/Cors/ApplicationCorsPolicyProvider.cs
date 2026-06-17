using Authly.Web.Infrastructure.Clients;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.Options;

namespace Authly.Web.Infrastructure.Cors;

/// <summary>
/// CORS policy provider for the SPA OAuth/OIDC policy. The set of allowed browser web-origins comes
/// from <see cref="IClientOriginProvider"/> — derived from the redirect URIs tenant admins register
/// for their applications — so adding a redirect URI in the admin panel automatically whitelists
/// that origin for the discovery / token / userinfo XHRs, with no per-customer configuration.
///
/// CORS only governs which browser origins may <em>read</em> a response — the OAuth flow still
/// enforces client_id / PKCE / redirect_uri, so this is not an authorization boundary.
/// </summary>
public sealed class ApplicationCorsPolicyProvider : ICorsPolicyProvider
{
    public const string SpaPolicyName = "OAuthSpa";

    private readonly CorsOptions _options;

    public ApplicationCorsPolicyProvider(IOptions<CorsOptions> options) => _options = options.Value;

    public async Task<CorsPolicy?> GetPolicyAsync(HttpContext context, string? policyName)
    {
        // Defer all non-SPA policy names to the default behaviour.
        if (policyName != SpaPolicyName)
            return _options.GetPolicy(policyName ?? _options.DefaultPolicyName ?? string.Empty);

        var origins = await context.RequestServices.GetRequiredService<IClientOriginProvider>()
            .GetAllowedOriginsAsync(context.RequestAborted);

        var builder = new CorsPolicyBuilder();
        if (origins.Count > 0)
            builder.WithOrigins(origins.ToArray())
                   .AllowAnyHeader()
                   .AllowAnyMethod();
        // No AllowCredentials: SPA clients are public (PKCE) and authenticate the token/userinfo
        // calls with a Bearer header, not cookies — so credentialed CORS isn't needed.
        return builder.Build();
    }
}
