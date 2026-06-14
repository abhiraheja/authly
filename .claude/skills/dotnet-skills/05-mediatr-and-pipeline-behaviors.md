---
name: MediatR & pipeline behaviors
description: How Saarvix wires MediatR (via the Saar_Packages.MediatR package), the two always-on pipeline behaviors (validation + unhandled exception), and the ValidatorException : GraphQLException error model.
type: skill-section
---

# MediatR & pipeline behaviors

## When to use

Every business operation in a Saarvix service goes through MediatR. Query handlers read via repositories; command handlers validate, mutate via repositories, and publish events via producers.

## Package — `Saar_Packages.MediatR`

Saarvix does **not** use the open-source MediatR package directly. The shared `Saar_Packages.MediatR` wraps it and re-exports:

- `IRequest<TResponse>`, `IRequestHandler<TRequest, TResponse>` (or equivalent)
- `INotification`, `INotificationHandler<T>`
- `IPipelineBehavior<TRequest, TResponse>`
- `IMediator`
- Extension method: **`IServiceCollection.AddMediatr(Assembly)`** — note the intentionally lowercase `r`
- Namespace split: `Saar_Packages.MediatR` for the extension method; `Saar_Packages.MediatR.Interfaces` for the core interfaces

This central package is the only approved MediatR entry point. Do not reference `MediatR` directly; referencing only the wrapper keeps cross-service behavior consistent.

## Registration in `ApplicationInstaller`

```csharp
using FluentValidation;
using HotChocolate.Data.Filters;
using HotChocolate.Data.Sorting;
using Mapster;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Saar_Packages.MediatR;
using Saar_Packages.MediatR.Interfaces;
using {ServiceName}.Application.Behavior;

namespace {ServiceName}.Application;

// Marker type so the assembly can be referenced cheaply from elsewhere
public class Application { }

public static class ApplicationInstaller
{
    public static IServiceCollection AddApplication(
        this IServiceCollection services,
        HotChocolate.Execution.Configuration.IRequestExecutorBuilder graphqlBuilder)
    {
        var assembly = typeof(Application).Assembly;

        // --- GraphQL DataLoaders / IObjectType ---
        foreach (var t in assembly.GetTypes()
                     .Where(x => x.IsAssignableTo(typeof(IDataLoader)) && !x.IsInterface))
            services.TryAddScoped(t);

        foreach (var t in assembly.GetTypes()
                     .Where(x => x.IsAssignableTo(typeof(IObjectType)) && !x.IsInterface))
            graphqlBuilder.AddType(t);

        // --- Filter/Sort input types — walk the base chain, not just direct base ---
        var filterOrSortTypes = assembly.GetTypes()
            .Where(t => !t.IsAbstract && !t.IsGenericTypeDefinition)
            .Where(t =>
            {
                var b = t.BaseType;
                while (b is not null && b != typeof(object))
                {
                    if (b.IsGenericType)
                    {
                        var def = b.GetGenericTypeDefinition();
                        if (def == typeof(FilterInputType<>) || def == typeof(SortInputType<>))
                            return true;
                    }
                    b = b.BaseType;
                }
                return t.GetInterfaces().Any(i => i.IsGenericType &&
                    (i.GetGenericTypeDefinition() == typeof(FilterInputType<>) ||
                     i.GetGenericTypeDefinition() == typeof(SortInputType<>)));
            });

        foreach (var t in filterOrSortTypes)
            graphqlBuilder.AddType(t);

        // --- Mapster + FluentValidation + MediatR ---
        services.AddMapster();
        services.AddValidatorsFromAssembly(assembly);
        services.AddMediatr(assembly);                                  // ⚠ lowercase 'r'

        // --- Pipeline behaviors (order matters: outer catches inner exceptions) ---
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(UnhandledExceptionBehavior<,>));
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

        return services;
    }
}
```

**Pipeline order matters.** Registration order is execution order: `UnhandledExceptionBehavior` wraps `ValidationBehavior` wraps the handler. So validation failures inside a handler are caught by `UnhandledExceptionBehavior` and rethrown as `ValidatorException`.

