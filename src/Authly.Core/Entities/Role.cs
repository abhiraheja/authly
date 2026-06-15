namespace Authly.Core.Entities;

/// <summary>
/// A named bundle of permissions within a tenant. System roles (<see cref="IsSystem"/>) are
/// seeded automatically and cannot be deleted or renamed. Maps to table "roles".
/// </summary>
public class Role
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public string Name { get; set; } = default!;

    public string? Description { get; set; }

    /// <summary>System roles are seeded by the platform and are protected from edit/delete.</summary>
    public bool IsSystem { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}
