using Authly.Core.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace Authly.Web.Infrastructure.Clients;

/// <summary>
/// The set of browser web-origins that belong to registered OAuth clients, derived from the
/// redirect URIs tenant admins configure for their applications (plus any extra origins from
/// <c>CORS_ALLOWED_ORIGINS</c> for trusted infrastructure). A single source of truth shared by the
/// CORS policy (<see cref="Cors.ApplicationCorsPolicyProvider"/>) and the CSP <c>form-action</c>
/// directive (<see cref="Security.SecurityHeadersMiddleware"/>) — so registering a redirect URI
/// automatically permits both the cross-origin XHRs and the cross-origin login/logout redirects to
/// that origin, with no per-customer configuration. Results are cached briefly to keep the hot path
/// (every response carries CSP; every XHR is CORS-checked) off the database.
/// </summary>
public interface IClientOriginProvider
{
    Task<IReadOnlyCollection<string>> GetAllowedOriginsAsync(CancellationToken ct = default);
}

/// <inheritdoc />
public sealed class ClientOriginProvider : IClientOriginProvider
{
    private const string CacheKey = "client:allowed-origins";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);

    private readonly IApplicationRepository _apps;
    private readonly IMemoryCache _cache;
    private readonly IReadOnlyList<string> _staticOrigins;

    public ClientOriginProvider(IApplicationRepository apps, IMemoryCache cache, IReadOnlyList<string> staticOrigins)
    {
        _apps = apps;
        _cache = cache;
        _staticOrigins = staticOrigins;
    }

    public async Task<IReadOnlyCollection<string>> GetAllowedOriginsAsync(CancellationToken ct = default)
    {
        if (_cache.TryGetValue(CacheKey, out IReadOnlyCollection<string>? cached) && cached is not null)
            return cached;

        var uris = await _apps.ListAllRedirectUrisAsync(ct);

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
