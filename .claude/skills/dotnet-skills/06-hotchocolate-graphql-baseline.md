---
name: HotChocolate GraphQL baseline
description: The Saarvix HotChocolate v15 setup — sliced Query/Mutation via [ExtendObjectType], assembly-scan QueryBase/MutationBase registration, Mongo filter/sort/project push-down, paging defaults, cost limits, UtcDateTimeScalar, and the introspection kill-switch.
type: skill-section
---

# HotChocolate GraphQL baseline

## When to use

Every Saarvix backend exposes its API as GraphQL over HotChocolate v15. REST is reserved for health checks, webhooks, and file downloads. All business operations are GraphQL queries / mutations / subscriptions.

## Slice pattern — `[ExtendObjectType(nameof(Query|Mutation))]`

There is exactly **one** `Query` class and **one** `Mutation` class per service, both empty marker types in the Api layer. Every feature contributes a *slice* via `[ExtendObjectType]`.

### Empty root markers (in `Api/Graphql/Base/`)

```csharp
namespace {ServiceName}.Graphql.Base;

public class Query    { }
public abstract class QueryBase    { }

public class Mutation { }
public abstract class MutationBase { }
```

- `Query` / `Mutation` are the root marker classes HotChocolate builds the schema from.
- `QueryBase` / `MutationBase` are abstract bases every slice extends so the installer can find them via assembly scan.

### A feature slice

```csharp
using HotChocolate.Types;
using {ServiceName}.Api.Authorization.Attributes;
using {ServiceName}.Api.Graphql.Base;
using {ServiceName}.Application.Interfaces.{Module};
using {ServiceName}.Domain.{Module};

namespace {ServiceName}.Api.Graphql.{Module};

[ExtendObjectType(nameof(Query))]
public class {Entity}Queries : QueryBase
{
    [CompanyDetailsRequired]
    [RequirePermission("{Module}.Read")]
    [UseFiltering]
    [UseSorting]
    public IExecutable<{Entity}> Get{Entities}(
        [Service] I{Entity}Repository repo)
        => repo.GetAll();

    [CompanyDetailsRequired]
    [RequirePermission("{Module}.Read")]
    [UseProjection]
    public IExecutable<{Entity}> Get{Entity}ById(
        [Service] I{Entity}Repository repo,
        string id)
        => repo.GetById(id);
}
```

```csharp
[ExtendObjectType(nameof(Mutation))]
public class {Entity}Mutations : MutationBase
{
    [CompanyDetailsRequired]
    [RequirePermission("{Module}.Manage")]
    public Task<Create{Entity}Response> Create{Entity}(
        [Service] IMediator mediator,
        Create{Entity}Command input,
        CancellationToken ct)
        => mediator.Send(input, ct);
}
```

**Why slices?** One monolithic Query class becomes unmergeable and unreviewable by ~20 fields. Per-feature slices are independently reviewable and move with their module.

## `GraphqlInstaller` (`{ServiceName}.Api/Installers/GraphqlInstaller.cs`)

```csharp
using System.Reflection;
using HotChocolate.AspNetCore;
using HotChocolate.AspNetCore.Serialization;
using HotChocolate.Execution.Configuration;
using HotChocolate.Types;
using {ServiceName}.Graphql.Base;
using {ServiceName}.Helpers;
using {ServiceName}.Models.Base;

namespace {ServiceName}.Api.Installers;

public static class GraphqlInstaller
{
    public static IRequestExecutorBuilder AddGraphqlService(
        this IRequestExecutorBuilder graphqlBuilder,
        IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddHttpResponseFormatter(new HttpResponseFormatterOptions
        {
            HttpTransportVersion = HttpTransportVersion.Legacy  // supports multipart/form-data file uploads
        });

        var graphqlConfig = configuration.GetSection("GraphqlConfiguration").Get<GraphqlConfiguration>()!;

        graphqlBuilder
            .AddQueryType(d => d.Name(nameof(Query)))
            .AddMutationType(d => d.Name(nameof(Mutation)))
            .AddInMemorySubscriptions()                       // ⚠ single-instance only; swap for Redis to scale horizontally
            .AddFiltering()
            .AddSorting()
            .AddProjections()
            .AddMongoDbFiltering()                             // pushes IExecutable<T> filters down to Mongo
            .AddMongoDbSorting()
            .AddMongoDbProjections()
            .AddAuthorization()                                // wires the 5-dimensional policies
            .AddType<UploadType>()
            .AddType<UtcDateTimeScalar>()
            .BindRuntimeType<DateTime,       UtcDateTimeScalar>()
            .BindRuntimeType<DateTimeOffset, UtcDateTimeScalar>()
            .ModifyCostOptions(o =>
            {
                o.EnforceCostLimits = true;
                o.MaxFieldCost      = 1_000;
                o.MaxTypeCost       = 1_000;
            })
            .ModifyPagingOptions(p =>
            {
                p.MaxPageSize       = 100;
                p.DefaultPageSize   = 20;
                p.IncludeTotalCount = true;
            })
            .AddHttpRequestInterceptor<HeaderRequestInterceptor>();  // see §"Request interceptor" below

        // Auto-register every QueryBase/MutationBase slice
        foreach (var t in Assembly.GetExecutingAssembly().GetTypes()
                     .Where(x => (x.IsAssignableTo(typeof(QueryBase)) || x.IsAssignableTo(typeof(MutationBase)))
                              && !x.IsAbstract))
        {
            services.AddScoped(t);
            graphqlBuilder.AddType(t);
        }

        if (!graphqlConfig.AllowIntrospection)
            graphqlBuilder.UseField<DisableIntrospectionMiddleware>();

        return graphqlBuilder;
    }
}
```

