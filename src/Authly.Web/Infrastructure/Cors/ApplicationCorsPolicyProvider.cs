using Authly.Core.Interfaces;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Authly.Web.Infrastructure.Cors;

/// <summary>
/// CORS policy provider for the SPA OAuth/OIDC policy. The set of allowed browser web-origins is
/// derived from the redirect URIs that tenant admins register for their applications (plus any
/// extra origins from <c>CORS_ALLOWED_ORIGINS</c> for trusted infrastructure). This means adding a
/// redirect URI in the admin panel automatically whitelists that origin for the discovery / token /
/// userinfo XHRs — no per-customer configuration or redeploy is required.
///
/// A cross-tenant read is intentional here: the <c>applications</c> table is not RLS-scoped, and
/// CORS only governs which browser origins may <em>read</em> a response — the OAuth flow still
/// enforces client_id / PKCE / redirect_uri, so this is not an authorization boundary. Origins are
/// cached briefly so the hot preflight/XHR path doesn't hit the database on every request.
/// </summary>
public sealed class ApplicationCorsPolicyProvider : ICorsPolicyProvider
{
    public const string SpaPolicyName = "OAuthSpa";

    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);
    private const string CacheKey = "cors:allowed-origins";

    private readonly CorsOptions _options;
    private readonly IMemoryCache _cache;
    private readonly IReadOnlyList<string> _staticOrigins;

    public ApplicationCorsPolicyProvider(IOptions<CorsOptions> options, IMemoryCache cache, IReadOnlyList<string> staticOrigins)
    {
        _options = options.Value;
        _cache = cache;
        _staticOrigins = staticOrigins;
    }

    public async Task<CorsPolicy?> GetPolicyAsync(HttpContext context, string? policyName)
    {
        // Defer all non-SPA policy names to the default behaviour.
        if (policyName != SpaPolicyName)
            return _options.GetPolicy(policyName ?? _options.DefaultPolicyName ?? string.Empty);

        var origins = await GetAllowedOriginsAsync(context);

        var builder = new CorsPolicyBuilder();
        if (origins.Count > 0)
            builder.WithOrigins(origins.ToArray())
                   .AllowAnyHeader()
                   .AllowAnyMethod();
        // No AllowCredentials: SPA clients are public (PKCE) and authenticate the token/userinfo
        // calls with a Bearer header, not cookies — so credentialed CORS isn't needed.
        return builder.Build();
    }

    private async Task<IReadOnlyCollection<string>> GetAllowedOriginsAsync(HttpContext context)
    {
        if (_cache.TryGetValue(CacheKey, out IReadOnlyCollection<string>? cached) && cached is not null)
            return cached;

        var repo = context.RequestServices.GetRequiredService<IApplicationRepository>();
        var uris = await repo.ListAllRedirectUrisAsync(context.RequestAborted);

        var origins = new HashSet<string>(_staticOrigins, StringComparer.OrdinalIgnoreCase);
        foreach (var uri in uris)
            if (Uri.TryCreate(uri, UriKind.Absolute, out var parsed))
                // scheme://host[:port] — matches the browser's Origin header (default ports omitted).
                origins.Add(parsed.GetLeftPart(UriPartial.Authority));

        IReadOnlyCollection<string> result = origins;
        _cache.Set(CacheKey, result, CacheTtl);
        return result;
    }
}
