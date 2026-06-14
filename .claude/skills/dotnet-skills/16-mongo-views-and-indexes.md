# MongoDB Views and Indexes

## Views

### Where they live
- **Interface**: `{ServiceName}.Application/Views/Base/IMongoView.cs`
- **Implementations**: `{ServiceName}.Application/Views/{Entity}View.cs`
- Views live in the **Application** layer, not Infrastructure.

### Canonical view template

```csharp
using MongoDB.Bson;
using MongoDB.Driver;
using {ServiceName}.Application.Views.Base;

namespace {ServiceName}.Application.Views;

public class {Entity}View(IMongoDatabase database) : IMongoView
{
    private const string ViewName         = "{entity}_view";       // snake_case
    private const string SourceCollection = "{collection-name}";   // matches DocumentNameAttribute

    public void CreateView()
    {
        // Always drop and recreate so view definition stays in sync with code.
        var existing = database.ListCollectionNames().ToList();
        if (existing.Contains(ViewName))
            database.DropCollection(ViewName);

        var pipeline = new EmptyPipelineDefinition<BsonDocument>()
            .AppendStage<BsonDocument, BsonDocument, BsonDocument>(new BsonDocument("$addFields", new BsonDocument
            {
                { "fts", BuildFtsField() }
            }));

        database.CreateView(ViewName, SourceCollection, pipeline);
    }

    private static BsonDocument BuildFtsField()
    {
        var candidates = new BsonArray
        {
            new BsonDocument("$ifNull", new BsonArray { "$field1", "" }),
            new BsonDocument("$ifNull", new BsonArray { "$field2", "" }),
            // add all searchable string fields — nullable or not
        };

        var nonEmpty = new BsonDocument("$filter", new BsonDocument
        {
            { "input", candidates },
            { "as",    "t" },
            { "cond",  new BsonDocument("$gt", new BsonArray { new BsonDocument("$strLenCP", "$$t"), 0 }) }
        });

        return new BsonDocument("$reduce", new BsonDocument
        {
            { "input",        nonEmpty },
            { "initialValue", "" },
            { "in", new BsonDocument("$cond", new BsonArray
                {
                    new BsonDocument("$eq",  new BsonArray { "$$value", "" }),
                    "$$this",
                    new BsonDocument("$concat", new BsonArray { "$$value", " ", "$$this" })
                })
            }
        });
    }
}
```

### Rules
1. **Always include an `fts` field** — every view must have a full-text search computed field. No exceptions.
2. `fts` is a space-joined concatenation of all searchable string tokens with null/empty values stripped via `$reduce` + `$filter`.
3. Include every searchable string field in `candidates`, using `$ifNull` to coerce nulls to `""`.
4. Do **not** include enum fields directly — they are stored as integers. Convert with `$toString` only if full-text search on the enum label is needed.
5. `ViewName` is always `snake_case`. `SourceCollection` must match the `[DocumentName("...")]` attribute on the document.
6. Discovery and instantiation is handled automatically by `MongoStartupInitializer` via reflection — no manual registration needed.

---

## View Documents

### Where they live
- `{ServiceName}.Domain/ViewDocuments/{Entity}ViewDocument.cs`
- Namespace: `{ServiceName}.Domain.ViewDocuments`

### Rules
1. **Always inherit `CompanyDocumentBase`** if the entity belongs to a company (i.e., has `companyId`). `companyId` is the most important identifier in the system — tenant scoping depends on it.
2. **Always include `fts`** as the last property with `[BsonElement("fts")]`. It mirrors the computed field added by the view pipeline.
3. Mirror every field from the source document with the same `[BsonElement("...")]` names.
4. `[DocumentName("...")]` must match the `ViewName` constant in the corresponding `IMongoView` implementation (snake_case).

### Canonical template

