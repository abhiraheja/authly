---
name: Rate limiting — Global / Public / Authenticated policies
description: Fixed-window partitioned rate limiter with three policies (IP-keyed global, IP-keyed public, subject-keyed authenticated), config-driven permit/window values, and 429 response shaping.
type: skill-section
---

# Rate limiting — Global / Public / Authenticated policies

## When to use

Every Saarvix service applies rate limiting at the ASP.NET Core pipeline level using `Microsoft.AspNetCore.RateLimiting`. The three policies compose to defend against:

1. **Global abuse** — any IP sending an absurd number of requests across the service.
2. **Public endpoint abuse** — explicitly-public endpoints (login, forgot-password, registration) that accept unauthenticated callers.
3. **Authenticated endpoint abuse / scraping** — per-subject caps so a single user can't DoS the service even with a valid token.

## Architectural decisions

- **Partitioned, not flat.** A single limiter would punish all callers for one noisy neighbor. `PartitionedRateLimiter<HttpContext, string>` keys on the partition (`ip` or `sub`).
- **Fixed window, not sliding.** Simpler semantics, lower memory, predictable replenishment. Sliding window is a premature optimization for the current traffic profile.
- **Global uses IP; Authenticated uses `sub` claim.** A signed-in user rotating IPs can't evade Authenticated limits. A logged-out caller can't rotate partitioning trivially across Global/Public.
- **Config-driven limits** via `RateLimitConfiguration` POCO — never hardcode; different environments (dev/stage/prod) must tune independently.
- **Policy names are constants** in `{ServiceName}.Models/Constants/RateLimiterConstants.cs` so GraphQL descriptors / REST endpoints reference `RateLimiterConstants.Authenticated` instead of string literals.
- **429 is the rejection code**, not the default 503. 503 means "I'm broken"; 429 is the right signal.

## `RateLimitConfiguration` POCO (`{ServiceName}.Models/Base/RateLimitConfiguration.cs`)

```csharp
namespace {ServiceName}.Models.Base;

public class RateLimitConfiguration
{
    public required RateLimitPolicyOptions Global        { get; set; }
    public required RateLimitPolicyOptions Public        { get; set; }
    public required RateLimitPolicyOptions Authenticated { get; set; }
}

public class RateLimitPolicyOptions
{
    public int PermitLimit       { get; set; }
    public int WindowInSeconds   { get; set; }
}
```

`appsettings.json` block:

```json
"RateLimitConfiguration": {
  "Global":        { "PermitLimit": 600, "WindowInSeconds": 60 },
  "Public":        { "PermitLimit":  30, "WindowInSeconds": 60 },
  "Authenticated": { "PermitLimit": 200, "WindowInSeconds": 60 }
}
```

Numbers above are starting points — tune per environment. Dev can be generous; prod should mirror real traffic P95.

## Policy name constants (`{ServiceName}.Models/Constants/RateLimiterConstants.cs`)

```csharp
namespace {ServiceName}.Models.Constants;

public static class RateLimiterConstants
{
    public const string Public        = "public";
    public const string Authenticated = "authenticated";
}
```

`Global` has no constant because it is attached to `options.GlobalLimiter` rather than registered as a named policy.

## `RateLimiterInstaller` (`{ServiceName}.Api/Installers/RateLimiterInstaller.cs`)

```csharp
using System.Threading.RateLimiting;
using {ServiceName}.Models.Base;
using {ServiceName}.Models.Constants;

namespace {ServiceName}.Api.Installers;

public static class RateLimiterInstaller
{
    public static IServiceCollection AddRateLimiterInstaller(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var cfg = configuration.GetSection("RateLimitConfiguration").Get<RateLimitConfiguration>()
            ?? throw new InvalidOperationException("RateLimitConfiguration missing.");

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            // --- Global: applied to every request ---
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
            {
                var ip = ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                return RateLimitPartition.GetFixedWindowLimiter(ip, _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = cfg.Global.PermitLimit,
                    Window      = TimeSpan.FromSeconds(cfg.Global.WindowInSeconds)
                });
            });

            // --- Public: opt-in policy for unauthenticated endpoints ---
            options.AddPolicy(RateLimiterConstants.Public, ctx =>
            {
                var ip = ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                return RateLimitPartition.GetFixedWindowLimiter(ip, _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = cfg.Public.PermitLimit,
                    Window      = TimeSpan.FromSeconds(cfg.Public.WindowInSeconds)
                });
            });

            // --- Authenticated: opt-in policy keyed on `sub` claim ---
            options.AddPolicy(RateLimiterConstants.Authenticated, ctx =>
            {
                var userId = ctx.User.FindFirst("sub")?.Value;
                return RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: userId ?? ctx.Connection.RemoteIpAddress?.ToString() ?? "anon",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = cfg.Authenticated.PermitLimit,
                        Window      = TimeSpan.FromSeconds(cfg.Authenticated.WindowInSeconds)
                    });
            });
        });

        return services;
    }
}
```

