---
name: JWT authentication & SmartScheme dual-auth
description: Dual-scheme authentication (JWT + API key) via an ASP.NET Core policy scheme, JwtBearer configuration, TokenValidationParameters, event hooks (logging, claim augmentation), and the rule that authentication must never trust HTTP headers as claims.
type: skill-section
---

# JWT authentication & SmartScheme dual-auth

## When to use

Every Saarvix backend service that accepts either a user-bearing JWT **or** a service-to-service API key uses this installer. The "SmartScheme" pattern is a `AddPolicyScheme` that forwards to `JwtBearer` by default and to `"ApiKey"` when an `x-api-key` header is present.

## Architectural decisions

- **Default scheme is a policy scheme, not JWT directly.** `DefaultScheme = "SmartScheme"` so both auth flows share one pipeline. Without this, endpoints tagged `[Authorize]` silently only accept JWTs and reject API keys.
- **Symmetric signing key (HS256) in current wallet-v2** — `SymmetricSecurityKey` derived from `jwt.Secret`. The long-term plan (Azure Entra External ID / CIAM) will switch this to RS256 via JWKS; until then, **the Secret must live in Key Vault and never in appsettings.json**.
- **`ClockSkew = TimeSpan.Zero`.** Default is 5 minutes, which is sloppy for short-lived access tokens. Zero skew gives us accurate expiration behavior.
- **`SaveToken = true`** so downstream services (producers, outbound HTTP) can echo the caller's token via `HttpContext.GetTokenAsync("access_token")`.
- **`RequireHttpsMetadata = false`** in current config because the service lives behind a gateway that terminates TLS. Left intentionally; revisit when we front the service directly.
- **No header-as-claim injection.** The `OnTokenValidated` hook appends a blank `ClaimsIdentity` solely to anchor the authentication type. It MUST NOT copy HTTP headers into the principal — that would break the 5-dimensional authz model because anyone could spoof `company_id` / `sub` / `Permissions` by setting headers.

## `AuthInstaller` reference implementation (`{ServiceName}.Api/Installers/AuthInstaller.cs`)

```csharp
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Saar_Packages.Models;

namespace {ServiceName}.Api.Installers;

public static class AuthInstaller
{
    public static IServiceCollection AddAuthenticationServices(
        this IServiceCollection services,
        IConfigurationManager configurations)
    {
        var jwt = configurations.GetSection("JWT").Get<JwtConfiguration>()
            ?? throw new InvalidOperationException("JWT configuration section missing.");

        services.AddSingleton(jwt);

        services.AddAuthentication(options =>
        {
            options.DefaultScheme = "SmartScheme";
        })
        .AddPolicyScheme("SmartScheme", "Smart Authentication", options =>
        {
            options.ForwardDefaultSelector = context =>
            {
                var hasApiKey = !string.IsNullOrEmpty(context.Request.Headers["x-api-key"]);
                return hasApiKey ? "ApiKey" : JwtBearerDefaults.AuthenticationScheme;
            };
        })
        .AddJwtBearer(options =>
        {
            options.Events = new JwtBearerEvents
            {
                OnTokenValidated = ctx =>
                {
                    var logger = ctx.HttpContext.RequestServices
                        .GetRequiredService<ILoggerFactory>()
                        .CreateLogger("JWT");

                    logger.LogInformation(
                        "JWT token validated. Subject: {Sub}",
                        ctx.Principal?.FindFirst("sub")?.Value
                            ?? ctx.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value);

                    // Anchor the authentication type. Do NOT inject any header-derived claims here.
                    ctx.Principal!.AddIdentity(
                        new ClaimsIdentity(authenticationType: JwtBearerDefaults.AuthenticationScheme));

                    return Task.CompletedTask;
                },

                OnAuthenticationFailed = ctx =>
                {
                    ctx.HttpContext.RequestServices
                        .GetRequiredService<ILoggerFactory>()
                        .CreateLogger("JWT")
                        .LogError(ctx.Exception,
                            "JWT authentication FAILED: {ExType} — {Message}",
                            ctx.Exception.GetType().Name, ctx.Exception.Message);
                    return Task.CompletedTask;
                },

                OnChallenge = ctx =>
                {
                    ctx.HttpContext.RequestServices
                        .GetRequiredService<ILoggerFactory>()
                        .CreateLogger("JWT")
                        .LogWarning(
                            "JWT challenge. Error: [{Error}] Desc: [{Desc}] Failure: {Failure}",
                            ctx.Error, ctx.ErrorDescription, ctx.AuthenticateFailure?.Message);
                    return Task.CompletedTask;
                },

                OnForbidden = ctx =>
                {
                    ctx.HttpContext.RequestServices
                        .GetRequiredService<ILoggerFactory>()
                        .CreateLogger("JWT")
                        .LogWarning("JWT Forbidden — principal authenticated but not authorized.");
                    return Task.CompletedTask;
                },

                OnMessageReceived = ctx =>
                {
                    var authHeader = ctx.Request.Headers.Authorization.FirstOrDefault();
                    if (string.IsNullOrEmpty(authHeader))
                        return Task.CompletedTask;

                    // Strip "Bearer "/"bearer " prefix. Never log the token, not even partially.
                    var token = authHeader;
                    if (token.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                        token = token["Bearer ".Length..];

                    ctx.Token = token;
                    return Task.CompletedTask;
                }
            };

            options.ClaimsIssuer        = jwt.ValidIssuer;
            options.SaveToken           = true;
            options.RequireHttpsMetadata = false; // gateway terminates TLS

            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer            = true,
                ValidateAudience          = true,
                ValidateLifetime          = true,
                ValidateIssuerSigningKey  = true,
                RequireExpirationTime     = true,
                ClockSkew                 = TimeSpan.Zero,
                ValidAudiences            = [jwt.ValidAudience],
                ValidIssuer               = jwt.ValidIssuer,
                IssuerSigningKey          = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Secret))
            };
        });

        return services;
    }
}
```