```csharp
using MongoDB.Bson.Serialization.Attributes;
using {ServiceName}.Domain.Base;
using {ServiceName}.Domain.Helper;

namespace {ServiceName}.Domain.ViewDocuments;

[DocumentName("{entity}_view")]
public class {Entity}ViewDocument : CompanyDocumentBase
{
    // mirror all fields from {Entity}Document with identical [BsonElement] names
    [BsonElement("field1")]
    public string Field1 { get; set; } = string.Empty;

    // fts is always last
    [BsonElement("fts")]
    public string Fts { get; set; } = string.Empty;
}
```

---

## View Repositories & Interfaces

### Where they live
- **Interface**: `{ServiceName}.Application/Interfaces/ViewInterfaces/{Entity}ViewRepository.cs`
  - Namespace: `{ServiceName}.Application.Interfaces.ViewInterfaces`
- **Repository**: `{ServiceName}.Infrastructure/Repositories/ViewRepositories/{Entity}ViewRepository.cs`
  - Namespace: `{ServiceName}.Infrastructure.Repositories.ViewRepositories`

### Inheritance rules
| View document base | Interface inherits | Repository inherits |
|---|---|---|
| `CompanyDocumentBase` | `ICompanyRepositoryBase<{Entity}ViewDocument>` | `CompanyRepositoryBase<{Entity}ViewDocument>` |
| `DocumentBase` | `IRepositoryBase<{Entity}ViewDocument>` | `RepositoryBase<{Entity}ViewDocument>` |

### Interface template

```csharp
using {ServiceName}.Application.Interfaces.Base;
using {ServiceName}.Domain.ViewDocuments;

namespace {ServiceName}.Application.Interfaces.ViewInterfaces;

public interface I{Entity}ViewRepository : ICompanyRepositoryBase<{Entity}ViewDocument> { }
```

### Repository template

```csharp
using Microsoft.AspNetCore.Http;
using MongoDB.Driver;
using {ServiceName}.Application.Interfaces.ViewInterfaces;
using {ServiceName}.Domain.ViewDocuments;
using {ServiceName}.Infrastructure.Repositories.Base;

namespace {ServiceName}.Infrastructure.Repositories.ViewRepositories;

public class {Entity}ViewRepository : CompanyRepositoryBase<{Entity}ViewDocument>, I{Entity}ViewRepository
{
    public {Entity}ViewRepository(IMongoDatabase db, IHttpContextAccessor http)
        : base(db, http) { }
}
```

### Registration
No manual registration needed. `InfrastructureInstaller` auto-scans for all types subclassing `RepositoryBase<>` or `CompanyRepositoryBase<>` and registers them against any interface whose namespace contains `.Application.Interfaces`. The `ViewInterfaces` sub-namespace satisfies this filter automatically.

---

## Indexes

### Where they live
- **Interface**: `{ServiceName}.Application/Indexes/Base/IIndex.cs`
- **Implementations**: `{ServiceName}.Application/Indexes/{Entity}Index.cs`
- Both interface and implementations live in the **Application** layer — same as views.

### Canonical index template

```csharp
using MongoDB.Driver;
using {ServiceName}.Application.Indexes.Base;
using {ServiceName}.Domain.Documents;
using {ServiceName}.Domain.Helper;

namespace {ServiceName}.Application.Indexes;

public class {Entity}Index(IMongoDatabase db) : IIndex
{
    public void CreateIndexes()
    {
        // Always derive the collection name from DocumentNameAttribute — never hardcode.
        var collectionName = typeof({Entity}Document).GetCustomAttribute<DocumentNameAttribute>()?.Name
            ?? typeof({Entity}Document).Name;

        var collection = db.GetCollection<{Entity}Document>(collectionName);

        // Partial unique: safe for soft-delete — excludes IsDeleted=true documents.
        collection.Indexes.CreateOne(
            new CreateIndexModel<{Entity}Document>(
                Builders<{Entity}Document>.IndexKeys
                    .Ascending(x => x.CompanyId)
                    .Ascending(x => x.{UniqueField}),
                new CreateIndexOptions
                {
                    Unique = true,
                    Name = "uidx_company_{uniqueField}_active",
                    PartialFilterExpression = Builders<{Entity}Document>.Filter.Eq(x => x.IsDeleted, false)
                }));

        // Covers GetAll / tenant-scoped list queries.
        collection.Indexes.CreateOne(
            new CreateIndexModel<{Entity}Document>(
                Builders<{Entity}Document>.IndexKeys
                    .Ascending(x => x.CompanyId)
                    .Ascending(x => x.IsDeleted),
                new CreateIndexOptions { Name = "idx_company_isDeleted" }));
    }
}
```

