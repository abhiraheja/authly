namespace Authly.Core.Entities;

/// <summary>
/// An immutable record that a user granted (or withdrew) consent for a specific purpose at a
/// point in time — the audit trail GDPR/DPDP require. Tenant-scoped. Maps to
/// table "consent_records".
/// </summary>
public class ConsentRecord
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }

    /// <summary>What the consent is for, e.g. "terms_of_service", "privacy_policy", "marketing".</summary>
    public string Purpose { get; set; } = default!;

    /// <summary>True if granted, false if explicitly withdrawn.</summary>
    public bool Granted { get; set; }

    /// <summary>The version of the document/policy consented to (so re-consent can be required on change).</summary>
    public string? Version { get; set; }

    /// <summary>IP the consent was captured from (evidence of the act). Not used to identify beyond that.</summary>
    public string? IpAddress { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
