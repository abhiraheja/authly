---
name: Shared Saar_Packages (Models, MediatR) — what not to duplicate
description: The cross-service types shipped via Saarvix's private NuGet feed (Saar_Packages.Models, Saar_Packages.MediatR), what lives there vs locally, the lowercase-`r` AddMediatr quirk, and private-feed wiring.
type: skill-section
---

# Shared Saar_Packages — what not to duplicate

## When to use

Every Saarvix backend service references two shared NuGet packages from the private Azure DevOps feed:

- **`Saar_Packages.Models`** — cross-service POCOs (send-message envelope, email models, email-template constants, configuration records common to every service).
- **`Saar_Packages.MediatR`** — a custom MediatR distribution with a namespaced `IMediator` / `INotification` / handler interfaces under `Saar_Packages.MediatR.Interfaces`, plus an `AddMediatr` extension (note: lowercase `r` — real, not a typo).

**Do not** duplicate the types these packages ship. If you find yourself redefining `SendMessageModel<T>`, `IMediator`, or `JwtConfiguration` locally, stop and check the package first.

## Private feed — `nuget.config`

Every service root has this:

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <add key="SaarvixPackages"
         value="https://pkgs.dev.azure.com/avdesignworks/Saarvix/_packaging/SaarvixPackages/nuget/v3/index.json" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
</configuration>
```

**Important:** this file has **no credentials** in source. The PAT is injected by CI (`NUGET_AUTH_TOKEN` env var on Linux runners) or by a `packageSourceCredentials` block added at build time via the Azure DevOps Nuget task.

For local dev, developers authenticate once with the Azure Artifacts Credential Provider:

```bash
dotnet tool install -g Microsoft.CredentialProvider
az login
dotnet restore
```

## `Saar_Packages.Models` — inventory

Known types shipped in this package (confirm exact names by browsing the package on the feed; below is the observable surface from current usage):

| Namespace / Type | Purpose |
|---|---|
| `Saar_Packages.Models.SendMessageModel<T>` | Service Bus envelope (`Date`, `ChangedBy`, `Message`, `EventName`, ...) |
| `Saar_Packages.Models.JwtConfiguration` | JWT issuer/audience/secret POCO |
| `Saar_Packages.Models.MongoDbConfiguration` | Mongo connection + database POCO |
| `Saar_Packages.Models.AzureAdConfiguration` | Azure Entra / CIAM config POCO |
| `Saar_Packages.Models.SendEmailModel` | Email producer payload (subject, template, recipients, model JSON) |
| `Saar_Packages.Models.ToEmail`, `EmailRequest` | Recipient POCOs |
| `Saar_Packages.Models.RazorEngineTemplateConfiguration` | Email template folder path config |
| `Saar_Packages.Models.*TemplateModel` | Per-template view-model POCOs (verification, reset, welcome, ...) |
| `Saar_Packages.Models.EmailTemplatesConstants` | Constant template names |
| `Saar_Packages.Models.MessageTypeConstants` | Event-name constants (`EmailMessageEvent`, `CreateUserAccountEvent`, ...) |

### Rule: shared = contract

A type belongs in `Saar_Packages.Models` if **more than one service** needs it to interoperate. If only one service uses it, keep it local in `{ServiceName}.Models`.

**Examples:**

- `JwtConfiguration` → shared (every service validates tokens the same way).
- `SendMessageModel<T>` → shared (producer in service A, consumer in service B — they must agree on envelope).
- `RateLimitConfiguration` → local (each service tunes its own limits; no interoperation).
- Domain documents (`Invoice`, `Wallet`, `Transaction`) → NEVER shared — that would violate service boundaries.

### Anti-pattern: mirroring a shared type locally

```csharp
// ❌ DO NOT DO THIS in {ServiceName}.Models
namespace {ServiceName}.Models.Base;

public class JwtConfiguration   // silently shadows Saar_Packages.Models.JwtConfiguration
{
    public string Secret { get; set; } = "";
    // ... diverges over time ...
}
```

The compiler won't complain — both classes exist in different namespaces — but your service now drifts from the rest of Saarvix, and a consumer of your events will fail to deserialize.

## `Saar_Packages.MediatR` — inventory & quirks

This package is Saarvix's own wrapping around the MediatR concept. **It is not the NuGet `MediatR` package.** Expect these public types:

| Namespace / Type | Purpose |
|---|---|
| `Saar_Packages.MediatR.Interfaces.IMediator` | Entry point for `Send` / `Publish` |
| `Saar_Packages.MediatR.Interfaces.IRequest<TResponse>` | Command/query marker |
| `Saar_Packages.MediatR.Interfaces.IRequestHandler<TRequest, TResponse>` | Handler contract |
| `Saar_Packages.MediatR.Interfaces.INotification` | Broadcast marker |
| `Saar_Packages.MediatR.Interfaces.INotificationHandler<TNotification>` | Broadcast handler |
| `Saar_Packages.MediatR.Interfaces.IPipelineBehavior<TRequest, TResponse>` | Cross-cutting behavior |
| `Saar_Packages.MediatR.ServiceExtensions.AddMediatr(...)` | Registration extension (**lowercase `r`**) |

### The lowercase-`r` `AddMediatr` — real

```csharp
// {ServiceName}.Application/ApplicationInstaller.cs
using Saar_Packages.MediatR.ServiceExtensions;