## `JwtConfiguration` POCO (lives in `Saar_Packages.Models`)

```csharp
public sealed class JwtConfiguration
{
    public string Secret         { get; set; } = default!;
    public string ValidIssuer    { get; set; } = default!;
    public string ValidAudience  { get; set; } = default!;
}
```

Bound from `appsettings.json`:

```json
{
  "JWT": {
    "Secret":        "@Microsoft.KeyVault(SecretUri=...)",
    "ValidIssuer":   "https://{tenant}.ciamlogin.com/{tenantId}/v2.0",
    "ValidAudience": "{client-id-guid}"
  }
}
```

## Event hooks — what each one is for

| Event | Purpose | Do |  Don't |
|---|---|---|---|
| `OnMessageReceived` | Normalize where the token comes from | Strip `Bearer ` prefix; accept non-standard headers only if whitelisted | Log any part of the token |
| `OnTokenValidated` | Post-validation observability + principal shaping | Log `sub`; anchor auth type | Inject headers as claims, mutate principal based on body/query |
| `OnAuthenticationFailed` | Error logging | Log exception type + message | Return 200 or swallow silently |
| `OnChallenge` | 401 path observability | Log error/description | Leak `AuthenticateFailure` details to response body |
| `OnForbidden` | 403 path observability | Log at Warning | Downgrade to Info — 403s are investigation-worthy |

## The *"inject headers as claims"* anti-pattern

A previous version of the codebase included a GraphQL `IHttpRequestInterceptor` that iterated every HTTP header and appended each as a claim on the principal. **This is unsafe.** It allows any authenticated caller to set, e.g., `company_id: <other-tenant>` or `Permissions: Manage_Manage` as a header and have it treated as a JWT claim by downstream authorization handlers.

**Rules:**

1. Claims come **only** from the signed JWT.
2. If a value must be derived from context (e.g., tenant ID extracted from the token), introduce an `IRequestContext` service that wraps `IHttpContextAccessor` and reads claims — do not mutate the principal.
3. If you absolutely must accept a non-claim header (idempotency key, correlation id, feature flag), read it directly from `HttpContext.Request.Headers` where it's needed, not via claims.

## Dual-scheme rationale: why not two separate `[Authorize]` attributes?

Instead of mixing `[Authorize(AuthenticationSchemes="Bearer")]` and `[Authorize(AuthenticationSchemes="ApiKey")]` across endpoints, the policy scheme consolidates routing into one place. Benefits:

- Every `[Authorize]` works identically regardless of caller type.
- `ForwardDefaultSelector` runs once per request, cheaply.
- Any future scheme (mTLS, HMAC-signed requests) is added in the same switch.

## Call-site wiring

```csharp
// Program.cs
services.AddAuthenticationServices(configuration);

// ... later ...
app.UseAuthentication();
app.UseAuthorization(); // from 07-five-dimensional-authorization.md
```

Order is strict: `UseAuthentication` before `UseAuthorization` before `MapGraphQL`.

## Transitioning from HS256 → RS256 (CIAM)

When moving to Azure Entra External ID (or any OIDC provider), replace the `IssuerSigningKey` stanza with metadata-driven key discovery:

```csharp
options.Authority = jwt.Authority; // e.g., "https://{tenant}.ciamlogin.com/{tenantId}/v2.0"
options.MetadataAddress = $"{jwt.Authority}/.well-known/openid-configuration";
options.TokenValidationParameters.IssuerSigningKeys = null;  // JWKS auto-refresh
options.TokenValidationParameters.ValidateIssuerSigningKey = true;
```

**Checklist before flipping:**

1. Key Vault: remove `Secret`; keep `Authority`, `ValidAudience`, `ValidIssuer`.
2. Identity provider: confirm `iss` and `aud` claims in issued tokens exactly match `ValidIssuer` / `ValidAudience`.
3. Local dev: make sure the authority URL is reachable from the dev machine (gateway routing can block JWKS fetch).
4. Rotate old HS256 tokens out — both schemes cannot coexist on the same `AddJwtBearer`.

## Common mistakes

1. **Injecting headers as claims** — covered above, unsafe.
2. **Leaving `ClockSkew` at default 5m** — short-lived tokens behave unexpectedly at boundaries.
3. **Logging the token** — even "first 40 chars" is enough to reconstruct parts of the signature or leak PII-bearing claims. Never do it.
4. **Forgetting `AddPolicyScheme` and setting `DefaultScheme = JwtBearerDefaults.AuthenticationScheme`** — API-key callers then get 401s silently.
5. **Putting `jwt.Secret` in appsettings.json committed to the repo** — use Key Vault via `@Microsoft.KeyVault(...)` references.
6. **Forwarding JWT to `ApiKey` scheme when no API-key scheme is registered** — the policy scheme fails opaquely. Either register `AddApiKey(...)` via a library, or gate the forward with `hasApiKey && ApiKeySchemeRegistered`.

## Related skills

- `07-five-dimensional-authorization.md` — consumes the claims populated here.
- `10-azure-keyvault-and-configuration.md` — where `jwt.Secret` actually comes from.
- `12-api-installer-pattern-and-startup.md` — middleware ordering.
