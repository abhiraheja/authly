using Saarvix.Identity.Core.Enums;

namespace Saarvix.Identity.Core.Entities;

/// <summary>
/// An organization that signs up to the platform. Root of tenant isolation —
/// every tenant-scoped record carries this tenant's id. Maps to table "tenants".
/// </summary>
public class Tenant
{
    public Guid Id { get; set; }

    /// <summary>URL-safe unique identifier (e.g. "acme").</summary>
    public string Slug { get; set; } = default!;

    public string Name { get; set; } = default!;

    public TenantStatus Status { get; set; } = TenantStatus.Active;

    /// <summary>Parent tenant for sub-organizations (nested divisions/branches).</summary>
    public Guid? ParentId { get; set; }

    /// <summary>Feature flags and policies (JSONB).</summary>
    public string Settings { get; set; } = "{}";

    /// <summary>Logo url, colors, fonts, layout (JSONB).</summary>
    public string Branding { get; set; } = "{}";

    /// <summary>e.g. auth.theircompany.com</summary>
    public string? CustomDomain { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Tenant? Parent { get; set; }
    public ICollection<User> Users { get; set; } = new List<User>();
}