## `ValidatorException` — the GraphQL-native error

Saarvix throws a single exception type for all business/validation failures. It extends HotChocolate's `GraphQLException`, so the framework serialises it into the GraphQL `errors` array automatically.

```csharp
using FluentValidation.Results;
using HotChocolate;

namespace {ServiceName}.Application.Exceptions;

public class ValidatorException : GraphQLException
{
    private const string DefaultMessage = "One or more validation failure have occurred";

    public string[]? GraphqlErrors { get; }

    private ValidatorException() : base(DefaultMessage)
        => GraphqlErrors = new[] { DefaultMessage };

    public ValidatorException(string message) : base(message)
        => GraphqlErrors = new[] { message };

    public ValidatorException(Exception ex) : base(ex.Message)
        => GraphqlErrors = new[] { ex.Message };

    public ValidatorException(string message, Exception inner) : base(message, inner)
        => GraphqlErrors = new[] { message };

    public ValidatorException(params string[] message) : base(string.Join(",", message))
        => GraphqlErrors = message;

    public ValidatorException(IEnumerable<ValidationFailure> failures)
        : this(string.Join(", ", failures.Select(f => f.ErrorMessage).Distinct()))
    {
        GraphqlErrors = failures.Select(f => f.ErrorMessage).Distinct().ToArray();
    }
}
```

**Rules:**

- Handlers, repositories, and producers **all** throw `ValidatorException` for any user-visible failure.
- Unexpected exceptions are caught by `UnhandledExceptionBehavior` and converted into a `ValidatorException(ex)` so the client gets a deterministic error shape — but the original exception is logged with `{@Request}` so we can debug server-side.
- Do not throw domain-specific exception types. Saarvix's contract is "one error type, multiple messages in `GraphqlErrors`".

## `ValidationBehavior<TRequest, TResponse>`

```csharp
using FluentValidation;
using Saar_Packages.MediatR.Interfaces;
using {ServiceName}.Application.Exceptions;

namespace {ServiceName}.Application.Behavior;

public class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
        => _validators = validators;

    public async Task<TResponse> Handle(
        TRequest request,
        CancellationToken ct,
        RequestHandlerDelegate<TResponse> next)
    {
        if (_validators.Any())
        {
            var context = new ValidationContext<TRequest>(request);
            var results = await Task.WhenAll(_validators.Select(v => v.ValidateAsync(context, ct)));
            var failures = results.SelectMany(r => r.Errors).Where(e => e is not null).ToList();
            if (failures.Count > 0) throw new ValidatorException(failures);
        }
        return await next(ct);
    }
}
```

**Behavior notes:**

- Validators are resolved per-request type via `IEnumerable<IValidator<TRequest>>`. If none are registered, the behavior is a pass-through.
- Validators run in parallel with `Task.WhenAll` — only use synchronous / thread-safe validators (which is the FluentValidation default).
- Failures are deduped inside `ValidatorException` to avoid repeating "Name is required" four times when four fields are missing.

## `UnhandledExceptionBehavior<TRequest, TResponse>`

```csharp
using Microsoft.Extensions.Logging;
using Saar_Packages.MediatR.Interfaces;
using {ServiceName}.Application.Exceptions;

namespace {ServiceName}.Application.Behavior;

public class UnhandledExceptionBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly ILogger<UnhandledExceptionBehavior<TRequest, TResponse>> _logger;

    public UnhandledExceptionBehavior(ILogger<UnhandledExceptionBehavior<TRequest, TResponse>> logger)
        => _logger = logger;

    public async Task<TResponse> Handle(
        TRequest request,
        CancellationToken ct,
        RequestHandlerDelegate<TResponse> next)
    {
        try
        {
            return await next(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Unhandled exception for request {RequestName} with request data: {@Request}",
                typeof(TRequest).Name, request);
            throw new ValidatorException(ex);
        }
    }
}
```

**Logging caveat:** `{@Request}` serialises the request with its values. If the request contains secrets (passwords, tokens, OTPs), wrap that field with `[JsonIgnore]` or use a scrubbing logger enricher. Never let an OTP hit production logs.

