---
name: Saarvix .NET GraphQL Backend — Reference Architecture
description: Canonical pattern for building a new Saarvix backend service. Use whenever scaffolding a new Saarvix .NET + GraphQL + MongoDB + Azure Service Bus service from scratch.
type: skill
---

# Saarvix .NET GraphQL Backend — Reference Architecture

## When to use this skill

Use this skill (and every file it indexes) whenever you are:

- Scaffolding a **brand-new Saarvix backend service** from scratch.
- Adding a **major cross-cutting concern** (auth, rate limiting, service bus, Mongo setup) to an existing Saarvix service.
- Auditing an existing Saarvix service against the canonical conventions to find drift.

Every pattern in this skill has been extracted from a shipped Saarvix service and is intended to be **re-used verbatim**, with placeholders substituted. Do not improvise.

## Tech stack (locked)

| Concern | Choice |
|---|---|
| Runtime | ASP.NET Core on `.NET 10` |
| Architecture | DDD + CQRS (MediatR) + Clean Layering |
| GraphQL | HotChocolate v15.1.x |
| Persistence | MongoDB 3.7.x driver, replica-set required |
| Messaging | Azure Service Bus (topic/subscription), blob-offload for large payloads |
| Auth | Microsoft Entra External ID (CIAM) JWT + optional ApiKey via `SmartScheme` |
| Authorization | Custom 5-dimensional policy provider on top of HotChocolate descriptor attributes |
| Validation | FluentValidation v12 via MediatR pipeline behavior |
| Object mapping | Mapster v10 + `IRegister` profiles |
| Secrets | Azure Key Vault via `DefaultAzureCredential` |
| CI/CD | Azure DevOps → private NuGet feed → Docker registry → SSH + `docker compose` |

## Project layout (6 projects, flat — no `src/` folder)

```
{ServiceName}.slnx
├── {ServiceName}.Models            (leaf — DTOs, config POCOs, constants)
├── {ServiceName}.Domain            → Models                        (entities, base docs, Mongo serialization)
├── {ServiceName}.Application       → Domain                        (CQRS, validators, Mapster, GraphQL types, repository INTERFACES, producer INTERFACES)
├── {ServiceName}.AzureServiceBus   → Application                   (producer/consumer base + implementations)
├── {ServiceName}.Infrastructure    → Application + AzureServiceBus (repository IMPLEMENTATIONS)
└── {ServiceName}.Api (Web SDK)     → Infrastructure                (Program.cs, installers, authorization policies, GraphQL base types)
```

**Compile-time reference chain:** `Models ← Domain ← Application ← { AzureServiceBus, Infrastructure } ← Api`.
**Runtime flow (sync):** `Client → Api → Application (MediatR) → Domain → Infrastructure → MongoDB`.
**Runtime flow (async):** `Application → Producer → Azure Service Bus topic → Consumer (BackgroundService) → MediatR.Publish → Notification handlers`.

Api only ever references `Infrastructure`. Everything else flows via transitive closure.

## Index of skill files

Read these in roughly the order given — each builds on the earlier ones.

| # | File | What it covers |
|---|---|---|
| 01 | [Solution layout & reference chain](01-solution-layout-and-reference-chain.md) | Project graph, csproj templates, nuget.config, layer responsibilities |
| 02 | [Domain base documents & multi-tenancy](02-domain-base-documents-and-multitenancy.md) | `DocumentBase`, `CompanyDocumentBase`, `ChangedByModel`, `[DocumentName]`, tenant invariant |
| 03 | [MongoDB setup & BsonClassMap auto-registration](03-mongo-setup-and-bsonclassmap.md) | `IMongoClient` singleton, pool/concerns tuning, OTel, `DomainInstaller` reflection scan |
| 04 | [Repository base pattern](04-repository-base-pattern.md) | `IRepositoryBase<T>` / `ICompanyRepositoryBase<T>`, `IExecutable<T>` push-down, soft delete, auto-audit, tenant filter |
| 05 | [MediatR & pipeline behaviors](05-mediatr-and-pipeline-behaviors.md) | `Saar_Packages.MediatR.AddMediatr`, `ValidationBehavior`, `UnhandledExceptionBehavior`, `ValidatorException : GraphQLException` |
| 06 | [HotChocolate GraphQL baseline](06-hotchocolate-graphql-baseline.md) | Query/Mutation slice via `[ExtendObjectType]`, QueryBase/MutationBase scan, paging/cost/filtering defaults, `UtcDateTimeScalar`, introspection gate |
| 07 | [Five-dimensional authorization](07-five-dimensional-authorization.md) | `UserDetails` / `CompanyDetails` / `ApplicationType` / `Permission` / `SubRole` requirements, handlers, dynamic policy provider, descriptor attributes |
| 08 | [JWT authentication (dual scheme)](08-jwt-authentication-dual-scheme.md) | `SmartScheme` policy scheme, `JwtBearer` with `TokenValidationParameters`, CIAM authority resolution, event hooks |
| 09 | [Rate limiting (three policies)](09-rate-limiting-three-policies.md) | Global / Public / Authenticated fixed-window partitioning |
| 10 | [Azure Key Vault & configuration binding](10-azure-keyvault-and-configuration.md) | `AddAzureKeyVault`, `DefaultAzureCredential`, `{Xxx}Configuration` POCO pattern |
| 11 | [Azure Service Bus producers & consumers](11-azure-service-bus-producers-and-consumers.md) | Producer base with blob offload, consumer `BackgroundService` with SSRF guard, auto-registration |
| 12 | [Api installer pattern & startup](12-api-installer-pattern-and-startup.md) | One installer per concern, `Program.cs` skeleton, middleware order |
| 13 | [Shared Saar_Packages references](13-shared-saar-packages.md) | `Saar_Packages.Models`, `Saar_Packages.MediatR` — quirks & what NOT to duplicate |
| 14 | [CI/CD pipeline](14-cicd-pipeline.md) | Azure Pipelines YAML skeleton, version tag scheme, private feed auth, multi-stage Dockerfile, SSH deploy |
| 15 | [Helper utilities catalog](15-helper-utilities-catalog.md) | `EnumParsing`, `RandomHelpers.GenerateSecureRandomPassword`, `TimeStampHelper`, `StringEqualsCheck` |
| 16 | [MongoDB views and indexes](16-mongo-views-and-indexes.md) | `IMongoView` drop-recreate pattern, FTS `$reduce` field, `IIndex` partial-unique + `(companyId, isDeleted)` pattern |

