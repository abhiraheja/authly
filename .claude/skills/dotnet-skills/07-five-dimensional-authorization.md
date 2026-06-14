---
name: Five-dimensional authorization
description: The UserDetails / CompanyDetails / ApplicationType / Permission / SubRole authorization model, its requirements, handlers, dynamic IAuthorizationPolicyProvider, and HotChocolate descriptor attributes that wire them into GraphQL fields.
type: skill-section
---

# Five-dimensional authorization

## When to use

This is the authorization model for every Saarvix GraphQL service. All five dimensions are orthogonal — you combine them on a resolver as needed. Never read claims ad-hoc from `HttpContext.User` inside a resolver; always express access as an attribute.

## The five dimensions

| # | Dimension | Answers the question | Source claim |
|---|---|---|---|
| 1 | **UserDetails** | Is there a valid user identity? | `ClaimTypes.Sid` (non-empty) |
| 2 | **CompanyDetails** | Is there a valid tenant context? | `ClaimsConstants.CompanyId` (non-empty) |
| 3 | **ApplicationType** | Is the JWT issued for the correct SaaS product? | `ClaimsConstants.ApplicationTypeId` (matches `ApplicationTypeConstant` DB id) |
| 4 | **Permission** | Does the user have the module-level action permission? | `ClaimsConstants.Permissions` (space-separated) |
| 5 | **SubRole** | Does the user hold an allowed sub-role? | `ClaimsConstants.SubRole` |

Claim-name constants live in `Saar_Packages.Models.Constants.ClaimsConstants` so every Saarvix service speaks the same claim dictionary.

## Policy names & the dynamic provider

Two policies are **static** and registered explicitly:

- `UserDetailsRequired`
- `CompanyDetailsRequired`

Three are **dynamic** and constructed on first use from the policy name itself:

- `ApplicationType:{name}` — e.g. `ApplicationType:clinic`
- `Permission:{module}.{action}` — e.g. `Permission:Invoices.Read`
- `SubRole:{csv-of-roles}` — e.g. `SubRole:admin,manager`

The dynamic policies are resolved by a custom `IAuthorizationPolicyProvider`. This is the engine that makes `[RequirePermission("Invoices.Manage")]` work without having to `AddPolicy` each one by hand.

## `AuthPolicies` — the name constants

```csharp
namespace {ServiceName}.Api.Authorization;

public static class AuthPolicies
{
    public const string UserDetailsRequired    = "UserDetailsRequired";
    public const string CompanyDetailsRequired = "CompanyDetailsRequired";

    public const string ApplicationTypePrefix  = "ApplicationType";
    public const string PermissionPrefix       = "Permission";
    public const string SubRolePrefix          = "SubRole";
}
```

## Requirements — empty marker types

Each dimension has an `IAuthorizationRequirement`. For the static policies, the requirement has no state; for the dynamic ones, it carries the parsed argument.

```csharp
using Microsoft.AspNetCore.Authorization;

namespace {ServiceName}.Api.Authorization.Requirements;

public sealed class UserDetailsRequirement    : IAuthorizationRequirement { }
public sealed class CompanyDetailsRequirement : IAuthorizationRequirement { }

public sealed class ApplicationTypeRequirement(string applicationType) : IAuthorizationRequirement
{
    public string ApplicationType { get; } = applicationType;
}

public sealed class PermissionRequirement(string permissionCode) : IAuthorizationRequirement
{
    public string PermissionCode { get; } = permissionCode;
}

public sealed class SubRoleRequirement(string[] allowedSubRoles) : IAuthorizationRequirement
{
    public string[] AllowedSubRoles { get; } = allowedSubRoles;
}
```

## Handlers — one per requirement

Handlers are **singleton** and read claims from `HttpContext.User`. They must not capture scoped services.

### `UserDetailsAuthorizationHandler`

```csharp
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using {ServiceName}.Api.Authorization.Requirements;

namespace {ServiceName}.Api.Authorization.Handlers;

public sealed class UserDetailsAuthorizationHandler
    : AuthorizationHandler<UserDetailsRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        UserDetailsRequirement requirement)
    {
        var userId = context.User.FindFirstValue(ClaimTypes.Sid);

        if (!string.IsNullOrWhiteSpace(userId))
            context.Succeed(requirement);
        else
            context.Fail(new AuthorizationFailureReason(this, "User identity not found in token."));

        return Task.CompletedTask;
    }
}
```

### `CompanyDetailsAuthorizationHandler`

