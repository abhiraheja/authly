---
name: Solution layout & reference chain
description: The 6-project layout every Saarvix .NET backend service uses, the compile-time reference graph, and the csproj templates for each project.
type: skill-section
---

# Solution layout & reference chain

## When to use

Every new Saarvix backend service starts here. Create these six projects in this exact order (dependencies first) using the csproj templates below.

## Project graph

```
{ServiceName}.Models            (leaf — no project refs)
        ▲
{ServiceName}.Domain            ← references Models
        ▲
{ServiceName}.Application       ← references Domain
        ▲                                ▲
{ServiceName}.AzureServiceBus   ────────┤ (also references Application)
        ▲
{ServiceName}.Infrastructure    ← references Application + AzureServiceBus
        ▲
{ServiceName}.Api               ← references Infrastructure
```

**Rule:** `Api` is the only project with `Microsoft.NET.Sdk.Web`. Everything else is `Microsoft.NET.Sdk`.

**Rule:** Api must reference Infrastructure and nothing else. Any temptation to add `ProjectReference Include="../{ServiceName}.Application"` in Api is a code smell — route access through Infrastructure or through MediatR.

## Layer responsibilities (read before writing ANY code in a layer)

| Layer | What goes here | What does NOT go here |
|---|---|---|
| **Models** | DTOs, request/response envelopes, configuration POCOs (`XxxConfiguration`), string constants | Any behavior, any Mongo attribute, any HotChocolate attribute beyond `UploadType` re-exports |
| **Domain** | Entities (with BSON attrs), `DocumentBase` / `CompanyDocumentBase`, value objects, domain enums, serialization config | MediatR handlers, FluentValidation, GraphQL types, business rules that need external services |
| **Application** | MediatR commands/queries/handlers/notifications, `IValidator<T>` validators, Mapster `IRegister` profiles, `IObjectType` / `FilterInputType` / `SortInputType`, `IRepositoryBase<T>` & `ICompanyRepositoryBase<T>` **interfaces**, producer **interfaces**, `IIndex` / `IMongoView`, pipeline behaviors, helpers | Mongo driver calls, HTTP calls, service bus clients, anything vendor-specific |
| **AzureServiceBus** | `ServiceBusProducerService<T>` base, `ServiceBusConsumerService<T,TMsg>` base (`BackgroundService`), concrete producers, concrete consumer hosted services, `AzureServiceBusInstaller` | Business rules, Mongo access |
| **Infrastructure** | `RepositoryBase<T>` / `CompanyRepositoryBase<T>` implementations, external API clients, Mongo connection wiring, `InfrastructureInstaller` as composition root | Auth policies, GraphQL types, HTTP pipeline |
| **Api** | `Program.cs`, installer extension methods (`{Concern}Installer`), authorization requirements/handlers/attributes/policies, `QueryBase` / `MutationBase` empty marker types, global GraphQL helpers (`UtcDateTimeScalar`, `DisableIntrospectionMiddleware`, `HeaderRequestInterceptor`) | Business logic, Mongo access, validators |

## csproj templates

### `{ServiceName}.Models.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="HotChocolate.Abstractions" Version="15.1.12" />
    <PackageReference Include="Microsoft.AspNetCore.Http.Abstractions" Version="2.3.9" />
    <PackageReference Include="Microsoft.AspNetCore.Http.Features" Version="5.0.17" />
    <PackageReference Include="Newtonsoft.Json" Version="13.0.4" />
    <PackageReference Include="Saar_Packages.Models" Version="1.21.0" />
  </ItemGroup>

</Project>
```

**Why HotChocolate.Abstractions in Models?** So that `UploadType`/`IFile` references in shared DTOs compile. Keep this the ONLY HotChocolate reference in Models — the server packages belong in Application.

**Why legacy AspNetCore.Http.Abstractions/Features?** They are pinned by `Saar_Packages.Models`. Do not remove.

### `{ServiceName}.Domain.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="MongoDB.Driver" Version="3.7.1" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="../{ServiceName}.Models/{ServiceName}.Models.csproj" />
  </ItemGroup>

