namespace Authly.Core.Entities;

/// <summary>
/// A named bundle of operator permissions within an <see cref="Organization"/>. The console-operator
/// counterpart of <see cref="Role"/> — org-scoped (NOT tenant-scoped), and entirely separate from
/// end-user RBAC. System roles (<see cref="IsSystem"/>) are seeded per org and protected from
/// edit/delete. Maps to table "operator_roles".
/// </summary>
public class OperatorRole
{
    public Guid Id { get; set; }

    public Guid OrganizationId { get; set; }

    public string Name { get; set; } = default!;

    public string? Description { get; set; }

    /// <summary>System roles are seeded by the platform and are protected from edit/delete.</summary>
    public bool IsSystem { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public ICollection<OperatorRolePermission> RolePermissions { get; set; } = new List<OperatorRolePermission>();
}
