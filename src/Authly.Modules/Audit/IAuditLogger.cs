using Authly.Modules.Common;

namespace Authly.Modules.Audit;

/// <summary>
/// Writes immutable audit entries. Called from every state-changing operation.
/// </summary>
public interface IAuditLogger
{
    Task LogAsync(
        string @event,
        AuditContext actor,
        Guid? tenantId = null,
        string? resourceType = null,
        Guid? resourceId = null,
        string result = "success",
        object? metadata = null,
        CancellationToken ct = default);
}