services.AddMediatr(config =>
{
    config.RegisterServicesFromAssembly(typeof(ApplicationInstaller).Assembly);
});
```

**Do NOT "correct" this to `AddMediatR`.** The extension is genuinely named `AddMediatr`. A global find-replace rename would break every service. Document it, move on.

### Consuming MediatR in domain code

Handlers and notification consumers reference the `Saar_Packages.MediatR.Interfaces.*` types, not the public `MediatR` NuGet types:

```csharp
using Saar_Packages.MediatR.Interfaces;   // ← always this namespace

public sealed record CreateWalletCommand(Guid UserId) : IRequest<WalletDto>;

public sealed class CreateWalletCommandHandler
    : IRequestHandler<CreateWalletCommand, WalletDto>
{
    public Task<WalletDto> Handle(CreateWalletCommand cmd, CancellationToken ct)
    { /* ... */ }
}
```

### Consuming in Service Bus consumers

The consumer base class resolves `Saar_Packages.MediatR.Interfaces.IMediator` from the scope:

```csharp
using var scope    = _serviceProvider.CreateScope();
var       mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
await mediator.Publish(notification, ct);
```

**`ConsumerNotification`** — a `Saar_Packages.MediatR.Interfaces.INotification` base class carrying transport metadata (`MessageId`, `EventTypeId`, `Source`, `ChangedBy`, `Timestamp`). Specific event notifications derive from this to inherit the envelope.

## Package versioning

- Both packages follow `Major.Minor.Patch`. Current observed: `Saar_Packages.Models 1.x`, `Saar_Packages.MediatR 1.2.0`.
- **Upgrade all services together.** A consumer on v1.1 reading events published with v1.2 schema changes may silently deserialize wrong.
- Breaking changes require a coordinated bump across every service repo + a deploy plan. Don't bump one service unilaterally.

## When to contribute TO `Saar_Packages` vs stay local

| Scenario | Goes in Saar_Packages | Stays local |
|---|---|---|
| Envelope/schema used by producer + consumer across services | ✓ | |
| Constants referenced by multiple services | ✓ | |
| Configuration POCO representing org-wide contract (JWT, CIAM, ...) | ✓ | |
| Configuration POCO for service-specific tuning | | ✓ |
| Domain entities / aggregates | | ✓ (NEVER shared) |
| Utility helpers used in one service | | ✓ |
| Utility helpers used in ≥2 services | ✓ | |

## Project reference check

In every `.csproj` that uses these packages:

```xml
<ItemGroup>
  <PackageReference Include="Saar_Packages.Models"   Version="1.x.x" />
  <PackageReference Include="Saar_Packages.MediatR"  Version="1.2.0" />
</ItemGroup>
```

`Saar_Packages.Models` is referenced by Models, Domain, Application, AzureServiceBus, Api. `Saar_Packages.MediatR` is referenced by Application (handlers), AzureServiceBus (consumer base), Api (if mediator is used in middleware/resolvers).

## Common mistakes

1. **Installing the public `MediatR` NuGet alongside `Saar_Packages.MediatR`.** Type name collisions and ambiguous `AddMediatR` vs `AddMediatr` resolution. Uninstall the public package.
2. **Duplicating `SendMessageModel<T>` locally** because the name is generic and easy to miss. Always `using Saar_Packages.Models;` first.
3. **Committing a PAT into `nuget.config`.** PATs belong in CI secrets or the Azure Artifacts Credential Provider cache.
4. **Bumping `Saar_Packages.Models` in one service without bumping others.** Producer/consumer drift leads to silent deserialization failures.
5. **Putting service-specific types (entities, rate-limit POCOs) into `Saar_Packages.Models`.** Pollutes the shared contract surface; everyone has to upgrade every time you change a local type.

## Related skills

- `05-mediatr-and-pipeline-behaviors.md` — how `AddMediatr` is wired in the Application installer.
- `11-azure-service-bus-producers-and-consumers.md` — how `SendMessageModel<T>` flows through the bus.
- `01-solution-layout-and-reference-chain.md` — the reference graph that dictates where these packages must be referenced.
