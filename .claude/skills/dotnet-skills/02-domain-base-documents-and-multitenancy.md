---
name: Domain base documents & multi-tenancy
description: Base document contracts (DocumentBase, CompanyDocumentBase), the audit model, and the multi-tenancy invariant that every Saarvix service enforces by default.
type: skill-section
---

# Domain base documents & multi-tenancy

## When to use

Every Mongo-backed entity in a Saarvix service inherits from one of two base classes. Pick based on whether the record is tenant-scoped:

- **Per-tenant (almost everything)** → `CompanyDocumentBase`
- **Cross-tenant / system-wide** (rare: reference data, platform config, global audit) → `DocumentBase`

## The base hierarchy

```
IDocumentBase
    ▲
DocumentBase              (adds: [BsonId] Id, audit, soft delete)
    ▲
CompanyDocumentBase       (adds: CompanyId — the tenant discriminator)
    ▲
{YourEntity}              (your concrete document)
```

## `IDocumentBase` — the minimal contract

```csharp
using {ServiceName}.Domain.Individuals;

namespace {ServiceName}.Domain.Base;

public interface IDocumentBase
{
    string Id { get; set; }
    ChangedByModel? CreatedBy { get; set; }
    ChangedByModel? UpdatedBy { get; set; }
    bool IsDeleted { get; set; }
}
```

**Why an interface at all?** So pipeline behaviors, generic filters, and HotChocolate input types can talk to "any document" without knowing the concrete type.

## `DocumentBase` — the universal base

```csharp
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using {ServiceName}.Domain.Individuals;

namespace {ServiceName}.Domain.Base;

public abstract class DocumentBase : IDocumentBase
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public required string Id { get; set; }

    [BsonElement("createdBy")]
    public ChangedByModel? CreatedBy { get; set; }

    [BsonElement("updatedBy")]
    public ChangedByModel? UpdatedBy { get; set; }

    [BsonElement("isDeleted")]
    public bool IsDeleted { get; set; }
}
```

**Key decisions encoded here:**

- `Id` is a **string** whose on-the-wire representation is `ObjectId`. This lets GraphQL expose it as a `String` scalar while Mongo stores it as a native 12-byte ObjectId (indexed, sortable by creation time).
- `required` forces explicit initialization. The repository generates the id via `ObjectId.GenerateNewId().ToString()` and the only other legitimate source is a deterministic migration.
- Soft delete via `IsDeleted` is the default. Hard delete is an opt-in (`HardDeleteAsync`) intended for GDPR-style scrubs and test fixtures only.
- Audit is on the document itself, not a separate audit collection. The trade-off (large docs, no full history) is accepted because most Saarvix use-cases care about "who last changed this" not "give me the diff at T-12 days".

## `CompanyDocumentBase` — the tenant-scoped base

```csharp
using MongoDB.Bson.Serialization.Attributes;

namespace {ServiceName}.Domain.Base;

public class CompanyDocumentBase : DocumentBase
{
    [BsonElement("companyId")]
    public required string CompanyId { get; set; }
}
```

That's the entire added contract: one `CompanyId`. Do not leak more tenant state (subscription tier, plan, region) into the base — those belong on a separate `Company` aggregate that tenant-scoped documents can look up when they need it.

## `ChangedByModel` — the audit stamp

```csharp
using MongoDB.Bson.Serialization.Attributes;

namespace {ServiceName}.Domain.Individuals;

public class ChangedByModel
{
    [BsonElement("changedById")]
    public required string ChangedById { get; set; }

    [BsonElement("changedByName")]
    public required string ChangedByName { get; set; }

    [BsonElement("changedDate")]
    public DateTimeOffset ChangedDate { get; set; }

    [BsonElement("changedByEmail")]
    public required string ChangedByEmail { get; set; }
}
```

This model is populated by the repository base from the `HttpContext.User` claims automatically. Handlers never construct it by hand except for test seeds or system-initiated operations (where you use `"SYSTEM"` / `"system@{service-name}.saarvix"`).

## `[DocumentName("collection-name")]` — the collection attribute

Collection names are declared on the entity class itself, not centrally. This keeps the mapping local to the file you're reading.

