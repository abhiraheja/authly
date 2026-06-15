namespace Authly.Modules.Tenants;

/// <summary>Input for creating a tenant. Slug is optional — derived from name when blank.</summary>
public sealed record CreateTenantRequest(string Name, string? Slug = null);

/// <summary>Raised when a requested slug is already taken.</summary>
public sealed class SlugAlreadyExistsException(string slug)
    : Exception($"A tenant with slug '{slug}' already exists.")
{
    public string Slug { get; } = slug;
}