```csharp
using Microsoft.AspNetCore.Authorization;
using Saar_Packages.Models.Constants;
using {ServiceName}.Api.Authorization.Requirements;

public sealed class CompanyDetailsAuthorizationHandler
    : AuthorizationHandler<CompanyDetailsRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        CompanyDetailsRequirement requirement)
    {
        var companyId = context.User.FindFirst(ClaimsConstants.CompanyId)?.Value;

        if (!string.IsNullOrWhiteSpace(companyId))
            context.Succeed(requirement);
        else
            context.Fail(new AuthorizationFailureReason(this, "Company context not found in token."));

        return Task.CompletedTask;
    }
}
```

### `ApplicationTypeAuthorizationHandler`

```csharp
using Microsoft.AspNetCore.Authorization;
using Saar_Packages.Models.Constants;
using {ServiceName}.Api.Authorization.Requirements;

public sealed class ApplicationTypeAuthorizationHandler
    : AuthorizationHandler<ApplicationTypeRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ApplicationTypeRequirement requirement)
    {
        var jwtApplicationId = context.User.FindFirst(ClaimsConstants.ApplicationTypeId)?.Value;

        // ApplicationTypeConstant comes from Saar_Packages.Models.Constants —
        // it maps human-readable names ("clinic", "lco", "aura") to DB application ids.
        var requiredApp = ApplicationTypeConstant.ToArray()
            .FirstOrDefault(x => x.ApplicationName.Equals(requirement.ApplicationType, StringComparison.OrdinalIgnoreCase));

        if (requiredApp is not null && jwtApplicationId == requiredApp.Id)
            context.Succeed(requirement);
        else
            context.Fail(new AuthorizationFailureReason(this,
                $"Token is not issued for the '{requirement.ApplicationType}' application."));

        return Task.CompletedTask;
    }
}
```

### `PermissionAuthorizationHandler`

Supports implicit inheritance: a user with `{Module}.Manage` satisfies any narrower action (`{Module}.Read`, `{Module}.Write`, `{Module}.Delete`). Claim value can hold many permissions space-separated.

```csharp
using Microsoft.AspNetCore.Authorization;
using Saar_Packages.Models.Constants;
using {ServiceName}.Api.Authorization.Requirements;

public sealed class PermissionAuthorizationHandler
    : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        var granted = context.User
            .FindAll(ClaimsConstants.Permissions)
            .SelectMany(c => c.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (HasPermission(granted, requirement.PermissionCode))
            context.Succeed(requirement);
        else
            context.Fail(new AuthorizationFailureReason(this,
                $"Permission '{requirement.PermissionCode}' is required."));

        return Task.CompletedTask;
    }

    private static bool HasPermission(HashSet<string> granted, string required)
    {
        if (granted.Contains(required)) return true;

        var dot = required.IndexOf('.');
        if (dot > 0)
        {
            var module = required[..dot];
            if (granted.Contains($"{module}.Manage")) return true;   // Manage subsumes Read/Write/Delete
        }
        return false;
    }
}
```

### `SubRoleAuthorizationHandler`

```csharp
using Microsoft.AspNetCore.Authorization;
using Saar_Packages.Models.Constants;
using {ServiceName}.Api.Authorization.Requirements;

public sealed class SubRoleAuthorizationHandler
    : AuthorizationHandler<SubRoleRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        SubRoleRequirement requirement)
    {
        var subRole = context.User.FindFirst(ClaimsConstants.SubRole)?.Value;

        if (!string.IsNullOrWhiteSpace(subRole) &&
            requirement.AllowedSubRoles.Contains(subRole, StringComparer.OrdinalIgnoreCase))
        {
            context.Succeed(requirement);
        }
        else
        {
            context.Fail(new AuthorizationFailureReason(this,
                $"Sub-role '{subRole}' is not permitted. Required: {string.Join(", ", requirement.AllowedSubRoles)}."));
        }

        return Task.CompletedTask;
    }
}
```

## `ApplicationTypeAuthorizationPolicyProvider` — the dynamic policy builder

Parses `Prefix:value` policy names and builds an `AuthorizationPolicy` on the fly. Falls through to the default provider for static names.

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using {ServiceName}.Api.Authorization.Requirements;

namespace {ServiceName}.Api.Authorization;

