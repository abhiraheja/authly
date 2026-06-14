---
name: API installer pattern & Program.cs composition
description: The Saarvix installer convention (one extension method per concern), the fixed Program.cs composition order, and the non-negotiable middleware pipeline order with rationale for each boundary.
type: skill-section
---

# API installer pattern & Program.cs composition

## When to use

Every Saarvix `{ServiceName}.Api` project uses this pattern. Program.cs stays ~40 lines — each concern is a self-contained extension method in `{ServiceName}.Api/Installers/`.

## Architectural decisions

- **One installer per concern.** Key Vault, logging, Mongo, auth, rate limiter, GraphQL, etc. each get their own file. Resist the temptation to merge — small-and-focused beats clever-and-coupled.
- **Extension methods on `IServiceCollection`**, returning `IServiceCollection` to allow fluent chaining in Program.cs.
- **Naming: `Add{Concern}Installer` or `Add{Concern}Services`**. Both appear in the codebase; pick one and be consistent within a project. Avoid `AddAddRateLimiterInstaller` — that's a typo (one real case in this repo; fix it).
- **Ordering matters for config providers** (Key Vault must be first), **for DI** (Singletons before their consumers), and **for middleware** (Auth before Authorization before RateLimiter).
- **Installers that mutate `ConfigurationManager` take the concrete `ConfigurationManager` or `IConfigurationManager`**, not `IConfiguration`. Example: KeyVaultInstaller calls `configurations.AddAzureKeyVault(...)` — only valid on `ConfigurationManager`.
- **No business logic in installers.** They register services and wire options — never compute, query, or transform data.

## Installer naming + signatures

| Installer | Signature | Reason |
|---|---|---|
| `KeyVaultInstaller` | `ConfigurationManager configurations` | Mutates the config pipeline |
| `AuthInstaller` | `IConfigurationManager configurations` | Reads config early, before build |
| All others | `IConfiguration configuration` | Pure read |

## Reference `Program.cs`

```csharp
using {ServiceName}.Api.Installers;
using {ServiceName}.Application;
using {ServiceName}.AzureServiceBus;
using {ServiceName}.Domain;
using {ServiceName}.Infrastructure;
using {ServiceName}.Models.Constants;

var builder       = WebApplication.CreateBuilder(args);
var configuration = builder.Configuration;
var services      = builder.Services;

// --- Health / infra concerns ---
services.AddHealthChecks();

// --- Config pipeline (Key Vault first — must mutate ConfigurationManager before anything reads) ---
services.AddKeyVaultInstaller(configuration)
        .AddLoggingServices(configuration);

// --- Persistence + tenancy ---
services.AddMongoDBInstaller(configuration)
        .AddDomainInstaller()                       // BsonClassMap registration
        .AddInfrastructureServices(configuration);  // Repository DI

// --- Application layer (MediatR, FluentValidation, Mapster, pipeline behaviors) ---
services.AddApplicationInstaller();

// --- Messaging ---
services.AddAzureServiceBusInstaller(configuration);

// --- Security ---
services.AddAuthenticationServices(configuration)
        .AddSaarAuthorization();  // 5-dimensional authorization + dynamic policy provider

// --- Transport ---
services.AddProgramInstaller(configuration)         // FormOptions, small cross-cutting concerns
        .AddRateLimiterInstaller(configuration)
        .AddHttpContextAccessor();

services.AddGraphQLServer()
        .AddGraphqlService(services, configuration);

// --- CORS (gateway terminates in prod; relaxed here) ---
services.AddCors(options =>
{
    var allowedOrigins = configuration.GetSection("AllowedOrigins").Get<string[]>() ?? [];
    options.AddPolicy("Allow{ServiceName}App", policy =>
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod());
});

var app = builder.Build();

// --- Mongo startup: indexes + views (scan Application assembly) ---
app.UseMongoStartupInitializer();

// --- Middleware pipeline ---
app.UseRouting();
app.UseCors("Allow{ServiceName}App");
app.UseStaticFiles();
app.MapHealthChecks("/health").AllowAnonymous();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapGraphQL().RequireRateLimiting(RateLimiterConstants.Authenticated);

app.Run();
```

## Composition order — DI phase (`services.Add...`)

The order below is not arbitrary. Each step has a reason:

