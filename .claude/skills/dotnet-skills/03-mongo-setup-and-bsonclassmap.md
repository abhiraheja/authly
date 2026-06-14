---
name: MongoDB setup & BsonClassMap auto-registration
description: MongoClient/MongoDatabase singleton registration, replica-set-aware connection tuning, OpenTelemetry wiring, and the reflection-based DomainInstaller that registers all BSON class maps at startup.
type: skill-section
---

# MongoDB setup & BsonClassMap auto-registration

## When to use

Every Saarvix backend service that reads or writes MongoDB uses the exact `MongoDbInstaller` + `DomainInstaller` pair below. Do not introduce a second Mongo-client registration path.

## Architectural decisions

- **One `MongoClient` per process.** `MongoClient` is thread-safe and owns the connection pool. Creating a new one per request would destroy the pool and thrash TLS handshakes.
- **`IMongoDatabase` is also a singleton** derived from the singleton client. Acquiring the database object is allocation-only — no I/O.
- **Replica-set required** — all Saarvix Mongo deployments are replica sets so multi-document transactions are available. Write-concern `majority` and read-concern `majority` are the default for data safety.
- **`BsonClassMap` registration is assembly-scan based** via `[DocumentName]`, done once at startup in `DomainInstaller`.
- **OpenTelemetry activity instrumentation** is wired at client construction via `MongoDB.Driver.Core.Extensions.DiagnosticSources` so traces propagate through to APM.

## `MongoDbInstaller` (`{ServiceName}.Api/Installers/MongoDbInstaller.cs`)

```csharp
using MongoDB.Driver;
using MongoDB.Driver.Core.Extensions.DiagnosticSources;
using Saar_Packages.Models;

namespace {ServiceName}.Api.Installers;

public static class MongoDbInstaller
{
    public static IServiceCollection AddMongoDBInstaller(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetValue<string>("MongoDbConnectionString")
            ?? throw new InvalidOperationException("MongoDbConnectionString is not configured.");

        var databaseName = configuration.GetConnectionString("MongoDbName")
            ?? throw new InvalidOperationException("MongoDbName is not configured.");

        // Publish the bound config as a singleton so repos / health checks can reach it
        services.AddSingleton(new MongoDbConfiguration
        {
            ConnectionString = connectionString,
            DatabaseName     = databaseName,
        });

        // MongoClient — ONE instance for the entire app lifetime.
        // It is thread-safe and owns its own connection pool.
        services.AddSingleton<IMongoClient>(sp =>
        {
            var cfg      = sp.GetRequiredService<MongoDbConfiguration>();
            var settings = MongoClientSettings.FromConnectionString(cfg.ConnectionString);

            // --- Connection pool (production-tuned defaults) ---
            settings.MaxConnectionPoolSize   = 100;
            settings.MinConnectionPoolSize   = 5;
            settings.ConnectTimeout          = TimeSpan.FromSeconds(10);
            settings.ServerSelectionTimeout  = TimeSpan.FromSeconds(30);
            settings.SocketTimeout           = TimeSpan.FromSeconds(30);
            settings.HeartbeatInterval       = TimeSpan.FromSeconds(10);
            settings.HeartbeatTimeout        = TimeSpan.FromSeconds(10);

            // --- Retry semantics — safe for replica-set primary elections ---
            settings.RetryWrites = true;
            settings.RetryReads  = true;

            // --- Consistency (replica-set) ---
            settings.ReadPreference = ReadPreference.Primary;
            settings.WriteConcern   = WriteConcern.WMajority;
            settings.ReadConcern    = ReadConcern.Majority;

            // --- OpenTelemetry / DiagnosticSource wiring ---
            settings.ClusterConfigurator = cb =>
                cb.Subscribe(new DiagnosticsActivityEventSubscriber(new InstrumentationOptions
                {
                    CaptureCommandText   = false,   // ⚠ PII leak risk if true
                    ShouldStartActivity  = _ => true
                }));

            return new MongoClient(settings);
        });

        // Lightweight — just navigates into a db from the singleton client.
        services.AddSingleton<IMongoDatabase>(sp =>
        {
            var client = sp.GetRequiredService<IMongoClient>();
            var cfg    = sp.GetRequiredService<MongoDbConfiguration>();
            return client.GetDatabase(cfg.DatabaseName);
        });

        return services;
    }
}
```

### Why these specific numbers?

| Setting | Default | Saarvix | Why |
|---|---|---|---|
| `MaxConnectionPoolSize` | 100 | 100 | Matches typical pod replica count × concurrent-request ceiling |
| `MinConnectionPoolSize` | 0 | 5 | Keeps a warm pool so the first request after a quiet period isn't slow |
| `ConnectTimeout` | 30s | 10s | Fail fast — our network is fine-tuned |
| `ServerSelectionTimeout` | 30s | 30s | Leaves room for primary election during rolling upgrades |
| `HeartbeatInterval` | 10s | 10s | Standard |
| `RetryWrites` / `RetryReads` | true | true | Required for replica-set transient primary election handling |
| `ReadPreference` | Primary | Primary | Read-your-writes consistency |
| `WriteConcern` | 1 | Majority | No silent data loss if a secondary is behind |
| `ReadConcern` | Local | Majority | Reads see only committed data |

### Why `CaptureCommandText = false`?

Because Mongo command text can include the **values** of filters (emails, tokens, tenant ids). Turning this on would leak PII into tracing backends. Keep it off in prod; flip it manually when debugging locally.

## `DomainInstaller` (`{ServiceName}.Domain/DomainInstaller.cs`) — BsonClassMap auto-registration

