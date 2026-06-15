namespace Authly.Core.Entities;

/// <summary>
/// Join row mapping a <see cref="Role"/> to a <see cref="Permission"/>. Composite primary key
/// (role_id, permission_id). Maps to table "role_permissions".
/// </summary>
public class RolePermission
{
    public Guid RoleId { get; set; }
    public Guid PermissionId { get; set; }

    public Role Role { get; set; } = default!;
    public Permission Permission { get; set; } = default!;
}