## Non-negotiables (read before writing any code)

1. **Never reference a layer you don't compile-time-depend on.** Api can only call `AddInfrastructureServices()`. Infrastructure composes everything below it.
2. **Every `CompanyDocumentBase` query MUST be filtered by `CompanyId`.** The filter must live in the repository base — never at the handler. See file 02 + 04.
3. **Handlers never talk to `IMongoCollection<T>` directly.** They go through a typed repository that returns `IExecutable<T>`. This is mandatory for HotChocolate filter/sort/project push-down.
4. **Mutations/queries are sliced with `[ExtendObjectType(nameof(Query|Mutation))]`** — never one monolithic Query class. See file 06.
5. **Authorize at the resolver via the descriptor attribute family** (`[UserDetailsRequired]`, `[CompanyDetailsRequired]`, `[ApplicationTypeRequired(...)]`, `[RequirePermission("Module.Action")]`, `[RequireSubRole("admin","manager")]`). Never read claims ad-hoc inside resolvers.
6. **All cross-service types come from `Saar_Packages.*` — do not re-declare them locally.** See file 13.
7. **`FluentValidation` → `ValidationBehavior` → `ValidatorException` is the only path to surface validation failures.** The exception extends `GraphQLException` so errors are GraphQL-native. See file 05.
8. **No synchronous I/O in constructors.** Producer/consumer initialization uses lazy `EnsureInitializedAsync` with a semaphore. See file 11.
9. **Do not log secrets, partial tokens, or OTPs at Information level.** Ever.
10. **Configs live in `{ServiceName}.Models/Base/*Configuration.cs`** as POCOs; bind via `configuration.GetSection("Xxx").Get<XxxConfiguration>()`; register as singleton. See file 10.

## Portability

Every file in this skill uses `{ServiceName}` / `{YourModule}` / `{Entity}` placeholders. When scaffolding a new service, do a global find-replace:

- `{ServiceName}` → e.g. `SaarBilling`
- `{service-name}` → kebab-case, e.g. `saar-billing`
- `{module}` → e.g. `Invoices`
- `{entity}` → e.g. `Invoice`

## Cross-cutting gotchas encountered in practice

| Gotcha | File |
|---|---|
| `Saar_Packages.MediatR.AddMediatr(...)` uses lowercase `r` intentionally | 05, 13 |
| Generic `FilterInputType<>` / `SortInputType<>` detection must walk base-type chain, not just direct base | 06 |
| `IHttpContextAccessor` must be registered in `Program.cs` — HotChocolate doesn't add it for you | 04, 12 |
| Singleton authorization handlers must not capture scoped services | 07 |
| `BsonClassMap` registration must run **before** the first Mongo read — do it at startup via `DomainInstaller` | 03 |
| HotChocolate subscriptions `AddInMemorySubscriptions` breaks under horizontal scale — switch to Redis later | 06 |

## Memory cross-references

- `MEMORY.md → project_saar_wallet.md` — concrete instantiation of this architecture for one service.
- `MEMORY.md → project_saarvix_shared_packages.md` — what ships in `Saar_Packages.*`.
- `MEMORY.md → project_auth_model.md` — the 5-dim auth model as implemented.
- `MEMORY.md → project_multitenancy.md` — why `CompanyId` scoping must be at the repo layer.
