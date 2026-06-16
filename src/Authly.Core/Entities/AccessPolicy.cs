namespace Authly.Core.Entities;

/// <summary>
/// An attribute-based access-control (ABAC) policy. Evaluated by the policy decision point against
/// a request's subject/resource/environment attributes. Tenant-scoped (RLS). The combining rule is
/// deny-overrides: any matching Deny wins; otherwise a matching Allow permits; otherwise default deny.
/// </summary>
public class AccessPolicy
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }

    public string Name { get; set; } = default!;
    public string? Description { get; set; }

    /// <summary>"allow" or "deny".</summary>
    public string Effect { get; set; } = "allow";

    /// <summary>Action this policy targets — exact ("document.read"), prefix ("document.*"), or "*".</summary>
    public string Action { get; set; } = "*";

    /// <summary>Resource type this policy targets — exact, prefix ("doc*"), or "*".</summary>
    public string ResourceType { get; set; } = "*";

    /// <summary>JSON array of conditions (attribute/operator/value); all must hold for the policy to match.</summary>
    public string Conditions { get; set; } = "[]";

    /// <summary>Higher priority wins among competing Allow policies (Deny always overrides).</summary>
    public int Priority { get; set; }

    public bool Enabled { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
}