Mongo's C# driver requires every class it serializes to have a registered `BsonClassMap` **before the first deserialization**. Registering by hand is tedious and error-prone. We scan the Domain assembly for `[DocumentName]`-attributed classes and auto-map each one:

```csharp
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson.Serialization;
using {ServiceName}.Domain.Helper;

namespace {ServiceName}.Domain;

public static class DomainInstaller
{
    public static IServiceCollection AddDomainInstaller(this IServiceCollection services)
    {
        var documentClasses = Assembly.GetExecutingAssembly()
            .GetTypes()
            .Where(t => t.GetCustomAttribute<DocumentNameAttribute>() is not null);

        foreach (var docClass in documentClasses)
        {
            if (BsonClassMap.IsClassMapRegistered(docClass))
                continue;

            var map = new BsonClassMap(docClass);
            map.AutoMap();
            map.SetIgnoreExtraElements(true);   // forward-compat: older clients keep working after a new field ships
            BsonClassMap.RegisterClassMap(map);
        }

        return services;
    }
}
```

**Key rules:**

- `IgnoreExtraElements = true` is mandatory. Without it, a rolling deploy where pod A writes a new field and pod B (old) reads the document throws. This is non-negotiable for zero-downtime deploys.
- Scanning happens on the Domain assembly, not `AppDomain.CurrentDomain.GetAssemblies()`, because Domain is the only place entities live. Keeps the scan cheap and deterministic.
- Call `.AddDomainInstaller()` from `InfrastructureInstaller` so Api's `Program.cs` never imports Domain directly.

## Startup initializer: indexes & views (`MongoStartupInitializer`)

After the DI container is built, apply indexes and views by scanning loaded assemblies for `IIndex` and `IMongoView` implementations. This lives in Application.

```csharp
// {ServiceName}.Application/Indexes/Base/IIndex.cs
public interface IIndex { void CreateIndexes(); }

// {ServiceName}.Application/Views/Base/IMongoView.cs
public interface IMongoView { void CreateView(); }
```

**Implementation skeleton:** (shape — full reference code in the repo's `MongoStartupInitializer.cs`)

```csharp
public static IApplicationBuilder UseMongoStartupInitializer(this IApplicationBuilder app)
{
    using var scope  = app.ApplicationServices.CreateScope();
    var logger       = scope.ServiceProvider.GetRequiredService<ILogger<MongoStartupInitializerLogCategory>>();

    logger.LogInformation("🔧 Initializing MongoDB indexes and views...");

    RunAll<IIndex>(scope, logger,     i => i.CreateIndexes(), "index");
    RunAll<IMongoView>(scope, logger, v => v.CreateView(),    "view");

    return app;
}
```

Each `IIndex` implementation takes `IMongoDatabase` via constructor injection (or `ActivatorUtilities.CreateInstance` as a fallback) and defines its `CreateIndexes()`:

```csharp
public sealed class InvoiceIndexes(IMongoDatabase db) : IIndex
{
    public void CreateIndexes()
    {
        var col = db.GetCollection<Invoice>("invoices");
        col.Indexes.CreateMany(new[]
        {
            new CreateIndexModel<Invoice>(
                Builders<Invoice>.IndexKeys.Ascending(x => x.CompanyId).Ascending(x => x.Status),
                new CreateIndexOptions { Name = "ix_company_status" }),

            new CreateIndexModel<Invoice>(
                Builders<Invoice>.IndexKeys.Ascending(x => x.CompanyId).Descending(x => x.CreatedBy!.ChangedDate),
                new CreateIndexOptions { Name = "ix_company_createdAt" }),
        });
    }
}
```

**Index design convention:**

- Every index on a `CompanyDocumentBase` subtype must **lead with `CompanyId`** so the index is usable for the tenant-scoped repository queries.
- Name indexes explicitly (`ix_{field}_{modifier}`) — anonymous auto-named indexes are hard to reason about in Atlas.
- Do not rely on `BackgroundBuild` — large collections should have their indexes created during maintenance windows via Atlas UI, not at startup. The startup scan should be idempotent and cheap.

## Call-site composition

From `Program.cs`:

```csharp
services.AddMongoDBInstaller(configuration);
```

From `InfrastructureInstaller.AddInfrastructureServices(...)` (or equivalent composition point):

```csharp
services.AddDomainInstaller();
```

After `var app = builder.Build();`:

```csharp
app.UseMongoStartupInitializer();
```

## When NOT to follow this pattern

- A service that only talks to Mongo through a single aggregate and has no need for reflection-driven mapping can skip `DomainInstaller` and register its class map inline in one file. In practice, every service has grown beyond this within weeks, so start with the scan.
- A service that uses Cosmos DB with the Mongo API needs different retry semantics (`RetryWrites = false` with most Cosmos SKUs). Flag this in a comment and deviate only from the retry block.

## Common mistakes

1. **Calling `MongoClient` per request.** Causes pool exhaustion under load. Always singleton.
2. **Forgetting `SetIgnoreExtraElements(true)`.** First time you ship a new field, old pods blow up.
3. **Registering the class map twice.** `BsonClassMap.IsClassMapRegistered(...)` guards this. Do not remove the guard.
4. **`CaptureCommandText = true` in prod.** Leaks filter values into traces.

## Related skills

- `02-domain-base-documents-and-multitenancy.md` — the entity hierarchy these indexes target.
- `04-repository-base-pattern.md` — how the singleton `IMongoDatabase` flows into repositories.
- `12-api-installer-pattern-and-startup.md` — where `MongoDbInstaller` fits in the composition sequence.