public sealed class ApplicationTypeAuthorizationPolicyProvider(IOptions<AuthorizationOptions> options)
    : DefaultAuthorizationPolicyProvider(options)
{
    public override async Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        var appTypePrefix   = AuthPolicies.ApplicationTypePrefix + ":";
        var permissionPrefix = AuthPolicies.PermissionPrefix     + ":";
        var subRolePrefix   = AuthPolicies.SubRolePrefix         + ":";

        if (policyName.StartsWith(appTypePrefix, StringComparison.OrdinalIgnoreCase))
        {
            return new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .AddRequirements(new ApplicationTypeRequirement(policyName[appTypePrefix.Length..]))
                .Build();
        }

        if (policyName.StartsWith(permissionPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .AddRequirements(new PermissionRequirement(policyName[permissionPrefix.Length..]))
                .Build();
        }

        if (policyName.StartsWith(subRolePrefix, StringComparison.OrdinalIgnoreCase))
        {
            var allowed = policyName[subRolePrefix.Length..]
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .AddRequirements(new SubRoleRequirement(allowed))
                .Build();
        }

        return await base.GetPolicyAsync(policyName);
    }
}
```

## `AuthorizationInstaller` — the DI wiring

```csharp
using Microsoft.AspNetCore.Authorization;
using {ServiceName}.Api.Authorization;
using {ServiceName}.Api.Authorization.Handlers;
using {ServiceName}.Api.Authorization.Requirements;

namespace {ServiceName}.Api.Installers;

public static class AuthorizationInstaller
{
    public static IServiceCollection Add{ServiceName}Authorization(this IServiceCollection services)
    {
        services.AddAuthorization(options =>
        {
            options.AddPolicy(AuthPolicies.UserDetailsRequired,    p => p.RequireAuthenticatedUser()
                                                                         .AddRequirements(new UserDetailsRequirement()));
            options.AddPolicy(AuthPolicies.CompanyDetailsRequired, p => p.RequireAuthenticatedUser()
                                                                         .AddRequirements(new CompanyDetailsRequirement()));
            // Dynamic prefixes handled by the custom provider below — do not register per-name here.
        });

        services.AddSingleton<IAuthorizationPolicyProvider, ApplicationTypeAuthorizationPolicyProvider>();

        services.AddSingleton<IAuthorizationHandler, UserDetailsAuthorizationHandler>();
        services.AddSingleton<IAuthorizationHandler, CompanyDetailsAuthorizationHandler>();
        services.AddSingleton<IAuthorizationHandler, ApplicationTypeAuthorizationHandler>();
        services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
        services.AddSingleton<IAuthorizationHandler, SubRoleAuthorizationHandler>();

        return services;
    }
}
```

**Do not also call `services.AddAuthorization()` from `AuthInstaller`.** There must be exactly one call to `AddAuthorization(options => ...)` or the options builder state gets clobbered depending on registration order.

## HotChocolate descriptor attributes — the clean API

Each dimension has a descriptor attribute that writes `.Authorize(<policy>)` on the field/type descriptor. Use these on resolvers — never query `HttpContext.User` yourself.

### `[UserDetailsRequired]` / `[CompanyDetailsRequired]`

```csharp
using System.Reflection;
using HotChocolate.Types;
using HotChocolate.Types.Descriptors;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class CompanyDetailsRequiredAttribute : DescriptorAttribute
{
    protected override void TryConfigure(
        IDescriptorContext context,
        IDescriptor descriptor,
        ICustomAttributeProvider element)
    {
        switch (descriptor)
        {
            case IObjectFieldDescriptor field: field.Authorize(AuthPolicies.CompanyDetailsRequired); break;
            case IObjectTypeDescriptor  type:  type.Authorize(AuthPolicies.CompanyDetailsRequired);  break;
        }
    }
}
```

`UserDetailsRequiredAttribute` is structurally identical, just targets the `UserDetailsRequired` policy.

### `[ApplicationTypeRequired("clinic")]`

```csharp
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class ApplicationTypeRequiredAttribute(string applicationType) : DescriptorAttribute
{
    protected override void TryConfigure(
        IDescriptorContext context,
        IDescriptor descriptor,
        ICustomAttributeProvider element)
    {
        var policy = $"{AuthPolicies.ApplicationTypePrefix}:{applicationType}";
        switch (descriptor)
        {
            case IObjectFieldDescriptor f: f.Authorize(policy); break;
            case IObjectTypeDescriptor  t: t.Authorize(policy); break;
        }
    }
}
```

### `[RequirePermission("Invoices.Manage")]`

```csharp
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
public sealed class RequirePermissionAttribute(string permissionCode) : DescriptorAttribute
{
    protected override void TryConfigure(
        IDescriptorContext context,
        IDescriptor descriptor,
        ICustomAttributeProvider element)
    {
        var policy = $"{AuthPolicies.PermissionPrefix}:{permissionCode}";
        switch (descriptor)
        {
            case IObjectFieldDescriptor f: f.Authorize(policy); break;
            case IObjectTypeDescriptor  t: t.Authorize(policy); break;
        }
    }
}
```

`AllowMultiple = true` — you can stack multiple permission attributes to require ALL of them.

### `[RequireSubRole("admin","manager")]`

```csharp
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
public sealed class RequireSubRoleAttribute(params string[] subRoles) : DescriptorAttribute
{
    protected override void TryConfigure(
        IDescriptorContext context,
        IDescriptor descriptor,
        ICustomAttributeProvider element)
    {
        var policy = $"{AuthPolicies.SubRolePrefix}:{string.Join(',', subRoles)}";
        switch (descriptor)
        {
            case IObjectFieldDescriptor f: f.Authorize(policy); break;
            case IObjectTypeDescriptor  t: t.Authorize(policy); break;
        }
    }
}
```

## Usage patterns

### Tenant-scoped resolver (most common)

```csharp
[CompanyDetailsRequired]
[RequirePermission("Invoices.Read")]
public IExecutable<Invoice> GetInvoices([Service] IInvoiceRepository repo) => repo.GetAll();
```

### Application-specific mutation

```csharp
[ApplicationTypeRequired(ApplicationTypeConstant.Clinic)]
[RequirePermission("Appointments.Manage")]
public Task<...> CreateAppointment(...) => ...;
```

### Multiple permissions required (AND)

```csharp
[RequirePermission("Billing.Read")]
[RequirePermission("Patients.Read")]
public Task<PatientBill> GetPatientBill(...) => ...;
```

### Sub-role gate (for admin / super-admin views)

```csharp
[CompanyDetailsRequired]
[RequireSubRole("admin", "manager")]
public IExecutable<User> GetAllUsers([Service] IUserRepository repo) => repo.GetAll();
```

### Type-level authorization (apply to all fields of a type)

```csharp
[CompanyDetailsRequired]
[ExtendObjectType(nameof(Query))]
public class InvoiceQueries : QueryBase
{
    [RequirePermission("Invoices.Read")]
    public IExecutable<Invoice> GetInvoices(...) => ...;

