namespace Authly.Core.Entities;

/// <summary>
/// A registered self-hosted deployment, tracked on the cloud control plane only (§4.14). Holds
/// the hash of the issued sync key plus the latest aggregate telemetry the instance pushes —
/// <b>never any PII, user records, tokens, or secrets</b>. Not tenant-scoped (platform-level,
/// like super_admins); it is owned by the tenant that registered it. Maps to
/// table "self_hosted_instances".
/// </summary>
public class SelfHostedInstance
{
    public Guid Id { get; set; }

    /// <summary>The cloud tenant that registered this instance (who it belongs to).</summary>
    public Guid? OwnerTenantId { get; set; }

    /// <summary>A human label so the operator can tell instances apart.</summary>
    public string? Name { get; set; }

    /// <summary>SHA-256 hash of the issued sync key. The raw key is shown once and never stored.</summary>
    public string SyncKeyHash { get; set; } = default!;

    public string? Version { get; set; }
    public DateTimeOffset? LastSeenAt { get; set; }

    // --- aggregate metrics ONLY — never PII ---
    public int UserCount { get; set; }
    public int AppCount { get; set; }
    public int TenantCount { get; set; }

    /// <summary>Free-form aggregate health blob (status, db reachability). JSONB. Never PII.</summary>
    public string Health { get; set; } = "{}";

    public DateTimeOffset CreatedAt { get; set; }
}
