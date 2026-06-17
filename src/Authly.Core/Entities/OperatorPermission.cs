namespace Authly.Core.Entities;

/// <summary>
/// A fine-grained, org-scoped console capability expressed as <c>resource.action</c>
/// (e.g. <c>client.manage</c>). The console-operator counterpart of <see cref="Permission"/>,
/// separate from end-user RBAC. Maps to table "operator_permissions".
/// </summary>
public class OperatorPermission
{
    public Guid Id { get; set; }

    public Guid OrganizationId { get; set; }

    public string Resource { get; set; } = default!;

    public string Action { get; set; } = default!;

    public string? Description { get; set; }

    /// <summary>The canonical <c>resource.action</c> form used in permission checks.</summary>
    public string Name => $"{Resource}.{Action}";
}