### Rules
1. **Always derive the collection name via `DocumentNameAttribute` reflection** — never hardcode the string.
2. **Unique constraints on business fields must be partial** with `PartialFilterExpression = Filter.Eq(x => x.IsDeleted, false)`. A plain unique index breaks soft-delete: you cannot reuse a name after deletion.
3. **Always index `(companyId, isDeleted)`** — every `CompanyRepositoryBase<T>` query filters on both. Without this index, all tenant-scoped queries do a full collection scan.
4. Only add additional indexes for fields actually queried (checked against handlers). Do not add indexes for fields that are only read after retrieval.
5. Discovery is handled by `MongoStartupInitializer` via reflection — no DI registration needed.

---

## Query Handlers

### Rules
1. **Queries always use the view repository interface** (`I{Entity}ViewRepository`), never the write repository.
2. **Always return `IExecutable<TDto>`** — never a plain list or a single object.
3. **Always include `Fts`** in the projected DTO — it must be returned every time.
4. **Never return the whole view document** — always project via `AsExecutable` selector into a DTO.
5. **DTO lives in `{ServiceName}.Models`** — not in Application or Domain.
6. Build an optional `predicate` when the query has filter parameters (e.g. filter by Id, status). The `CompanyId` tenant filter is applied automatically by `CompanyRepositoryBase` — do not add it to the predicate.

### List query template

```csharp
public class List{Entity}QueryHandler(I{Entity}ViewRepository viewRepository)
    : IRequestHandler<List{Entity}Query, IExecutable<{Entity}Dto>>
{
    public Task<IExecutable<{Entity}Dto>> Handle(List{Entity}Query request, CancellationToken ct)
        => Task.FromResult(viewRepository.AsExecutable(selector: v => new {Entity}Dto
        {
            Id    = v.Id,
            // ... all fields ...
            Fts   = v.Fts   // always last
        }));
}
```

### Get-by-id / filtered query template

```csharp
public class Get{Entity}ByIdQueryHandler(I{Entity}ViewRepository viewRepository)
    : IRequestHandler<Get{Entity}ByIdQuery, IExecutable<{Entity}Dto>>
{
    public Task<IExecutable<{Entity}Dto>> Handle(Get{Entity}ByIdQuery request, CancellationToken ct)
        => Task.FromResult(viewRepository.AsExecutable(
            selector: v => new {Entity}Dto
            {
                Id  = v.Id,
                // ... all fields ...
                Fts = v.Fts
            },
            predicate: v => v.Id == request.Id));
}
```

### Optional predicate pattern (when filter param may be absent)

```csharp
Expression<Func<{Entity}ViewDocument, bool>>? predicate = null;
if (!string.IsNullOrWhiteSpace(request.SomeFilter))
    predicate = v => v.SomeField == request.SomeFilter;

return Task.FromResult(viewRepository.AsExecutable(
    selector: v => new {Entity}Dto { ... },
    predicate: predicate));
```

---

## Startup wiring (reference)

Both views and indexes are discovered and initialized automatically at startup:

```
Program.cs
  app.UseMongoStartupInitializer()
    → InitializeIndexes()  — scans assemblies for IIndex, calls CreateIndexes()
    → InitializeViews()    — scans assemblies for IMongoView, calls CreateView()
```

`ActivatorUtilities.CreateInstance` is used as fallback if the type is not registered in DI, so neither views nor index classes need to be explicitly registered — just implement the interface.