</Project>
```

**Domain is deliberately Mongo-aware.** BSON attributes (`[BsonId]`, `[BsonElement("field")]`, `[BsonRepresentation(BsonType.ObjectId)]`) live on entities in Domain. This is a pragmatic trade-off — we get a single mapping location and no separate persistence model.

### `{ServiceName}.Application.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Graph" Version="5.103.0" />
    <PackageReference Include="Mapster" Version="10.0.4" />
    <PackageReference Include="Mapster.DependencyInjection" Version="10.0.4" />
    <PackageReference Include="FluentValidation.DependencyInjectionExtensions" Version="12.1.1" />
    <PackageReference Include="HotChocolate.AspNetCore" Version="15.1.12" />
    <PackageReference Include="HotChocolate.AspNetCore.Authorization" Version="15.1.12" />
    <PackageReference Include="HotChocolate.Data.MongoDb" Version="15.1.12" />
    <PackageReference Include="Saar_Packages.MediatR" Version="1.2.0" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="../{ServiceName}.Domain/{ServiceName}.Domain.csproj" />
  </ItemGroup>

</Project>
```

**Why `HotChocolate.Data.MongoDb` in Application?** Because `FilterInputType<>` / `SortInputType<>` and the Mongo filter/sort/project provider registration all need the v15 server packages where the types live. Handlers must return `IExecutable<T>` so HotChocolate can push filter/sort/project down to Mongo.

### `{ServiceName}.AzureServiceBus.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Azure.Messaging.ServiceBus" Version="7.20.1" />
    <PackageReference Include="Azure.Identity" Version="1.20.0" />
    <PackageReference Include="Azure.Storage.Blobs" Version="12.27.0" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="../{ServiceName}.Application/{ServiceName}.Application.csproj" />
  </ItemGroup>

</Project>
```

### `{ServiceName}.Infrastructure.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="../{ServiceName}.Application/{ServiceName}.Application.csproj" />
    <ProjectReference Include="../{ServiceName}.AzureServiceBus/{ServiceName}.AzureServiceBus.csproj" />
  </ItemGroup>

</Project>
```

Infrastructure ships no direct NuGet packages of its own — everything is transitive.

### `{ServiceName}.Api.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Azure.Extensions.AspNetCore.Configuration.Secrets" Version="1.5.0" />
    <PackageReference Include="Azure.Security.KeyVault.Secrets" Version="4.9.0" />
    <PackageReference Include="MongoDB.Driver.Core.Extensions.DiagnosticSources" Version="3.0.0" />
    <PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="10.0.5" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="../{ServiceName}.Infrastructure/{ServiceName}.Infrastructure.csproj" />
  </ItemGroup>

</Project>
```

## `{ServiceName}.slnx`

```xml
<Solution>
  <Project Path="{ServiceName}.Api/{ServiceName}.Api.csproj" />
  <Project Path="{ServiceName}.Application/{ServiceName}.Application.csproj" />
  <Project Path="{ServiceName}.AzureServiceBus/{ServiceName}.AzureServiceBus.csproj" />
  <Project Path="{ServiceName}.Domain/{ServiceName}.Domain.csproj" />
  <Project Path="{ServiceName}.Infrastructure/{ServiceName}.Infrastructure.csproj" />
  <Project Path="{ServiceName}.Models/{ServiceName}.Models.csproj" />
</Solution>
```

`slnx` is the modern XML-based solution format introduced in VS 17.13. Do not emit `.sln` files for new services.

## `nuget.config` at repo root

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

Commit this to source control. The PAT is injected by the Azure DevOps pipeline at build time — never commit a PAT. Central Package Management (`Directory.Packages.props`) is NOT used in Saarvix backends; versions are pinned inline in each csproj so each project documents its own dependencies.

## When NOT to follow this layout

- Truly micro utilities (one endpoint, no persistence, no auth) can collapse Domain/Application/Infrastructure into a single "Core" project. In practice, no production Saarvix service has ever warranted this.
- Workers that have zero HTTP surface (pure consumers) still keep all six projects — the Api project hosts the health endpoint and the BackgroundService registrations via the standard installer pipeline.

## Related skills

- `02-domain-base-documents-and-multitenancy.md` for what actually lives in Domain.
- `13-shared-saar-packages.md` for what ships in the private packages and must not be re-declared here.
