---
name: Azure Key Vault & configuration POCO pattern
description: Multi-vault AddAzureKeyVault wiring with DefaultAzureCredential, SecretClient singleton registration, and the strongly-typed `{Xxx}Configuration` POCO pattern used to bind appsettings sections.
type: skill-section
---

# Azure Key Vault & configuration POCO pattern

## When to use

Every Saarvix service fetches secrets (JWT signing keys, Mongo connection strings, Service Bus connection strings, external API keys) from Azure Key Vault at startup. The pattern below supports **multiple vaults** so a service can combine shared-org secrets with service-specific secrets.

## Architectural decisions

- **Secrets are merged into `IConfiguration` at bootstrap** via `configurations.AddAzureKeyVault(...)`. Downstream code binds config via `GetSection().Get<TConfig>()` — it never knows whether a value came from appsettings, env var, or Key Vault.
- **`DefaultAzureCredential`** is used uniformly. This probes, in order: environment vars → managed identity → Visual Studio → Azure CLI. Local devs authenticate via `az login`; pods authenticate via managed identity. Same code, zero local overrides.
- **`SecretClient` is registered as a singleton** so components that need secret rotation at runtime (rarely) can inject it. Most code reads via `IConfiguration` instead.
- **Multiple vaults supported** via a `KeyVaultName` string array in config. Later vaults override earlier ones on key collision — follow Azure's standard provider precedence.
- **POCO-binding over `IOptions<T>`** for simple, startup-only config. `IOptions<T>` is preferred only when values might change via `IOptionsMonitor<T>`; Saarvix services do not hot-reload config.

## `KeyVaultInstaller` (`{ServiceName}.Api/Installers/KeyVaultInstaller.cs`)

```csharp
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;

namespace {ServiceName}.Api.Installers;

public static class KeyVaultInstaller
{
    public static IServiceCollection AddKeyVaultInstaller(
        this IServiceCollection services,
        ConfigurationManager configurations)
    {
        var keyVaults = configurations.GetSection("KeyVaultName").Get<string[]>();
        if (keyVaults is null || keyVaults.Length == 0)
            return services; // dev/local environments can skip Key Vault entirely

        foreach (var kv in keyVaults)
        {
            var keyVaultUri = new Uri($"https://{kv}.vault.azure.net/");
            var credential  = new DefaultAzureCredential();

            configurations.AddAzureKeyVault(keyVaultUri, credential);
            services.AddSingleton(new SecretClient(keyVaultUri, credential));
        }

        return services;
    }
}
```

### `appsettings.json` shape

```json
{
  "KeyVaultName": [ "saarvix-shared", "{service-name}-kv" ]
}
```

For local dev, leave this array empty so the installer short-circuits. Secrets come from `appsettings.Development.json` or user secrets.

## Secret reference syntax in appsettings

Key Vault automatically backs the config provider once it's added — so a key like `JWT:Secret` inside the vault becomes `configuration["JWT:Secret"]` transparently. No `@Microsoft.KeyVault(...)` reference syntax is needed (that's an App Service construct, not the Azure.Identity SDK path).

**Convention:** flatten nested config keys with `--` in the Key Vault secret name because Key Vault secret names allow only alphanumeric and `-`:

| appsettings path | Key Vault secret name |
|---|---|
| `JWT:Secret` | `JWT--Secret` |
| `MongoDbConnectionString` | `MongoDbConnectionString` |
| `ConnectionStrings:MongoDbName` | `ConnectionStrings--MongoDbName` |

The default `AddAzureKeyVault` overload translates `--` ↔ `:` automatically via `AzureKeyVaultConfigurationOptions.Manager`.

## Configuration POCO pattern

Every configuration section binds to a strongly-typed class that lives in `{ServiceName}.Models/Base/`. The Models project is the leaf of the dependency graph (see `01-solution-layout-and-reference-chain.md`), so any project — Api, Application, Infrastructure, AzureServiceBus — can bind the same POCO without circular references.

### Example POCOs