    [RequirePermission("Invoices.Manage")]
    public Task<Invoice?> GetInvoiceStatus(...) => ...;
}
```

## Why this is better than ad-hoc claim checks

- **Centralized policy names** — grep `RequirePermission(` to audit what the API exposes.
- **Schema-driven** — HotChocolate decorates the SDL, so clients see which fields require which permissions via error messages.
- **Consistent failure shape** — all denials are `GraphQL authorization` errors with deterministic codes.
- **Testable independently** — handlers are singletons with no DI complexity; unit-testable.

## Security invariants — do not violate

1. **Authorization handlers must read ONLY from the JWT identity** — never from headers, cookies, or request body.
2. **`HeaderRequestInterceptor` must not inject claims.** See file 06 for the safe shape.
3. **Never pass claim values through to business logic from a resolver** (e.g. `companyId` as a query argument). The repository reads the claim itself.
4. **`Manage` subsumes narrower actions — design your claim set accordingly.** Granting `{Module}.Manage` is a deliberate escalation.

## When NOT to follow this pattern

- Public endpoints (login, health, webhook verification) skip all five dimensions and rely on rate limiting + signed-request verification. They must be explicitly opted out via `[AllowAnonymous]` or an unauthenticated route.
- Service-to-service calls typically skip UserDetails/CompanyDetails and use an ApplicationTypeRequired + ApiKey scheme instead.

## Common mistakes

1. **Reading `context.User.FindFirst(...)` inside a resolver.** Use a descriptor attribute.
2. **Typo in policy name.** No compile-time check — favor the string constants via the descriptor attributes.
3. **Adding `[Authorize(Policy = "...")]` from `Microsoft.AspNetCore.Authorization` to a GraphQL resolver.** Use HotChocolate's `[Authorize]` (or our descriptor attributes) — the MVC attribute does not participate in the GraphQL pipeline.
4. **Registering handlers as scoped.** Must be singleton; they hold no per-request state.
5. **Adding a sixth dimension without discussion.** If you need "Region" or "Environment" gating, extend the policy-provider prefix pattern — do not inline a new style.

## Related skills

- `06-hotchocolate-graphql-baseline.md` — where the descriptor attributes attach.
- `08-jwt-authentication-dual-scheme.md` — how claims get into `HttpContext.User` in the first place.
- `13-shared-saar-packages.md` — the `ClaimsConstants` and `ApplicationTypeConstant` sources.
