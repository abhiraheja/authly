using Authly.Modules.Common;

namespace Authly.Modules.Audit;

/// <summary>
/// Writes immutable audit entries. Called from every state-changing operation.
/// </summary>
public interface IAuditLogger
{
    /// <param name="publishEvent">
    /// When false the audit row is still written but the event is NOT fanned out to webhook
    /// subscribers. Use for bulk imports/migrations that provision downstream state themselves and
    /// must not trigger lifecycle webhooks (e.g. an importer creating users without firing
    /// <c>user.created</c>).
    /// </param>
    Task LogAsync(
        string @event,
        AuditContext actor,
        Guid? tenantId = null,
        string? resourceType = null,
        Guid? resourceId = null,
        string result = "success",
        object? metadata = null,
        bool publishEvent = true,
        CancellationToken ct = default);
}
