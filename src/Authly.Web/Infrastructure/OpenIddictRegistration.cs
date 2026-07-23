using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
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
        this IServiceCollection services, IWebHostEnvironment environment, IConfiguration configuration)
    {
        var isDevelopment = environment.IsDevelopment();

        // By default OpenIddict issues ENCRYPTED (JWE) access tokens, which third-party resource
        // servers can't read with only the JWKS signing key. Setting this flag makes the access
        // token a plain signed JWT (still RS256/JWKS-verifiable) so resource servers can read its
        // claims directly. Server-wide setting (OpenIddict has no per-tenant encryption switch).
        // Explicit config wins; otherwise default to readable in development, encrypted in prod.
        var disableAccessTokenEncryption =
            configuration.GetValue<bool?>("Authly:Tokens:DisableAccessTokenEncryption") ?? isDevelopment;

        // The access-token lifetime is the hard ceiling on how quickly a revoked session, a
        // suspended/deleted user, or a role change becomes effective for a relying app: the token is a
        // self-contained JWT verified offline against JWKS, so it cannot be revoked mid-flight — it must
        // expire. Keep it short; the refresh path (AuthorizationController.ExchangeUserGrantAsync)
        // re-checks the session + account + RBAC on every rotation, so a short access token bounds the
        // window without forcing frequent full re-logins. Config-overridable (minutes); default 5.
        var accessTokenLifetime = TimeSpan.FromMinutes(
            configuration.GetValue<int?>("Authly:Tokens:AccessTokenLifetimeMinutes") ?? 5);
        var refreshTokenLifetime = TimeSpan.FromDays(
            configuration.GetValue<int?>("Authly:Tokens:RefreshTokenLifetimeDays") ?? 14);

        // Resolve stable signing + encryption certificates. These MUST survive process restarts and be
        // identical across replicas, otherwise the JWKS a resource server fetches won't contain the key
        // (kid) that signed a still-valid token → "no JWKS key for kid" 401s. See ResolveCertificate.
        var signingCertificate = ResolveCertificate(
            environment, configuration, kind: "Signing", X509KeyUsageFlags.DigitalSignature);
        var encryptionCertificate = ResolveCertificate(
            environment, configuration, kind: "Encryption", X509KeyUsageFlags.KeyEncipherment);

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
                options.SetAccessTokenLifetime(accessTokenLifetime);
                options.SetRefreshTokenLifetime(refreshTokenLifetime);

                // Persisted keys (see ResolveCertificate) instead of the per-instance, per-restart
                // development certificates — those regenerate a new thumbprint (kid) on every restart
                // and differ across replicas, which invalidates outstanding tokens against JWKS.
                options.AddSigningCertificate(signingCertificate)
                    .AddEncryptionCertificate(encryptionCertificate);

                var aspNetCore = options.UseAspNetCore()
                    .EnableAuthorizationEndpointPassthrough()
                    .EnableTokenEndpointPassthrough()
                    .EnableUserInfoEndpointPassthrough()
                    .EnableEndSessionEndpointPassthrough();

                if (isDevelopment)
                {
                    // Dev-only: allow plain HTTP so local runs work without TLS.
                    aspNetCore.DisableTransportSecurityRequirement();
                }
            })
            .AddValidation(options =>
            {
                options.UseLocalServer();
                options.UseAspNetCore();
            });

        return services;
    }

    /// <summary>
    /// Resolves a stable X.509 certificate for OpenIddict signing/encryption.
    /// <para>
    /// Resolution order:
    /// <list type="number">
    /// <item>A managed certificate supplied via configuration
    /// (<c>Authly:Keys:{kind}CertificatePath</c> + optional <c>...Password</c>) — production path.</item>
    /// <item>Otherwise a self-signed certificate persisted as a PKCS#12 file under the key directory
    /// (<c>Authly:Keys:Directory</c>, default <c>{ContentRoot}/keys</c>). Generated once and reused on
    /// every start, so the thumbprint/kid stays constant across restarts.</item>
    /// </list>
    /// Mount the key directory on a persistent (and, for multi-replica deployments, shared) volume so the
    /// keys survive redeploys and every instance signs with — and publishes — the same key.
    /// </para>
    /// </summary>
    private static X509Certificate2 ResolveCertificate(
        IWebHostEnvironment environment, IConfiguration configuration, string kind, X509KeyUsageFlags keyUsage)
    {
        // Container/prod: an operator-managed PFX takes precedence when provided.
        var configuredPath = configuration[$"Authly:Keys:{kind}CertificatePath"];
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            var password = configuration[$"Authly:Keys:{kind}CertificatePassword"];
            return X509CertificateLoader.LoadPkcs12FromFile(
                configuredPath, password, X509KeyStorageFlags.EphemeralKeySet | X509KeyStorageFlags.Exportable);
        }

        // Otherwise persist an auto-generated cert to disk so it is reused on the next start.
        var keyDirectory = configuration["Authly:Keys:Directory"];
        if (string.IsNullOrWhiteSpace(keyDirectory))
            keyDirectory = Path.Combine(environment.ContentRootPath, "keys");

        Directory.CreateDirectory(keyDirectory);
        var pfxPath = Path.Combine(keyDirectory, $"{kind.ToLowerInvariant()}.pfx");

        if (File.Exists(pfxPath))
        {
            return X509CertificateLoader.LoadPkcs12FromFile(
                pfxPath, password: null, X509KeyStorageFlags.EphemeralKeySet | X509KeyStorageFlags.Exportable);
        }

        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            $"CN=Authly {kind} Certificate", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509KeyUsageExtension(keyUsage, critical: true));

        using var generated = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(10));
        var pfxBytes = generated.Export(X509ContentType.Pfx);
        File.WriteAllBytes(pfxPath, pfxBytes);

        return X509CertificateLoader.LoadPkcs12(
            pfxBytes, password: null, X509KeyStorageFlags.EphemeralKeySet | X509KeyStorageFlags.Exportable);
    }
}