### Composition in `Program.cs`

```csharp
var graphqlBuilder = services
    .AddGraphQLServer()
    .AddGraphqlService(services, configuration);

// And after other slice registrations from Application:
//   ApplicationInstaller.AddApplication(services, graphqlBuilder);
```

## `IExecutable<T>` push-down — the rule

Resolvers backed by Mongo MUST return `IExecutable<T>`:

```csharp
public IExecutable<Invoice> GetInvoices([Service] IInvoiceRepository repo)
    => repo.GetAll();
```

This lets `@filtering`, `@sorting`, `@projections`, and the Mongo-specific providers compile the GraphQL arguments down to a native Mongo pipeline (`$match` + `$project` + `$sort` + pagination). Returning `Task<List<T>>` breaks this — everything runs in memory after a full collection fetch.

## Paging

Add pagination with the appropriate attribute:

- `[UsePaging]` — cursor-based (Relay connections). Default when you want `pageInfo { hasNextPage, endCursor }`.
- `[UseOffsetPaging]` — offset/limit. Use when clients need absolute page numbers.

Both honour the globals set in `ModifyPagingOptions` above (max 100, default 20, include total count).

```csharp
[UsePaging]
[UseFiltering]
[UseSorting]
public IExecutable<Invoice> GetInvoices([Service] IInvoiceRepository repo) => repo.GetAll();
```

## Cost limits

`ModifyCostOptions.EnforceCostLimits = true` activates HotChocolate's `@cost`-directive analysis. Field cost defaults to 10, type cost to 1. The ceiling (`MaxFieldCost = 1000`, `MaxTypeCost = 1000`) blocks deeply-nested or giant projection queries before they hit Mongo.

When a specific field legitimately needs more headroom, annotate it with `[Cost(<weight>)]` or raise the global ceilings — but raise with intent.

## `UtcDateTimeScalar` — the only date scalar you should use

ISO-8601 with `Z` suffix, round-trips both `DateTime` and `DateTimeOffset`:

```csharp
public sealed class UtcDateTimeScalar : ScalarType<DateTime, StringValueNode>
{
    public UtcDateTimeScalar() : base("DateTimeUtc")
    {
        Description = "ISO-8601 UTC date-time that always serializes with 'Z'.";
    }

    protected override DateTime ParseLiteral(StringValueNode valueSyntax)
    {
        if (DateTime.TryParse(valueSyntax.Value, null, DateTimeStyles.RoundtripKind, out var dt))
            return DateTime.SpecifyKind(dt.ToUniversalTime(), DateTimeKind.Utc);
        throw new SerializationException("Invalid DateTime format.", this);
    }

    protected override StringValueNode ParseValue(DateTime runtimeValue)
        => new(runtimeValue.ToUniversalTime().ToString("o"));

    public override IValueNode ParseResult(object? resultValue) => resultValue switch
    {
        null                      => NullValueNode.Default,
        DateTime dt               => new StringValueNode(dt.ToUniversalTime().ToString("o")),
        DateTimeOffset dto        => new StringValueNode(dto.UtcDateTime.ToString("o")),
        string s when DateTime.TryParse(s, null, DateTimeStyles.RoundtripKind, out var d)
                                  => new StringValueNode(d.ToUniversalTime().ToString("o")),
        _                         => throw new SerializationException("Cannot parse DateTime result.", this)
    };

    public override bool TrySerialize(object? runtimeValue, out object? resultValue) => runtimeValue switch
    {
        null                => (resultValue = null) is null,
        DateTime dt         => (resultValue = dt.ToUniversalTime().ToString("o")) is not null,
        DateTimeOffset dto  => (resultValue = dto.UtcDateTime.ToString("o")) is not null,
        _                   => (resultValue = null) is not null
    };

    public override bool TryDeserialize(object? resultValue, out object? runtimeValue) => resultValue switch
    {
        null => (runtimeValue = null) is null,
        string s when DateTime.TryParse(s, null, DateTimeStyles.RoundtripKind, out var dt)
             => (runtimeValue = DateTime.SpecifyKind(dt.ToUniversalTime(), DateTimeKind.Utc)) is not null,
        _    => (runtimeValue = null) is not null
    };
}
```

Bind both `DateTime` and `DateTimeOffset` to it (see `GraphqlInstaller`). Never rely on HotChocolate's default `DateTime` scalar — it will bite you with local-time drift.