1. **`AddHealthChecks()`** — trivial, no dependencies.
2. **`AddKeyVaultInstaller`** — **must run first** among installers that read secrets. It mutates the `ConfigurationManager` so every subsequent `GetSection(...)` call transparently reads from the vault.
3. **`AddLoggingServices`** — before anything that logs during registration (e.g., the reflection scan in `AzureServiceBusInstaller` logs discovered producers).
4. **`AddMongoDBInstaller`** — before `AddDomainInstaller` (DomainInstaller doesn't use Mongo at DI time, but making the order explicit avoids future regressions).
5. **`AddDomainInstaller`** — before any repository registration, because `RepositoryBase` constructors hit `BsonClassMap` lookups.
6. **`AddInfrastructureServices`** — registers repository concretes against their interfaces. Runs after Mongo+Domain so it has both.
7. **`AddApplicationInstaller`** — wires MediatR + FluentValidation + Mapster + pipeline behaviors + IIndex/IMongoView scans.
8. **`AddAzureServiceBusInstaller`** — registers producers (scoped, against narrow interfaces) and consumers (singleton `IHostedService`). Runs after Application so consumers can `Publish` via MediatR.
9. **`AddAuthenticationServices`** + **`AddSaarAuthorization`** — auth DI must precede `AddGraphQLServer` so HotChocolate descriptor attributes can resolve policies.
10. **`AddRateLimiterInstaller`** — order-independent at DI time but conventionally placed after auth.
11. **`AddGraphQLServer().AddGraphqlService(...)`** — last, so everything it needs (auth policies, MediatR, repositories) is already registered.
12. **`AddCors`** — must be in DI before `UseCors` in the pipeline.

## Middleware order — pipeline phase (`app.Use...` / `app.Map...`)

**This order is non-negotiable.** Breaking it produces silent, hard-to-diagnose failures:

```csharp
app.UseMongoStartupInitializer();  // one-shot at startup

app.UseRouting();                  // (1) resolve route first; other middleware reads RouteData
app.UseCors("...");                // (2) CORS before auth — preflight (OPTIONS) must not hit auth
app.UseStaticFiles();              // (3) anonymous static assets served before auth overhead
app.MapHealthChecks(...)           // (4) health endpoint registered before auth
   .AllowAnonymous();
app.UseAuthentication();           // (5) populate ctx.User
app.UseAuthorization();            // (6) enforce [Authorize] — MUST be after Authentication
app.UseRateLimiter();              // (7) partition by `sub` claim — MUST be after Authentication
app.MapGraphQL().Require...;       // (8) endpoint registration — after all middleware
```

### Why each boundary matters

| Boundary | If violated | Symptom |
|---|---|---|
| Routing before everything | Routing data unavailable to middleware | `RouteData` is empty in middleware; endpoint-aware logic fails |
| CORS before Auth | Preflight `OPTIONS` rejected with 401 | Browsers silently fail, no error in Network tab beyond "CORS" |
| Auth before Authz | Authz has no principal | All `[Authorize]` endpoints return 401 instead of 403 |
| Auth before RateLimiter | Subject-partitioned bucket degrades to IP | One shared IP (gateway) = single global limit |
| HealthChecks mapped after auth | Probes fail with 401 | Kubernetes/Azure restarts pods unnecessarily |

## `ProgramInstaller` — cross-cutting transport concerns

`ProgramInstaller` is the catch-all for transport-layer concerns too small to warrant their own file:

```csharp
public static class ProgramInstaller
{
    public static IServiceCollection AddProgramInstaller(
        this IServiceCollection services,
        ConfigurationManager configurations)
    {
        services.Configure<FormOptions>(options =>
        {
            options.MultipartBodyLengthLimit = 52_428_800;  // 50MB — upload ceiling
        });

        // Future: request-timeout middleware options, default JsonSerializerOptions, etc.
        return services;
    }
}
```

**Rules:**

- If a concern grows past ~15 lines, graduate it to its own installer.
- Do not put business logic config here. Domain configs live in Application.

## `LoggerInstaller` — baseline logging

```csharp
public static IServiceCollection AddLoggingServices(
    this IServiceCollection services,
    IConfiguration configuration)
{
    services.AddLogging(logging =>
    {
        logging.ClearProviders();
        logging.AddConsole();
        logging.AddDebug();
        logging.AddConfiguration(configuration.GetSection("Logging"));
    });
    return services;
}
```

`ClearProviders()` is required because `WebApplication.CreateBuilder` pre-registers console + debug + EventSource, and we don't want EventSource listeners in prod. Add OpenTelemetry / Seq / App Insights here when ready.

## Naming discipline

| Good | Bad |
|---|---|
| `AddMongoDBInstaller` | `AddMongo`, `ConfigureMongo`, `MongoSetup` |
| `AddAuthenticationServices` | `AddAuth`, `SetupAuth`, `WireAuth` |
| `AddSaarAuthorization` | `AddSaarWalletorization` (real typo in repo — fix it) |
| `AddGraphqlService` | `UseGraphQL`, `ConfigureGraphQL` |

The installer name should be **unambiguous when chained** (`services.AddMongoDBInstaller(...).AddAuthenticationServices(...)`) — not just when read in isolation.

## Common mistakes

1. **Calling `AddKeyVaultInstaller` after another installer that reads secrets.** The secret values are `null` because the vault provider hasn't been added yet.
2. **Putting `UseCors` after `UseAuthentication`.** CORS preflight `OPTIONS` requests hit auth and return 401. Browsers display "CORS error" with no diagnostic.
3. **Mapping health checks inside an auth-required scope.** Kubernetes liveness probes fail; pods restart cyclically.
4. **Skipping `AddHttpContextAccessor`.** Anything that needs claims outside a controller action (repositories, handlers, services) gets a null context.
5. **Building a ServiceProvider inside an installer (`services.BuildServiceProvider()`).** This creates a second container — every singleton is instantiated twice. Only acceptable for startup-time one-shots (logging a discovery message), never for runtime resolution.
6. **Adding installers mid-pipeline after `var app = builder.Build()`.** The service collection is frozen. You'll get runtime errors.

## Minimal mandatory installer list

For a brand-new Saarvix service:

1. `KeyVaultInstaller`
2. `LoggerInstaller`
3. `MongoDbInstaller`
4. `DomainInstaller` (from Domain project)
5. `InfrastructureInstaller` (from Infrastructure project)
6. `ApplicationInstaller` (from Application project)
7. `AzureServiceBusInstaller` (from AzureServiceBus project)
8. `AuthInstaller`
9. `AuthorizationInstaller`
10. `RateLimiterInstaller`
11. `GraphqlInstaller`
12. `ProgramInstaller`

If any of these are missing from Program.cs, the service will compile but silently misbehave. Keep this list as a startup checklist.

## Related skills

- All the installer-specific skills (`03`, `05`, `06`, `07`, `08`, `09`, `10`, `11`).
- `01-solution-layout-and-reference-chain.md` — why installers in each layer can be invoked from Api without circular references.
