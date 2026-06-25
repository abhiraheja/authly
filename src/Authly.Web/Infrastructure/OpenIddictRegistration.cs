using Authly.Infrastructure.Data;

namespace Authly.Web.Infrastructure;

/// <summary>
/// Configures the OpenIddict OAuth 2.0 / OpenID Connect server. Uses OpenIddict's own EF Core
/// stores (clients/authorizations/tokens/scopes live in <see cref="AppDbContext"/>); our
/// <c>applications</c> table mirrors the tenant-facing metadata. Standard endpoints, Authorization
/// Code + PKCE, Refresh (with rotation), and Client Credentials are enabled.
/// </summary>
public static class OpenIddictRegistration
{
    public static IServiceCollection AddAuthlyOpenIddict(
        this IServiceCollection services, bool isDevelopment, IConfiguration configuration)
    {
        // By default OpenIddict issues ENCRYPTED (JWE) access tokens, which third-party resource
        // servers can't read with only the JWKS signing key. Setting this flag makes the access
        // token a plain signed JWT (still RS256/JWKS-verifiable) so resource servers can read its
        // claims directly. Server-wide setting (OpenIddict has no per-tenant encryption switch).
        // Explicit config wins; otherwise default to readable in development, encrypted in prod.
        var disableAccessTokenEncryption =
            configuration.GetValue<bool?>("Authly:Tokens:DisableAccessTokenEncryption") ?? isDevelopment;

        services.AddOpenIddict()
            .AddCore(options =>
            {
                options.UseEntityFrameworkCore()
                    .UseDbContext<AppDbContext>();
            })
            .AddServer(options =>
            {
                // §5.1 standard endpoints
                options.SetAuthorizationEndpointUris("connect/authorize")
                    .SetTokenEndpointUris("connect/token")
                    .SetUserInfoEndpointUris("connect/userinfo")
                    .SetIntrospectionEndpointUris("connect/introspect")
                    .SetRevocationEndpointUris("connect/revoke")
                    .SetEndSessionEndpointUris("connect/logout");

                // §5.2–5.4 flows
                options.AllowAuthorizationCodeFlow()
                    .AllowRefreshTokenFlow()
                    .AllowClientCredentialsFlow();

                // PKCE is mandatory for the authorization-code flow (public + confidential clients).
                options.RequireProofKeyForCodeExchange();

                options.RegisterScopes("openid", "profile", "email", "offline_access", "roles");

                // Emit readable (signed-only) access tokens when configured — see flag above.
                if (disableAccessTokenEncryption)
                    options.DisableAccessTokenEncryption();

                // Refresh-token rotation: a fresh refresh token is issued on each use and the old
                // one is one-time. Reuse of a redeemed token is rejected and the associated
                // authorization's tokens are revoked (theft detection — the "family" guarantee).
                options.SetAccessTokenLifetime(TimeSpan.FromHours(1));
                options.SetRefreshTokenLifetime(TimeSpan.FromDays(14));

                var aspNetCore = options.UseAspNetCore()
                    .EnableAuthorizationEndpointPassthrough()
                    .EnableTokenEndpointPassthrough()
                    .EnableUserInfoEndpointPassthrough()
                    .EnableEndSessionEndpointPassthrough();

                if (isDevelopment)
                {
                    // Dev-only: ephemeral certs + allow HTTP. Production must use persisted
                    // signing/encryption keys (managed/rotated by super admin) and HTTPS.
                    options.AddDevelopmentEncryptionCertificate()
                        .AddDevelopmentSigningCertificate();
                    aspNetCore.DisableTransportSecurityRequirement();
                }
                else
                {
                    options.AddDevelopmentEncryptionCertificate()
                        .AddDevelopmentSigningCertificate();
                    // TODO(Phase 3 hardening): replace with persisted X.509 keys + rotation.
                }
            })
            .AddValidation(options =>
            {
                options.UseLocalServer();
                options.UseAspNetCore();
            });

        return services;
    }
}
