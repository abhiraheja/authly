namespace Authly.Core.Entities;

/// <summary>
/// Immutable, append-only audit record. Written on every state-changing operation.
/// Maps to table "audit_logs". No UPDATE or DELETE is permitted in app code.
/// </summary>
public class AuditLog
{
    public Guid Id { get; set; }

    /// <summary>Null for platform-level (super-admin / system) events.</summary>
    public Guid? TenantId { get; set; }

    public Guid? ActorId { get; set; }

    /// <summary>user | service | system | super_admin</summary>
    public string? ActorType { get; set; }

    /// <summary>e.g. tenant.created, tenant.suspended, role.assigned</summary>
    public string Event { get; set; } = default!;

    public string? ResourceType { get; set; }
    public Guid? ResourceId { get; set; }

    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }

    /// <summary>success | failure</summary>
    public string Result { get; set; } = "success";

    /// <summary>Arbitrary structured context (JSONB).</summary>
    public string Metadata { get; set; } = "{}";

    public DateTimeOffset CreatedAt { get; set; }
}