## Introspection kill-switch

Introspection is fine in dev, off in prod. Drive it from `appsettings.{Env}.json`:

```json
{ "GraphqlConfiguration": { "AllowIntrospection": true } }
```

```csharp
public class DisableIntrospectionMiddleware
{
    private readonly FieldDelegate _next;
    public DisableIntrospectionMiddleware(FieldDelegate next) => _next = next;

    public async Task InvokeAsync(IMiddlewareContext context)
    {
        if (context.Selection.Field.Name.StartsWith("__"))
        {
            context.ReportError(ErrorBuilder.New()
                .SetMessage("Introspection is disabled.")
                .SetCode("INTROSPECTION_DISABLED")
                .Build());
            return;
        }
        await _next(context);
    }
}
```

Applied globally via `graphqlBuilder.UseField<DisableIntrospectionMiddleware>()` when introspection is disabled.

## `HeaderRequestInterceptor` — what it's for, and what it is NOT for

There's an interceptor that copies HTTP headers into the request pipeline. It must **only** expose non-security headers to resolvers as a `IRequestContext`-like scoped service. Do NOT let it write into `context.User.Identities` — that lets clients spoof claims like `company_id`, `sub`, `Permissions` and bypass the 5-dimensional authorization.

Safe shape:

```csharp
public class HeaderRequestInterceptor : DefaultHttpRequestInterceptor
{
    private static readonly HashSet<string> Whitelist = new(StringComparer.OrdinalIgnoreCase)
    {
        "x-correlation-id", "x-request-id", "x-client-app-version"
    };

    public override ValueTask OnCreateAsync(
        HttpContext context,
        IRequestExecutor requestExecutor,
        OperationRequestBuilder requestBuilder,
        CancellationToken ct)
    {
        // Expose whitelisted headers via ContextData — NEVER inject as user claims.
        var ctx = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var h in context.Request.Headers)
            if (Whitelist.Contains(h.Key))
                ctx[h.Key] = h.Value.FirstOrDefault();

        requestBuilder.SetContextData("request-headers", ctx);
        return base.OnCreateAsync(context, requestExecutor, requestBuilder, ct);
    }
}
```

**Anti-pattern to avoid** (do not do this — it lets any authenticated caller spoof tenant and permission claims):

```csharp
// ❌ INSECURE — never do this
var identity = new ClaimsIdentity();
foreach (var h in context.Request.Headers)
    identity.AddClaim(new Claim(h.Key, h.Value.FirstOrDefault() ?? ""));
context.User.AddIdentity(identity);
```

## Subscriptions

`AddInMemorySubscriptions()` works for a single-instance deployment. As soon as you scale horizontally, swap for Redis:

```csharp
// Future:
graphqlBuilder.AddRedisSubscriptions(_ => ConnectionMultiplexer.Connect(redisConn));
```

Until then, keep the in-memory registration but document the scale-up trigger.

## CORS & auth pipeline

GraphQL is mapped via `app.MapGraphQL()` after `UseAuthentication()` and `UseAuthorization()`. See file 12 for the full middleware order.

```csharp
app.UseRouting();
app.UseRateLimiter();
app.UseCors("{YourCorsPolicy}");
app.UseStaticFiles();
app.MapHealthChecks("/health").AllowAnonymous();
app.UseAuthentication();
app.UseAuthorization();
app.MapGraphQL();
```

## When NOT to follow this pattern

- A service with literally one query and one mutation can skip the slice pattern — but it's a few lines extra to start sliced, and you'll thank yourself when it grows.
- Resolvers that must return materialised shapes (e.g. analytics aggregations, cross-service joins) can return `Task<List<T>>` but should still set `[UsePaging]` with reasonable limits and accept that filtering won't push down to Mongo.

## Common mistakes

1. **Directly subclassing `Query` / `Mutation`.** Use `[ExtendObjectType(nameof(Query))] ... : QueryBase` — slices don't inherit from the marker.
2. **Returning `Task<List<T>>` from a resolver with `[UseFiltering]`.** Silently disables push-down; all data streams into memory.
3. **Skipping `UtcDateTimeScalar` binding.** Default `DateTime` scalar causes local-time bugs.
4. **Leaving `EnforceCostLimits = false`.** Opens a DoS vector via deep nesting.
5. **Leaving introspection on in production.** Exposes schema internals to anyone who can reach the endpoint.
6. **Using `HeaderRequestInterceptor` to stuff headers into `context.User`.** Authentication-claim spoofing vulnerability.
7. **Calling `context.User.FindFirst(...)` inside a resolver.** Use the descriptor attributes (`[CompanyDetailsRequired]`) instead.

## Related skills

- `04-repository-base-pattern.md` — where `IExecutable<T>` comes from.
- `07-five-dimensional-authorization.md` — the descriptor attributes that replace ad-hoc claim lookups.
- `12-api-installer-pattern-and-startup.md` — where `AddGraphqlService` fits in the overall composition.
