---
name: Repository base pattern
description: The IRepositoryBase / ICompanyRepositoryBase contracts, their Mongo-backed implementations, IExecutable push-down for HotChocolate, soft-delete semantics, auto-audit, and tenant-scope enforcement.
type: skill-section
---

# Repository base pattern

## When to use

Every Mongo-persisted aggregate uses this pattern. There is no "direct `IMongoCollection<T>` in a handler" escape hatch — if you're tempted, add the method to the repository base.

## Two base interfaces, two base implementations

| If entity is … | Interface (Application) | Implementation (Infrastructure) |
|---|---|---|
| `DocumentBase` (cross-tenant) | `IRepositoryBase<TDocument>` | `RepositoryBase<TDocument>` |
| `CompanyDocumentBase` (tenant-scoped) | `ICompanyRepositoryBase<TDocument>` | `CompanyRepositoryBase<TDocument>` |

Interfaces live in Application (so handlers don't take a compile-time dependency on Mongo); implementations live in Infrastructure.

## Contract: `IRepositoryBase<TDocument>`

```csharp
using System.Linq.Expressions;
using {ServiceName}.Domain.Base;
using {ServiceName}.Domain.Individuals;

namespace {ServiceName}.Application.Interfaces.Base;

public interface IRepositoryBase<TDocument> where TDocument : DocumentBase
{
    string GenerateId();

    Task<TDocument> AddAsync(TDocument document, CancellationToken ct = default);
    Task<TDocument> UpdateAsync(TDocument document, CancellationToken ct = default);

    Task<bool> DeleteAsync(string id, CancellationToken ct = default);
    Task<bool> DeleteAsync(TDocument document, CancellationToken ct = default);
    Task<bool> HardDeleteAsync(string id, CancellationToken ct = default);

    IExecutable<TDocument> GetAll(bool includeDeleted = false);
    IExecutable<TDocument> GetById(string id, bool includeDeleted = false);
    IExecutable<TDocument> Find(Expression<Func<TDocument, bool>> predicate, bool includeDeleted = false);

    ChangedByModel GetChangedByModel();
}
```

## Contract: `ICompanyRepositoryBase<TDocument>`

```csharp
public interface ICompanyRepositoryBase<TDocument> where TDocument : CompanyDocumentBase
{
    string GenerateId();
    string GetCompanyId();

    Task<TDocument> AddAsync(TDocument document, string? companyId = null, CancellationToken ct = default);
    Task<TDocument> UpdateAsync(TDocument document, string? companyId = null, CancellationToken ct = default);
    Task<bool> DeleteAsync(string id, string? companyId = null, CancellationToken ct = default);
    Task<bool> DeleteAsync(TDocument document, string? companyId = null, CancellationToken ct = default);
    Task<bool> HardDeleteAsync(string id, string? companyId = null, CancellationToken ct = default);

    IExecutable<TDocument> GetAll(string? companyId = null, bool includeDeleted = false);
    IExecutable<TDocument> GetById(string id, string? companyId = null, bool includeDeleted = false);
    IExecutable<TDocument> Find(Expression<Func<TDocument, bool>> predicate, string? companyId = null, bool includeDeleted = false);

    IExecutable<TResult> AsExecutable<TResult>(
        Expression<Func<TDocument, TResult>> selector,
        Expression<Func<TDocument, bool>>? predicate = null,
        bool skipDeleteCheck = false,
        string? companyId = null);

    ChangedByModel GetChangedByModel();
}
```

**Why the optional `companyId` parameter?** For system-initiated flows (e.g. a consumer handling a cross-tenant event) where the tenant comes from the message, not the HTTP caller. Default is `null` → use caller's tenant.

## `IExecutable<T>` — the push-down contract

All read methods return `IExecutable<T>`, not `IQueryable<T>` or `List<T>`. This matters because HotChocolate's `[UseFiltering]`, `[UseSorting]`, `[UseProjection]` attributes only push down to Mongo when the resolver returns an `IExecutable<T>`. Returning a materialized list breaks filtering.

```csharp
// ✅ CORRECT — filters/sorts push down to Mongo
[UseFiltering]
[UseSorting]
public IExecutable<Invoice> GetInvoices(
    [Service] ICompanyRepositoryBase<Invoice> repo)
    => repo.GetAll();

// ❌ WRONG — materialises entire collection, filters run in memory
public async Task<List<Invoice>> GetInvoices(
    ICompanyRepositoryBase<Invoice> repo,
    CancellationToken ct)
    => await repo.GetAll().ToListAsync(ct);
```

## `RepositoryBase<TDocument>` — full reference implementation

```csharp
using System.Linq.Expressions;
using System.Reflection;
using HotChocolate;
using HotChocolate.Data;
using Microsoft.AspNetCore.Http;
using MongoDB.Bson;
using MongoDB.Driver;
using Saar_Packages.Models;
using Saar_Packages.Models.Helpers;
using {ServiceName}.Application.Exceptions;
using {ServiceName}.Application.Interfaces.Base;
using {ServiceName}.Domain.Base;
using {ServiceName}.Domain.Helper;
using {ServiceName}.Domain.Individuals;

namespace {ServiceName}.Infrastructure.Repositories.Base;

public abstract class RepositoryBase<TDocument> : IRepositoryBase<TDocument>
    where TDocument : DocumentBase
{
    protected readonly IMongoCollection<TDocument> _collection;

    protected readonly string _changedById;
    protected readonly string _changedByName;
    protected readonly string _changedByEmail;

    protected readonly UserContextModel? _user;
    protected readonly UserHeaderModel?  _headers;

    protected RepositoryBase(IMongoDatabase database, IHttpContextAccessor http)
    {
        _collection = database.GetCollection<TDocument>(GetCollectionName(typeof(TDocument)));

        _user    = http?.HttpContext?.User?.UserDetails();
        _headers = http?.HeaderDetails();

        _changedById    = _user?.Id    ?? "UNKNOWN";
        _changedByName  = _user?.Name  ?? "UNKNOWN";
        _changedByEmail = _user?.Email ?? "UNKNOWN";
    }

    private static string GetCollectionName(Type documentType)
        => documentType.GetCustomAttribute<DocumentNameAttribute>()?.Name ?? documentType.Name;

    public string GenerateId() => ObjectId.GenerateNewId().ToString();

    public virtual async Task<TDocument> AddAsync(TDocument document, CancellationToken ct = default)
    {
        try
        {
            if (document is null) throw new Exception("Entity is null");

            document.Id        ??= GenerateId();
            document.CreatedBy ??= BuildStamp();

            await _collection.InsertOneAsync(document, cancellationToken: ct);
            return document;
        }
        catch (Exception ex)
        {
            throw new ValidatorException(ex.Message);
        }
    }

    public virtual async Task<TDocument> UpdateAsync(TDocument document, CancellationToken ct = default)
    {
        try
        {
            if (document is null) throw new Exception("Entity is null");
            if (string.IsNullOrEmpty(document.Id)) throw new Exception("Id is null");

            document.UpdatedBy = BuildStamp();

            var result = await _collection.ReplaceOneAsync(x => x.Id == document.Id, document, cancellationToken: ct);
            if (result.MatchedCount == 0) throw new Exception("Document not found");

            return document;
        }
        catch (Exception ex)
        {
            throw new ValidatorException(ex.Message);
        }
    }

    public virtual async Task<bool> DeleteAsync(string id, CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrEmpty(id)) throw new Exception("Id is null");

            var update = Builders<TDocument>.Update
                .Set(x => x.IsDeleted, true)
                .Set(x => x.UpdatedBy, BuildStamp());

            var result = await _collection.UpdateOneAsync(
                x => x.Id == id && !x.IsDeleted,
                update,
                cancellationToken: ct);

            return result.MatchedCount > 0;
        }
        catch (Exception ex)
        {
            throw new ValidatorException(ex.Message);
        }
    }

    public virtual Task<bool> DeleteAsync(TDocument document, CancellationToken ct = default)
    {
        if (document is null || string.IsNullOrEmpty(document.Id))
            throw new ValidatorException("Invalid document");
        return DeleteAsync(document.Id, ct);
    }

    public virtual async Task<bool> HardDeleteAsync(string id, CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrEmpty(id)) throw new Exception("Id is null");

            var result = await _collection.DeleteOneAsync(x => x.Id == id, ct);
            return result.DeletedCount > 0;
        }
        catch (Exception ex)
        {
            throw new ValidatorException(ex.Message);
        }
    }

    public virtual IExecutable<TDocument> GetAll(bool includeDeleted = false)
    {
        var q = _collection.AsQueryable();
        if (!includeDeleted) q = q.Where(x => !x.IsDeleted);
        return q.AsExecutable();
    }

    public virtual IExecutable<TDocument> GetById(string id, bool includeDeleted = false)
    {
        var q = _collection.AsQueryable().Where(x => x.Id == id);
        if (!includeDeleted) q = q.Where(x => !x.IsDeleted);
        return q.AsExecutable();
    }

    public virtual IExecutable<TDocument> Find(Expression<Func<TDocument, bool>> predicate, bool includeDeleted = false)
    {
        var q = _collection.AsQueryable();
        if (!includeDeleted) q = q.Where(x => !x.IsDeleted);
        q = q.Where(predicate);
        return q.AsExecutable();
    }

    public virtual ChangedByModel GetChangedByModel() => BuildStamp();

    private ChangedByModel BuildStamp() => new()
    {
        ChangedByEmail = _changedByEmail,
        ChangedById    = _changedById,
        ChangedByName  = _changedByName,
        ChangedDate    = DateTimeOffset.Now
    };
}
```

## `CompanyRepositoryBase<TDocument>` — additions on top

The company-scoped base is structurally identical plus four additions:

1. Reads `companyId` from the current user into a `_companyId` field at construction.
2. Every `Where(...)` prepends `x.CompanyId == effectiveCompanyId` unless `_ignoreCompanyFilter == true`.
3. `_ignoreCompanyFilter` is **one-shot** — reset to `false` in a `finally` block on every query method. This makes cross-tenant opt-outs impossible to leak into the next call.
4. Optional `AsExecutable<TResult>(selector, predicate?, skipDeleteCheck?, companyId?)` for projection-only reads that need to return a projected type rather than the entity.

Example of the tenant filter in action:

```csharp
public virtual IExecutable<TDocument> GetAll(string? companyId = null, bool includeDeleted = false)
{
    try
    {
        var effective = companyId ?? _companyId;
        var q = _ignoreCompanyFilter
            ? _collection.AsQueryable()
            : _collection.AsQueryable().Where(x => x.CompanyId == effective);

        if (!includeDeleted) q = q.Where(x => !x.IsDeleted);
        return q.AsExecutable();
    }
    finally { _ignoreCompanyFilter = false; }
}
```

**`AddAsync` always forces the tenant:**

```csharp
var effective = companyId ?? _companyId;
document.CompanyId = effective;          // overwrites whatever the client sent
```

This is the single most important line in the file. Never let a client dictate the tenant.

## Concrete repository example

Per-entity concrete repositories subclass the right base. They're pure composition — no Mongo calls outside base methods.

```csharp
using MongoDB.Driver;
using Microsoft.AspNetCore.Http;
using {ServiceName}.Application.Interfaces.{Module};
using {ServiceName}.Domain.{Module};
using {ServiceName}.Infrastructure.Repositories.Base;

namespace {ServiceName}.Infrastructure.Repositories.{Module};

public sealed class InvoiceRepository : CompanyRepositoryBase<Invoice>, IInvoiceRepository
{
    public InvoiceRepository(IMongoDatabase database, IHttpContextAccessor http)
        : base(database, http) { }

    // entity-specific query
    public IExecutable<Invoice> GetOverdue(DateTime today)
        => Find(x => x.Status == InvoiceStatus.Unpaid && x.DueDate < today);
}
```

Notice the per-module interface `IInvoiceRepository` — it extends `ICompanyRepositoryBase<Invoice>` and adds only the methods that are genuinely entity-specific. Base methods are already there.

```csharp
namespace {ServiceName}.Application.Interfaces.{Module};

public interface IInvoiceRepository : ICompanyRepositoryBase<Invoice>
{
    IExecutable<Invoice> GetOverdue(DateTime today);
}
```

## DI registration (Infrastructure → `InfrastructureInstaller`)

Auto-scan concrete repositories and register them:

```csharp
public static IServiceCollection AddInfrastructureServices(this IServiceCollection services)
{
    var assembly = typeof(InfrastructureInstaller).Assembly;

    foreach (var impl in assembly.GetTypes()
                 .Where(t => t is { IsClass: true, IsAbstract: false }
                          && (t.BaseType?.IsGenericType ?? false)
                          && (t.BaseType.GetGenericTypeDefinition() == typeof(RepositoryBase<>)
                           || t.BaseType.GetGenericTypeDefinition() == typeof(CompanyRepositoryBase<>))))
    {
        // Register against every application-facing interface the impl declares
        foreach (var iface in impl.GetInterfaces()
                     .Where(i => i.Namespace?.Contains(".Application.Interfaces.") == true))
        {
            services.AddScoped(iface, impl);
        }
    }

    // Mongo setup + Domain BsonClassMap scan + AzureServiceBusInstaller are chained here
    services.AddDomainInstaller();
    // services.AddAzureServiceBusInstaller(configuration);  // passed in from caller if needed

    return services;
}
```

Scope lifetime is **scoped**, not singleton, because `HttpContextAccessor` resolution must happen per-request.

## Soft delete semantics

- `DeleteAsync(id)` — sets `IsDeleted = true`, stamps `UpdatedBy`. Returns true if a row was matched. **Never removes data.**
- `HardDeleteAsync(id)` — actually removes. Use only for GDPR purges, test fixtures, or administrative scrubs.
- All read methods default to `includeDeleted: false`. Explicit opt-in via `includeDeleted: true` for admin views / audit queries.

## Audit stamp semantics

- `CreatedBy` is set only if `null` — the repository never overwrites an existing creator (protects against migrations stomping history).
- `UpdatedBy` is overwritten on every write.
- `ChangedDate` is `DateTimeOffset.Now` on the host — accept the ~ms clock skew between pods for this purpose.
- `changedById == "UNKNOWN"` signals a system/unauthenticated write. Grep for it in production logs to spot holes.

## When NOT to follow this pattern

- A write-heavy hot-path that cannot tolerate a `ReplaceOneAsync` round-trip (e.g. high-volume event ingestion) should bypass `UpdateAsync` and use typed `Builders<T>.Update` directly at the repository layer — but still behind a method on the repository, not in the handler.
- Bulk operations (`InsertManyAsync`, `BulkWriteAsync`) should get first-class base methods when needed. Do not call the collection directly from a handler.
- A query that truly needs aggregation pipelines (`$lookup`, `$group`) lives on the concrete repository as a dedicated typed method — never inlined into a handler.

## Common mistakes

1. **Returning `Task<List<T>>` from a query resolver.** Breaks HotChocolate push-down. Return `IExecutable<T>`.
2. **Writing `_collection.Find(...)` inside a handler.** Add a method to the repository.
3. **Overriding `CompanyId` in a test.** Use the override parameter on `AddAsync(document, companyId: "test-tenant")` — do not poke `_companyId`.
4. **Forgetting `includeDeleted: true` when building admin views.** Ghost records.
5. **Constructing a `ChangedByModel` by hand in a handler.** Use `repo.GetChangedByModel()`.

## Related skills

- `02-domain-base-documents-and-multitenancy.md` — the entity side of the contract.
- `06-hotchocolate-graphql-baseline.md` — how `IExecutable<T>` feeds `[UseFiltering]` / `[UseSorting]` / `[UseProjection]`.
- `05-mediatr-and-pipeline-behaviors.md` — how `ValidatorException` from the repository surfaces in GraphQL.