## Middleware placement

```csharp
// Program.cs — order matters
app.UseRouting();
app.UseAuthentication();   // populates ctx.User.FindFirst("sub")
app.UseAuthorization();
app.UseRateLimiter();      // must come AFTER Authentication for subject partitioning
app.MapGraphQL().RequireRateLimiting(RateLimiterConstants.Authenticated);
```

**Invariant:** `UseRateLimiter()` must run after `UseAuthentication()` — otherwise the `Authenticated` policy degrades to IP partitioning (since `ctx.User` is anonymous).

## Attaching policies to endpoints

**REST / Minimal APIs:**

```csharp
app.MapPost("/auth/login",        LoginHandler).RequireRateLimiting(RateLimiterConstants.Public);
app.MapGet ("/wallet/transactions", TxHandler).RequireRateLimiting(RateLimiterConstants.Authenticated);
```

**GraphQL (per-field):**

```csharp
public class WalletMutations : MutationBase
{
    [EnableRateLimiting(RateLimiterConstants.Authenticated)]
    public async Task<TransactionDto> SendMoney(...) { ... }
}
```

**GraphQL (whole schema):** `MapGraphQL().RequireRateLimiting(RateLimiterConstants.Authenticated)` applies to every operation — but this blocks unauthenticated `login`-style mutations. Prefer per-field descriptors.

## Partition-key strategy cheat sheet

| Policy | Partition key | Rationale |
|---|---|---|
| `Global` | Remote IP | Catch-all DoS defense; works pre-auth |
| `Public` | Remote IP | Applied to `login` / `forgot-password` / similar — no `sub` yet |
| `Authenticated` | `sub` claim (fallback to IP) | Per-user fairness; survives IP rotation |

**Why `sub` and not `user_details` / `company_id`?**

- `sub` is the **subject** identifier — stable across sessions, present in every OIDC token by spec.
- `company_id` is per-tenant — limits would be shared across all users of a big tenant, which is the opposite of what we want.

## Behind a reverse proxy / gateway

Remote IP as seen by Kestrel is the gateway's IP unless `ForwardedHeaders` middleware is wired. Register it **before** `UseRateLimiter`:

```csharp
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
    KnownProxies     = { /* trusted gateway IPs */ }
});
```

Without this, every request appears to come from the gateway, collapsing all IP-partitioned traffic into a single bucket.

## Observability

The default rate-limiter middleware logs rejections at `Information`. Promote to `Warning` if you need them to surface in the default log filter:

```csharp
services.AddRateLimiter(options =>
{
    options.OnRejected = (ctx, _) =>
    {
        var log = ctx.HttpContext.RequestServices
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("RateLimiter");
        log.LogWarning("Rate-limited {Path} for partition {Ip}/{Sub}",
            ctx.HttpContext.Request.Path,
            ctx.HttpContext.Connection.RemoteIpAddress,
            ctx.HttpContext.User.FindFirst("sub")?.Value);
        ctx.HttpContext.Response.Headers["Retry-After"] = "60";
        return ValueTask.CompletedTask;
    };
    // ... policies ...
});
```

## Common mistakes

1. **Registering rate limiter before authentication** — Authenticated policy silently degrades to anonymous IP partitioning.
2. **Using company_id as the partition key** — creates tenant-shared buckets; one heavy user on a big tenant blocks the rest.
3. **Applying `RequireRateLimiting` on the GraphQL endpoint root** — blocks unauthenticated mutations like login; prefer per-field descriptors.
4. **Hardcoding permit/window values** — tuning per environment becomes impossible without a redeploy.
5. **Forgetting forwarded headers behind a gateway** — all traffic partitions into one bucket and triggers 429 storms.
6. **Naming the installer extension `AddAddRateLimiterInstaller`** — typo from copy-paste; keep it `AddRateLimiterInstaller`.

## Future evolution: distributed rate limiting

The built-in limiter is per-instance in-memory. For a multi-pod deployment, either:

- Accept per-pod limits (`PermitLimit × podCount` effective ceiling) — current Saar-Wallet stance.
- Move to a Redis-backed sliding window (`StackExchange.Redis` + a custom `PartitionedRateLimiter`) — adopt only after measuring real breaches.

## Related skills

- `08-jwt-authentication-dual-scheme.md` — why `ctx.User.FindFirst("sub")` is populated.
- `12-api-installer-pattern-and-startup.md` — middleware ordering.