```csharp
// {ServiceName}.Models/Base/JwtConfiguration.cs
namespace {ServiceName}.Models.Base;

public class JwtConfiguration
{
    public string Secret        { get; set; } = default!;
    public string ValidIssuer   { get; set; } = default!;
    public string ValidAudience { get; set; } = default!;
}

// {ServiceName}.Models/Base/MongoDbConfiguration.cs
public class MongoDbConfiguration
{
    public string ConnectionString { get; set; } = default!;
    public string DatabaseName     { get; set; } = default!;
}

// {ServiceName}.Models/Base/GraphqlConfiguration.cs
public class GraphqlConfiguration
{
    public bool   EnableIntrospection { get; set; }
    public int    MaxDepth            { get; set; } = 12;
    public int    MaxFieldCost        { get; set; } = 1000;
    public int    MaxTypeCost         { get; set; } = 1000;
}

// {ServiceName}.Models/Base/ServiceBusConfiguration.cs
public class ServiceBusConfiguration
{
    public string ConnectionString        { get; set; } = default!;
    public string BlobConnectionString    { get; set; } = default!;
    public string BlobContainer           { get; set; } = default!;
    public int    LargeMessageThreshold   { get; set; } = 256 * 1024;
}
```

### Binding + DI registration

Inside each installer that owns a section:

```csharp
var jwt = configuration.GetSection("JWT").Get<JwtConfiguration>()
    ?? throw new InvalidOperationException("JWT configuration section missing.");

services.AddSingleton(jwt);
```

**Rules:**

1. **Throw loudly on missing sections.** Silent `null` here creates NullReferenceExceptions later in request handling, which are harder to diagnose than a startup failure.
2. **Register as singleton, not `IOptions<T>`.** Avoids the per-request `Value` property accessor for config that never changes.
3. **One POCO per section.** Do not bind overlapping sections to the same POCO — it silently merges.
4. **Bind only in the installer that needs it.** Don't centralize binding in `Program.cs` or a shared "ConfigInstaller" — configs should be co-located with the installer that uses them.

## Using secrets from non-Api projects

Infrastructure / Application / AzureServiceBus projects receive bound POCOs via DI, not `IConfiguration`. This keeps business logic free of string keys.

```csharp
// Application/Services/TokenService.cs
public class TokenService(JwtConfiguration jwt)
{
    public string IssueToken(...) { /* uses jwt.Secret, jwt.ValidAudience */ }
}
```

**Anti-pattern:**

```csharp
public class TokenService(IConfiguration configuration)
{
    private readonly string _secret = configuration["JWT:Secret"]!;  // ❌ stringly-typed, no validation
}
```

## Rotating secrets at runtime

When a secret rotates in Key Vault, already-bound singletons still hold the old value. If you need true rotation:

1. Register `IOptionsMonitor<TConfig>` instead of a singleton.
2. Add `reloadOnChange: true` to the config provider.
3. In long-lived services (e.g., `ServiceBusConsumerService`), subscribe to `OnChange` to re-initialize clients.

**Saarvix default:** don't bother. Rotate via pod restart. It's simpler and the restart blast radius is already small.

## Local dev without Key Vault

```jsonc
// appsettings.Development.json
{
  "KeyVaultName": [],
  "JWT":  { "Secret": "dev-secret-not-for-prod", "ValidIssuer": "...", "ValidAudience": "..." },
  "MongoDbConnectionString": "mongodb://localhost:27017/?replicaSet=rs0"
}
```

Prefer `dotnet user-secrets` for anything genuinely sensitive even locally:

```bash
dotnet user-secrets set "JWT:Secret" "..." --project {ServiceName}.Api
```

## Common mistakes

1. **`configurations.AddAzureKeyVault(...)` called after `Build()`.** The `ConfigurationManager` must still be mutable — call the installer BEFORE `builder.Build()`.
2. **Using a separate `TokenCredential` per vault.** `DefaultAzureCredential` is cheap to construct but caches tokens per instance; reuse one credential across vaults for better caching.
3. **Hardcoding vault names in code.** Environments differ — always drive from config.
4. **Relying on `@Microsoft.KeyVault(...)` syntax.** That's an Azure App Service feature, not a Key Vault SDK feature. On AKS / self-hosted, it silently reads the literal string.
5. **Binding the same config POCO to multiple sections.** Creates invisible override behavior — use separate POCOs.

## Call-site composition

```csharp
// Program.cs — very early, before any other installer that reads secrets
builder.Services.AddKeyVaultInstaller(builder.Configuration);

// NOW safe:
builder.Services.AddMongoDBInstaller(builder.Configuration);
builder.Services.AddAuthenticationServices(builder.Configuration);
// ...
```

## Related skills

- `01-solution-layout-and-reference-chain.md` — why config POCOs live in Models.
- `12-api-installer-pattern-and-startup.md` — composition ordering of installers.