```csharp
namespace {ServiceName}.Domain.Helper;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class DocumentNameAttribute : Attribute
{
    public string Name { get; }
    public DocumentNameAttribute(string name) => Name = name;
}
```

Usage on an entity:

```csharp
using MongoDB.Bson.Serialization.Attributes;
using {ServiceName}.Domain.Base;
using {ServiceName}.Domain.Helper;

namespace {ServiceName}.Domain.{Module};

[DocumentName("invoices")]
public class Invoice : CompanyDocumentBase
{
    [BsonElement("amount")]
    public required decimal Amount { get; set; }

    [BsonElement("currency")]
    public required string Currency { get; set; }

    [BsonElement("status")]
    public required InvoiceStatus Status { get; set; }
}
```

**Collection-name conventions:**

- Lowercase, plural, hyphen-separated if multi-word: `invoices`, `whatsapp-connections`, `email-templates`.
- Must match across all services that read the same collection.
- Never compute the name from the type name at runtime — always set it explicitly via the attribute.

## The multi-tenancy invariant (non-negotiable)

> **Every read or write that touches a `CompanyDocumentBase` subtype MUST be filtered by `CompanyId`.**

This invariant is enforced in one place: `CompanyRepositoryBase<TDocument>` in Infrastructure. See `04-repository-base-pattern.md` for the implementation. The base:

1. Pulls `CompanyId` from `HttpContext.User` in the constructor (via the `UserDetails()` extension from `Saar_Packages.Models.Helpers`).
2. Injects `x => x.CompanyId == effectiveCompanyId` into every `GetAll`, `GetById`, `Find`, `UpdateAsync`, `DeleteAsync`, `HardDeleteAsync`, `AsExecutable` call.
3. Exposes an opt-out (`_ignoreCompanyFilter = true;`) that is one-shot — it resets itself after a single call via a `try/finally`. Use only for cross-tenant system queries (super-admin lists, analytics).
4. Exposes an override parameter (`companyId: "..."`) that substitutes `effectiveCompanyId` for the current-user's id — use only for system-initiated operations where the "acting" tenant differs from the "target" tenant.

**Never enforce this in a handler.** Every time you write `x => x.CompanyId == _currentUser.CompanyId` inside a command handler, you've introduced a vector where the next handler forgets to do it.

## System-level exceptions to the invariant

These are the **only** legitimate cases for `_ignoreCompanyFilter = true`:

- A cross-tenant maintenance job (scheduled index rebuild, GDPR purge).
- A super-admin query authorized by `[RequireSubRole("super_admin")]`.
- A system-to-system notification handler where the tenant is supplied by the message envelope, not the HTTP user.

In all three cases, **add a code comment explaining why**, because the grep-ability of `_ignoreCompanyFilter` is part of the security story.

## GraphQL exposure

`CompanyId` should not be writeable by clients. Expose it read-only (or not at all) on GraphQL types. The repository sets it from server-side claims; never trust a client-supplied value.

```csharp
// In Application/GraphqlTypes/InvoiceType.cs
public class InvoiceType : ObjectType<Invoice>
{
    protected override void Configure(IObjectTypeDescriptor<Invoice> descriptor)
    {
        descriptor.Field(x => x.CompanyId).Ignore();       // hide tenant discriminator entirely
        descriptor.Field(x => x.IsDeleted).Ignore();       // internal state
    }
}
```

## When NOT to follow this pattern

- A reference-data collection shared across tenants (e.g. currencies, countries, industry codes) belongs on `DocumentBase`, not `CompanyDocumentBase`.
- An audit/event-log collection that stores cross-tenant events (cross-service activity stream) may store `CompanyId` as a field but skip `CompanyDocumentBase` if the queries are rarely per-tenant.
- Ephemeral / cache-like state (TTL-indexed sessions, OTP attempts) can use `DocumentBase` with a scoped index and skip the audit stamps by using a different base class — but be explicit in a comment.

## Related skills

- `03-mongo-setup-and-bsonclassmap.md` — how `[DocumentName]` classes get their `BsonClassMap` registered at startup.
- `04-repository-base-pattern.md` — where the tenant invariant is implemented.
- `07-five-dimensional-authorization.md` — the `[CompanyDetailsRequired]` descriptor that guarantees a tenant context before resolvers run.
