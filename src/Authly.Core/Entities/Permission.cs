namespace Authly.Core.Entities;

/// <summary>
/// A fine-grained, tenant-scoped capability expressed as <c>resource.action</c>
/// (e.g. <c>user.read</c>). Roles are mapped to permissions; tokens carry the flattened
/// permission set. Maps to table "permissions".
/// </summary>
public class Permission
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    /// <summary>The resource the permission applies to (e.g. <c>user</c>, <c>application</c>).</summary>
    public string Resource { get; set; } = default!;

    /// <summary>The action allowed on the resource (e.g. <c>read</c>, <c>write</c>, <c>delete</c>).</summary>
    public string Action { get; set; } = default!;

    public string? Description { get; set; }

    /// <summary>The canonical <c>resource.action</c> form used in tokens and permission checks.</summary>
    public string Name => $"{Resource}.{Action}";
}