## Request / handler skeleton

```csharp
using Saar_Packages.MediatR.Interfaces;

namespace {ServiceName}.Application.{Module}.Commands;

public sealed record Create{Entity}Command(
    string Name,
    decimal Amount) : IRequest<Create{Entity}Response>;

public sealed record Create{Entity}Response(string Id);
```

```csharp
using FluentValidation;

public sealed class Create{Entity}Validator : AbstractValidator<Create{Entity}Command>
{
    public Create{Entity}Validator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Amount).GreaterThan(0);
    }
}
```

```csharp
using Saar_Packages.MediatR.Interfaces;
using {ServiceName}.Application.Interfaces.{Module};
using {ServiceName}.Domain.{Module};

public sealed class Create{Entity}Handler
    : IRequestHandler<Create{Entity}Command, Create{Entity}Response>
{
    private readonly I{Entity}Repository _repo;

    public Create{Entity}Handler(I{Entity}Repository repo) => _repo = repo;

    public async Task<Create{Entity}Response> Handle(
        Create{Entity}Command request,
        CancellationToken ct)
    {
        var entity = new {Entity}
        {
            Id        = _repo.GenerateId(),
            CompanyId = _repo.GetCompanyId(),   // redundant with repo auto-set, but makes intent explicit
            Name      = request.Name,
            Amount    = request.Amount,
        };

        await _repo.AddAsync(entity, ct: ct);
        return new Create{Entity}Response(entity.Id);
    }
}
```

## Invoking MediatR from a GraphQL resolver

```csharp
using HotChocolate.Types;
using Saar_Packages.MediatR.Interfaces;
using {ServiceName}.Api.Authorization.Attributes;
using {ServiceName}.Api.Graphql.Base;
using {ServiceName}.Application.{Module}.Commands;

namespace {ServiceName}.Api.Graphql.{Module};

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

Notice that **the resolver contains no business logic** — it's authorization + `mediator.Send`. This is the canonical slice shape.

## Notifications (publish / subscribe inside the process)

Notifications carry multiple handlers and return void. They're the glue between bounded contexts and between the service bus consumer and application handlers.

```csharp
public sealed record InvoicePaidNotification(string InvoiceId, decimal Amount, string CompanyId) : INotification;

public sealed class SendPaymentReceiptHandler : INotificationHandler<InvoicePaidNotification> { ... }
public sealed class UpdateLedgerHandler       : INotificationHandler<InvoicePaidNotification> { ... }
```

Handlers run sequentially in registration order under the default MediatR dispatcher. If you need parallel fan-out, publish with a custom publisher strategy or break them into separate notifications.

## When NOT to follow this pattern

- A truly trivial read (e.g. "fetch single entity by id") can skip MediatR and call the repository directly from a resolver — but be consistent per module. Mixing "most queries go through MediatR, but this one doesn't" inside the same module is confusing.
- Ultra-hot paths (bulk ingest) may benefit from bypassing the pipeline. Measure first; the overhead of two behaviors is negligible vs Mongo round-trips.

## Common mistakes

1. **Using MediatR's own `MediatR` namespace / `AddMediatR` (uppercase R).** Use `Saar_Packages.MediatR` and the lowercase-r extension method.
2. **Catching exceptions inside a handler and returning an error object.** Let the exception flow. `ValidationBehavior` / `UnhandledExceptionBehavior` will convert it.
3. **Logging the request with secrets.** Scrub first or mark fields with `[JsonIgnore]`.
4. **Registering a third pipeline behavior ad-hoc.** All behaviors belong in `{ServiceName}.Application/Behavior/` and get registered in `ApplicationInstaller` to keep ordering deterministic.

## Related skills

- `04-repository-base-pattern.md` — where `ValidatorException` is thrown from.
- `06-hotchocolate-graphql-baseline.md` — how `GraphQLException` subclasses become GraphQL errors.
- `13-shared-saar-packages.md` — what `Saar_Packages.MediatR` exposes.
