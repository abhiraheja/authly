namespace Authly.Core.Entities;

/// <summary>
/// Assignment of a <see cref="Role"/> to a <see cref="User"/> within a tenant. Composite primary
/// key (user_id, role_id); <see cref="TenantId"/> is carried for tenant-scoped queries + RLS.
/// Maps to table "user_roles".
/// </summary>
public class UserRole
{
    public Guid UserId { get; set; }
    public Guid RoleId { get; set; }
    public Guid TenantId { get; set; }

    /// <summary>The user who granted this role (null for system-seeded / bootstrap grants).</summary>
    public Guid? GrantedBy { get; set; }

    public DateTimeOffset GrantedAt { get; set; }

    public Role Role { get; set; } = default!;
}
