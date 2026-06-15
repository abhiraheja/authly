namespace Authly.Modules.Common;

/// <summary>
/// Identifies who performed an operation and from where, so module services can write
/// audit entries without depending on the web/HTTP layer. Built at the controller boundary.
/// </summary>
/// <param name="ActorId">The acting principal's id (super admin / user), if known.</param>
/// <param name="ActorType">user | service | system | super_admin</param>
public sealed record AuditContext(
    Guid? ActorId,
    string ActorType,
    string? IpAddress = null,
    string? UserAgent = null)
{
    public static readonly AuditContext System = new(null, "system");
}
