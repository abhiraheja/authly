namespace Authly.Core.Entities;

/// <summary>
/// Join row mapping an <see cref="OperatorRole"/> to an <see cref="OperatorPermission"/>. Composite
/// primary key (operator_role_id, operator_permission_id). Maps to table "operator_role_permissions".
/// </summary>
public class OperatorRolePermission
{
    public Guid OperatorRoleId { get; set; }
    public Guid OperatorPermissionId { get; set; }

    public OperatorRole OperatorRole { get; set; } = default!;
    public OperatorPermission OperatorPermission { get; set; } = default!;
}
