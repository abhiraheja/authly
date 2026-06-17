namespace Authly.Core.Entities;

/// <summary>
/// A company that groups its projects (<see cref="Tenant"/>s = environments) and holds an
/// employee directory (<see cref="OrganizationMembership"/>s). Global / RLS-exempt — read at
/// login/switch time before any tenant is resolved. Maps to table "organizations".
/// </summary>
public class Organization
{
    public Guid Id { get; set; }

    public string Name { get; set; } = default!;

    /// <summary>URL-safe, globally unique identifier (slugified from the name).</summary>
    public string Slug { get; set; } = default!;

    /// <summary>The account that created/owns the organization. Null for platform-provisioned orgs
    /// (e.g. a legacy super-admin-created tenant) that have no founding account.</summary>
    public Guid? OwnerAccountId { get; set; }

    /// <summary>Org-level settings (JSONB).</summary>
    public string Settings { get; set; } = "{}";

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Account? Owner { get; set; }
    public ICollection<OrganizationMembership> Memberships { get; set; } = new List<OrganizationMembership>();
    public ICollection<Tenant> Tenants { get; set; } = new List<Tenant>();
}
